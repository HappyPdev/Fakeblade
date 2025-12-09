using UnityEngine;
using System;

namespace FakeBlade.Core
{
    /// <summary>
    /// Controlador principal de la peonza (FakeBlade). 
    /// Gestiona física, movimiento, spin y combate con optimizaciones de rendimiento.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(FakeBladeStats))]
    public class FakeBladeController : MonoBehaviour
    {
        #region Constants
        private const float MIN_SPIN_THRESHOLD = 0.1f;
        private const float COLLISION_DAMAGE_BASE = 0.1f;
        private const float SPECIAL_VERTICAL_FORCE = 3f;
        private const float GROUND_CHECK_DISTANCE = 0.1f;
        private const float TILT_SMOOTHING = 8f;
        private const float MAX_TILT_ANGLE = 15f;
        #endregion

        #region Events
        public event Action<float> OnSpinChanged;
        public event Action OnDashExecuted;
        public event Action<SpecialAbilityType> OnSpecialExecuted;
        public event Action<FakeBladeController, float> OnCollisionWithFakeBlade;
        public event Action OnSpinOut;
        public event Action OnGrounded;
        public event Action OnAirborne;
        #endregion

        #region Serialized Fields
        [Header("Movement Settings")]
        [SerializeField] private float accelerationForce = 80f;   // Aumentado de 20
        [SerializeField] private float maxVelocity = 12f;          // Aumentado de 8
        [SerializeField] private float groundFriction = 8f;        // Cambiado para ser más directo

        [Header("Dash Settings")]
        [SerializeField] private float dashForce = 25f;            // Aumentado de 15
        [SerializeField] private float dashCooldown = 1.5f;        // Reducido de 2
        [SerializeField] private float dashSpinCost = 30f;         // Reducido de 50

        [Header("Combat Settings")]
        [SerializeField] private float minCollisionVelocity = 1.5f;
        [SerializeField] private float knockbackForce = 5f;

        [Header("Physics")]
        [SerializeField] private float baseDrag = 0.5f;
        [SerializeField] private float baseAngularDrag = 0.1f;
        [SerializeField] private LayerMask groundLayer;

        [Header("Visual")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private ParticleSystem sparkEffect;
        [SerializeField] private ParticleSystem spinEffect;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip collisionSound;
        [SerializeField] private AudioClip dashSound;
        [SerializeField] private AudioClip spinOutSound;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        #endregion

        #region Private Fields
        // Components (cached)
        private Rigidbody _rb;
        private FakeBladeStats _stats;
        private SpecialAbilityHandler _abilityHandler;

        // State
        private float _currentSpinSpeed;
        private float _maxSpinSpeed;
        private float _dashTimer;
        private bool _canDash = true;
        private bool _isDestroyed;
        private bool _isGrounded;
        private bool _wasGrounded;

        // Movement
        private Vector3 _targetVelocity;
        private Vector3 _currentTilt;

        // Cache
        private Transform _transform;
        private static readonly int SpinSpeedProperty = Shader.PropertyToID("_SpinSpeed");
        private MaterialPropertyBlock _propertyBlock;
        private Renderer[] _renderers;
        #endregion

        #region Properties
        public float SpinSpeedPercentage => _maxSpinSpeed > 0 ? _currentSpinSpeed / _maxSpinSpeed : 0f;
        public float CurrentSpinSpeed => _currentSpinSpeed;
        public float MaxSpinSpeed => _maxSpinSpeed;
        public bool CanDash => _canDash && !_isDestroyed;
        public bool IsDestroyed => _isDestroyed;
        public bool IsGrounded => _isGrounded;
        public float Weight => _stats?.Weight ?? 1f;
        public Vector3 Velocity => _rb?.linearVelocity ?? Vector3.zero;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            CacheComponents();
            CacheTransform();
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (_isDestroyed) return;

            UpdateSpin();
            UpdateVisualRotation();
            UpdateDashCooldown();
            UpdateGroundCheck();

            if (showDebugInfo)
            {
                DrawDebugInfo();
            }
        }

        private void FixedUpdate()
        {
            if (_isDestroyed) return;

            ApplyMovementPhysics();
            ApplyTiltPhysics();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_isDestroyed) return;
            HandleCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            // Fricción adicional en contacto prolongado
            if (_isDestroyed) return;

            FakeBladeController other = collision.gameObject.GetComponent<FakeBladeController>();
            if (other != null && !other._isDestroyed)
            {
                // Daño por fricción menor
                float frictionDamage = Time.fixedDeltaTime * 10f;
                ReduceSpin(frictionDamage);
            }
        }
        #endregion

        #region Initialization
        private void CacheComponents()
        {
            _rb = GetComponent<Rigidbody>();
            _stats = GetComponent<FakeBladeStats>();
            _abilityHandler = GetComponent<SpecialAbilityHandler>();
            _renderers = GetComponentsInChildren<Renderer>();
            _propertyBlock = new MaterialPropertyBlock();

            if (_rb == null)
            {
                Debug.LogError($"[FakeBladeController] Rigidbody missing on {gameObject.name}");
            }
        }

        private void CacheTransform()
        {
            _transform = transform;
            if (visualRoot == null)
            {
                visualRoot = _transform;
            }
        }

        private void Initialize()
        {
            ApplyStatsFromComponent();
            ConfigureRigidbody();
            ResetFakeBlade();

            // Suscribirse a cambios de stats
            if (_stats != null)
            {
                _stats.OnStatsChanged += ApplyStatsFromComponent;
            }
        }

        private void ApplyStatsFromComponent()
        {
            if (_stats == null) return;

            _maxSpinSpeed = _stats.MaxSpin;
            dashForce = _stats.DashForce;
            maxVelocity = _stats.MoveSpeed + 3f;
            accelerationForce = _stats.MoveSpeed * 4f;

            ConfigureRigidbody();
        }

        private void ConfigureRigidbody()
        {
            if (_rb == null) return;

            _rb.mass = _stats?.Weight ?? 1f;
            _rb.linearDamping = baseDrag;
            _rb.angularDamping = baseAngularDrag;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
        #endregion

        #region Spin System
        private void UpdateSpin()
        {
            // Decay natural basado en stats
            float decay = _stats?.SpinDecay ?? 5f;
            _currentSpinSpeed = Mathf.Max(0f, _currentSpinSpeed - decay * Time.deltaTime);

            // Notificar cambio
            OnSpinChanged?.Invoke(SpinSpeedPercentage);

            // Actualizar efectos visuales
            UpdateSpinVisuals();

            // Check eliminación
            if (_currentSpinSpeed <= MIN_SPIN_THRESHOLD && !_isDestroyed)
            {
                HandleSpinOut();
            }
        }

        private void UpdateVisualRotation()
        {
            // Rotación visual basada en spin
            float rotationSpeed = _currentSpinSpeed * 0.5f; // Ajustar factor visual
            visualRoot.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }

        private void UpdateSpinVisuals()
        {
            // Actualizar shader properties
            if (_renderers != null && _propertyBlock != null)
            {
                _propertyBlock.SetFloat(SpinSpeedProperty, SpinSpeedPercentage);
                foreach (var renderer in _renderers)
                {
                    if (renderer != null)
                    {
                        renderer.SetPropertyBlock(_propertyBlock);
                    }
                }
            }

            // Efectos de partículas
            if (spinEffect != null)
            {
                var emission = spinEffect.emission;
                emission.rateOverTime = _currentSpinSpeed * 0.1f;
            }
        }

        public void ReduceSpin(float amount)
        {
            if (_isDestroyed || amount <= 0) return;

            // Aplicar defensa
            float defense = _stats?.Defense ?? 0f;
            float actualDamage = amount * (1f - defense * 0.01f);
            actualDamage = Mathf.Max(0.1f, actualDamage); // Daño mínimo

            _currentSpinSpeed = Mathf.Max(0f, _currentSpinSpeed - actualDamage);

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] Spin reduced: {actualDamage:F1} (defense: {defense:F0}%). Current: {_currentSpinSpeed:F0}");
            }
        }

        public void AddSpin(float amount)
        {
            if (_isDestroyed) return;
            _currentSpinSpeed = Mathf.Min(_maxSpinSpeed, _currentSpinSpeed + amount);
        }

        private void HandleSpinOut()
        {
            _isDestroyed = true;

            OnSpinOut?.Invoke();

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] {gameObject.name} SPIN OUT!");
            }

            // Efectos
            PlaySound(spinOutSound);

            // Liberar constraints para caída dramática
            if (_rb != null)
            {
                _rb.constraints = RigidbodyConstraints.None;
                _rb.AddTorque(UnityEngine.Random.insideUnitSphere * 10f, ForceMode.Impulse);
            }

            // Notificar al PlayerController
            var playerController = GetComponent<PlayerController>();
            playerController?.OnFakeBladeDestroyed();

            // Desactivar después de delay
            Invoke(nameof(DeactivateFakeBlade), 1.5f);
        }

        private void DeactivateFakeBlade()
        {
            gameObject.SetActive(false);
        }
        #endregion

        #region Movement
        public void HandleMovement(Vector2 input)
        {
            if (_isDestroyed || _rb == null) return;

            // Convertir input a world space (sin normalizar para mantener magnitud)
            Vector3 desiredDirection = new Vector3(input.x, 0f, input.y);

            if (desiredDirection.sqrMagnitude > 0.01f)
            {
                // Limitar a magnitud 1 máximo
                if (desiredDirection.sqrMagnitude > 1f)
                    desiredDirection.Normalize();

                _targetVelocity = desiredDirection * maxVelocity;
            }
            else
            {
                _targetVelocity = Vector3.zero;
            }
        }

        private void ApplyMovementPhysics()
        {
            if (_rb == null) return;

            Vector3 currentHorizontalVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

            if (_targetVelocity.sqrMagnitude > 0.01f)
            {
                // Movimiento directo y responsivo usando Force
                Vector3 velocityDiff = _targetVelocity - currentHorizontalVel;
                Vector3 force = velocityDiff * accelerationForce;
                _rb.AddForce(force, ForceMode.Force);
            }
            else
            {
                // Frenar rápidamente cuando no hay input
                if (currentHorizontalVel.sqrMagnitude > 0.1f)
                {
                    Vector3 brakeForce = -currentHorizontalVel * groundFriction;
                    _rb.AddForce(brakeForce, ForceMode.Force);
                }
                else
                {
                    // Detener completamente si va muy lento
                    _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                }
            }

            // Limitar velocidad máxima
            float currentSpeed = currentHorizontalVel.magnitude;
            if (currentSpeed > maxVelocity)
            {
                Vector3 limitedVel = currentHorizontalVel.normalized * maxVelocity;
                _rb.linearVelocity = new Vector3(limitedVel.x, _rb.linearVelocity.y, limitedVel.z);
            }
        }

        private void ApplyTiltPhysics()
        {
            if (visualRoot == null) return;

            // Calcular tilt basado en velocidad
            Vector3 velocity = _rb.linearVelocity;
            Vector3 targetTilt = new Vector3(
                -velocity.z * MAX_TILT_ANGLE / maxVelocity,
                0f,
                velocity.x * MAX_TILT_ANGLE / maxVelocity
            );

            // Suavizar tilt
            _currentTilt = Vector3.Lerp(_currentTilt, targetTilt, Time.fixedDeltaTime * TILT_SMOOTHING);

            // Aplicar solo al visual (no al rigidbody)
            // El rigidbody mantiene su rotación bloqueada
        }
        #endregion

        #region Dash
        public void ExecuteDash()
        {
            if (!_canDash || _isDestroyed || _rb == null) return;

            // Coste de spin
            if (_currentSpinSpeed < dashSpinCost)
            {
                if (showDebugInfo) Debug.Log("[FakeBlade] Not enough spin for dash!");
                return;
            }

            // Dirección del dash
            Vector3 dashDirection = _rb.linearVelocity.normalized;
            if (dashDirection.sqrMagnitude < 0.01f)
            {
                dashDirection = _transform.forward;
            }

            // Aplicar dash
            _rb.AddForce(dashDirection * dashForce, ForceMode.Impulse);

            // Consumir spin
            ReduceSpin(dashSpinCost);

            // Cooldown
            _canDash = false;
            _dashTimer = dashCooldown;

            // Efectos
            PlaySound(dashSound);
            OnDashExecuted?.Invoke();

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] Dash! Direction: {dashDirection}, Remaining spin: {_currentSpinSpeed:F0}");
            }
        }

        private void UpdateDashCooldown()
        {
            if (_canDash) return;

            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f)
            {
                _canDash = true;
            }
        }

        public float GetDashCooldownProgress()
        {
            return _canDash ? 1f : 1f - (_dashTimer / dashCooldown);
        }
        #endregion

        #region Special Abilities
        public void ExecuteSpecial()
        {
            if (_isDestroyed) return;

            // Delegar al handler de habilidades si existe
            if (_abilityHandler != null)
            {
                _abilityHandler.ExecuteAbility();
                return;
            }

            // Habilidad por defecto: Spin Boost
            ExecuteDefaultSpecial();
        }

        private void ExecuteDefaultSpecial()
        {
            // Impulso vertical + recuperación de spin
            if (_rb != null)
            {
                _rb.AddForce(Vector3.up * SPECIAL_VERTICAL_FORCE, ForceMode.Impulse);
            }

            AddSpin(100f);

            OnSpecialExecuted?.Invoke(SpecialAbilityType.SpinBoost);

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] Special executed! Spin: {_currentSpinSpeed:F0}");
            }
        }
        #endregion

        #region Combat
        private void HandleCollision(Collision collision)
        {
            // Filtrar colisiones de baja velocidad
            float relativeSpeed = collision.relativeVelocity.magnitude;
            if (relativeSpeed < minCollisionVelocity) return;

            FakeBladeController other = collision.gameObject.GetComponent<FakeBladeController>();

            if (other != null && !other._isDestroyed)
            {
                ProcessFakeBladeCollision(other, collision, relativeSpeed);
            }
            else
            {
                // Colisión con pared u otro objeto
                ProcessEnvironmentCollision(collision, relativeSpeed);
            }
        }

        private void ProcessFakeBladeCollision(FakeBladeController other, Collision collision, float relativeSpeed)
        {
            // Calcular daño base
            float attackPower = _stats?.AttackPower ?? 10f;
            float baseDamage = relativeSpeed * attackPower * COLLISION_DAMAGE_BASE;

            // Factor de peso
            float myWeight = Weight;
            float otherWeight = other.Weight;
            float weightRatio = otherWeight / (myWeight + 0.001f);

            // Factor de spin (quien gira más rápido hace más daño)
            float spinRatio = _currentSpinSpeed / (other._currentSpinSpeed + 0.001f);
            spinRatio = Mathf.Clamp(spinRatio, 0.5f, 2f);

            // Daño final
            float damageToOther = baseDamage * spinRatio * (myWeight / otherWeight);
            float damageToSelf = baseDamage * (1f / spinRatio) * weightRatio;

            // Aplicar daño
            other.ReduceSpin(damageToOther);
            ReduceSpin(damageToSelf);

            // Knockback
            Vector3 knockbackDir = (other._transform.position - _transform.position).normalized;
            knockbackDir.y = 0.1f; // Pequeño lift
            other._rb?.AddForce(knockbackDir * knockbackForce * spinRatio, ForceMode.Impulse);

            // Efectos
            SpawnCollisionEffects(collision.GetContact(0).point);
            PlaySound(collisionSound);

            // Vibración del gamepad
            var inputHandler = GetComponent<InputHandler>();
            inputHandler?.Vibrate(0.3f, 0.5f, 0.15f);

            // Eventos
            OnCollisionWithFakeBlade?.Invoke(other, damageToOther);
            other.OnCollisionWithFakeBlade?.Invoke(this, damageToSelf);

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] CLASH! Speed:{relativeSpeed:F1} Dealt:{damageToOther:F1} Received:{damageToSelf:F1}");
            }
        }

        private void ProcessEnvironmentCollision(Collision collision, float relativeSpeed)
        {
            // Pequeño daño por chocar contra paredes
            float environmentDamage = relativeSpeed * 0.5f;
            ReduceSpin(environmentDamage);

            // Efecto de chispas menor
            if (sparkEffect != null && relativeSpeed > 3f)
            {
                sparkEffect.transform.position = collision.GetContact(0).point;
                sparkEffect.Emit(5);
            }
        }

        private void SpawnCollisionEffects(Vector3 point)
        {
            if (sparkEffect != null)
            {
                sparkEffect.transform.position = point;
                sparkEffect.Emit(15);
            }
        }
        #endregion

        #region Ground Check
        private void UpdateGroundCheck()
        {
            _wasGrounded = _isGrounded;
            _isGrounded = Physics.Raycast(
                _transform.position + Vector3.up * 0.1f,
                Vector3.down,
                GROUND_CHECK_DISTANCE + 0.1f,
                groundLayer
            );

            if (_isGrounded && !_wasGrounded)
            {
                OnGrounded?.Invoke();
            }
            else if (!_isGrounded && _wasGrounded)
            {
                OnAirborne?.Invoke();
            }
        }
        #endregion

        #region Audio
        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
        #endregion

        #region Reset
        public void ResetFakeBlade()
        {
            _currentSpinSpeed = _maxSpinSpeed;
            _canDash = true;
            _dashTimer = 0f;
            _isDestroyed = false;
            _targetVelocity = Vector3.zero;
            _currentTilt = Vector3.zero;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }

            gameObject.SetActive(true);

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] Reset complete. Spin: {_currentSpinSpeed:F0}/{_maxSpinSpeed:F0}");
            }
        }

        public void SetPosition(Vector3 position, Quaternion rotation)
        {
            _transform.SetPositionAndRotation(position, rotation);
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
        }
        #endregion

        #region Debug
        private void DrawDebugInfo()
        {
            // Velocidad
            Debug.DrawRay(_transform.position, _rb.linearVelocity, Color.green);

            // Target velocity
            Debug.DrawRay(_transform.position, _targetVelocity, Color.yellow);

            // Ground check
            Color groundColor = _isGrounded ? Color.green : Color.red;
            Debug.DrawRay(_transform.position + Vector3.up * 0.1f, Vector3.down * (GROUND_CHECK_DISTANCE + 0.1f), groundColor);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            // Spin indicator
            Gizmos.color = Color.Lerp(Color.red, Color.green, SpinSpeedPercentage);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 0.3f);

            // Dash ready indicator
            if (_canDash)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
        }
        #endregion

        #region Cleanup
        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.OnStatsChanged -= ApplyStatsFromComponent;
            }
        }
        #endregion
    }
}