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
        [Header("Encendido")]
        [Tooltip("Carga al activarlo (s): se frena y no puede atacar ni hacer dash; el fuego empieza al acabar (0 = enseguida)")]
        [Min(0f)] public float windupTime = 0.3f;
        [Tooltip("Frenado durante la carga (fracción de la velocidad que pierde por segundo)")]
        public float windupBrake = 12f;

        [Header("Quemadura")]
        [Tooltip("Fracción de las RPM máximas del rival que pierde en cada tic (0,015 = 1,5%)")]
        [Range(0f, 0.2f)] public float burnDamagePct = 0.015f;
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
            target.TryBurn(Owner, Config.burnDamagePct, Config.burnTickInterval, Config.burnDuration);
        }
    }
}
