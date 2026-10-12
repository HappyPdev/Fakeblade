using UnityEngine;

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
    /// Un nivel de IA (GDD 6.4, D2): lo bien que juega, con cualquier perfil. La IA puede leer cualquier dato interno
    /// del juego; cada nivel decide cuáles ignora (los "Lee...") y con qué retraso y error reacciona.
    /// Un asset por nivel en Resources/AI/Levels (menú FakeBlade → Setup AI). La IA lee el asset en cada decisión,
    /// así que lo que se cambie en el Inspector en Play se nota al momento (y se queda guardado al salir de Play).
    /// Si falta un asset, se usan los valores por defecto de ApplyDefaults.
    /// </summary>
    [CreateAssetMenu(fileName = "AILevel", menuName = "FakeBlade/AI Level")]
    public sealed class AILevel : ScriptableObject
    {
        public const string ResourcesFolder = "AI/Levels";

        [Tooltip("Nivel al que corresponde este asset (uno por nivel)")]
        public AIDifficulty difficulty;

        [Header("=== HABILIDAD ===")]
        [Tooltip("Segundos hasta reaccionar a un ataque y entre decisiones")]
        public float reactionTime = 0.3f;
        [Tooltip("Error máximo de puntería al atacar (grados, a cada lado)")]
        public float aimErrorDegrees = 14f;
        [Tooltip("Probabilidad de atacar en cada decisión con el rival a tiro")]
        [Range(0f, 1f)] public float attackChance = 0.6f;
        [Tooltip("De esos ataques, los que son cargados (si tiene 2 cargas o más)")]
        [Range(0f, 1f)] public float chargedChance = 0.25f;
        [Tooltip("Probabilidad de intentar un parry ante un ataque que llega")]
        [Range(0f, 1f)] public float parryChance = 0.25f;
        [Tooltip("Error al lanzar el parry (s, a cada lado): demasiado pronto o tarde, falla")]
        public float parryTimingError = 0.08f;
        [Tooltip("Probabilidad de esquivar con dash un ataque que llega (si no intenta el parry)")]
        [Range(0f, 1f)] public float dodgeChance = 0.2f;
        [Tooltip("Cuándo activa el especial")]
        public AISpecialUse specialUse = AISpecialUse.WhenFull;

        [Header("=== QUÉ LEE ===")]
        [Tooltip("Ve al rival cargar y se prepara antes de que lance el ataque")]
        public bool readsCharging = true;
        [Tooltip("Apunta a donde estará el rival (según su velocidad), no a donde está")]
        public bool predictsMovement;
        [Tooltip("Mira las cargas del rival: sin cargas no puede hacer parry, así que ataca más")]
        public bool readsResources;
        [Tooltip("Se aparta de la pared")]
        public bool watchesWalls = true;
        [Tooltip("Sin atacar, guarda distancia con el rival para tener tiempo de reaccionar (el principiante va directo)")]
        public bool keepsDistance = true;
        [Tooltip("Capa táctica: decide cuánto arriesgar (agresividad) según la vida de cada una, los parrys que le hace " +
                 "el rival, si el rival está quieto o cerca de la pared; y coge carrerilla para los golpes fuertes")]
        public bool tactical;
        [Tooltip("Juega su perfil entero (D3): carrerillas, golpes y huidas, contraataques. Sin él (Fácil), " +
                 "solo se nota la tendencia del perfil: dónde se coloca y cuánto ataca o carga")]
        public bool fullProfile = true;

        [Header("=== FALLOS HUMANOS (2026-10-12) ===")]
        [Tooltip("Ve la posición y la velocidad del rival con este retraso (s): las fintas y los cambios de dirección la engañan")]
        public float perceptionDelay = 0.25f;
        [Tooltip("Error de la predicción al apuntar adelantado (fracción, a cada lado): calcula de más o de menos")]
        public float predictionError;
        [Tooltip("Segundos de media entre despistes (0 = nunca): deja de corregir el rumbo y no reacciona a los ataques")]
        public float lapseInterval = 8f;
        [Tooltip("Lo que dura un despiste (s)")]
        public float lapseDuration = 0.5f;
        [Tooltip("Deriva del stick al moverse (grados, a cada lado): no va en línea perfecta")]
        public float steerWobbleDegrees = 12f;
        [Tooltip("Probabilidad por segundo de pasarse mientras rodea al rival o llega a él: sigue recta un momento " +
                 "(abre el círculo y puede rozar la pared, o se pasa de frenada)")]
        public float overshootRate = 0.08f;

        private static AILevel[] _levels;

        /// <summary>El nivel en uso: su asset de Resources o, si falta, uno con los valores por defecto.</summary>
        public static AILevel Get(AIDifficulty difficulty)
        {
            if (_levels == null) Load();
            return _levels[Mathf.Clamp((int)difficulty, 0, _levels.Length - 1)];
        }

        /// <summary>Olvida los niveles cargados (al crear o borrar assets desde el editor).</summary>
        public static void ClearCache() => _levels = null;

        private static void Load()
        {
            int count = System.Enum.GetValues(typeof(AIDifficulty)).Length;
            _levels = new AILevel[count];
            foreach (AILevel level in Resources.LoadAll<AILevel>(ResourcesFolder))
            {
                int i = (int)level.difficulty;
                if (i >= 0 && i < count && _levels[i] == null) _levels[i] = level;
            }
            for (int i = 0; i < count; i++)
            {
                if (_levels[i] != null) continue;
                var level = CreateInstance<AILevel>();
                level.name = $"AILevel {(AIDifficulty)i} (Default)";
                level.hideFlags = HideFlags.DontSave;
                level.ApplyDefaults((AIDifficulty)i);
                _levels[i] = level;
            }
        }

        /// <summary>Valores por defecto de cada nivel (los de la tabla de 6.4 al pasarla a assets en D2).</summary>
        public void ApplyDefaults(AIDifficulty level)
        {
            difficulty = level;
            switch (level)
            {
                case AIDifficulty.Easy:
                    // Lenta, falla, nunca hace parry ni esquiva y no mira la pared
                    Set(0.5f, 25f, 0.45f, 0.15f, 0f, 0f, 0f, AISpecialUse.Random);
                    Reads(false, false, false, false, false, false, false);
                    Errors(0.35f, 0f, 5f, 0.6f, 20f, 0.15f);
                    break;
                case AIDifficulty.Normal:
                    // Parry a veces, ve las cargas del rival venir
                    Set(0.3f, 14f, 0.6f, 0.25f, 0.25f, 0.08f, 0.2f, AISpecialUse.WhenFull);
                    Reads(true, false, false, true, true, false, true);
                    Errors(0.25f, 0f, 8f, 0.5f, 12f, 0.08f);
                    break;
                case AIDifficulty.Hard:
                    // Parry a menudo, apunta adelantado y mira los recursos del rival
                    Set(0.18f, 7f, 0.75f, 0.3f, 0.45f, 0.05f, 0.35f, AISpecialUse.GoodMoment);
                    Reads(true, true, true, true, true, true, true);
                    Errors(0.15f, 0.35f, 14f, 0.4f, 6f, 0.02f);
                    break;
                case AIDifficulty.Expert:
                    // Parry a menudo y casi a tiempo, pero no siempre (2026-10-11: con 90% era imbatible)
                    Set(0.1f, 2f, 0.85f, 0.35f, 0.7f, 0.03f, 0.5f, AISpecialUse.GoodMoment);
                    Reads(true, true, true, true, true, true, true);
                    Errors(0.08f, 0.15f, 22f, 0.3f, 3f, 0.01f);
                    break;
                default:
                    // Imposible: como el Experto pero mejor y sin ningún fallo humano; su parry aún falla a veces (85%, ±0,02 s)
                    Set(0.08f, 0f, 0.85f, 0.35f, 0.85f, 0.02f, 0.6f, AISpecialUse.GoodMoment);
                    Reads(true, true, true, true, true, true, true);
                    Errors(0f, 0f, 0f, 0f, 0f, 0f);
                    break;
            }
        }

        private void Set(float reaction, float aimError, float attack, float charged, float parry, float parryError,
            float dodge, AISpecialUse special)
        {
            reactionTime = reaction;
            aimErrorDegrees = aimError;
            attackChance = attack;
            chargedChance = charged;
            parryChance = parry;
            parryTimingError = parryError;
            dodgeChance = dodge;
            specialUse = special;
        }

        private void Reads(bool charging, bool predicts, bool resources, bool walls, bool distance, bool isTactical, bool full)
        {
            readsCharging = charging;
            predictsMovement = predicts;
            readsResources = resources;
            watchesWalls = walls;
            keepsDistance = distance;
            tactical = isTactical;
            fullProfile = full;
        }

        private void Errors(float delay, float prediction, float lapseEvery, float lapseFor, float wobble, float overshoot)
        {
            perceptionDelay = delay;
            predictionError = prediction;
            lapseInterval = lapseEvery;
            lapseDuration = lapseFor;
            steerWobbleDegrees = wobble;
            overshootRate = overshoot;
        }
    }
}
