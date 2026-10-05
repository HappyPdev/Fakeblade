using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>Onda de choque (GDD 5): al activarse empuja y quita RPM a las peonzas cercanas.</summary>
    [CreateAssetMenu(fileName = "ShockWave", menuName = "FakeBlade/Specials/Onda de choque")]
    public class ShockWaveData : SpecialAbilityData
    {
        [Header("Onda de choque")]
        [Tooltip("Radio de la onda (m). El efecto baja con la distancia hasta 0 en el borde")]
        public float radius = 5f;
        [Tooltip("Empuje (m/s) a distancia 0")]
        public float force = 12f;
        [Tooltip("Daño a distancia 0 (fracción de las RPM máximas del objetivo)")]
        [Range(0f, 1f)] public float damagePct = 0.1f;

        public override SpecialAbilityType Type => SpecialAbilityType.ShockWave;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new ShockWaveAbility(this, owner);
    }

    public sealed class ShockWaveAbility : SpecialAbility<ShockWaveData>
    {
        public ShockWaveAbility(ShockWaveData data, FakeBladeController owner) : base(data, owner) { }

        public override void OnActivate()
        {
            float radius = Config.radius;
            float radiusSqr = radius * radius;
            Vector3 origin = Owner.Position;
            var blades = FakeBladeController.ActiveBlades;

            for (int i = 0; i < blades.Count; i++)
            {
                FakeBladeController other = blades[i];
                if (other == Owner || other.IsDestroyed || !Owner.CanHarm(other)) continue;

                Vector3 offset = other.Position - origin;
                offset.y = 0f;
                float distSqr = offset.sqrMagnitude;
                if (distSqr > radiusSqr) continue;

                float distance = Mathf.Sqrt(distSqr);
                float falloff = 1f - distance / radius;
                Vector3 dir = distance > 0.001f ? offset / distance : Owner.Facing;

                other.ApplyKnockback(dir * Config.force * falloff * other.KnockbackResistance);
                other.ApplyDamage(other.MaxSpinSpeed * Config.damagePct * falloff, Owner);
            }
        }
    }
}
