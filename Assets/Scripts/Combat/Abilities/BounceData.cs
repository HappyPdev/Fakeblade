using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Rebote (GDD 5, Fase L, L2; decidido el 2026-10-13). Un «parry pasivo»: mientras dura, cualquier peonza que
    /// la embista (que vaya hacia ella a smallMinSpeed o más: ataque, dash o empujón) sale despedida con el empuje
    /// entero ×1,8 y recibe el daño normal del choque, y ella no pierde RPM en ese choque. Sus propios ataques
    /// hacen y cuestan lo de siempre. Convive con Defensa: Rebote castiga al que la embiste; Defensa solo aguanta.
    /// </summary>
    [CreateAssetMenu(fileName = "Bounce", menuName = "FakeBlade/Specials/Rebote")]
    public class BounceData : SpecialAbilityData
    {
        [Header("Rebote")]
        [Tooltip("Velocidad mínima de la otra peonza hacia ella (m/s) para que el choque rebote")]
        [Min(0f)] public float minAttackerSpeed = 1f;
        [Tooltip("Multiplicador del empuje que se lleva quien la embiste (decidido: x1,8, como un choque pequeño de Rayos)")]
        public float knockbackMultiplier = 1.8f;
        [Tooltip("Daño que recibe ella en esos choques (0 = ninguno)")]
        [Range(0f, 1f)] public float damageTakenMultiplier = 0f;

        public override SpecialAbilityType Type => SpecialAbilityType.Bounce;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new BounceAbility(this, owner);
    }

    public sealed class BounceAbility : SpecialAbility<BounceData>
    {
        public BounceAbility(BounceData data, FakeBladeController owner) : base(data, owner) { }

        public override ClashGuard GetClashGuard(FakeBladeController attacker, float attackerApproachSpeed) =>
            attackerApproachSpeed >= Config.minAttackerSpeed
                ? new ClashGuard(Config.damageTakenMultiplier, Config.knockbackMultiplier)
                : ClashGuard.None;

        public override void OnClashGuardApplied(FakeBladeController attacker, Vector3 contactPoint)
        {
            VfxSystem.Play(VfxType.Parry, contactPoint, Vector3.up, Data.color, 0.8f);
            VfxSystem.EmitParticle(VfxType.ChargeRing, contactPoint, Vector3.zero, Data.color, 1.2f);
        }
    }
}
