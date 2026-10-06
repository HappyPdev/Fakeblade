using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>Spin Boost (GDD 5): recupera RPM poco a poco mientras está activo.</summary>
    [CreateAssetMenu(fileName = "SpinBoost", menuName = "FakeBlade/Specials/Spin Boost")]
    public class SpinBoostData : SpecialAbilityData
    {
        [Header("Spin Boost")]
        [Tooltip("RPM recuperadas por segundo mientras está activo (fracción de las RPM máximas)")]
        [Range(0f, 0.5f)] public float regenPctPerSecond = 0.04f;

        public override SpecialAbilityType Type => SpecialAbilityType.SpinBoost;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new SpinBoostAbility(this, owner);
    }

    public sealed class SpinBoostAbility : SpecialAbility<SpinBoostData>
    {
        public SpinBoostAbility(SpinBoostData data, FakeBladeController owner) : base(data, owner) { }

        public override bool Heals => true;

        // Por el sistema de poderes: un golpe de ataque enemigo corta la regeneración
        public override void Tick(float dt) => Owner.Special.Heal(Owner.MaxSpinSpeed * Config.regenPctPerSecond * dt);
    }
}
