using UnityEngine;
using UnityEngine.InputSystem;

namespace FakeBlade.Core
{
    /// <summary>Utilidades para identificar mandos de forma estable (por deviceId).</summary>
    public static class InputDeviceUtil
    {
        public static Gamepad FindGamepad(int deviceId)
        {
            if (deviceId <= 0) return null;
            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
                if (pads[i].deviceId == deviceId) return pads[i];
            return null;
        }

        public static int GamepadIdAt(int index)
        {
            var pads = Gamepad.all;
            return index >= 0 && index < pads.Count ? pads[index].deviceId : 0;
        }

        /// <summary>Nombre corto del dispositivo para la UI.</summary>
        public static string DeviceLabel(InputDeviceKind kind, int gamepadDeviceId)
        {
            switch (kind)
            {
                case InputDeviceKind.KeyboardLeft: return Loc.Get("DEVICE_KB_LEFT");
                case InputDeviceKind.KeyboardRight: return Loc.Get("DEVICE_KB_RIGHT");
                case InputDeviceKind.Gamepad:
                    var pads = Gamepad.all;
                    for (int i = 0; i < pads.Count; i++)
                        if (pads[i].deviceId == gamepadDeviceId) return Loc.Format("DEVICE_PAD", i + 1);
                    return Loc.Format("DEVICE_PAD", "?");
                default: return string.Empty;
            }
        }
    }

    /// <summary>
    /// Lectura de menú de UN dispositivo (cada columna de la selección de peonzas).
    ///
    /// Teclado: movimiento del esquema; confirmar = ataque; atrás = dash (+ Esc en J1, Retroceso en J2).
    /// Mando:   stick/cruceta; confirmar = A; atrás = B.
    /// Las direcciones se repiten al mantenerlas (retardo + cadencia).
    /// </summary>
    public sealed class DeviceMenuInput
    {
        private const float RepeatDelay = 0.35f;
        private const float RepeatRate = 0.11f;
        private const float StickThreshold = 0.55f;

        public InputDeviceKind Kind { get; private set; }
        public int GamepadDeviceId { get; private set; }

        public bool Up { get; private set; }
        public bool Down { get; private set; }
        public bool Left { get; private set; }
        public bool Right { get; private set; }
        public bool Confirm { get; private set; }
        public bool Back { get; private set; }
        public bool ConfirmHeld { get; private set; }
        public bool BackHeld { get; private set; }

        private Vector2Int _heldDir;
        private float _repeatTimer;

        public DeviceMenuInput(InputDeviceKind kind, int gamepadDeviceId)
        {
            Kind = kind;
            GamepadDeviceId = gamepadDeviceId;
        }

        public bool Matches(InputDeviceKind kind, int gamepadDeviceId) =>
            Kind == kind && (kind != InputDeviceKind.Gamepad || GamepadDeviceId == gamepadDeviceId);

        public void Update(float dt)
        {
            Up = Down = Left = Right = Confirm = Back = false;

            Vector2Int dir = ReadDirection(Kind, GamepadDeviceId);
            if (dir != Vector2Int.zero)
            {
                bool fire = false;
                if (dir != _heldDir)
                {
                    fire = true;
                    _repeatTimer = RepeatDelay;
                }
                else
                {
                    _repeatTimer -= dt;
                    if (_repeatTimer <= 0f)
                    {
                        fire = true;
                        _repeatTimer = RepeatRate;
                    }
                }

                if (fire)
                {
                    Up = dir.y > 0;
                    Down = dir.y < 0;
                    Left = dir.x < 0;
                    Right = dir.x > 0;
                }
            }
            _heldDir = dir;

            Confirm = ConfirmPressed(Kind, GamepadDeviceId);
            Back = BackPressed(Kind, GamepadDeviceId);
            ConfirmHeld = IsConfirmHeld(Kind, GamepadDeviceId);
            BackHeld = IsBackHeld(Kind, GamepadDeviceId);
        }

        #region Static readers
        public static Vector2Int ReadDirection(InputDeviceKind kind, int padId)
        {
            Vector2 v = Vector2.zero;
            if (kind == InputDeviceKind.Gamepad)
            {
                Gamepad pad = InputDeviceUtil.FindGamepad(padId);
                if (pad == null) return Vector2Int.zero;
                v = pad.dpad.ReadValue();
                if (v.sqrMagnitude < 0.01f) v = pad.leftStick.ReadValue();
            }
            else if (kind == InputDeviceKind.KeyboardLeft || kind == InputDeviceKind.KeyboardRight)
            {
                Keyboard kb = Keyboard.current;
                if (kb == null) return Vector2Int.zero;
                KeyboardScheme s = InputBindings.GetKeyboard(kind);
                if (Held(kb, s.up)) v.y += 1f;
                if (Held(kb, s.down)) v.y -= 1f;
                if (Held(kb, s.left)) v.x -= 1f;
                if (Held(kb, s.right)) v.x += 1f;
            }

            // Solo una dirección cada vez (la dominante)
            if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
                return Mathf.Abs(v.x) >= StickThreshold ? new Vector2Int(v.x > 0 ? 1 : -1, 0) : Vector2Int.zero;
            return Mathf.Abs(v.y) >= StickThreshold ? new Vector2Int(0, v.y > 0 ? 1 : -1) : Vector2Int.zero;
        }

        public static bool IsConfirmHeld(InputDeviceKind kind, int padId)
        {
            if (kind == InputDeviceKind.Gamepad)
            {
                Gamepad pad = InputDeviceUtil.FindGamepad(padId);
                return pad != null && pad.buttonSouth.isPressed;
            }
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            KeyboardScheme s = InputBindings.GetKeyboard(kind);
            return Held(kb, s.attack) || Held(kb, s.attackAlt);
        }

        public static bool ConfirmPressed(InputDeviceKind kind, int padId)
        {
            if (kind == InputDeviceKind.Gamepad)
            {
                Gamepad pad = InputDeviceUtil.FindGamepad(padId);
                return pad != null && pad.buttonSouth.wasPressedThisFrame;
            }
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            KeyboardScheme s = InputBindings.GetKeyboard(kind);
            return Pressed(kb, s.attack) || Pressed(kb, s.attackAlt);
        }

        public static bool IsBackHeld(InputDeviceKind kind, int padId)
        {
            if (kind == InputDeviceKind.Gamepad)
            {
                Gamepad pad = InputDeviceUtil.FindGamepad(padId);
                return pad != null && pad.buttonEast.isPressed;
            }
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            KeyboardScheme s = InputBindings.GetKeyboard(kind);
            Key extra = kind == InputDeviceKind.KeyboardLeft ? Key.Escape : Key.Backspace;
            return Held(kb, s.dash) || Held(kb, s.dashAlt) || Held(kb, extra);
        }

        public static bool BackPressed(InputDeviceKind kind, int padId)
        {
            if (kind == InputDeviceKind.Gamepad)
            {
                Gamepad pad = InputDeviceUtil.FindGamepad(padId);
                return pad != null && pad.buttonEast.wasPressedThisFrame;
            }
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            KeyboardScheme s = InputBindings.GetKeyboard(kind);
            Key extra = kind == InputDeviceKind.KeyboardLeft ? Key.Escape : Key.Backspace;
            return Pressed(kb, s.dash) || Pressed(kb, s.dashAlt) || Pressed(kb, extra);
        }

        private static bool Held(Keyboard kb, Key key) => key != Key.None && kb[key].isPressed;
        private static bool Pressed(Keyboard kb, Key key) => key != Key.None && kb[key].wasPressedThisFrame;
        #endregion
    }

    /// <summary>Entrada de menú de cualquier dispositivo (menús de un solo usuario).</summary>
    public static class MenuInput
    {
        /// <summary>Atrás: Esc/Retroceso o B de cualquier mando.</summary>
        public static bool AnyBackPressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
                return true;

            var pads = Gamepad.all;
            for (int i = 0; i < pads.Count; i++)
                if (pads[i].buttonEast.wasPressedThisFrame) return true;
            return false;
        }
    }
}
