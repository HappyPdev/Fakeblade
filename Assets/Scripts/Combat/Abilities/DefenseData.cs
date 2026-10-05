using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Defensa (GDD 5; sustituye a Storm Breaker). Defensa casi al 100%: no pierde RPM por golpes,
    /// paredes, quemadura ni desgaste, y el empuje que recibe es casi nulo. Además recarga los ataques
    /// más rápido, se mueve más lento y su dash es barato pero corto. Atacar sí cuesta RPM.
    /// </summary>
    [CreateAssetMenu(fileName = "Defense", menuName = "FakeBlade/Specials/Defensa")]
    public class DefenseData : SpecialAbilityData
    {
        [Header("Defensa")]
        [Tooltip("Fracción de la pérdida de RPM que se evita (golpes, paredes, quemadura y desgaste)")]
        [Range(0f, 1f)] public float spinLossReduction = 1f;
        [Tooltip("Fracción del empuje que se evita")]
        [Range(0f, 1f)] public float knockbackResistance = 0.9f;
        [Tooltip("Velocidad de recarga de las cargas de ataque (1,5 = x1,5)")]
        public float attackRechargeMultiplier = 1.5f;
        [Tooltip("Velocidad de movimiento (0,85 = −15%)")]
        public float moveMultiplier = 0.85f;

        [Header("Dash")]
        [Tooltip("Coste en RPM del dash (0,1 = 10% de lo normal)")]
        public float dashCostMultiplier = 0.1f;
        [Tooltip("Impulso del dash, y con él su alcance (0,6 = −40%)")]
        public float dashRangeMultiplier = 0.6f;

        public override SpecialAbilityType Type => SpecialAbilityType.Defense;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new DefenseAbility(this, owner);
    }

    public sealed class DefenseAbility : SpecialAbility<DefenseData>
    {
        public DefenseAbility(DefenseData data, FakeBladeController owner) : base(data, owner) { }

        public override float DamageTakenMultiplier => 1f - Config.spinLossReduction;
        public override float SpinDecayMultiplier => 1f - Config.spinLossReduction;
        public override float KnockbackTakenMultiplier => 1f - Config.knockbackResistance;
        public override float AttackRechargeMultiplier => Config.attackRechargeMultiplier;
        public override float MoveSpeedMultiplier => Config.moveMultiplier;
        public override float DashCostMultiplier => Config.dashCostMultiplier;
        public override float DashImpulseMultiplier => Config.dashRangeMultiplier;
    }
}
