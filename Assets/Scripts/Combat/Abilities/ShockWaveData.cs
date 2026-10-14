using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Onda de choque (GDD 5; rehecha en F1, 2026-10-13). Tres ondas que lanza el jugador: la primera al
    /// activarlo y las demás pulsando Especial otra vez mientras dura. Cada onda quita RPM y empuja fuerte a
    /// las peonzas cercanas (menos en el borde) y las deja lanzadas un momento: si chocan contra la pared,
    /// la pared les hace daño, como con Rayos. Con la última onda el poder termina.
    /// </summary>
    [CreateAssetMenu(fileName = "ShockWave", menuName = "FakeBlade/Specials/Onda de choque")]
    public class ShockWaveData : SpecialAbilityData
    {
        [Header("Ondas")]
        [Tooltip("Ondas en total (la primera sale al activarlo; las demás, pulsando Especial)")]
        [Min(1)] public int waves = 3;
        [Tooltip("Radio de cada onda (m)")]
        public float radius = 5f;
        [Tooltip("Lo que queda del efecto en el borde (0,5 = la mitad que en el centro)")]
        [Range(0f, 1f)] public float edgeFactor = 0.5f;

        [Header("Cada onda")]
        [Tooltip("Empuje (m/s) en el centro")]
        public float force = 14f;
        [Tooltip("Daño en el centro (fracción de las RPM máximas del objetivo)")]
        [Range(0f, 1f)] public float damagePct = 0.06f;
        [Tooltip("Segundos que quedan lanzadas: sin control y, si chocan contra la pared, con daño de pared (0 = no se lanzan)")]
        [Min(0f)] public float launchDuration = 0.4f;

        [Header("Efecto de cada onda")]
        [Min(0f)] public float waveShake = 0.15f;

        public override SpecialAbilityType Type => SpecialAbilityType.ShockWave;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new ShockWaveAbility(this, owner);
    }

    public sealed class ShockWaveAbility : SpecialAbility<ShockWaveData>
    {
        private int _wavesLeft;

        public ShockWaveAbility(ShockWaveData data, FakeBladeController owner) : base(data, owner) { }

        /// <summary>La IA la usa con el rival dentro de la parte fuerte de la onda.</summary>
        public override float UseRange => Config.radius * 0.6f;

        public override bool WantsRepeatPress => _wavesLeft > 0;

        /// <summary>Ondas que le quedan por lanzar.</summary>
        public int WavesLeft => _wavesLeft;
        /// <summary>Radio de cada onda (m).</summary>
        public float Radius => Config.radius;

        public override void OnActivate()
        {
            _wavesLeft = Mathf.Max(0, Config.waves - 1);
            Wave();
        }

        public override bool OnPressedWhileActive()
        {
            if (_wavesLeft <= 0) return false;
            _wavesLeft--;
            Wave();
            VfxSystem.Play(VfxType.SpecialBurst, Owner.Position, Vector3.up, Data.color, Data.activationBurst);
            CameraShake.Shake(Config.waveShake, 0.1f);
            // La última onda gasta el poder
            if (_wavesLeft == 0) Owner.EndSpecial();
            return true;
        }

        public override void OnEnd() => _wavesLeft = 0;

        private void Wave()
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
                float falloff = Mathf.Lerp(1f, Config.edgeFactor, distance / radius);
                Vector3 dir = distance > 0.001f ? offset / distance : Owner.Facing;

                other.ApplyKnockback(dir * Config.force * falloff * other.KnockbackResistance);
                other.ApplyDamage(other.MaxSpinSpeed * Config.damagePct * falloff, Owner);
                // Después del empuje: la lanzada cuenta ya con él para no frenarla en el tope de velocidad
                if (Config.launchDuration > 0f) other.TryLaunch(Owner, Config.launchDuration);
            }
        }
    }
}
