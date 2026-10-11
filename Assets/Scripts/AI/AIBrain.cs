using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// IA rival (GDD 6.4, D1). Pulsa los mismos botones que un jugador (IBladeInputSource) y decide
    /// por prioridades:
    /// 1. Salir de la pared si está pegada y va hacia ella.
    /// 2. Defenderse de un ataque que llega: parry (ataque rápido justo antes del choque) o esquivar con dash.
    ///    Se mira cada frame, con el retraso de reacción del nivel.
    /// 3. Atacar si el rival está a tiro y tiene cargas: cargado (se acerca mientras carga) o rápido
    ///    (embiste y lo suelta al tenerlo a StrikeDistance).
    /// 4. Acercarse rodeando al rival (con algún dash para cerrar distancia) y, sin atacar, guardar
    ///    distancia (KeepDistance, salvo en Fácil) para tener tiempo de reaccionar.
    /// Los niveles que hacen parry a menudo guardan una carga para él (ReservedCharges).
    /// Capa táctica (Difícil, Experto): una agresividad (EvaluateAggression) según la vida, los parrys que le
    /// hace el rival, si está quieto o junto a la pared, que decide si guarda carga, cuánto carga y si coge carrerilla.
    /// El nivel (AILevel) fija reacción, puntería, probabilidades y qué datos lee.
    ///
    /// Clase base de los perfiles por arquetipo (D3): sobrescriben Think, OnIncomingAttack,
    /// ChooseAttackLevel o TryUseSpecial. Sin asignaciones por frame.
    /// </summary>
    [RequireComponent(typeof(FakeBladeController))]
    public class AIBrain : MonoBehaviour, IBladeInputSource
    {
        /// <summary>Distancia entre centros a la que intenta atacar.</summary>
        protected const float AttackRange = 4f;
        /// <summary>
        /// Distancia entre centros que guarda mientras no ataca: pegada al rival, un ataque suyo llega
        /// antes de poder reaccionar (ni parry ni esquiva).
        /// </summary>
        protected const float KeepDistance = 2.8f;
        /// <summary>Distancia a la pared a la que intenta separarse de ella.</summary>
        protected const float WallMargin = 1.2f;
        /// <summary>Distancia mínima para empezar un cargado (cargar tarda).</summary>
        protected const float ChargedMinDistance = 2.5f;
        /// <summary>Un ataque rápido se suelta con el rival a esta distancia entre centros (lo que alcanza el impulso).</summary>
        protected const float StrikeDistance = 3f;
        /// <summary>Lo que dura como mucho la embestida antes de un ataque rápido.</summary>
        private const float EngageTime = 1f;
        /// <summary>Carrerilla: distancia entre centros desde la que entra a por el rival (la velocidad del choque suma daño).</summary>
        protected const float RunUpDistance = 5f;
        /// <summary>Lo que dura como mucho alejarse para coger carrerilla.</summary>
        private const float RunUpTime = 1.2f;
        /// <summary>Rival a menos de esta distancia de la pared: ocasión de empotrarlo.</summary>
        protected const float PinWallDistance = 1.5f;
        /// <summary>Con esta agresividad o más no se guarda carga para el parry y coge carrerilla contra un rival quieto.</summary>
        protected const float BoldAggression = 0.65f;
        /// <summary>Si el rival le ha lanzado un ataque hace menos de esto (s), se guarda la carga del parry aunque sea agresiva.</summary>
        private const float RecentThreatTime = 2f;
        /// <summary>Tope de lo que mantiene pulsado un cargado (por si no llega al nivel).</summary>
        private const float MaxChargeHold = 2.5f;
        /// <summary>Lanza el parry cuando faltan estos segundos para el choque (ventana de parry 0,12 s).</summary>
        private const float ParryLead = 0.1f;
        /// <summary>Tras soltar el ataque o pedir el dash, sigue empujando en esa dirección (la usa el controller).</summary>
        private const float AimHoldTime = 0.08f;

        /// <summary>
        /// Engage: va recto a por el rival para soltar un ataque rápido al tenerlo a StrikeDistance.
        /// RunUp: se aleja hasta RunUpDistance para entrar con velocidad (carrerilla).
        /// </summary>
        private enum Intent { Wander, Approach, EscapeWall, Engage, RunUp }

        [SerializeField] private AIDifficulty difficulty = AIDifficulty.Normal;

        protected FakeBladeController Blade;
        protected AILevel Level;
        protected readonly AIPerception Perception = new AIPerception();
        private ArenaDefinition _arena;

        private Intent _intent;
        private float _orbitSign = 1f;
        private float _thinkTimer;
        private float _engageUntil;
        private float _runUpUntil;
        private int _runUpLevel;

        // Capa táctica: cuánto arriesga (0-1, 0,5 = neutral) y lo que ha visto en la partida
        protected float Aggression { get; private set; } = 0.5f;
        private int _rivalParries;
        private int _attacksLaunched;

        // Ataque en curso (botón pulsado) y dirección forzada
        private bool _attackHeld;
        private int _chargeTarget;
        private float _holdTimer;
        private int _pressFrame;
        private float _aimError;
        private bool _aimTracksTarget;
        private Vector3 _aimDir;
        private float _aimTimer;

        // Defensa: amenaza vista, cuándo reacciona y parry preparado
        private bool _threatSeen;
        private bool _threatHandled;
        private float _reactAt;
        private bool _parryPending;
        private float _parryLead;
        private float _lastThreatTime = float.NegativeInfinity;

        private bool _dash;
        private bool _special;
        private Vector2 _move;

        public AIDifficulty Difficulty
        {
            get => difficulty;
            set
            {
                difficulty = value;
                Level = AILevel.Get(value);
            }
        }

        #region IBladeInputSource
        public Vector2 MovementInput => _move;
        public bool AttackHeld => _attackHeld;

        public bool ConsumeDash()
        {
            bool value = _dash;
            _dash = false;
            return value;
        }

        public bool ConsumeSpecial()
        {
            bool value = _special;
            _special = false;
            return value;
        }

        public void ClearBuffers()
        {
            _dash = _special = false;
            _attackHeld = false;
            _parryPending = false;
            _aimTimer = 0f;
            _engageUntil = 0f;
            _move = Vector2.zero;
        }

        public void Vibrate(float lowFrequency, float highFrequency, float duration) { }
        #endregion

        protected virtual void Awake()
        {
            Blade = GetComponent<FakeBladeController>();
            Level = AILevel.Get(difficulty);
            _orbitSign = Random.value < 0.5f ? -1f : 1f;
            _thinkTimer = Random.Range(0f, Level.reactionTime);
        }

        protected virtual void OnValidate()
        {
            Level = AILevel.Get(difficulty);
        }

        protected virtual void OnEnable()
        {
            CollisionResolver.OnParry += HandleParry;
            if (Blade != null) Blade.OnAttackLaunched += HandleAttackLaunched;
        }

        protected virtual void OnDisable()
        {
            CollisionResolver.OnParry -= HandleParry;
            if (Blade != null) Blade.OnAttackLaunched -= HandleAttackLaunched;
        }

        /// <summary>Cuenta los parrys que le hacen (para saber si el rival es capaz de hacérselos).</summary>
        private void HandleParry(FakeBladeController parrier, FakeBladeController attacker, bool doubleParry)
        {
            if (!doubleParry && attacker == Blade) _rivalParries++;
        }

        private void HandleAttackLaunched(int level) => _attacksLaunched++;

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (Blade.IsDestroyed)
            {
                ClearBuffers();
                return;
            }

            Perception.Refresh(Blade, _arena, Level);
            UpdateAttack(dt);
            UpdateDefense();
            if (_intent == Intent.Engage) UpdateEngage();
            else if (_intent == Intent.RunUp) UpdateRunUp();

            _thinkTimer -= dt;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = Level.reactionTime * Random.Range(0.7f, 1.3f);
                if (_arena == null) _arena = FindAnyObjectByType<ArenaDefinition>();
                Think();
            }

            _move = Steer();
        }

        #region Decisión
        /// <summary>Decisión periódica (cada tiempo de reacción): prioridades 1, 3 y 4 y el especial.</summary>
        protected virtual void Think()
        {
            AIPerception p = Perception;

            // 1. Pared
            if (p.WallDistance < WallMargin && p.OutwardSpeed > -0.5f)
            {
                _intent = Intent.EscapeWall;
                return;
            }

            if (p.Target == null)
            {
                _intent = Intent.Wander;
                return;
            }

            // Embistiendo o cogiendo carrerilla: sigue hasta soltar el golpe (UpdateEngage / UpdateRunUp)
            if ((_intent == Intent.Engage && Time.time < _engageUntil) || (_intent == Intent.RunUp && Time.time < _runUpUntil))
            {
                TryUseSpecial();
                return;
            }

            _intent = Intent.Approach;
            if (Random.value < 0.05f) _orbitSign = -_orbitSign;
            Aggression = EvaluateAggression();

            TryUseSpecial();

            // 2. La defensa va cada frame (UpdateDefense): mientras prepara un parry o ataca, no empieza otro
            if (_parryPending || _attackHeld || Blade.Attack.IsAttacking || Blade.IsStaggered) return;

            // 3. Atacar (sin gastar las cargas que guarda para el parry)
            if (p.Distance < AttackRange && Blade.Attack.CurrentCharges > ReservedCharges())
            {
                float chance = Level.attackChance * AggressionScale(0.7f, 1.2f);
                // Sin cargas, el rival no puede hacer parry: es un buen momento
                if (p.TargetCharges == 0) chance += 0.3f;
                if (Random.value < chance)
                {
                    int level = ChooseAttackLevel();
                    if (level < 0) return; // espera a juntar cargas para un cargado
                    // Golpe fuerte contra un rival quieto: antes, carrerilla (la velocidad del choque suma daño)
                    if (Level.tactical && p.TargetIdle && Aggression >= BoldAggression && p.Distance < RunUpDistance)
                    {
                        _intent = Intent.RunUp;
                        _runUpUntil = Time.time + RunUpTime;
                        _runUpLevel = level;
                    }
                    // El cargado se acerca mientras carga; el rápido, si no llega, embiste primero
                    else if (level > 0 || p.Distance <= StrikeDistance)
                    {
                        StartAttack(level);
                    }
                    else
                    {
                        BeginEngage();
                    }
                    return;
                }
            }

            // 4. Acercarse: a media distancia, a veces con un dash (también golpea)
            if (p.Distance > AttackRange * 1.5f && p.Distance < AttackRange * 3f && Blade.CanDash && Random.value < 0.15f)
                DashTowards(AimDirection(), false);
        }

        /// <summary>
        /// Cargas que no gasta atacando, para poder hacer parry: una si el nivel hace parry a menudo
        /// y el rival puede atacar (si el nivel no lee sus cargas, supone que sí).
        /// </summary>
        protected virtual int ReservedCharges() =>
            Level.parryChance >= 0.4f && Perception.TargetCharges != 0 &&
            (Aggression < BoldAggression || Time.time - _lastThreatTime < RecentThreatTime) ? 1 : 0;

        /// <summary>
        /// Nivel del siguiente ataque: 0 = rápido, 2+ = cargado (más probable cuanto más agresiva), o -1 = esperar:
        /// con capa táctica, si quiere un cargado y aún no tiene cargas para él, no gasta la que tiene.
        /// </summary>
        protected virtual int ChooseAttackLevel()
        {
            int charges = Blade.Attack.CurrentCharges - ReservedCharges();
            bool roomToCharge = Perception.Distance > ChargedMinDistance || (Level.tactical && Perception.TargetIdle);
            bool wantsCharged = roomToCharge && Blade.Attack.MaxCharges >= 2 &&
                                Random.value < Level.chargedChance * AggressionScale(0.5f, 2f);
            if (!wantsCharged) return 0;
            if (charges >= 2) return Random.Range(2, charges + 1);
            return Level.tactical ? -1 : 0;
        }

        /// <summary>
        /// Capa táctica (GDD 6.4): cuánto arriesgar, de 0 a 1 (0,5 = neutral). Baja si va por delante en vida o si
        /// el rival le hace parry a menudo; sube si va por detrás, si el rival está quieto o si puede empotrarlo
        /// contra la pared. Sin capa táctica (Fácil, Normal), siempre 0,5.
        /// </summary>
        protected virtual float EvaluateAggression()
        {
            AIPerception p = Perception;
            if (!Level.tactical || p.Target == null) return 0.5f;

            float aggression = 0.5f;
            // Vida: +30% de ventaja → −0,24
            aggression -= (Blade.SpinSpeedPercentage - p.TargetSpin) * 0.8f;
            // Parrys del rival: tasa estimada partiendo de un 30% (como si ya llevara 2 ataques)
            float parryRate = (_rivalParries + 0.6f) / (_attacksLaunched + 2f);
            aggression -= parryRate - 0.3f;
            if (p.TargetIdle) aggression += 0.25f;
            // Rival cerca de la pared y la IA empujando hacia ella
            if (p.TargetWallDistance < PinWallDistance && Vector3.Dot(p.ToTarget, p.TargetOutward) > 0.5f) aggression += 0.2f;
            return Mathf.Clamp01(aggression);
        }

        /// <summary>Escala según la agresividad: low con 0, 1 con 0,5 (neutral) y high con 1.</summary>
        protected float AggressionScale(float low, float high) =>
            Aggression < 0.5f ? Mathf.Lerp(low, 1f, Aggression * 2f) : Mathf.Lerp(1f, high, (Aggression - 0.5f) * 2f);

        /// <summary>Un ataque del rival viene hacia la IA (ya ha pasado su tiempo de reacción).</summary>
        protected virtual void OnIncomingAttack()
        {
            // Cargando: suelta lo que lleve para chocar atacando
            if (_attackHeld)
            {
                if (_chargeTarget > 0) ReleaseAttack();
                return;
            }

            if (Blade.Attack.CurrentCharges > 0 && Random.value < Level.parryChance)
            {
                // Con el error de timing del nivel: demasiado pronto o tarde, el parry falla
                _parryPending = true;
                _parryLead = ParryLead + Random.Range(-Level.parryTimingError, Level.parryTimingError);
                return;
            }

            if (Blade.CanDash && Random.value < Level.dodgeChance)
                Dodge();
        }

        /// <summary>Especial según el nivel (el buen momento de cada poder llega en D5).</summary>
        protected virtual void TryUseSpecial()
        {
            if (!Blade.Special.IsReady) return;
            switch (Level.specialUse)
            {
                case AISpecialUse.Random:
                    if (Random.value < 0.15f) _special = true;
                    break;
                case AISpecialUse.WhenFull:
                    _special = true;
                    break;
                case AISpecialUse.GoodMoment:
                    if (Perception.Distance < AttackRange * 1.5f) _special = true;
                    break;
            }
        }
        #endregion

        #region Acciones
        /// <summary>
        /// Pulsa ataque: rápido (nivel 0) o cargado hasta el nivel pedido. Con <paramref name="evenIfAttacking"/>
        /// se lanza aunque ya esté atacando (un ataque nuevo abre otra ventana de parry).
        /// </summary>
        protected bool StartAttack(int level, bool evenIfAttacking = false)
        {
            if (_attackHeld || Blade.Attack.CurrentCharges <= 0 || Blade.IsStaggered) return false;
            if (Blade.Attack.IsAttacking && !evenIfAttacking) return false;
            _attackHeld = true;
            _holdTimer = 0f;
            _pressFrame = Time.frameCount;
            _chargeTarget = level;
            _aimError = Random.Range(-Level.aimErrorDegrees, Level.aimErrorDegrees);
            _aimTracksTarget = true;
            _aimDir = AimDirection();
            return true;
        }

        private void ReleaseAttack()
        {
            _attackHeld = false;
            _aimTimer = AimHoldTime;
        }

        /// <summary>Dash en una dirección (el controller lo lanza hacia el input de movimiento).</summary>
        protected void DashTowards(Vector3 direction, bool tracksTarget)
        {
            _aimDir = direction;
            _aimTracksTarget = tracksTarget;
            _aimTimer = AimHoldTime;
            _dash = true;
        }

        /// <summary>Esquiva de lado, hacia el lado más alejado de la pared.</summary>
        protected void Dodge()
        {
            Vector3 side = new Vector3(-Perception.ToTarget.z, 0f, Perception.ToTarget.x);
            if (Vector3.Dot(side, Perception.ToCenter) < 0f) side = -side;
            DashTowards(side, false);
        }

        private void UpdateAttack(float dt)
        {
            if (!_attackHeld)
            {
                if (_aimTimer > 0f) _aimTimer -= dt;
                // Mientras dura su ataque sigue empujando hacia el rival: si vuelve a rodearlo, el golpe
                // se desvía y avanza la mitad (medido: 1,3-2,1 m rodeando frente a 2,5-3,2 m parada)
                if (_aimTracksTarget && Blade.Attack.IsAttacking) _aimDir = AimDirection();
                return;
            }

            _holdTimer += dt;
            if (_aimTracksTarget) _aimDir = AimDirection();

            // Rápido: suelta en el frame siguiente (el ataque sale al soltar; PlayerController ya ha visto la pulsación)
            bool release = _chargeTarget <= 0
                ? Time.frameCount > _pressFrame
                : Blade.Attack.ChargeLevel >= _chargeTarget || Blade.Attack.IsAttacking || _holdTimer >= MaxChargeHold;
            if (release || Blade.IsStaggered) ReleaseAttack();
        }

        /// <summary>Va recto a por el rival y suelta un ataque rápido al tenerlo a StrikeDistance.</summary>
        protected void BeginEngage()
        {
            _intent = Intent.Engage;
            _engageUntil = Time.time + EngageTime;
            _aimError = Random.Range(-Level.aimErrorDegrees, Level.aimErrorDegrees);
        }

        /// <summary>
        /// Carrerilla: se aleja del rival hasta RunUpDistance (o hasta la pared, o hasta que se acabe el tiempo)
        /// y entra: el cargado carga mientras corre hacia él; el rápido embiste.
        /// </summary>
        private void UpdateRunUp()
        {
            AIPerception p = Perception;
            if (p.Target == null || _parryPending)
            {
                _intent = Intent.Approach;
                return;
            }

            bool far = p.Distance >= RunUpDistance;
            bool noRoom = p.WallDistance < WallMargin;
            if (!far && !noRoom && Time.time < _runUpUntil) return;

            _intent = Intent.Approach;
            if (_runUpLevel > 0 && Blade.Attack.CurrentCharges >= _runUpLevel) StartAttack(_runUpLevel);
            else BeginEngage();
        }

        /// <summary>Embestida: suelta el ataque rápido al tener al rival a tiro; la deja si tarda o tiene que defenderse.</summary>
        private void UpdateEngage()
        {
            if (Perception.Target == null || _parryPending || Time.time >= _engageUntil)
            {
                _intent = Intent.Approach;
                return;
            }

            if (Perception.Distance <= StrikeDistance)
            {
                _intent = Intent.Approach;
                StartAttack(0);
            }
        }

        /// <summary>Cada frame: ve venir el ataque, espera su tiempo de reacción y decide; el parry sale justo antes del choque.</summary>
        private void UpdateDefense()
        {
            AIPerception p = Perception;
            // Un rival cargando ya cuenta como amenaza para quien lo lee: reacciona antes
            bool threat = p.IncomingAttack || (p.TargetCharging && p.Gap < AttackRange);
            if (!threat)
            {
                _threatSeen = false;
                _parryPending = false;
                return;
            }

            if (!_threatSeen)
            {
                _threatSeen = true;
                _lastThreatTime = Time.time;
                _threatHandled = false;
                _reactAt = Time.time + Level.reactionTime;
            }

            if (!_threatHandled && p.IncomingAttack && Time.time >= _reactAt)
            {
                _threatHandled = true;
                OnIncomingAttack();
            }

            if (_parryPending && p.IncomingAttack && p.TimeToImpact <= _parryLead)
            {
                _parryPending = false;
                StartAttack(0, true);
            }
        }
        #endregion

        #region Movimiento
        /// <summary>Hacia dónde apunta: al rival (o adelantado, si el nivel predice), con el error del nivel.</summary>
        protected Vector3 AimDirection()
        {
            AIPerception p = Perception;
            if (p.Target == null) return Blade.Facing;

            Vector3 aimPoint = p.Target.Position;
            if (Level.predictsMovement)
            {
                // Tiempo que tarda en llegar: hueco / (impulso del ataque + lo que ya avanza hacia él)
                float speed = Mathf.Max(4f, CombatConfig.Active.quickAttackImpulse + Vector3.Dot(Blade.Velocity, p.ToTarget));
                aimPoint += p.TargetVelocity * (p.Gap / speed);
            }

            Vector3 dir = aimPoint - Blade.Position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : p.ToTarget;
            return Quaternion.AngleAxis(_aimError, Vector3.up) * dir;
        }

        private Vector2 Steer()
        {
            Vector3 dir;
            if (_attackHeld || _aimTimer > 0f || (_aimTracksTarget && Blade.Attack.IsAttacking))
            {
                dir = _aimDir;
            }
            else
            {
                AIPerception p = Perception;
                switch (_intent)
                {
                    case Intent.EscapeWall:
                        // Hacia el centro, un poco de lado para no ir en línea recta contra el rival
                        dir = (p.ToCenter + new Vector3(-p.ToCenter.z, 0f, p.ToCenter.x) * (0.4f * _orbitSign)).normalized;
                        break;
                    case Intent.Engage:
                        dir = AimDirection();
                        break;
                    case Intent.RunUp:
                        // Alejarse del rival, algo de lado para no ir recto contra la pared
                        dir = (-p.ToTarget * 0.8f + new Vector3(-p.ToTarget.z, 0f, p.ToTarget.x) * (0.4f * _orbitSign)).normalized;
                        break;
                    case Intent.Approach:
                        // Lejos: casi recto. A tiro: lo rodea acercándose. Demasiado cerca: lo rodea alejándose
                        Vector3 perpendicular = new Vector3(-p.ToTarget.z, 0f, p.ToTarget.x) * _orbitSign;
                        if (p.Distance < KeepDistance && Level.keepsDistance)
                            dir = (perpendicular * 0.7f - p.ToTarget * 0.3f).normalized;
                        else if (p.Distance < AttackRange)
                            dir = (perpendicular * 0.6f + p.ToTarget * 0.4f).normalized;
                        else
                            dir = (perpendicular * 0.1f + p.ToTarget * 0.9f).normalized;
                        break;
                    default:
                        dir = p.ToCenter * 0.4f;
                        break;
                }
            }
            return new Vector2(dir.x, dir.z);
        }
        #endregion
    }
}
