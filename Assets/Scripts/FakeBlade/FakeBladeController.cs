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
        /// <summary>Choque en el que esta ha quitado RPM a otra: (otra, RPM que ha perdido la otra).</summary>
        public event Action<FakeBladeController, float> OnClashDealt;
        /// <summary>Golpe contra una pared que hace daño (0-1 según la velocidad).</summary>
        public event Action<float> OnWallHit;
        /// <summary>Un golpe de ataque enemigo ha cortado la curación del poder.</summary>
        public event Action OnHealCut;
        /// <summary>Un poder pide una vibración del mando de su dueño: (motor grave, motor agudo, segundos).</summary>
        public event Action<float, float, float> OnRumbleRequested;
        /// <summary>Ha perdido RPM por daño (cantidad real, quién lo ha causado o null). Golpes, paredes y quemadura.</summary>
        public event Action<float, FakeBladeController> OnDamaged;
        /// <summary>Ha recuperado RPM (cantidad real): curación de los poderes. Lo usa la telemetría.</summary>
        public event Action<float> OnHealed;
        public event Action OnSpinOut;
        #endregion

        #region Serialized Fields
        [Header("=== HIERARCHY ===")]
        [Tooltip("El hijo que gira visualmente. Si está vacío se busca/crea automáticamente. " +
                 "NUNCA debe ser el propio root.")]
        [SerializeField] private Transform visualRoot;

        [Tooltip("Cómo se monta el modelo con los modelos de las piezas equipadas (BladeModel)")]
        [SerializeField] private BladeModelSettings modelSettings = BladeModelSettings.Default;

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
        // Rastro tipo cinta pegado a la punta (K7)
        private BladeTrail _ribbonTrail;

        // RPM
        private float _currentSpin;
        private float _maxSpin;
        private bool _isDestroyed;
        private bool _simulationActive = true;
        private float _invulnerableTimer;
        /// <summary>Último golpe de pared contado (Time.time) y su velocidad: uno por wallHitCooldown.</summary>
        private float _wallHitTime = float.NegativeInfinity;
        private float _wallHitSpeed;

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
        /// <summary>Tras un empuje fuerte o una lanzada: hasta aquí el stick no mueve la peonza; después vuelve poco a poco.</summary>
        private float _controlLossEnd = float.NegativeInfinity;
        // Atrapada por un Tornado mientras atrae: no puede atacar, cargar ni hacer dash (lo renueva el Tornado cada frame)
        private float _trappedEnd = float.NegativeInfinity;
        private float _controlRecoverEnd = float.NegativeInfinity;
        /// <summary>RPM gastadas en el dash en curso (se devuelve una fracción si gana un choque).</summary>
        private float _dashRefundableCost;
        /// <summary>Ataque cargado en curso: velocidad que tendría uno rápido / velocidad real al lanzarlo.</summary>
        private float _chargeDamageSpeedScale = 1f;

        // Burst: tope de velocidad temporal tras ataque/dash/empuje
        private float _burstCap;
        private float _burstCapStart;
        private float _burstHold;
        /// <summary>Mientras dura el acelerón de un ataque o dash (se nota el impulso aunque ya vaya rápida): una segunda
        /// cinta más ancha, sumada a la normal, y los cuencos de la punta (K7).</summary>
        private BladeTrail _burstRibbon;
        private BladeBurstTip _burstTip;
        // Punta del especial activo (cuencos, sprite en el suelo y partículas en cono; K8)
        private BladeBurstTip _specialTip;
        // Estela en espiral del especial (Salto mientras vuela; K8)
        private BladeSpiralTrail _spiralTrail;
        // Punta de la peonza quemada (aro de fuego en el suelo; K8)
        private BladeBurstTip _burnTip;
        // Partículas del especial y de la quemadura, y manchas en el suelo (K8)
        private BladeSpecialFx _specialFx;
        private BladeArcs _arcs;
        // Cúpula de la carga (Onda de choque), la de la base (Embestida) y aviso de usos de los poderes con varios usos (K8)
        private BladeChargeDome _dome;
        private BladeChargeDome _baseDome;
        // Escudos del Escudo: panal de hexágonos que se hace añicos (K8, 2026-10-29)
        private BladeShieldHex _shieldHex;
        private BladeUsesIndicator _usesIndicator;
        // Borde brillante y destello sobre la peonza: carga, golpe recibido, parry y bloqueo
        private BladeOverlay _overlay;

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
        private float _effectiveTurnRate;
        private float _effectiveStoppingRate;
        /// <summary>Masa física de sus piezas (sin el multiplicador del poder activo).</summary>
        private float _baseMass = 1f;
        /// <summary>Hasta cuándo está en el aire por un Salto.</summary>
        private float _leapUntil = float.NegativeInfinity;
        /// <summary>Gravedad de más mientras salta (×gravedad; 1 = la normal).</summary>
        private float _leapGravityScale = 1f;
        /// <summary>El poder activo bloqueaba las acciones el frame anterior.</summary>
        private bool _actionsBlocked;
        private float _impulseFactor = 1f;
        #endregion

        #region Properties
        public float SpinSpeedPercentage => _maxSpin > 0f ? _currentSpin / _maxSpin : 0f;
        public float CurrentSpinSpeed => _currentSpin;
        public float MaxSpinSpeed => _maxSpin;
        /// <summary>Velocidad máxima de movimiento con las piezas montadas (sin poderes ni estados).</summary>
        public float MaxMoveSpeed => _effectiveMaxSpeed;

        public bool IsDestroyed => _isDestroyed;
        public bool IsInvulnerable => _invulnerableTimer > 0f;
        public bool IsDashAttacking => Time.time < _dashAttackEndTime;
        public bool IsStaggered => Time.time < _staggerEndTime;
        /// <summary>Atrapada por un Tornado que atrae (2026-10-19): no puede atacar, cargar ni hacer dash.</summary>
        public bool IsTrapped => Time.time < _trappedEnd;

        /// <summary>La atrapa un Tornado estos segundos (los renueva mientras sigue atrayéndola).</summary>
        public void Trap(float seconds) => _trappedEnd = Mathf.Max(_trappedEnd, Time.time + seconds);

        /// <summary>Ventana de parry de esta peonza: base de CombatConfig + lo que sumen/resten las piezas.</summary>
        public float ParryWindow =>
            Mathf.Clamp((CombatConfig.Active.parryWindow + (_stats != null ? _stats.ParryWindowBonus : 0f)) * Trait(PartTraitType.ParryWindow),
                0.02f, 0.4f);

        /// <summary>Está en los primeros instantes de un ataque rápido: un choque ahora contra un ataque enemigo es parry.</summary>
        public bool IsInParryWindow => !_isDestroyed && _attack.IsInParryWindow(ParryWindow);
        /// <summary>Ataque con botón o dash en curso (cuenta como ataque en los choques).</summary>
        public bool IsAttacking => _attack.IsAttacking || IsDashAttacking;
        /// <summary>En el acelerón de un ataque o dash (cinta ancha y cuencos en la punta, K7).</summary>
        public bool IsBursting => !_isDestroyed && _burstHold > 0f && IsAttacking;

        /// <summary>Nivel de carga del ataque en curso (0 = rápido, dash o sin atacar).</summary>
        public int ChargeLevel => _attack.IsAttacking ? _attack.AttackLevel : 0;

        /// <summary>
        /// Velocidad con la que cuenta el daño de un choque: sin la parte que puso la carga en el acelerón
        /// (el cargado da alcance y empuje; su daño extra sale de ChargeDamageMultiplier). Se quita en
        /// proporción (la que tendría un ataque rápido lanzado igual), no una cantidad fija: restar la
        /// velocidad extra entera dejaba el daño a 0 si el golpe llegaba tarde o frenado, y lo disparaba
        /// a toda velocidad.
        /// </summary>
        public float DamageSpeed(float approachSpeed) => ChargeLevel > 0 ? approachSpeed * _chargeDamageSpeedScale : approachSpeed;

        /// <summary>Daño del ataque cargado sobre el de uno rápido: 1 + chargedDamagePerLevel × nivel.</summary>
        public float ChargeDamageMultiplier => BladeFormulas.ChargeDamageMultiplier(CombatConfig.Active, ChargeLevel);

        public bool CanDash => _dashTimer <= 0f && !_isDestroyed && _simulationActive;
        /// <summary>0 = recién usado, 1 = listo.</summary>
        public float DashCooldownProgress => _dashTimer <= 0f ? 1f : 1f - _dashTimer / _dashCooldownTotal;

        public float Weight => _stats != null ? _stats.Weight : 1f;
        /// <summary>Masa usada en el empuje de los choques: peso + bonus de masa del ataque en curso.
        /// La masa no cuenta en el daño.</summary>
        public float EffectiveMass => Weight * (1f + _attack.MassBonus);

        /// <summary>Multiplicador de daño infligido: ataque de las piezas, combo y poder activo (Pinchos).</summary>
        public float OffenseMultiplier
        {
            get
            {
                float attackPower = _stats != null ? _stats.AttackPower : 10f;
                float combo = IsAttacking ? _attack.ComboMultiplier : 1f;
                // Las diferencias de ataque de las piezas cuentan solo en parte (attackSpread)
                var cfg = CombatConfig.Active;
                float attack = BladeFormulas.AttackMultiplier(cfg, attackPower);
                return attack * combo * _special.DamageDealtMultiplier * _ghostDamageMultiplier;
            }
        }

        /// <summary>
        /// Daño según el tipo de golpe: con ataque (rápido o cargado) un poco más que sin atacar; con
        /// dash, menos (ya llega mucho más rápido, y la velocidad es la base del daño).
        /// </summary>
        public float HitTypeDamageMultiplier
        {
            get
            {
                var cfg = CombatConfig.Active;
                // Rasgos de daño por tipo de golpe (H8)
                if (_attack.IsAttacking)
                    return cfg.attackHitDamageMultiplier *
                           Trait(_attack.AttackLevel > 0 ? PartTraitType.ChargedHitDamage : PartTraitType.QuickHitDamage);
                return IsDashAttacking ? cfg.dashHitDamageMultiplier * Trait(PartTraitType.DashHitDamage) : 1f;
            }
        }

        /// <summary>Multiplicador del empuje que provoca esta peonza.</summary>
        public float OutgoingKnockbackMultiplier =>
            _attack.KnockbackMultiplier * (IsAttacking ? _attack.ComboMultiplier : 1f) * Trait(PartTraitType.KnockbackDealt);

        /// <summary>Fracción del empuje que realmente recibe (defensa y poder Defensa).</summary>
        public float KnockbackResistance
        {
            get
            {
                float defense = _stats != null ? _stats.Defense : 0f;
                return BladeFormulas.KnockbackTakenFactor(defense) * _special.KnockbackTakenMultiplier * Trait(PartTraitType.KnockbackTaken);
            }
        }

        public Vector3 Position => _transform.position;
        /// <summary>Estela de un golpe de un especial (VFX, 2026-10-19): la deja esta peonza al salir despedida.</summary>
        public void StartSpecialHitTrail(SpecialVfxStyle style, Color power) => _specialFx?.StartHitTrail(style, power);
        /// <summary>Golpe en la cúpula del especial (Rebote, 2026-10-21): una Zbam que se abre desde point.</summary>
        public void PulseChargeDome(Vector3 point) => _dome?.Pulse(point);
        /// <summary>
        /// Choque con las cúpulas del especial puestas (Pinchos, 2026-10-28): las que tienen Spring Enabled se aplastan hacia el
        /// lado contrario a point y vuelven rebotando. speed: la de las dos peonzas, una hacia la otra (m/s).
        /// </summary>
        public void KickChargeDome(Vector3 point, float speed)
        {
            _dome?.Kick(point, speed);
            _baseDome?.Kick(point, speed);
        }

        /// <summary>Punta a ras de suelo (de donde salen los efectos de los especiales).</summary>
        public Vector3 TipPosition
        {
            get
            {
                Vector3 p = _transform.position;
                if (_particles != null) p.y += _particles.TipOffset;
                return p;
            }
        }
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
        /// <summary>Giro de cada pieza a su velocidad (O1); lo asigna el propio componente al montarse el modelo.</summary>
        internal BladePartSpin PartSpin { get; set; }

        /// <summary>Arcos eléctricos del especial (Rayos; VfxSystem los lanza al chocar).</summary>
        public BladeArcs Arcs => _arcs;

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
            _ribbonTrail = new BladeTrail(this);
            _burstRibbon = new BladeTrail(this, burst: true);
            _burstTip = new BladeBurstTip(this);
            _specialTip = new BladeBurstTip(this, "SpecialTip");
            _spiralTrail = new BladeSpiralTrail(this);
            _burnTip = new BladeBurstTip(this, "BurnTip");
            _specialFx = new BladeSpecialFx(this);
            _arcs = new BladeArcs(this);
            _dome = new BladeChargeDome(this);
            _baseDome = new BladeChargeDome(this, "ChargeDomeBase", 1);
            _shieldHex = new BladeShieldHex(this);
            _usesIndicator = new BladeUsesIndicator(this);
            _overlay = new BladeOverlay(this);

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
            // Su clon de Fantasma se va con ella
            if (GhostClone != null) Destroy(GhostClone.gameObject);
            _ribbonTrail?.Dispose();
            _burstRibbon?.Dispose();
            _overlay?.Dispose();
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

            // Salto: la gravedad de más que lo hace caer antes
            if (_leapGravityScale > 1f && IsLeaping)
                _rb.AddForce(Physics.gravity * (_leapGravityScale - 1f), ForceMode.Acceleration);

            // Masa física: la de sus piezas, por la del poder activo (Tornado: inamovible mientras atrae)
            float mass = _baseMass * _special.PhysicalMassMultiplier;
            if (_rb.mass != mass) _rb.mass = mass;

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
            var cfg = CombatConfig.Active;
            ApplyDamage(cfg.grindDamagePerSecond * Time.fixedDeltaTime, other);

            // Pegadas sin choque nuevo (sin velocidad no hay choque): se separan con un empuje y
            // un choque parejo pequeño. Lo resuelve una sola de las dos.
            if (!other._isDestroyed && GetInstanceID() < other.GetInstanceID() &&
                TimeSinceClash(other) >= cfg.stuckRepelDelay)
            {
                MarkClash(other);
                other.MarkClash(this);
                CollisionResolver.ResolveStuck(this, other, collision.GetContact(0).point);
            }
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

            // Una estadística, un efecto (C11/C13, GDD 2.8): la velocidad da la velocidad máxima; el peso, la
            // aceleración y los acelerones (una sola vez, sin dividir entre la masa); el agarre, el giro y el
            // frenado. Fórmulas en BladeFormulas (las mismas que usa el exportador de estadísticas).
            float grip = _stats.Grip;
            _effectiveAcceleration = BladeFormulas.Acceleration(cfg, moveSpeed, weight, Trait(PartTraitType.Acceleration));
            _effectiveMaxSpeed = BladeFormulas.MaxSpeed(cfg, moveSpeed);
            _effectiveTurnRate = BladeFormulas.TurnRate(cfg, grip, Trait(PartTraitType.TurnRate));
            _effectiveStoppingRate = BladeFormulas.StoppingRate(cfg, grip);
            _impulseFactor = BladeFormulas.ImpulseFactor(cfg, weight);

            _attack.SetMaxCharges(_stats.AttackCharges);

            // Núcleo cambiado en caliente (sandbox): el poder pasa a ser el nuevo, con la esfera vacía
            if (_special.Data != null && _special.Type != _stats.SpecialAbility)
            {
                bool wasActive = _special.IsActive;
                SpecialAbilityType previous = _special.Type;
                _special.Reset(this, _stats.SpecialAbility);
                if (wasActive) OnSpecialEnded?.Invoke(previous);
            }

            ConfigureRigidbody();
            _particles?.Reset(); // las piezas pueden cambiar el tamaño de la peonza
        }

        private void ConfigureRigidbody()
        {
            if (_rb == null) return;
            var cfg = CombatConfig.Active;

            _baseMass = BladeFormulas.PhysicalMass(cfg, Weight);
            _rb.mass = _baseMass;
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

        /// <summary>Vibración del mando de esta peonza (si la maneja un jugador con mando), para los momentos fuertes de un poder.</summary>
        public void RequestRumble(float low, float high, float duration) => OnRumbleRequested?.Invoke(low, high, duration);

        /// <summary>Termina el poder activo antes de tiempo (Rayos, al dar su golpe fuerte).</summary>
        public void EndSpecial()
        {
            if (_special.Stop())
                OnSpecialEnded?.Invoke(_special.Type);
        }

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

            // A la vez que pierde RPM por desgaste, el especial se carga solo (GDD 5); con el poder activo, no
            _special.AddEnergy(CombatConfig.Active.specialEnergyPerSecond * Trait(PartTraitType.SpecialEnergyAll) * dt);

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
            if (_isDestroyed || amount <= 0f || _invulnerableTimer > 0f || IsGhost) return 0f;
            // El daño del clon de Fantasma cuenta para su dueña (punto de K.O.)
            if (source != null && source.IsGhost) source = source.GhostOwner;

            float defense = _stats != null && !ignoreDefense ? _stats.Defense : 0f;
            // Multiplicador global (combates más largos); la quemadura va aparte, por porcentaje
            float global = ignoreDefense ? 1f : CombatConfig.Active.damageMultiplier;
            // Rasgo de daño recibido (H8): solo lo que viene de otra peonza (golpes, roce, lanzadas), no
            // las paredes (tienen su rasgo) ni los estados (van sin defensa)
            float traitTaken = !ignoreDefense && source != null && source != this ? Trait(PartTraitType.DamageTaken) : 1f;
            float damage = amount * BladeFormulas.DamageTakenFactor(defense) * _special.DamageTakenMultiplier * global * traitTaken;
            if (damage <= 0f) return 0f;

            _currentSpin = Mathf.Max(0f, _currentSpin - damage);
            OnDamaged?.Invoke(damage, source);

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
            CanReceiveStatus && NotifyStatusApplied(_status.TryBurn(source, damagePctPerTick, tickInterval, StatusDuration(source, duration)));

        /// <summary>Congelación (Hielo).</summary>
        public bool TryFreeze(FakeBladeController source, float moveMultiplier, float rechargeMultiplier, float duration) =>
            CanReceiveStatus && NotifyStatusApplied(_status.TryFreeze(source, moveMultiplier, rechargeMultiplier, StatusDuration(source, duration)));

        /// <summary>Lanzada (Rayos): sin control mientras dura.</summary>
        public bool TryLaunch(FakeBladeController source, float duration)
        {
            if (!CanReceiveStatus) return false;
            float seconds = StatusDuration(source, duration);
            if (!NotifyStatusApplied(_status.TryLaunch(source, seconds))) return false;
            LoseControl(seconds);
            // Sin tope de velocidad mientras vuela (cuenta el empuje del mismo choque, aún sin aplicar)
            Vector3 velocity = _rb.linearVelocity + _pendingVelocityChange;
            OpenBurst(new Vector3(velocity.x, 0f, velocity.z).magnitude, seconds);
            return true;
        }

        /// <summary>
        /// Mojada (Rastro de agua): resbala (agarre × gripMultiplier) durante duration. Va aparte del estado principal;
        /// apaga la quemadura. Devuelve false si está invulnerable o fuera de combate.
        /// </summary>
        public bool TryWet(FakeBladeController source, float gripMultiplier, float duration)
        {
            if (!CanReceiveStatus || _special.ImmuneToWet) return false;
            if (_status.Wet(gripMultiplier, StatusDuration(source, duration)))
                OnStatusEffectChanged?.Invoke(StatusEffectType.None); // ha apagado la quemadura
            return true;
        }

        /// <summary>Duración de un estado con los rasgos de quien lo provoca y de quien lo sufre (H8).</summary>
        private float StatusDuration(FakeBladeController source, float duration) =>
            duration * (source != null ? source.Trait(PartTraitType.StatusDurationDealt) : 1f) * Trait(PartTraitType.StatusDurationTaken);

        /// <summary>Apaga la quemadura, si la tiene (Hielo al activarlo). Los demás estados siguen.</summary>
        public void ClearBurning()
        {
            if (_status.Current == StatusEffectType.Burning && _status.Clear())
                OnStatusEffectChanged?.Invoke(StatusEffectType.None);
        }

        /// <summary>Se seca, si estaba mojada (Hielo al activarlo).</summary>
        public void Dry() => _status.ClearWet();

        public void ClearStatus()
        {
            _particles?.HideStatusIcon();
            _status.ClearWet();
            if (_status.Clear()) OnStatusEffectChanged?.Invoke(StatusEffectType.None);
        }

        /// <summary>Recién reaparecida (invulnerable) no recibe estados.</summary>
        private bool CanReceiveStatus => !_isDestroyed && _invulnerableTimer <= 0f && !IsGhost;

        private bool NotifyStatusApplied(bool applied)
        {
            if (applied) OnStatusEffectChanged?.Invoke(_status.Current);
            return applied;
        }

        private void UpdateStatus(float dt)
        {
            if (!_status.HasStatus && !_status.IsWet) return;
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
            float before = _currentSpin;
            _currentSpin = Mathf.Min(_maxSpin, _currentSpin + amount);
            if (_currentSpin > before) OnHealed?.Invoke(_currentSpin - before);
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
            // Lo que el poder deja puesto (escudos, agua, el clon de Fantasma) se va con ella
            _special.ClearLingering();
            ClearStatus();
            _ribbonTrail?.Stop();
            _burstRibbon?.Stop();
            _burstTip?.Hide();
            _specialTip?.Hide();
            _spiralTrail?.Hide();
            _burnTip?.Hide();
            _specialFx?.Clear();
            _arcs?.Hide();
            _dome?.Hide();
            _baseDome?.Hide();
            _shieldHex?.Hide();
            _usesIndicator?.Hide();
            _overlay?.Clear();

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

            // Un poder que bloquea las acciones (Salto) o atrapada por un Tornado: al empezar y al acabar se corta la carga y el botón
            // no cuenta hasta soltarlo, para que no se escape un ataque al cambiar
            bool blocked = _special.BlocksActions || IsTrapped;
            if (blocked != _actionsBlocked)
            {
                _actionsBlocked = blocked;
                _attack.CancelCharge();
                _attack.IgnoreHeldUntilReleased();
            }

            // Aturdida tras un parry o con las acciones bloqueadas: el botón de ataque no cuenta. La recarga depende
            // del poder (Defensa, más rápida), del estado (congelada, más lenta) y de los rasgos de las piezas
            int launch = _attack.Tick(dt, _attackHeld && !IsStaggered && !blocked,
                _special.ChargeSpeedMultiplier / Trait(PartTraitType.ChargeTime),
                _special.AttackRechargeMultiplier * _status.AttackRechargeMultiplier / Trait(PartTraitType.AttackRechargeTime));
            if (launch != AttackSystem.NoLaunch)
                LaunchAttack(launch);
        }

        private void LaunchAttack(int level)
        {
            var cfg = CombatConfig.Active;
            float cost = _maxSpin * cfg.quickAttackSpinCostPct * AttackSystem.ChargeCost(level) * Trait(PartTraitType.AttackCost);

            if (_currentSpin <= cost * 1.5f || !_attack.CanAfford(level))
            {
                _attack.CancelCharge();
                return;
            }

            int effectiveLevel = Mathf.Max(level, _special.MinAttackLevel);
            Vector3 direction = ResolveActionDirection();
            float speedGain = cfg.quickAttackImpulse * (1f + cfg.chargedImpulsePerLevel * effectiveLevel);

            // Parte de la velocidad que cuenta para el daño (ver DamageSpeed): la de un ataque rápido
            // lanzado desde la misma velocidad, sobre la del cargado
            Vector3 velocity = _rb.linearVelocity;
            float along = Mathf.Max(0f, velocity.x * direction.x + velocity.z * direction.z);
            _chargeDamageSpeedScale = (along + cfg.quickAttackImpulse * WeightSpeedFactor)
                                      / Mathf.Max(0.01f, along + speedGain * WeightSpeedFactor);

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
            if (!CanDash || IsStaggered || _special.BlocksActions || IsTrapped) return false;

            var cfg = CombatConfig.Active;
            float cost = _maxSpin * cfg.dashSpinCostPct * _special.DashCostMultiplier * Trait(PartTraitType.DashCost);
            if (_currentSpin <= cost * 1.5f) return false;

            Vector3 direction = ResolveActionDirection();
            float speedGain = (_stats != null ? _stats.DashForce : 18f) * _special.DashImpulseMultiplier;

            ApplyBurst(direction, speedGain * WeightSpeedFactor, cfg.dashAttackWindow);
            ConsumeSpin(cost);

            _dashCooldownTotal = Mathf.Max(0.05f, cfg.dashCooldown * _special.DashCooldownMultiplier * Trait(PartTraitType.DashCooldown));
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
            if (_isDestroyed || !_simulationActive || IsGhost) return false; // el clon de Fantasma no usa especial
            // Con el poder activo, la pulsación es para él (Onda de choque: siguiente onda)
            if (_special.IsActive) return _special.TryPressActive();
            // Con lo que deja el poder todavía puesto, primero es para él (Fantasma: cambiar de sitio con el clon, 2026-10-28)
            if (_special.TryPressLingering())
            {
                CameraShake.Shake(0.08f, 0.06f);
                return true;
            }
            // Efecto común (RPM y cargas) + efecto propio del poder: SpecialAbilitySystem
            if (!_special.TryActivate()) return false;

            // Explosión de estrellas común; no en los especiales con su propia explosión al encenderse (Fuego, Hielo, Rayos: las
            // estrellas de cuatro puntas no gustan, 2026-10-18)
            VfxLibrary lib = VfxSystem.Library;
            SpecialVfxStyle effects = lib != null ? lib.GetSpecialEffectsOrGeneric(_special.Type) : null;
            if (effects == null || effects.activationBurst == null || effects.activationBurst.Count == 0)
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
        private float WeightSpeedFactor => _impulseFactor;

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

        /// <summary>
        /// Empuje instantáneo (cambio de velocidad en m/s). Un empuje fuerte quita el control un rato
        /// (más cuanto más fuerte): si no, el stick a contrapelo lo cancelaba enseguida (2026-10-11).
        /// </summary>
        public void ApplyKnockback(Vector3 velocityChange)
        {
            if (_isDestroyed || _rb == null) return;

            velocityChange.y = 0f;
            _rb.AddForce(velocityChange, ForceMode.VelocityChange);
            _pendingVelocityChange += velocityChange;

            var cfg = CombatConfig.Active;
            float push = velocityChange.magnitude;
            float controlLoss = push >= cfg.knockbackControlMinSpeed
                ? cfg.knockbackControlLossBase + cfg.knockbackControlLossPerSpeed * push
                : 0f;
            if (controlLoss > 0f) LoseControl(controlLoss);

            // Permite que el empuje supere la velocidad máxima normal mientras dura
            Vector3 horizontal = _rb.linearVelocity;
            horizontal.y = 0f;
            OpenBurst((horizontal + velocityChange).magnitude, Mathf.Max(0.1f, controlLoss));
        }

        /// <summary>
        /// Salto (poder Salto): sale con esta velocidad horizontal y hacia arriba. Mientras vuela (airTime) el stick no
        /// la mueve ni la frena, puede pasar de su velocidad máxima y del tope de subida, y se cancela la carga de ataque.
        /// gravityScale: gravedad en el aire (×), para un salto más rápido con la misma altura.
        /// </summary>
        public void Leap(Vector3 horizontalVelocity, float upSpeed, float airTime, float gravityScale = 1f)
        {
            if (_isDestroyed || _rb == null) return;
            horizontalVelocity.y = 0f;
            _leapUntil = Time.time + airTime;
            _leapGravityScale = gravityScale;
            _attack.CancelCharge();
            LoseControl(airTime);
            OpenBurst(horizontalVelocity.magnitude, airTime);
            _rb.linearVelocity = horizontalVelocity + Vector3.up * upSpeed;
        }

        /// <summary>En el aire por un Salto.</summary>
        public bool IsLeaping => Time.time < _leapUntil;

        /// <summary>
        /// Arrancada de un poder (Embestida): sale con esta velocidad; mientras dura el stick no la mueve ni la frena,
        /// puede pasar de su velocidad máxima y se cancela la carga de ataque.
        /// </summary>
        public void Lunge(Vector3 velocity, float duration)
        {
            if (_isDestroyed || _rb == null) return;
            velocity.y = 0f;
            _attack.CancelCharge();
            LoseControl(duration);
            OpenBurst(velocity.magnitude, duration);
            _rb.linearVelocity = new Vector3(velocity.x, _rb.linearVelocity.y, velocity.z);
        }

        /// <summary>Dirección de ataques, dash y Salto: stick → enemigo más cercano → velocidad actual → última dirección.</summary>
        public Vector3 ActionDirection => ResolveActionDirection();

        /// <summary>
        /// Atracción continua (Tornado): cambia la velocidad poco a poco, sin quitar el control ni abrir el tope de
        /// velocidad como un empuje. Moviéndose en contra se puede resistir.
        /// </summary>
        public void ApplyPull(Vector3 velocityChange)
        {
            if (_isDestroyed || _rb == null) return;
            velocityChange.y = 0f;
            _rb.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        /// <summary>El stick deja de mover la peonza estos segundos; después el control vuelve poco a poco.</summary>
        private void LoseControl(float seconds)
        {
            _controlLossEnd = Mathf.Max(_controlLossEnd, Time.time + seconds);
            _controlRecoverEnd = _controlLossEnd + CombatConfig.Active.knockbackControlRecoverTime;
        }

        /// <summary>Control del movimiento: 0 tras un empuje o lanzada, subiendo a 1 al recuperarse.</summary>
        private float ControlFactor
        {
            get
            {
                float now = Time.time;
                if (now < _controlLossEnd) return 0f;
                if (now >= _controlRecoverEnd) return 1f;
                return (now - _controlLossEnd) / Mathf.Max(0.01f, _controlRecoverEnd - _controlLossEnd);
            }
        }

        /// <summary>Llamado por CollisionResolver cuando esta peonza inflige daño.</summary>
        /// <summary>
        /// Llamado por CollisionResolver cuando esta peonza gana un choque (even = choque parejo).
        /// La energía del especial depende del tipo de golpe, no del daño: los cargados, que cuestan
        /// más cargas, dan más (y golpear a una Defensa activa también cuenta).
        /// </summary>
        public void RegisterSuccessfulHit(bool even)
        {
            var cfg = CombatConfig.Active;
            float energy;
            if (_attack.IsAttacking)
            {
                int level = ChargeLevel;
                energy = level > 0
                    ? (cfg.specialEnergyChargedHit + cfg.specialEnergyPerChargeLevel * level) * Trait(PartTraitType.SpecialEnergyChargedHit)
                    : cfg.specialEnergyQuickHit * Trait(PartTraitType.SpecialEnergyQuickHit);
            }
            else if (IsDashAttacking) energy = cfg.specialEnergyDashHit * Trait(PartTraitType.SpecialEnergyDashHit);
            else energy = cfg.specialEnergyPassiveHit;

            if (even) energy *= 0.5f;
            AddSpecialEnergy(energy);

            if (IsAttacking) _attack.RegisterHit();
        }

        /// <summary>Energía del especial con el ritmo general y el rasgo de energía de las piezas.</summary>
        private void AddSpecialEnergy(float energy) =>
            _special.AddEnergy(energy * CombatConfig.Active.specialEnergyMultiplier * Trait(PartTraitType.SpecialEnergyAll));

        /// <summary>Multiplicador de un rasgo de las piezas equipadas (1 si ninguna lo tiene).</summary>
        /// <summary>Multiplicador de un rasgo de sus piezas (1 si ninguna lo tiene).</summary>
        public float Trait(PartTraitType type) => _stats != null ? _stats.Trait(type) : 1f;
        /// <summary>Suma de un rasgo aditivo de sus piezas (Espinas, Golpe lento; 0 si ninguna lo tiene).</summary>
        public float TraitSum(PartTraitType type) => _stats != null ? _stats.TraitSum(type) : 0f;

        /// <summary>¿Ya se resolvió un choque con este rival hace menos de clashCooldown? (memoria por rival)</summary>
        private bool IsClashOnCooldown(FakeBladeController other)
        {
            float cooldown = CombatConfig.Active.clashCooldown;
            for (int i = 0; i < ClashMemory; i++)
                if (_clashPartners[i] == other) return Time.time - _clashTimes[i] < cooldown;
            return false;
        }

        /// <summary>Segundos desde el último choque resuelto con este rival (infinito si no lo recuerda).</summary>
        private float TimeSinceClash(FakeBladeController other)
        {
            for (int i = 0; i < ClashMemory; i++)
                if (_clashPartners[i] == other) return Time.time - _clashTimes[i];
            return float.PositiveInfinity;
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

        /// <summary>Recompensa de un parry con éxito: energía de especial (la carga ya no se devuelve, 2026-10-11).</summary>
        /// <summary>Le han hecho parry: algo de energía por haberse arriesgado a atacar (2026-10-12).</summary>
        public void RegisterParried() => AddSpecialEnergy(CombatConfig.Active.parriedEnergy);

        /// <summary>Aturdida unos segundos (sin control, ataques ni dash) sin cortar lo que esté haciendo.</summary>
        public void ApplyStagger(float seconds)
        {
            if (seconds > 0f) _staggerEndTime = Mathf.Max(_staggerEndTime, Time.time + seconds);
        }

        /// <summary>Energía del parry; el bloqueo da solo una parte (<paramref name="energyScale"/>).</summary>
        public void RegisterParry(float energyScale = 1f)
        {
            AddSpecialEnergy(CombatConfig.Active.parryEnergy * energyScale * Trait(PartTraitType.SpecialEnergyParry));

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
            // Golpes con efecto (Fuego, Hielo): reacciona el poder de quien ha hecho el daño
            if (other != null && damageTaken > 0f) other._special.NotifyClashDamageDealt(this, damageTaken);
            OnClash?.Invoke(other, damageTaken);
            if (other != null) other.OnClashDealt?.Invoke(this, damageTaken);
        }

        /// <summary>
        /// Golpe de ataque enemigo: corta la curación del poder que quede (GDD 5). Paredes, roce,
        /// estados y daño de poderes no la cortan: solo lo llama CollisionResolver.
        /// </summary>
        public void InterruptHeal()
        {
            if (_isDestroyed || !_special.CutHeal()) return;

            VfxLibrary vfx = VfxSystem.Library;
            if (vfx != null) VfxSystem.Play(VfxType.LowSpinSmoke, _transform.position, Vector3.up, vfx.smokeColor, 0.6f);
            OnHealCut?.Invoke();

            if (showDebugInfo) Debug.Log($"[FakeBlade] {name} curación cortada", this);
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

            // Un solo golpe de pared cada wallHitCooldown: en la unión de dos tramos llegan dos
            // contactos a la vez. Si el segundo es más fuerte, solo se suma la diferencia, sin efectos
            bool repeat = Time.time - _wallHitTime < cfg.wallHitCooldown;
            if (repeat && impactSpeed <= _wallHitSpeed) return;
            float damageSpeed = repeat ? impactSpeed - _wallHitSpeed : impactSpeed;
            if (!repeat) _wallHitTime = Time.time;
            _wallHitSpeed = impactSpeed;

            if (_status.IsLaunched)
            {
                // Lanzada (Rayos y Onda de choque, GDD 5): la pared la golpea como una peonza parada con la fuerza de
                // quien la lanzó (daño base + diferencia de velocidad), y el daño cuenta para él
                FakeBladeController thrower = _status.Source;
                float offense = thrower != null ? thrower.OffenseMultiplier : 1f;
                ApplyDamage(damageSpeed * (cfg.damagePerImpactSpeed + cfg.damagePerSpeedDiff) * offense * Trait(PartTraitType.WallDamageTaken), thrower);
                if (repeat) return;
                PlayClashFeedback(contact.point, Mathf.Clamp01(impactSpeed / 15f));
                BladeSpecialFx.PlayLaunchedWallHit(this, contact.point);
                OnWallHit?.Invoke(Mathf.Clamp01(impactSpeed / 15f));
                return;
            }

            // Como un choque parejo contra una peonza a esa velocidad (wallDamageScale = 1)
            ApplyDamage(damageSpeed * cfg.damagePerImpactSpeed * cfg.wallDamageScale * Trait(PartTraitType.WallDamageTaken), null);
            if (repeat) return;
            VfxSystem.Play(VfxType.WallHit, contact.point, contact.normal, Color.white, Mathf.Clamp01(impactSpeed / 15f) + 0.3f);
            OnWallHit?.Invoke(Mathf.Clamp01(impactSpeed / 15f));
        }

        public bool IsAllyOf(FakeBladeController other)
        {
            if (other == null || other == this) return false;
            // El clon de Fantasma es del bando de su dueña: aliado de ella y de sus aliados
            FakeBladeController me = IsGhost ? GhostOwner : this;
            FakeBladeController them = other.IsGhost ? other.GhostOwner : other;
            if (me == them) return true;
            if (me._owner == null || them._owner == null) return false;

            var gm = GameManager.Instance;
            return gm != null && gm.Rules.teams && me._owner.TeamID == them._owner.TeamID;
        }

        /// <summary>Una es el clon de Fantasma de la otra (nunca se hacen daño, ni con fuego amigo).</summary>
        public bool IsGhostPairWith(FakeBladeController other) =>
            other != null && (other.GhostOwner == this || GhostOwner == other || (IsGhost && GhostOwner == other.GhostOwner));

        #region Fantasma (clon)
        /// <summary>Si esta peonza es el clon de Fantasma de otra, su dueña (si no, null).</summary>
        public FakeBladeController GhostOwner { get; private set; }
        public bool IsGhost => GhostOwner != null;
        /// <summary>Ataques que ha recibido el clon desde que apareció (con 3 se deshace).</summary>
        public int GhostHits { get; private set; }
        /// <summary>El clon de Fantasma de esta peonza (se crea una vez y se reutiliza).</summary>
        public FakeBladeController GhostClone { get; set; }
        private float _ghostDamageMultiplier = 1f;

        /// <summary>
        /// Convierte esta peonza en el clon de Fantasma de owner: no pierde RPM ni recibe estados, no usa especial y su
        /// daño (× damageMultiplier) cuenta para su dueña.
        /// </summary>
        public void MakeGhost(FakeBladeController owner, float damageMultiplier)
        {
            GhostOwner = owner;
            _ghostDamageMultiplier = damageMultiplier;
        }

        public void ResetGhostHits() => GhostHits = 0;
        public void RegisterGhostHit() => GhostHits++;

        /// <summary>
        /// Poder cuyos efectos lleva la peonza (K8): el suyo o, en el clon de Fantasma, el de su dueña (2026-10-27: el clon lleva
        /// los efectos de mientras dura el poder).
        /// </summary>
        public SpecialAbilitySystem FxSpecial => IsGhost ? GhostOwner.Special : _special;

        /// <summary>
        /// Los efectos del poder siguen (con su carga): activo o, si su entrada de la biblioteca lo pide (While Lingering), mientras
        /// dura lo que deja (el clon de Fantasma). En el clon, todo el rato que está fuera.
        /// </summary>
        public bool FxSpecialActive
        {
            get
            {
                if (_isDestroyed) return false;
                if (IsGhost || _special.IsActive) return true;
                if (!_special.IsLingering) return false;
                VfxLibrary lib = VfxSystem.Library;
                SpecialVfxStyle style = lib != null ? lib.GetSpecialEffects(_special.Type) : null;
                return style != null && style.whileLingering;
            }
        }

        /// <summary>Efectos del poder encendidos: FxSpecialActive y fuera de la carga de juego (el clon no tiene carga).</summary>
        public bool FxSpecialOn => FxSpecialActive && (IsGhost || !_special.IsWindingUp);

        /// <summary>Opacidad de la peonza (1 = opaca; Fantasma, 2026-10-27). La pinta PlayerController con BladePaint.</summary>
        public float BodyAlpha { get; private set; } = 1f;
        private float _bodyAlpha = 1f;
        private float _fadeFlickerWait, _fadeFlickerLeft;
        /// <summary>Segundos desde que se encendieron los efectos del poder (cúpulas After Windup).</summary>
        private float _fxOnTime;

        /// <summary>
        /// El clon de Fantasma vuelve a salir: aparece desde invisible, sus cúpulas vuelven a crecer y sus efectos empiezan de
        /// cero. Al volver a activar su objeto, Unity arranca solas las auras de paquetes que lleva escondidas (Play On Awake:
        /// salía con el Aura Burn y el Aura Smoke, como quemado; 2026-10-27), así que se paran aquí, como al reaparecer.
        /// </summary>
        public void RestartGhostVisuals()
        {
            _bodyAlpha = 0f;
            BodyAlpha = 0f;
            _fxOnTime = 0f;
            _specialFx?.Clear();
            _arcs?.Hide();
            _dome?.Hide();
            _baseDome?.Hide();
        }
        #endregion

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
            if (velocity.y > MAX_UPWARD_VELOCITY && !IsLeaping)
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
            // Desactivado en prueba (2026-10-07): el freno al cargar no se notaba jugando, pero hacía que un
            // ataque rápido a toda velocidad pegara igual o más que uno cargado. Si se recupera, volver a activarlo.
            // if (_attack.IsCharging) maxSpeed *= cfg.moveMultiplierWhileCharging;

            // Recuperación del tope de velocidad tras un burst
            if (_burstHold > 0f)
                _burstHold -= dt;
            else if (_burstCap > 0f)
                _burstCap = Mathf.MoveTowards(_burstCap, 0f, _burstCapStart / Mathf.Max(0.05f, cfg.burstRecoverTime) * dt);

            bool bursting = _burstHold > 0f;

            bool staggered = IsStaggered; // tras un parry: sin control y sin frenar el rebote
            // Tras un empuje fuerte o una lanzada: el stick no acelera ni frena (0), y vuelve poco a poco
            float control = ControlFactor;
            if (_simulationActive && !staggered && control > 0f && _moveInput.sqrMagnitude > 0.01f)
            {
                Vector3 inputDir = _moveInput.normalized;
                float inputMag = Mathf.Min(1f, _moveInput.magnitude);

                // 1. Aceleración hacia el input (se reduce al acercarse al tope en esa dirección)
                float speedInInputDir = Vector3.Dot(horizontal, inputDir);
                float targetSpeed = maxSpeed * inputMag;
                float accel = _effectiveAcceleration * control * _special.AccelerationMultiplier;
                if (speedInInputDir > 0f && targetSpeed > 0f)
                    accel *= Mathf.Clamp01(1f - speedInInputDir / targetSpeed);

                // Como aceleración: igual para cualquier masa (el peso ya cuenta en _effectiveAcceleration, C11)
                _rb.AddForce(inputDir * accel, ForceMode.Acceleration);

                // 2. Frenado lateral (permite girar). Más suave durante un burst.
                //    Se aplica como cambio de velocidad acotado (fracción ≤ 1 por paso): un frenado
                //    proporcional aplicado como fuerza diverge con peonzas ligeras (k·dt/m > 2).
                Vector3 lateral = horizontal - inputDir * speedInInputDir;
                if (lateral.sqrMagnitude > 0.01f)
                {
                    // Mojada (Rastro de agua): casi sin agarre, derrapa
                    float brakeRate = _effectiveTurnRate * control * _status.WetGripMultiplier;
                    if (bursting) brakeRate *= 0.5f;
                    _rb.AddForce(-lateral * Mathf.Min(1f, brakeRate * dt), ForceMode.VelocityChange);
                }

                // 3. Frenado extra si va en dirección contraria al input
                if (speedInInputDir < -0.5f && speed > 0.01f)
                    _rb.AddForce(-horizontal / speed * (_effectiveAcceleration * 0.5f * control), ForceMode.Acceleration);
            }
            else if (!bursting && !staggered && control >= 1f)
            {
                // Sin input (y con control): frenado progresivo (acotado igual que el lateral para que sea estable)
                if (speed > 0.1f)
                {
                    float brakeRate = _effectiveStoppingRate * _status.WetGripMultiplier;
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
            // Cada pieza a su velocidad (O1); el disco, más lento con el estado que lo pida (congelada, K8), sin saltos
            if (PartSpin != null)
            {
                StatusVfxStyle statusVfx = BladeSpecialFx.StatusStyle(this);
                PartSpin.DiscSpin = Mathf.MoveTowards(PartSpin.DiscSpin, statusVfx != null ? statusVfx.discSpin : 1f, dt * 3f);
                PartSpin.Tick(visualSpeed, dt);
            }

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
            if (_particles != null)
            {
                _ribbonTrail?.Tick(dt, _particles.TipOffset);
                _burstRibbon?.Tick(dt, _particles.TipOffset);
                VfxLibrary lib = VfxSystem.Library;
                _burstTip?.Tick(dt, _particles.TipOffset, lib != null ? lib.burstTip : null, IsBursting);
                // El poder cuyos efectos lleva: el suyo o, en el clon de Fantasma, el de su dueña (2026-10-27)
                SpecialAbilitySystem fx = FxSpecial;
                SpecialVfxStyle specialVfx = FxStyle(lib);
                bool fxActive = FxSpecialActive;
                // La punta del especial; la de la Embestida, solo mientras embiste (cuenco delante, 2026-10-20), y la del
                // Salto, mientras vuela
                // Carga solo de los efectos más larga que la de juego (Defensa, 2026-10-23): hasta entonces siguen los de la carga.
                // El clon no tiene carga: sale ya encendido
                float visualWindup = specialVfx != null ? specialVfx.visualWindupTime : 0f;
                bool visualCharge = !IsGhost && visualWindup > 0f && _special.IsActive && _special.ActiveTime < visualWindup;
                // Rastro de agua (2026-10-26): la carga visual solo para las cúpulas; lo demás, con la de juego
                bool gameWindup = !IsGhost && _special.IsWindingUp;
                bool windingUp = gameWindup || (visualCharge && !specialVfx.visualWindupDomesOnly);
                bool effectsOn = fxActive && !windingUp;
                bool rushing = effectsOn && fx.Ability != null && fx.Ability.IsRushing;
                bool tipOn = effectsOn;
                if (tipOn && specialVfx != null && specialVfx.tip != null && specialVfx.tip.onlyWhileRushing) tipOn = rushing;
                // Fuerte al encenderse y suave después (Soft After, 2026-10-29: el Escudo, mientras quedan escudos)
                float fxLevel = FxLevel(specialVfx);
                _specialTip?.Tick(dt, _particles.TipOffset, specialVfx?.tip, tipOn, fxLevel);
                SpiralTrailStyle spiral = specialVfx?.spiralTrail;
                _spiralTrail?.Tick(dt, _particles.TipOffset, spiral, spiral != null && spiral.onlyWhileRushing ? rushing : effectsOn,
                    fx.Color);
                // Aro del estado (quemada o congelada; los estados van de uno en uno)
                StatusVfxStyle statusFx = BladeSpecialFx.StatusStyle(this);
                _burnTip?.Tick(dt, _particles.TipOffset, statusFx?.tip, statusFx != null);
                _specialFx?.Tick(dt, _particles.TipOffset, specialVfx, fxActive, windingUp, fxLevel);
                _arcs?.Tick(dt, _particles.TipOffset, specialVfx?.arcs, effectsOn, fxActive && windingUp);
                // Cúpula: mientras carga (Onda de choque), mientras atrae (Tornado: embudo y cúpula) o mientras dura (Defensa;
                // y cualquiera con Dome While Active)
                float domeCharge = _special.ChargeVisualProgress;
                // Todo el rato que dura el poder (Dome While Active, 2026-10-27): crece en la carga y se queda entera
                if (domeCharge < 0f && fxActive && specialVfx != null && specialVfx.domeWhileActive)
                    domeCharge = gameWindup ? _special.WindupProgress : 1f;
                if (visualCharge && domeCharge >= 0f) domeCharge = _special.ActiveTime / visualWindup;
                _fxOnTime = effectsOn ? _fxOnTime + dt : 0f;
                float windupCharge = visualCharge ? _special.ActiveTime / visualWindup : _special.WindupProgress;
                float dome = DomeProgress(specialVfx?.dome, domeCharge, gameWindup || visualCharge, windupCharge, effectsOn);
                float baseDome = DomeProgress(specialVfx?.baseDome, domeCharge, gameWindup || visualCharge, windupCharge, effectsOn);
                _dome?.Tick(dt, _particles.TipOffset, specialVfx?.dome, dome >= 0f && !_isDestroyed, dome);
                _baseDome?.Tick(dt, _particles.TipOffset, specialVfx?.baseDome, baseDome >= 0f && !_isDestroyed, baseDome);
                // Escudos (Escudo): se ven mientras queden, aunque el poder ya no esté activo; el clon de Fantasma no los lleva
                ShieldsAbility shields = IsGhost ? null : _special.Ability as ShieldsAbility;
                _shieldHex?.Tick(dt, _particles.TipOffset, shields != null ? specialVfx?.shieldHex : null, shields);
                _usesIndicator?.Tick(dt, _particles.TipOffset, lib != null ? lib.usesIndicator : null, _isDestroyed ? 0 : _special.UsesLeft,
                    _special.Color);
                UpdateBodyFade(specialVfx?.bodyFade, effectsOn, dt);
            }
            _overlay?.Tick(dt);
        }

        /// <summary>
        /// Fuerza de los efectos de mientras dura el poder (Soft After / Soft Level, 2026-10-29): toda durante Soft After s desde
        /// que se encienden y después baja a Soft Level en Soft Fade s (Escudo: fuerte al activarlo y suave mientras quedan
        /// escudos). Vuelve a toda al encenderse otra vez (_fxOnTime se pone a cero en la carga).
        /// </summary>
        private float FxLevel(SpecialVfxStyle style)
        {
            if (style == null || style.softAfter <= 0f || style.softLevel >= 1f) return 1f;
            float t = (_fxOnTime - style.softAfter) / style.softFade;
            return t <= 0f ? 1f : Mathf.Lerp(1f, style.softLevel, t);
        }

        /// <summary>
        /// Carga que pinta una cúpula según cuándo sale (0-1; -1 = no sale). Normal: la del poder (normal). Windup Only: solo en
        /// la carga del principio (windup, con su carga windupCharge), y revienta al encenderse. After Windup: solo mientras
        /// duran los efectos, creciendo en Grow Time (Fantasma, 2026-10-27).
        /// </summary>
        private float DomeProgress(ChargeDomeStyle style, float normal, bool windup, float windupCharge, bool effectsOn)
        {
            if (style == null) return -1f;
            switch (style.timing)
            {
                case DomeTiming.WindupOnly: return windup ? windupCharge : -1f;
                case DomeTiming.AfterWindup: return effectsOn ? Mathf.Clamp01(_fxOnTime / style.growTime) : -1f;
                default: return normal;
            }
        }

        /// <summary>
        /// Transparencia de la peonza (Fantasma, 2026-10-27): la dueña se queda medio transparente mientras duran los efectos y
        /// recupera la opacidad poco a poco al acabar; el clon aparece desde invisible y se queda más transparente. Los dos
        /// parpadean de vez en cuando. La aplica PlayerController con BladePaint.
        /// </summary>
        private void UpdateBodyFade(BladeFadeStyle fade, bool effectsOn, float dt)
        {
            if (fade == null || !fade.enabled)
            {
                _bodyAlpha = 1f;
                BodyAlpha = 1f;
                return;
            }
            float target = IsGhost ? fade.cloneAlpha : effectsOn ? fade.alpha : 1f;
            // Cada paso, en su tiempo: que la dueña tarde Recover Time en volver, sea cual sea su opacidad
            float speed = IsGhost ? fade.cloneAlpha / fade.cloneAppearTime
                : (1f - fade.alpha) / (target < _bodyAlpha ? fade.fadeTime : fade.recoverTime);
            _bodyAlpha = Mathf.MoveTowards(_bodyAlpha, target, Mathf.Max(0.01f, speed) * dt);

            float shown = _bodyAlpha;
            if (_bodyAlpha < 0.999f && fade.flickerInterval.y > 0f)
            {
                if (_fadeFlickerLeft > 0f)
                {
                    _fadeFlickerLeft -= dt;
                    shown *= fade.flickerAlpha;
                }
                else
                {
                    _fadeFlickerWait -= dt;
                    if (_fadeFlickerWait <= 0f)
                    {
                        _fadeFlickerWait = UnityEngine.Random.Range(fade.flickerInterval.x, fade.flickerInterval.y);
                        _fadeFlickerLeft = fade.flickerTime;
                    }
                }
            }
            BodyAlpha = shown;
        }

        /// <summary>Efectos del poder que lleva (FxSpecial); en el clon, solo si la entrada del Fantasma los pide (Clone Effects).</summary>
        private SpecialVfxStyle FxStyle(VfxLibrary lib)
        {
            if (lib == null) return null;
            if (!IsGhost) return lib.GetSpecialEffectsOrGeneric(_special.Type);
            SpecialVfxStyle style = lib.GetSpecialEffects(FxSpecial.Type);
            return style != null && style.cloneEffects ? style : null;
        }

        /// <summary>El modelo se ha vuelto a montar (piezas nuevas): los efectos sobre la peonza ponen su material en él.</summary>
        public void OnModelRebuilt() => _overlay?.Rebind();

        /// <summary>Color del jugador (efectos y HUD).</summary>
        public Color OwnerColor => _owner != null ? _owner.PlayerColor : Color.white;

        /// <summary>Pivote que gira (donde va el modelo).</summary>
        public Transform VisualRoot => visualRoot;
        /// <summary>Cómo se monta el modelo con los de las piezas (también lo usan las vistas previas).</summary>
        public BladeModelSettings ModelSettings => modelSettings;

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
            _controlLossEnd = float.NegativeInfinity;
            _trappedEnd = float.NegativeInfinity;
            _controlRecoverEnd = float.NegativeInfinity;
            _leapUntil = float.NegativeInfinity;
            _leapGravityScale = 1f;
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
            _ribbonTrail?.Clear();
            _burstRibbon?.Clear();
            _burstTip?.Hide();
            _specialTip?.Hide();
            _spiralTrail?.Hide();
            _burnTip?.Hide();
            _specialFx?.Clear();
            _arcs?.Hide();
            _dome?.Hide();
            _baseDome?.Hide();
            _shieldHex?.Hide();
            _usesIndicator?.Hide();
            _overlay?.Clear();

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
            _ribbonTrail?.Clear(); // sin una cinta que cruce la arena
            _burstRibbon?.Clear();
            if (_rb == null) return;

            _rb.position = position;
            _rb.rotation = Quaternion.identity;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        /// <summary>Pone la velocidad tras un SetPosition (Fantasma: cada una sigue con la suya al cambiar de sitio con el clon).</summary>
        public void SetVelocity(Vector3 velocity)
        {
            if (_rb != null) _rb.linearVelocity = velocity;
        }

        public void SetInvulnerable(float seconds)
        {
            _invulnerableTimer = Mathf.Max(_invulnerableTimer, seconds);
        }

        /// <summary>El dash vuelve a estar disponible ya (truco del sandbox).</summary>
        public void ClearDashCooldown() => _dashTimer = 0f;
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
