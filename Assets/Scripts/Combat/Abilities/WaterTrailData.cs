using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Rastro de agua (GDD 5, Fase L, L4; decidido el 2026-10-14). Durante 4 s va dejando agua por donde pasa; cada
    /// trozo se seca a los 3 s. Las rivales que la pisan quedan Mojadas 2 s (estado aparte de los demás: casi sin
    /// agarre, derrapan; mojarse apaga la quemadura) y volver a pisarla lo renueva. Su dueña patina a favor: no
    /// resbala y va un 20% más rápida sobre su agua. El agua sigue en el suelo después de terminar el poder.
    /// Al terminar (2026-10-26, pedido del usuario) deja un charco mucho más grande que dura bastante más y moja igual.
    /// Sin instanciar nada: los trozos son una lista fija de puntos.
    /// </summary>
    [CreateAssetMenu(fileName = "WaterTrail", menuName = "FakeBlade/Specials/Rastro de agua")]
    public class WaterTrailData : SpecialAbilityData
    {
        [Header("Rastro")]
        [Tooltip("Segundos que dura un trozo de agua en el suelo")]
        [Min(0.1f)] public float puddleLifetime = 3f;
        [Tooltip("Distancia entre trozos (m)")]
        [Min(0.1f)] public float spacing = 0.4f;
        [Tooltip("Radio de cada trozo (m, desde el centro de la peonza que lo pisa)")]
        public float puddleRadius = 0.8f;

        [Header("Charco al terminar (2026-10-26)")]
        [Tooltip("Radio del charco que deja al terminar el poder (m; 0 = ninguno). Su mancha en el suelo es la de End Burst " +
                 "en VfxLibrary (Special Effects → WaterTrail): mismo tamaño (el doble del radio) y misma duración")]
        [Min(0f)] public float finalPuddleRadius = 2f;
        [Tooltip("Segundos que dura el charco del final")]
        [Min(0.1f)] public float finalPuddleLifetime = 6f;

        [Header("Mojada")]
        [Tooltip("Segundos que resbala quien pisa el agua (pisarla otra vez lo renueva)")]
        [Min(0.1f)] public float wetDuration = 2f;
        [Tooltip("Agarre mientras resbala: giro y frenado (0,1 = casi nada; el mínimo de las puntas es 0,25)")]
        [Range(0f, 1f)] public float wetGripMultiplier = 0.1f;

        [Header("Su dueña")]
        [Tooltip("Velocidad máxima de la dueña sobre su propia agua (1,2 = +20%)")]
        public float ownerSpeedOnWater = 1.2f;

        public override SpecialAbilityType Type => SpecialAbilityType.WaterTrail;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new WaterTrailAbility(this, owner);
    }

    public sealed class WaterTrailAbility : SpecialAbility<WaterTrailData>
    {
        /// <summary>Trozos como mucho (4 s a toda velocidad, ~15 m/s, con 0,4 m entre trozos: ~150; y el charco del final).</summary>
        public const int MaxPuddles = 160;

        private readonly Vector3[] _points = new Vector3[MaxPuddles];
        private readonly float[] _times = new float[MaxPuddles];
        // Radio y duración de cada trozo: los del rastro, o los del charco del final
        private readonly float[] _radii = new float[MaxPuddles];
        private readonly float[] _lifetimes = new float[MaxPuddles];
        private int _next;
        private int _count;
        private Vector3 _lastLaid;
        private bool _onOwnWater;

        public WaterTrailAbility(WaterTrailData data, FakeBladeController owner) : base(data, owner) { }

        public int PuddleCapacity => MaxPuddles;
        /// <summary>Radio de los trozos del rastro (m, desde el centro de la peonza que lo pisa).</summary>
        public float PuddleRadius => Config.puddleRadius;
        /// <summary>Radio del trozo i (el charco del final es más grande).</summary>
        public float PuddleRadiusAt(int index) => _radii[index];
        /// <summary>Trozo i del anillo: posición y si sigue mojado.</summary>
        public bool IsPuddleAlive(int index) => index < _count && Time.time - _times[index] < _lifetimes[index];
        public Vector3 PuddlePosition(int index) => _points[index];
        /// <summary>0-1: lo que le queda a un trozo antes de secarse (para el aspecto).</summary>
        public float PuddleLife(int index) => 1f - Mathf.Clamp01((Time.time - _times[index]) / _lifetimes[index]);

        // Los cilindros de agua: crecen ya en la carga común y se quedan mientras dura (como la Defensa)
        public override bool DomeInCommonWindup => true;
        public override float ChargeVisualProgress => 1f;

        public override void OnActivate()
        {
            Lay(Owner.Position, Config.puddleRadius, Config.puddleLifetime);
        }

        public override void Tick(float dt)
        {
            Vector3 p = Owner.Position;
            Vector3 d = p - _lastLaid;
            d.y = 0f;
            if (d.sqrMagnitude >= Config.spacing * Config.spacing) Lay(p, Config.puddleRadius, Config.puddleLifetime);
            Soak();
        }

        /// <summary>Al terminar, el agua cae al suelo: un charco grande que dura más (salvo si la peonza ha caído).</summary>
        public override void OnEnd()
        {
            if (Config.finalPuddleRadius > 0f && !Owner.IsDestroyed)
                Lay(Owner.Position, Config.finalPuddleRadius, Config.finalPuddleLifetime);
        }

        // El agua sigue mojando después del poder, hasta secarse
        public override bool IsLingering
        {
            get
            {
                for (int i = 0; i < _count; i++)
                    if (IsPuddleAlive(i)) return true;
                return false;
            }
        }

        public override void TickLingering(float dt) => Soak();

        public override void ClearLingering()
        {
            _count = 0;
            _next = 0;
            _onOwnWater = false;
        }

        /// <summary>La dueña patina a favor sobre su agua.</summary>
        public override float MoveSpeedMultiplier => _onOwnWater ? Config.ownerSpeedOnWater : 1f;

        private void Lay(Vector3 p, float radius, float lifetime)
        {
            _points[_next] = p;
            _times[_next] = Time.time;
            _radii[_next] = radius;
            _lifetimes[_next] = Mathf.Max(0.1f, lifetime);
            _next = (_next + 1) % MaxPuddles;
            if (_count < MaxPuddles) _count++;
            _lastLaid = p;
        }

        /// <summary>Moja a las rivales que pisan agua y mira si la dueña está sobre la suya.</summary>
        private void Soak()
        {
            _onOwnWater = OnWater(Owner.Position);

            var blades = FakeBladeController.ActiveBlades;
            for (int b = 0; b < blades.Count; b++)
            {
                FakeBladeController other = blades[b];
                if (other == Owner || other.IsDestroyed || !Owner.CanHarm(other)) continue;
                if (OnWater(other.Position))
                    other.TryWet(Owner, Config.wetGripMultiplier, Config.wetDuration);
            }
        }

        private bool OnWater(Vector3 position)
        {
            float now = Time.time;
            for (int i = 0; i < _count; i++)
            {
                if (now - _times[i] >= _lifetimes[i]) continue;
                float dx = position.x - _points[i].x, dz = position.z - _points[i].z;
                if (dx * dx + dz * dz <= _radii[i] * _radii[i]) return true;
            }
            return false;
        }
    }
}
