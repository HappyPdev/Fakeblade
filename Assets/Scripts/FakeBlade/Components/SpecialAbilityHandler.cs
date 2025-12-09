using UnityEngine;
using System;
using System.Collections;

namespace FakeBlade.Core
{
    /// <summary>
    /// Manejador de habilidades especiales para FakeBlades.
    /// Las habilidades se definen en el componente Core equipado.
    /// </summary>
    [RequireComponent(typeof(FakeBladeController))]
    [RequireComponent(typeof(FakeBladeStats))]
    public class SpecialAbilityHandler : MonoBehaviour
    {
        #region Events
        public event Action<SpecialAbilityType> OnAbilityExecuted;
        public event Action<int> OnUsesChanged;
        public event Action<float> OnCooldownChanged;
        #endregion

        #region Serialized Fields
        [Header("Ability Settings")]
        [SerializeField] private SpecialAbilityType currentAbility = SpecialAbilityType.None;
        [SerializeField] private int maxUses = 3;
        [SerializeField] private float cooldown = 5f;
        [SerializeField] private float power = 1f;

        [Header("Effects")]
        [SerializeField] private ParticleSystem abilityEffect;
        [SerializeField] private AudioClip abilitySound;

        [Header("Spin Boost Settings")]
        [SerializeField] private float spinBoostAmount = 150f;

        [Header("Shockwave Settings")]
        [SerializeField] private float shockwaveRadius = 5f;
        [SerializeField] private float shockwaveForce = 20f;

        [Header("Shield Settings")]
        [SerializeField] private float shieldDuration = 3f;
        [SerializeField] private float shieldDamageReduction = 0.7f;

        [Header("Dash Settings")]
        [SerializeField] private float superDashForce = 30f;

        [Header("Drain Settings")]
        [SerializeField] private float drainRange = 3f;
        [SerializeField] private float drainAmount = 50f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        #endregion

        #region Private Fields
        private FakeBladeController _controller;
        private FakeBladeStats _stats;
        private Rigidbody _rb;
        private AudioSource _audioSource;

        private int _remainingUses;
        private float _cooldownTimer;
        private bool _isOnCooldown;
        private bool _isAbilityActive;

        // Shield state
        private float _originalDefense;
        private bool _hasShield;
        #endregion

        #region Properties
        public SpecialAbilityType CurrentAbility => currentAbility;
        public int RemainingUses => _remainingUses;
        public bool CanUseAbility => _remainingUses > 0 && !_isOnCooldown && !_isAbilityActive;
        public float CooldownProgress => _isOnCooldown ? 1f - (_cooldownTimer / cooldown) : 1f;
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
            UpdateCooldown();
        }
        #endregion

        #region Initialization
        private void CacheComponents()
        {
            _controller = GetComponent<FakeBladeController>();
            _stats = GetComponent<FakeBladeStats>();
            _rb = GetComponent<Rigidbody>();
            _audioSource = GetComponent<AudioSource>();
        }

        private void Initialize()
        {
            // Obtener habilidad del componente Core
            LoadAbilityFromStats();

            _remainingUses = maxUses;
            _cooldownTimer = 0f;
            _isOnCooldown = false;

            // Suscribirse a cambios de stats
            if (_stats != null)
            {
                _stats.OnStatsChanged += LoadAbilityFromStats;
            }
        }

        private void LoadAbilityFromStats()
        {
            if (_stats?.Core == null) return;

            currentAbility = _stats.Core.SpecialAbility;
            maxUses = _stats.Core.MaxAbilityUses;
            cooldown = _stats.Core.AbilityCooldown;
            power = _stats.Core.AbilityPower;

            _remainingUses = maxUses;

            if (debugMode)
            {
                Debug.Log($"[SpecialAbility] Loaded: {currentAbility}, Uses: {maxUses}, Cooldown: {cooldown}s");
            }
        }
        #endregion

        #region Cooldown
        private void UpdateCooldown()
        {
            if (!_isOnCooldown) return;

            _cooldownTimer -= Time.deltaTime;
            OnCooldownChanged?.Invoke(CooldownProgress);

            if (_cooldownTimer <= 0f)
            {
                _isOnCooldown = false;
                _cooldownTimer = 0f;
            }
        }

        private void StartCooldown()
        {
            _isOnCooldown = true;
            _cooldownTimer = cooldown;
        }
        #endregion

        #region Ability Execution
        public bool ExecuteAbility()
        {
            if (!CanUseAbility)
            {
                if (debugMode)
                {
                    string reason = _remainingUses <= 0 ? "No uses left" :
                                   _isOnCooldown ? "On cooldown" : "Ability active";
                    Debug.Log($"[SpecialAbility] Cannot execute: {reason}");
                }
                return false;
            }

            bool success = currentAbility switch
            {
                SpecialAbilityType.SpinBoost => ExecuteSpinBoost(),
                SpecialAbilityType.ShockWave => ExecuteShockwave(),
                SpecialAbilityType.Shield => ExecuteShield(),
                SpecialAbilityType.Dash => ExecuteSuperDash(),
                SpecialAbilityType.Drain => ExecuteDrain(),
                SpecialAbilityType.Berserk => ExecuteBerserk(),
                SpecialAbilityType.Anchor => ExecuteAnchor(),
                SpecialAbilityType.Phantom => ExecutePhantom(),
                _ => false
            };

            if (success)
            {
                _remainingUses--;
                StartCooldown();
                PlayAbilityEffects();

                OnAbilityExecuted?.Invoke(currentAbility);
                OnUsesChanged?.Invoke(_remainingUses);

                if (debugMode)
                {
                    Debug.Log($"[SpecialAbility] {currentAbility} executed! Remaining: {_remainingUses}");
                }
            }

            return success;
        }
        #endregion

        #region Individual Abilities
        private bool ExecuteSpinBoost()
        {
            float boost = spinBoostAmount * power;
            _controller?.AddSpin(boost);

            // Pequeño impulso hacia arriba
            _rb?.AddForce(Vector3.up * 2f, ForceMode.Impulse);

            return true;
        }

        private bool ExecuteShockwave()
        {
            // Encontrar FakeBlades cercanas
            Collider[] hits = Physics.OverlapSphere(transform.position, shockwaveRadius * power);

            int enemiesHit = 0;

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                FakeBladeController enemy = hit.GetComponent<FakeBladeController>();
                if (enemy != null && !enemy.IsDestroyed)
                {
                    // Calcular dirección y aplicar fuerza
                    Vector3 direction = (enemy.transform.position - transform.position).normalized;
                    direction.y = 0.2f;

                    Rigidbody enemyRb = enemy.GetComponent<Rigidbody>();
                    enemyRb?.AddForce(direction * shockwaveForce * power, ForceMode.Impulse);

                    // Daño de spin
                    enemy.ReduceSpin(30f * power);
                    enemiesHit++;
                }
            }

            // Auto-daño si no golpea a nadie
            if (enemiesHit == 0)
            {
                _controller?.ReduceSpin(20f);
            }

            return true;
        }

        private bool ExecuteShield()
        {
            if (_hasShield) return false;

            StartCoroutine(ShieldCoroutine());
            return true;
        }

        private IEnumerator ShieldCoroutine()
        {
            _hasShield = true;
            _isAbilityActive = true;

            // Guardar y modificar defensa
            // Nota: Necesitaría modificar FakeBladeStats para esto
            // Por ahora usamos un multiplicador interno

            if (debugMode)
            {
                Debug.Log($"[SpecialAbility] Shield activated for {shieldDuration}s");
            }

            yield return new WaitForSeconds(shieldDuration * power);

            _hasShield = false;
            _isAbilityActive = false;

            if (debugMode)
            {
                Debug.Log("[SpecialAbility] Shield deactivated");
            }
        }

        private bool ExecuteSuperDash()
        {
            if (_rb == null) return false;

            Vector3 direction = _rb.linearVelocity.normalized;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = transform.forward;
            }

            _rb.AddForce(direction * superDashForce * power, ForceMode.Impulse);
            return true;
        }

        private bool ExecuteDrain()
        {
            // Encontrar el enemigo más cercano
            FakeBladeController nearestEnemy = null;
            float nearestDistance = drainRange * power;

            Collider[] hits = Physics.OverlapSphere(transform.position, nearestDistance);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                FakeBladeController enemy = hit.GetComponent<FakeBladeController>();
                if (enemy != null && !enemy.IsDestroyed)
                {
                    float dist = Vector3.Distance(transform.position, enemy.transform.position);
                    if (dist < nearestDistance)
                    {
                        nearestDistance = dist;
                        nearestEnemy = enemy;
                    }
                }
            }

            if (nearestEnemy != null)
            {
                float drain = drainAmount * power;
                nearestEnemy.ReduceSpin(drain);
                _controller?.AddSpin(drain * 0.5f); // 50% de lo drenado
                return true;
            }

            return false;
        }

        private bool ExecuteBerserk()
        {
            StartCoroutine(BerserkCoroutine());
            return true;
        }

        private IEnumerator BerserkCoroutine()
        {
            _isAbilityActive = true;

            // Aumentar velocidad y ataque temporalmente
            // Esto requeriría modificar stats temporalmente

            if (debugMode)
            {
                Debug.Log("[SpecialAbility] Berserk mode activated!");
            }

            yield return new WaitForSeconds(4f * power);

            _isAbilityActive = false;

            if (debugMode)
            {
                Debug.Log("[SpecialAbility] Berserk mode ended");
            }
        }

        private bool ExecuteAnchor()
        {
            StartCoroutine(AnchorCoroutine());
            return true;
        }

        private IEnumerator AnchorCoroutine()
        {
            _isAbilityActive = true;

            // Aumentar masa temporalmente
            float originalMass = _rb.mass;
            _rb.mass *= 3f * power;

            if (debugMode)
            {
                Debug.Log("[SpecialAbility] Anchor activated!");
            }

            yield return new WaitForSeconds(3f);

            _rb.mass = originalMass;
            _isAbilityActive = false;

            if (debugMode)
            {
                Debug.Log("[SpecialAbility] Anchor ended");
            }
        }

        private bool ExecutePhantom()
        {
            StartCoroutine(PhantomCoroutine());
            return true;
        }

        private IEnumerator PhantomCoroutine()
        {
            _isAbilityActive = true;

            // Hacer temporalmente invulnerable
            // Cambiar layer temporalmente para evitar colisiones
            int originalLayer = gameObject.layer;
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            // Visual: hacer semi-transparente
            SetTransparency(0.3f);

            if (debugMode)
            {
                Debug.Log("[SpecialAbility] Phantom activated!");
            }

            yield return new WaitForSeconds(1.5f * power);

            gameObject.layer = originalLayer;
            SetTransparency(1f);
            _isAbilityActive = false;

            if (debugMode)
            {
                Debug.Log("[SpecialAbility] Phantom ended");
            }
        }

        private void SetTransparency(float alpha)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            MaterialPropertyBlock block = new MaterialPropertyBlock();

            foreach (var renderer in renderers)
            {
                renderer.GetPropertyBlock(block);
                Color color = block.GetColor("_BaseColor");
                color.a = alpha;
                block.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(block);
            }
        }
        #endregion

        #region Effects
        private void PlayAbilityEffects()
        {
            if (abilityEffect != null)
            {
                abilityEffect.Play();
            }

            if (_audioSource != null && abilitySound != null)
            {
                _audioSource.PlayOneShot(abilitySound);
            }
        }
        #endregion

        #region Public Methods
        public void ResetAbility()
        {
            _remainingUses = maxUses;
            _cooldownTimer = 0f;
            _isOnCooldown = false;
            _isAbilityActive = false;
            _hasShield = false;

            StopAllCoroutines();
        }

        public void SetAbility(SpecialAbilityType ability, int uses, float cd, float pow)
        {
            currentAbility = ability;
            maxUses = uses;
            cooldown = cd;
            power = pow;
            _remainingUses = uses;
        }

        public float GetDamageMultiplier()
        {
            if (_hasShield)
            {
                return 1f - shieldDamageReduction;
            }
            return 1f;
        }
        #endregion

        #region Cleanup
        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.OnStatsChanged -= LoadAbilityFromStats;
            }
        }
        #endregion

        #region Debug
        private void OnDrawGizmosSelected()
        {
            // Visualizar rangos de habilidades
            switch (currentAbility)
            {
                case SpecialAbilityType.ShockWave:
                    Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                    Gizmos.DrawWireSphere(transform.position, shockwaveRadius);
                    break;

                case SpecialAbilityType.Drain:
                    Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.3f);
                    Gizmos.DrawWireSphere(transform.position, drainRange);
                    break;
            }
        }
        #endregion
    }
}