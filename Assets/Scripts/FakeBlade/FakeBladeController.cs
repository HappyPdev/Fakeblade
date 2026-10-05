using System;
using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Controlador principal de la peonza: movimiento, RPM, ataque, dash, especial y choques.
    ///
    /// === JERARQUÍA DEL PREFAB ===
    ///
    /// FakeBlade_Root (este GameObject)   ← Scripts + Rigidbody + Collider. NUNCA rota.
    ///   └── TiltPivot (se crea solo)     ← solo inclinación X/Z
    ///        └── SpinPivot (visualRoot)  ← solo gira en Y
    ///             ├── Body_Mesh
    ///             └── ...
    ///
    /// El ajuste numérico global está en CombatConfig; las diferencias entre peonzas
    /// vienen de FakeBladeStats (piezas equipadas).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(FakeBladeStats))]
    public class FakeBladeController : MonoBehaviour
    {
        #region Constants
        private const float MIN_SPIN_THRESHOLD = 0.1f;
        private const float TILT_SMOOTHING = 5f;
        private const float MAX_TILT_ANGLE = 20f;
        private const float INPUT_DEADZONE_SQR = 0.04f;
        private const float SPIN_OUT_DEACTIVATE_DELAY = 1.5f;
        private const float MAX_UPWARD_VELOCITY = 2f;
        private const float MAX_SANE_SPEED = 200f;
        /// <summary>Superficies con |normal.y| mayor que esto se consideran suelo, no pared.</summary>
        private const float WALL_NORMAL_MAX_Y = 0.6f;
        private const float FALL_OUT_HEIGHT = -30f;
        #endregion

        #region Registry
        private static readonly List<FakeBladeController> s_active = new List<FakeBladeController>(8);

        /// <summary>Peonzas activas en escena (sin asignaciones ni búsquedas).</summary>
        public static IReadOnlyList<FakeBladeController> ActiveBlades => s_active;
        #endregion

        #region Events
        public event Action OnDashExecuted;
        /// <summary>Nivel del ataque lanzado (0 = rápido, 1+ = cargado).</summary>
        public event Action<int> OnAttackLaunched;
        public event Action<SpecialAbilityType> OnSpecialActivated;
        public event Action<SpecialAbilityType> OnSpecialEnded;
        /// <summary>Cambio de estado alterado (None = se acabó).</summary>
        public event Action<StatusEffectType> OnStatusEffectChanged;
        /// <summary>Choque con otra peonza: (otra, RPM perdidas por esta).</summary>
        public event Action<FakeBladeController, float> OnClash;
        public event Action OnSpinOut;
        #endregion

        #region Serialized Fields
        [Header("=== HIERARCHY ===")]
        [Tooltip("El hijo que gira visualmente. Si está vacío se busca/crea automáticamente. " +
                 "NUNCA debe ser el propio root.")]
        [SerializeField] private Transform visualRoot;

        [Header("=== AUDIO ===")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip collisionSound;
        [SerializeField] private AudioClip attackSound;
        [SerializeField] private AudioClip dashSound;
        [SerializeField] private AudioClip specialSound;
        [Tooltip("Sonido del parry. Vacío = usa el de choque")]
        [SerializeField] private AudioClip parrySound;
        [SerializeField] private AudioClip spinOutSound;

        [Header("=== DEBUG ===")]
        [SerializeField] private bool showDebugInfo = false;
        #endregion

        #region Private Fields
        private Rigidbody _rb;
        private FakeBladeStats _stats;
        private Transform _transform;
        private Transform _tiltPivot;
        private PlayerController _owner;

        private readonly AttackSystem _attack = new AttackSystem();
        private readonly SpecialAbilitySystem _special = new SpecialAbilitySystem();
        private readonly StatusEffectSystem _status = new StatusEffectSystem();
        // Estela, chispas, humo, carga, auras y estados: todo con el pool de VfxSystem
        private BladeParticles _particles;

        // RPM
        private float _currentSpin;
        private float _maxSpin;
        private bool _isDestroyed;
        private bool _simulationActive = true;
        private float _invulnerableTimer;

        // Input
        private Vector3 _moveInput;
        private bool _attackHeld;
        private Vector3 _lastFacing = Vector3.forward;

        // Dash
        private float _dashTimer;
        private float _dashCooldownTotal = 1f;
        /// <summary>Fin de la ventana en la que el dash cuenta como ataque (Time.time, exacto en física).</summary>
        private float _dashAttackEndTime = float.NegativeInfinity;
        /// <summary>Fin del aturdimiento tras recibir un parry (sin control ni ataques).</summary>
        private float _staggerEndTime = float.NegativeInfinity;
        /// <summary>RPM gastadas en el dash en curso (se devuelve una fracción si gana un choque).</summary>
        private float _dashRefundableCost;

        // Burst: tope de velocidad temporal tras ataque/dash/empuje
        private float _burstCap;
        private float _burstCapStart;
        private float _burstHold;

        // Choques
        private Vector3 _preImpactVelocity;
        private Vector3 _pendingVelocityChange;
        // Último choque con cada rival (hasta 4 a la vez) para ignorar los rebotes inmediatos
        private const int ClashMemory = 4;
        private readonly FakeBladeController[] _clashPartners = new FakeBladeController[ClashMemory];
        private readonly float[] _clashTimes = new float[ClashMemory];

        // Visual
        private float _spinAngle;
        private Vector3 _currentTilt;
        private float _wobblePhase;

        // Stats derivadas
        private float _effectiveAcceleration;
        private float _effectiveMaxSpeed;
        private float _effectiveTurnSpeed;
        private float _effectiveDrag;
        private float _weightNormalized;
        #endregion

        #region Properties
        public float SpinSpeedPercentage => _maxSpin > 0f ? _currentSpin / _maxSpin : 0f;
        public float CurrentSpinSpeed => _currentSpin;
        public float MaxSpinSpeed => _maxSpin;

        public bool IsDestroyed => _isDestroyed;
        public bool IsInvulnerable => _invulnerableTimer > 0f;
        public bool IsDashAttacking => Time.time < _dashAttackEndTime;
        public bool IsStaggered => Time.time < _staggerEndTime;

        /// <summary>Ventana de parry de esta peonza: base de CombatConfig + lo que sumen/resten las piezas.</summary>
        public float ParryWindow =>
            Mathf.Clamp(CombatConfig.Active.parryWindow + (_stats != null ? _stats.ParryWindowBonus : 0f), 0.02f, 0.4f);

        /// <summary>Está en los primeros instantes de un ataque rápido: un choque ahora contra un ataque enemigo es parry.</summary>
        public bool IsInParryWindow => !_isDestroyed && _attack.IsInParryWindow(ParryWindow);
        /// <summary>Ataque con botón o dash en curso (cuenta como ataque en los choques).</summary>
        public bool IsAttacking => _attack.IsAttacking || IsDashAttacking;

        public bool CanDash => _dashTimer <= 0f && !_isDestroyed && _simulationActive;
        /// <summary>0 = recién usado, 1 = listo.</summary>
        public float DashCooldownProgress => _dashTimer <= 0f ? 1f : 1f - _dashTimer / _dashCooldownTotal;

        public float Weight => _stats != null ? _stats.Weight : 1f;
        /// <summary>Masa usada en los choques: peso + bonus de masa del ataque en curso.</summary>
        public float EffectiveMass => Weight * (1f + _attack.MassBonus);

        /// <summary>Multiplicador de daño infligido: ataque de las piezas y combo.</summary>
        public float OffenseMultiplier
        {
            get
            {
                float attackPower = _stats != null ? _stats.AttackPower : 10f;
                float combo = IsAttacking ? _attack.ComboMultiplier : 1f;
                return attackPower / Mathf.Max(1f, CombatConfig.Active.referenceAttackPower) * combo;
            }
        }

        /// <summary>Multiplicador del empuje que provoca esta peonza.</summary>
        public float OutgoingKnockbackMultiplier =>
            _attack.KnockbackMultiplier * (IsAttacking ? _attack.ComboMultiplier : 1f);

        /// <summary>Fracción del empuje que realmente recibe (defensa y poder Defensa).</summary>
        public float KnockbackResistance
        {
            get
            {
                float defense = _stats != null ? _stats.Defense : 0f;
                return (1f - defense * 0.005f) * _special.KnockbackTakenMultiplier;
            }
        }

        public Vector3 Position => _transform.position;
        /// <summary>Última dirección horizontal hacia la que se movió o atacó.</summary>
        public Vector3 Facing => _lastFacing;
        public Vector3 Velocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
        /// <summary>Velocidad justo antes del último paso de física (para comparar choques).</summary>
        public Vector3 PreImpactVelocity => _preImpactVelocity;

        public AttackSystem Attack => _attack;
        public SpecialAbilitySystem Special => _special;
        /// <summary>Estado alterado actual (solo lectura; para aplicar uno: TryBurn/TryFreeze/TryLaunch).</summary>
        public StatusEffectSystem Status => _status;
        public FakeBladeStats Stats => _stats;
        public PlayerController Owner => _owner;

        /// <summary>Última peonza que le quitó RPM (para dar el punto de K.O.).</summary>
        public FakeBladeController LastDamageSource { get; private set; }
        public float LastDamageTime { get; private set; } = float.NegativeInfinity;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            _transform = transform;
            _rb = GetComponent<Rigidbody>();
            _stats = GetComponent<FakeBladeStats>();
            _owner = GetComponent<PlayerController>();
            _particles = new BladeParticles(this);

            SetupVisualRoot();
        }

        private void OnEnable()
        {
            if (!s_active.Contains(this)) s_active.Add(this);
        }

        private void OnDisable()
        {
            s_active.Remove(this);
        }

        private void Start()
        {
            ApplyStatsFromComponent();
            ResetFakeBlade();

            if (_stats != null)
                _stats.OnStatsChanged += ApplyStatsFromComponent;

            if (showDebugInfo)
                Debug.Log($"[FakeBlade] {name} init | {_stats?.GetStatsSummary()} | " +
                          $"Accel:{_effectiveAcceleration:F1} MaxSpd:{_effectiveMaxSpeed:F1}", this);
        }

        private void OnDestroy()
        {
            if (_stats != null)
                _stats.OnStatsChanged -= ApplyStatsFromComponent;
        }

        private void Update()
        {
            if (_isDestroyed) return;

            float dt = Time.deltaTime;
            if (_simulationActive)
            {
                UpdateSpin(dt);
                if (_isDestroyed) return;
                UpdateCombatTimers(dt);
            }

            UpdateVisuals(dt);

            if (showDebugInfo) DrawDebugInfo();
        }

        private void FixedUpdate()
        {
            if (_isDestroyed) return;

            ApplyMovementPhysics(Time.fixedDeltaTime);

            // Velocidad con la que entra en el siguiente paso de física
            _preImpactVelocity = _rb.linearVelocity + _pendingVelocityChange;
            _pendingVelocityChange = Vector3.zero;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_isDestroyed) return;

            Rigidbody otherRb = collision.rigidbody;
            if (otherRb != null && otherRb.TryGetComponent(out FakeBladeController other))
            {
                if (showDebugInfo || other.showDebugInfo)
                    Debug.Log($"[FakeBlade] ENTER {name}↔{other.name} f{Time.frameCount} t{Time.time:F3} " +
                              $"lower:{GetInstanceID() < other.GetInstanceID()} cd:{IsClashOnCooldown(other)}", this);

                DampContactPush();

                // Se resuelve una sola vez por pareja y se ignoran los rebotes inmediatos
                if (!other._isDestroyed && GetInstanceID() < other.GetInstanceID() && !IsClashOnCooldown(other))
                {
                    MarkClash(other);
                    other.MarkClash(this);
                    CollisionResolver.Resolve(this, other, collision.GetContact(0).point);
                }
                return;
            }

            HandleEnvironmentCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (_isDestroyed || !_simulationActive) return;

            Rigidbody otherRb = collision.rigidbody;
            if (otherRb == null || !otherRb.TryGetComponent(out FakeBladeController other)) return;
            if (other._isDestroyed || (IsAllyOf(other) && !FriendlyFire)) return;

            // Roce continuo entre peonzas
            ApplyDamage(CombatConfig.Active.grindDamagePerSecond * Time.fixedDeltaTime, other);
        }

        /// <summary>
        /// Un poder que reduce el empuje (Defensa) también reduce el que da la propia física al
        /// separar las dos peonzas: se escala el cambio de velocidad horizontal del choque.
        /// </summary>
        private void DampContactPush()
        {
            float multiplier = _special.KnockbackTakenMultiplier;
            if (multiplier >= 1f) return;

            Vector3 velocity = _rb.linearVelocity;
            Vector3 contactChange = velocity - _preImpactVelocity;
            contactChange.y = 0f;
            _rb.linearVelocity = velocity - contactChange * (1f - multiplier);
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Garantiza la jerarquía Root > TiltPivot > SpinPivot(visualRoot).
        /// </summary>
        private void SetupVisualRoot()
        {
            if (visualRoot == null || visualRoot == _transform)
            {
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
                    var pivot = new GameObject("SpinPivot").transform;
                    pivot.SetParent(_transform, false);
                    for (int i = _transform.childCount - 1; i >= 0; i--)
                    {
                        Transform child = _transform.GetChild(i);
                        if (child != pivot) child.SetParent(pivot, true);
                    }
                    visualRoot = pivot;
                }
            }

            _tiltPivot = _transform.Find("TiltPivot");
            if (_tiltPivot == null)
            {
                _tiltPivot = new GameObject("TiltPivot").transform;
                _tiltPivot.SetParent(_transform, false);
            }

            if (visualRoot.parent != _tiltPivot)
            {
                visualRoot.SetParent(_tiltPivot, false);
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;
            }
        }

        private void ApplyStatsFromComponent()
        {
            if (_stats == null) return;
            var cfg = CombatConfig.Active;

            float weight = _stats.Weight;
            float moveSpeed = _stats.MoveSpeed;

            float previousMax = _maxSpin;
            _maxSpin = _stats.MaxSpin;
            if (previousMax > 0f)
                _currentSpin = Mathf.Min(_currentSpin, _maxSpin);

            _weightNormalized = Mathf.InverseLerp(0.5f, 3f, weight);

            _effectiveAcceleration = Mathf.Clamp(
                cfg.accelerationForce * Mathf.Max(moveSpeed * 0.1f, 1f) * Mathf.Lerp(1.5f, 0.5f, _weightNormalized),
                5f, 120f);
            _effectiveMaxSpeed = Mathf.Clamp(cfg.maxVelocity + moveSpeed * Mathf.Lerp(0.8f, 0.4f, _weightNormalized), 3f, 25f);
            _effectiveTurnSpeed = cfg.turnResponsiveness * Mathf.Lerp(2.5f, 0.5f, _weightNormalized);
            _effectiveDrag = cfg.stoppingFriction * Mathf.Lerp(1.5f, 0.4f, _weightNormalized);

            _attack.SetMaxCharges(_stats.AttackCharges);
            ConfigureRigidbody();
            _particles?.Reset(); // las piezas pueden cambiar el tamaño de la peonza
        }

        private void ConfigureRigidbody()
        {
            if (_rb == null) return;
            var cfg = CombatConfig.Active;

            _rb.mass = Weight;
            _rb.linearDamping = cfg.linearDamping;
            _rb.angularDamping = cfg.angularDamping;
            _rb.useGravity = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.centerOfMass = new Vector3(0f, -0.15f, 0f);
        }
        #endregion

        #region Input API (PlayerController)
        public void HandleMovement(Vector2 input)
        {
            if (input.sqrMagnitude < INPUT_DEADZONE_SQR)
            {
                _moveInput = Vector3.zero;
                return;
            }

            Vector3 dir = new Vector3(input.x, 0f, input.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            _moveInput = dir;
        }

        public void SetAttackHeld(bool held) => _attackHeld = held;

        public bool ExecuteDash() => TryDash();

        public bool ExecuteSpecial() => TryActivateSpecial();

        /// <summary>
        /// Activa o congela la simulación de juego (desgaste, cargas, input).
        /// La física sigue funcionando. Lo controla el GameManager según el estado.
        /// </summary>
        public void SetSimulationActive(bool active)
        {
            _simulationActive = active;
            if (active)
            {
                // El botón con el que se cerró un menú no debe acabar en un ataque al soltarlo
                _attack.IgnoreHeldUntilReleased();
                return;
            }

            _moveInput = Vector3.zero;
            _attackHeld = false;
            _attack.CancelCharge();
        }
        #endregion

        #region Spin (RPM)
        private void UpdateSpin(float dt)
        {
            float decay = (_stats != null ? _stats.SpinDecay : 2f) * _special.SpinDecayMultiplier;
            _currentSpin = Mathf.Clamp(_currentSpin - decay * dt, 0f, _maxSpin);

            if (_currentSpin <= MIN_SPIN_THRESHOLD)
                HandleSpinOut();
        }

        /// <summary>
        /// Aplica daño (RPM) tras defensa y modificadores. Devuelve las RPM realmente perdidas.
        /// </summary>
        public float ApplyDamage(float amount, FakeBladeController source) => ApplyDamageInternal(amount, source, false);

        /// <summary>
        /// Daño de un estado alterado (quemadura): fijo, no lo reduce la defensa de las piezas,
        /// pero sí un poder defensivo activo y la invulnerabilidad.
        /// </summary>
        public float ApplyStatusDamage(float amount, FakeBladeController source) => ApplyDamageInternal(amount, source, true);

        private float ApplyDamageInternal(float amount, FakeBladeController source, bool ignoreDefense)
        {
            if (_isDestroyed || amount <= 0f || _invulnerableTimer > 0f) return 0f;

            float defense = _stats != null && !ignoreDefense ? _stats.Defense : 0f;
            float damage = amount * (1f - defense * 0.01f) * _special.DamageTakenMultiplier;
            if (damage <= 0f) return 0f;

            _currentSpin = Mathf.Max(0f, _currentSpin - damage);

            if (source != null && source != this)
            {
                LastDamageSource = source;
                LastDamageTime = Time.time;
            }

            if (showDebugInfo)
                Debug.Log($"[FakeBlade] {name} -{damage:F1} RPM (def {defense:F0}%) = {_currentSpin:F0}", this);

            if (_currentSpin <= MIN_SPIN_THRESHOLD)
                HandleSpinOut();

            return damage;
        }

        /// <summary>Compatibilidad: daño sin atacante.</summary>
        public void ReduceSpin(float amount) => ApplyDamage(amount, null);

        #region Status Effects (GDD 5)
        /// <summary>Quemadura (Fuego). Devuelve false si está bloqueada, invulnerable o fuera de combate.</summary>
        public bool TryBurn(FakeBladeController source, float damagePctPerTick, float tickInterval, float duration) =>
            CanReceiveStatus && NotifyStatusApplied(_status.TryBurn(source, damagePctPerTick, tickInterval, duration));

        /// <summary>Congelación (Hielo).</summary>
        public bool TryFreeze(FakeBladeController source, float moveMultiplier, float rechargeMultiplier, float duration) =>
            CanReceiveStatus && NotifyStatusApplied(_status.TryFreeze(source, moveMultiplier, rechargeMultiplier, duration));

        /// <summary>Lanzada (Rayos).</summary>
        public bool TryLaunch(FakeBladeController source, float duration) =>
            CanReceiveStatus && NotifyStatusApplied(_status.TryLaunch(source, duration));

        public void ClearStatus()
        {
            _particles?.HideStatusIcon();
            if (_status.Clear()) OnStatusEffectChanged?.Invoke(StatusEffectType.None);
        }

        /// <summary>Recién reaparecida (invulnerable) no recibe estados.</summary>
        private bool CanReceiveStatus => !_isDestroyed && _invulnerableTimer <= 0f;

        private bool NotifyStatusApplied(bool applied)
        {
            if (applied) OnStatusEffectChanged?.Invoke(_status.Current);
            return applied;
        }

        private void UpdateStatus(float dt)
        {
            if (!_status.HasStatus) return;
            StatusEffectType before = _status.Current;
            _status.Tick(dt, this);
            // Si la quemadura lo ha dejado K.O., HandleSpinOut ya avisó
            if (!_isDestroyed && _status.Current != before)
                OnStatusEffectChanged?.Invoke(_status.Current);
        }
        #endregion

        public void AddSpin(float amount)
        {
            if (_isDestroyed) return;
            _currentSpin = Mathf.Min(_maxSpin, _currentSpin + amount);
        }

        /// <summary>Coste propio de RPM (no le afecta la defensa).</summary>
        private void ConsumeSpin(float cost)
        {
            _currentSpin = Mathf.Max(0f, _currentSpin - cost);
            if (_currentSpin <= MIN_SPIN_THRESHOLD)
                HandleSpinOut();
        }

        private void HandleSpinOut()
        {
            if (_isDestroyed) return;

            _isDestroyed = true;
            _currentSpin = 0f;
            _moveInput = Vector3.zero;
            _attackHeld = false;
            _attack.CancelCharge();
            if (_special.Stop())
                OnSpecialEnded?.Invoke(_special.Type);
            ClearStatus();

            PlaySound(spinOutSound);
            VfxSystem.Play(VfxType.SpinOut, _transform.position, Vector3.up, OwnerColor);
            VfxLibrary vfx = VfxSystem.Library;
            if (vfx != null) VfxSystem.Play(VfxType.LowSpinSmoke, _transform.position, Vector3.up, vfx.smokeColor, 0.8f);
            CameraShake.Shake(0.25f, 0.2f);

            if (_rb != null)
            {
                _rb.constraints = RigidbodyConstraints.None;
                _rb.AddTorque(UnityEngine.Random.insideUnitSphere * 10f, ForceMode.Impulse);
            }

            if (showDebugInfo) Debug.Log($"[FakeBlade] {name} SPIN OUT!", this);

            OnSpinOut?.Invoke();
            Invoke(nameof(DeactivateFakeBlade), SPIN_OUT_DEACTIVATE_DELAY);
        }

        private void DeactivateFakeBlade() => gameObject.SetActive(false);
        #endregion

        #region Combat Timers & Actions
        private void UpdateCombatTimers(float dt)
        {
            if (_dashTimer > 0f) _dashTimer -= dt;
            if (_dashRefundableCost > 0f && !IsDashAttacking)
                _dashRefundableCost = 0f; // la ventana del dash terminó sin acertar
            if (_invulnerableTimer > 0f) _invulnerableTimer -= dt;

            if (_special.Tick(dt))
                OnSpecialEnded?.Invoke(_special.Type);

            UpdateStatus(dt);
            if (_isDestroyed) return;

            // Aturdida tras un parry: el botón de ataque no cuenta. La recarga depende del poder
            // (Defensa, más rápida) y del estado (congelada, más lenta)
            int launch = _attack.Tick(dt, _attackHeld && !IsStaggered, _special.ChargeSpeedMultiplier,
                _special.AttackRechargeMultiplier * _status.AttackRechargeMultiplier);
            if (launch != AttackSystem.NoLaunch)
                LaunchAttack(launch);
        }

        private void LaunchAttack(int level)
        {
            var cfg = CombatConfig.Active;
            float cost = _maxSpin * cfg.quickAttackSpinCostPct * AttackSystem.ChargeCost(level);

            if (_currentSpin <= cost * 1.5f || !_attack.CanAfford(level))
            {
                _attack.CancelCharge();
                return;
            }

            int effectiveLevel = Mathf.Max(level, _special.MinAttackLevel);
            Vector3 direction = ResolveActionDirection();
            float speedGain = cfg.quickAttackImpulse * (1f + cfg.chargedImpulsePerLevel * effectiveLevel);

            _attack.Commit(level, effectiveLevel);
            ApplyBurst(direction, speedGain * WeightSpeedFactor, cfg.quickAttackDuration);
            ConsumeSpin(cost);

            VfxSystem.Play(VfxType.Attack, _transform.position, -direction, OwnerColor, 1f + 0.6f * effectiveLevel);
            CameraShake.Shake(0.04f + 0.03f * effectiveLevel, 0.05f);
            PlaySound(attackSound);
            OnAttackLaunched?.Invoke(effectiveLevel);

            if (showDebugInfo)
                Debug.Log($"[FakeBlade] {name} attack L{effectiveLevel} dir:{direction} cost:{cost:F1}", this);
        }

        private bool TryDash()
        {
            if (!CanDash || IsStaggered) return false;

            var cfg = CombatConfig.Active;
            float cost = _maxSpin * cfg.dashSpinCostPct * _special.DashCostMultiplier;
            if (_currentSpin <= cost * 1.5f) return false;

            Vector3 direction = ResolveActionDirection();
            float speedGain = (_stats != null ? _stats.DashForce : 18f) * _special.DashImpulseMultiplier;

            ApplyBurst(direction, speedGain * WeightSpeedFactor, cfg.dashAttackWindow);
            ConsumeSpin(cost);

            _dashCooldownTotal = Mathf.Max(0.05f, cfg.dashCooldown * _special.DashCooldownMultiplier);
            _dashTimer = _dashCooldownTotal;
            _dashAttackEndTime = Time.time + cfg.dashAttackWindow;
            _dashRefundableCost = cost;

            VfxSystem.Play(VfxType.Dash, _transform.position, -direction, OwnerColor);
            CameraShake.Shake(0.08f, 0.06f);
            PlaySound(dashSound);
            OnDashExecuted?.Invoke();

            if (showDebugInfo)
                Debug.Log($"[FakeBlade] {name} dash dir:{direction} gain:{speedGain:F1} cost:{cost:F1}", this);
            return true;
        }

        private bool TryActivateSpecial()
        {
            if (_isDestroyed || !_simulationActive) return false;
            // Efecto común (RPM y cargas) + efecto propio del poder: SpecialAbilitySystem
            if (!_special.TryActivate()) return false;

            VfxSystem.Play(VfxType.SpecialBurst, _transform.position, Vector3.up, _special.Color, _special.Data.activationBurst);

            CameraShake.Shake(0.1f, 0.08f);
            PlaySound(specialSound);
            OnSpecialActivated?.Invoke(_special.Type);

            if (showDebugInfo) Debug.Log($"[FakeBlade] {name} special {_special.Type}", this);
            return true;
        }

        /// <summary>
        /// Dirección de ataques y dash: input → enemigo más cercano → velocidad actual → última dirección.
        /// </summary>
        private Vector3 ResolveActionDirection()
        {
            if (_moveInput.sqrMagnitude > 0.01f)
                return _moveInput.normalized;

            FakeBladeController enemy = FindNearestEnemy();
            if (enemy != null)
            {
                Vector3 toEnemy = enemy.Position - _transform.position;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude > 0.001f) return toEnemy.normalized;
            }

            Vector3 velocity = _rb.linearVelocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 1f) return velocity.normalized;

            return _lastFacing;
        }

        public FakeBladeController FindNearestEnemy()
        {
            FakeBladeController nearest = null;
            float bestSqr = float.MaxValue;
            Vector3 origin = _transform.position;

            for (int i = 0; i < s_active.Count; i++)
            {
                FakeBladeController other = s_active[i];
                if (other == this || other._isDestroyed || IsAllyOf(other)) continue;

                float sqr = (other.Position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = other;
                }
            }
            return nearest;
        }

        /// <summary>Peonzas ligeras ganan algo más de velocidad por impulso.</summary>
        private float WeightSpeedFactor => Mathf.Lerp(1.15f, 0.85f, _weightNormalized);

        /// <summary>
        /// Acelerón instantáneo en una dirección. Conserva la velocidad que ya llevaba
        /// en esa dirección y amortigua la lateral, y abre un tope de velocidad temporal.
        /// </summary>
        private void ApplyBurst(Vector3 direction, float speedGain, float holdTime)
        {
            Vector3 velocity = _rb.linearVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);

            float along = Vector3.Dot(horizontal, direction);
            Vector3 lateral = horizontal - direction * along;
            Vector3 newHorizontal = direction * (Mathf.Max(0f, along) + speedGain) + lateral * 0.3f;

            _rb.linearVelocity = new Vector3(newHorizontal.x, velocity.y, newHorizontal.z);
            OpenBurst(newHorizontal.magnitude, holdTime);
            _lastFacing = direction;
        }

        private void OpenBurst(float speedCap, float holdTime)
        {
            if (speedCap <= _burstCap && _burstHold >= holdTime) return;

            _burstCap = Mathf.Max(_burstCap, speedCap);
            _burstCapStart = _burstCap;
            _burstHold = Mathf.Max(_burstHold, holdTime);
        }

        /// <summary>Empuje instantáneo (cambio de velocidad en m/s).</summary>
        public void ApplyKnockback(Vector3 velocityChange)
        {
            if (_isDestroyed || _rb == null) return;

            velocityChange.y = 0f;
            _rb.AddForce(velocityChange, ForceMode.VelocityChange);
            _pendingVelocityChange += velocityChange;

            // Permite que el empuje supere la velocidad máxima normal
            Vector3 horizontal = _rb.linearVelocity;
            horizontal.y = 0f;
            OpenBurst((horizontal + velocityChange).magnitude, 0.1f);
        }

        /// <summary>Llamado por CollisionResolver cuando esta peonza inflige daño.</summary>
        public void RegisterSuccessfulHit(float damageDealt)
        {
            if (damageDealt <= 0f) return;
            var cfg = CombatConfig.Active;

            float multiplier = IsAttacking ? 1f : cfg.passiveHitEnergyMultiplier;
            _special.AddEnergy(damageDealt * cfg.specialEnergyPerDamage * multiplier);

            if (IsAttacking) _attack.RegisterHit();
        }

        /// <summary>¿Ya se resolvió un choque con este rival hace menos de clashCooldown? (memoria por rival)</summary>
        private bool IsClashOnCooldown(FakeBladeController other)
        {
            float cooldown = CombatConfig.Active.clashCooldown;
            for (int i = 0; i < ClashMemory; i++)
                if (_clashPartners[i] == other) return Time.time - _clashTimes[i] < cooldown;
            return false;
        }

        private void MarkClash(FakeBladeController other)
        {
            // Reutiliza la entrada de ese rival o, si no la hay, la más antigua
            int slot = 0;
            for (int i = 0; i < ClashMemory; i++)
            {
                if (_clashPartners[i] == other)
                {
                    slot = i;
                    break;
                }
                if (_clashTimes[i] < _clashTimes[slot]) slot = i;
            }
            _clashPartners[slot] = other;
            _clashTimes[slot] = Time.time;
        }

        /// <summary>
        /// Llamado por CollisionResolver cuando esta peonza gana un choque contra un enemigo.
        /// Si iba en dash, recupera una fracción de las RPM que le costó (una vez por dash).
        /// </summary>
        public void RegisterClashWon()
        {
            if (!IsDashAttacking || _dashRefundableCost <= 0f) return;

            float refund = _dashRefundableCost * CombatConfig.Active.dashHitRefundFraction;
            _dashRefundableCost = 0f;
            if (refund <= 0f) return;

            AddSpin(refund);
            VfxSystem.Play(VfxType.Respawn, _transform.position, Vector3.up, OwnerColor, 0.4f);

            if (showDebugInfo)
                Debug.Log($"[FakeBlade] {name} dash acertado: +{refund:F1} RPM", this);
        }

        /// <summary>
        /// Rebote del parry: fija la velocidad horizontal (no la suma, para que un atacante que venía
        /// en dash salga despedido de verdad) y, si se indica, corta su ataque/dash en curso.
        /// </summary>
        public void ParryRebound(Vector3 horizontalVelocity, bool cancelAttack, float staggerTime = 0f)
        {
            if (_isDestroyed || _rb == null) return;

            if (cancelAttack)
            {
                _attack.EndAttack();
                _attack.CancelCharge();
                _dashAttackEndTime = float.NegativeInfinity;
                _dashRefundableCost = 0f;
            }

            if (staggerTime > 0f)
                _staggerEndTime = Mathf.Max(_staggerEndTime, Time.time + staggerTime);

            horizontalVelocity.y = 0f;
            _rb.linearVelocity = new Vector3(horizontalVelocity.x, _rb.linearVelocity.y, horizontalVelocity.z);
            _pendingVelocityChange = Vector3.zero;
            _burstCap = 0f;
            _burstHold = 0f;
            OpenBurst(horizontalVelocity.magnitude, 0.1f);
        }

        /// <summary>Recompensas de un parry con éxito: devuelve la carga y da energía de especial.</summary>
        public void RegisterParry()
        {
            _attack.RefundCharge();
            _special.AddEnergy(CombatConfig.Active.parryEnergy);

            if (showDebugInfo) Debug.Log($"[FakeBlade] {name} PARRY", this);
        }

        public void PlayParryFeedback(Vector3 point)
        {
            VfxSystem.Play(VfxType.Parry, point, Vector3.up, VfxSystem.ParryColor);
            CameraShake.Shake(0.12f, 0.1f);
            PlaySound(parrySound != null ? parrySound : collisionSound);
            HitStop.Trigger(CombatConfig.Active.parryHitStop);
        }

        public void NotifyClash(FakeBladeController other, float damageTaken)
        {
            OnClash?.Invoke(other, damageTaken);
        }

        public void PlayClashFeedback(Vector3 point, float intensity)
        {
            VfxSystem.Play(VfxType.Clash, point, Vector3.up, ClashColor, 0.4f + intensity);
            CameraShake.Shake(0.15f * intensity, 0.12f * intensity);
            PlaySound(collisionSound);
        }

        private void HandleEnvironmentCollision(Collision collision)
        {
            if (!_simulationActive) return;

            var cfg = CombatConfig.Active;
            ContactPoint contact = collision.GetContact(0);

            // El suelo (normal hacia arriba) nunca hace daño: volver a tocarlo tras un pequeño
            // salto no es un golpe. En paredes solo cuenta la velocidad perpendicular (rozar no duele).
            if (Mathf.Abs(contact.normal.y) > WALL_NORMAL_MAX_Y) return;

            float impactSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (impactSpeed < cfg.wallMinDamageSpeed) return;

            if (_status.IsLaunched)
            {
                // Lanzada (Rayos, GDD 5): la pared la golpea como una peonza parada con la fuerza de
                // quien la lanzó (daño base + diferencia de velocidad), y el daño cuenta para él
                FakeBladeController thrower = _status.Source;
                float offense = thrower != null ? thrower.OffenseMultiplier : 1f;
                ApplyDamage(impactSpeed * (cfg.damagePerImpactSpeed + cfg.damagePerSpeedDiff) * offense, thrower);
                PlayClashFeedback(contact.point, Mathf.Clamp01(impactSpeed / 15f));
                return;
            }

            ApplyDamage(impactSpeed * cfg.wallDamagePerSpeed, null);
            VfxSystem.Play(VfxType.WallHit, contact.point, contact.normal, Color.white, Mathf.Clamp01(impactSpeed / 15f) + 0.3f);
        }

        public bool IsAllyOf(FakeBladeController other)
        {
            if (other == null || other == this || _owner == null || other._owner == null) return false;

            var gm = GameManager.Instance;
            return gm != null && gm.Rules.teams && _owner.TeamID == other._owner.TeamID;
        }

        /// <summary>¿Puede quitarle RPM a esa peonza? (no es aliada, o el fuego amigo está activo)</summary>
        public bool CanHarm(FakeBladeController other) => other != null && other != this && (!IsAllyOf(other) || FriendlyFire);

        private static bool FriendlyFire
        {
            get
            {
                var gm = GameManager.Instance;
                return gm == null || gm.Rules.friendlyFire;
            }
        }
        #endregion

        #region Movement
        /// <summary>
        /// Fuerza directa hacia el input + frenado lateral (giro).
        /// Las peonzas pesadas derrapan más; las ligeras cambian de dirección antes.
        /// Tras un ataque, dash o empuje se permite superar la velocidad máxima
        /// y se vuelve a ella de forma progresiva.
        /// </summary>
        private void ApplyMovementPhysics(float dt)
        {
            var cfg = CombatConfig.Active;

            Vector3 velocity = _rb.linearVelocity;
            if (velocity.y > MAX_UPWARD_VELOCITY)
            {
                velocity.y = MAX_UPWARD_VELOCITY;
                _rb.linearVelocity = velocity;
            }

            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontal.magnitude;

            // Red de seguridad ante fallos de física (no debería ocurrir)
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed > MAX_SANE_SPEED)
            {
                _rb.linearVelocity = Vector3.zero;
                return;
            }
            if (_transform.position.y < FALL_OUT_HEIGHT)
            {
                HandleSpinOut();
                return;
            }

            float maxSpeed = _effectiveMaxSpeed * _special.MoveSpeedMultiplier * _status.MoveSpeedMultiplier;
            if (_attack.IsCharging) maxSpeed *= cfg.moveMultiplierWhileCharging;

            // Recuperación del tope de velocidad tras un burst
            if (_burstHold > 0f)
                _burstHold -= dt;
            else if (_burstCap > 0f)
                _burstCap = Mathf.MoveTowards(_burstCap, 0f, _burstCapStart / Mathf.Max(0.05f, cfg.burstRecoverTime) * dt);

            bool bursting = _burstHold > 0f;

            bool staggered = IsStaggered; // tras un parry: sin control y sin frenar el rebote
            if (_simulationActive && !staggered && _moveInput.sqrMagnitude > 0.01f)
            {
                Vector3 inputDir = _moveInput.normalized;
                float inputMag = Mathf.Min(1f, _moveInput.magnitude);

                // 1. Aceleración hacia el input (se reduce al acercarse al tope en esa dirección)
                float speedInInputDir = Vector3.Dot(horizontal, inputDir);
                float targetSpeed = maxSpeed * inputMag;
                float accel = _effectiveAcceleration;
                if (speedInInputDir > 0f && targetSpeed > 0f)
                    accel *= Mathf.Clamp01(1f - speedInInputDir / targetSpeed);

                _rb.AddForce(inputDir * accel, ForceMode.Force);

                // 2. Frenado lateral (permite girar). Más suave durante un burst.
                //    Se aplica como cambio de velocidad acotado (fracción ≤ 1 por paso): un frenado
                //    proporcional aplicado como fuerza diverge con peonzas ligeras (k·dt/m > 2).
                Vector3 lateral = horizontal - inputDir * speedInInputDir;
                if (lateral.sqrMagnitude > 0.01f)
                {
                    float brakeRate = Mathf.Clamp(_effectiveTurnSpeed * 120f, 5f, 50f);
                    if (bursting) brakeRate *= 0.5f;
                    _rb.AddForce(-lateral * Mathf.Min(1f, brakeRate * dt), ForceMode.VelocityChange);
                }

                // 3. Frenado extra si va en dirección contraria al input
                if (speedInInputDir < -0.5f && speed > 0.01f)
                    _rb.AddForce(-horizontal / speed * (_effectiveAcceleration * 0.5f), ForceMode.Force);
            }
            else if (!bursting && !staggered)
            {
                // Sin input: frenado progresivo (acotado igual que el lateral para que sea estable)
                if (speed > 0.1f)
                {
                    float brakeRate = _effectiveDrag / Mathf.Max(0.1f, _rb.mass);
                    _rb.AddForce(-horizontal * Mathf.Min(1f, brakeRate * dt), ForceMode.VelocityChange);
                }
                else if (speed > 0f)
                    _rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
            }

            // Tope de velocidad (10% de margen para colisiones)
            float cap = Mathf.Max(maxSpeed * 1.1f, _burstCap);
            if (speed > cap)
            {
                Vector3 clamped = horizontal / speed * cap;
                _rb.linearVelocity = new Vector3(clamped.x, _rb.linearVelocity.y, clamped.z);
            }
        }
        #endregion

        #region Visuals
        private void UpdateVisuals(float dt)
        {
            // Giro: solo el SpinPivot en Y
            // Curva no lineal: gira casi a tope hasta estar muy cerca de 0 RPM (CombatConfig.visualSpinKnee)
            var cfg = CombatConfig.Active;
            float visualSpeed = _maxSpin * cfg.visualSpinDegreesPerRpm * cfg.EvaluateVisualSpin(SpinSpeedPercentage);
            _spinAngle = (_spinAngle + visualSpeed * dt) % 360f;
            visualRoot.localRotation = Quaternion.Euler(0f, _spinAngle, 0f);

            // Inclinación: solo el TiltPivot, según la velocidad horizontal
            Vector3 velocity = _rb.linearVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontal.magnitude;

            Vector3 targetTilt = Vector3.zero;
            if (speed > 0.3f)
            {
                float tilt = MAX_TILT_ANGLE * Mathf.Clamp01(speed / Mathf.Max(_effectiveMaxSpeed, 0.1f));
                Vector3 dir = horizontal / speed;
                targetTilt = new Vector3(dir.z * tilt, 0f, -dir.x * tilt);
            }

            float lerpSpeed = targetTilt.sqrMagnitude > _currentTilt.sqrMagnitude
                ? TILT_SMOOTHING * 2f
                : TILT_SMOOTHING * 0.8f;
            _currentTilt = Vector3.Lerp(_currentTilt, targetTilt, dt * lerpSpeed);

            // Bamboleo con RPM bajas (GDD 2.1): la inclinación da vueltas (precesión) y crece hacia 0 RPM
            Vector3 wobble = Vector3.zero;
            float low = cfg.LowSpinFactor(SpinSpeedPercentage);
            if (low > 0f && cfg.lowSpinWobbleAngle > 0f)
            {
                float turnsPerSecond = Mathf.Lerp(cfg.lowSpinWobbleFrequency.x, cfg.lowSpinWobbleFrequency.y, low);
                _wobblePhase = Mathf.Repeat(_wobblePhase + dt * turnsPerSecond * Mathf.PI * 2f, Mathf.PI * 2f);
                float amplitude = cfg.lowSpinWobbleAngle * low;
                wobble = new Vector3(Mathf.Cos(_wobblePhase) * amplitude, 0f, Mathf.Sin(_wobblePhase) * amplitude);
            }
            _tiltPivot.localRotation = Quaternion.Euler(_currentTilt.x + wobble.x, 0f, _currentTilt.z + wobble.z);

            // null solo tras recompilar en Play (no se serializa)
            _particles?.Tick(dt);
        }

        /// <summary>Color del jugador (efectos y HUD).</summary>
        public Color OwnerColor => _owner != null ? _owner.PlayerColor : Color.white;

        private static readonly Color ClashColor = new Color(1f, 0.92f, 0.55f);

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip, SettingsService.SfxVolume);
        }
        #endregion

        #region Reset / Respawn
        /// <summary>
        /// Deja la peonza como al inicio de la partida (RPM llenas, cargas llenas, energía 0).
        /// </summary>
        public void ResetFakeBlade()
        {
            CancelInvoke(nameof(DeactivateFakeBlade));
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            _currentSpin = _maxSpin;
            _isDestroyed = false;
            _invulnerableTimer = 0f;
            _dashTimer = 0f;
            _dashAttackEndTime = float.NegativeInfinity;
            _staggerEndTime = float.NegativeInfinity;
            _dashRefundableCost = 0f;
            _burstCap = 0f;
            _burstHold = 0f;
            _moveInput = Vector3.zero;
            _attackHeld = false;
            _currentTilt = Vector3.zero;
            _preImpactVelocity = Vector3.zero;
            _pendingVelocityChange = Vector3.zero;
            LastDamageSource = null;
            LastDamageTime = float.NegativeInfinity;

            _attack.Reset(_stats != null ? _stats.AttackCharges : 3);
            _special.Reset(this, _stats != null ? _stats.SpecialAbility : SpecialAbilityType.SpinBoost);
            ClearStatus();
            _particles?.Reset();
            _wobblePhase = 0f;

            if (_rb != null)
            {
                _rb.constraints = RigidbodyConstraints.FreezeRotation;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            _transform.rotation = Quaternion.identity;
            if (_tiltPivot != null) _tiltPivot.localRotation = Quaternion.identity;
            if (visualRoot != null) visualRoot.localRotation = Quaternion.identity;
        }

        public void SetPosition(Vector3 position, Quaternion rotation)
        {
            // El root nunca rota: la rotación del spawn se ignora a propósito
            _transform.SetPositionAndRotation(position, Quaternion.identity);
            if (_rb == null) return;

            _rb.position = position;
            _rb.rotation = Quaternion.identity;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        public void SetInvulnerable(float seconds)
        {
            _invulnerableTimer = Mathf.Max(_invulnerableTimer, seconds);
        }
        #endregion

        #region Debug
        private void DrawDebugInfo()
        {
            Debug.DrawRay(_transform.position, _rb.linearVelocity, Color.green);
            Debug.DrawRay(_transform.position, _moveInput * 2f, Color.red);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Gizmos.color = Color.Lerp(Color.red, Color.green, SpinSpeedPercentage);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 0.3f);

            if (_special.Data is ShockWaveData shockWave)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, shockWave.radius);
            }
        }
        #endregion
    }
}
