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
        #region Peso y agarre
        /// <summary>
        /// Peso de referencia ÷ peso (>1 más ligera, <1 más pesada). Base de los efectos del peso, que van
        /// como (referencia ÷ peso)^exponente: sin topes, así un jefe muy pesado sigue siendo distinto de
        /// uno pesado, y nunca llega a 0 ni a negativo (C11, GDD 2.8).
        /// </summary>
        public static float WeightRatio(CombatConfig cfg, float weight) =>
            cfg.referenceWeight / Mathf.Max(cfg.minPhysicalMass, weight);

        /// <summary>Masa física del Rigidbody: solo para los choques de la física de Unity, no para moverse.</summary>
        public static float PhysicalMass(CombatConfig cfg, float weight) => Mathf.Max(cfg.minPhysicalMass, weight);

        /// <summary>Multiplicador del agarre sobre el giro y el frenado: 1 + agarre × gripPerPoint (mínimo ×0,25).</summary>
        public static float GripMultiplier(CombatConfig cfg, float grip) => Mathf.Max(0.25f, 1f + grip * cfg.gripPerPoint);
        #endregion

        #region Movimiento
        /// <summary>
        /// Velocidad máxima (m/s): solo la da la estadística de velocidad (el peso no la toca); la mitad de
        /// la diferencia con la de referencia se iguala (speedSpread).
        /// </summary>
        public static float MaxSpeed(CombatConfig cfg, float moveSpeed)
        {
            float raw = cfg.maxVelocity + moveSpeed * cfg.speedPerPoint;
            return Mathf.Clamp(Mathf.Lerp(cfg.referenceMaxSpeed, raw, cfg.speedSpread), 3f, 25f);
        }

        /// <summary>
        /// Aceleración (m/s²), igual para cualquier masa (se aplica como aceleración, no como fuerza): el peso
        /// cuenta una sola vez. La velocidad solo la sube por encima de 10 puntos.
        /// </summary>
        public static float Acceleration(CombatConfig cfg, float moveSpeed, float weight, float traitMultiplier = 1f) =>
            Mathf.Clamp(cfg.referenceAcceleration * Mathf.Max(moveSpeed * 0.1f, 1f) *
                        Mathf.Pow(WeightRatio(cfg, weight), cfg.accelerationWeightExponent) * traitMultiplier,
                1f, 120f);

        /// <summary>Corrección de la velocidad lateral por segundo al girar: depende del agarre, no del peso (C13).</summary>
        public static float TurnRate(CombatConfig cfg, float grip, float traitMultiplier = 1f) =>
            Mathf.Clamp(cfg.turnRate * GripMultiplier(cfg, grip) * traitMultiplier, 2f, 50f);

        /// <summary>Frenado al soltar el stick (fracción de la velocidad por segundo): depende del agarre, no del peso.</summary>
        public static float StoppingRate(CombatConfig cfg, float grip) => cfg.stoppingRate * GripMultiplier(cfg, grip);

        /// <summary>Multiplicador de los acelerones del dash y del ataque: las pesadas salen más lentas.</summary>
        public static float ImpulseFactor(CombatConfig cfg, float weight) =>
            cfg.referenceImpulse * Mathf.Pow(WeightRatio(cfg, weight), cfg.impulseWeightExponent);
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
