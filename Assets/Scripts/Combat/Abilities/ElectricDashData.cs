using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Dash eléctrico (GDD 5): dashes más largos casi sin cooldown y ataques cargados más rápidos.
    /// Lo sustituirá el poder Rayos (Quehaceres A5/C4).
    /// </summary>
    [CreateAssetMenu(fileName = "ElectricDash", menuName = "FakeBlade/Specials/Dash eléctrico")]
    public class ElectricDashData : SpecialAbilityData
    {
        [Header("Dash eléctrico")]
        [Tooltip("Multiplicador del cooldown del dash (0,3 = un 30% del normal)")]
        [Range(0f, 1f)] public float dashCooldownMultiplier = 0.3f;
        [Tooltip("Multiplicador del impulso (alcance) del dash")]
        public float dashImpulseMultiplier = 1.5f;
        [Tooltip("Multiplicador de la velocidad de carga del ataque cargado")]
        public float chargeSpeedMultiplier = 2f;

        public override SpecialAbilityType Type => SpecialAbilityType.Dash;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new ElectricDashAbility(this, owner);
    }

    public sealed class ElectricDashAbility : SpecialAbility<ElectricDashData>
    {
        public ElectricDashAbility(ElectricDashData data, FakeBladeController owner) : base(data, owner) { }

        public override float DashCooldownMultiplier => Config.dashCooldownMultiplier;
        public override float DashImpulseMultiplier => Config.dashImpulseMultiplier;
        public override float ChargeSpeedMultiplier => Config.chargeSpeedMultiplier;
    }
}
