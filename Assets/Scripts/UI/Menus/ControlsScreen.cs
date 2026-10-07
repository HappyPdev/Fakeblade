using System;
using FakeBlade.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FakeBlade.UI
{
    /// <summary>
    /// Controles (GDD 9.2.4): dispositivo de cada jugador y reasignación de Teclado J1, Teclado J2 y Mando.
    ///
    /// Dispositivos: una fila por jugador humano de la partida (en el menú principal, J1 y J2) con
    /// Teclado J1 / Teclado J2 / Mando 1..N. En partida se aplica al momento; si otro jugador ya usaba
    /// ese dispositivo, se intercambian. Se guarda como preferencia para las partidas que se abren sin
    /// pasar por la selección (escena abierta desde el editor).
    ///
    /// Teclas: confirmar sobre una acción → pulsar la tecla/botón nuevo. Esc (o Start) cancela.
    /// Si la tecla ya se usaba, InputBindings la intercambia con la otra acción.
    /// </summary>
    public class ControlsScreen : MenuScreen
    {
        private const int RowHeight = 9;
        private const int MaxDeviceRows = 4;
        private const int MenuDeviceRows = 2;
        private static readonly BindingAction[] AllActions = (BindingAction[])Enum.GetValues(typeof(BindingAction));

        private readonly PixelOptionRow[] _deviceRows = new PixelOptionRow[MaxDeviceRows];
        /// <summary>Jugador de cada fila de dispositivo (null en el menú principal: solo preferencia).</summary>
        private readonly PlayerController[] _devicePlayers = new PlayerController[MaxDeviceRows];
        private int _padCount = -1;

        private PixelOptionRow _schemeRow;
        private readonly PixelOptionRow[] _actionRows = new PixelOptionRow[AllActions.Length];
        private TextMeshProUGUI _hint;

        private InputDeviceKind _scheme = InputDeviceKind.KeyboardLeft;
        private bool _capturing;
        private BindingAction _captureAction;
        private int _captureFrame;
        private float _blinkTime;

        protected override bool CanCloseWithBack => !_capturing;

        public static ControlsScreen Create(Transform parent, HUDTheme theme, Action onClose)
        {
            var screen = CreateScreen<ControlsScreen>("ControlsScreen", parent);
            screen.Initialize(theme, "CONTROLS", 150, onClose);
            return screen;
        }

        protected override void BuildContent()
        {
            List.AddText("CTRL_DEVICES", 5, Theme.textDimColor, TextAlignmentOptions.Center, 8);
            for (int i = 0; i < MaxDeviceRows; i++)
            {
                int row = i;
                _deviceRows[i] = List.AddSelector("PLAYER_BADGE", DeviceLabels(), 0, o => OnDeviceChanged(row, o), RowHeight);
                _deviceRows[i].LabelKey = null; // la etiqueta (J1, J2...) la pone RefreshDevices
            }

            List.AddSpacer(4);

            _schemeRow = List.AddSelector("CTRL_SCHEME", SchemeLabels(), 0, i =>
            {
                _scheme = i == 0 ? InputDeviceKind.KeyboardLeft : i == 1 ? InputDeviceKind.KeyboardRight : InputDeviceKind.Gamepad;
                RefreshRows();
            }, RowHeight);

            List.AddSpacer(2);

            for (int i = 0; i < AllActions.Length; i++)
            {
                BindingAction action = AllActions[i];
                _actionRows[i] = List.AddCustom("ACTION_" + action.ToString().ToUpperInvariant(), string.Empty,
                    null, () => BeginCapture(action), RowHeight);
            }

            List.AddSpacer(2);
            _hint = List.AddText("CTRL_HINT", 4, Theme.textDimColor, TextAlignmentOptions.Center, 8);
            List.AddButton("CTRL_RESET", () =>
            {
                InputBindings.ResetDefaults();
                RefreshRows();
            }, RowHeight);
            List.AddButton("BACK", Close, RowHeight);
        }

        protected override void OnOpened()
        {
            RefreshDevices();
            RefreshRows();
        }

        protected override void OnLanguageRefreshed()
        {
            _schemeRow.SetOptions(SchemeLabels(), SchemeIndex());
            RefreshDevices();
            RefreshRows();
        }

        #region Dispositivos
        /// <summary>Teclado J1, Teclado J2 y un "Mando N" por mando conectado (InputAssignment.OptionCount).</summary>
        private static string[] DeviceLabels()
        {
            var labels = new string[InputAssignment.OptionCount];
            labels[0] = Loc.Get("DEVICE_KB_LEFT");
            labels[1] = Loc.Get("DEVICE_KB_RIGHT");
            for (int i = 2; i < labels.Length; i++) labels[i] = Loc.Format("DEVICE_PAD", i - 1);
            return labels;
        }

        /// <summary>
        /// Filas visibles: en partida, los jugadores humanos (los dummies y la IA no); en el menú
        /// principal, J1 y J2 con su preferencia guardada.
        /// </summary>
        private void RefreshDevices()
        {
            _padCount = UnityEngine.InputSystem.Gamepad.all.Count;
            string[] labels = DeviceLabels();
            Array.Clear(_devicePlayers, 0, _devicePlayers.Length);

            var gm = GameManager.Instance;
            int shown = 0;
            if (gm != null)
            {
                foreach (PlayerController player in gm.Players)
                {
                    if (player == null || !player.IsHumanControlled || shown >= MaxDeviceRows) continue;
                    _devicePlayers[shown] = player;
                    InputHandler input = player.Input;
                    SetDeviceRow(shown, player.PlayerID, labels, InputAssignment.ToOption(input.DeviceKind, input.GamepadDeviceId));
                    shown++;
                }
            }

            // Menú principal (o partida sin humanos, como el fondo del menú): solo la preferencia
            if (shown == 0)
            {
                for (; shown < MenuDeviceRows; shown++)
                {
                    InputAssignment.GetDefault(shown, out InputDeviceKind kind, out int padIndex);
                    SetDeviceRow(shown, shown, labels, InputAssignment.ToOption(kind, InputDeviceUtil.GamepadIdAt(padIndex)));
                }
            }

            for (int i = 0; i < MaxDeviceRows; i++) _deviceRows[i].gameObject.SetActive(i < shown);
        }

        private void SetDeviceRow(int row, int playerIndex, string[] labels, int option)
        {
            _deviceRows[row].SetLabel(Loc.Format("PLAYER_BADGE", playerIndex + 1));
            _deviceRows[row].SetOptions(labels, option);
        }

        private void OnDeviceChanged(int row, int option)
        {
            PlayerController player = _devicePlayers[row];
            int playerIndex = player != null ? player.PlayerID : row;
            InputAssignment.SetPreference(playerIndex, option);

            if (player != null)
            {
                // Si otro jugador ya tenía ese dispositivo, se queda con el que deja este (intercambio)
                int previous = InputAssignment.ToOption(player.Input.DeviceKind, player.Input.GamepadDeviceId);
                for (int i = 0; i < MaxDeviceRows; i++)
                {
                    PlayerController other = _devicePlayers[i];
                    if (other == null || other == player) continue;
                    if (InputAssignment.ToOption(other.Input.DeviceKind, other.Input.GamepadDeviceId) != option) continue;
                    ApplyDevice(other, previous);
                    InputAssignment.SetPreference(other.PlayerID, previous);
                }
                ApplyDevice(player, option);
            }

            RefreshDevices();
        }

        /// <summary>Cambia el dispositivo de la peonza al momento y en MatchSetup (para reinicios y revanchas).</summary>
        private static void ApplyDevice(PlayerController player, int option)
        {
            InputAssignment.FromOption(option, out InputDeviceKind kind, out int padIndex);
            int deviceId = kind == InputDeviceKind.Gamepad ? InputDeviceUtil.GamepadIdAt(padIndex) : 0;
            player.SetInputDeviceById(kind, deviceId);

            foreach (PlayerSetup setup in MatchSetup.Players)
            {
                if (setup.PlayerIndex != player.PlayerID) continue;
                setup.Device = kind;
                setup.GamepadDeviceId = deviceId;
            }
        }
        #endregion

        private static string[] SchemeLabels() =>
            new[] { Loc.Get("DEVICE_KB_LEFT"), Loc.Get("DEVICE_KB_RIGHT"), Loc.Get("DEVICE_PAD_ANY") };

        private int SchemeIndex() => _scheme == InputDeviceKind.KeyboardLeft ? 0 : _scheme == InputDeviceKind.KeyboardRight ? 1 : 2;

        private void RefreshRows()
        {
            bool pad = _scheme == InputDeviceKind.Gamepad;
            for (int i = 0; i < AllActions.Length; i++)
            {
                BindingAction action = AllActions[i];
                bool isMovement = action <= BindingAction.Right;
                PixelOptionRow row = _actionRows[i];

                // En mando el movimiento es fijo (stick/cruceta): esas filas se ocultan
                row.gameObject.SetActive(!(pad && isMovement));
                row.SetValueText(pad ? InputBindings.ButtonName(InputBindings.Pad.Get(action))
                                     : InputBindings.KeyName(InputBindings.GetKeyboard(_scheme).Get(action)));
                row.SetValueColor(Theme.chargeReady);
            }
        }

        #region Capture
        private void BeginCapture(BindingAction action)
        {
            _capturing = true;
            _captureAction = action;
            _captureFrame = Time.frameCount;
            _blinkTime = 0f;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
            _hint.text = Loc.Get(_scheme == InputDeviceKind.Gamepad ? "CTRL_PRESS_BUTTON" : "CTRL_PRESS_KEY");
        }

        private void EndCapture()
        {
            _capturing = false;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
            _hint.text = Loc.Get("CTRL_HINT");
            RefreshRows();
        }

        protected override void Update()
        {
            base.Update();

            // Se ha conectado o desconectado un mando con el menú abierto
            if (!_capturing && UnityEngine.InputSystem.Gamepad.all.Count != _padCount) RefreshDevices();

            if (!_capturing) return;

            // Parpadeo del valor mientras se espera
            _blinkTime += Time.unscaledDeltaTime;
            int rowIndex = Array.IndexOf(AllActions, _captureAction);
            _actionRows[rowIndex].SetValueText(PixelUI.Blink(_blinkTime, 3f) ? "..." : string.Empty);

            // Ignorar el frame en el que se pulsó Confirmar
            if (Time.frameCount <= _captureFrame) return;

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                EndCapture();
                return;
            }

            if (_scheme == InputDeviceKind.Gamepad) CaptureGamepad();
            else CaptureKeyboard(kb);
        }

        private void CaptureKeyboard(Keyboard kb)
        {
            if (kb == null) return;

            var keys = kb.allKeys;
            for (int i = 0; i < keys.Count; i++)
            {
                KeyControl key = keys[i];
                if (key == null || !key.wasPressedThisFrame || key.keyCode == Key.Escape) continue;

                InputBindings.SetKey(_scheme, _captureAction, key.keyCode);
                EndCapture();
                return;
            }
        }

        private void CaptureGamepad()
        {
            var pads = Gamepad.all;
            for (int p = 0; p < pads.Count; p++)
            {
                Gamepad pad = pads[p];
                if (pad.startButton.wasPressedThisFrame)
                {
                    EndCapture();
                    return;
                }

                var buttons = InputBindings.AssignableButtons;
                for (int b = 0; b < buttons.Length; b++)
                {
                    if (!pad[buttons[b]].wasPressedThisFrame) continue;
                    InputBindings.SetButton(_captureAction, buttons[b]);
                    EndCapture();
                    return;
                }
            }
        }
        #endregion

        protected override void OnDestroy()
        {
            if (_capturing && EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
            base.OnDestroy();
        }
    }
}
