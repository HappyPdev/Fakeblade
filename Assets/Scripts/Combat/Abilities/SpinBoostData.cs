using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Spin Boost (GDD 5; rehecho en F1, 2026-10-13: «Acelerón»). Mientras está activo la peonza lo hace
    /// todo mejor: más velocidad y aceleración, recarga los ataques más rápido y no pierde RPM por desgaste.
    /// Además cura un poco cada segundo, hasta que la golpea un ataque enemigo (como todas las curaciones).
    /// </summary>
    [CreateAssetMenu(fileName = "SpinBoost", menuName = "FakeBlade/Specials/Spin Boost")]
    public class SpinBoostData : SpecialAbilityData
    {
        [Header("Acelerón")]
        [Tooltip("Velocidad máxima de movimiento (1,25 = +25%)")]
        public float moveSpeedMultiplier = 1.25f;
        [Tooltip("Aceleración (1,25 = +25%)")]
        public float accelerationMultiplier = 1.25f;
        [Tooltip("Velocidad de recarga de las cargas de ataque (1,5 = x1,5)")]
        public float attackRechargeMultiplier = 1.5f;
        [Tooltip("Desgaste de RPM con el tiempo (0 = sin desgaste)")]
        [Range(0f, 1f)] public float spinDecayMultiplier = 0f;

        [Header("Curación")]
        [Tooltip("RPM recuperadas por segundo mientras está activo (fracción de las RPM máximas). Un golpe de ataque enemigo la corta")]
        [Range(0f, 0.5f)] public float regenPctPerSecond = 0.02f;

        public override SpecialAbilityType Type => SpecialAbilityType.SpinBoost;

        public override SpecialAbility CreateRuntime(FakeBladeController owner) => new SpinBoostAbility(this, owner);
    }

    public sealed class SpinBoostAbility : SpecialAbility<SpinBoostData>
    {
        public SpinBoostAbility(SpinBoostData data, FakeBladeController owner) : base(data, owner) { }

        public override bool Heals => Config.regenPctPerSecond > 0f;

        // Por el sistema de poderes: un golpe de ataque enemigo corta la regeneración
        public override void Tick(float dt) => Owner.Special.Heal(Owner.MaxSpinSpeed * Config.regenPctPerSecond * dt);

        public override float MoveSpeedMultiplier => Config.moveSpeedMultiplier;
        public override float AccelerationMultiplier => Config.accelerationMultiplier;
        public override float AttackRechargeMultiplier => Config.attackRechargeMultiplier;
        public override float SpinDecayMultiplier => Config.spinDecayMultiplier;
    }
}
