using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Fuego (GDD 5; sustituye a Rastro de fuego). Al activarlo, una carga corta (se frena, como Embestida y Salto;
    /// 2026-10-17) y se enciende. Desde entonces, cualquier choque en el que quite RPM al rival lo quema: pierde un
    /// porcentaje de sus RPM máximas cada pocos segundos. Un golpe nuevo reinicia la duración; no se acumula. Las reglas
    /// entre estados las aplica StatusEffectSystem.
    /// </summary>
    [CreateAssetMenu(fileName = "Fire", menuName = "FakeBlade/Specials/Fuego")]
    public class FireData : SpecialAbilityData
    {
        [Header("Quemadura")]
        [Tooltip("RPM que pierde el rival en cada tic (fijas desde el 2026-11-02; antes el 1,5% de sus RPM máximas, 6 con 400 de base)")]
        [Min(0f)] public float burnDamagePerTick = 6f;
        [Tooltip("Segundos entre tics")]
        [Min(0.05f)] public float burnTickInterval = 0.5f;
        [Tooltip("Segundos que dura la quemadura")]
        [Min(0.1f)] public float burnDuration = 3f;

        public override SpecialAbilityType Type => SpecialAbilityType.Fire;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new FireAbility(this, owner);
    }

    public sealed class FireAbility : SpecialAbility<FireData>
    {
        private float _windup;

        public FireAbility(FireData data, FakeBladeController owner) : base(data, owner) { }

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

        public override void OnClashDamageDealt(FakeBladeController target, float damage)
        {
            if (IsWindingUp) return; // todavía no se ha encendido
            target.TryBurn(Owner, Config.burnDamagePerTick, Config.burnTickInterval, Config.burnDuration);
        }
    }
}
