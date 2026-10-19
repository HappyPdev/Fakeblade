using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Rayos (GDD 5; sustituye a Dash eléctrico). Al activarlo, una carga corta (se frena, como el Fuego y el Hielo;
    /// 2026-10-18) y después, modo cargado durante el poder (hasta 6 s):
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
        private float _windup;

        public LightningAbility(LightningData data, FakeBladeController owner) : base(data, owner) { }

        public override bool IsWindingUp => _windup > 0f;
        /// <summary>La carga la lleva el propio poder (no la común de SpecialAbilitySystem).</summary>
        public override bool HandlesWindup => true;
        public override bool BlocksActions => IsWindingUp;

        public override void OnActivate()
        {
            _windup = Config.windupTime;
        }

        public override void OnEnd()
        {
            _windup = 0f;
        }

        public override void Tick(float dt)
        {
            if (!IsWindingUp) return;
            // Carga: se frena hasta quedarse quieta
            Vector3 v = Owner.Velocity;
            v.y = 0f;
            Owner.ApplyPull(-v * Mathf.Min(1f, Config.windupBrake * dt));
            _windup -= dt;
        }

        public override ClashBonus GetClashBonus(FakeBladeController target, float approachSpeed)
        {
            // En la carga todavía no está cargado: choque normal
            if (IsWindingUp || approachSpeed < Config.smallMinSpeed || target.Status.IsFrozen) return ClashBonus.None;

            return approachSpeed >= Config.strongMinSpeed
                ? new ClashBonus(Config.strongDamageMultiplier, Config.strongKnockbackMultiplier, true)
                : new ClashBonus(1f, Config.smallKnockbackMultiplier, false);
        }

        public override void OnClashBonusApplied(FakeBladeController target, ClashBonus bonus, Vector3 contactPoint)
        {
            // Si no se puede lanzar (invulnerable, fuera de combate) no se gasta el poder
            if (!bonus.Strong || !target.TryLaunch(Owner, Config.launchDuration)) return;

            PlayStrike(contactPoint);
            // Arcos hasta el rival (K8): el poder se gasta aquí, antes de que VfxSystem se entere del choque
            BladeArcs.PlayClash(Owner, target, evenIfEnded: true);
            Owner.EndSpecial();
        }

        private void PlayStrike(Vector3 point)
        {
            // Círculo grande de chispas y ceniza (K8, 2026-10-18): sin las estrellas de cuatro puntas ni los rayitos pixel de
            // antes, que no gustan; los rayos ya los dibujan los arcos (BladeArcs)
            BladeSpecialFx.PlayStrongImpact(Owner, point);
            CameraShake.Shake(Config.strikeShake, 0.15f);
            HitStop.Trigger(Config.strikeHitStop);
        }
    }
}
