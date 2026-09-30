using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Ajuste global de movimiento y combate (GDD 2.x).
    /// Un único asset compartido por todas las peonzas: las diferencias entre peonzas
    /// vienen de sus piezas (FakeBladeStats), no de este archivo.
    ///
    /// Asignar el asset en el GameManager. Si no hay ninguno, se usan estos valores por defecto.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatConfig", menuName = "FakeBlade/Combat Config")]
    public class CombatConfig : ScriptableObject
    {
        #region Active Instance
        private static CombatConfig _active;

        /// <summary>Config en uso. Nunca es null (crea una por defecto si hace falta).</summary>
        public static CombatConfig Active
        {
            get
            {
                if (_active == null)
                {
                    _active = CreateInstance<CombatConfig>();
                    _active.name = "CombatConfig (Default)";
                    _active.hideFlags = HideFlags.DontSave;
                }
                return _active;
            }
        }

        public static void SetActive(CombatConfig config)
        {
            if (config != null) _active = config;
        }
        #endregion

        [Header("=== MOVIMIENTO ===")]
        [Tooltip("Fuerza de aceleración base")]
        public float accelerationForce = 80f;
        [Tooltip("Velocidad máxima de movimiento normal (sin ataques ni dash)")]
        public float maxVelocity = 12f;
        [Tooltip("Frenado cuando no hay input")]
        public float stoppingFriction = 3f;
        [Range(0.01f, 1f)] public float turnResponsiveness = 0.15f;
        public float linearDamping = 0.5f;
        public float angularDamping = 0.1f;
        [Tooltip("Tiempo que tarda en volver a la velocidad normal tras un ataque o dash")]
        public float burstRecoverTime = 0.45f;

        [Header("=== GIRO VISUAL ===")]
        [Tooltip("Grados por segundo de giro visual por cada RPM máxima (a vida llena)")]
        public float visualSpinDegreesPerRpm = 6f;
        [Tooltip("Fracción de RPM a la que la peonza todavía gira al 90% de su velocidad visual máxima. " +
                 "Más bajo = solo se frena cuando está muy cerca de 0 (0.08 = sigue casi a tope hasta el 8%).")]
        [Range(0.005f, 0.5f)] public float visualSpinKnee = 0.08f;
        [Tooltip("Fracción de RPM por debajo de la cual la peonza se bambolea y echa humo y chispas")]
        [Range(0f, 1f)] public float lowSpinThreshold = 0.3f;
        [Tooltip("Inclinación máxima (grados) del bamboleo cerca de 0 RPM. 0 = sin bamboleo")]
        public float lowSpinWobbleAngle = 12f;
        [Tooltip("Vueltas por segundo del bamboleo (precesión) justo al entrar en RPM bajas y cerca de 0")]
        public Vector2 lowSpinWobbleFrequency = new Vector2(3.5f, 1.6f);

        /// <summary>0 por encima del umbral de RPM bajas, 1 a 0 RPM.</summary>
        public float LowSpinFactor(float spinPercentage)
        {
            if (lowSpinThreshold <= 0f || spinPercentage >= lowSpinThreshold) return 0f;
            return 1f - Mathf.Clamp01(spinPercentage / lowSpinThreshold);
        }

        /// <summary>
        /// Factor 0-1 de la velocidad de giro visual según las RPM restantes (0-1).
        /// Curva de saturación con forma logarítmica: casi al máximo durante casi toda la vida
        /// y cae de golpe solo cerca de 0. f(knee) ≈ 0.9, f(0) = 0, f(1) = 1.
        /// </summary>
        public float EvaluateVisualSpin(float spinPercentage)
        {
            float p = Mathf.Clamp01(spinPercentage);
            float k = 2.302585f / Mathf.Max(0.001f, visualSpinKnee); // ln(10) / knee
            return (1f - Mathf.Exp(-k * p)) / (1f - Mathf.Exp(-k));
        }

        [Header("=== RPM (VIDA) ===")]
        [Tooltip("RPM perdidas por unidad de velocidad al chocar contra paredes")]
        public float wallDamagePerSpeed = 0.5f;
        [Tooltip("Velocidad mínima contra paredes para que haga daño")]
        public float wallMinDamageSpeed = 3f;
        [Tooltip("RPM por segundo que se pierden al rozar con otra peonza")]
        public float grindDamagePerSecond = 8f;
        [Tooltip("Potencia de ataque de referencia (ataque = referencia → multiplicador x1)")]
        public float referenceAttackPower = 15f;

        [Header("=== ATAQUE ===")]
        [Tooltip("Segundos para recuperar una carga de ataque")]
        public float attackRechargeTime = 1.5f;
        [Tooltip("Impulso (m/s) del ataque rápido")]
        public float quickAttackImpulse = 9f;
        [Tooltip("Coste en RPM del ataque rápido (fracción de las RPM máximas)")]
        [Range(0f, 0.2f)] public float quickAttackSpinCostPct = 0.02f;
        [Tooltip("Duración del estado de ataque (bonus de masa activo)")]
        public float quickAttackDuration = 0.35f;
        [Tooltip("Bonus de masa durante el ataque rápido (0.5 = +50%)")]
        public float quickAttackMassBonus = 0.5f;

        [Header("=== ATAQUE CARGADO ===")]
        [Tooltip("Tiempo pulsado a partir del cual empieza a cargar")]
        public float chargeHoldThreshold = 0.18f;
        [Tooltip("Segundos para subir un nivel de carga")]
        public float chargeTimePerLevel = 0.4f;
        [Tooltip("Segundos en carga máxima antes de lanzarse solo")]
        public float chargedAutoReleaseTime = 0.6f;
        [Tooltip("Impulso extra por nivel de carga (0.6 = +60% por nivel)")]
        public float chargedImpulsePerLevel = 0.6f;
        [Tooltip("Bonus de masa extra por nivel de carga")]
        public float chargedMassBonusPerLevel = 0.35f;
        [Tooltip("Empuje extra al objetivo por nivel de carga")]
        public float chargedKnockbackPerLevel = 0.4f;
        [Tooltip("Enfriamiento tras un ataque cargado antes de poder cargar otro")]
        public float chargedAttackCooldown = 0.8f;
        [Tooltip("Multiplicador de velocidad mientras se carga")]
        [Range(0f, 1f)] public float moveMultiplierWhileCharging = 0.6f;

        [Header("=== PARRY (GDD 2.5) ===")]
        [Tooltip("Segundos desde que se lanza un ataque rápido en los que un choque contra un ataque " +
                 "enemigo es parry. Las piezas pueden sumar o restar (parryWindowModifier)")]
        public float parryWindow = 0.12f;
        [Tooltip("Rebote (m/s) del atacante al que le hacen parry")]
        public float parryKnockback = 10f;
        [Tooltip("Segundos que el atacante queda aturdido (sin control ni ataques) mientras sale rebotado. 0 = sin aturdir")]
        public float parryStagger = 0.3f;
        [Tooltip("Fracción de ese rebote que recibe quien hace el parry (casi no se mueve)")]
        [Range(0f, 1f)] public float parryDefenderKnockbackFraction = 0.1f;
        [Tooltip("Energía de especial que gana quien hace el parry (1 = esfera llena)")]
        [Range(0f, 1f)] public float parryEnergy = 0.2f;
        [Tooltip("Pausa breve del juego al hacer parry (segundos reales). 0 = sin pausa")]
        public float parryHitStop = 0.08f;

        [Header("=== COMBOS ===")]
        [Tooltip("Tiempo máximo entre golpes para encadenar combo")]
        public float comboWindow = 1f;
        [Tooltip("Bonus de daño/empuje por golpe encadenado")]
        public float comboBonusPerHit = 0.1f;
        public int maxComboHits = 3;

        [Header("=== DASH ===")]
        public float dashCooldown = 1.5f;
        [Tooltip("Coste en RPM del dash (fracción de las RPM máximas)")]
        [Range(0f, 0.3f)] public float dashSpinCostPct = 0.06f;
        [Tooltip("Ventana en la que un choque tras el dash cuenta como ataque")]
        public float dashAttackWindow = 0.35f;
        [Tooltip("Fracción de las RPM gastadas en el dash que se recuperan si el dash gana un choque " +
                 "contra un enemigo (una vez por dash)")]
        [Range(0f, 1f)] public float dashHitRefundFraction = 0.5f;

        [Header("=== CHOQUE (GDD 2.5) ===")]
        [Tooltip("Velocidad de cierre mínima para que un choque cuente")]
        public float minCollisionSpeed = 1.5f;
        [Tooltip("Tiempo mínimo entre dos choques de la misma pareja (evita contar rebotes como golpes)")]
        public float clashCooldown = 0.15f;
        [Tooltip("Daño base por unidad de velocidad de cierre (lo reciben ambos)")]
        public float damagePerImpactSpeed = 1f;
        [Tooltip("Daño extra al más lento por unidad de diferencia de velocidad")]
        public float damagePerSpeedDiff = 2.5f;
        [Tooltip("Fracción del daño base que recibe la peonza más rápida")]
        [Range(0f, 1f)] public float fasterDamageFraction = 0.25f;
        [Tooltip("Diferencia de velocidad por debajo de la cual el choque es neutro")]
        public float equalSpeedTolerance = 0.5f;
        [Tooltip("Empuje base (m/s) al perdedor del choque")]
        public float knockbackBase = 5f;
        public float knockbackPerSpeedDiff = 0.4f;
        [Tooltip("Fracción del empuje que recibe el ganador")]
        [Range(0f, 1f)] public float winnerKnockbackFraction = 0.35f;
        public float maxKnockback = 18f;
        [Tooltip("Segundos durante los que un golpe cuenta para dar el punto de K.O.")]
        public float killCreditWindow = 5f;

        [Header("=== ESPECIAL (GDD 5) ===")]
        [Tooltip("Energía ganada por punto de daño infligido (1 = barra llena)")]
        public float specialEnergyPerDamage = 0.006f;
        [Tooltip("Multiplicador de energía si el golpe no fue con ataque o dash")]
        [Range(0f, 1f)] public float passiveHitEnergyMultiplier = 0.5f;
        [Tooltip("Duración del poder activo (la esfera se vacía en este tiempo)")]
        public float specialDuration = 5f;
        [Tooltip("Spin Boost: RPM recuperadas por segundo (fracción del máximo)")]
        public float spinBoostPctPerSecond = 0.06f;
        public float shockWaveRadius = 5f;
        public float shockWaveForce = 12f;
        [Tooltip("Onda de choque: daño (fracción de RPM máx. del objetivo)")]
        public float shockWaveDamagePct = 0.1f;
        [Range(0f, 1f)] public float stormBreakerDamageReduction = 0.6f;
        [Range(0f, 1f)] public float stormBreakerKnockbackResistance = 0.8f;
        public float stormBreakerMoveBonus = 0.15f;
        [Range(0f, 1f)] public float electricDashCooldownMultiplier = 0.3f;
        public float electricDashImpulseMultiplier = 1.5f;
        public float electricChargeSpeedMultiplier = 2f;

        [Header("=== FEEDBACK ===")]
        public bool cameraShake = true;
        public bool gamepadVibration = true;
    }
}
