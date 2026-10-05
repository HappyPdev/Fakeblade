using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Storm Breaker (GDD 5): menos daño, gran resistencia al empuje, algo más de velocidad y
    /// los ataques rápidos cuentan como cargados. Lo sustituirá el poder Defensa (Quehaceres A5/C1).
    /// </summary>
    [CreateAssetMenu(fileName = "StormBreaker", menuName = "FakeBlade/Specials/Storm Breaker")]
    public class StormBreakerData : SpecialAbilityData
    {
        [Header("Storm Breaker")]
        [Tooltip("Fracción del daño que se evita")]
        [Range(0f, 1f)] public float damageReduction = 0.6f;
        [Tooltip("Fracción del empuje que se evita")]
        [Range(0f, 1f)] public float knockbackResistance = 0.8f;
        [Tooltip("Velocidad de movimiento extra (0,15 = +15%)")]
        public float moveBonus = 0.15f;
        [Tooltip("Los ataques rápidos cuentan como cargados de nivel 1")]
        public bool quickAttacksAsCharged = true;

        public override SpecialAbilityType Type => SpecialAbilityType.Shield;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new StormBreakerAbility(this, owner);
    }

    public sealed class StormBreakerAbility : SpecialAbility<StormBreakerData>
    {
        public StormBreakerAbility(StormBreakerData data, FakeBladeController owner) : base(data, owner) { }

        public override float DamageTakenMultiplier => 1f - Config.damageReduction;
        public override float KnockbackTakenMultiplier => 1f - Config.knockbackResistance;
        public override float MoveSpeedMultiplier => 1f + Config.moveBonus;
        public override int MinAttackLevel => Config.quickAttacksAsCharged ? 1 : 0;
    }
}
