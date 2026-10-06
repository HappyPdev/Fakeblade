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

        #region Modifiers
        /// <summary>Multiplicador del daño recibido.</summary>
        public virtual float DamageTakenMultiplier => 1f;
        /// <summary>Multiplicador del empuje recibido.</summary>
        public virtual float KnockbackTakenMultiplier => 1f;
        /// <summary>Multiplicador del desgaste de RPM con el tiempo.</summary>
        public virtual float SpinDecayMultiplier => 1f;
        public virtual float MoveSpeedMultiplier => 1f;
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
