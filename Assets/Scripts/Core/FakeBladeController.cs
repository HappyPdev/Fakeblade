using UnityEngine;
using System;

namespace FakeBlade.Core
{
    /// <summary>
    /// Controlador principal de la peonza (FakeBlade). 
    /// 
    /// === JERARQUÍA REQUERIDA DEL PREFAB ===
    /// 
    /// FakeBlade_Root (este GameObject)          ← Scripts + Rigidbody + Collider
    ///   └── SpinPivot (hijo)                    ← visualRoot, SOLO gira en Y
    ///       ├── Body_Mesh                       ← modelos 3D
    ///       ├── Blade_Mesh
    ///       └── etc.
    /// 
    /// IMPORTANTE:
    /// - El ROOT no rota nunca (FreezeRotationX/Z + no spin visual)
    /// - El SpinPivot es el que gira visualmente
    /// - Si visualRoot no está asignado, se auto-busca el primer hijo
    /// - Si no hay hijos, se crea un pivot vacío automáticamente
    /// 
    /// === POR QUÉ FALLA SI EL ROOT GIRA ===
    /// Si pones la rotación de spin en el root (donde está el Rigidbody),
    /// las fuerzas en world space se "suman" siempre a la orientación actual
    /// del transform rotado, causando que la peonza solo vaya en una dirección.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(FakeBladeStats))]
    public class FakeBladeController : MonoBehaviour
    {
        #region Constants
        private const float MIN_SPIN_THRESHOLD = 0.1f;
        private const float COLLISION_DAMAGE_BASE = 0.15f;
        private const float SPECIAL_VERTICAL_FORCE = 3f;
        private const float GROUND_CHECK_DISTANCE = 0.15f;
        private const float TILT_SMOOTHING = 5f;
        private const float MAX_TILT_ANGLE = 20f;
        private const float VISUAL_SPIN_MULTIPLIER = 6f;
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
        [Header("=== HIERARCHY ===")]
        [Tooltip("El hijo que gira visualmente. Si está vacío, se busca/crea automáticamente. " +
                 "NUNCA debe ser el propio root - tiene que ser un hijo.")]
        [SerializeField] private Transform visualRoot;

        [Header("=== MOVEMENT (Inertia-Based) ===")]
        [SerializeField] private float accelerationForce = 35f;
        [SerializeField] private float maxVelocity = 10f;
        [SerializeField] private float stoppingFriction = 3f;
        [SerializeField][Range(0.01f, 1f)] private float turnResponsiveness = 0.15f;

        [Header("=== DASH ===")]
        [SerializeField] private float dashForce = 20f;
        [SerializeField] private float dashCooldown = 1.5f;
        [SerializeField] private float dashSpinCost = 25f;

        [Header("=== COMBAT ===")]
        [SerializeField] private float minCollisionVelocity = 1.5f;
        [SerializeField] private float knockbackForce = 8f;

        [Header("=== PHYSICS ===")]
        [SerializeField] private float baseDrag = 1.5f;
        [SerializeField] private float baseAngularDrag = 0.5f;
        [SerializeField] private LayerMask groundLayer;

        [Header("=== VISUAL ===")]
        [SerializeField] private ParticleSystem sparkEffect;
        [SerializeField] private ParticleSystem spinEffect;

        [Header("=== AUDIO ===")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip collisionSound;
        [SerializeField] private AudioClip dashSound;
        [SerializeField] private AudioClip spinOutSound;

        [Header("=== DEBUG ===")]
        [SerializeField] private bool showDebugInfo = false;
        #endregion

        #region Private Fields
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
        private Vector3 _inputDirection;
        private Vector3 _smoothedDirection;
        private float _currentSpeed;
        private Vector3 _currentTilt;

        // Stats derivadas
        private float _effectiveAcceleration;
        private float _effectiveMaxSpeed;
        private float _effectiveTurnSpeed;
        private float _effectiveDrag;

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
            SetupVisualRoot();
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

            if (showDebugInfo) DrawDebugInfo();
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
            if (_isDestroyed) return;
            FakeBladeController other = collision.gameObject.GetComponent<FakeBladeController>();
            if (other != null && !other._isDestroyed)
            {
                ReduceSpin(Time.fixedDeltaTime * 8f);
            }
        }
        #endregion

        #region Initialization
        private void CacheComponents()
        {
            _rb = GetComponent<Rigidbody>();
            _stats = GetComponent<FakeBladeStats>();
            _abilityHandler = GetComponent<SpecialAbilityHandler>();
            _propertyBlock = new MaterialPropertyBlock();

            if (_rb == null)
                Debug.LogError($"[FakeBladeController] Rigidbody missing on {gameObject.name}");
        }

        private void CacheTransform()
        {
            _transform = transform;
        }

        /// <summary>
        /// Configura el visualRoot correctamente.
        /// 
        /// REGLAS:
        /// 1. Si visualRoot ya está asignado Y es un hijo → OK
        /// 2. Si visualRoot es el propio root → ERROR, reparar
        /// 3. Si no hay visualRoot → buscar primer hijo, o crear pivot
        /// </summary>
        private void SetupVisualRoot()
        {
            // Caso 1: Ya está asignado y es un hijo válido
            if (visualRoot != null && visualRoot != _transform && visualRoot.parent == _transform)
            {
                if (showDebugInfo)
                    Debug.Log($"[FakeBlade] visualRoot OK: {visualRoot.name}");
                CacheRenderers();
                return;
            }

            // Caso 2: visualRoot es el propio root - ESTO CAUSA EL BUG
            if (visualRoot == _transform || visualRoot == null)
            {
                // Buscar primer hijo que NO sea un collider suelto
                Transform bestChild = null;
                for (int i = 0; i < _transform.childCount; i++)
                {
                    Transform child = _transform.GetChild(i);
                    // Preferir hijos que tengan renderers (los modelos 3D)
                    if (child.GetComponentInChildren<Renderer>() != null)
                    {
                        bestChild = child;
                        break;
                    }
                }

                if (bestChild != null)
                {
                    visualRoot = bestChild;
                    Debug.Log($"[FakeBlade] visualRoot auto-assigned to child: {bestChild.name}");
                }
                else
                {
                    // No hay hijos con renderers - crear un pivot vacío y mover todos los hijos dentro
                    GameObject pivot = new GameObject("SpinPivot");
                    pivot.transform.SetParent(_transform, false);
                    pivot.transform.localPosition = Vector3.zero;
                    pivot.transform.localRotation = Quaternion.identity;

                    // Mover todos los hijos existentes dentro del pivot
                    // (iterar en reversa para no saltarnos hijos al re-parentar)
                    for (int i = _transform.childCount - 1; i >= 0; i--)
                    {
                        Transform child = _transform.GetChild(i);
                        if (child != pivot.transform)
                        {
                            child.SetParent(pivot.transform, true);
                        }
                    }

                    visualRoot = pivot.transform;
                    Debug.Log($"[FakeBlade] Created SpinPivot and moved {pivot.transform.childCount} children into it");
                }
            }

            CacheRenderers();
        }

        private void CacheRenderers()
        {
            // Buscar renderers en el visualRoot y sus hijos
            if (visualRoot != null)
            {
                _renderers = visualRoot.GetComponentsInChildren<Renderer>();
            }
            else
            {
                _renderers = GetComponentsInChildren<Renderer>();
            }
        }

        private void Initialize()
        {
            ApplyStatsFromComponent();
            ConfigureRigidbody();
            ResetFakeBlade();

            if (_stats != null)
                _stats.OnStatsChanged += ApplyStatsFromComponent;

            // SIEMPRE logear la inicialización para diagnosticar
            Debug.Log($"[FB-INIT] {gameObject.name} | " +
                $"Mass:{_rb?.mass:F2} Drag:{_rb?.linearDamping:F2} Gravity:{_rb?.useGravity} | " +
                $"Accel:{_effectiveAcceleration:F1} MaxSpd:{_effectiveMaxSpeed:F1} Turn:{_effectiveTurnSpeed:F3} Drag:{_effectiveDrag:F2} | " +
                $"Weight:{_stats?.Weight:F1} MoveSpd:{_stats?.MoveSpeed:F1} MaxSpin:{_maxSpinSpeed:F0} | " +
                $"visualRoot:{(visualRoot != null ? visualRoot.name : "NULL")} isChild:{(visualRoot != null && visualRoot != _transform)} | " +
                $"Constraints:{_rb?.constraints} | " +
                $"Pos:{_transform.position} Collider:{GetComponent<Collider>()?.GetType().Name ?? "NONE"}");
        }

        private void ApplyStatsFromComponent()
        {
            if (_stats == null) return;

            float weight = _stats.Weight;
            float moveSpeed = _stats.MoveSpeed;

            _maxSpinSpeed = _stats.MaxSpin;
            dashForce = _stats.DashForce;

            float weightNormalized = Mathf.InverseLerp(0.5f, 3f, weight);

            _effectiveAcceleration = accelerationForce * Mathf.Max(moveSpeed * 0.1f, 1f) * Mathf.Lerp(1.5f, 0.5f, weightNormalized);
            _effectiveAcceleration = Mathf.Clamp(_effectiveAcceleration, 5f, 80f); // Nunca más de 80
            _effectiveMaxSpeed = Mathf.Clamp(maxVelocity + moveSpeed * Mathf.Lerp(0.8f, 0.4f, weightNormalized), 3f, 20f);
            _effectiveTurnSpeed = turnResponsiveness * Mathf.Lerp(2.5f, 0.5f, weightNormalized);
            _effectiveDrag = stoppingFriction * Mathf.Lerp(1.5f, 0.4f, weightNormalized);

            ConfigureRigidbody();

            if (showDebugInfo)
            {
                Debug.Log($"[FakeBlade] Stats: W:{weight:F1} Spd:{moveSpeed:F1} " +
                         $"Accel:{_effectiveAcceleration:F1} MaxSpd:{_effectiveMaxSpeed:F1} " +
                         $"Turn:{_effectiveTurnSpeed:F2} Drag:{_effectiveDrag:F2}");
            }
        }

        private void ConfigureRigidbody()
        {
            if (_rb == null) return;

            _rb.mass = _stats?.Weight ?? 1f;
            _rb.linearDamping = baseDrag;
            _rb.angularDamping = baseAngularDrag;
            _rb.useGravity = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX |
                             RigidbodyConstraints.FreezeRotationY |
                             RigidbodyConstraints.FreezeRotationZ;
            _rb.centerOfMass = new Vector3(0, -0.15f, 0); // Centro de masa bajo = más estable
        }
        #endregion

        #region Spin System
        private void UpdateSpin()
        {
            float decay = _stats?.SpinDecay ?? 3f;
            _currentSpinSpeed = Mathf.Max(0f, _currentSpinSpeed - decay * Time.deltaTime);

            OnSpinChanged?.Invoke(SpinSpeedPercentage);
            UpdateSpinVisuals();

            if (_currentSpinSpeed <= MIN_SPIN_THRESHOLD && !_isDestroyed)
                HandleSpinOut();
        }

        /// <summary>
        /// Rota SOLO el visualRoot (hijo), NO el root.
        /// El root del Rigidbody NUNCA debe rotar.
        /// </summary>
        private void UpdateVisualRotation()
        {
            if (visualRoot == null || visualRoot == _transform) return;

            float rotationSpeed = _currentSpinSpeed * VISUAL_SPIN_MULTIPLIER;
            visualRoot.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }

        private void UpdateSpinVisuals()
        {
            if (_renderers != null && _propertyBlock != null)
            {
                _propertyBlock.SetFloat(SpinSpeedProperty, SpinSpeedPercentage);
                foreach (var renderer in _renderers)
                {
                    if (renderer != null)
                        renderer.SetPropertyBlock(_propertyBlock);
                }
            }

            if (spinEffect != null)
            {
                var emission = spinEffect.emission;
                emission.rateOverTime = _currentSpinSpeed * 0.1f;
            }
        }

        public void ReduceSpin(float amount)
        {
            if (_isDestroyed || amount <= 0) return;

            float defense = _stats?.Defense ?? 0f;
            float actualDamage = amount * (1f - defense * 0.01f);
            actualDamage = Mathf.Max(0.1f, actualDamage);
            _currentSpinSpeed = Mathf.Max(0f, _currentSpinSpeed - actualDamage);

            if (showDebugInfo)
                Debug.Log($"[FakeBlade] Spin: -{actualDamage:F1} (def:{defense:F0}%) = {_currentSpinSpeed:F0}");
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

            if (showDebugInfo) Debug.Log($"[FakeBlade] {gameObject.name} SPIN OUT!");

            PlaySound(spinOutSound);

            if (_rb != null)
            {
                _rb.constraints = RigidbodyConstraints.None;
                _rb.AddTorque(UnityEngine.Random.insideUnitSphere * 10f, ForceMode.Impulse);
            }

            GetComponent<PlayerController>()?.OnFakeBladeDestroyed();
            Invoke(nameof(DeactivateFakeBlade), 1.5f);
        }

        private void DeactivateFakeBlade() => gameObject.SetActive(false);
        #endregion

        #region Movement
        public void HandleMovement(Vector2 input)
        {
            if (_isDestroyed || _rb == null) return;

            // Dead zone de seguridad
            if (input.sqrMagnitude < 0.04f)
            {
                _inputDirection = Vector3.zero;
                return;
            }

            Vector3 desiredDirection = new Vector3(input.x, 0f, input.y);
            if (desiredDirection.sqrMagnitude > 1f)
                desiredDirection.Normalize();

            _inputDirection = desiredDirection;
        }

        // Debug timer
        private float _debugTimer;
        private int _debugFrameCount;

        /// <summary>
        /// TODAS las fuerzas se aplican en WORLD SPACE.
        /// Como el root NUNCA rota, world space es siempre consistente.
        /// </summary>
        private void ApplyMovementPhysics()
        {
            if (_rb == null) return;

            // ===== DEBUG =====
            _debugTimer += Time.fixedDeltaTime;
            _debugFrameCount++;
            bool shouldLog = false;
            float logInterval = Time.timeSinceLevelLoad < 5f ? 0.5f : 2f;
            if (_debugTimer >= logInterval)
            {
                _debugTimer = 0f;
                shouldLog = true;
            }

            if (shouldLog)
            {
                Debug.Log($"[FB-DEBUG] {gameObject.name} | " +
                    $"Pos:({_transform.position.x:F1},{_transform.position.y:F1},{_transform.position.z:F1}) | " +
                    $"Vel:({_rb.linearVelocity.x:F2},{_rb.linearVelocity.y:F2},{_rb.linearVelocity.z:F2}) Spd:{_currentSpeed:F2} | " +
                    $"Input:({_inputDirection.x:F2},{_inputDirection.z:F2}) | " +
                    $"SmDir:({_smoothedDirection.x:F2},{_smoothedDirection.z:F2}) | " +
                    $"Accel:{_effectiveAcceleration:F1} MaxSpd:{_effectiveMaxSpeed:F1} Turn:{_effectiveTurnSpeed:F3} | " +
                    $"Mass:{_rb.mass:F2} LinDamp:{_rb.linearDamping:F2} Grounded:{_isGrounded}");
            }
            // ===== FIN DEBUG =====

            // Anti-flotación
            if (_rb.linearVelocity.y > 2f)
            {
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 2f, _rb.linearVelocity.z);
            }

            Vector3 currentHorizontalVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            _currentSpeed = currentHorizontalVel.magnitude;

            if (_inputDirection.sqrMagnitude > 0.01f)
            {
                // === DIRECCIÓN: respuesta directa con algo de suavizado ===
                if (_smoothedDirection.sqrMagnitude < 0.01f)
                {
                    // Primera vez: dirección inmediata
                    _smoothedDirection = _inputDirection.normalized;
                }
                else
                {
                    // Suavizado de giro - MUCHO más rápido que antes
                    // turnResponsiveness 0.15 base → effectiveTurnSpeed ~0.315
                    // Multiplicador alto (60) para que sea arcade y responsivo
                    // Light: 0.375 * 0.02 * 60 = 0.45 por frame → giro en ~5 frames
                    // Heavy: 0.075 * 0.02 * 60 = 0.09 por frame → giro en ~20 frames
                    float turnLerp = Mathf.Clamp01(_effectiveTurnSpeed * Time.fixedDeltaTime * 60f);
                    _smoothedDirection = Vector3.Lerp(
                        _smoothedDirection,
                        _inputDirection.normalized,
                        turnLerp
                    ).normalized;
                }

                // === FUERZA DE MOVIMIENTO ===
                Vector3 desiredVelocity = _smoothedDirection * _effectiveMaxSpeed * _inputDirection.magnitude;
                Vector3 velocityDifference = desiredVelocity - currentHorizontalVel;

                float forceMagnitude = _effectiveAcceleration;

                // Reducir fuerza cerca de velocidad máxima
                float speedRatio = _currentSpeed / Mathf.Max(_effectiveMaxSpeed, 0.1f);
                forceMagnitude *= Mathf.Lerp(1f, 0.15f, speedRatio);

                // Fuerza en WORLD SPACE, sin componente Y
                Vector3 force = velocityDifference.normalized * forceMagnitude;
                force.y = 0f;
                _rb.AddForce(force, ForceMode.Force);
            }
            else
            {
                // Sin input: frenar
                // Resetear smoothedDirection para que la próxima pulsación sea instantánea
                if (_currentSpeed < 0.5f)
                {
                    _smoothedDirection = Vector3.zero;
                }

                if (_currentSpeed > 0.05f)
                {
                    Vector3 brakeForce = -currentHorizontalVel.normalized * _effectiveDrag * _rb.mass;
                    _rb.AddForce(brakeForce, ForceMode.Force);
                }
                else if (_currentSpeed > 0f)
                {
                    _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                }
            }

            // Limitar velocidad máxima
            if (_currentSpeed > _effectiveMaxSpeed)
            {
                Vector3 clampedVel = currentHorizontalVel.normalized * _effectiveMaxSpeed;
                _rb.linearVelocity = new Vector3(clampedVel.x, _rb.linearVelocity.y, clampedVel.z);
            }
        }

        /// <summary>
        /// Tilt visual aplicado al visualRoot, no al root.
        /// </summary>
        private void ApplyTiltPhysics()
        {
            if (visualRoot == null || visualRoot == _transform) return;

            Vector3 velocity = _rb.linearVelocity;
            float speedRatio = Mathf.Clamp01(_currentSpeed / Mathf.Max(_effectiveMaxSpeed, 0.1f));

            Vector3 targetTilt = new Vector3(
                -velocity.z * MAX_TILT_ANGLE * speedRatio / Mathf.Max(_effectiveMaxSpeed, 0.1f),
                0f,
                velocity.x * MAX_TILT_ANGLE * speedRatio / Mathf.Max(_effectiveMaxSpeed, 0.1f)
            );

            _currentTilt = Vector3.Lerp(_currentTilt, targetTilt, Time.fixedDeltaTime * TILT_SMOOTHING);

            // NOTA: No podemos hacer localRotation = Euler(tilt) porque eso
            // resetea la rotación de spin. En su lugar, el spin se acumula
            // en UpdateVisualRotation() y el tilt se aplica como offset.
            // Solución: separar spin y tilt usando la rotación actual del spin.
            float currentSpinAngle = visualRoot.localEulerAngles.y;
            visualRoot.localRotation = Quaternion.Euler(_currentTilt.x, currentSpinAngle, _currentTilt.z);
        }
        #endregion

        #region Dash
        public void ExecuteDash()
        {
            if (!_canDash || _isDestroyed || _rb == null) return;

            if (_currentSpinSpeed < dashSpinCost)
            {
                if (showDebugInfo) Debug.Log("[FakeBlade] Not enough spin for dash!");
                return;
            }

            Vector3 dashDirection;
            if (_currentSpeed > 1f)
                dashDirection = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).normalized;
            else if (_smoothedDirection.sqrMagnitude > 0.01f)
                dashDirection = _smoothedDirection;
            else if (_inputDirection.sqrMagnitude > 0.01f)
                dashDirection = _inputDirection.normalized;
            else
                dashDirection = Vector3.forward; // Fallback world forward, no transform.forward

            dashDirection.y = 0f;
            dashDirection.Normalize();

            float weightedDash = dashForce * Mathf.Lerp(0.8f, 1.4f, Mathf.InverseLerp(0.5f, 3f, Weight));
            _rb.AddForce(dashDirection * weightedDash, ForceMode.Impulse);

            ReduceSpin(dashSpinCost);
            _canDash = false;
            _dashTimer = dashCooldown;

            PlaySound(dashSound);
            OnDashExecuted?.Invoke();

            if (showDebugInfo)
                Debug.Log($"[FakeBlade] Dash! Dir:{dashDirection} Force:{weightedDash:F1}");
        }

        private void UpdateDashCooldown()
        {
            if (_canDash) return;
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f) _canDash = true;
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

            if (_abilityHandler != null)
            {
                _abilityHandler.ExecuteAbility();
                return;
            }

            // Default: SpinBoost
            if (_rb != null)
                _rb.AddForce(Vector3.up * SPECIAL_VERTICAL_FORCE, ForceMode.Impulse);

            AddSpin(100f);
            OnSpecialExecuted?.Invoke(SpecialAbilityType.SpinBoost);

            if (showDebugInfo)
                Debug.Log($"[FakeBlade] Default special! Spin: {_currentSpinSpeed:F0}");
        }
        #endregion

        #region Combat
        private void HandleCollision(Collision collision)
        {
            float relativeSpeed = collision.relativeVelocity.magnitude;

            Debug.Log($"[FB-COLLISION] {gameObject.name} hit {collision.gameObject.name} | " +
                $"RelSpd:{relativeSpeed:F2} | ContactNormal:{collision.GetContact(0).normal} | " +
                $"MyVel:{_rb.linearVelocity} MyPos:{_transform.position}");

            if (relativeSpeed < minCollisionVelocity) return;

            FakeBladeController other = collision.gameObject.GetComponent<FakeBladeController>();

            if (other != null && !other._isDestroyed)
                ProcessFakeBladeCollision(other, collision, relativeSpeed);
            else
                ProcessEnvironmentCollision(collision, relativeSpeed);
        }

        private void ProcessFakeBladeCollision(FakeBladeController other, Collision collision, float relativeSpeed)
        {
            float attackPower = _stats?.AttackPower ?? 10f;
            float baseDamage = relativeSpeed * attackPower * COLLISION_DAMAGE_BASE;

            float myWeight = Weight;
            float otherWeight = other.Weight;

            float spinRatio = _currentSpinSpeed / (other._currentSpinSpeed + 0.001f);
            spinRatio = Mathf.Clamp(spinRatio, 0.5f, 2f);

            float damageToOther = baseDamage * spinRatio * (myWeight / (otherWeight + 0.001f));
            float damageToSelf = baseDamage * (1f / spinRatio) * (otherWeight / (myWeight + 0.001f));

            other.ReduceSpin(damageToOther);
            ReduceSpin(damageToSelf);

            Vector3 knockbackDir = (other._transform.position - _transform.position);
            knockbackDir.y = 0f; // NUNCA empujar hacia arriba
            knockbackDir.Normalize();

            float knockbackToOther = knockbackForce * spinRatio * (myWeight / (otherWeight + 0.001f));
            float knockbackToSelf = knockbackForce * (1f / spinRatio) * (otherWeight / (myWeight + 0.001f)) * 0.3f;

            // Clamp para evitar lanzar a la órbita
            knockbackToOther = Mathf.Min(knockbackToOther, knockbackForce * 3f);
            knockbackToSelf = Mathf.Min(knockbackToSelf, knockbackForce * 2f);

            other._rb?.AddForce(knockbackDir * knockbackToOther, ForceMode.Impulse);
            _rb?.AddForce(-knockbackDir * knockbackToSelf, ForceMode.Impulse);

            SpawnCollisionEffects(collision.GetContact(0).point);
            PlaySound(collisionSound);

            GetComponent<InputHandler>()?.Vibrate(0.3f, 0.5f, 0.15f);

            OnCollisionWithFakeBlade?.Invoke(other, damageToOther);
            other.OnCollisionWithFakeBlade?.Invoke(this, damageToSelf);

            if (showDebugInfo)
                Debug.Log($"[FakeBlade] CLASH! Spd:{relativeSpeed:F1} Dealt:{damageToOther:F1} Recv:{damageToSelf:F1}");
        }

        private void ProcessEnvironmentCollision(Collision collision, float relativeSpeed)
        {
            ReduceSpin(relativeSpeed * 0.5f);

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

            // Si groundLayer está configurado, usarlo. Si no, raycast sin máscara (todo).
            if (groundLayer.value != 0)
            {
                _isGrounded = Physics.Raycast(
                    _transform.position + Vector3.up * 0.1f,
                    Vector3.down,
                    GROUND_CHECK_DISTANCE + 0.1f,
                    groundLayer
                );
            }
            else
            {
                // Raycast contra todo excepto triggers
                _isGrounded = Physics.Raycast(
                    _transform.position + Vector3.up * 0.1f,
                    Vector3.down,
                    GROUND_CHECK_DISTANCE + 0.1f
                );
            }

            if (_isGrounded && !_wasGrounded) OnGrounded?.Invoke();
            else if (!_isGrounded && _wasGrounded) OnAirborne?.Invoke();
        }
        #endregion

        #region Audio
        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }
        #endregion

        #region Reset
        public void ResetFakeBlade()
        {
            _currentSpinSpeed = _maxSpinSpeed;
            _canDash = true;
            _dashTimer = 0f;
            _isDestroyed = false;
            _inputDirection = Vector3.zero;
            _smoothedDirection = Vector3.zero;
            _currentTilt = Vector3.zero;
            _currentSpeed = 0f;

            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.constraints = RigidbodyConstraints.FreezeRotation; // Bloquear toda rotación
            }

            // Reset rotación del visual
            if (visualRoot != null && visualRoot != _transform)
            {
                visualRoot.localRotation = Quaternion.identity;
            }

            gameObject.SetActive(true);

            if (showDebugInfo)
                Debug.Log($"[FakeBlade] Reset. Spin:{_currentSpinSpeed:F0}/{_maxSpinSpeed:F0}");
        }

        public void SetPosition(Vector3 position, Quaternion rotation)
        {
            _transform.SetPositionAndRotation(position, Quaternion.identity); // Root siempre sin rotación
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
            Debug.DrawRay(_transform.position, _rb.linearVelocity, Color.green);
            Debug.DrawRay(_transform.position, _smoothedDirection * 3f, Color.yellow);
            Debug.DrawRay(_transform.position, _inputDirection * 2f, Color.red);

            Color groundColor = _isGrounded ? Color.green : Color.red;
            Debug.DrawRay(_transform.position + Vector3.up * 0.1f, Vector3.down * (GROUND_CHECK_DISTANCE + 0.1f), groundColor);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Gizmos.color = Color.Lerp(Color.red, Color.green, SpinSpeedPercentage);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 0.3f);

            if (_canDash)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }

            // Mostrar dirección suavizada
            if (_smoothedDirection.sqrMagnitude > 0.01f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, transform.position + _smoothedDirection * 2f);
            }
        }
        #endregion

        #region Cleanup
        private void OnDestroy()
        {
            if (_stats != null)
                _stats.OnStatsChanged -= ApplyStatsFromComponent;
        }
        #endregion
    }
}