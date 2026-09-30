using UnityEngine;
using UnityEngine.InputSystem;

namespace FakeBlade.Core
{
    /// <summary>Dispositivo asignado a un jugador (GDD 2.2 / 6).</summary>
    public enum InputDeviceKind
    {
        /// <summary>Teclado J1 (por defecto WASD).</summary>
        KeyboardLeft = 0,
        /// <summary>Teclado J2 (por defecto flechas).</summary>
        KeyboardRight = 1,
        Gamepad = 2,
        None = 3
    }

    /// <summary>
    /// Input de un jugador humano con el Input System. Las teclas y botones salen de
    /// InputBindings (reasignables en el menú Controles).
    ///
    /// Por defecto:
    /// Teclado J1: WASD + Espacio (ataque) + Shift izq. (dash) + E (especial)
    /// Teclado J2: Flechas + Ctrl der./Num0 + Shift der./Num1 + Enter/Num2
    /// Mando:      Stick izq./cruceta + A/RB (ataque) + B/RT (dash) + Y/LT (especial)
    ///
    /// La pausa (Esc / Start) la gestiona el GameManager para cualquier jugador.
    /// </summary>
    public class InputHandler : MonoBehaviour, IBladeInputSource
    {
        #region Serialized Fields
        [Header("Dispositivo")]
        [SerializeField] private InputDeviceKind deviceKind = InputDeviceKind.KeyboardLeft;
        [Tooltip("Índice en Gamepad.all cuando deviceKind = Gamepad y no hay deviceId asignado")]
        [SerializeField] private int gamepadIndex = 0;

        [Header("Ajustes")]
        [SerializeField][Range(0f, 0.9f)] private float deadzone = 0.2f;
        [Tooltip("Tiempo que se recuerda una pulsación de dash/especial")]
        [SerializeField] private float inputBufferTime = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool debugInput = false;
        #endregion

        #region Private Fields
        private int _gamepadDeviceId;
        private Gamepad _gamepad;
        private Vector2 _move;
        private bool _attackHeld;
        private float _dashBuffer;
        private float _specialBuffer;
        private float _vibrationEndTime = -1f;
        #endregion

        #region Properties
        public InputDeviceKind DeviceKind => deviceKind;
        public int GamepadIndex => gamepadIndex;
        public int GamepadDeviceId => _gamepad != null ? _gamepad.deviceId : _gamepadDeviceId;
        public bool IsUsingGamepad => deviceKind == InputDeviceKind.Gamepad;
        public bool HasDevice => deviceKind == InputDeviceKind.Gamepad ? _gamepad != null : deviceKind != InputDeviceKind.None;

        public Vector2 MovementInput => _move;
        public bool AttackHeld => _attackHeld;
        #endregion

        #region Unity Lifecycle
        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            ResolveGamepad();
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            StopVibration();
            _move = Vector2.zero;
            _attackHeld = false;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_dashBuffer > 0f) _dashBuffer -= dt;
            if (_specialBuffer > 0f) _specialBuffer -= dt;

            if (_vibrationEndTime > 0f && Time.unscaledTime >= _vibrationEndTime)
                StopVibration();

            switch (deviceKind)
            {
                case InputDeviceKind.KeyboardLeft:
                case InputDeviceKind.KeyboardRight:
                    ReadKeyboard(InputBindings.GetKeyboard(deviceKind));
                    break;
                case InputDeviceKind.Gamepad:
                    ReadGamepad();
                    break;
                default:
                    _move = Vector2.zero;
                    _attackHeld = false;
                    break;
            }
        }
        #endregion

        #region Reading
        private void ReadKeyboard(KeyboardScheme scheme)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                _move = Vector2.zero;
                _attackHeld = false;
                return;
            }

            Vector2 move = Vector2.zero;
            if (IsPressed(kb, scheme.up)) move.y += 1f;
            if (IsPressed(kb, scheme.down)) move.y -= 1f;
            if (IsPressed(kb, scheme.left)) move.x -= 1f;
            if (IsPressed(kb, scheme.right)) move.x += 1f;
            if (move.sqrMagnitude > 1f) move.Normalize();
            _move = move;

            _attackHeld = IsPressed(kb, scheme.attack) || IsPressed(kb, scheme.attackAlt);

            if (WasPressed(kb, scheme.dash) || WasPressed(kb, scheme.dashAlt))
                BufferDash();
            if (WasPressed(kb, scheme.special) || WasPressed(kb, scheme.specialAlt))
                BufferSpecial();
        }

        private void ReadGamepad()
        {
            if (_gamepad == null)
            {
                ResolveGamepad();
                if (_gamepad == null)
                {
                    _move = Vector2.zero;
                    _attackHeld = false;
                    return;
                }
            }

            Vector2 stick = _gamepad.leftStick.ReadValue();
            Vector2 dpad = _gamepad.dpad.ReadValue();
            Vector2 move = stick.sqrMagnitude >= dpad.sqrMagnitude ? stick : dpad;
            _move = move.sqrMagnitude < deadzone * deadzone ? Vector2.zero : Vector2.ClampMagnitude(move, 1f);

            GamepadScheme s = InputBindings.Pad;
            _attackHeld = _gamepad[s.attack].isPressed || _gamepad[s.attackAlt].isPressed;

            if (_gamepad[s.dash].wasPressedThisFrame || _gamepad[s.dashAlt].wasPressedThisFrame)
                BufferDash();
            if (_gamepad[s.special].wasPressedThisFrame || _gamepad[s.specialAlt].wasPressedThisFrame)
                BufferSpecial();
        }

        private static bool IsPressed(Keyboard kb, Key key) => key != Key.None && kb[key].isPressed;
        private static bool WasPressed(Keyboard kb, Key key) => key != Key.None && kb[key].wasPressedThisFrame;

        private void BufferDash()
        {
            _dashBuffer = inputBufferTime;
            if (debugInput) Debug.Log($"[InputHandler] {name} dash", this);
        }

        private void BufferSpecial()
        {
            _specialBuffer = inputBufferTime;
            if (debugInput) Debug.Log($"[InputHandler] {name} special", this);
        }
        #endregion

        #region Public Interface
        public Vector2 GetMovementInput() => _move;

        public bool ConsumeDash()
        {
            if (_dashBuffer <= 0f) return false;
            _dashBuffer = 0f;
            return true;
        }

        public bool ConsumeSpecial()
        {
            if (_specialBuffer <= 0f) return false;
            _specialBuffer = 0f;
            return true;
        }

        public void ClearBuffers()
        {
            _dashBuffer = 0f;
            _specialBuffer = 0f;
        }

        /// <summary>Asigna teclado o mando por índice en Gamepad.all.</summary>
        public void SetDevice(InputDeviceKind kind, int padIndex = 0)
        {
            StopVibration();
            deviceKind = kind;
            gamepadIndex = Mathf.Max(0, padIndex);
            _gamepadDeviceId = 0;
            ResolveGamepad();

            if (debugInput)
                Debug.Log($"[InputHandler] {name} → {kind}{(kind == InputDeviceKind.Gamepad ? $" #{gamepadIndex}" : "")}", this);
        }

        /// <summary>Asigna un mando concreto por deviceId (estable aunque cambie el orden de conexión).</summary>
        public void SetDeviceById(InputDeviceKind kind, int gamepadDeviceId)
        {
            StopVibration();
            deviceKind = kind;
            _gamepadDeviceId = gamepadDeviceId;
            ResolveGamepad();
        }

        /// <summary>Compatibilidad: índice &lt; 0 = teclado J1.</summary>
        public void SetGamepadIndex(int index)
        {
            if (index < 0) SetDevice(InputDeviceKind.KeyboardLeft);
            else SetDevice(InputDeviceKind.Gamepad, index);
        }

        public void SetDeadzone(float value) => deadzone = Mathf.Clamp(value, 0f, 0.9f);
        #endregion

        #region Devices
        private void ResolveGamepad()
        {
            _gamepad = null;
            if (deviceKind != InputDeviceKind.Gamepad) return;

            if (_gamepadDeviceId > 0)
            {
                _gamepad = InputDeviceUtil.FindGamepad(_gamepadDeviceId);
                return;
            }

            var pads = Gamepad.all;
            if (gamepadIndex < pads.Count)
                _gamepad = pads[gamepadIndex];
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad)) return;

            if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed ||
                change == InputDeviceChange.Reconnected || change == InputDeviceChange.Disconnected)
            {
                ResolveGamepad();
            }
        }
        #endregion

        #region Vibration
        public void Vibrate(float lowFreq, float highFreq, float duration)
        {
            if (_gamepad == null || !CombatConfig.Active.gamepadVibration || !SettingsService.Current.vibration) return;

            _gamepad.SetMotorSpeeds(lowFreq, highFreq);
            _vibrationEndTime = Time.unscaledTime + duration;
        }

        public void StopVibration()
        {
            _vibrationEndTime = -1f;
            _gamepad?.SetMotorSpeeds(0f, 0f);
        }
        #endregion
    }

    /// <summary>
    /// Asignación de dispositivos por defecto para partidas de prueba (sin selección):
    /// J1 = teclado WASD; J2 = primer mando o, si no hay mandos, teclado con flechas; J3-J4 = siguientes mandos.
    /// </summary>
    public static class InputAssignment
    {
        public static void GetDefault(int playerIndex, out InputDeviceKind kind, out int gamepadIndex)
        {
            gamepadIndex = 0;
            if (playerIndex <= 0)
            {
                kind = InputDeviceKind.KeyboardLeft;
                return;
            }

            int pads = Gamepad.all.Count;
            bool secondKeyboardUsed = pads == 0;

            if (playerIndex == 1 && secondKeyboardUsed)
            {
                kind = InputDeviceKind.KeyboardRight;
                return;
            }

            kind = InputDeviceKind.Gamepad;
            gamepadIndex = playerIndex - 1 - (secondKeyboardUsed ? 1 : 0);
        }
    }
}
