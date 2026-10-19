using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Onda de choque (GDD 5; rehecha en F1, 2026-10-13, y en K8, 2026-10-18). Dos ondas que lanza el jugador: la primera
    /// al activarlo y la otra pulsando Especial otra vez mientras dura. Cada onda tiene su carga (se frena, no ataca ni
    /// hace dash y la cúpula crece sobre la peonza) y al acabarla explota: quita RPM y empuja fuerte a las peonzas
    /// cercanas (menos en el borde) y las deja lanzadas un momento; si chocan contra la pared, la pared les hace daño,
    /// como con Rayos. Con la última onda el poder termina.
    /// </summary>
    [CreateAssetMenu(fileName = "ShockWave", menuName = "FakeBlade/Specials/Onda de choque")]
    public class ShockWaveData : SpecialAbilityData
    {
        [Header("Ondas")]
        [Tooltip("Ondas en total (la primera sale al activarlo; las demás, pulsando Especial)")]
        [Min(1)] public int waves = 2;
        [Tooltip("Radio de cada onda (m)")]
        public float radius = 5f;
        [Tooltip("Lo que queda del efecto en el borde (0,5 = la mitad que en el centro)")]
        [Range(0f, 1f)] public float edgeFactor = 0.5f;

        [Header("Cada onda")]
        [Tooltip("Empuje (m/s) en el centro")]
        public float force = 20f;
        [Tooltip("Daño en el centro (fracción de las RPM máximas del objetivo)")]
        [Range(0f, 1f)] public float damagePct = 0.06f;
        [Tooltip("Segundos que quedan lanzadas: sin control y, si chocan contra la pared, con daño de pared (0 = no se lanzan)")]
        [Min(0f)] public float launchDuration = 0.5f;

        [Header("Efecto de cada onda")]
        [Min(0f)] public float waveShake = 0.15f;

        public override SpecialAbilityType Type => SpecialAbilityType.ShockWave;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new ShockWaveAbility(this, owner);
    }

    public sealed class ShockWaveAbility : SpecialAbility<ShockWaveData>
    {
        // Ondas por lanzar, contando la que se está cargando
        private int _wavesLeft;
        private float _windup;

        public ShockWaveAbility(ShockWaveData data, FakeBladeController owner) : base(data, owner) { }

        /// <summary>La IA la usa con el rival dentro de la parte fuerte de la onda.</summary>
        public override float UseRange => Config.radius * 0.6f;

        public override bool WantsRepeatPress => _wavesLeft > 0 && !IsWindingUp;

        public override bool IsWindingUp => _windup > 0f;
        /// <summary>La carga la lleva el propio poder (no la común de SpecialAbilitySystem).</summary>
        public override bool HandlesWindup => true;
        public override float WindupProgress => Config.windupTime > 0f ? 1f - Mathf.Clamp01(_windup / Config.windupTime) : 1f;
        public override bool BlocksActions => IsWindingUp;

        public override int MaxUses => Mathf.Max(1, Config.waves);
        public override int UsesLeft => _wavesLeft;
        public override float RepeatWindup => Config.windupTime;

        /// <summary>Ondas que le quedan por lanzar (contando la que se está cargando).</summary>
        public int WavesLeft => _wavesLeft;
        /// <summary>Radio de cada onda (m).</summary>
        public float Radius => Config.radius;

        public override void OnActivate()
        {
            _wavesLeft = Mathf.Max(1, Config.waves);
            StartWave();
        }

        public override bool OnPressedWhileActive()
        {
            // Mientras carga una onda, otra pulsación no hace nada
            if (_wavesLeft <= 0 || IsWindingUp) return false;
            StartWave();
            return true;
        }

        public override void OnEnd()
        {
            _wavesLeft = 0;
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
            if (_windup <= 0f) Release();
        }

        private void StartWave()
        {
            _windup = Config.windupTime;
            if (_windup <= 0f) Release();
        }

        /// <summary>Acaba la carga: la onda explota. Con la última, el poder termina.</summary>
        private void Release()
        {
            _windup = 0f;
            _wavesLeft--;
            Wave();
            VfxSystem.Play(VfxType.SpecialBurst, Owner.Position, Vector3.up, Data.color, Data.activationBurst);
            CameraShake.Shake(Config.waveShake, 0.1f);
            if (_wavesLeft <= 0) Owner.EndSpecial();
        }

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
                BladeSpecialFx.PlayPushHit(Owner, other, dir);
            }
        }
    }
}
