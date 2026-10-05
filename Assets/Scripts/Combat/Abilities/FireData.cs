using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Fuego (GDD 5; sustituye a Rastro de fuego). Cualquier choque en el que quite RPM al rival
    /// lo quema: pierde un porcentaje de sus RPM máximas cada pocos segundos. Un golpe nuevo
    /// reinicia la duración; no se acumula. Las reglas entre estados las aplica StatusEffectSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "Fire", menuName = "FakeBlade/Specials/Fuego")]
    public class FireData : SpecialAbilityData
    {
        [Header("Quemadura")]
        [Tooltip("Fracción de las RPM máximas del rival que pierde en cada tic (0,015 = 1,5%)")]
        [Range(0f, 0.2f)] public float burnDamagePct = 0.015f;
        [Tooltip("Segundos entre tics")]
        [Min(0.05f)] public float burnTickInterval = 0.5f;
        [Tooltip("Segundos que dura la quemadura")]
        [Min(0.1f)] public float burnDuration = 3f;

        public override SpecialAbilityType Type => SpecialAbilityType.Fire;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new FireAbility(this, owner);
    }

    public sealed class FireAbility : SpecialAbility<FireData>
    {
        public FireAbility(FireData data, FakeBladeController owner) : base(data, owner) { }

        public override void OnClashDamageDealt(FakeBladeController target, float damage)
        {
            target.TryBurn(Owner, Config.burnDamagePct, Config.burnTickInterval, Config.burnDuration);
        }
    }
}
