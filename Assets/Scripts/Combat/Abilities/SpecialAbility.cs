using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>Bonus de un poder en un choque: daño que hace y empuje que provoca (Rayos).</summary>
    public readonly struct ClashBonus
    {
        public static readonly ClashBonus None = new ClashBonus(1f, 1f, false);

        public ClashBonus(float damageMultiplier, float knockbackMultiplier, bool strong)
        {
            DamageMultiplier = damageMultiplier;
            KnockbackMultiplier = knockbackMultiplier;
            Strong = strong;
        }

        public float DamageMultiplier { get; }
        public float KnockbackMultiplier { get; }
        /// <summary>Golpe fuerte (Rayos): lanza al rival y gasta el poder.</summary>
        public bool Strong { get; }
        public bool IsNone => DamageMultiplier == 1f && KnockbackMultiplier == 1f && !Strong;
    }

    /// <summary>
    /// Defensa de un poder cuando otra peonza la embiste (Rebote, Escudo): cuánto daño recibe de ese choque, cuánto
    /// empuje se lleva la que la embiste (completo: sin la fracción reducida de quien gana el choque) y cuánto daño
    /// recibe esa otra.
    /// </summary>
    public readonly struct ClashGuard
    {
        public static readonly ClashGuard None = new ClashGuard(1f, 1f);

        public ClashGuard(float damageTakenMultiplier, float knockbackToAttackerMultiplier, float damageToAttackerMultiplier = 1f)
        {
            DamageTakenMultiplier = damageTakenMultiplier;
            KnockbackToAttackerMultiplier = knockbackToAttackerMultiplier;
            DamageToAttackerMultiplier = damageToAttackerMultiplier;
        }

        public float DamageTakenMultiplier { get; }
        public float KnockbackToAttackerMultiplier { get; }
        public float DamageToAttackerMultiplier { get; }
        public bool IsNone => DamageTakenMultiplier == 1f && KnockbackToAttackerMultiplier == 1f && DamageToAttackerMultiplier == 1f;
    }

    /// <summary>
    /// Comportamiento de un poder para una peonza concreta (lo crea su <see cref="SpecialAbilityData"/>).
    /// <see cref="SpecialAbilitySystem"/> lo activa, lo avanza y lo termina; el controller consulta sus
    /// modificadores, que solo cuentan mientras el poder está activo. Todo es opcional: cada poder
    /// sobrescribe solo lo que necesita.
    /// </summary>
    public abstract class SpecialAbility
    {
        protected SpecialAbility(SpecialAbilityData data, FakeBladeController owner)
        {
            Data = data;
            Owner = owner;
        }

        public SpecialAbilityData Data { get; }
        protected FakeBladeController Owner { get; }

        /// <summary>Al activarse, después del efecto común (RPM y cargas).</summary>
        public virtual void OnActivate() { }

        /// <summary>Cada frame mientras está activo.</summary>
        public virtual void Tick(float dt) { }

        /// <summary>Al terminar: se vacía la esfera, la peonza cae o se reinicia.</summary>
        public virtual void OnEnd() { }

        /// <summary>
        /// Ha quitado RPM a otra peonza en un choque (lo gane o no, también en un parry; el roce
        /// continuo no cuenta). Para los poderes cuyos golpes tienen efecto (Fuego, Hielo).
        /// </summary>
        public virtual void OnClashDamageDealt(FakeBladeController target, float damage) { }

        /// <summary>
        /// Bonus para un choque normal contra otra peonza (no en parrys ni con peonzas lanzadas),
        /// antes de calcular el daño y el empuje. approachSpeed: su velocidad hacia el rival (m/s).
        /// </summary>
        public virtual ClashBonus GetClashBonus(FakeBladeController target, float approachSpeed) => ClashBonus.None;

        /// <summary>Después de aplicar el daño y el empuje de un choque con bonus.</summary>
        public virtual void OnClashBonusApplied(FakeBladeController target, ClashBonus bonus, Vector3 contactPoint) { }

        /// <summary>
        /// Otra peonza la embiste en un choque normal (no en parrys ni con peonzas lanzadas). attackerApproachSpeed: la
        /// velocidad de la otra hacia ella (m/s). Rebote: no recibe daño y la otra sale despedida.
        /// </summary>
        public virtual ClashGuard GetClashGuard(FakeBladeController attacker, float attackerApproachSpeed) => ClashGuard.None;

        /// <summary>Después de aplicar un choque con defensa (efecto visual del rebote).</summary>
        public virtual void OnClashGuardApplied(FakeBladeController attacker, Vector3 contactPoint) { }

        /// <summary>
        /// Su efecto sigue después de terminar (Escudo: los escudos se quedan hasta romperse). Mientras, la esfera
        /// se vuelve a cargar y su defensa (GetClashGuard) sigue contando.
        /// </summary>
        public virtual bool IsLingering => false;

        /// <summary>Quita lo que quede del efecto (al reiniciar la peonza).</summary>
        public virtual void ClearLingering() { }

        /// <summary>Cada frame mientras el efecto sigue sin el poder activo (Rastro de agua: el agua que queda).</summary>
        public virtual void TickLingering(float dt) { }

        /// <summary>Cura mientras está activo (se corta con un golpe de ataque enemigo).</summary>
        public virtual bool Heals => false;

        /// <summary>
        /// Se pulsa Especial con el poder ya activo (Onda de choque: la siguiente onda, F1).
        /// Devuelve true si el poder lo ha usado.
        /// </summary>
        public virtual bool OnPressedWhileActive() => false;

        /// <summary>
        /// Distancia al rival (entre centros, m) a la que conviene activarlo o volver a pulsarlo; 0 = cualquiera.
        /// La usa la IA (Onda de choque: dentro de su radio).
        /// </summary>
        public virtual float UseRange => 0f;

        /// <summary>
        /// Distancia mínima al rival (entre centros, m) a la que conviene activarlo; 0 = sin mínimo. La usa la IA
        /// (Salto: con el rival demasiado cerca, el salto se pasa de largo).
        /// </summary>
        public virtual float UseRangeMin => 0f;

        /// <summary>Quedan usos que se lanzan pulsando Especial con el poder activo (la IA vuelve a pulsar).</summary>
        public virtual bool WantsRepeatPress => false;

        #region Modifiers
        /// <summary>Multiplicador del daño recibido.</summary>
        public virtual float DamageTakenMultiplier => 1f;
        /// <summary>Multiplicador del daño que hace a otras peonzas (Pinchos).</summary>
        public virtual float DamageDealtMultiplier => 1f;
        /// <summary>
        /// Fracción del daño de un ataque recibido que se devuelve a quien atacaba, como el rasgo Espinas
        /// (daño fijo, sin defensa; Pinchos).
        /// </summary>
        public virtual float ReflectFraction => 0f;
        /// <summary>Multiplicador del empuje recibido.</summary>
        public virtual float KnockbackTakenMultiplier => 1f;
        /// <summary>
        /// Multiplicador de la masa física (Tornado: inamovible mientras atrae). Su propio movimiento no cambia, porque
        /// se aplica como aceleración; solo cuesta más que la muevan los choques físicos.
        /// </summary>
        public virtual float PhysicalMassMultiplier => 1f;
        /// <summary>Multiplicador del desgaste de RPM con el tiempo.</summary>
        public virtual float SpinDecayMultiplier => 1f;
        public virtual float MoveSpeedMultiplier => 1f;
        /// <summary>Multiplicador de la aceleración (Spin Boost).</summary>
        public virtual float AccelerationMultiplier => 1f;
        /// <summary>Nivel mínimo con el que cuentan los ataques (1 = los rápidos cuentan como cargados).</summary>
        public virtual int MinAttackLevel => 0;
        /// <summary>Velocidad de recarga de las cargas de ataque.</summary>
        public virtual float AttackRechargeMultiplier => 1f;
        public virtual float DashCooldownMultiplier => 1f;
        /// <summary>Multiplicador del impulso del dash (y con él, de su alcance).</summary>
        public virtual float DashImpulseMultiplier => 1f;
        /// <summary>Multiplicador del coste en RPM del dash.</summary>
        public virtual float DashCostMultiplier => 1f;
        /// <summary>Velocidad de carga del ataque cargado.</summary>
        public virtual float ChargeSpeedMultiplier => 1f;
        /// <summary>Mientras está activo no se puede atacar ni hacer dash (Salto: ni en la carga ni en el aire).</summary>
        public virtual bool BlocksActions => false;
        /// <summary>Sus choques no admiten parry ni bloqueo (Embestida, mientras embiste).</summary>
        public virtual bool PreventsParry => false;
        #endregion
    }

    /// <summary>Base con acceso tipado a los datos del poder.</summary>
    public abstract class SpecialAbility<TData> : SpecialAbility where TData : SpecialAbilityData
    {
        protected SpecialAbility(TData data, FakeBladeController owner) : base(data, owner)
        {
            Config = data;
        }

        protected TData Config { get; }
    }
}
