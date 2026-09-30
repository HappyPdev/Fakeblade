using System;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Jugador: identidad (ID, nombre, color, equipo), estado de partida (vidas, puntos)
    /// y paso del input a la peonza. El flujo de partida lo decide el GameManager.
    /// </summary>
    [RequireComponent(typeof(FakeBladeController))]
    [RequireComponent(typeof(InputHandler))]
    [RequireComponent(typeof(FakeBladeStats))]
    public class PlayerController : MonoBehaviour
    {
        #region Events
        /// <summary>La peonza se ha quedado sin RPM (id del jugador).</summary>
        public event Action<int> OnPlayerDefeated;
        /// <summary>Cambian vidas, puntos o estado de eliminación.</summary>
        public event Action<PlayerController> OnStatusChanged;
        #endregion

        #region Serialized Fields
        [Header("Player Info")]
        [SerializeField] private int playerID = 0;
        [SerializeField] private string playerName = "Player";
        [SerializeField] private Color playerColor = Color.white;
        [SerializeField] private int teamID = 0;

        [Header("Input")]
        [Tooltip("Asigna el dispositivo por defecto según el ID (J1 teclado, J2 mando/flechas...)")]
        [SerializeField] private bool autoAssignInput = true;

        [Header("Visual")]
        [Tooltip("Renderers que se tiñen con el color del jugador. Vacío = todos los hijos.")]
        [SerializeField] private Renderer[] coloredRenderers;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        #endregion

        #region Private Fields
        private FakeBladeController _blade;
        private InputHandler _input;
        private IBladeInputSource _source;
        private FakeBladeStats _stats;
        private MaterialPropertyBlock _propertyBlock;
        private bool _registered;
        private bool _inputAssignedExternally;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        #endregion

        #region Properties
        public int PlayerID => playerID;
        public string PlayerName => playerName;
        public Color PlayerColor => playerColor;
        public int TeamID => teamID;

        public FakeBladeController FakeBladeController => _blade;
        public FakeBladeController Blade => _blade;
        public InputHandler Input => _input;
        public FakeBladeStats Stats => _stats;
        public float SpinPercentage => _blade != null ? _blade.SpinSpeedPercentage : 0f;

        /// <summary>Vidas restantes. 0 con reglas de vidas infinitas (modo Points).</summary>
        public int Lives { get; private set; }
        public int Score { get; private set; }
        /// <summary>Fuera de la partida (sin vidas).</summary>
        public bool IsEliminated { get; private set; }
        public bool IsAlive => _blade != null && !_blade.IsDestroyed;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            _blade = GetComponent<FakeBladeController>();
            _input = GetComponent<InputHandler>();
            _source = _input;
            _stats = GetComponent<FakeBladeStats>();
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void Start()
        {
            if (autoAssignInput && !_inputAssignedExternally)
            {
                InputAssignment.GetDefault(playerID, out var kind, out int pad);
                _input.SetDevice(kind, pad);
            }

            ApplyPlayerColor();

            _blade.OnSpinOut += HandleSpinOut;
            _blade.OnDashExecuted += HandleDash;
            _blade.OnClash += HandleClash;

            var gm = GameManager.Instance;
            if (gm != null) _registered = gm.RegisterPlayer(this);

            if (debugMode)
                Debug.Log($"[PlayerController] P{playerID} ({playerName}) input:{_input.DeviceKind}", this);
        }

        private void OnDestroy()
        {
            if (_blade != null)
            {
                _blade.OnSpinOut -= HandleSpinOut;
                _blade.OnDashExecuted -= HandleDash;
                _blade.OnClash -= HandleClash;
            }

            if (_registered && GameManager.HasInstance)
                GameManager.Instance.UnregisterPlayer(this);
        }

        private void Update()
        {
            if (_blade.IsDestroyed) return;
            if (_source == null) _source = _input; // tras una recompilación en caliente

            var gm = GameManager.Instance;
            bool canAct = gm == null || gm.IsMatchActive;
            if (!canAct)
            {
                _source.ClearBuffers();
                return;
            }

            _blade.HandleMovement(_source.MovementInput);
            _blade.SetAttackHeld(_source.AttackHeld);

            if (_source.ConsumeDash()) _blade.ExecuteDash();
            if (_source.ConsumeSpecial()) _blade.ExecuteSpecial();
        }
        #endregion

        #region Event Handlers
        private void HandleSpinOut()
        {
            if (debugMode) Debug.Log($"[PlayerController] P{playerID} K.O.", this);
            _source.Vibrate(0.6f, 0.9f, 0.35f);
            OnPlayerDefeated?.Invoke(playerID);
        }

        private void HandleDash() => _source.Vibrate(0.2f, 0.4f, 0.1f);

        private void HandleClash(FakeBladeController other, float damageTaken)
        {
            float intensity = Mathf.Clamp01(damageTaken / Mathf.Max(1f, _blade.MaxSpinSpeed * 0.1f));
            _source.Vibrate(0.15f + intensity * 0.35f, 0.3f + intensity * 0.6f, 0.15f);
        }
        #endregion

        #region Match State (GameManager)
        public void ResetMatchState(int startingLives)
        {
            Lives = startingLives;
            Score = 0;
            IsEliminated = false;
            OnStatusChanged?.Invoke(this);
        }

        public void LoseLife()
        {
            if (Lives > 0) Lives--;
            OnStatusChanged?.Invoke(this);
        }

        public void AddScore(int amount)
        {
            Score += amount;
            OnStatusChanged?.Invoke(this);
        }

        public void SetEliminated(bool eliminated)
        {
            IsEliminated = eliminated;
            OnStatusChanged?.Invoke(this);
        }

        /// <summary>Compatibilidad con el flujo antiguo: reinicia la peonza.</summary>
        public void ResetPlayer() => _blade.ResetFakeBlade();

        public void SetSpawnPosition(Transform spawnPoint)
        {
            if (spawnPoint != null)
                _blade.SetPosition(spawnPoint.position, spawnPoint.rotation);
        }
        #endregion

        #region Setup
        public void SetPlayerID(int id)
        {
            playerID = id;
            gameObject.name = $"Player_{id}_{playerName}";
        }

        public void SetPlayerName(string newName)
        {
            playerName = newName;
            gameObject.name = $"Player_{playerID}_{newName}";
        }

        public void SetPlayerColor(Color color)
        {
            playerColor = color;
            ApplyPlayerColor();
        }

        public void SetTeamID(int team) => teamID = team;

        /// <summary>Asigna el dispositivo (menú de selección). Desactiva la asignación automática.</summary>
        public void SetInputDevice(InputDeviceKind kind, int gamepadIndex = 0)
        {
            _inputAssignedExternally = true;
            if (_input == null) _input = GetComponent<InputHandler>();
            _input.SetDevice(kind, gamepadIndex);
        }

        /// <summary>Asigna un mando concreto por deviceId (selección de peonzas).</summary>
        public void SetInputDeviceById(InputDeviceKind kind, int gamepadDeviceId)
        {
            _inputAssignedExternally = true;
            if (_input == null) _input = GetComponent<InputHandler>();
            _input.SetDeviceById(kind, gamepadDeviceId);
        }

        /// <summary>
        /// Cambia quién controla la peonza (dummy, IA...). Null vuelve al input humano.
        /// Con una fuente no humana se desactiva el InputHandler para no leer el teclado en vano.
        /// </summary>
        public void SetInputSource(IBladeInputSource source)
        {
            if (_input == null) _input = GetComponent<InputHandler>();
            _source = source ?? _input;
            _inputAssignedExternally = true;
            _input.enabled = ReferenceEquals(_source, _input);
        }

        public bool IsHumanControlled => ReferenceEquals(_source, _input);
        #endregion

        #region Visual
        private void ApplyPlayerColor()
        {
            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();

            if (coloredRenderers == null || coloredRenderers.Length == 0)
                coloredRenderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < coloredRenderers.Length; i++)
            {
                Renderer r = coloredRenderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, playerColor);
                _propertyBlock.SetColor(EmissionColorId, playerColor * 0.2f);
                r.SetPropertyBlock(_propertyBlock);
            }
        }
        #endregion

        #region Debug
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = playerColor;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.3f);
        }
        #endregion
    }
}
