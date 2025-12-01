using UnityEngine;
using System;

namespace FakeBlade.Core
{
    /// <summary>
    /// Controlador de jugador individual. Gestiona input y estado de vida.
    /// </summary>
    [RequireComponent(typeof(FakeBladeController))]
    [RequireComponent(typeof(InputHandler))]
    public class PlayerController : MonoBehaviour
    {
        #region Events
        public event Action<int> OnPlayerDefeated;
        #endregion

        #region Serialized Fields
        [Header("Player Info")]
        [SerializeField] private int playerID = 0;
        [SerializeField] private Color playerColor = Color.white;

        [Header("UI")]
        [SerializeField] private string playerName = "Player";

        [Header("Debug")]
        [SerializeField] private bool debugInput = false;
        #endregion

        #region Private Fields
        private FakeBladeController fakeBladeController;
        private InputHandler inputHandler;
        private bool isAlive = true;
        private bool isInitialized = false;
        #endregion

        #region Properties
        public bool IsAlive => isAlive;
        public int PlayerID => playerID;
        public Color PlayerColor => playerColor;
        public string PlayerName => playerName;
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
            if (!isInitialized || !isAlive) return;

            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameManager.GameState.InMatch)
            {
                HandleInput();
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UnregisterPlayer(this);
            }
        }
        #endregion

        #region Initialization
        private void CacheComponents()
        {
            fakeBladeController = GetComponent<FakeBladeController>();
            inputHandler = GetComponent<InputHandler>();

            if (fakeBladeController == null)
            {
                Debug.LogError($"[PlayerController] FakeBladeController missing on {gameObject.name}");
            }

            if (inputHandler == null)
            {
                Debug.LogError($"[PlayerController] InputHandler missing on {gameObject.name}");
            }
        }

        private void Initialize()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[PlayerController] GameManager not found!");
                return;
            }

            if (GameManager.Instance.RegisterPlayer(this))
            {
                ApplyPlayerColor();
                isInitialized = true;
            }
        }
        #endregion

        #region Input Handling
        private void HandleInput()
        {
            // Obtener inputs
            Vector2 moveInput = inputHandler.GetMovementInput();
            bool dashInput = inputHandler.GetDashInput();
            bool specialInput = inputHandler.GetSpecialInput();

            if (debugInput && (moveInput.sqrMagnitude > 0.01f || dashInput || specialInput))
            {
                Debug.Log($"[Player {playerID}] Move: {moveInput}, Dash: {dashInput}, Special: {specialInput}");
            }

            // Aplicar movimiento
            if (moveInput.sqrMagnitude > 0.01f)
            {
                fakeBladeController.HandleMovement(moveInput);
            }

            // Ejecutar dash
            if (dashInput)
            {
                fakeBladeController.ExecuteDash();
            }

            // Ejecutar especial
            if (specialInput)
            {
                fakeBladeController.ExecuteSpecial();
            }
        }
        #endregion

        #region Public Methods
        public void OnFakeBladeDestroyed()
        {
            if (!isAlive) return;

            isAlive = false;
            OnPlayerDefeated?.Invoke(playerID);

            Debug.Log($"[PlayerController] Player {playerID} ({playerName}) eliminated!");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.UnregisterPlayer(this);
            }
        }

        public void SetPlayerID(int id)
        {
            playerID = id;
        }

        public void SetPlayerColor(Color color)
        {
            playerColor = color;
            ApplyPlayerColor();
        }

        public void SetPlayerName(string name)
        {
            playerName = name;
        }
        #endregion

        #region Visual
        private void ApplyPlayerColor()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor("_Color", playerColor);
                    renderer.SetPropertyBlock(propertyBlock);
                }
            }
        }
        #endregion
    }
}