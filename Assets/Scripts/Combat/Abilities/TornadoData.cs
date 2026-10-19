using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Tornado (GDD 5, Fase L, L1; decidido el 2026-10-13). Durante 1,5 s atrae hacia sí a las peonzas enemigas que estén
    /// a 6 m o menos (más cuanto más cerca; moviéndose en contra solo se frena) sin que nada la mueva a ella; mientras, las de
    /// dentro quedan atrapadas y ella tampoco puede atacar ni hacer dash (2026-10-19). Después las
    /// expulsa con un empujón que les quita un 25% (la mitad en el borde) y las deja lanzadas (si chocan contra la pared, la
    /// pared les hace daño).
    /// El empujón se carga mientras atrae: pulsando Especial antes sale en ese momento, más flojo; si no se
    /// hace nada, sale al acabar con la fuerza completa. Convive con Onda de choque.
    /// </summary>
    [CreateAssetMenu(fileName = "Tornado", menuName = "FakeBlade/Specials/Tornado")]
    public class TornadoData : SpecialAbilityData
    {
        [Header("Atracción")]
        [Tooltip("Segundos atrayendo antes del empujón (la duración del asset debe ser algo mayor)")]
        [Min(0.1f)] public float pullDuration = 1.5f;
        [Tooltip("Alcance (m)")]
        public float radius = 6f;
        [Tooltip("Aceleración hacia ella (m/s²) en el centro. Una peonza acelera ~24 m/s²: con 40 en el centro no se escapa, solo se frena")]
        public float pullAcceleration = 40f;
        [Tooltip("Lo que queda de la atracción en el borde (0,5 = la mitad)")]
        [Range(0f, 1f)] public float pullEdgeFactor = 0.5f;
        [Tooltip("Más cerca de esto (m, entre centros) ya no atrae: están pegadas")]
        public float minPullDistance = 1f;
        [Tooltip("Empuje que recibe ella mientras atrae (0 = inmune)")]
        [Range(0f, 1f)] public float knockbackTakenWhilePulling = 0f;
        [Tooltip("Multiplicador de su masa física mientras atrae (los choques físicos casi no la mueven)")]
        [Min(1f)] public float massWhilePulling = 50f;
        [Tooltip("Las peonzas dentro del alcance mientras atrae quedan atrapadas: no pueden atacar, cargar ni hacer dash (2026-10-19: " +
                 "si no, la que quedaba pegada le pegaba más de lo que ella le quitaba)")]
        public bool trapsInRange = true;

        [Header("Empujón")]
        [Tooltip("Empuje (m/s) en el centro con la fuerza completa (la mitad en el borde)")]
        public float pushForce = 26f;
        [Tooltip("Daño en el centro (fracción de las RPM máximas del objetivo; la mitad en el borde y menos si se suelta antes). " +
                 "2026-10-19: 25% para probarlo, porque con solo la pared quitaba unos 20 RPM, poco para un poder de un solo uso; la " +
                 "defensa y el multiplicador global lo rebajan: al dummy Balanceado, unos 35 RPM")]
        [Range(0f, 1f)] public float pushDamagePct = 0.25f;
        [Tooltip("Fuerza si se suelta nada más activarlo (sube hasta 1 al acabar la atracción)")]
        [Range(0f, 1f)] public float minPushFraction = 0.5f;
        [Tooltip("Segundos que quedan lanzadas (si chocan contra la pared, la pared les hace daño)")]
        [Min(0f)] public float launchDuration = 0.4f;
        [Min(0f)] public float pushShake = 0.2f;

        public override SpecialAbilityType Type => SpecialAbilityType.Tornado;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new TornadoAbility(this, owner);
    }

    public sealed class TornadoAbility : SpecialAbility<TornadoData>
    {
        /// <summary>Lo que dura cada renovación de la atrapada (s): sale enseguida al soltar el empujón o al salir del alcance.</summary>
        private const float TrapRefresh = 0.1f;

        private float _elapsed;
        // Sin empezar (o en la carga común, L9) cuenta como ya soltado: IsPulling solo mientras atrae
        private bool _pushed = true;

        public TornadoAbility(TornadoData data, FakeBladeController owner) : base(data, owner) { }

        /// <summary>La IA lo activa con el rival dentro del alcance.</summary>
        public override float UseRange => Config.radius * 0.7f;

        /// <summary>Fracción de la atracción que ha pasado (0-1), para el aspecto.</summary>
        public float PullProgress => Mathf.Clamp01(_elapsed / Config.pullDuration);
        /// <summary>Está atrayendo (aún no ha soltado el empujón).</summary>
        public bool IsPulling => !_pushed;
        /// <summary>Alcance (m).</summary>
        public float Radius => Config.radius;
        /// <summary>Más cerca de esto (m, entre centros) ya no atrae.</summary>
        public float MinPullDistance => Config.minPullDistance;

        /// <summary>La cúpula (embudo que se hace cúpula) crece mientras atrae y revienta al soltar el empujón.</summary>
        public override float ChargeVisualProgress => IsPulling ? PullProgress : -1f;

        /// <summary>
        /// Mientras atrae, nada la mueve: es el ojo del tornado (2026-10-13, tras probarlo con mando). Sin empuje de
        /// los choques y con mucha masa física, para que tampoco la arrastre quien se queda empujándola.
        /// </summary>
        public override float KnockbackTakenMultiplier => _pushed ? 1f : Config.knockbackTakenWhilePulling;
        public override float PhysicalMassMultiplier => _pushed ? 1f : Config.massWhilePulling;

        /// <summary>Mientras atrae no puede atacar ni hacer dash (2026-10-19, tras probarlo el usuario).</summary>
        public override bool BlocksActions => !_pushed;

        public override void OnActivate()
        {
            _elapsed = 0f;
            _pushed = false;
        }

        public override void Tick(float dt)
        {
            if (_pushed) return;
            _elapsed += dt;
            if (_elapsed >= Config.pullDuration)
            {
                Push(1f);
                return;
            }
            Pull(dt);
        }

        /// <summary>Pulsar Especial durante la atracción suelta el empujón ya, más flojo cuanto antes.</summary>
        public override bool OnPressedWhileActive()
        {
            if (_pushed) return false;
            Push(Mathf.Lerp(Config.minPushFraction, 1f, PullProgress));
            return true;
        }

        private void Pull(float dt)
        {
            float radius = Config.radius;
            Vector3 origin = Owner.Position;
            var blades = FakeBladeController.ActiveBlades;
            for (int i = 0; i < blades.Count; i++)
            {
                FakeBladeController other = blades[i];
                if (other == Owner || other.IsDestroyed || !Owner.CanHarm(other)) continue;

                Vector3 offset = origin - other.Position;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > radius) continue;
                // Atrapada: sin ataques ni dash mientras siga dentro (se renueva cada frame; acaba justo después del empujón)
                if (Config.trapsInRange) other.Trap(TrapRefresh);
                if (distance < Config.minPullDistance) continue;

                float falloff = Mathf.Lerp(1f, Config.pullEdgeFactor, distance / radius);
                other.ApplyPull(offset / distance * (Config.pullAcceleration * falloff * other.KnockbackResistance * dt));
            }
        }

        private void Push(float strength)
        {
            _pushed = true;
            float radius = Config.radius;
            Vector3 origin = Owner.Position;
            var blades = FakeBladeController.ActiveBlades;
            for (int i = 0; i < blades.Count; i++)
            {
                FakeBladeController other = blades[i];
                if (other == Owner || other.IsDestroyed || !Owner.CanHarm(other)) continue;

                Vector3 offset = other.Position - origin;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > radius) continue;

                float falloff = Mathf.Lerp(1f, 0.5f, distance / radius);
                Vector3 dir = distance > 0.001f ? offset / distance : Owner.Facing;
                // Primero se anula la velocidad con la que venía atraída hacia ella: si no, se come parte del empujón
                Vector3 velocity = other.Velocity;
                float inward = Mathf.Max(0f, -(velocity.x * dir.x + velocity.z * dir.z));
                other.ApplyKnockback(dir * (inward + Config.pushForce * strength * falloff * other.KnockbackResistance));
                other.ApplyDamage(other.MaxSpinSpeed * Config.pushDamagePct * strength * falloff, Owner);
                // Después del empuje: la lanzada cuenta ya con él (como Onda de choque)
                if (Config.launchDuration > 0f) other.TryLaunch(Owner, Config.launchDuration);
                BladeSpecialFx.PlayPushHit(Owner, other, dir);
            }

            // Ráfagas de viento que salen hacia fuera (K8); sin efectos propios, las estrellas y la onda comunes
            if (!BladeSpecialFx.PlayRelease(Owner))
            {
                VfxSystem.Play(VfxType.SpecialBurst, origin, Vector3.up, Data.color, 1.5f + strength);
                VfxSystem.EmitParticle(VfxType.AuraShockWave, origin, Vector3.zero, Data.color, radius * 1.2f);
            }
            CameraShake.Shake(Config.pushShake * strength, 0.12f);
            Owner.EndSpecial();
        }
    }
}
