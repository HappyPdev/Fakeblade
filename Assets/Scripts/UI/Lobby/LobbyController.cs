using System.Collections.Generic;
using FakeBlade.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FakeBlade.UI
{
    /// <summary>
    /// Selección de peonzas y parámetros de partida (GDD 9.2.1 y 9.2.2).
    ///
    /// 1. Columnas: empiezan 2; cada jugador se une manteniendo el botón de ataque de su
    ///    dispositivo (A / Espacio / Ctrl der.). Se añade una columna por jugador (máx. 4).
    ///    Cada uno monta su peonza (preset + piezas), elige color (sin repetir) y equipo, y confirma.
    /// 2. Con todos listos, J1 configura la partida (modo, vidas, tiempo, puntos, fuego amigo, escenario).
    /// 3. ¡Empezar! guarda todo en MatchSetup y carga la batalla.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class LobbyController : MonoBehaviour
    {
        [SerializeField] private FakeBladeCatalog catalog;
        [SerializeField] private float joinHoldTime = 0.7f;
        [SerializeField] private float leaveHoldTime = 0.8f;
        [SerializeField] private float allReadyDelay = 0.5f;
        [SerializeField] private bool debugInput = false;

        private const int MaxPlayers = 4;

        private enum Phase { Columns, Settings }

        private enum ModeOption { LastStanding, Stocks, Points, Teams, Sandbox }

        private sealed class Slot
        {
            public PlayerSetup Setup;
            public DeviceMenuInput Input;
            public bool Ready;
            public float LeaveTimer;
            public LobbyColumn Column;
            public BladePreviewStage Stage;
        }

        // Parámetros recordados entre visitas (p. ej. tras "Cambiar peonzas")
        private static ModeOption s_mode = ModeOption.LastStanding;
        private static int s_lives = 3;
        private static int s_timeIndex;
        private static int s_pointsIndex;
        private static bool s_friendlyFire;
        private static int s_arena;

        private static readonly int[] TimeOptions = { 0, 60, 120, 180, 300, 600 };
        private static readonly int[] PointOptions = { 0, 3, 5, 10, 15, 20 };

        private HUDTheme _theme;
        private int _px;
        private Canvas _canvas;
        private TextMeshProUGUI _title;
        private PixelOptionRow _backButton;
        private readonly List<LobbyColumn> _columns = new List<LobbyColumn>(MaxPlayers);
        private readonly List<Slot> _slots = new List<Slot>(MaxPlayers);
        private readonly BladePreviewStage[] _stages = new BladePreviewStage[MaxPlayers];
        private readonly Dictionary<long, float> _joinHold = new Dictionary<long, float>(8);

        private Phase _phase = Phase.Columns;
        private float _allReadyTimer;

        private GameObject _settingsDim;
        private PixelMenuList _settings;
        private PixelOptionRow _modeRow, _livesRow, _timeRow, _pointsRow, _ffRow, _arenaRow;
        private TextMeshProUGUI _settingsWarning;
        private readonly List<ModeOption> _modes = new List<ModeOption>(5);

        #region Lifecycle
        private void Awake()
        {
            MenuStack.Reset();
            Time.timeScale = 1f;
            if (catalog != null) CombatConfig.SetActive(catalog.combatConfig);
        }

        private void Start()
        {
            if (catalog == null)
            {
                Debug.LogError("[Lobby] Falta el catálogo (FakeBladeCatalog).", this);
                return;
            }

            _theme = catalog.uiTheme != null ? catalog.uiTheme : HUDTheme.CreateDefault();
            _px = Mathf.Max(1, _theme.pixelSize);

            MatchMenuUI.EnsureEventSystem();
            SetupCamera();

            _canvas = PixelUI.CreateOverlayCanvas("Lobby_Canvas", transform, _theme, 10);
            BuildTopBar(_canvas.transform);
            BuildColumns(_canvas.transform);
            BuildSettings(_canvas.transform);

            RestoreFromSetup();
            RebuildColumns();

            Loc.OnLanguageChanged += RefreshTexts;
        }

        private void OnDestroy()
        {
            Loc.OnLanguageChanged -= RefreshTexts;
        }

        private void Update()
        {
            if (_canvas == null) return;
            float dt = Time.unscaledDeltaTime;

            if (_phase == Phase.Columns) UpdateColumns(dt);
            else UpdateSettings();
        }
        #endregion

        #region Build
        private void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _theme.barBackground;
            cam.cullingMask = 0; // solo UI; las vistas previas tienen sus propias cámaras
        }

        private void BuildTopBar(Transform root)
        {
            _title = PixelUI.CreateShadowedTitle("Title", root, _theme, 12 * _px, _theme.textColor);
            var titleRt = (RectTransform)_title.transform.parent;
            titleRt.anchorMin = new Vector2(0.2f, 1f);
            titleRt.anchorMax = new Vector2(0.8f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -3 * _px);
            titleRt.sizeDelta = new Vector2(0f, 16 * _px);

            RectTransform backHolder = PixelUI.CreateRect("BackHolder", root);
            backHolder.anchorMin = backHolder.anchorMax = backHolder.pivot = new Vector2(0f, 1f);
            backHolder.anchoredPosition = new Vector2(6 * _px, -5 * _px);
            backHolder.sizeDelta = new Vector2(44 * _px, 10 * _px);
            _backButton = PixelOptionRow.Create(backHolder, _theme, 10, true, 5f);
            PixelUI.Stretch((RectTransform)_backButton.transform, 0, _px);
            _backButton.gameObject.AddComponent<PixelRowSelectable>();
            _backButton.onSubmit = BackToMenu;

            RefreshTopBar();
        }

        private void BuildColumns(Transform root)
        {
            RectTransform area = PixelUI.CreateRect("Columns", root);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(6 * _px, 5 * _px);
            area.offsetMax = new Vector2(-6 * _px, -22 * _px);

            var layout = area.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 3 * _px;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            for (int i = 0; i < MaxPlayers; i++)
                _columns.Add(LobbyColumn.Create(area, _theme));
        }

        private void BuildSettings(Transform root)
        {
            var dim = PixelUI.CreateImage("SettingsDim", root, _theme.overlayColor);
            dim.raycastTarget = true;
            PixelUI.Stretch(dim.rectTransform, 0, _px);
            _settingsDim = dim.gameObject;

            _settings = PixelMenuList.Create(root, _theme, "SET_TITLE", 150, false);
            _modeRow = _settings.AddSelector("SET_MODE", new[] { "-" }, 0, OnModeChanged, 9);
            _livesRow = _settings.AddSelector("SET_LIVES", NumberLabels(1, 9), s_lives - 1, i => s_lives = i + 1, 9);
            _timeRow = _settings.AddSelector("SET_TIME", TimeLabels(), s_timeIndex, i => s_timeIndex = i, 9);
            _pointsRow = _settings.AddSelector("SET_POINTS", PointLabels(), s_pointsIndex, i => s_pointsIndex = i, 9);
            _ffRow = _settings.AddSelector("SET_FRIENDLY_FIRE", YesNo(), s_friendlyFire ? 1 : 0, i => s_friendlyFire = i == 1, 9);
            _arenaRow = _settings.AddSelector("SET_ARENA", ArenaLabels(), s_arena, i => s_arena = i, 9);
            _settings.AddSpacer(2);
            _settings.AddButton("SET_START", StartMatch, 11);
            _settingsWarning = _settings.AddText(null, 4, _theme.healthLow, TextAlignmentOptions.Center, 7);
            _settings.AddText("SET_HINT", 3.5f, _theme.textDimColor, TextAlignmentOptions.Center, 6);
            _settings.OnLanguageRefreshed += RefreshSettingsLabels;

            _settingsDim.SetActive(false);
            _settings.Hide();
        }
        #endregion

        #region Columns phase
        private void UpdateColumns(float dt)
        {
            UpdateJoin(dt);

            for (int i = _slots.Count - 1; i >= 0; i--)
                UpdateSlot(_slots[i], dt);

            bool allReady = _slots.Count > 0;
            for (int i = 0; i < _slots.Count; i++)
                allReady &= _slots[i].Ready;

            if (allReady)
            {
                _allReadyTimer += dt;
                if (_allReadyTimer >= allReadyDelay) EnterSettings();
            }
            else
            {
                _allReadyTimer = 0f;
            }

            // Sin jugadores, Atrás vuelve al menú principal
            if (_slots.Count == 0 && MenuInput.AnyBackPressed())
                BackToMenu();
        }

        private void UpdateJoin(float dt)
        {
            float best = 0f;
            if (_slots.Count < MaxPlayers)
            {
                CheckJoin(InputDeviceKind.KeyboardLeft, 0, dt, ref best);
                CheckJoin(InputDeviceKind.KeyboardRight, 0, dt, ref best);
                var pads = Gamepad.all;
                for (int i = 0; i < pads.Count; i++)
                    CheckJoin(InputDeviceKind.Gamepad, pads[i].deviceId, dt, ref best);
            }

            if (_slots.Count < _columns.Count && _columns[_slots.Count].gameObject.activeSelf)
                _columns[_slots.Count].SetJoinProgress(best);
        }

        private void CheckJoin(InputDeviceKind kind, int deviceId, float dt, ref float best)
        {
            if (IsAssigned(kind, deviceId)) return;

            long key = ((long)kind << 32) | (uint)deviceId;
            if (!DeviceMenuInput.IsConfirmHeld(kind, deviceId))
            {
                _joinHold.Remove(key);
                return;
            }

            _joinHold.TryGetValue(key, out float held);
            held += dt;
            if (held >= joinHoldTime)
            {
                _joinHold.Remove(key);
                Join(kind, deviceId);
                return;
            }

            _joinHold[key] = held;
            best = Mathf.Max(best, held / joinHoldTime);
        }

        private bool IsAssigned(InputDeviceKind kind, int deviceId)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Input.Matches(kind, deviceId)) return true;
            return false;
        }

        private void Join(InputDeviceKind kind, int deviceId)
        {
            int index = _slots.Count;
            int presetCount = Mathf.Max(1, catalog.presets.Count);
            var setup = new PlayerSetup
            {
                PlayerIndex = index,
                Controller = ControllerType.Human,
                Device = kind,
                GamepadDeviceId = deviceId,
                PresetIndex = index % presetCount,
                ColorIndex = FirstFreeColor(),
                Team = index % 2
            };
            setup.Color = catalog.GetColor(setup.ColorIndex);
            setup.ApplyPreset(catalog.GetPreset(setup.PresetIndex));

            AddSlot(setup);
            RebuildColumns();

            if (kind == InputDeviceKind.Gamepad)
            {
                var pad = InputDeviceUtil.FindGamepad(deviceId);
                if (pad != null && SettingsService.Current.vibration)
                {
                    pad.SetMotorSpeeds(0.3f, 0.5f);
                    StartCoroutine(StopMotors(pad));
                }
            }
        }

        private static System.Collections.IEnumerator StopMotors(Gamepad pad)
        {
            yield return new WaitForSecondsRealtime(0.15f);
            if (pad != null) pad.SetMotorSpeeds(0f, 0f);
        }

        private void AddSlot(PlayerSetup setup)
        {
            _slots.Add(new Slot
            {
                Setup = setup,
                Input = new DeviceMenuInput(setup.Device, setup.GamepadDeviceId)
            });
        }

        private void Leave(Slot slot)
        {
            _slots.Remove(slot);
            _allReadyTimer = 0f;
            RebuildColumns();
        }

        private void RebuildColumns()
        {
            int visible = Mathf.Clamp(_slots.Count + 1, 2, MaxPlayers);
            for (int i = 0; i < _columns.Count; i++)
            {
                LobbyColumn column = _columns[i];
                bool active = i < visible;
                column.gameObject.SetActive(active);

                if (i < _slots.Count) BindSlot(_slots[i], i);
                else if (active) column.ShowEmpty(0f);

                if (i >= _slots.Count && _stages[i] != null) _stages[i].SetVisible(false);
            }
        }

        private void BindSlot(Slot slot, int index)
        {
            slot.Setup.PlayerIndex = index;
            slot.Column = _columns[index];
            slot.Stage = GetStage(index);
            slot.Stage.SetVisible(true);
            slot.LeaveTimer = 0f;

            LobbyColumn column = slot.Column;
            column.ShowPlayer(index + 1, slot.Setup.Color, InputDeviceUtil.DeviceLabel(slot.Setup.Device, slot.Setup.GamepadDeviceId));
            column.SetPreview(slot.Stage.Texture);
            column.ResetFocus();
            column.SetReady(slot.Ready);
            column.SetLeaveProgress(0f);
            RefreshSlot(slot);
        }

        private BladePreviewStage GetStage(int index)
        {
            if (_stages[index] == null)
                _stages[index] = BladePreviewStage.Create(index, catalog.playerPrefab, _theme.barBackground);
            return _stages[index];
        }

        private void UpdateSlot(Slot slot, float dt)
        {
            DeviceMenuInput input = slot.Input;
            input.Update(dt);
            LobbyColumn column = slot.Column;

            if (debugInput && (input.Up || input.Down || input.Left || input.Right || input.Confirm || input.Back))
                Debug.Log($"[Lobby] J{slot.Setup.PlayerIndex + 1} f{Time.frameCount} U{input.Up} D{input.Down} L{input.Left} R{input.Right} C{input.Confirm} B{input.Back}", this);

            if (slot.Ready)
            {
                if (input.Back) SetReady(slot, false);
                return;
            }

            if (input.Up) column.MoveFocus(-1);
            if (input.Down) column.MoveFocus(1);
            if (input.Left) StepRow(slot, -1);
            if (input.Right) StepRow(slot, 1);
            if (input.Confirm)
            {
                SetReady(slot, true);
                return;
            }

            // Mantener Atrás para abandonar la columna
            if (input.BackHeld)
            {
                slot.LeaveTimer += dt;
                column.SetLeaveProgress(slot.LeaveTimer / leaveHoldTime);
                if (slot.LeaveTimer >= leaveHoldTime) Leave(slot);
            }
            else if (slot.LeaveTimer > 0f)
            {
                slot.LeaveTimer = 0f;
                column.SetLeaveProgress(0f);
            }
        }

        private void SetReady(Slot slot, bool ready)
        {
            slot.Ready = ready;
            slot.Column.SetReady(ready);
            if (!ready) _allReadyTimer = 0f;
        }

        private void StepRow(Slot slot, int dir)
        {
            LobbyColumn column = slot.Column;
            PixelOptionRow row = column.FocusedRow;
            PlayerSetup s = slot.Setup;

            if (row == column.PresetRow) StepPreset(s, dir);
            else if (row == column.TipRow) StepPart(s, ComponentSlot.Tip, dir);
            else if (row == column.BodyRow) StepPart(s, ComponentSlot.Body, dir);
            else if (row == column.BladeRow) StepPart(s, ComponentSlot.Blade, dir);
            else if (row == column.CoreRow) StepPart(s, ComponentSlot.Core, dir);
            else if (row == column.ColorRow) StepColor(slot, dir);
            else if (row == column.TeamRow) s.Team = 1 - s.Team;
            else return;

            RefreshSlot(slot);
        }

        /// <summary>Presets + "Aleatorio" (índice = número de presets). -1 = personalizada.</summary>
        private void StepPreset(PlayerSetup s, int dir)
        {
            int count = catalog.presets.Count;
            int options = count + 1;
            int index = s.PresetIndex < 0 ? (dir > 0 ? 0 : count) : (s.PresetIndex + dir + options) % options;
            s.PresetIndex = index;

            if (index < count)
            {
                s.ApplyPreset(catalog.presets[index]);
            }
            else
            {
                s.Tip = RandomPart(ComponentSlot.Tip);
                s.Body = RandomPart(ComponentSlot.Body);
                s.Blade = RandomPart(ComponentSlot.Blade);
                s.Core = RandomPart(ComponentSlot.Core);
            }
        }

        private FakeBladeComponentData RandomPart(ComponentSlot slot)
        {
            var parts = catalog.GetParts(slot);
            return parts.Count == 0 ? null : parts[Random.Range(0, parts.Count)];
        }

        private void StepPart(PlayerSetup s, ComponentSlot slot, int dir)
        {
            var parts = catalog.GetParts(slot);
            if (parts.Count == 0) return;

            int index = parts.IndexOf(s.GetPart(slot));
            index = index < 0 ? 0 : (index + dir + parts.Count) % parts.Count;
            s.SetPart(slot, parts[index]);
            s.PresetIndex = -1; // personalizada
        }

        private void StepColor(Slot slot, int dir)
        {
            int count = catalog.palette.Length;
            int index = slot.Setup.ColorIndex;
            for (int i = 0; i < count; i++)
            {
                index = (index + dir + count) % count;
                if (!IsColorUsed(index, slot)) break;
            }
            slot.Setup.ColorIndex = index;
            slot.Setup.Color = catalog.GetColor(index);
        }

        private bool IsColorUsed(int colorIndex, Slot except)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] != except && _slots[i].Setup.ColorIndex == colorIndex) return true;
            return false;
        }

        private int FirstFreeColor()
        {
            for (int c = 0; c < catalog.palette.Length; c++)
                if (!IsColorUsed(c, null)) return c;
            return 0;
        }

        /// <summary>Actualiza los valores de la columna, las stats y la vista previa.</summary>
        private void RefreshSlot(Slot slot)
        {
            PlayerSetup s = slot.Setup;
            LobbyColumn column = slot.Column;

            column.PresetRow.SetValueText(PresetName(s.PresetIndex));
            column.TipRow.SetValueText(PartName(s.Tip));
            column.BodyRow.SetValueText(PartName(s.Body));
            column.BladeRow.SetValueText(PartName(s.Blade));
            column.CoreRow.SetValueText(PartName(s.Core));
            column.CoreRow.SetValueColor(CoreColor(s.Core));
            SetPartArchetype(column.TipRow, s.Tip);
            SetPartArchetype(column.BodyRow, s.Body);
            SetPartArchetype(column.BladeRow, s.Blade);
            SetPartArchetype(column.CoreRow, s.Core);

            column.ColorRow.SetValueText(Loc.Format("COLOR_N", s.ColorIndex + 1));
            column.ColorRow.SetValueColor(s.Color);
            column.TeamRow.SetValueText(s.Team == 0 ? "A" : "B");
            column.TeamRow.SetValueColor(catalog.GetTeamColor(s.Team));
            column.SetBorderColor(s.Color);

            BladeStatBlock stats = FakeBladeStats.Calculate(catalog.GetBaseStats(), s.Tip, s.Body, s.Blade, s.Core);
            column.SetArchetype(ArchetypeName(stats.Archetype),
                Loc.Format("LOBBY_SPECIAL_LINE", SpecialAbilities.Get(stats.Special).DisplayName, stats.AttackCharges),
                _theme.GetArchetypeColor(stats.Archetype));

            // Barras por tramos: base + lo que aporta cada pieza
            BladeBaseStats b = catalog.GetBaseStats();
            SetStatBar(column, 0, "STAT_ATTACK", s, b.attackPower, p => p.AttackPowerModifier, stats.AttackPower, 40f);
            SetStatBar(column, 1, "STAT_DEFENSE", s, b.defense, p => p.DefenseModifier, stats.Defense, 60f);
            SetStatBar(column, 2, "STAT_SPEED", s, b.moveSpeed, p => p.MoveSpeedModifier, stats.MoveSpeed, 16f);
            SetStatBar(column, 3, "STAT_RPM", s, b.maxSpin, p => p.MaxSpinModifier, stats.MaxSpin, 900f);
            SetStatBar(column, 4, "STAT_WEIGHT", s, b.weight, p => p.WeightModifier, stats.Weight, 4f);

            slot.Stage.SetScheme(BladeColors.Get(catalog, s.ColorIndex));
            // El núcleo brilla con el color de su poder, como con la esfera llena
            slot.Stage.SetCoreGlow(SpecialAbilities.Get(stats.Special).color, 1f);
            slot.Stage.SetSpinSpeed(300f + stats.MaxSpin);
        }

        private string PresetName(int index)
        {
            if (index < 0) return Loc.Get("PRESET_CUSTOM");
            if (index >= catalog.presets.Count) return Loc.Get("PRESET_RANDOM");
            return Loc.Get(catalog.presets[index].nameKey);
        }

        private static string PartName(FakeBladeComponentData part) =>
            part != null ? part.ComponentName.ToUpperInvariant() : "—";

        private static string ArchetypeName(BladeArchetype archetype) =>
            Loc.Get("ARCHETYPE_" + archetype.ToString().ToUpperInvariant());

        /// <summary>Arquetipo de la pieza, pequeño y de su color, debajo del nombre (para elegir sin confusiones).</summary>
        private void SetPartArchetype(PixelOptionRow row, FakeBladeComponentData part)
        {
            if (part == null) row.SetSubValue("", _theme.textDimColor);
            else row.SetSubValue(ArchetypeName(part.Archetype), _theme.GetArchetypeColor(part.Archetype));
        }

        private readonly float[] _statParts = new float[LobbyColumn.PartCount];

        private void SetStatBar(LobbyColumn column, int index, string labelKey, PlayerSetup s, float baseValue,
            System.Func<FakeBladeComponentData, float> modifier, float finalValue, float scale)
        {
            for (int i = 0; i < _statParts.Length; i++)
            {
                FakeBladeComponentData part = s.GetPart((ComponentSlot)i);
                _statParts[i] = part != null ? modifier(part) : 0f;
            }
            column.SetStat(index, Loc.Get(labelKey), baseValue, _statParts, finalValue, scale);
        }

        /// <summary>El núcleo se ve con el color de su poder (el de su aura y su esfera), para reconocerlo de un vistazo.</summary>
        private Color CoreColor(FakeBladeComponentData core) =>
            core != null ? SpecialAbilities.Get(core.SpecialAbility).color : _theme.chargeReady;
        #endregion

        #region Settings phase
        private void EnterSettings()
        {
            _phase = Phase.Settings;
            _allReadyTimer = 0f;

            _modes.Clear();
            // Con 1 jugador solo hay sandbox; con 2-4, los modos normales y el sandbox (GDD 6.3)
            if (_slots.Count > 1)
            {
                _modes.Add(ModeOption.LastStanding);
                _modes.Add(ModeOption.Stocks);
                _modes.Add(ModeOption.Points);
                _modes.Add(ModeOption.Teams);
            }
            _modes.Add(ModeOption.Sandbox);

            RefreshSettingsLabels();
            _settingsWarning.text = string.Empty;
            _settings.SetBorderColor(_slots[0].Setup.Color);
            _settingsDim.SetActive(true);
            _settings.Show();
            _settings.Focus(-1);
            _settings.MoveFocus(1);
        }

        private void ExitSettings()
        {
            _phase = Phase.Columns;
            _settingsDim.SetActive(false);
            _settings.Hide();
            for (int i = 0; i < _slots.Count; i++) SetReady(_slots[i], false);
        }

        private void UpdateSettings()
        {
            if (_slots.Count == 0)
            {
                ExitSettings();
                return;
            }

            // Solo J1 configura la partida
            DeviceMenuInput j1 = _slots[0].Input;
            j1.Update(Time.unscaledDeltaTime);

            if (debugInput && (j1.Up || j1.Down || j1.Left || j1.Right || j1.Confirm || j1.Back))
                Debug.Log($"[Lobby] Settings f{Time.frameCount} U{j1.Up} D{j1.Down} L{j1.Left} R{j1.Right} C{j1.Confirm} B{j1.Back} focus:{_settings.FocusedRow?.LabelKey}", this);

            if (j1.Up) _settings.MoveFocus(-1);
            if (j1.Down) _settings.MoveFocus(1);
            if (j1.Left) _settings.StepFocused(-1);
            if (j1.Right) _settings.StepFocused(1);
            if (j1.Confirm) _settings.SubmitFocused();
            if (j1.Back) ExitSettings();
        }

        private ModeOption CurrentMode
        {
            get
            {
                if (_modes.Count == 0) return ModeOption.LastStanding;
                int index = _modes.IndexOf(s_mode);
                return index >= 0 ? s_mode : _modes[0];
            }
        }

        private void OnModeChanged(int index)
        {
            if (index >= 0 && index < _modes.Count) s_mode = _modes[index];
            UpdateSettingsVisibility();
        }

        private void RefreshSettingsLabels()
        {
            var labels = new string[Mathf.Max(1, _modes.Count)];
            for (int i = 0; i < _modes.Count; i++) labels[i] = Loc.Get(ModeKey(_modes[i]));
            if (_modes.Count == 0) labels[0] = "-";

            ModeOption current = CurrentMode;
            _modeRow.SetOptions(labels, Mathf.Max(0, _modes.IndexOf(current)));
            if (_modes.Count > 0) s_mode = current;

            _livesRow.SetOptions(NumberLabels(1, 9), s_lives - 1);
            _timeRow.SetOptions(TimeLabels(), s_timeIndex);
            _pointsRow.SetOptions(PointLabels(), s_pointsIndex);
            _ffRow.SetOptions(YesNo(), s_friendlyFire ? 1 : 0);
            _arenaRow.SetOptions(ArenaLabels(), Mathf.Clamp(s_arena, 0, Mathf.Max(0, catalog.arenas.Count - 1)));

            // Con un solo modo (sandbox con 1 jugador) no tiene sentido cambiarlo
            _modeRow.SetInteractable(_modes.Count > 1);
            UpdateSettingsVisibility();
        }

        private void UpdateSettingsVisibility()
        {
            ModeOption mode = CurrentMode;
            _livesRow.gameObject.SetActive(mode == ModeOption.Stocks);
            _pointsRow.gameObject.SetActive(mode == ModeOption.Points);
            _ffRow.gameObject.SetActive(mode == ModeOption.Teams);
            _timeRow.gameObject.SetActive(mode != ModeOption.Sandbox);
            _settingsWarning.text = string.Empty;
            _settings.EnsureValidFocus();
        }

        private void StartMatch()
        {
            ModeOption mode = CurrentMode;

            if (mode == ModeOption.Teams)
            {
                int teamA = 0, teamB = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].Setup.Team == 0) teamA++;
                    else teamB++;
                }
                if (teamA == 0 || teamB == 0)
                {
                    _settingsWarning.text = Loc.Get("SET_TEAMS_INVALID");
                    return;
                }
            }

            MatchRules rules = BuildRules(mode);

            MatchSetup.Players.Clear();
            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].Setup.PlayerIndex = i;
                MatchSetup.Players.Add(_slots[i].Setup);
            }
            MatchSetup.Rules = rules;
            MatchSetup.Arena = catalog.arenas.Count > 0 ? catalog.arenas[Mathf.Clamp(s_arena, 0, catalog.arenas.Count - 1)] : null;

            SceneFlow.Load(mode == ModeOption.Sandbox ? SceneFlow.Sandbox : SceneFlow.Battle);
        }

        private MatchRules BuildRules(ModeOption mode)
        {
            MatchRules template;
            switch (mode)
            {
                case ModeOption.Stocks: template = catalog.stocks; break;
                case ModeOption.Points: template = catalog.points; break;
                case ModeOption.Teams: template = catalog.teams; break;
                case ModeOption.Sandbox: template = catalog.sandbox; break;
                default: template = catalog.lastStanding; break;
            }

            MatchRules rules = template != null ? template.CloneRuntime() : MatchRules.CreateDefault();
            rules.teams = mode == ModeOption.Teams;

            switch (mode)
            {
                case ModeOption.Sandbox:
                    rules.winCondition = WinCondition.Sandbox;
                    rules.timeLimit = 0f;
                    rules.displayNameKey = "MODE_SANDBOX";
                    return rules;
                case ModeOption.Stocks:
                    rules.winCondition = WinCondition.Stocks;
                    rules.lives = s_lives;
                    break;
                case ModeOption.Points:
                    rules.winCondition = WinCondition.Points;
                    rules.pointsToWin = PointOptions[s_pointsIndex];
                    break;
                case ModeOption.Teams:
                    rules.winCondition = WinCondition.LastStanding;
                    rules.friendlyFire = s_friendlyFire;
                    break;
                default:
                    rules.winCondition = WinCondition.LastStanding;
                    break;
            }

            rules.timeLimit = TimeOptions[s_timeIndex];
            // Por puntos sin tiempo ni objetivo nunca terminaría: 3 minutos por defecto
            if (mode == ModeOption.Points && rules.timeLimit <= 0f && rules.pointsToWin <= 0)
                rules.timeLimit = 180f;
            return rules;
        }
        #endregion

        #region Restore / navigation
        /// <summary>Al volver de una partida ("Cambiar peonzas") se recuperan los jugadores y su montaje.</summary>
        private void RestoreFromSetup()
        {
            if (!MatchSetup.HasPlayers) return;

            for (int i = 0; i < MatchSetup.Players.Count && _slots.Count < MaxPlayers; i++)
            {
                PlayerSetup setup = MatchSetup.Players[i];
                if (setup.Controller != ControllerType.Human) continue;
                // Un mando desconectado no podría ni salir de su columna: no se restaura
                if (setup.Device == InputDeviceKind.Gamepad && InputDeviceUtil.FindGamepad(setup.GamepadDeviceId) == null) continue;
                AddSlot(setup);
            }
        }

        private void BackToMenu()
        {
            MatchSetup.Clear();
            SceneFlow.Load(SceneFlow.MainMenu);
        }

        private void RefreshTexts()
        {
            RefreshTopBar();
            RebuildColumns();
        }

        private void RefreshTopBar()
        {
            _title.text = Loc.Get("LOBBY_TITLE");
            _backButton.SetLabel("< " + Loc.Get("BACK"));
        }
        #endregion

        #region Labels
        private static string ModeKey(ModeOption mode)
        {
            switch (mode)
            {
                case ModeOption.Stocks: return "MODE_STOCKS";
                case ModeOption.Points: return "MODE_POINTS";
                case ModeOption.Teams: return "MODE_TEAMS";
                case ModeOption.Sandbox: return "MODE_SANDBOX";
                default: return "MODE_LAST_STANDING";
            }
        }

        private static string[] NumberLabels(int from, int to)
        {
            var labels = new string[to - from + 1];
            for (int i = from; i <= to; i++) labels[i - from] = PixelUI.Number(i);
            return labels;
        }

        private static string[] TimeLabels()
        {
            var labels = new string[TimeOptions.Length];
            for (int i = 0; i < TimeOptions.Length; i++)
                labels[i] = TimeOptions[i] == 0 ? Loc.Get("SET_NO_LIMIT") : $"{TimeOptions[i] / 60}:00";
            return labels;
        }

        private static string[] PointLabels()
        {
            var labels = new string[PointOptions.Length];
            for (int i = 0; i < PointOptions.Length; i++)
                labels[i] = PointOptions[i] == 0 ? Loc.Get("SET_NO_TARGET") : PixelUI.Number(PointOptions[i]);
            return labels;
        }

        private string[] ArenaLabels()
        {
            if (catalog.arenas.Count == 0) return new[] { "-" };
            var labels = new string[catalog.arenas.Count];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = catalog.arenas[i] != null ? catalog.arenas[i].DisplayName : "-";
            return labels;
        }

        private static string[] YesNo() => new[] { Loc.Get("NO"), Loc.Get("YES") };
        #endregion
    }
}
