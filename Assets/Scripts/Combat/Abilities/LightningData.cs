using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Rayos (GDD 5; sustituye a Dash eléctrico).
    /// De momento conserva el efecto de Dash eléctrico: dashes más largos casi sin cooldown y
    /// ataques cargados más rápidos. Su efecto propio (modo cargado, golpe fuerte que empuja
    /// y deja lanzado al rival) llega en Quehaceres C4.
    /// </summary>
    [CreateAssetMenu(fileName = "Lightning", menuName = "FakeBlade/Specials/Rayos")]
    public class LightningData : SpecialAbilityData
    {
        [Header("Rayos")]
        [Tooltip("Multiplicador del cooldown del dash (0,3 = un 30% del normal)")]
        [Range(0f, 1f)] public float dashCooldownMultiplier = 0.3f;
        [Tooltip("Multiplicador del impulso (alcance) del dash")]
        public float dashImpulseMultiplier = 1.5f;
        [Tooltip("Multiplicador de la velocidad de carga del ataque cargado")]
        public float chargeSpeedMultiplier = 2f;

        public override SpecialAbilityType Type => SpecialAbilityType.Lightning;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new LightningAbility(this, owner);
    }

    public sealed class LightningAbility : SpecialAbility<LightningData>
    {
        public LightningAbility(LightningData data, FakeBladeController owner) : base(data, owner) { }

        public override float DashCooldownMultiplier => Config.dashCooldownMultiplier;
        public override float DashImpulseMultiplier => Config.dashImpulseMultiplier;
        public override float ChargeSpeedMultiplier => Config.chargeSpeedMultiplier;
    }
}
