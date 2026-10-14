using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Salto (GDD 5, Fase L, L5; decidido el 2026-10-13). Primero se carga 0,3 s (se frena hasta quedarse quieta;
    /// mientras, se apunta con el stick) y después salta unos 0,9 m de alto y 5 m hacia el stick (sin stick, hacia el
    /// rival más cercano). En el aire, si choca con otras peonzas las empuja sin daño para nadie, y no se le puede
    /// hacer parry ni bloquearla. Al caer hace una onda pequeña y fuerte: 10% de las RPM en el centro (la mitad en el
    /// borde de 3 m), empuje de 22 m/s y las deja lanzadas (daño de pared si chocan). Un salto por activación: al
    /// caer, el poder termina.
    /// </summary>
    [CreateAssetMenu(fileName = "Leap", menuName = "FakeBlade/Specials/Salto")]
    public class LeapData : SpecialAbilityData
    {
        [Header("Carga")]
        [Tooltip("Segundos que se carga antes de saltar (se frena; la dirección se toma al despegar)")]
        [Min(0f)] public float windupTime = 0.3f;
        [Tooltip("Frenado durante la carga (fracción de la velocidad que pierde por segundo)")]
        public float windupBrake = 12f;

        [Header("Salto")]
        [Tooltip("Distancia horizontal del salto (m, nominal: la fricción al subir la acorta un poco; 5,3 ≈ 5 m reales)")]
        public float distance = 5.3f;
        [Tooltip("Altura del salto (m, nominal: la fricción del aire la recorta; 1,1 ≈ 0,9 m reales, el doble del salto bajo inicial)")]
        [Min(0.1f)] public float height = 1.1f;
        [Tooltip("Empuje que recibe en el aire al chocar (para que el choque no le corte el salto)")]
        [Range(0f, 1f)] public float knockbackTakenInAir = 0.2f;

        [Header("Onda al caer")]
        [Tooltip("Radio de la onda (m)")]
        public float radius = 3f;
        [Tooltip("Lo que queda del efecto en el borde (0,5 = la mitad que en el centro)")]
        [Range(0f, 1f)] public float edgeFactor = 0.5f;
        [Tooltip("Daño en el centro (fracción de las RPM máximas del objetivo)")]
        [Range(0f, 1f)] public float damagePct = 0.1f;
        [Tooltip("Empuje (m/s) en el centro")]
        public float force = 22f;
        [Tooltip("Segundos que quedan lanzadas (daño de pared si chocan)")]
        [Min(0f)] public float launchDuration = 0.4f;
        [Min(0f)] public float landShake = 0.25f;
        [Min(0f)] public float landHitStop = 0.05f;

        public override SpecialAbilityType Type => SpecialAbilityType.Leap;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new LeapAbility(this, owner);
    }

    public sealed class LeapAbility : SpecialAbility<LeapData>
    {
        private float _elapsed;
        private float _airTime;
        private float _windup;
        private bool _flying;
        private Vector3 _landing;

        public LeapAbility(LeapData data, FakeBladeController owner) : base(data, owner) { }

        /// <summary>La IA salta con el rival a la distancia del salto: ni tan cerca que se pase de largo ni fuera de la onda.</summary>
        public override float UseRange => Config.distance + Config.radius * 0.7f;
        public override float UseRangeMin => Config.distance - Config.radius * 0.7f;

        /// <summary>Segundos de carga que le quedan (0 si ya ha saltado).</summary>
        public float WindupLeft => _windup;
        /// <summary>Dónde caerá (en el aire; lo que recorre el salto desde el despegue). Para la IA del rival.</summary>
        public Vector3 LandingPoint => _landing;
        /// <summary>Segundos que le quedan en el aire (aproximado).</summary>
        public float TimeToLand => _flying ? Mathf.Max(0f, _airTime - _elapsed) : 0f;
        /// <summary>Distancia horizontal del salto (m, nominal).</summary>
        public float JumpDistance => Config.distance;
        /// <summary>Radio de la onda al caer (m).</summary>
        public float WaveRadius => Config.radius;
        /// <summary>Segundos en el aire de un salto (sale de la altura).</summary>
        public float AirTime => 2f * Mathf.Sqrt(2f * Config.height / -Physics.gravity.y);

        /// <summary>Ni en la carga ni en el aire se puede atacar o hacer dash (no cambia la dirección del salto).</summary>
        public override bool BlocksActions => true;

        public bool IsWindingUp => _windup > 0f;
        public bool IsFlying => _flying;
        /// <summary>Lo que lleva de carga (0-1), para el aspecto.</summary>
        public float WindupProgress => Config.windupTime > 0f ? 1f - Mathf.Clamp01(_windup / Config.windupTime) : 1f;

        public override void OnActivate()
        {
            _flying = false;
            _windup = Config.windupTime;
            if (_windup <= 0f) TakeOff();
        }

        public override void Tick(float dt)
        {
            if (_windup > 0f)
            {
                // Carga: se frena hasta quedarse quieta
                Vector3 v = Owner.Velocity;
                v.y = 0f;
                Owner.ApplyPull(-v * Mathf.Min(1f, Config.windupBrake * dt));
                _windup -= dt;
                if (_windup <= 0f) TakeOff();
                return;
            }

            if (!_flying) return;
            _elapsed += dt;
            // Aterriza al dejar de caer pasada la mitad del vuelo (con un tope por si se queda encima de algo)
            bool landed = _elapsed > _airTime * 0.6f && Owner.Velocity.y > -0.3f;
            if (landed || _elapsed > _airTime + 0.4f) Land();
        }

        /// <summary>Despega hacia el stick en ese momento (se apunta durante la carga).</summary>
        private void TakeOff()
        {
            _windup = 0f;
            Vector3 dir = Owner.ActionDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Owner.Facing;
            dir.Normalize();

            // Subida para llegar a la altura y tiempo en el aire que sale de ella; velocidad para recorrer la distancia
            // en ese tiempo contando la fricción del aire del Rigidbody (v·(1 − e^(−k·t))/k = distancia)
            float g = -Physics.gravity.y;
            float up = Mathf.Sqrt(2f * g * Config.height);
            _airTime = 2f * up / g;
            float k = CombatConfig.Active.linearDamping;
            float speed = k > 0.001f ? Config.distance * k / (1f - Mathf.Exp(-k * _airTime)) : Config.distance / _airTime;
            Owner.Leap(dir * speed, up, _airTime);
            _landing = Owner.Position + dir * Config.distance;
            _landing.y = Owner.Position.y;
            _elapsed = 0f;
            _flying = true;
        }

        public override void OnEnd()
        {
            _flying = false;
            _windup = 0f;
        }

        // En el aire: los choques empujan sin daño para nadie, y casi no le desvían el salto
        public override ClashGuard GetClashGuard(FakeBladeController attacker, float attackerApproachSpeed) =>
            _flying ? new ClashGuard(0f, 1f, 0f) : ClashGuard.None;

        public override float KnockbackTakenMultiplier => _flying ? Config.knockbackTakenInAir : 1f;

        private void Land()
        {
            _flying = false;
            Vector3 origin = Owner.Position;
            float radius = Config.radius;
            var blades = FakeBladeController.ActiveBlades;
            for (int i = 0; i < blades.Count; i++)
            {
                FakeBladeController other = blades[i];
                if (other == Owner || other.IsDestroyed || !Owner.CanHarm(other)) continue;

                Vector3 offset = other.Position - origin;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > radius) continue;

                float falloff = Mathf.Lerp(1f, Config.edgeFactor, distance / radius);
                Vector3 dir = distance > 0.001f ? offset / distance : Owner.Facing;
                other.ApplyKnockback(dir * (Config.force * falloff * other.KnockbackResistance));
                other.ApplyDamage(other.MaxSpinSpeed * Config.damagePct * falloff, Owner);
                if (Config.launchDuration > 0f) other.TryLaunch(Owner, Config.launchDuration);
            }

            VfxSystem.Play(VfxType.SpecialBurst, origin, Vector3.up, Data.color, 2f);
            VfxSystem.EmitParticle(VfxType.AuraShockWave, origin, Vector3.zero, Data.color, radius * 2f);
            VfxSystem.Play(VfxType.WallHit, origin, Vector3.up, Color.white, 1.5f);
            CameraShake.Shake(Config.landShake, 0.15f);
            HitStop.Trigger(Config.landHitStop);
            Owner.EndSpecial();
        }
    }
}
