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
    /// Controles (GDD 9.2.4): reasignación de Teclado J1, Teclado J2 y Mando.
    /// Confirmar sobre una acción → pulsar la tecla/botón nuevo. Esc (o Start) cancela.
    /// Si la tecla ya se usaba, InputBindings la intercambia con la otra acción.
    /// </summary>
    public class ControlsScreen : MenuScreen
    {
        private const int RowHeight = 9;
        private static readonly BindingAction[] AllActions = (BindingAction[])Enum.GetValues(typeof(BindingAction));

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

        protected override void OnOpened() => RefreshRows();

        protected override void OnLanguageRefreshed()
        {
            _schemeRow.SetOptions(SchemeLabels(), SchemeIndex());
            RefreshRows();
        }

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
