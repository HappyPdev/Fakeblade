namespace FakeBlade.Core
{
    /// <summary>Niveles de dificultad de la IA (GDD 6.4).</summary>
    public enum AIDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Expert = 3,
        /// <summary>Experto mejorado y sin fallos humanos, para quien busque un reto muy difícil (2026-10-12).</summary>
        Impossible = 4
    }

    /// <summary>Cuándo activa la IA su especial (GDD 6.4; el buen momento de cada poder llega en D5).</summary>
    public enum AISpecialUse
    {
        /// <summary>De vez en cuando, cuando lo tiene lleno.</summary>
        Random = 0,
        /// <summary>En cuanto lo tiene lleno.</summary>
        WhenFull = 1,
        /// <summary>Lleno y con el rival cerca.</summary>
        GoodMoment = 2
    }

    /// <summary>
    /// Parámetros de un nivel de IA (GDD 6.4). La IA puede leer cualquier dato interno del juego;
    /// cada nivel decide cuáles ignora (los "Lee...") y con qué retraso y error reacciona.
    /// Provisional en código: en D2 pasa a un asset de datos.
    /// </summary>
    [System.Serializable]
    public sealed class AILevel
    {
        /// <summary>Segundos hasta reaccionar a un ataque y entre decisiones.</summary>
        public float reactionTime;
        /// <summary>Error máximo de puntería al atacar (grados, a cada lado).</summary>
        public float aimErrorDegrees;
        /// <summary>Probabilidad de atacar en cada decisión con el rival a tiro.</summary>
        public float attackChance;
        /// <summary>De esos ataques, los que son cargados (si tiene 2 cargas o más).</summary>
        public float chargedChance;
        /// <summary>Probabilidad de intentar un parry ante un ataque que llega.</summary>
        public float parryChance;
        /// <summary>Error al lanzar el parry (s, a cada lado): demasiado pronto o tarde, falla.</summary>
        public float parryTimingError;
        /// <summary>Probabilidad de esquivar con dash un ataque que llega (si no intenta el parry).</summary>
        public float dodgeChance;
        /// <summary>Ve al rival cargar y se prepara antes de que lance el ataque.</summary>
        public bool readsCharging;
        /// <summary>Apunta a donde estará el rival (según su velocidad), no a donde está.</summary>
        public bool predictsMovement;
        /// <summary>Mira las cargas del rival: sin cargas no puede hacer parry, así que ataca más.</summary>
        public bool readsResources;
        /// <summary>Se aparta de la pared.</summary>
        public bool watchesWalls;
        /// <summary>Sin atacar, guarda distancia con el rival para tener tiempo de reaccionar (el principiante va directo).</summary>
        public bool keepsDistance;
        /// <summary>
        /// Capa táctica: decide cuánto arriesgar (agresividad) según la vida de cada una, los parrys que le hace
        /// el rival, si el rival está quieto o cerca de la pared; y coge carrerilla para los golpes fuertes.
        /// </summary>
        public bool tactical;
        /// <summary>
        /// Juega su perfil de arquetipo entero (D3): carrerillas, golpes y huidas, contraataques. Sin él (Fácil),
        /// solo se nota la tendencia del perfil: dónde se coloca y cuánto ataca o carga.
        /// </summary>
        public bool fullProfile;

        // Fallos humanos (2026-10-12): para que no lea los movimientos del rival de forma perfecta
        /// <summary>Ve la posición y la velocidad del rival con este retraso (s): las fintas y los cambios de dirección la engañan.</summary>
        public float perceptionDelay;
        /// <summary>Error de la predicción al apuntar adelantado (fracción, a cada lado): calcula de más o de menos.</summary>
        public float predictionError;
        /// <summary>Segundos de media entre despistes (0 = nunca): deja de corregir el rumbo y no reacciona a los ataques.</summary>
        public float lapseInterval;
        /// <summary>Lo que dura un despiste (s).</summary>
        public float lapseDuration;
        /// <summary>Deriva del stick al moverse (grados, a cada lado): no va en línea perfecta.</summary>
        public float steerWobbleDegrees;
        /// <summary>
        /// Probabilidad por segundo de pasarse mientras rodea al rival o llega a él: sigue recta un momento
        /// (abre el círculo y puede rozar la pared, o se pasa de frenada).
        /// </summary>
        public float overshootRate;
        public AISpecialUse specialUse;

        private static readonly AILevel[] Levels =
        {
            // Fácil: lenta, falla, nunca hace parry ni esquiva y no mira la pared
            new AILevel { reactionTime = 0.5f, aimErrorDegrees = 25f, attackChance = 0.45f, chargedChance = 0.15f,
                parryChance = 0f, parryTimingError = 0f, dodgeChance = 0f, readsCharging = false, predictsMovement = false,
                readsResources = false, watchesWalls = false, keepsDistance = false, tactical = false, fullProfile = false, specialUse = AISpecialUse.Random,
                perceptionDelay = 0.35f, predictionError = 0f, lapseInterval = 5f, lapseDuration = 0.6f, steerWobbleDegrees = 20f, overshootRate = 0.15f },
            // Normal: parry a veces, ve las cargas del rival venir
            new AILevel { reactionTime = 0.3f, aimErrorDegrees = 14f, attackChance = 0.6f, chargedChance = 0.25f,
                parryChance = 0.25f, parryTimingError = 0.08f, dodgeChance = 0.2f, readsCharging = true, predictsMovement = false,
                readsResources = false, watchesWalls = true, keepsDistance = true, tactical = false, fullProfile = true, specialUse = AISpecialUse.WhenFull,
                perceptionDelay = 0.25f, predictionError = 0f, lapseInterval = 8f, lapseDuration = 0.5f, steerWobbleDegrees = 12f, overshootRate = 0.08f },
            // Difícil: parry a menudo, apunta adelantado y mira los recursos del rival
            new AILevel { reactionTime = 0.18f, aimErrorDegrees = 7f, attackChance = 0.75f, chargedChance = 0.3f,
                parryChance = 0.45f, parryTimingError = 0.05f, dodgeChance = 0.35f, readsCharging = true, predictsMovement = true,
                readsResources = true, watchesWalls = true, keepsDistance = true, tactical = true, fullProfile = true, specialUse = AISpecialUse.GoodMoment,
                perceptionDelay = 0.15f, predictionError = 0.35f, lapseInterval = 14f, lapseDuration = 0.4f, steerWobbleDegrees = 6f, overshootRate = 0.02f },
            // Experto: parry a menudo y casi a tiempo, pero no siempre (2026-10-11: con 90% era imbatible)
            new AILevel { reactionTime = 0.1f, aimErrorDegrees = 2f, attackChance = 0.85f, chargedChance = 0.35f,
                parryChance = 0.7f, parryTimingError = 0.03f, dodgeChance = 0.5f, readsCharging = true, predictsMovement = true,
                readsResources = true, watchesWalls = true, keepsDistance = true, tactical = true, fullProfile = true, specialUse = AISpecialUse.GoodMoment,
                perceptionDelay = 0.08f, predictionError = 0.15f, lapseInterval = 22f, lapseDuration = 0.3f, steerWobbleDegrees = 3f, overshootRate = 0.01f },
            // Imposible: como el Experto pero mejor y sin ningún fallo humano; su parry aún falla a veces (85%, ±0,02 s)
            new AILevel { reactionTime = 0.08f, aimErrorDegrees = 0f, attackChance = 0.85f, chargedChance = 0.35f,
                parryChance = 0.85f, parryTimingError = 0.02f, dodgeChance = 0.6f, readsCharging = true, predictsMovement = true,
                readsResources = true, watchesWalls = true, keepsDistance = true, tactical = true, fullProfile = true, specialUse = AISpecialUse.GoodMoment,
                perceptionDelay = 0f, predictionError = 0f, lapseInterval = 0f, lapseDuration = 0f, steerWobbleDegrees = 0f, overshootRate = 0f },
        };

        public static AILevel Get(AIDifficulty difficulty) =>
            Levels[UnityEngine.Mathf.Clamp((int)difficulty, 0, Levels.Length - 1)];
    }
}
