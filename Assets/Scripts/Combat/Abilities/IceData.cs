using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Hielo (GDD 5). Al activarlo, una carga corta (se frena, como el Fuego; 2026-10-17), se quita la quemadura y lo
    /// mojado y se enciende. Mientras dura, la peonza recibe lo contrario de lo que sufren sus rivales congelados: se
    /// mueve más rápido y recarga los ataques antes, y el agua del suelo no la moja. Cualquier choque en el que quite RPM
    /// al rival lo congela: se mueve más lento y recarga los ataques más despacio. Un golpe nuevo reinicia la duración; no
    /// se acumula. Las reglas entre estados (una peonza en llamas no se congela; el fuego puede volver a quemarla aunque
    /// tenga el Hielo activo) las aplica StatusEffectSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "Ice", menuName = "FakeBlade/Specials/Hielo")]
    public class IceData : SpecialAbilityData
    {
        [Header("Ella misma, mientras dura")]
        [Tooltip("Velocidad de movimiento (1,2 = +20%)")]
        [Min(1f)] public float selfMoveMultiplier = 1.2f;
        [Tooltip("Velocidad de recarga de sus ataques (1,5 = una vez y media más rápida)")]
        [Min(1f)] public float selfRechargeMultiplier = 1.5f;

        [Header("Congelación (rivales)")]
        [Tooltip("Velocidad de movimiento del congelado (0,65 = −35%)")]
        [Range(0f, 1f)] public float freezeMoveMultiplier = 0.65f;
        [Tooltip("Velocidad de recarga de sus ataques (0,5 = a la mitad)")]
        [Range(0f, 1f)] public float freezeRechargeMultiplier = 0.5f;
        [Tooltip("Segundos que dura la congelación")]
        [Min(0.1f)] public float freezeDuration = 3f;

        public override SpecialAbilityType Type => SpecialAbilityType.Ice;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new IceAbility(this, owner);
    }

    public sealed class IceAbility : SpecialAbility<IceData>
    {
        private float _windup;

        public IceAbility(IceData data, FakeBladeController owner) : base(data, owner) { }

        public override bool IsWindingUp => _windup > 0f;
        /// <summary>La carga la lleva el propio poder (no la común de SpecialAbilitySystem).</summary>
        public override bool HandlesWindup => true;
        public override bool BlocksActions => IsWindingUp;
        public override float MoveSpeedMultiplier => IsWindingUp ? 1f : Config.selfMoveMultiplier;
        public override float AttackRechargeMultiplier => IsWindingUp ? 1f : Config.selfRechargeMultiplier;
        public override bool ImmuneToWet => true;

        public override void OnActivate()
        {
            _windup = Config.windupTime;
            // El hielo apaga el fuego y seca el agua que tuviera (el fuego puede volver a quemarla después)
            Owner.ClearBurning();
            Owner.Dry();
        }

        public override void OnEnd()
        {
            _windup = 0f;
        }

        public override void Tick(float dt)
        {
            if (!IsWindingUp) return;
            // Carga: se frena hasta quedarse quieta
            Vector3 v = Owner.Velocity;
            v.y = 0f;
            Owner.ApplyPull(-v * Mathf.Min(1f, Config.windupBrake * dt));
            _windup -= dt;
        }

        public override void OnClashDamageDealt(FakeBladeController target, float damage)
        {
            if (IsWindingUp) return; // todavía no se ha encendido
            target.TryFreeze(Owner, Config.freezeMoveMultiplier, Config.freezeRechargeMultiplier, Config.freezeDuration);
        }
    }
}
