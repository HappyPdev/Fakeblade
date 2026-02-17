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
        [Tooltip("Fuerza de aceleración base")]
        [SerializeField] private float accelerationForce = 35f;
        [Tooltip("Velocidad máxima de movimiento normal (bajar para más contraste con dash)")]
        [SerializeField] private float maxVelocity = 5f;
        [SerializeField] private float stoppingFriction = 3f;
        [SerializeField][Range(0.01f, 1f)] private float turnResponsiveness = 0.15f;

        [Header("=== DASH ===")]
        [Tooltip("Fuerza de impulso del dash (subir para dash más explosivo)")]
        [SerializeField] private float dashForce = 30f;
        [SerializeField] private float dashCooldown = 1.2f;
        [SerializeField] private float dashSpinCost = 10f;

        [Header("=== COMBAT ===")]
        [SerializeField] private float minCollisionVelocity = 1.5f;
        [SerializeField] private float knockbackForce = 8f;

        [Header("=== PHYSICS ===")]
        [SerializeField] private float baseDrag = 1.5f;
        [SerializeField] private float baseAngularDrag = 0.5f;
        [SerializeField] private LayerMask groundLayer;

        [Header("=== VISUAL FX ===")]
        [Tooltip("Chispas al chocar entre peonzas")]
        [SerializeField] private ParticleSystem vfxCollision;
        [Tooltip("Trail de partículas en la base mientras se mueve")]
        [SerializeField] private ParticleSystem vfxSpinTrail;
        [Tooltip("Explosión de partículas al hacer dash")]
        [SerializeField] private ParticleSystem vfxDash;
        [Tooltip("Efecto al usar habilidad especial")]
        [SerializeField] private ParticleSystem vfxSpecial;
        [Tooltip("Efecto al ser eliminada (spin out)")]
        [SerializeField] private ParticleSystem vfxSpinOut;

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

        // Dash armor: protección temporal al dashear
        private bool _isDashing;
        private float _dashArmorTimer;
        private const float DASH_ARMOR_DURATION = 0.4f;     // Ventana de protección al dashear
        private const float DASH_ARMOR_REDUCTION = 0.8f;    // 80% reducción de daño recibido durante dash
        private const float DASH_ATTACKER_BONUS = 0.6f;     // Solo recibe 40% del daño al atacar con dash

        // Movement
        private Vector3 _inputDirection;
        private Vector3 _smoothedDirection;
        private float _currentSpeed;
        private Vector3 _currentTilt;
        private Transform _tiltPivot; // Nodo intermedio para tilt, padre del SpinPivot

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
        public bool IsDashing => _isDashing;
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
        /// Configura la jerarquía visual:
        /// 
        /// FakeBlade_Root (Rigidbody, NUNCA rota)
        ///   └── TiltPivot (_tiltPivot - solo inclinación X/Z, NO gira en Y)
        ///        └── SpinPivot (visualRoot - solo gira en Y)
        ///             ├── Body_Mesh
        ///             └── Blade_Mesh
        /// 
        /// El tilt y el spin están en transforms separados para evitar
        /// que la rotación de spin a alta velocidad distorsione la inclinación.
        /// </summary>
        private void SetupVisualRoot()
        {
            // Paso 1: Encontrar o crear el SpinPivot (visualRoot)
            if (visualRoot == null || visualRoot == _transform)
            {
                // Buscar primer hijo con renderers
                Transform bestChild = null;
                for (int i = 0; i < _transform.childCount; i++)
                {
                    Transform child = _transform.GetChild(i);
                    if (child.GetComponentInChildren<Renderer>() != null)
                    {
                        bestChild = child;
                        break;
                    }
                }

                if (bestChild != null)
                {
                    visualRoot = bestChild;
                }
                else
                {
                    // Crear SpinPivot y mover todos los hijos dentro
                    GameObject pivot = new GameObject("SpinPivot");
                    pivot.transform.SetParent(_transform, false);
                    pivot.transform.localPosition = Vector3.zero;
                    pivot.transform.localRotation = Quaternion.identity;

                    for (int i = _transform.childCount - 1; i >= 0; i--)
                    {
                        Transform child = _transform.GetChild(i);
                        if (child != pivot.transform)
                            child.SetParent(pivot.transform, true);
                    }

                    visualRoot = pivot.transform;
                    Debug.Log($"[FakeBlade] Created SpinPivot with {pivot.transform.childCount} children");
                }
            }

            // Paso 2: Crear TiltPivot entre Root y SpinPivot
            // Comprobar si ya existe un TiltPivot
            _tiltPivot = _transform.Find("TiltPivot");

            if (_tiltPivot == null)
            {
                // Crear TiltPivot
                GameObject tiltObj = new GameObject("TiltPivot");
                tiltObj.transform.SetParent(_transform, false);
                tiltObj.transform.localPosition = Vector3.zero;
                tiltObj.transform.localRotation = Quaternion.identity;
                _tiltPivot = tiltObj.transform;

                // Re-parentar SpinPivot dentro de TiltPivot
                visualRoot.SetParent(_tiltPivot, false);
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;

                Debug.Log($"[FakeBlade] Created TiltPivot. Hierarchy: {_transform.name} > TiltPivot > {visualRoot.name}");
            }
            else
            {
                // TiltPivot ya existe, asegurar que SpinPivot está dentro
                if (visualRoot.parent != _tiltPivot)
                {
                    visualRoot.SetParent(_tiltPivot, false);
                    visualRoot.localPosition = Vector3.zero;
                }
            }

            CacheRenderers();

            if (showDebugInfo)
                Debug.Log($"[FakeBlade] Hierarchy OK: {_transform.name} > {_tiltPivot.name} > {visualRoot.name} ({_renderers?.Length ?? 0} renderers)");
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
        /// Rota SOLO el SpinPivot (visualRoot) en Y.
        /// La inclinación viene del TiltPivot padre, no se toca aquí.
        /// </summary>
        private void UpdateVisualRotation()
        {
            if (visualRoot == null || visualRoot == _transform) return;

            float rotationSpeed = _currentSpinSpeed * VISUAL_SPIN_MULTIPLIER;
            // Solo rotar en Y local - el tilt viene del padre (TiltPivot)
            visualRoot.localRotation = Quaternion.Euler(0f, visualRoot.localEulerAngles.y + rotationSpeed * Time.deltaTime, 0f);
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

            // Spin trail: emite más partículas cuanto más rápido gira
            if (vfxSpinTrail != null)
            {
                var emission = vfxSpinTrail.emission;
                emission.rateOverTime = _currentSpinSpeed * 0.1f + _currentSpeed * 2f;
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

            // VFX: explosión de eliminación
            if (vfxSpinOut != null)
            {
                vfxSpinOut.transform.position = _transform.position;
                vfxSpinOut.Play();
            }

            // Camera shake fuerte
            CameraShake.Shake(0.25f, 0.2f);

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

        /// <summary>
        /// Sistema de movimiento: fuerza directa en dirección del input + frenado lateral.
        /// 
        /// FILOSOFÍA:
        /// - La fuerza se aplica DIRECTAMENTE en la dirección que el jugador quiere ir
        /// - La velocidad perpendicular al input se frena activamente (giro)
        /// - Cuanto más pesada la peonza, más lento frena lateralmente (inercia de giro)
        /// - Cuanto más ligera, más instantáneo el cambio de dirección
        /// </summary>
        private void ApplyMovementPhysics()
        {
            if (_rb == null) return;

            // ===== DEBUG =====
            _debugTimer += Time.fixedDeltaTime;
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
                    $"Accel:{_effectiveAcceleration:F1} MaxSpd:{_effectiveMaxSpeed:F1} Turn:{_effectiveTurnSpeed:F3} | " +
                    $"Mass:{_rb.mass:F2} LinDamp:{_rb.linearDamping:F2} Grounded:{_isGrounded}");
            }

            // Anti-flotación
            if (_rb.linearVelocity.y > 2f)
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 2f, _rb.linearVelocity.z);

            Vector3 vel = _rb.linearVelocity;
            Vector3 horizontalVel = new Vector3(vel.x, 0f, vel.z);
            _currentSpeed = horizontalVel.magnitude;

            if (_inputDirection.sqrMagnitude > 0.01f)
            {
                Vector3 inputDir = _inputDirection.normalized;
                float inputMag = _inputDirection.magnitude;

                // === 1. FUERZA DE ACELERACIÓN: directa en la dirección del input ===
                float accelForce = _effectiveAcceleration;

                // Reducir aceleración si ya estamos a tope en ESA dirección
                float speedInInputDir = Vector3.Dot(horizontalVel, inputDir);
                float targetSpeed = _effectiveMaxSpeed * inputMag;
                if (speedInInputDir > 0f)
                {
                    float ratio = speedInInputDir / targetSpeed;
                    accelForce *= Mathf.Clamp01(1f - ratio);
                }

                Vector3 driveForce = inputDir * accelForce;
                driveForce.y = 0f;
                _rb.AddForce(driveForce, ForceMode.Force);

                // === 2. FRENADO LATERAL: frenar la velocidad perpendicular al input ===
                // Esto es lo que permite cambiar de dirección
                Vector3 lateralVel = horizontalVel - inputDir * speedInInputDir;

                if (lateralVel.sqrMagnitude > 0.01f)
                {
                    // turnSpeed controla cuánto frenamos lateralmente
                    // Light: frena lateral rápido → giro responsivo
                    // Heavy: frena lateral lento → derrapa más
                    float lateralBrakeStrength = _effectiveTurnSpeed * 120f; // Multiplicador alto para arcade
                    lateralBrakeStrength = Mathf.Clamp(lateralBrakeStrength, 5f, 50f);

                    Vector3 lateralBrake = -lateralVel * lateralBrakeStrength;
                    lateralBrake.y = 0f;
                    _rb.AddForce(lateralBrake, ForceMode.Force);
                }

                // === 3. FRENADO CONTRARIO: si vamos en dirección opuesta al input, frenar más ===
                if (speedInInputDir < -0.5f)
                {
                    Vector3 counterBrake = -horizontalVel.normalized * _effectiveAcceleration * 0.5f;
                    counterBrake.y = 0f;
                    _rb.AddForce(counterBrake, ForceMode.Force);
                }

                // Mantener smoothedDirection para el visual tilt (no afecta física)
                _smoothedDirection = inputDir;
            }
            else
            {
                // Sin input: frenar progresivamente
                _smoothedDirection = Vector3.zero;

                if (_currentSpeed > 0.1f)
                {
                    Vector3 brakeForce = -horizontalVel * _effectiveDrag;
                    brakeForce.y = 0f;
                    _rb.AddForce(brakeForce, ForceMode.Force);
                }
                else if (_currentSpeed > 0f)
                {
                    _rb.linearVelocity = new Vector3(0f, vel.y, 0f);
                }
            }

            // Limitar velocidad máxima
            if (_currentSpeed > _effectiveMaxSpeed * 1.1f) // 10% de margen para colisiones
            {
                Vector3 clamped = horizontalVel.normalized * _effectiveMaxSpeed;
                _rb.linearVelocity = new Vector3(clamped.x, vel.y, clamped.z);
            }
        }

        /// <summary>
        /// Inclinación visual aplicada al TiltPivot (padre del SpinPivot).
        /// Como el TiltPivot NO gira en Y, la inclinación es estable y limpia.
        /// El SpinPivot (hijo) solo gira en Y, heredando la inclinación del padre.
        /// </summary>
        private void ApplyTiltPhysics()
        {
            if (_tiltPivot == null) return;

            Vector3 vel = _rb.linearVelocity;
            Vector3 horizontalVel = new Vector3(vel.x, 0f, vel.z);
            float speed = horizontalVel.magnitude;
            float speedRatio = Mathf.Clamp01(speed / Mathf.Max(_effectiveMaxSpeed, 0.1f));

            float tiltAmount = MAX_TILT_ANGLE * speedRatio;

            Vector3 targetTilt;
            if (speed > 0.3f)
            {
                Vector3 velDir = horizontalVel / speed;
                targetTilt = new Vector3(
                    velDir.z * tiltAmount,
                    0f,
                    -velDir.x * tiltAmount
                );
            }
            else
            {
                targetTilt = Vector3.zero;
            }

            float lerpSpeed = (targetTilt.sqrMagnitude > _currentTilt.sqrMagnitude)
                ? TILT_SMOOTHING * 2f
                : TILT_SMOOTHING * 0.8f;

            _currentTilt = Vector3.Lerp(_currentTilt, targetTilt, Time.fixedDeltaTime * lerpSpeed);

            // Aplicar SOLO tilt al TiltPivot (sin componente Y)
            // El SpinPivot hijo hereda esta inclinación y gira libremente en Y
            _tiltPivot.localRotation = Quaternion.Euler(_currentTilt.x, 0f, _currentTilt.z);
        }
        #endregion

        #region Dash
        public void ExecuteDash()
        {
            if (!_canDash || _isDestroyed || _rb == null) return;

            // Coste reducido: 5% del spin máximo en vez de un valor fijo alto
            float actualCost = Mathf.Max(dashSpinCost, _maxSpinSpeed * 0.05f);
            if (_currentSpinSpeed < actualCost * 1.5f) // No dashear si te dejaría muy bajo
            {
                if (showDebugInfo) Debug.Log("[FakeBlade] Not enough spin for dash!");
                return;
            }

            Vector3 dashDirection;
            if (_inputDirection.sqrMagnitude > 0.01f)
                dashDirection = _inputDirection.normalized; // Prioridad: dirección del input
            else if (_currentSpeed > 1f)
                dashDirection = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).normalized;
            else
                dashDirection = Vector3.forward;

            dashDirection.y = 0f;
            dashDirection.Normalize();

            float weightedDash = dashForce * Mathf.Lerp(0.8f, 1.4f, Mathf.InverseLerp(0.5f, 3f, Weight));
            _rb.AddForce(dashDirection * weightedDash, ForceMode.Impulse);

            ReduceSpin(actualCost);
            _canDash = false;
            _dashTimer = dashCooldown;

            // Activar dash armor
            _isDashing = true;
            _dashArmorTimer = DASH_ARMOR_DURATION;

            // VFX: burst de partículas en la base
            if (vfxDash != null)
            {
                vfxDash.transform.position = _transform.position;
                vfxDash.transform.rotation = Quaternion.LookRotation(dashDirection);
                vfxDash.Emit(20);
            }

            // Camera shake ligero
            CameraShake.Shake(0.08f, 0.06f);

            PlaySound(dashSound);
            OnDashExecuted?.Invoke();

            if (showDebugInfo)
                Debug.Log($"[FakeBlade] Dash! Dir:{dashDirection} Force:{weightedDash:F1} Cost:{actualCost:F1} DashArmor:ON");
        }

        private void UpdateDashCooldown()
        {
            if (!_canDash)
            {
                _dashTimer -= Time.deltaTime;
                if (_dashTimer <= 0f) _canDash = true;
            }

            // Dash armor countdown
            if (_isDashing)
            {
                _dashArmorTimer -= Time.deltaTime;
                if (_dashArmorTimer <= 0f)
                {
                    _isDashing = false;
                }
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

            if (_abilityHandler != null)
            {
                _abilityHandler.ExecuteAbility();
                return;
            }

            // Default: SpinBoost
            if (_rb != null)
                _rb.AddForce(Vector3.up * SPECIAL_VERTICAL_FORCE, ForceMode.Impulse);

            AddSpin(100f);

            // VFX: efecto especial
            if (vfxSpecial != null)
            {
                vfxSpecial.transform.position = _transform.position;
                vfxSpecial.Play();
            }

            CameraShake.Shake(0.1f, 0.08f);

            OnSpecialExecuted?.Invoke(SpecialAbilityType.SpinBoost);

            if (showDebugInfo)
                Debug.Log($"[FakeBlade] Default special! Spin: {_currentSpinSpeed:F0}");
        }
        #endregion

        #region Combat
        private void HandleCollision(Collision collision)
        {
            float relativeSpeed = collision.relativeVelocity.magnitude;

            if (showDebugInfo)
            {
                Debug.Log($"[FB-COLLISION] {gameObject.name} hit {collision.gameObject.name} | " +
                    $"RelSpd:{relativeSpeed:F2} | MyVel:{_rb.linearVelocity} | Dashing:{_isDashing}");
            }

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
            float otherAttackPower = other._stats?.AttackPower ?? 10f;

            float myWeight = Weight;
            float otherWeight = other.Weight;

            // === DETERMINAR QUIÉN ES EL ATACANTE ===
            // El atacante es quien se mueve más hacia el otro (mayor velocidad relativa en esa dirección)
            Vector3 toOther = (other._transform.position - _transform.position).normalized;
            float myApproachSpeed = Vector3.Dot(_rb.linearVelocity, toOther);
            float otherApproachSpeed = Vector3.Dot(other._rb.linearVelocity, -toOther);

            bool iAmAttacker = myApproachSpeed > otherApproachSpeed;

            // Spin ratio: más spin = más daño
            float spinRatio = _currentSpinSpeed / (other._currentSpinSpeed + 0.001f);
            spinRatio = Mathf.Clamp(spinRatio, 0.5f, 2f);

            // === CALCULAR DAÑO BASE ===
            float baseDmg = relativeSpeed * COLLISION_DAMAGE_BASE;

            // Daño que YO hago al otro
            float damageToOther = baseDmg * attackPower * 0.1f * spinRatio * (myWeight / (otherWeight + 0.001f));

            // Daño que el OTRO me hace a mí
            float damageToSelf = baseDmg * otherAttackPower * 0.1f * (1f / spinRatio) * (otherWeight / (myWeight + 0.001f));

            // === VENTAJA DEL ATACANTE ===
            if (iAmAttacker)
            {
                // El atacante recibe mucho menos daño
                damageToSelf *= 0.3f;

                // Bonus de daño al otro por ser atacante
                damageToOther *= 1.3f;
            }

            // === DASH ARMOR ===
            // Si estoy dasheando, recibo menos daño y hago más
            if (_isDashing)
            {
                damageToSelf *= (1f - DASH_ARMOR_REDUCTION); // 80% reducción
                damageToOther *= 1.5f; // 50% más daño al otro
            }

            // Si el otro está dasheando, él recibe menos daño
            if (other._isDashing)
            {
                damageToOther *= (1f - DASH_ARMOR_REDUCTION);
                damageToSelf *= 1.5f;
            }

            // Aplicar daño
            other.ReduceSpin(damageToOther);
            ReduceSpin(damageToSelf);

            // === KNOCKBACK ===
            Vector3 knockbackDir = toOther;
            knockbackDir.y = 0f;
            knockbackDir.Normalize();

            float baseKnockback = knockbackForce;

            // El atacante empuja más
            float knockbackToOther = baseKnockback * (iAmAttacker ? 1.3f : 0.8f) * spinRatio * (myWeight / (otherWeight + 0.001f));
            float knockbackToSelf = baseKnockback * (iAmAttacker ? 0.2f : 0.5f) * (1f / spinRatio) * (otherWeight / (myWeight + 0.001f));

            // Dash = knockback extra al otro
            if (_isDashing) knockbackToOther *= 1.5f;
            if (other._isDashing) knockbackToSelf *= 1.5f;

            // Clamp
            knockbackToOther = Mathf.Min(knockbackToOther, knockbackForce * 3f);
            knockbackToSelf = Mathf.Min(knockbackToSelf, knockbackForce * 2f);

            other._rb?.AddForce(knockbackDir * knockbackToOther, ForceMode.Impulse);
            _rb?.AddForce(-knockbackDir * knockbackToSelf, ForceMode.Impulse);

            float collisionIntensity = Mathf.Clamp01(relativeSpeed / 15f);
            SpawnCollisionEffects(collision.GetContact(0).point, collisionIntensity);
            PlaySound(collisionSound);

            GetComponent<InputHandler>()?.Vibrate(0.3f, 0.5f, 0.15f);

            OnCollisionWithFakeBlade?.Invoke(other, damageToOther);
            other.OnCollisionWithFakeBlade?.Invoke(this, damageToSelf);

            if (showDebugInfo)
            {
                string role = iAmAttacker ? "ATTACKER" : "DEFENDER";
                string dashInfo = _isDashing ? " [DASH]" : "";
                Debug.Log($"[FakeBlade] CLASH! {role}{dashInfo} | Dealt:{damageToOther:F1} Recv:{damageToSelf:F1} | " +
                    $"KBout:{knockbackToOther:F1} KBin:{knockbackToSelf:F1}");
            }
        }

        private void ProcessEnvironmentCollision(Collision collision, float relativeSpeed)
        {
            ReduceSpin(relativeSpeed * 0.5f);

            if (vfxCollision != null && relativeSpeed > 3f)
            {
                vfxCollision.transform.position = collision.GetContact(0).point;
                vfxCollision.Emit(5);
            }
        }

        private void SpawnCollisionEffects(Vector3 point, float intensity = 1f)
        {
            if (vfxCollision != null)
            {
                vfxCollision.transform.position = point;
                vfxCollision.Emit(Mathf.RoundToInt(10 + 15 * intensity));
            }

            // Camera shake proporcional a la intensidad del golpe
            CameraShake.Shake(0.15f * intensity, 0.12f * intensity);
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
            _isDashing = false;
            _dashArmorTimer = 0f;
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
            if (_tiltPivot != null)
                _tiltPivot.localRotation = Quaternion.identity;

            if (visualRoot != null && visualRoot != _transform)
                visualRoot.localRotation = Quaternion.identity;

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