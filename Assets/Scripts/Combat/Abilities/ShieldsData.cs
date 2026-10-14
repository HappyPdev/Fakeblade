using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Escudo (GDD 5, Fase L, L6; decidido el 2026-10-13). Tres escudos de 60º giran alrededor de la peonza a una
    /// vuelta por segundo y cubren la mitad del contorno. Si una peonza la embiste (ataque, dash o empujón) y entra
    /// por un escudo, el choque se para: ninguna de las dos pierde RPM, la que embiste sale despedida con el empuje
    /// entero ×1,8 y ese escudo se rompe. Si entra por un hueco, es un choque normal.
    /// Duración indefinida (2026-10-13, tras probarlo con mando): el poder activo dura muy poco (duration del asset)
    /// y los escudos se quedan hasta romperse; mientras, la esfera se vuelve a cargar, y activarlo otra vez rellena
    /// los tres. Necesita menos energía (0,6): es un poder flojo, sin ventaja de daño y difícil de controlar.
    /// </summary>
    [CreateAssetMenu(fileName = "Shields", menuName = "FakeBlade/Specials/Escudo")]
    public class ShieldsData : SpecialAbilityData
    {
        [Header("Escudos")]
        [Min(1)] public int shieldCount = 3;
        [Tooltip("Ancho de cada escudo (grados). 3 de 60º cubren la mitad del contorno")]
        [Range(5f, 120f)] public float shieldArc = 60f;
        [Tooltip("Vueltas por segundo")]
        public float turnsPerSecond = 1f;
        [Tooltip("Distancia de los escudos al centro de la peonza (m), para el aspecto")]
        public float radius = 0.95f;

        [Header("Al chocar con un escudo")]
        [Tooltip("Velocidad mínima de la otra peonza hacia ella (m/s) para que el escudo la pare")]
        [Min(0f)] public float minAttackerSpeed = 1f;
        [Tooltip("Multiplicador del empuje que se lleva quien choca con el escudo")]
        public float knockbackMultiplier = 1.8f;
        [Tooltip("Daño que recibe quien choca con el escudo (0 = ninguno: el escudo protege, no castiga)")]
        [Range(0f, 1f)] public float damageToAttackerMultiplier = 0f;

        public override SpecialAbilityType Type => SpecialAbilityType.Shields;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new ShieldsAbility(this, owner);
    }

    public sealed class ShieldsAbility : SpecialAbility<ShieldsData>
    {
        private const int MaxShields = 8;
        private readonly bool[] _up = new bool[MaxShields];
        /// <summary>Ángulo inicial (al azar en cada activación); el giro sale del tiempo, con o sin el poder activo.</summary>
        private float _phase;
        private int _pending = -1;

        public ShieldsAbility(ShieldsData data, FakeBladeController owner) : base(data, owner) { }

        /// <summary>Escudos que hay (para el aspecto).</summary>
        public int ShieldCount => Mathf.Min(Config.shieldCount, MaxShields);
        public bool IsUp(int index) => index >= 0 && index < MaxShields && _up[index];

        /// <summary>Escudos que siguen puestos.</summary>
        public int ShieldsUp
        {
            get
            {
                int count = 0;
                for (int i = 0; i < ShieldCount; i++)
                    if (_up[i]) count++;
                return count;
            }
        }
        /// <summary>Ángulo del centro de un escudo (radianes, en el plano XZ).</summary>
        public float CenterAngle(int index) =>
            _phase + Time.time * Config.turnsPerSecond * Mathf.PI * 2f + index * Mathf.PI * 2f / ShieldCount;
        public float ArcRadians => Config.shieldArc * Mathf.Deg2Rad;
        public float Radius => Config.radius;

        public override void OnActivate()
        {
            for (int i = 0; i < MaxShields; i++) _up[i] = i < ShieldCount;
            _phase = Random.value * Mathf.PI * 2f;
            _pending = -1;
        }

        // Los escudos siguen puestos al terminar la fase activa: duran hasta romperse (o hasta reiniciar la peonza)
        public override bool IsLingering
        {
            get
            {
                for (int i = 0; i < ShieldCount; i++)
                    if (_up[i]) return true;
                return false;
            }
        }

        public override void ClearLingering()
        {
            for (int i = 0; i < MaxShields; i++) _up[i] = false;
        }

        public override ClashGuard GetClashGuard(FakeBladeController attacker, float attackerApproachSpeed)
        {
            _pending = -1;
            if (attackerApproachSpeed < Config.minAttackerSpeed) return ClashGuard.None;

            Vector3 offset = attacker.Position - Owner.Position;
            float hitAngle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
            float half = Config.shieldArc * 0.5f;
            for (int i = 0; i < ShieldCount; i++)
            {
                if (!_up[i]) continue;
                if (Mathf.Abs(Mathf.DeltaAngle(hitAngle, CenterAngle(i) * Mathf.Rad2Deg)) > half) continue;
                _pending = i;
                return new ClashGuard(0f, Config.knockbackMultiplier, Config.damageToAttackerMultiplier);
            }
            return ClashGuard.None; // entra por un hueco
        }

        public override void OnClashGuardApplied(FakeBladeController attacker, Vector3 contactPoint)
        {
            if (_pending < 0) return;
            _up[_pending] = false;
            float a = CenterAngle(_pending);
            Vector3 p = Owner.Position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Config.radius;
            VfxSystem.Play(VfxType.Parry, p, Vector3.up, Data.color, 0.9f);
            VfxSystem.Play(VfxType.Clash, p, Vector3.up, Data.color, 1.2f);
            CameraShake.Shake(0.08f, 0.08f);
            _pending = -1;

            // Sin escudos, el poder termina (si aún estaba en su fase activa; si no, ya no queda nada puesto)
            if (!IsLingering) Owner.EndSpecial();
        }
    }
}
