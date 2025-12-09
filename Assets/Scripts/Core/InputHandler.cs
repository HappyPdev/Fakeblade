using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace FakeBlade.Core
{
    /// <summary>
    /// Manejador de input optimizado con soporte para múltiples dispositivos.
    /// Usa el nuevo Input System de Unity con input buffering.
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        #region Input Configuration
        [System.Serializable]
        public class InputConfig
        {
            [Header("Keyboard Bindings")]
            public KeyCode moveUp = KeyCode.W;
            public KeyCode moveDown = KeyCode.S;
            public KeyCode moveLeft = KeyCode.A;
            public KeyCode moveRight = KeyCode.D;
            public KeyCode dash = KeyCode.Space;
            public KeyCode special = KeyCode.LeftShift;

            [Header("Gamepad Settings")]
            public int gamepadIndex = -1; // -1 = keyboard
            public float deadzone = 0.15f;
            public float sensitivity = 1f;
        }
        #endregion

        #region Events
        public event Action OnDashPressed;
        public event Action OnSpecialPressed;
        public event Action<Vector2> OnMovementChanged;
        #endregion

        #region Serialized Fields
        [Header("Input Configuration")]
        [SerializeField] private InputConfig config = new InputConfig();

        [Header("Input Buffering")]
        [SerializeField] private float inputBufferTime = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool debugInput = false;
        #endregion

        #region Private Fields
        private Vector2 _movementInput;
        private Vector2 _smoothedMovement;
        private float _dashBufferTimer;
        private float _specialBufferTimer;
        private bool _dashBuffered;
        private bool _specialBuffered;
        private Gamepad _assignedGamepad;
        private bool _isInitialized;

        // Cache para evitar allocations
        private readonly Vector2[] _keyboardDirections = new Vector2[4];
        #endregion

        #region Properties
        public Vector2 MovementInput => _smoothedMovement;
        public bool IsUsingGamepad => _assignedGamepad != null;
        public int GamepadIndex => config.gamepadIndex;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            InitializeKeyboardDirections();
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!_isInitialized) return;

            UpdateMovementInput();
            UpdateActionInputs();
            UpdateBuffers();
        }

        private void OnEnable()
        {
            // Suscribirse a cambios de dispositivos
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }
        #endregion

        #region Initialization
        private void InitializeKeyboardDirections()
        {
            _keyboardDirections[0] = Vector2.up;
            _keyboardDirections[1] = Vector2.down;
            _keyboardDirections[2] = Vector2.left;
            _keyboardDirections[3] = Vector2.right;
        }

        private void Initialize()
        {
            AssignGamepad();
            _isInitialized = true;

            if (debugInput)
            {
                string device = _assignedGamepad != null ? $"Gamepad {config.gamepadIndex}" : "Keyboard";
                Debug.Log($"[InputHandler] Initialized with {device}");
            }
        }

        private void AssignGamepad()
        {
            if (config.gamepadIndex < 0)
            {
                _assignedGamepad = null;
                return;
            }

            var gamepads = Gamepad.all;
            if (config.gamepadIndex < gamepads.Count)
            {
                _assignedGamepad = gamepads[config.gamepadIndex];
            }
            else
            {
                Debug.LogWarning($"[InputHandler] Gamepad {config.gamepadIndex} not found. Using keyboard.");
                _assignedGamepad = null;
            }
        }
        #endregion

        #region Input Reading
        private void UpdateMovementInput()
        {
            Vector2 rawInput;

            if (_assignedGamepad != null)
            {
                rawInput = ReadGamepadMovement();
            }
            else
            {
                rawInput = ReadKeyboardMovement();
            }

            // Aplicar deadzone
            if (rawInput.sqrMagnitude < config.deadzone * config.deadzone)
            {
                rawInput = Vector2.zero;
            }

            // Input más directo - menos suavizado para mayor respuesta
            _smoothedMovement = Vector2.Lerp(_smoothedMovement, rawInput, Time.deltaTime * 25f);

            // Para teclado, usar input directo sin suavizar
            if (_assignedGamepad == null && rawInput.sqrMagnitude > 0.01f)
            {
                _smoothedMovement = rawInput;
            }

            // Notificar cambio significativo
            if (Vector2.Distance(_movementInput, rawInput) > 0.01f)
            {
                _movementInput = rawInput;
                OnMovementChanged?.Invoke(_smoothedMovement);
            }
        }

        private Vector2 ReadGamepadMovement()
        {
            if (_assignedGamepad == null) return Vector2.zero;

            Vector2 stick = _assignedGamepad.leftStick.ReadValue();
            return stick * config.sensitivity;
        }

        private Vector2 ReadKeyboardMovement()
        {
            Vector2 result = Vector2.zero;

            if (Input.GetKey(config.moveUp)) result += _keyboardDirections[0];
            if (Input.GetKey(config.moveDown)) result += _keyboardDirections[1];
            if (Input.GetKey(config.moveLeft)) result += _keyboardDirections[2];
            if (Input.GetKey(config.moveRight)) result += _keyboardDirections[3];

            // Normalizar diagonal
            if (result.sqrMagnitude > 1f)
            {
                result.Normalize();
            }

            return result * config.sensitivity;
        }

        private void UpdateActionInputs()
        {
            // Dash
            if (ReadDashInput())
            {
                _dashBuffered = true;
                _dashBufferTimer = inputBufferTime;
                OnDashPressed?.Invoke();

                if (debugInput)
                {
                    Debug.Log("[InputHandler] Dash input detected");
                }
            }

            // Special
            if (ReadSpecialInput())
            {
                _specialBuffered = true;
                _specialBufferTimer = inputBufferTime;
                OnSpecialPressed?.Invoke();

                if (debugInput)
                {
                    Debug.Log("[InputHandler] Special input detected");
                }
            }
        }

        private bool ReadDashInput()
        {
            if (_assignedGamepad != null)
            {
                return _assignedGamepad.buttonSouth.wasPressedThisFrame ||
                       _assignedGamepad.rightTrigger.wasPressedThisFrame;
            }
            return Input.GetKeyDown(config.dash);
        }

        private bool ReadSpecialInput()
        {
            if (_assignedGamepad != null)
            {
                return _assignedGamepad.buttonWest.wasPressedThisFrame ||
                       _assignedGamepad.leftTrigger.wasPressedThisFrame;
            }
            return Input.GetKeyDown(config.special);
        }

        private void UpdateBuffers()
        {
            if (_dashBuffered)
            {
                _dashBufferTimer -= Time.deltaTime;
                if (_dashBufferTimer <= 0f)
                {
                    _dashBuffered = false;
                }
            }

            if (_specialBuffered)
            {
                _specialBufferTimer -= Time.deltaTime;
                if (_specialBufferTimer <= 0f)
                {
                    _specialBuffered = false;
                }
            }
        }
        #endregion

        #region Public Interface
        public Vector2 GetMovementInput()
        {
            return _smoothedMovement;
        }

        public bool GetDashInput()
        {
            if (_dashBuffered)
            {
                _dashBuffered = false;
                return true;
            }
            return false;
        }

        public bool GetSpecialInput()
        {
            if (_specialBuffered)
            {
                _specialBuffered = false;
                return true;
            }
            return false;
        }

        public void SetGamepadIndex(int index)
        {
            config.gamepadIndex = index;
            AssignGamepad();
        }

        public void SetKeyboardBindings(KeyCode up, KeyCode down, KeyCode left, KeyCode right, KeyCode dash, KeyCode special)
        {
            config.moveUp = up;
            config.moveDown = down;
            config.moveLeft = left;
            config.moveRight = right;
            config.dash = dash;
            config.special = special;
        }

        public void SetDeadzone(float deadzone)
        {
            config.deadzone = Mathf.Clamp01(deadzone);
        }

        public void SetSensitivity(float sensitivity)
        {
            config.sensitivity = Mathf.Clamp(sensitivity, 0.1f, 2f);
        }
        #endregion

        #region Device Management
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad gamepad)
            {
                switch (change)
                {
                    case InputDeviceChange.Added:
                        if (debugInput)
                            Debug.Log($"[InputHandler] Gamepad connected: {gamepad.displayName}");
                        break;

                    case InputDeviceChange.Removed:
                        if (_assignedGamepad == gamepad)
                        {
                            Debug.LogWarning($"[InputHandler] Assigned gamepad disconnected!");
                            _assignedGamepad = null;
                        }
                        break;
                }
            }
        }
        #endregion

        #region Vibration (Gamepad)
        public void Vibrate(float lowFreq, float highFreq, float duration)
        {
            if (_assignedGamepad == null) return;

            _assignedGamepad.SetMotorSpeeds(lowFreq, highFreq);
            Invoke(nameof(StopVibration), duration);
        }

        private void StopVibration()
        {
            _assignedGamepad?.SetMotorSpeeds(0f, 0f);
        }
        #endregion
    }
}