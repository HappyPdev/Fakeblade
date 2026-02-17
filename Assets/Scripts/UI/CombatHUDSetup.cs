using UnityEngine;

namespace FakeBlade.UI
{
    /// <summary>
    /// Script de configuración automática del HUD.
    /// 
    /// === CÓMO INTEGRAR CON EL PROYECTO EXISTENTE ===
    /// 
    /// OPCIÓN A: Añadir a la escena junto a TestArenaSetup
    ///   1. Crear GameObject vacío "[CombatHUD]"
    ///   2. Añadir este script (CombatHUDSetup)
    ///   3. Arrastrar TestArenaSetup al campo si se quiere auto-bind
    ///   4. Al dar Play, el HUD se genera automáticamente
    /// 
    /// OPCIÓN B: Llamar manualmente desde GameManager
    ///   CombatHUDSetup.Instance.SetupHUD(playerControllers);
    /// 
    /// OPCIÓN C: Integrar directamente en TestArenaSetup (nuevo)
    ///   Añadir CombatHUDManager como componente y llamar InitializeHUD
    ///   después de crear los jugadores.
    /// 
    /// === JERARQUÍA FINAL EN LA ESCENA ===
    /// 
    /// [CombatHUD]                     ← Este GameObject
    ///   └── CombatHUDManager          ← Componente (se añade auto)
    ///       └── [CombatHUD_Canvas]    ← Canvas generado
    ///           ├── PlayerPanel_0
    ///           ├── PlayerPanel_1
    ///           ├── PlayerPanel_2
    ///           ├── PlayerPanel_3
    ///           └── CenterInfo
    /// </summary>
    public class CombatHUDSetup : MonoBehaviour
    {
        #region Singleton
        public static CombatHUDSetup Instance { get; private set; }
        #endregion

        #region Serialized Fields
        [Header("=== AUTO SETUP ===")]
        [Tooltip("Si es true, busca jugadores automáticamente al iniciar")]
        [SerializeField] private bool autoSetup = true;

        [Tooltip("Delay en segundos antes de buscar jugadores (dar tiempo a que se creen)")]
        [SerializeField] private float setupDelay = 0.6f;

        [Header("=== ABILITY CONFIG (se aplica a todos los jugadores) ===")]
        [SerializeField] private int abilityCharges = 3;
        [SerializeField] private float abilityRechargeTime = 5f;

        [Header("=== DEBUG ===")]
        [SerializeField] private bool debugMode = false;
        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Asegurar que existe CombatHUDManager
            if (GetComponent<CombatHUDManager>() == null)
                gameObject.AddComponent<CombatHUDManager>();
        }

        private void Start()
        {
            if (autoSetup)
            {
                Invoke(nameof(AutoSetup), setupDelay);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        // =====================================================================
        // PUBLIC API
        // =====================================================================

        #region Public Methods

        /// <summary>
        /// Configura el HUD para un array de PlayerControllers ya existentes.
        /// Llamar manualmente si autoSetup = false.
        /// </summary>
        public void SetupHUD(FakeBlade.Core.PlayerController[] players)
        {
            if (players == null || players.Length == 0)
            {
                Debug.LogError("[CombatHUDSetup] No players provided!");
                return;
            }

            // Añadir bridges a cada jugador
            foreach (var player in players)
            {
                if (player == null) continue;

                PlayerHUDBridge bridge = player.GetComponent<PlayerHUDBridge>();
                if (bridge == null)
                {
                    bridge = player.gameObject.AddComponent<PlayerHUDBridge>();
                }

                // Configurar cargas via serialización (requiere reflexión o método público)
                // Por ahora usamos los valores por defecto del bridge
            }

            // Inicializar HUD
            CombatHUDManager hudManager = GetComponent<CombatHUDManager>();
            if (hudManager == null)
                hudManager = gameObject.AddComponent<CombatHUDManager>();

            hudManager.InitializeHUD(players);

            if (debugMode)
                Debug.Log($"[CombatHUDSetup] HUD setup complete for {players.Length} players");
        }

        #endregion

        // =====================================================================
        // AUTO SETUP
        // =====================================================================

        #region Auto Setup

        private void AutoSetup()
        {
            if (debugMode)
                Debug.Log("[CombatHUDSetup] Starting auto-setup...");

            // Buscar todos los PlayerControllers en la escena
            FakeBlade.Core.PlayerController[] players =
                FindObjectsByType<FakeBlade.Core.PlayerController>(FindObjectsSortMode.None);

            if (players == null || players.Length == 0)
            {
                Debug.LogWarning("[CombatHUDSetup] No PlayerControllers found! " +
                    "Make sure players are created before CombatHUDSetup runs. " +
                    "Try increasing setupDelay.");
                return;
            }

            // Ordenar por PlayerID para que el orden sea consistente
            System.Array.Sort(players, (a, b) => a.PlayerID.CompareTo(b.PlayerID));

            SetupHUD(players);
        }

        #endregion
    }
}
