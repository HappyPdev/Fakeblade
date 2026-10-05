using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Hielo (GDD 5). Cualquier choque en el que quite RPM al rival lo congela: se mueve más lento
    /// y recarga los ataques más despacio. Un golpe nuevo reinicia la duración; no se acumula.
    /// Las reglas entre estados (una peonza en llamas no se congela) las aplica StatusEffectSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "Ice", menuName = "FakeBlade/Specials/Hielo")]
    public class IceData : SpecialAbilityData
    {
        [Header("Congelación")]
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
        public IceAbility(IceData data, FakeBladeController owner) : base(data, owner) { }

        public override void OnClashDamageDealt(FakeBladeController target, float damage)
        {
            target.TryFreeze(Owner, Config.freezeMoveMultiplier, Config.freezeRechargeMultiplier, Config.freezeDuration);
        }
    }
}
