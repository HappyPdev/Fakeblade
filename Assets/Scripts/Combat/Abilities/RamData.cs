using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Embestida (GDD 5; decidido el 2026-10-14). Dos embestidas (charges, ajustable): la primera al activarlo y la otra
    /// pulsando Especial otra vez (hay 6 s; si no se usa, se pierde). Cada una empieza con una carga corta (se frena y
    /// apunta; así no se encadenan seguidas) y sale como una arrancada a velocidad media hacia el stick con mucha
    /// fuerza: golpea en un área más amplia que un choque (más difícil de esquivar) y a cada rival lo empuja fuerte, le
    /// quita un 6% de las RPM y lo deja lanzado (daño de pared si choca). No se puede bloquear ni hacer parry, y mientras
    /// carga o embiste no puede atacar ni hacer dash. Con la última, el poder termina.
    /// </summary>
    [CreateAssetMenu(fileName = "Ram", menuName = "FakeBlade/Specials/Embestida")]
    public class RamData : SpecialAbilityData
    {
        [Header("Embestidas")]
        [Tooltip("Embestidas por activación (2026-10-14: de 3 a 2, porque si la primera entraba encadenaba las tres)")]
        [Min(1)] public int charges = 2;
        [Tooltip("Carga antes de cada embestida (s): se frena y apunta con el stick; la dirección se toma al salir")]
        [Min(0f)] public float windupTime = 0.3f;
        [Tooltip("Frenado durante la carga (fracción de la velocidad que pierde por segundo)")]
        public float windupBrake = 12f;
        [Tooltip("Velocidad de la embestida (m/s): media, menos que un dash")]
        public float speed = 13f;
        [Tooltip("Segundos que dura cada embestida")]
        [Min(0.1f)] public float lungeTime = 0.45f;

        [Header("Golpe")]
        [Tooltip("Distancia entre centros a la que golpea (m). Un choque normal es a ~1,2 m: más = más difícil de esquivar")]
        public float hitRadius = 1.8f;
        [Tooltip("Empuje (m/s)")]
        public float force = 24f;
        [Tooltip("Daño (fracción de las RPM máximas del rival)")]
        [Range(0f, 1f)] public float damagePct = 0.06f;
        [Tooltip("Segundos que queda lanzado (daño de pared si choca)")]
        [Min(0f)] public float launchDuration = 0.4f;
        [Tooltip("Lo que le queda de velocidad a ella tras golpear (el golpe la frena)")]
        [Range(0f, 1f)] public float speedKeptOnHit = 0.35f;
        [Min(0f)] public float hitShake = 0.2f;
        [Min(0f)] public float hitStop = 0.04f;

        public override SpecialAbilityType Type => SpecialAbilityType.Ram;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new RamAbility(this, owner);
    }

    public sealed class RamAbility : SpecialAbility<RamData>
    {
        private const int MaxTargets = 8;
        private readonly FakeBladeController[] _hit = new FakeBladeController[MaxTargets];
        private int _hitCount;
        private int _left;
        private float _windup;
        private float _lungeTimer;
        private Vector3 _dir;

        public RamAbility(RamData data, FakeBladeController owner) : base(data, owner) { }

        public bool IsLunging => _lungeTimer > 0f;
        public bool IsWindingUp => _windup > 0f;
        /// <summary>Lo que lleva de carga (0-1), para el aspecto.</summary>
        public float WindupProgress => Config.windupTime > 0f ? 1f - Mathf.Clamp01(_windup / Config.windupTime) : 1f;
        /// <summary>Embestidas que le quedan (para el aspecto).</summary>
        public int ChargesLeft => _left;

        private bool Busy => IsLunging || IsWindingUp;

        /// <summary>La IA embiste con el rival a una embestida de distancia.</summary>
        public override float UseRange => Config.speed * Config.lungeTime + Config.hitRadius;
        public override bool WantsRepeatPress => _left > 0 && !Busy;
        public override bool BlocksActions => Busy;
        public override bool PreventsParry => IsLunging;

        // Los choques físicos mientras embiste no suman daño (el golpe es el de la embestida)
        public override ClashBonus GetClashBonus(FakeBladeController target, float approachSpeed) =>
            IsLunging ? new ClashBonus(0f, 1f, false) : ClashBonus.None;

        public override void OnActivate()
        {
            _left = Config.charges;
            _lungeTimer = 0f;
            BeginWindup();
        }

        public override bool OnPressedWhileActive()
        {
            if (_left <= 0 || Busy) return false;
            BeginWindup();
            return true;
        }

        public override void OnEnd()
        {
            _lungeTimer = 0f;
            _windup = 0f;
            _left = 0;
        }

        /// <summary>Gasta una embestida y empieza su carga (sin carga, sale ya).</summary>
        private void BeginWindup()
        {
            _left--;
            _windup = Config.windupTime;
            if (_windup <= 0f) Launch();
        }

        /// <summary>Sale hacia el stick en ese momento (se apunta durante la carga).</summary>
        private void Launch()
        {
            _windup = 0f;
            _dir = Owner.ActionDirection;
            _dir.y = 0f;
            if (_dir.sqrMagnitude < 0.001f) _dir = Owner.Facing;
            _dir.Normalize();
            _lungeTimer = Config.lungeTime;
            _hitCount = 0;
            Owner.Lunge(_dir * Config.speed, Config.lungeTime);
            VfxSystem.Play(VfxType.Dash, Owner.Position, -_dir, Data.color, 1.5f);
        }

        public override void Tick(float dt)
        {
            if (IsWindingUp)
            {
                // Carga: se frena hasta quedarse quieta
                Vector3 v = Owner.Velocity;
                v.y = 0f;
                Owner.ApplyPull(-v * Mathf.Min(1f, Config.windupBrake * dt));
                _windup -= dt;
                if (_windup <= 0f) Launch();
                return;
            }
            if (!IsLunging) return;
            _lungeTimer -= dt;
            HitAround();
            // Con la última embestida terminada, el poder se acaba
            if (_lungeTimer <= 0f && _left <= 0) Owner.EndSpecial();
        }

        /// <summary>Golpea a cada rival que entre en su radio (una vez por embestida).</summary>
        private void HitAround()
        {
            float r2 = Config.hitRadius * Config.hitRadius;
            Vector3 origin = Owner.Position;
            var blades = FakeBladeController.ActiveBlades;
            for (int i = 0; i < blades.Count; i++)
            {
                FakeBladeController other = blades[i];
                if (other == Owner || other.IsDestroyed || !Owner.CanHarm(other) || AlreadyHit(other)) continue;
                Vector3 offset = other.Position - origin;
                offset.y = 0f;
                if (offset.sqrMagnitude > r2) continue;

                if (_hitCount < MaxTargets) _hit[_hitCount++] = other;
                // Empuje sobre todo en la dirección de la embestida, algo hacia donde está el rival
                Vector3 toOther = offset.sqrMagnitude > 0.0001f ? offset.normalized : _dir;
                Vector3 dir = (_dir * 0.6f + toOther * 0.4f).normalized;
                other.ApplyKnockback(dir * (Config.force * other.KnockbackResistance));
                other.ApplyDamage(other.MaxSpinSpeed * Config.damagePct, Owner);
                if (Config.launchDuration > 0f) other.TryLaunch(Owner, Config.launchDuration);
                if (other.IsGhost) other.RegisterGhostHit(); // cuenta como un ataque contra el clon de Fantasma

                // El golpe la frena a ella
                Vector3 v = Owner.Velocity;
                v.y = 0f;
                Owner.ApplyPull(-v * (1f - Config.speedKeptOnHit));

                Vector3 contact = origin + toOther * (Config.hitRadius * 0.5f);
                VfxSystem.Play(VfxType.SpecialBurst, contact, Vector3.up, Data.color, 1.5f);
                VfxSystem.Play(VfxType.Clash, contact, toOther, Color.white, 1.5f);
                CameraShake.Shake(Config.hitShake, 0.12f);
                HitStop.Trigger(Config.hitStop);
            }
        }

        private bool AlreadyHit(FakeBladeController other)
        {
            for (int i = 0; i < _hitCount; i++)
                if (_hit[i] == other) return true;
            return false;
        }
    }
}
