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
        private TextMeshProUGUI _maxHint;

        private PixelOptionRow _playerRow;
        private readonly PixelOptionRow[] _partRows = new PixelOptionRow[4];
        private int _bladePlayer;

        private PixelOptionRow _spinCheat, _specialCheat, _chargesCheat, _dashCheat, _invulnerableCheat;

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
            BuildUI();

            InputBindings.OnChanged += RefreshHint;
            Loc.OnLanguageChanged += RefreshHint;
        }

        private void OnDestroy()
        {
            InputBindings.OnChanged -= RefreshHint;
            Loc.OnLanguageChanged -= RefreshHint;
            if (_open) MenuStack.Pop();
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

            ApplyCheats(gm);
        }
        #endregion

        #region Panel
        private void OpenPanel()
        {
            _open = true;
            MenuStack.Push();
            GameManager.Instance.SetPanelPause(true);

            _pendingDummies = _dummies.Count;
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
                Loc.Get("DUMMY_DASH"), Loc.Get("DUMMY_SPECIAL")
            }, (int)SandboxSettings.Behaviour);

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
                _dummies.Add(_bootstrap.SpawnDummy(playerIndex, _dummies.Count + 1));
            }

            _bootstrap.RefreshCameraTargets();
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
            _main.AddButton("SANDBOX_RESET", ResetAll);
            _main.AddSpacer(2);
            _main.AddButton("BACK", ClosePanel);

            // Rivales
            _rivalsPanel = CreatePanel(canvas, "SANDBOX_SECTION_RIVALS");
            _rivalsRow = _rivalsPanel.AddSelector("SANDBOX_RIVALS", new[] { "-" }, 0, i => _pendingDummies = i);
            _maxHint = _rivalsPanel.AddText("SANDBOX_MAX_HINT", 5, _theme.textColor, TextAlignmentOptions.Center, 7);
            _behaviourRow = _rivalsPanel.AddSelector("SANDBOX_BEHAVIOUR", new[] { "-" }, 0,
                i => SandboxSettings.Behaviour = (DummyBehaviour)i);
            _intervalRow = _rivalsPanel.AddSelector("SANDBOX_INTERVAL", new[] { "-" }, 0,
                i => SandboxSettings.IntervalIndex = i);
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

            // Recordatorio del botón del panel, abajo en el centro
            _hint = PixelUI.CreateText("SandboxHint", canvas.transform, _theme, 5 * _px, TextAlignmentOptions.Center, _theme.textColor);
            RectTransform rt = _hint.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 4 * _px);
            rt.sizeDelta = new Vector2(160 * _px, 8 * _px);
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

        private void RefreshHint()
        {
            if (_hint == null) return;
            // Nombre corto ("TAB"): el de la distribución del teclado puede ser largo y con tildes
            string key = InputBindings.Left.sandboxPanel.ToString().ToUpperInvariant();
            string button = InputBindings.ButtonName(InputBindings.Pad.sandboxPanel);
            _hint.text = Loc.Format("SANDBOX_HINT", key + " / " + button);
        }
        #endregion
    }
}
