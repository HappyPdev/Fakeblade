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
        [Tooltip("Cuánto se separan las velocidades máximas de las peonzas de la de referencia " +
                 "(1 = lo que dicen sus piezas; menos = más parecidas entre sí)")]
        [Range(0f, 1f)] public float speedSpread = 0.5f;
        [Tooltip("Velocidad máxima de referencia (más o menos la de una peonza media)")]
        public float referenceMaxSpeed = 15.5f;
        [Tooltip("Respuesta del giro según el peso: x = la más ligera, y = la más pesada")]
        public Vector2 turnByWeight = new Vector2(1.3f, 0.8f);
        [Tooltip("Aceleración según el peso: x = la más ligera, y = la más pesada")]
        public Vector2 accelerationByWeight = new Vector2(1f, 0.7f);
        [Tooltip("Masa física mínima del Rigidbody: las muy ligeras no se sienten flotantes ni aceleran " +
                 "de golpe (el peso de sus piezas sigue contando para el daño)")]
        public float minPhysicalMass = 0.6f;
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
        [Tooltip("Daño de la pared respecto a un choque parejo contra una peonza a la misma velocidad " +
                 "(1 = igual: velocidad perpendicular × daño por velocidad de choque)")]
        public float wallDamageScale = 1f;
        [Tooltip("Velocidad mínima contra paredes para que haga daño")]
        public float wallMinDamageSpeed = 3f;
        [Tooltip("RPM por segundo que se pierden al rozar con otra peonza")]
        public float grindDamagePerSecond = 8f;
        [Tooltip("Segundos pegadas a otra peonza (sin choque nuevo) antes de que se repelan")]
        public float stuckRepelDelay = 0.3f;
        [Tooltip("Empuje (m/s) con el que se separan dos peonzas pegadas")]
        public float stuckRepelSpeed = 7f;
        [Tooltip("Velocidad de cierre con la que cuenta el daño al separarse (como un choque parejo a esa velocidad)")]
        public float stuckImpactSpeed = 5f;
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
        [Tooltip("Impulso extra por nivel de carga (0.6 = +60% por nivel): más alcance y más empuje, no más daño")]
        public float chargedImpulsePerLevel = 0.6f;
        [Tooltip("Daño extra por nivel de carga sobre el de un ataque rápido (0,1 = +10% por nivel; nivel 3 = x1,3). " +
                 "El daño del cargado se calcula sin la velocidad extra de la carga")]
        public float chargedDamagePerLevel = 0.1f;
        [Tooltip("Bonus de masa extra por nivel de carga (la masa multiplica el daño: mejor dejarlo a 0 y usar chargedDamagePerLevel)")]
        public float chargedMassBonusPerLevel = 0f;
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
        public float dashCooldown = 1.8f;
        [Tooltip("Coste en RPM del dash (fracción de las RPM máximas)")]
        [Range(0f, 0.3f)] public float dashSpinCostPct = 0.08f;
        [Tooltip("Ventana en la que un choque tras el dash cuenta como ataque")]
        public float dashAttackWindow = 0.35f;
        [Tooltip("Fracción de las RPM gastadas en el dash que se recuperan si el dash gana un choque " +
                 "contra un enemigo (una vez por dash)")]
        [Range(0f, 1f)] public float dashHitRefundFraction = 0.5f;

        [Header("=== DAÑO GLOBAL ===")]
        [Tooltip("Multiplica todo el daño de golpes, paredes y roce (la quemadura va aparte, por porcentaje). " +
                 "Menos = combates más largos")]
        public float damageMultiplier = 0.8f;
        [Tooltip("Límites de la relación de masas en el daño y el empuje (x = mínimo, y = máximo). " +
                 "Más estrecho = el peso decide menos y las ligeras no pegan tan poco")]
        public Vector2 massRatioRange = new Vector2(0.8f, 1.3f);
        [Tooltip("Cuánto cuentan las diferencias de ataque de las piezas en el daño (1 = todo; 0 = nada)")]
        [Range(0f, 1f)] public float attackSpread = 0.6f;

        [Header("=== CHOQUE (GDD 2.5) ===")]
        [Tooltip("Velocidad de cierre mínima para que un choque cuente")]
        public float minCollisionSpeed = 1.5f;
        [Tooltip("Tiempo mínimo entre dos choques de la misma pareja (evita contar rebotes como golpes)")]
        public float clashCooldown = 0.15f;
        [Tooltip("Daño base por unidad de velocidad de cierre (lo reciben ambos)")]
        public float damagePerImpactSpeed = 1f;
        [Tooltip("Daño extra al más lento por unidad de diferencia de velocidad")]
        public float damagePerSpeedDiff = 2.5f;
        [Tooltip("Daño de los golpes con ataque (rápido o cargado) sobre el de un choque sin atacar")]
        public float attackHitDamageMultiplier = 1.2f;
        [Tooltip("Daño de los golpes con dash. Menos de 1: el dash sirve para moverse y rematar, no para " +
                 "ser el golpe principal (llega mucho más rápido que un ataque)")]
        public float dashHitDamageMultiplier = 0.75f;
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
        [Tooltip("Efecto común de todos los poderes al activarse: RPM recuperadas (fracción de las RPM máximas). " +
                 "Además se rellenan todas las cargas de ataque.")]
        [Range(0f, 1f)] public float specialActivationSpinPct = 0.25f;
        [Tooltip("Segundos en los que se recuperan esas RPM (no es al momento). Un golpe de ataque enemigo " +
                 "corta lo que falte, también la regeneración del poder")]
        public float specialHealTime = 2f;
        // Energía por golpe acertado (ganar el choque), según el tipo de golpe; no depende del daño.
        // 1 = barra estándar llena (cada poder pide la suya). Choque parejo: la mitad. Parry: parryEnergy.
        [Tooltip("Energía por golpe acertado con ataque rápido")]
        public float specialEnergyQuickHit = 0.08f;
        [Tooltip("Energía por golpe acertado con dash")]
        public float specialEnergyDashHit = 0.06f;
        [Tooltip("Energía por golpe acertado con ataque cargado: base + por nivel (nivel 3 = 0,12 + 3 × 0,05 = 0,27)")]
        public float specialEnergyChargedHit = 0.12f;
        public float specialEnergyPerChargeLevel = 0.05f;
        [Tooltip("Energía por ganar un choque sin atacar ni hacer dash")]
        public float specialEnergyPassiveHit = 0.03f;
        [Tooltip("Multiplica toda la energía ganada (ritmo general del especial)")]
        public float specialEnergyMultiplier = 1.2f;
        // Duración, energía necesaria y valores propios de cada poder: en su asset de
        // Resources/SpecialAbilities (SpecialAbilityData)

        [Header("=== FEEDBACK ===")]
        public bool cameraShake = true;
        public bool gamepadVibration = true;
    }
}
