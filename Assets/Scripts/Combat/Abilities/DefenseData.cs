using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Defensa (GDD 5; sustituye a Storm Breaker).
    /// De momento conserva el efecto de Storm Breaker: menos daño, gran resistencia al empuje,
    /// algo más de velocidad y los ataques rápidos cuentan como cargados.
    /// Su efecto propio (sin pérdida de RPM, recarga x1,5, movimiento −15%, dash barato y corto)
    /// llega en Quehaceres C1.
    /// </summary>
    [CreateAssetMenu(fileName = "Defense", menuName = "FakeBlade/Specials/Defensa")]
    public class DefenseData : SpecialAbilityData
    {
        [Header("Defensa")]
        [Tooltip("Fracción del daño que se evita")]
        [Range(0f, 1f)] public float damageReduction = 0.6f;
        [Tooltip("Fracción del empuje que se evita")]
        [Range(0f, 1f)] public float knockbackResistance = 0.8f;
        [Tooltip("Velocidad de movimiento extra (0,15 = +15%)")]
        public float moveBonus = 0.15f;
        [Tooltip("Los ataques rápidos cuentan como cargados de nivel 1")]
        public bool quickAttacksAsCharged = true;

        public override SpecialAbilityType Type => SpecialAbilityType.Defense;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new DefenseAbility(this, owner);
    }

    public sealed class DefenseAbility : SpecialAbility<DefenseData>
    {
        public DefenseAbility(DefenseData data, FakeBladeController owner) : base(data, owner) { }

        public override float DamageTakenMultiplier => 1f - Config.damageReduction;
        public override float KnockbackTakenMultiplier => 1f - Config.knockbackResistance;
        public override float MoveSpeedMultiplier => 1f + Config.moveBonus;
        public override int MinAttackLevel => Config.quickAttacksAsCharged ? 1 : 0;
    }
}
