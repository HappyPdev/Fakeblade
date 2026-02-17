using UnityEngine;

namespace FakeBlade.UI
{
    /// <summary>
    /// Puente entre PlayerController/FakeBladeController y el HUD.
    /// 
    /// Se añade a cada GameObject de jugador y se encarga de:
    /// 1. Leer datos del FakeBladeController cada frame
    /// 2. Enviar actualizaciones al panel HUD correspondiente
    /// 3. Gestionar las cargas de habilidad especial
    /// 
    /// === CÓMO USAR ===
    /// - Se añade automáticamente por CombatHUDSetup, o manualmente al prefab
    /// - Necesita que CombatHUDManager esté inicializado
    /// 
    /// === DATOS QUE ACTUALIZA ===
    /// - Spin (vida) → UpdateHealth
    /// - Cargas de habilidad → UpdateAbilityCharges
    /// - Cooldown del dash → UpdateDashCooldown
    /// - Stats (velocidad, peso) → UpdateStats
    /// </summary>
    [RequireComponent(typeof(FakeBlade.Core.PlayerController))]
    public class PlayerHUDBridge : MonoBehaviour
    {
        #region Serialized Fields
        [Header("=== ABILITY CONFIG ===")]
        [Tooltip("Número máximo de cargas de habilidad especial")]
        [SerializeField] private int maxAbilityCharges = 3;

        [Tooltip("Tiempo en segundos para recargar una carga")]
        [SerializeField] private float chargeRechargeTime = 5f;

        [Header("=== DEBUG ===")]
        [SerializeField] private bool showDebugInfo = false;
        #endregion

        #region Private Fields
        private FakeBlade.Core.PlayerController _playerController;
        private FakeBlade.Core.FakeBladeController _fakeBladeController;
        private FakeBlade.Core.FakeBladeStats _stats;

        // Estado de cargas
        private int _currentCharges;
        private float _chargeTimer;
        private bool _isInitialized;
        #endregion

        #region Properties
        public int CurrentCharges => _currentCharges;
        public int MaxCharges => maxAbilityCharges;
        public float ChargeProgress => _chargeTimer / chargeRechargeTime;
        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _playerController = GetComponent<FakeBlade.Core.PlayerController>();
            _fakeBladeController = GetComponent<FakeBlade.Core.FakeBladeController>();
            _stats = GetComponent<FakeBlade.Core.FakeBladeStats>();
        }

        private void Start()
        {
            _currentCharges = maxAbilityCharges;
            _chargeTimer = 0f;

            // Subscribirse al evento de uso de especial
            if (_fakeBladeController != null)
            {
                _fakeBladeController.OnSpecialExecuted += HandleSpecialUsed;
            }

            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized) return;
            if (CombatHUDManager.Instance == null || !CombatHUDManager.Instance.IsInitialized) return;

            int id = _playerController.PlayerID;

            UpdateChargeRegeneration();
            SendHUDUpdates(id);
        }

        private void OnDestroy()
        {
            if (_fakeBladeController != null)
            {
                _fakeBladeController.OnSpecialExecuted -= HandleSpecialUsed;
            }
        }

        #endregion

        // =====================================================================
        // HUD UPDATES
        // =====================================================================

        #region HUD Updates

        private void SendHUDUpdates(int playerID)
        {
            var hud = CombatHUDManager.Instance;

            // Cargas de habilidad
            float progress = (_currentCharges < maxAbilityCharges) ? ChargeProgress : 0f;
            hud.UpdateAbilityCharges(playerID, _currentCharges, maxAbilityCharges, progress);

            // Dash cooldown
            if (_fakeBladeController != null)
            {
                float dashProgress = _fakeBladeController.GetDashCooldownProgress();
                hud.UpdateDashCooldown(playerID, dashProgress);
            }

            // Stats (velocidad, peso)
            if (_fakeBladeController != null)
            {
                float speed = _fakeBladeController.Velocity.magnitude;
                float weight = _stats != null ? _stats.Weight : 1f;

                var panel = hud.GetPlayerPanel(playerID);
                if (panel != null)
                    panel.UpdateStats(speed, weight);
            }
        }

        #endregion

        // =====================================================================
        // CHARGE SYSTEM
        // =====================================================================

        #region Charges

        private void UpdateChargeRegeneration()
        {
            if (_currentCharges >= maxAbilityCharges) return;

            _chargeTimer += Time.deltaTime;

            if (_chargeTimer >= chargeRechargeTime)
            {
                _currentCharges++;
                _chargeTimer = 0f;

                if (showDebugInfo)
                    Debug.Log($"[HUDBridge P{_playerController.PlayerID}] Charge restored! " +
                        $"Now: {_currentCharges}/{maxAbilityCharges}");
            }
        }

        private void HandleSpecialUsed(FakeBlade.Core.SpecialAbilityType abilityType)
        {
            if (_currentCharges <= 0) return;

            _currentCharges--;
            _chargeTimer = 0f; // Reset progreso de la carga actual

            if (showDebugInfo)
                Debug.Log($"[HUDBridge P{_playerController.PlayerID}] Special used! " +
                    $"Charges: {_currentCharges}/{maxAbilityCharges}");
        }

        /// <summary>
        /// Comprueba si hay cargas disponibles. 
        /// Llamar desde el sistema de habilidades antes de ejecutar.
        /// </summary>
        public bool HasCharges()
        {
            return _currentCharges > 0;
        }

        /// <summary>
        /// Consume una carga manualmente (si se gestiona desde fuera).
        /// </summary>
        public void ConsumeCharge()
        {
            if (_currentCharges > 0)
            {
                _currentCharges--;
                _chargeTimer = 0f;
            }
        }

        /// <summary>
        /// Restaura todas las cargas (ej: al resetear partida).
        /// </summary>
        public void RestoreAllCharges()
        {
            _currentCharges = maxAbilityCharges;
            _chargeTimer = 0f;
        }

        #endregion
    }
}
