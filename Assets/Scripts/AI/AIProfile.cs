using System;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Comportamiento de un perfil de IA: la clase de AIBrain que lo juega (AIProfiles.BrainType).
    /// Un comportamiento nuevo es una clase que hereda de AIBrain (o de un perfil) y una entrada aquí.
    /// Los cuatro primeros tienen el mismo orden que BladeArchetype.
    /// </summary>
    public enum AIStyle
    {
        Balanced = 0,
        Attack = 1,
        Defense = 2,
        Agility = 3
    }

    /// <summary>Valores del comportamiento de Ataque (AttackAIBrain): presión y carrerillas.</summary>
    [Serializable]
    public class AIPressureStyle
    {
        [Tooltip("Probabilidad de coger carrerilla también para un ataque rápido (para los cargados, siempre que haya sitio)")]
        [Range(0f, 1f)] public float quickRunUpChance = 0.4f;
    }

    /// <summary>Valores del comportamiento de Defensa (DefenseAIBrain): dominar el centro y contraatacar.</summary>
    [Serializable]
    public class AICenterStyle
    {
        [Tooltip("Se coloca a esta distancia del centro (m), del lado del rival (le corta el paso)")]
        public float centerHold = 1f;
        [Tooltip("Segundos tras el ataque del rival en que contraataca")]
        public float counterWindow = 0.8f;
        [Tooltip("Si el rival no se acerca en estos segundos, sale a por él")]
        public float patience = 4f;
        [Tooltip("Con esta agresividad o más (va muy por detrás, rival quieto...) deja el centro")]
        [Range(0f, 1f)] public float leaveCenterAggression = 0.9f;
        [Tooltip("Ataque con hueco: el rival acaba de atacar, está aturdido o sin cargas (multiplica la probabilidad de atacar)")]
        public float openingAttackScale = 1.6f;
        [Tooltip("Ataque con el rival junto a la pared y la IA entre él y el centro (empotrarlo)")]
        public float pinAttackScale = 1.4f;
        [Tooltip("Ataque sin hueco: aguanta en el centro")]
        public float waitingAttackScale = 0.5f;
    }

    /// <summary>Valores del comportamiento de Agilidad (AgilityAIBrain): rodear, golpe y fuera, parrys.</summary>
    [Serializable]
    public class AIOrbitStyle
    {
        [Tooltip("Distancia entre centros a la que rodea al rival (m; a tiro son 4 m)")]
        public float orbitRadius = 3.6f;
        [Tooltip("Probabilidad de entrar con un dash en vez de embestir para el ataque rápido")]
        [Range(0f, 1f)] public float dashInChance = 0.3f;
        [Tooltip("Probabilidad de salir con un dash al acabar su ataque, si sigue pegada al rival")]
        [Range(0f, 1f)] public float dashOutChance = 0.3f;
        [Tooltip("Tiempo mínimo entre cambios de sentido al rodear (s; evita ir y venir junto a la pared)")]
        public float flipCooldown = 1f;
        [Tooltip("Probabilidad de acercarse con un dash a media distancia (sustituye a la general desde Normal)")]
        [Range(0f, 1f)] public float approachDashChance = 0.3f;
    }

    /// <summary>
    /// Un perfil de IA (GDD 6.4, D2): cómo juega, con cualquier nivel. Elige su comportamiento (la clase de IA) y
    /// sus valores; el nivel (AILevel) da la habilidad y el perfil la multiplica. Un asset por perfil en
    /// Resources/AI/Profiles (menú FakeBlade → Setup AI). Un rival nuevo con su forma de jugar es otro asset: se
    /// duplica un perfil y se cambian sus valores; si necesita algo que ningún comportamiento hace, una clase nueva.
    /// La IA lee el asset en cada decisión, así que lo que se cambie en el Inspector en Play se nota al momento
    /// (salvo el comportamiento, que se aplica al volver a poner la IA). Si faltan, se usan los valores por defecto.
    /// </summary>
    [CreateAssetMenu(fileName = "AIProfile", menuName = "FakeBlade/AI Profile")]
    public sealed class AIProfile : ScriptableObject
    {
        public const string ResourcesFolder = "AI/Profiles";

        [Tooltip("Clave de Loc del nombre que sale en el Sandbox (p. ej. ARCHETYPE_ATTACK)")]
        public string nameKey = "ARCHETYPE_BALANCED";
        [Tooltip("Orden en las listas. El primero de cada comportamiento es el que se usa «según peonza» " +
                 "(los cuatro de los arquetipos van del 0 al 3)")]
        public int order;
        [Tooltip("Comportamiento: la clase de IA que juega este perfil")]
        public AIStyle style;

        [Header("=== HABILIDAD (multiplica la del nivel) ===")]
        [Tooltip("Probabilidad de parry")]
        public float parryScale = 1f;
        [Tooltip("Probabilidad de esquivar con dash")]
        public float dodgeScale = 1f;
        [Tooltip("Tiempo de reacción (menos = más rápida)")]
        public float reactionScale = 1f;

        [Header("=== ATAQUE ===")]
        [Tooltip("Multiplica la probabilidad de atacar con el rival a tiro")]
        public float attackChanceScale = 1f;
        [Tooltip("Multiplica la probabilidad de que el ataque sea cargado")]
        public float chargedChanceScale = 1f;
        [Tooltip("Se suma a la agresividad de la capa táctica (0,5 = neutral)")]
        [Range(-0.5f, 0.5f)] public float aggressionBias;

        [Header("=== COLOCACIÓN ===")]
        [Tooltip("Distancia entre centros que guarda mientras no ataca (m): pegada al rival, un ataque suyo llega " +
                 "antes de poder reaccionar")]
        public float keepDistance = 2.8f;
        [Tooltip("Probabilidad de acercarse con un dash a media distancia")]
        [Range(0f, 1f)] public float approachDashChance = 0.15f;

        [Header("=== VALORES DE CADA COMPORTAMIENTO (solo usa los suyos) ===")]
        [Tooltip("Ataque: presión y carrerillas")]
        public AIPressureStyle pressure = new AIPressureStyle();
        [Tooltip("Defensa: dominar el centro y contraatacar")]
        public AICenterStyle center = new AICenterStyle();
        [Tooltip("Agilidad: rodear, golpe y fuera")]
        public AIOrbitStyle orbit = new AIOrbitStyle();

        /// <summary>Valores por defecto de los cuatro perfiles de arquetipo (tabla de 6.4, D3).</summary>
        public void ApplyDefaults(AIStyle profileStyle)
        {
            style = profileStyle;
            order = (int)profileStyle;
            pressure = new AIPressureStyle();
            center = new AICenterStyle();
            orbit = new AIOrbitStyle();
            keepDistance = 2.8f;
            approachDashChance = 0.15f;
            switch (profileStyle)
            {
                case AIStyle.Attack:
                    // Presión: guarda menos distancia, ataca y carga más, para menos
                    nameKey = "ARCHETYPE_ATTACK";
                    Set(0.7f, 0.8f, 1f, 1.25f, 1.6f, 0.15f);
                    keepDistance = 2.2f;
                    break;
                case AIStyle.Defense:
                    // Aguanta el golpe: para algo más y casi no esquiva; sin el perfil entero (Fácil) ataca ×0,8
                    nameKey = "ARCHETYPE_DEFENSE";
                    Set(1.1f, 0.5f, 1f, 0.8f, 1.2f, -0.1f);
                    break;
                case AIStyle.Agility:
                    // Parrys y esquivas, reacción algo más rápida, casi no carga
                    nameKey = "ARCHETYPE_AGILITY";
                    Set(1.3f, 1.3f, 0.9f, 1f, 0.5f, 0f);
                    break;
                default:
                    nameKey = "ARCHETYPE_BALANCED";
                    Set(1f, 1f, 1f, 1f, 1f, 0f);
                    break;
            }
        }

        private void Set(float parry, float dodge, float reaction, float attack, float charged, float bias)
        {
            parryScale = parry;
            dodgeScale = dodge;
            reactionScale = reaction;
            attackChanceScale = attack;
            chargedChanceScale = charged;
            aggressionBias = bias;
        }
    }
}
