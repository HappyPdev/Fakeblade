using System.Collections;
using System.Collections.Generic;
using FakeBlade.UI;
using TMPro;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Escena Sandbox (GDD 6.3, Quehaceres B y E): campo de pruebas con dummies y un panel que se
    /// abre con su propio botón (Tab / Select, reasignable) y pausa el juego.
    ///
    /// Panel principal con secciones (cada una en su subpanel; Atrás vuelve al principal):
    /// - Rivales: de 0 a 3 dummies (cambiar el número reinicia la partida al cerrar el panel),
    ///   su comportamiento y su intervalo (al momento, SandboxSettings).
    /// - Mi peonza (E1): punta, cuerpo, disco y núcleo de un jugador, en caliente.
    /// - Trucos (E2): RPM infinitas, especial lleno, cargas infinitas, dash sin espera e
    ///   invulnerable, cada uno para nadie, los jugadores, los dummies o todos.
    /// - Reiniciar todo (E3): posiciones, RPM, cargas, energía y estados de todas las peonzas.
    /// El panel lo navega cualquiera (EventSystem, como la pausa). Al cerrarlo no hay cuenta atrás.
    ///
    /// Va en el mismo objeto que BattleBootstrap, que crea la arena, los jugadores y la cámara.
    /// </summary>
    [RequireComponent(typeof(BattleBootstrap))]
    public class SandboxController : MonoBehaviour
    {
        private const int PanelWidthPx = 160;
        private const int PartRowHeight = 13;
        private const int CanvasOrder = 520;
        /// <summary>Invulnerable por truco: se renueva cada frame, así que dura poco al quitarlo.</summary>
        private const float CheatInvulnerableSeconds = 0.2f;

        private static readonly ComponentSlot[] PartSlots =
            { ComponentSlot.Tip, ComponentSlot.Body, ComponentSlot.Blade, ComponentSlot.Core };
        private static readonly string[] PartKeys = { "SLOT_TIP", "SLOT_BODY", "SLOT_BLADE", "SLOT_CORE" };

        private BattleBootstrap _bootstrap;
        private readonly List<PlayerController> _dummies = new List<PlayerController>(3);
        private readonly List<PlayerController> _humans = new List<PlayerController>(4);

        private HUDTheme _theme;
        private int _px;
        private GameObject _dim;
        private TextMeshProUGUI _hint;

        // Panel principal y subpaneles
        private PixelMenuList _main;
        private PixelMenuList _rivalsPanel;
        private PixelMenuList _bladePanel;
        private PixelMenuList _cheatsPanel;
        private PixelMenuList _current;

        private PixelOptionRow _rivalsRow;
        private PixelOptionRow _behaviourRow;
        private PixelOptionRow _intervalRow;
        private PixelOptionRow _aiLevelRow;
        private PixelOptionRow _aiProfileRow;
        private PixelOptionRow _dummyPresetRow;
        private PixelOptionRow _dummyCoreRow;
        private TextMeshProUGUI _maxHint;

        private PixelOptionRow _playerRow;
        private readonly PixelOptionRow[] _partRows = new PixelOptionRow[4];
        private int _bladePlayer;

        private PixelOptionRow _spinCheat, _specialCheat, _chargesCheat, _dashCheat, _invulnerableCheat;

        private PixelMenuList _debugPanel;
        private PixelOptionRow _parryDebug, _velocityDebug, _damageDebug, _statusDebug, _fpsDebug, _logDebug;
        private SandboxDebugView _debug;
        private SandboxRecorder _recorder;
        private PixelOptionRow _recordDebug;

        private PixelOptionRow _speedRow;
        private bool _stepping;

        private PixelMenuList _tuningPanel;
        private PixelMenuList _effectsPanel;
        private PixelOptionRow _impactRow;
        private PixelOptionRow _impactIntervalRow;
        private float _impactTimer;
        private PixelOptionRow _arenaRow;
        private readonly PixelOptionRow[] _tuningRows = new PixelOptionRow[Tunables.Length];
        private int _pendingArena;
        private bool _reloading;

        private bool _open;
        private int _pendingDummies;

        public bool IsPanelOpen => _open;

        private int MaxDummies
        {
            get
            {
                var gm = GameManager.Instance;
                int max = gm != null ? gm.MaxPlayers : 4;
                return Mathf.Max(0, max - MatchSetup.Players.Count);
            }
        }

        #region Lifecycle
        private void Awake()
        {
            _bootstrap = GetComponent<BattleBootstrap>();
        }

        private void Start()
        {
            // BattleBootstrap (orden -200) ya ha creado la arena, los jugadores y la cámara
            SandboxSettings.DummyCount = Mathf.Clamp(SandboxSettings.DummyCount, 0, MaxDummies);
            SetDummyCount(SandboxSettings.DummyCount);
            SetupTunedConfig();
            GameTime.SetNormalScale(SandboxSettings.Speed, false);
            BuildUI();
            _debug = SandboxDebugView.Create(transform, _theme, CanvasOrder - 10);
            _recorder = SandboxRecorder.Create(transform, p => _dummies.Contains(p));

            InputBindings.OnChanged += RefreshHint;
            Loc.OnLanguageChanged += RefreshHint;
        }

        private void OnDestroy()
        {
            InputBindings.OnChanged -= RefreshHint;
            Loc.OnLanguageChanged -= RefreshHint;
            if (_open) MenuStack.Pop();

            // Al salir del sandbox (no al recargarlo para cambiar de arena) se pierde lo temporal
            if (!_reloading)
            {
                if (SandboxSettings.OriginalConfig != null) CombatConfig.SetActive(SandboxSettings.OriginalConfig);
                SandboxSettings.ResetSession();
                GameTime.SetNormalScale(1f, false);
            }
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || _main == null) return;

            // GameManager (orden -100) ya ha mirado la pausa este frame: Esc/Start no se pisan
            if (_open)
            {
                if (MenuInput.AnySandboxPanelPressed()) ClosePanel();
                else if (MenuInput.AnyBackPressed()) Back();
            }
            else if (gm.CurrentState == GameManager.GameState.InMatch && !MenuStack.IsSubmenuOpen && MenuInput.AnySandboxPanelPressed())
            {
                OpenPanel();
            }

            bool showHint = !_open && (gm.CurrentState == GameManager.GameState.InMatch || gm.CurrentState == GameManager.GameState.Countdown);
            if (_hint.enabled != showHint) _hint.enabled = showHint;

            UpdateFrameStep(gm);
            ApplyCheats(gm);
            UpdateImpactPreview(gm);
        }
        #endregion

        #region Panel
        private void OpenPanel()
        {
            _open = true;
            MenuStack.Push();
            GameManager.Instance.SetPanelPause(true);

            _pendingDummies = _dummies.Count;
            _pendingArena = _bootstrap.Catalog.arenas.IndexOf(CurrentArena);
            RefreshAll();
            _dim.SetActive(true);
            ShowPanel(_main);
        }

        private void ClosePanel()
        {
            if (!_open) return;
            _open = false;
            _current?.Hide();
            _current = null;
            _dim.SetActive(false);
            MenuStack.Pop();

            // Otra arena: se recarga la escena con ella (lo del sandbox se mantiene)
            if (_pendingArena >= 0 && _pendingArena < _bootstrap.Catalog.arenas.Count &&
                _bootstrap.Catalog.arenas[_pendingArena] != CurrentArena)
            {
                SandboxSettings.DummyCount = _pendingDummies;
                MatchSetup.Arena = _bootstrap.Catalog.arenas[_pendingArena];
                _reloading = true;
                SceneFlow.Load(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                return;
            }

            var gm = GameManager.Instance;
            if (_pendingDummies != _dummies.Count)
            {
                SetDummyCount(_pendingDummies);
                StartCoroutine(RestartWhenRegistered(gm));
            }
            else
            {
                gm.SetPanelPause(false);
            }
        }

        /// <summary>Atrás: de un subpanel al principal; desde el principal, cierra.</summary>
        private void Back()
        {
            if (_current != null && _current != _main) ShowPanel(_main);
            else ClosePanel();
        }

        private void ShowPanel(PixelMenuList panel)
        {
            if (_current != null && _current != panel) _current.Hide();
            _current = panel;
            RefreshAll();
            panel.Show();
        }

        /// <summary>Los dummies nuevos se registran en su Start: se espera a que estén para reiniciar.</summary>
        private static IEnumerator RestartWhenRegistered(GameManager gm)
        {
            yield return null;
            yield return null;
            if (gm != null) gm.RestartMatch();
        }

        private void RefreshAll()
        {
            RefreshRivals();
            RefreshBlade();
            RefreshCheats();
            RefreshDebug();
            RefreshTuning();
            RefreshSpeed();
            RefreshEffects();
        }

        #region Efectos (O5)
        private void RefreshEffects()
        {
            var labels = new string[SandboxSettings.ImpactPreviews.Length];
            labels[0] = Loc.Get("NO");
            for (int i = 1; i < labels.Length; i++)
            {
                var (kind, damage) = SandboxSettings.ImpactPreviews[i];
                labels[i] = kind == VfxSystem.ImpactKind.Hit ? Loc.Format("IMPACT_HIT", damage)
                    : kind == VfxSystem.ImpactKind.Charged ? Loc.Format("IMPACT_CHARGED", damage)
                    : kind == VfxSystem.ImpactKind.Parry ? Loc.Get("IMPACT_PARRY") : Loc.Get("IMPACT_BLOCK");
            }
            _impactRow.SetOptions(labels, SandboxSettings.ImpactPreview);

            char separator = Loc.Current == Language.English ? '.' : ',';
            var intervals = new string[SandboxSettings.Intervals.Length];
            for (int i = 0; i < intervals.Length; i++)
                intervals[i] = SandboxSettings.Intervals[i].ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                    .Replace('.', separator) + " S";
            _impactIntervalRow.SetOptions(intervals, SandboxSettings.ImpactIntervalIndex);
        }

        /// <summary>Con el panel cerrado, repite el impacto elegido junto a la peonza del jugador.</summary>
        private void UpdateImpactPreview(GameManager gm)
        {
            int preview = SandboxSettings.ImpactPreview;
            if (_open || preview <= 0 || preview >= SandboxSettings.ImpactPreviews.Length ||
                gm.CurrentState != GameManager.GameState.InMatch) return;

            _impactTimer -= Time.deltaTime;
            if (_impactTimer > 0f) return;
            _impactTimer = SandboxSettings.Intervals[Mathf.Clamp(SandboxSettings.ImpactIntervalIndex, 0, SandboxSettings.Intervals.Length - 1)];

            PlayerController player = BladePlayer;
            if (player == null || player.Blade == null) return;
            var (kind, damage) = SandboxSettings.ImpactPreviews[preview];
            // Al lado de la peonza, para ver el impacto y la peonza a la vez
            // El parry y el bloqueo, con el color de la peonza del jugador (como si parase él)
            VfxSystem.PlayImpact(kind, player.Blade.Position + Vector3.right * 1.2f, damage, player.PlayerColor);
        }
        #endregion
        #endregion

        #region Debug (E4, E5)
        private void RefreshDebug()
        {
            string[] yesNo = { Loc.Get("NO"), Loc.Get("YES") };
            _parryDebug.SetOptions(yesNo, SandboxSettings.ShowParryWindow ? 1 : 0);
            _velocityDebug.SetOptions(yesNo, SandboxSettings.ShowVelocity ? 1 : 0);
            _damageDebug.SetOptions(yesNo, SandboxSettings.ShowDamageNumbers ? 1 : 0);
            _statusDebug.SetOptions(yesNo, SandboxSettings.ShowStatus ? 1 : 0);
            _fpsDebug.SetOptions(yesNo, SandboxSettings.ShowFps ? 1 : 0);
            _logDebug.SetOptions(yesNo, SandboxSettings.ShowEventLog ? 1 : 0);
            _recordDebug.SetOptions(yesNo, SandboxSettings.RecordData ? 1 : 0);
        }
        #endregion

        #region Tiempo (E6)
        private void RefreshSpeed()
        {
            var options = new string[SandboxSettings.Speeds.Length];
            for (int i = 0; i < options.Length; i++)
            {
                float speed = SandboxSettings.Speeds[i];
                options[i] = speed <= 0f ? Loc.Get("SPEED_PAUSE") : "X" + Decimal(speed, "0.##");
            }
            _speedRow.SetOptions(options, SandboxSettings.SpeedIndex);
        }

        private void SetSpeed(int index)
        {
            SandboxSettings.SpeedIndex = index;
            // Con el panel abierto el juego está parado: se aplica al cerrarlo
            GameTime.SetNormalScale(SandboxSettings.Speed, false);
            RefreshHint();
        }

        /// <summary>
        /// Con la velocidad en PAUSA (y el panel cerrado) el juego está congelado: "." o el clic del
        /// stick derecho lo dejan correr un frame.
        /// </summary>
        private void UpdateFrameStep(GameManager gm)
        {
            if (_stepping)
            {
                _stepping = false;
                if (Time.timeScale > 0f) Time.timeScale = GameTime.NormalScale;
                return;
            }
            if (_open || gm.CurrentState != GameManager.GameState.InMatch || GameTime.NormalScale > 0f) return;
            if (!StepPressed()) return;

            _stepping = true;
            Time.timeScale = 1f;
        }

        private static bool StepPressed()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.periodKey.wasPressedThisFrame) return true;
            var pads = UnityEngine.InputSystem.Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
                if (pads[i].rightStickButton.wasPressedThisFrame) return true;
            return false;
        }
        #endregion

        #region Ajustes (E7)
        /// <summary>Valor de CombatConfig que se puede retocar: se multiplica por uno de TuningFactors.</summary>
        private struct Tunable
        {
            public string Key;
            public System.Func<CombatConfig, float> Get;
            public System.Action<CombatConfig, float> Set;
            public System.Func<float, string> Format;
        }

        private static readonly Tunable[] Tunables =
        {
            new Tunable { Key = "TUNE_GLOBAL_DAMAGE", Get = c => c.damageMultiplier, Set = (c, v) => c.damageMultiplier = v, Format = v => "X" + Decimal(v, "0.##") },
            new Tunable { Key = "TUNE_CHARGE_DAMAGE", Get = c => c.chargedDamagePerLevel, Set = (c, v) => c.chargedDamagePerLevel = v, Format = v => "+" + Decimal(v * 100f, "0.#") + "%" },
            new Tunable { Key = "TUNE_PARRY", Get = c => c.parryWindow, Set = (c, v) => c.parryWindow = v, Format = v => Decimal(v, "0.00") + " S" },
            new Tunable { Key = "TUNE_ATTACK_COST", Get = c => c.quickAttackSpinCostPct, Set = (c, v) => c.quickAttackSpinCostPct = v, Format = v => Decimal(v * 100f, "0.#") + "%" },
            new Tunable { Key = "TUNE_DASH_COST", Get = c => c.dashSpinCostPct, Set = (c, v) => c.dashSpinCostPct = v, Format = v => Decimal(v * 100f, "0.#") + "%" },
            new Tunable { Key = "TUNE_ATTACK_HIT", Get = c => c.attackHitDamageMultiplier, Set = (c, v) => c.attackHitDamageMultiplier = v, Format = v => "X" + Decimal(v, "0.##") },
            new Tunable { Key = "TUNE_DASH_HIT", Get = c => c.dashHitDamageMultiplier, Set = (c, v) => c.dashHitDamageMultiplier = v, Format = v => "X" + Decimal(v, "0.##") },
            new Tunable { Key = "TUNE_DAMAGE", Get = c => c.hitBaseDamage, Set = (c, v) => c.hitBaseDamage = v, Format = v => Decimal(v, "0.#") },
            new Tunable { Key = "TUNE_KNOCKBACK", Get = c => c.knockbackBase, Set = (c, v) => c.knockbackBase = v, Format = v => Decimal(v, "0.#") },
            new Tunable { Key = "TUNE_ENERGY", Get = c => c.specialEnergyMultiplier, Set = (c, v) => c.specialEnergyMultiplier = v, Format = v => "X" + Decimal(v, "0.##") },
        };

        private ArenaData CurrentArena
        {
            get
            {
                var arenas = _bootstrap.Catalog.arenas;
                return MatchSetup.Arena != null ? MatchSetup.Arena : (arenas.Count > 0 ? arenas[0] : null);
            }
        }

        /// <summary>El sandbox juega con una copia de CombatConfig: los retoques no tocan el asset.</summary>
        private static void SetupTunedConfig()
        {
            if (SandboxSettings.TunedConfig == null)
            {
                SandboxSettings.OriginalConfig = CombatConfig.Active;
                SandboxSettings.TunedConfig = Instantiate(SandboxSettings.OriginalConfig);
                SandboxSettings.TunedConfig.name = SandboxSettings.OriginalConfig.name + " (Sandbox)";
                // Que sobreviva a recargar la escena (cambio de arena)
                SandboxSettings.TunedConfig.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            ApplyTuning();
            CombatConfig.SetActive(SandboxSettings.TunedConfig);
        }

        private static void ApplyTuning()
        {
            CombatConfig original = SandboxSettings.OriginalConfig, tuned = SandboxSettings.TunedConfig;
            if (original == null || tuned == null) return;
            for (int i = 0; i < Tunables.Length; i++)
                Tunables[i].Set(tuned, Tunables[i].Get(original) * SandboxSettings.TuningFactors[SandboxSettings.Tuning[i]]);
        }

        private void RefreshTuning()
        {
            var arenas = _bootstrap.Catalog.arenas;
            var arenaNames = new string[Mathf.Max(1, arenas.Count)];
            for (int i = 0; i < arenas.Count; i++) arenaNames[i] = arenas[i].DisplayName;
            if (arenas.Count == 0) arenaNames[0] = "-";
            _arenaRow.SetOptions(arenaNames, Mathf.Max(0, _pendingArena));
            _arenaRow.SetInteractable(arenas.Count > 1);

            CombatConfig original = SandboxSettings.OriginalConfig;
            for (int i = 0; i < Tunables.Length; i++)
            {
                var options = new string[SandboxSettings.TuningFactors.Length];
                for (int f = 0; f < options.Length; f++)
                {
                    float factor = SandboxSettings.TuningFactors[f];
                    string value = original != null ? Tunables[i].Format(Tunables[i].Get(original) * factor) : "-";
                    options[f] = f == SandboxSettings.NeutralFactor ? value : $"{value} (X{Decimal(factor, "0.##")})";
                }
                _tuningRows[i].SetOptions(options, SandboxSettings.Tuning[i]);
            }
        }

        private void SetTuning(int index, int factor)
        {
            SandboxSettings.Tuning[index] = factor;
            ApplyTuning();
        }

        private void ResetTuning()
        {
            for (int i = 0; i < SandboxSettings.Tuning.Length; i++) SandboxSettings.Tuning[i] = SandboxSettings.NeutralFactor;
            ApplyTuning();
            RefreshTuning();
        }

        /// <summary>Decimales con coma en español y con punto en inglés.</summary>
        private static string Decimal(float value, string format)
        {
            string text = value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
            return Loc.Current == Language.English ? text : text.Replace('.', ',');
        }
        #endregion

        #region Rivales
        private void RefreshRivals()
        {
            int max = MaxDummies;
            var rivals = new string[max + 1];
            rivals[0] = Loc.Get("SANDBOX_NONE");
            for (int i = 1; i <= max; i++) rivals[i] = PixelUI.Number(i);
            _rivalsRow.SetOptions(rivals, Mathf.Clamp(_pendingDummies, 0, max));
            _rivalsRow.SetInteractable(max > 0);
            _maxHint.gameObject.SetActive(max == 0);

            _behaviourRow.SetOptions(new[]
            {
                Loc.Get("DUMMY_IDLE"), Loc.Get("DUMMY_MOVE"), Loc.Get("DUMMY_ATTACK"),
                Loc.Get("DUMMY_DASH"), Loc.Get("DUMMY_SPECIAL"), Loc.Get("DUMMY_AI")
            }, (int)SandboxSettings.Behaviour);

            _aiLevelRow.SetOptions(new[]
            {
                Loc.Get("AI_EASY"), Loc.Get("AI_NORMAL"), Loc.Get("AI_HARD"), Loc.Get("AI_EXPERT"), Loc.Get("AI_IMPOSSIBLE")
            }, (int)SandboxSettings.AIDifficulty);

            // Según peonza y los perfiles de Resources/AI/Profiles por orden (D2: un perfil nuevo sale aquí solo)
            IReadOnlyList<AIProfile> profiles = AIProfiles.All;
            var profileNames = new string[profiles.Count + 1];
            profileNames[0] = Loc.Get("AI_PROFILE_AUTO");
            for (int i = 0; i < profiles.Count; i++) profileNames[i + 1] = Loc.Get(profiles[i].nameKey);
            _aiProfileRow.SetOptions(profileNames, Mathf.Clamp(SandboxSettings.AIProfile, 0, profiles.Count));

            List<BladePreset> presets = _bootstrap.Catalog.presets;
            var presetNames = new string[Mathf.Max(1, presets.Count)];
            presetNames[0] = "-";
            for (int i = 0; i < presets.Count; i++) presetNames[i] = Loc.Get(presets[i].nameKey);
            _dummyPresetRow.SetOptions(presetNames, DummyPresetIndex());

            // Núcleo: el del preset o cualquiera del catálogo, con el nombre de su poder
            List<FakeBladeComponentData> cores = _bootstrap.Catalog.GetParts(ComponentSlot.Core);
            var coreNames = new string[cores.Count + 1];
            coreNames[0] = Loc.Get("DUMMY_CORE_PRESET");
            for (int i = 0; i < cores.Count; i++)
            {
                SpecialAbilityData power = SpecialAbilities.Get(cores[i].SpecialAbility);
                coreNames[i + 1] = power != null ? power.DisplayName.ToUpperInvariant() : cores[i].ComponentName.ToUpperInvariant();
            }
            _dummyCoreRow.SetOptions(coreNames, DummyCoreOption(cores.Count));

            // Decimales con coma en español y con punto en inglés
            char separator = Loc.Current == Language.English ? '.' : ',';
            var intervals = new string[SandboxSettings.Intervals.Length];
            for (int i = 0; i < intervals.Length; i++)
            {
                string number = SandboxSettings.Intervals[i].ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                intervals[i] = number.Replace('.', separator) + " S";
            }
            _intervalRow.SetOptions(intervals, SandboxSettings.IntervalIndex);
        }

        private void SetDummyCount(int count)
        {
            count = Mathf.Clamp(count, 0, MaxDummies);
            SandboxSettings.DummyCount = count;

            while (_dummies.Count > count)
            {
                int last = _dummies.Count - 1;
                _bootstrap.Despawn(_dummies[last]);
                _dummies.RemoveAt(last);
            }

            while (_dummies.Count < count)
            {
                int playerIndex = MatchSetup.Players.Count + _dummies.Count;
                PlayerController dummy = _bootstrap.SpawnDummy(playerIndex, _dummies.Count + 1, DummyPresetIndex());
                EquipDummyCore(dummy);
                _dummies.Add(dummy);
            }

            ApplyDummyBrains();
            _bootstrap.RefreshCameraTargets();
        }

        /// <summary>Preset de los dummies en el catálogo (el último, la Balanceada, si no se ha elegido).</summary>
        private int DummyPresetIndex()
        {
            int count = _bootstrap.Catalog.presets.Count;
            if (count == 0) return 0;
            return SandboxSettings.DummyPreset < 0 || SandboxSettings.DummyPreset >= count
                ? count - 1
                : SandboxSettings.DummyPreset;
        }

        /// <summary>Peonza de los dummies (D6): se cambia al momento y la IA en Auto cambia con ella de perfil.</summary>
        private void SetDummyPreset(int index)
        {
            SandboxSettings.DummyPreset = index;
            BladePreset preset = _bootstrap.Catalog.GetPreset(DummyPresetIndex());
            var setup = new PlayerSetup();
            setup.ApplyPreset(preset);
            for (int i = 0; i < _dummies.Count; i++)
            {
                if (_dummies[i] == null) continue;
                setup.EquipOn(_dummies[i].Stats);
                EquipDummyCore(_dummies[i]);
            }
            ApplyDummyBrains();
        }

        /// <summary>Opción elegida en la fila del núcleo (0 = el del preset; i = el núcleo i - 1 del catálogo).</summary>
        private static int DummyCoreOption(int coreCount) =>
            SandboxSettings.DummyCore < 0 || SandboxSettings.DummyCore >= coreCount ? 0 : SandboxSettings.DummyCore + 1;

        /// <summary>Núcleo de los dummies (para probar cada poder contra la IA): se cambia al momento.</summary>
        private void SetDummyCore(int option)
        {
            SandboxSettings.DummyCore = option - 1;
            if (option == 0)
            {
                // Vuelve el núcleo de su preset
                SetDummyPreset(SandboxSettings.DummyPreset);
                return;
            }
            for (int i = 0; i < _dummies.Count; i++) EquipDummyCore(_dummies[i]);
            ApplyDummyBrains();
        }

        /// <summary>Pone el núcleo elegido en el panel (si no es el de su preset).</summary>
        private void EquipDummyCore(PlayerController dummy)
        {
            if (dummy == null) return;
            List<FakeBladeComponentData> cores = _bootstrap.Catalog.GetParts(ComponentSlot.Core);
            if (SandboxSettings.DummyCore < 0 || SandboxSettings.DummyCore >= cores.Count) return;
            dummy.Stats.EquipComponent(cores[SandboxSettings.DummyCore]);
        }

        /// <summary>
        /// Con el comportamiento "IA" los dummies los controla la IA del nivel y el perfil elegidos (D1, D3;
        /// en Auto, el arquetipo de su peonza); con los demás, su SandboxDummyBrain. Se aplica al momento.
        /// </summary>
        private void ApplyDummyBrains()
        {
            bool useAI = SandboxSettings.Behaviour == DummyBehaviour.AI;
            for (int i = 0; i < _dummies.Count; i++)
            {
                PlayerController dummy = _dummies[i];
                if (dummy == null) continue;
                var dummyBrain = dummy.GetComponent<SandboxDummyBrain>();
                AIBrain ai = AIProfiles.Ensure(dummy.gameObject, AIProfiles.Resolve(SandboxSettings.AIProfile, dummy.Stats));

                ai.Difficulty = SandboxSettings.AIDifficulty;
                ai.ClearBuffers();
                ai.enabled = useAI;
                if (dummyBrain != null)
                {
                    dummyBrain.ClearBuffers();
                    dummyBrain.enabled = !useAI;
                }
                dummy.SetInputSource(useAI ? ai : dummyBrain);
            }
        }
        #endregion

        #region Mi peonza (E1)
        /// <summary>Jugadores humanos de la partida, por orden (J1, J2...).</summary>
        private void CollectHumans()
        {
            _humans.Clear();
            var gm = GameManager.Instance;
            if (gm == null) return;
            foreach (PlayerController player in gm.Players)
                if (player != null && !_dummies.Contains(player)) _humans.Add(player);
            _humans.Sort((a, b) => a.PlayerID.CompareTo(b.PlayerID));
        }

        private PlayerController BladePlayer
        {
            get
            {
                if (_humans.Count == 0) return null;
                _bladePlayer = Mathf.Clamp(_bladePlayer, 0, _humans.Count - 1);
                return _humans[_bladePlayer];
            }
        }

        private void RefreshBlade()
        {
            CollectHumans();
            var players = new string[Mathf.Max(1, _humans.Count)];
            for (int i = 0; i < _humans.Count; i++) players[i] = Loc.Format("PLAYER_BADGE", _humans[i].PlayerID + 1);
            if (_humans.Count == 0) players[0] = "-";
            PlayerController player = BladePlayer;
            _playerRow.SetOptions(players, _bladePlayer);
            // Con un solo jugador no hace falta elegir
            _playerRow.gameObject.SetActive(_humans.Count > 1);

            for (int i = 0; i < PartSlots.Length; i++)
            {
                FakeBladeComponentData part = player != null ? Equipped(player.Stats, PartSlots[i]) : null;
                PixelOptionRow row = _partRows[i];
                row.SetValueText(part != null ? part.ComponentName.ToUpperInvariant() : "—");
                row.SetValueColor(PartSlots[i] == ComponentSlot.Core && part != null
                    ? SpecialAbilities.Get(part.SpecialAbility).color
                    : _theme.chargeReady);
                if (part == null) row.SetSubValue("", _theme.textDimColor);
                else row.SetSubValue(Loc.Get("ARCHETYPE_" + part.Archetype.ToString().ToUpperInvariant()),
                    _theme.GetArchetypeColor(part.Archetype));
            }
        }

        /// <summary>Siguiente pieza del catálogo en un hueco: se equipa al momento (modelo, stats y poder).</summary>
        private void StepPart(int slotIndex, int dir)
        {
            PlayerController player = BladePlayer;
            if (player == null) return;

            ComponentSlot slot = PartSlots[slotIndex];
            List<FakeBladeComponentData> parts = _bootstrap.Catalog.GetParts(slot);
            if (parts.Count == 0) return;
            int index = parts.IndexOf(Equipped(player.Stats, slot));
            index = index < 0 ? 0 : (index + dir + parts.Count) % parts.Count;
            FakeBladeComponentData part = parts[index];

            player.Stats.EquipComponent(part);

            // Que se mantenga al reiniciar la partida (cambiar rivales)
            foreach (PlayerSetup setup in MatchSetup.Players)
            {
                if (setup.PlayerIndex != player.PlayerID) continue;
                setup.SetPart(slot, part);
                setup.PresetIndex = -1;
            }

            RefreshBlade();
        }

        private static FakeBladeComponentData Equipped(FakeBladeStats stats, ComponentSlot slot)
        {
            switch (slot)
            {
                case ComponentSlot.Tip: return stats.EquippedTip;
                case ComponentSlot.Body: return stats.EquippedBody;
                case ComponentSlot.Blade: return stats.EquippedBlade;
                default: return stats.EquippedCore;
            }
        }
        #endregion

        #region Trucos (E2)
        private string[] CheatOptions() => new[]
        {
            Loc.Get("NO"), Loc.Get("CHEAT_PLAYERS"), Loc.Get("CHEAT_DUMMIES"), Loc.Get("CHEAT_ALL")
        };

        private void RefreshCheats()
        {
            string[] options = CheatOptions();
            _spinCheat.SetOptions(options, (int)SandboxSettings.InfiniteSpin);
            _specialCheat.SetOptions(options, (int)SandboxSettings.FullSpecial);
            _chargesCheat.SetOptions(options, (int)SandboxSettings.InfiniteCharges);
            _dashCheat.SetOptions(options, (int)SandboxSettings.NoDashCooldown);
            _invulnerableCheat.SetOptions(options, (int)SandboxSettings.Invulnerable);
        }

        private void ApplyCheats(GameManager gm)
        {
            if (SandboxSettings.InfiniteSpin == CheatTarget.Off && SandboxSettings.FullSpecial == CheatTarget.Off &&
                SandboxSettings.InfiniteCharges == CheatTarget.Off && SandboxSettings.NoDashCooldown == CheatTarget.Off &&
                SandboxSettings.Invulnerable == CheatTarget.Off)
                return;

            foreach (PlayerController player in gm.Players)
            {
                if (player == null) continue;
                FakeBladeController blade = player.Blade;
                if (blade == null || blade.IsDestroyed) continue;
                bool dummy = _dummies.Contains(player);

                if (SandboxSettings.Applies(SandboxSettings.InfiniteSpin, dummy)) blade.AddSpin(blade.MaxSpinSpeed);
                if (SandboxSettings.Applies(SandboxSettings.FullSpecial, dummy)) blade.Special.FillEnergy();
                if (SandboxSettings.Applies(SandboxSettings.InfiniteCharges, dummy)) blade.Attack.RefillCharges();
                if (SandboxSettings.Applies(SandboxSettings.NoDashCooldown, dummy)) blade.ClearDashCooldown();
                if (SandboxSettings.Applies(SandboxSettings.Invulnerable, dummy)) blade.SetInvulnerable(CheatInvulnerableSeconds);
            }
        }
        #endregion

        #region Reiniciar (E3)
        /// <summary>Todas las peonzas a su punto de salida, con RPM y cargas llenas, energía a 0 y sin estados.</summary>
        private void ResetAll()
        {
            _recorder?.Note("RESET", "");
            var gm = GameManager.Instance;
            if (gm != null)
            {
                foreach (PlayerController player in gm.Players)
                {
                    if (player == null) continue;
                    player.ResetPlayer();
                    Transform spawn = gm.GetSpawnPoint(player.PlayerID);
                    if (spawn != null) player.Blade.SetPosition(spawn.position, spawn.rotation);
                }
            }
            ClosePanel();
        }
        #endregion

        #region UI
        private void BuildUI()
        {
            _theme = _bootstrap.Catalog.uiTheme;
            _px = Mathf.Max(1, _theme.pixelSize);
            MatchMenuUI.EnsureEventSystem();

            Canvas canvas = PixelUI.CreateOverlayCanvas("[SandboxUI]", transform, _theme, CanvasOrder);

            var dim = PixelUI.CreateImage("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.45f));
            PixelUI.Stretch(dim.rectTransform, 0, _px);
            _dim = dim.gameObject;
            _dim.SetActive(false);

            // Principal
            _main = CreatePanel(canvas, "SANDBOX_TITLE");
            _main.AddButton("SANDBOX_SECTION_RIVALS", () => ShowPanel(_rivalsPanel));
            _main.AddButton("SANDBOX_SECTION_BLADE", () => ShowPanel(_bladePanel));
            _main.AddButton("SANDBOX_SECTION_CHEATS", () => ShowPanel(_cheatsPanel));
            _main.AddButton("SANDBOX_SECTION_DEBUG", () => ShowPanel(_debugPanel));
            _main.AddButton("SANDBOX_SECTION_TUNING", () => ShowPanel(_tuningPanel));
            _main.AddButton("SANDBOX_SECTION_EFFECTS", () => ShowPanel(_effectsPanel));
            _speedRow = _main.AddSelector("SANDBOX_SPEED", new[] { "-" }, 0, SetSpeed);
            _main.AddButton("SANDBOX_RESET", ResetAll);
            _main.AddSpacer(2);
            _main.AddButton("BACK", ClosePanel);

            // Rivales
            _rivalsPanel = CreatePanel(canvas, "SANDBOX_SECTION_RIVALS");
            _rivalsRow = _rivalsPanel.AddSelector("SANDBOX_RIVALS", new[] { "-" }, 0, i => _pendingDummies = i);
            _maxHint = _rivalsPanel.AddText("SANDBOX_MAX_HINT", 5, _theme.textColor, TextAlignmentOptions.Center, 7);
            _behaviourRow = _rivalsPanel.AddSelector("SANDBOX_BEHAVIOUR", new[] { "-" }, 0,
                i =>
                {
                    SandboxSettings.Behaviour = (DummyBehaviour)i;
                    ApplyDummyBrains();
                });
            _intervalRow = _rivalsPanel.AddSelector("SANDBOX_INTERVAL", new[] { "-" }, 0,
                i => SandboxSettings.IntervalIndex = i);
            _aiLevelRow = _rivalsPanel.AddSelector("SANDBOX_AI_LEVEL", new[] { "-" }, 0, i =>
            {
                SandboxSettings.AIDifficulty = (AIDifficulty)i;
                ApplyDummyBrains();
            });
            _aiProfileRow = _rivalsPanel.AddSelector("SANDBOX_AI_PROFILE", new[] { "-" }, 0, i =>
            {
                SandboxSettings.AIProfile = i;
                ApplyDummyBrains();
            });
            _dummyPresetRow = _rivalsPanel.AddSelector("SANDBOX_DUMMY_BLADE", new[] { "-" }, 0, SetDummyPreset);
            _dummyCoreRow = _rivalsPanel.AddSelector("SANDBOX_DUMMY_CORE", new[] { "-" }, 0, SetDummyCore);
            AddBack(_rivalsPanel);

            // Mi peonza
            _bladePanel = CreatePanel(canvas, "SANDBOX_SECTION_BLADE");
            _playerRow = _bladePanel.AddSelector("SANDBOX_PLAYER", new[] { "-" }, 0, i =>
            {
                _bladePlayer = i;
                RefreshBlade();
            });
            for (int i = 0; i < PartSlots.Length; i++)
            {
                int slot = i;
                _partRows[i] = _bladePanel.AddCustom(PartKeys[i], "", dir => StepPart(slot, dir), null, PartRowHeight);
            }
            AddBack(_bladePanel);

            // Trucos
            _cheatsPanel = CreatePanel(canvas, "SANDBOX_SECTION_CHEATS");
            _spinCheat = AddCheat("CHEAT_SPIN", t => SandboxSettings.InfiniteSpin = t);
            _specialCheat = AddCheat("CHEAT_SPECIAL", t => SandboxSettings.FullSpecial = t);
            _chargesCheat = AddCheat("CHEAT_CHARGES", t => SandboxSettings.InfiniteCharges = t);
            _dashCheat = AddCheat("CHEAT_DASH", t => SandboxSettings.NoDashCooldown = t);
            _invulnerableCheat = AddCheat("CHEAT_INVULNERABLE", t => SandboxSettings.Invulnerable = t);
            AddBack(_cheatsPanel);

            // Debug
            _debugPanel = CreatePanel(canvas, "SANDBOX_SECTION_DEBUG");
            _parryDebug = AddToggle(_debugPanel, "DEBUG_PARRY", v => SandboxSettings.ShowParryWindow = v);
            _velocityDebug = AddToggle(_debugPanel, "DEBUG_VELOCITY", v => SandboxSettings.ShowVelocity = v);
            _damageDebug = AddToggle(_debugPanel, "DEBUG_DAMAGE", v => SandboxSettings.ShowDamageNumbers = v);
            _statusDebug = AddToggle(_debugPanel, "DEBUG_STATUS", v => SandboxSettings.ShowStatus = v);
            _fpsDebug = AddToggle(_debugPanel, "DEBUG_FPS", v => SandboxSettings.ShowFps = v);
            _logDebug = AddToggle(_debugPanel, "DEBUG_LOG", v => SandboxSettings.ShowEventLog = v);
            _recordDebug = AddToggle(_debugPanel, "DEBUG_RECORD", v => SandboxSettings.RecordData = v);
            AddBack(_debugPanel);

            // Ajustes: arena (al cerrar el panel) y valores de combate (al momento, temporales)
            _tuningPanel = CreatePanel(canvas, "SANDBOX_SECTION_TUNING");
            _arenaRow = _tuningPanel.AddSelector("TUNE_ARENA", new[] { "-" }, 0, i => _pendingArena = i);
            for (int i = 0; i < Tunables.Length; i++)
            {
                int index = i;
                _tuningRows[i] = _tuningPanel.AddSelector(Tunables[i].Key, new[] { "-" }, 0, f => SetTuning(index, f));
            }
            _tuningPanel.AddSpacer(2);
            _tuningPanel.AddButton("TUNE_RESET", ResetTuning);
            AddBack(_tuningPanel);

            // Efectos (O5): galería para ver cada impacto repetido junto a la peonza, con el panel cerrado
            _effectsPanel = CreatePanel(canvas, "SANDBOX_SECTION_EFFECTS");
            _impactRow = _effectsPanel.AddSelector("SANDBOX_IMPACT", new[] { "-" }, 0, i => SandboxSettings.ImpactPreview = i);
            _impactIntervalRow = _effectsPanel.AddSelector("SANDBOX_INTERVAL", new[] { "-" }, 0,
                i => SandboxSettings.ImpactIntervalIndex = i);
            _effectsPanel.AddText("SANDBOX_IMPACT_HINT", 5, _theme.textColor, TextAlignmentOptions.Center, 13);
            AddBack(_effectsPanel);

            // Recordatorio del botón del panel, abajo en el centro
            _hint = PixelUI.CreateText("SandboxHint", canvas.transform, _theme, 5 * _px, TextAlignmentOptions.Center, _theme.textColor);
            RectTransform rt = _hint.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 4 * _px);
            rt.sizeDelta = new Vector2(320 * _px, 8 * _px);
            RefreshHint();
        }

        private PixelMenuList CreatePanel(Canvas canvas, string titleKey)
        {
            PixelMenuList panel = PixelMenuList.Create(canvas.transform, _theme, titleKey, PanelWidthPx, true);
            panel.OnLanguageRefreshed += RefreshAll;
            panel.Hide();
            return panel;
        }

        private void AddBack(PixelMenuList panel)
        {
            panel.AddSpacer(2);
            panel.AddButton("BACK", () => ShowPanel(_main));
        }

        private PixelOptionRow AddCheat(string labelKey, System.Action<CheatTarget> set) =>
            _cheatsPanel.AddSelector(labelKey, CheatOptions(), 0, i => set((CheatTarget)i));

        private static PixelOptionRow AddToggle(PixelMenuList panel, string labelKey, System.Action<bool> set) =>
            panel.AddSelector(labelKey, new[] { Loc.Get("NO"), Loc.Get("YES") }, 0, i => set(i == 1));

        private void RefreshHint()
        {
            if (_hint == null) return;
            // Nombre corto ("TAB"): el de la distribución del teclado puede ser largo y con tildes
            string key = InputBindings.Left.sandboxPanel.ToString().ToUpperInvariant();
            string button = InputBindings.ButtonName(InputBindings.Pad.sandboxPanel);
            _hint.text = Loc.Format("SANDBOX_HINT", key + " / " + button);
            // En pausa de depuración, cómo avanzar un frame
            if (SandboxSettings.Speed <= 0f) _hint.text = Loc.Get("SANDBOX_STEP_HINT") + " · " + _hint.text;
        }
        #endregion
    }
}
