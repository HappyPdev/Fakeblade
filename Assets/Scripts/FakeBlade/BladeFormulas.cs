using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Fórmulas que convierten las estadísticas de una peonza en valores del juego (GDD 2.8). Las usan
    /// la peonza (FakeBladeController) y el exportador de estadísticas, para que las tablas digan siempre
    /// lo mismo que el juego. Los rasgos y los poderes se aplican aparte, en la peonza.
    /// </summary>
    public static class BladeFormulas
    {
        #region Peso
        /// <summary>Peso en escala 0-1: 0,5 o menos = 0 (ligera), 3 o más = 1 (pesada).</summary>
        public static float WeightNormalized(float weight) => Mathf.InverseLerp(0.5f, 3f, weight);

        /// <summary>Masa física del Rigidbody.</summary>
        public static float PhysicalMass(CombatConfig cfg, float weight) => Mathf.Max(cfg.minPhysicalMass, weight);
        #endregion

        #region Movimiento
        /// <summary>m/s que aporta cada punto de velocidad antes de igualar (0,8 ligera → 0,4 pesada).</summary>
        public static float SpeedPerPoint(float weightNormalized) => Mathf.Lerp(0.8f, 0.4f, weightNormalized);

        /// <summary>Velocidad máxima (m/s): la mitad de la diferencia con la de referencia se iguala (speedSpread).</summary>
        public static float MaxSpeed(CombatConfig cfg, float moveSpeed, float weightNormalized)
        {
            float raw = cfg.maxVelocity + moveSpeed * SpeedPerPoint(weightNormalized);
            return Mathf.Clamp(Mathf.Lerp(cfg.referenceMaxSpeed, raw, cfg.speedSpread), 3f, 25f);
        }

        /// <summary>
        /// Fuerza de aceleración (sin rasgos). Se aplica como fuerza, así que la aceleración real es esto
        /// entre la masa física (ver Acceleration).
        /// </summary>
        public static float AccelerationForce(CombatConfig cfg, float moveSpeed, float weightNormalized, float traitMultiplier = 1f) =>
            Mathf.Clamp(cfg.accelerationForce * Mathf.Max(moveSpeed * 0.1f, 1f) *
                        Mathf.Lerp(cfg.accelerationByWeight.x, cfg.accelerationByWeight.y, weightNormalized) * traitMultiplier,
                5f, 120f);

        /// <summary>Aceleración real (m/s²): la fuerza entre la masa física.</summary>
        public static float Acceleration(CombatConfig cfg, float moveSpeed, float weight) =>
            AccelerationForce(cfg, moveSpeed, WeightNormalized(weight)) / PhysicalMass(cfg, weight);

        /// <summary>Respuesta del giro (sin rasgos).</summary>
        public static float TurnSpeed(CombatConfig cfg, float weightNormalized, float traitMultiplier = 1f) =>
            cfg.turnResponsiveness * Mathf.Lerp(cfg.turnByWeight.x, cfg.turnByWeight.y, weightNormalized) * traitMultiplier;

        /// <summary>Corrección lateral por segundo al girar (lo que la peonza aplica de verdad).</summary>
        public static float TurnRate(float turnSpeed) => Mathf.Clamp(turnSpeed * 120f, 5f, 50f);

        /// <summary>Rozamiento al soltar el stick (antes de dividir entre la masa).</summary>
        public static float StoppingDrag(CombatConfig cfg, float weightNormalized) =>
            cfg.stoppingFriction * Mathf.Lerp(1.5f, 0.4f, weightNormalized);

        /// <summary>Frenado real al soltar el stick (fracción de la velocidad por segundo).</summary>
        public static float StoppingRate(CombatConfig cfg, float weight) =>
            StoppingDrag(cfg, WeightNormalized(weight)) / Mathf.Max(0.1f, PhysicalMass(cfg, weight));

        /// <summary>Multiplicador de los acelerones del dash y del ataque (1,15 ligera → 0,85 pesada).</summary>
        public static float ImpulseFactor(float weightNormalized) => Mathf.Lerp(1.15f, 0.85f, weightNormalized);
        #endregion

        #region Combate
        /// <summary>Multiplicador de daño por el ataque de las piezas: 0,4 + 0,04 × ataque con los valores actuales.</summary>
        public static float AttackMultiplier(CombatConfig cfg, float attackPower) =>
            Mathf.Lerp(1f, attackPower / Mathf.Max(1f, cfg.referenceAttackPower), cfg.attackSpread);

        /// <summary>Parte del daño que se recibe por la defensa (1 − defensa / 100).</summary>
        public static float DamageTakenFactor(float defense) => 1f - defense * 0.01f;

        /// <summary>Parte del empuje que se recibe por la defensa (1 − defensa × 0,005).</summary>
        public static float KnockbackTakenFactor(float defense) => 1f - defense * 0.005f;

        /// <summary>
        /// Cuánto vale un golpe según la velocidad de choque (sin la de la carga): de hitSpeedFactor.x en
        /// hitSpeedRange.x a hitSpeedFactor.y en hitSpeedRange.y (y no más). Por debajo del mínimo baja en
        /// proporción hasta 0, para que un roce lento no quite como un golpe.
        /// </summary>
        public static float HitSpeedFactor(CombatConfig cfg, float speed)
        {
            Vector2 range = cfg.hitSpeedRange;
            Vector2 factor = cfg.hitSpeedFactor;
            if (speed < range.x) return factor.x * Mathf.Max(0f, speed) / Mathf.Max(0.01f, range.x);
            return Mathf.Lerp(factor.x, factor.y, Mathf.InverseLerp(range.x, range.y, speed));
        }

        /// <summary>Daño del ataque cargado sobre el de uno rápido: 1 + chargedDamagePerLevel × nivel.</summary>
        public static float ChargeDamageMultiplier(CombatConfig cfg, int chargeLevel) =>
            1f + cfg.chargedDamagePerLevel * chargeLevel;
        #endregion
    }
}
