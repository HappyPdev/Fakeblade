using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Gestiona la ejecución de habilidades especiales.
    /// Lee el tipo de habilidad del Core equipado en FakeBladeStats.
    /// Si no hay Core, usa SpinBoost por defecto.
    /// </summary>
    [RequireComponent(typeof(FakeBladeController))]
    [RequireComponent(typeof(FakeBladeStats))]
    public class SpecialAbilityHandler : MonoBehaviour
    {
        #region Settings
        [Header("=== ABILITY SETTINGS ===")]
        [SerializeField] private float shockWaveRadius = 5f;
        [SerializeField] private float shockWaveForce = 12f;
        [SerializeField] private float shockWaveDamage = 40f;

        [SerializeField] private float shieldDuration = 3f;
        [SerializeField] private float shieldDamageReduction = 0.7f; // 70% reducción

        [SerializeField] private float spinBoostAmount = 150f;
        [SerializeField] private float spinBoostVerticalForce = 3f;

        [SerializeField] private float dashAbilityForce = 25f;

        [Header("=== COOLDOWN ===")]
        [SerializeField] private float abilityCooldown = 2f;

        [Header("=== DEBUG ===")]
        [SerializeField] private bool showDebugInfo = false;
        #endregion

        #region Private Fields
        private FakeBladeController _controller;
        private FakeBladeStats _stats;
        private Rigidbody _rb;
        private Transform _transform;

        private int _remainingUses;
        private float _cooldownTimer;
        private bool _canUse = true;

        // Shield state
        private bool _shieldActive;
        private float _shieldTimer;
        #endregion

        #region Properties
        public int RemainingUses => _remainingUses;
        public bool CanUse => _canUse && _remainingUses > 0;
        public bool IsShieldActive => _shieldActive;
        public SpecialAbilityType CurrentAbilityType => GetCurrentAbilityType();

        public float CooldownProgress
        {
            get
            {
                if (_canUse) return 1f;
                return 1f - (_cooldownTimer / abilityCooldown);
            }
        }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            _controller = GetComponent<FakeBladeController>();
            _stats = GetComponent<FakeBladeStats>();
            _rb = GetComponent<Rigidbody>();
            _transform = transform;
        }

        private void Start()
        {
            ResetAbility();

            if (_stats != null)
            {
                _stats.OnStatsChanged += OnStatsChanged;
            }
        }

        private void Update()
        {
            UpdateCooldown();
            UpdateShield();
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.OnStatsChanged -= OnStatsChanged;
            }
        }
        #endregion

        #region Core Logic
        /// <summary>
        /// Ejecuta la habilidad especial actual.
        /// Llamado desde FakeBladeController.ExecuteSpecial().
        /// </summary>
        public void ExecuteAbility()
        {
            if (!_canUse || _remainingUses <= 0)
            {
                if (showDebugInfo) Debug.Log("[Ability] Cannot use - on cooldown or no uses left");
                return;
            }

            SpecialAbilityType type = GetCurrentAbilityType();

            switch (type)
            {
                case SpecialAbilityType.SpinBoost:
                    ExecuteSpinBoost();
                    break;
                case SpecialAbilityType.ShockWave:
                    ExecuteShockWave();
                    break;
                case SpecialAbilityType.Shield:
                    ExecuteShield();
                    break;
                case SpecialAbilityType.Dash:
                    ExecuteDashAbility();
                    break;
                case SpecialAbilityType.None:
                default:
                    ExecuteSpinBoost(); // Fallback
                    break;
            }

            _remainingUses--;
            _canUse = false;
            _cooldownTimer = abilityCooldown;

            if (showDebugInfo)
            {
                Debug.Log($"[Ability] Executed {type}! Uses left: {_remainingUses}");
            }
        }

        private SpecialAbilityType GetCurrentAbilityType()
        {
            if (_stats == null) return SpecialAbilityType.SpinBoost;

            var core = _stats.EquippedCore;
            if (core == null) return SpecialAbilityType.SpinBoost;

            return core.SpecialAbility != SpecialAbilityType.None
                ? core.SpecialAbility
                : SpecialAbilityType.SpinBoost;
        }
        #endregion

        #region Ability Implementations
        private void ExecuteSpinBoost()
        {
            _controller.AddSpin(spinBoostAmount);

            if (_rb != null)
            {
                _rb.AddForce(Vector3.up * spinBoostVerticalForce, ForceMode.Impulse);
            }

            if (showDebugInfo)
            {
                Debug.Log($"[Ability] SpinBoost: +{spinBoostAmount} spin");
            }
        }

        private void ExecuteShockWave()
        {
            // Encontrar todos los FakeBlades en el radio
            Collider[] hits = Physics.OverlapSphere(_transform.position, shockWaveRadius);

            int affected = 0;
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                FakeBladeController other = hit.GetComponent<FakeBladeController>();
                if (other == null || other.IsDestroyed) continue;

                // Empujar lejos
                Vector3 direction = (other.transform.position - _transform.position).normalized;
                direction.y = 0.2f;

                float distance = Vector3.Distance(_transform.position, other.transform.position);
                float falloff = 1f - (distance / shockWaveRadius);
                falloff = Mathf.Clamp01(falloff);

                Rigidbody otherRb = other.GetComponent<Rigidbody>();
                if (otherRb != null)
                {
                    otherRb.AddForce(direction * shockWaveForce * falloff, ForceMode.Impulse);
                }

                other.ReduceSpin(shockWaveDamage * falloff);
                affected++;
            }

            // Pequeño coste propio
            _controller.ReduceSpin(15f);

            if (showDebugInfo)
            {
                Debug.Log($"[Ability] ShockWave: Hit {affected} targets in radius {shockWaveRadius}");
            }
        }

        private void ExecuteShield()
        {
            _shieldActive = true;
            _shieldTimer = shieldDuration;

            if (showDebugInfo)
            {
                Debug.Log($"[Ability] Shield activated for {shieldDuration}s ({shieldDamageReduction * 100}% reduction)");
            }
        }

        private void ExecuteDashAbility()
        {
            if (_rb == null) return;

            // Dash en dirección de movimiento actual o forward
            Vector3 dashDir = _rb.linearVelocity.normalized;
            if (dashDir.sqrMagnitude < 0.01f)
            {
                dashDir = _transform.forward;
            }
            dashDir.y = 0f;
            dashDir.Normalize();

            _rb.AddForce(dashDir * dashAbilityForce, ForceMode.Impulse);

            if (showDebugInfo)
            {
                Debug.Log($"[Ability] Dash ability: Force {dashAbilityForce} in dir {dashDir}");
            }
        }
        #endregion

        #region Update Loops
        private void UpdateCooldown()
        {
            if (_canUse) return;

            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _canUse = true;
            }
        }

        private void UpdateShield()
        {
            if (!_shieldActive) return;

            _shieldTimer -= Time.deltaTime;
            if (_shieldTimer <= 0f)
            {
                _shieldActive = false;
                if (showDebugInfo) Debug.Log("[Ability] Shield expired");
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Devuelve el multiplicador de daño actual (afectado por Shield).
        /// Llamar desde FakeBladeController.ReduceSpin() si se quiere integrar.
        /// </summary>
        public float GetDamageMultiplier()
        {
            if (_shieldActive)
            {
                return 1f - shieldDamageReduction;
            }
            return 1f;
        }

        /// <summary>
        /// Resetea la habilidad al inicio de una partida.
        /// </summary>
        public void ResetAbility()
        {
            var core = _stats?.EquippedCore;
            _remainingUses = core != null ? core.SpecialAbilityUses : 3;
            _canUse = true;
            _cooldownTimer = 0f;
            _shieldActive = false;
            _shieldTimer = 0f;
        }

        private void OnStatsChanged()
        {
            // Recalcular usos cuando cambian los stats (nuevo Core equipado)
            ResetAbility();
        }
        #endregion

        #region Debug
        private void OnDrawGizmosSelected()
        {
            if (GetCurrentAbilityType() == SpecialAbilityType.ShockWave)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
                Gizmos.DrawWireSphere(transform.position, shockWaveRadius);
            }

            if (_shieldActive)
            {
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.3f);
                Gizmos.DrawSphere(transform.position, 1f);
            }
        }
        #endregion
    }
}