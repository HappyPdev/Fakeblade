using UnityEngine;
using System;

namespace FakeBlade.Core
{
    /// <summary>
    /// Controlador de jugador individual. 
    /// Gestiona input, estado de vida y vinculación con el sistema de juego.
    /// </summary>
    [RequireComponent(typeof(FakeBladeController))]
    [RequireComponent(typeof(InputHandler))]
    [RequireComponent(typeof(FakeBladeStats))]
    public class PlayerController : MonoBehaviour
    {
        #region Events
        public event Action<int> OnPlayerDefeated;
        public event Action<int> OnPlayerReady;
        public event Action<float> OnSpinChanged;
        #endregion

        #region Serialized Fields
        [Header("Player Info")]
        [SerializeField] private int playerID = 0;
        [SerializeField] private string playerName = "Player";
        [SerializeField] private Color playerColor = Color.white;
        [SerializeField] private int teamID = 0;

        [Header("Visual")]
        [SerializeField] private Renderer[] coloredRenderers;

        [Header("UI References")]
        [SerializeField] private Transform uiAnchor;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        #endregion

        #region Private Fields
        // Cached components
        private FakeBladeController _fakeBladeController;
        private InputHandler _inputHandler;
        private FakeBladeStats _stats;
        private Transform _transform;

        // State
        private bool _isAlive = true;
        private bool _isInitialized;
        private bool _isReady;

        // Visual
        private MaterialPropertyBlock _propertyBlock;
        private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionProperty = Shader.PropertyToID("_EmissionColor");
        #endregion

        #region Properties
        public int PlayerID => playerID;
        public string PlayerName => playerName;
        public Color PlayerColor => playerColor;
        public int TeamID => teamID;
        public bool IsAlive => _isAlive;
        public bool IsReady => _isReady;
        public FakeBladeController FakeBladeController => _fakeBladeController;
        public FakeBladeStats Stats => _stats;
        public Transform UIAnchor => uiAnchor ?? _transform;
        public float SpinPercentage => _fakeBladeController?.SpinSpeedPercentage ?? 0f;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            CacheComponents();
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!_isInitialized || !_isAlive) return;

            // Solo procesar input durante partida activa
            if (GameManager.Instance != null && 
                GameManager.Instance.CurrentState == GameManager.GameState.InMatch)
            {
                ProcessInput();
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }
        #endregion

        #region Initialization
        private void CacheComponents()
        {
            _transform = transform;
            _fakeBladeController = GetComponent<FakeBladeController>();
            _inputHandler = GetComponent<InputHandler>();
            _stats = GetComponent<FakeBladeStats>();
            _propertyBlock = new MaterialPropertyBlock();

            ValidateComponents();
        }

        private void ValidateComponents()
        {
            if (_fakeBladeController == null)
            {
                Debug.LogError($"[PlayerController] FakeBladeController missing on {gameObject.name}");
            }

            if (_inputHandler == null)
            {
                Debug.LogError($"[PlayerController] InputHandler missing on {gameObject.name}");
            }

            if (_stats == null)
            {
                Debug.LogError($"[PlayerController] FakeBladeStats missing on {gameObject.name}");
            }
        }

        private void Initialize()
        {
            // Registrar con GameManager
            if (GameManager.Instance != null)
            {
                if (!GameManager.Instance.RegisterPlayer(this))
                {
                    Debug.LogWarning($"[PlayerController] Failed to register Player {playerID}");
                    return;
                }
            }

            // Configurar visual
            ApplyPlayerColor();

            // Suscribirse a eventos del FakeBlade
            if (_fakeBladeController != null)
            {
                _fakeBladeController.OnSpinChanged += HandleSpinChanged;
                _fakeBladeController.OnSpinOut += HandleSpinOut;
                _fakeBladeController.OnDashExecuted += HandleDashExecuted;
                _fakeBladeController.OnCollisionWithFakeBlade += HandleCollision;
            }

            // Configurar input handler
            ConfigureInputHandler();

            _isInitialized = true;

            if (debugMode)
            {
                Debug.Log($"[PlayerController] Player {playerID} ({playerName}) initialized");
            }
        }

        private void ConfigureInputHandler()
        {
            if (_inputHandler == null) return;

            // Asignar gamepad según playerID
            // Player 0: Keyboard o Gamepad 0
            // Player 1-3: Gamepads 1-3
            if (playerID == 0)
            {
                // Primer jugador puede usar keyboard o gamepad
                _inputHandler.SetGamepadIndex(-1); // -1 = keyboard
            }
            else
            {
                _inputHandler.SetGamepadIndex(playerID - 1);
            }
        }

        private void Cleanup()
        {
            // Desuscribirse de eventos
            if (_fakeBladeController != null)
            {
                _fakeBladeController.OnSpinChanged -= HandleSpinChanged;
                _fakeBladeController.OnSpinOut -= HandleSpinOut;
                _fakeBladeController.OnDashExecuted -= HandleDashExecuted;
                _fakeBladeController.OnCollisionWithFakeBlade -= HandleCollision;
            }

            // Desregistrar del GameManager
            GameManager.Instance?.UnregisterPlayer(this);
        }
        #endregion

        #region Input Processing
        private void ProcessInput()
        {
            if (_inputHandler == null || _fakeBladeController == null) return;

            // Movimiento
            Vector2 moveInput = _inputHandler.GetMovementInput();
            if (moveInput.sqrMagnitude > 0.01f)
            {
                _fakeBladeController.HandleMovement(moveInput);

                if (debugMode)
                {
                    Debug.Log($"[Player {playerID}] Move: {moveInput}");
                }
            }

            // Dash (con input buffering del InputHandler)
            if (_inputHandler.GetDashInput())
            {
                _fakeBladeController.ExecuteDash();
            }

            // Special
            if (_inputHandler.GetSpecialInput())
            {
                _fakeBladeController.ExecuteSpecial();
            }
        }
        #endregion

        #region Event Handlers
        private void HandleSpinChanged(float percentage)
        {
            OnSpinChanged?.Invoke(percentage);
        }

        private void HandleSpinOut()
        {
            if (!_isAlive) return;

            _isAlive = false;

            if (debugMode)
            {
                Debug.Log($"[PlayerController] Player {playerID} ({playerName}) ELIMINATED!");
            }

            OnPlayerDefeated?.Invoke(playerID);
        }

        private void HandleDashExecuted()
        {
            // Vibración del gamepad
            _inputHandler?.Vibrate(0.2f, 0.4f, 0.1f);
        }

        private void HandleCollision(FakeBladeController other, float damage)
        {
            // Vibración proporcional al daño
            float intensity = Mathf.Clamp01(damage / 50f);
            _inputHandler?.Vibrate(intensity * 0.3f, intensity * 0.6f, 0.15f);
        }

        public void OnFakeBladeDestroyed()
        {
            HandleSpinOut();
        }
        #endregion

        #region Public Methods
        public void SetPlayerID(int id)
        {
            playerID = id;
            gameObject.name = $"Player_{id}_{playerName}";
        }

        public void SetPlayerName(string name)
        {
            playerName = name;
            gameObject.name = $"Player_{playerID}_{name}";
        }

        public void SetPlayerColor(Color color)
        {
            playerColor = color;
            ApplyPlayerColor();
        }

        public void SetTeamID(int team)
        {
            teamID = team;
        }

        public void SetReady(bool ready)
        {
            _isReady = ready;
            if (ready)
            {
                OnPlayerReady?.Invoke(playerID);
            }
        }

        public void ResetPlayer()
        {
            _isAlive = true;
            _fakeBladeController?.ResetFakeBlade();

            if (debugMode)
            {
                Debug.Log($"[PlayerController] Player {playerID} reset");
            }
        }

        public void SetSpawnPosition(Transform spawnPoint)
        {
            if (spawnPoint == null) return;

            _fakeBladeController?.SetPosition(spawnPoint.position, spawnPoint.rotation);
        }
        #endregion

        #region Visual
        private void ApplyPlayerColor()
        {
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            // Buscar renderers si no están asignados
            if (coloredRenderers == null || coloredRenderers.Length == 0)
            {
                coloredRenderers = GetComponentsInChildren<Renderer>();
            }

            foreach (var renderer in coloredRenderers)
            {
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(ColorProperty, playerColor);
                
                // Color de emisión sutil
                Color emission = playerColor * 0.2f;
                _propertyBlock.SetColor(EmissionProperty, emission);
                
                renderer.SetPropertyBlock(_propertyBlock);
            }

            if (debugMode)
            {
                Debug.Log($"[PlayerController] Applied color {playerColor} to Player {playerID}");
            }
        }

        public void SetHighlight(bool enabled)
        {
            if (_propertyBlock == null) return;

            Color emissionColor = enabled ? playerColor * 0.5f : playerColor * 0.1f;

            foreach (var renderer in coloredRenderers)
            {
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(EmissionProperty, emissionColor);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }
        #endregion

        #region Debug
        private void OnDrawGizmosSelected()
        {
            // Dibujar UI anchor
            if (uiAnchor != null)
            {
                Gizmos.color = playerColor;
                Gizmos.DrawWireSphere(uiAnchor.position, 0.2f);
                Gizmos.DrawLine(transform.position, uiAnchor.position);
            }

            // Team indicator
            Gizmos.color = teamID == 0 ? Color.red : Color.blue;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.3f);
        }
        #endregion
    }
}
