using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Rayos (GDD 5; sustituye a Dash eléctrico). Modo cargado durante el poder (hasta 6 s):
    /// - Choque pequeño (va hacia el rival a poca velocidad): empuja fuerte y no gasta el poder.
    /// - Choque fuerte (va hacia el rival a la velocidad mínima o más): más daño, mucho más empuje,
    ///   deja al rival lanzado y gasta el poder, con un destello.
    /// - Contra una peonza congelada es un choque normal y no se gasta (Hielo &gt; Rayos).
    /// Solo cuenta su propia velocidad hacia el rival: si la embisten estando quieta, choque normal.
    /// </summary>
    [CreateAssetMenu(fileName = "Lightning", menuName = "FakeBlade/Specials/Rayos")]
    public class LightningData : SpecialAbilityData
    {
        [Header("Choque pequeño")]
        [Tooltip("Velocidad mínima hacia el rival (m/s) para que el choque tenga efecto")]
        [Min(0f)] public float smallMinSpeed = 1f;
        [Tooltip("Multiplicador del empuje que provoca (sin daño extra ni lanzada)")]
        public float smallKnockbackMultiplier = 1.8f;

        [Header("Choque fuerte")]
        [Tooltip("Velocidad mínima hacia el rival (m/s) para el golpe fuerte")]
        [Min(0f)] public float strongMinSpeed = 6f;
        [Tooltip("Multiplicador del daño que hace (1,2 = +20%)")]
        public float strongDamageMultiplier = 1.2f;
        [Tooltip("Multiplicador del empuje que provoca")]
        public float strongKnockbackMultiplier = 2.5f;
        [Tooltip("Segundos que el rival queda lanzado")]
        [Min(0.05f)] public float launchDuration = 0.5f;

        [Header("Destello del golpe fuerte")]
        [Min(0f)] public float strikeHitStop = 0.06f;
        [Min(0f)] public float strikeShake = 0.25f;

        public override SpecialAbilityType Type => SpecialAbilityType.Lightning;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new LightningAbility(this, owner);
    }

    public sealed class LightningAbility : SpecialAbility<LightningData>
    {
        private const int StrikeBolts = 6;

        public LightningAbility(LightningData data, FakeBladeController owner) : base(data, owner) { }

        public override ClashBonus GetClashBonus(FakeBladeController target, float approachSpeed)
        {
            if (approachSpeed < Config.smallMinSpeed || target.Status.IsFrozen) return ClashBonus.None;

            return approachSpeed >= Config.strongMinSpeed
                ? new ClashBonus(Config.strongDamageMultiplier, Config.strongKnockbackMultiplier, true)
                : new ClashBonus(1f, Config.smallKnockbackMultiplier, false);
        }

        public override void OnClashBonusApplied(FakeBladeController target, ClashBonus bonus, Vector3 contactPoint)
        {
            // Si no se puede lanzar (invulnerable, fuera de combate) no se gasta el poder
            if (!bonus.Strong || !target.TryLaunch(Owner, Config.launchDuration)) return;

            PlayStrike(contactPoint);
            Owner.EndSpecial();
        }

        private void PlayStrike(Vector3 point)
        {
            Color color = Data.color;
            VfxSystem.Play(VfxType.SpecialBurst, point, Vector3.up, color, 2f);
            VfxSystem.Play(VfxType.Sparkle, point, Vector3.up, Color.white, 1.5f);
            for (int i = 0; i < StrikeBolts; i++)
            {
                Vector3 p = point + Random.insideUnitSphere * 0.8f;
                // Giros de 90º para que el rayo pixel no se deforme
                VfxSystem.EmitParticle(VfxType.AuraLightning, p, Vector3.zero, i % 2 == 0 ? color : Color.white,
                    Random.Range(0.5f, 0.8f), 0.15f, 90f * Random.Range(0, 4));
            }
            CameraShake.Shake(Config.strikeShake, 0.15f);
            HitStop.Trigger(Config.strikeHitStop);
        }
    }
}
