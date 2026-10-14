using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Pinchos (GDD 5, Fase L, L3; decidido el 2026-10-13). Unos pinchos giran alrededor de la peonza (solo se ven:
    /// avisan de que está activo). Mientras dura:
    /// - quien la golpea atacando (rápido, cargado o dash) recibe el 70% del daño que le ha hecho, como el rasgo
    ///   Espinas (daño fijo, sin defensa, cuenta como golpe suyo para el punto de K.O.);
    /// - recibe un 20% menos de daño;
    /// - hace un 10% más de daño.
    /// </summary>
    [CreateAssetMenu(fileName = "Spikes", menuName = "FakeBlade/Specials/Pinchos")]
    public class SpikesData : SpecialAbilityData
    {
        [Header("Pinchos")]
        [Tooltip("Fracción del daño de cada ataque recibido que se devuelve a quien atacaba (decidido: 60-80%)")]
        [Range(0f, 1f)] public float reflectFraction = 0.7f;
        [Tooltip("Daño que recibe (0,8 = −20%)")]
        [Range(0f, 1f)] public float damageTakenMultiplier = 0.8f;
        [Tooltip("Daño que hace (1,1 = +10%)")]
        public float damageDealtMultiplier = 1.1f;

        [Header("Aura")]
        [Tooltip("Pinchos que giran alrededor")]
        [Min(1)] public int spikeCount = 6;
        [Tooltip("Vueltas por segundo")]
        public float spinTurnsPerSecond = 1.2f;

        public override SpecialAbilityType Type => SpecialAbilityType.Spikes;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new SpikesAbility(this, owner);
    }

    public sealed class SpikesAbility : SpecialAbility<SpikesData>
    {
        public SpikesAbility(SpikesData data, FakeBladeController owner) : base(data, owner) { }

        public override float ReflectFraction => Config.reflectFraction;
        public override float DamageTakenMultiplier => Config.damageTakenMultiplier;
        public override float DamageDealtMultiplier => Config.damageDealtMultiplier;
    }
}
