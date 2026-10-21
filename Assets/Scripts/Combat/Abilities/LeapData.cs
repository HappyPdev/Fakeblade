using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Salto (GDD 5, Fase L, L5; decidido el 2026-10-13). Primero se carga 0,3 s (se frena hasta quedarse quieta;
    /// mientras, se apunta con el stick) y después salta unos 2 m de alto y 5 m hacia el stick (sin stick, hacia el
    /// rival más cercano), con 6 veces la gravedad para que dure unos 0,55 s en el aire (2026-10-15, tras probarlo con
    /// mando: un salto exagerado y rápido). En el aire, si choca con otras peonzas las empuja sin daño para nadie, y no
    /// se le puede hacer parry ni bloquearla. Al caer hace una onda fuerte: 20% de las RPM en el centro (el 14% en el borde
    /// de 4,5 m; 2026-10-21, antes 10% en 3 m y la mitad en el borde: costaba mucho acertar y, con el ×0,5 global y la
    /// defensa, el golpe de lleno se quedaba en ~15), empuje de 22 m/s y las deja lanzadas (daño de pared si chocan). Un salto por activación: al caer, el poder termina. Efectos (K8, 2026-10-21): VfxLibrary →
    /// Special Effects → Leap. Vibra mientras vuela y fuerte al caer.
    /// </summary>
    [CreateAssetMenu(fileName = "Leap", menuName = "FakeBlade/Specials/Salto")]
    public class LeapData : SpecialAbilityData
    {
        [Header("Salto")]
        [Tooltip("Distancia horizontal del salto (m, nominal: la fricción al subir la acorta un poco; 5,3 ≈ 5 m reales)")]
        public float distance = 5.3f;
        [Tooltip("Altura del salto (m, nominal: la fricción del aire la recorta un poco)")]
        [Min(0.1f)] public float height = 2.2f;
        [Tooltip("Gravedad en el aire (×): más gravedad, misma altura y distancia en menos tiempo. Tiempo en el aire = 2·√(2·altura / (9,81·esto))")]
        [Min(1f)] public float gravityScale = 6f;
        [Tooltip("Empuje que recibe en el aire al chocar (para que el choque no le corte el salto)")]
        [Range(0f, 1f)] public float knockbackTakenInAir = 0.2f;

        [Header("Onda al caer")]
        [Tooltip("Radio de la onda (m)")]
        public float radius = 4.5f;
        [Tooltip("Lo que queda del efecto en el borde (0,7 = el 70% del centro)")]
        [Range(0f, 1f)] public float edgeFactor = 0.7f;
        [Tooltip("Daño en el centro (fracción de las RPM máximas del objetivo)")]
        [Range(0f, 1f)] public float damagePct = 0.2f;
        [Tooltip("Empuje (m/s) en el centro")]
        public float force = 22f;
        [Tooltip("Segundos que quedan lanzadas (daño de pared si chocan)")]
        [Min(0f)] public float launchDuration = 0.4f;
        [Min(0f)] public float landShake = 0.25f;
        [Min(0f)] public float landHitStop = 0.05f;

        [Header("Vibración del mando (2026-10-21)")]
        [Tooltip("Vibración mientras vuela: al despegar y justo antes de caer (0-1; sube poco a poco)")]
        [Range(0f, 1f)] public float flightRumbleStart = 0.2f;
        [Range(0f, 1f)] public float flightRumbleEnd = 0.55f;
        [Tooltip("Golpe al caer: segundos con la fuerza completa (más fuerte que la carga)")]
        [Min(0f)] public float landRumbleTime = 0.45f;

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
        public float AirTime => 2f * Mathf.Sqrt(2f * Config.height / Gravity);

        /// <summary>Gravedad en el aire del salto (m/s²).</summary>
        private float Gravity => -Physics.gravity.y * Config.gravityScale;

        /// <summary>Ni en la carga ni en el aire se puede atacar o hacer dash (no cambia la dirección del salto).</summary>
        public override bool BlocksActions => true;

        public override bool IsWindingUp => _windup > 0f;
        /// <summary>La carga la lleva el propio poder (no la común de SpecialAbilitySystem).</summary>
        public override bool HandlesWindup => true;
        public bool IsFlying => _flying;
        /// <summary>Mientras vuela: su estela en espiral y los cuencos delante (efectos de la arrancada, K8).</summary>
        public override bool IsRushing => _flying;
        /// <summary>Mientras vuela, el mando vibra cada vez más hasta caer.</summary>
        public override float SustainedRumble => _flying && _airTime > 0f
            ? Mathf.Lerp(Config.flightRumbleStart, Config.flightRumbleEnd, _elapsed / _airTime)
            : 0f;

        public override bool TryGetTargetPoint(out Vector3 point, out float timeLeft)
        {
            point = _landing;
            timeLeft = TimeToLand;
            return _flying;
        }
        /// <summary>Lo que lleva de carga (0-1), para el aspecto.</summary>
        public override float WindupProgress => Config.windupTime > 0f ? 1f - Mathf.Clamp01(_windup / Config.windupTime) : 1f;

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
            // Aterriza al dejar de caer pasada la mitad del vuelo, ya cerca del suelo o encima de otra peonza (2026-10-21: al
            // chocar con la pared en lo alto se paraba un momento y caía en el aire; y cayendo de lleno sobre el rival tiene que
            // contar enseguida, o lo empuja lejos antes de la onda), con un tope por si se queda encima de algo
            bool landed = _elapsed > _airTime * 0.6f && Owner.Velocity.y > -0.3f &&
                          (Owner.Position.y < _landing.y + LandHeight || IsOnTopOfBlade());
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
            float g = Gravity;
            float up = Mathf.Sqrt(2f * g * Config.height);
            _airTime = 2f * up / g;
            float k = CombatConfig.Active.linearDamping;
            float speed = k > 0.001f ? Config.distance * k / (1f - Mathf.Exp(-k * _airTime)) : Config.distance / _airTime;
            Owner.Leap(dir * speed, up, _airTime, Config.gravityScale);
            _landing = Owner.Position + dir * LandingDistance(Owner.Position, dir);
            _landing.y = Owner.Position.y;
            _elapsed = 0f;
            _flying = true;
        }

        // Hasta dónde llega sin chocar con la pared: una esfera un poco más pequeña que la peonza, por encima del suelo
        private const float WallProbeRadius = 0.5f;
        private const float WallProbeHeight = 0.7f;
        private const float WallMargin = 0.1f;
        // Altura sobre el suelo del despegue por debajo de la que cuenta como caída
        private const float LandHeight = 0.4f;
        // Encima de otra peonza: a menos de esta distancia en horizontal (dos radios de peonza) y más alta que ella
        private const float OnTopRadius = 1.2f;
        private const float OnTopMinHeight = 0.1f;
        private static readonly RaycastHit[] s_wallHits = new RaycastHit[8];

        /// <summary>
        /// Distancia del salto recortada si hay pared antes (2026-10-21: cerca del borde, la marca de caída salía fuera de la
        /// arena). La peonza choca con la pared y cae junto a ella. No cuentan las peonzas, que en el aire solo se empujan.
        /// </summary>
        private float LandingDistance(Vector3 from, Vector3 dir)
        {
            float distance = Config.distance;
            Vector3 origin = from + Vector3.up * WallProbeHeight;
            int count = Physics.SphereCastNonAlloc(origin, WallProbeRadius, dir, s_wallHits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_wallHits[i];
                if (hit.distance <= 0f) continue; // ya tocando al empezar (su propio collider o el suelo)
                if (hit.rigidbody != null && hit.rigidbody.TryGetComponent(out FakeBladeController _)) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - WallMargin));
            }
            return distance;
        }

        /// <summary>Está encima de otra peonza (más alta y casi en su vertical): ha caído sobre ella.</summary>
        private bool IsOnTopOfBlade()
        {
            Vector3 pos = Owner.Position;
            var blades = FakeBladeController.ActiveBlades;
            for (int i = 0; i < blades.Count; i++)
            {
                FakeBladeController other = blades[i];
                if (other == Owner || other.IsDestroyed) continue;
                Vector3 offset = pos - other.Position;
                if (offset.y > OnTopMinHeight && offset.x * offset.x + offset.z * offset.z < OnTopRadius * OnTopRadius) return true;
            }
            return false;
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
                BladeSpecialFx.PlayPushHit(Owner, other, dir);
                other.RequestRumble(1f, 0.7f, 0.25f);
            }
            Owner.RequestRumble(1f, 1f, Config.landRumbleTime);

            // Con efectos propios al caer (K8, 2026-10-21), en vez del estallido común y su onda pixelada. A la altura del suelo
            // del despegue: al aterrizar se hunde un momento (6 veces la gravedad) o queda encima del rival, y los del suelo
            // quedarían debajo o flotando
            Vector3 tip = Owner.TipPosition;
            tip.y += _landing.y - origin.y; // también si ha caído encima del rival
            if (!BladeSpecialFx.PlayRelease(Owner, tip, ArenaDefinition.MaxWidthAt(tip)))
            {
                VfxSystem.Play(VfxType.SpecialBurst, origin, Vector3.up, Data.color, 2f);
                VfxSystem.EmitParticle(VfxType.AuraShockWave, origin, Vector3.zero, Data.color, radius * 2f);
                VfxSystem.Play(VfxType.WallHit, origin, Vector3.up, Color.white, 1.5f);
            }
            CameraShake.Shake(Config.landShake, 0.15f);
            HitStop.Trigger(Config.landHitStop);
            Owner.EndSpecial();
        }
    }
}
