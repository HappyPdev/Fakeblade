using System;
using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>Efectos de partículas del juego.</summary>
    public enum VfxType
    {
        Clash = 0,
        WallHit = 1,
        Dash = 2,
        Attack = 3,
        SpinOut = 4,
        SpecialBurst = 5,
        /// <summary>Aura genérica (poder sin aura propia).</summary>
        SpecialAura = 6,
        Respawn = 7,
        Parry = 8,

        // Aura de cada poder mientras está activo (GDD 5)
        AuraSpinBoost = 9,
        AuraShockWave = 10,
        AuraDefense = 11,
        AuraLightning = 12,

        // Ataque cargado (GDD 2.3)
        ChargeGather = 13,
        ChargeRing = 14,
        ChargeFlame = 15,

        // Peonza en movimiento y con RPM bajas (GDD 2.1)
        Trail = 16,
        GroundSpark = 17,
        LowSpinSmoke = 18,
        LowSpinSpark = 19,

        /// <summary>Destellos en estrella (carga al máximo, escudo...).</summary>
        Sparkle = 20

        // Los impactos con sprites animados no van aquí: tienen sus propios emisores (VfxLibrary.impactSprites)
    }

    /// <summary>
    /// Sprite animado de impacto (hoja de fotogramas en rejilla), 2026-10-17. Cada uno tiene su emisor; las capas de
    /// cada tipo de impacto (VfxLibrary) lo usan por su id. El prefab y el material los crea *Setup Impactos (sprites)*.
    /// </summary>
    [Serializable]
    public class ImpactSprite
    {
        [Tooltip("Nombre con el que lo usan las capas")]
        public string id;
        public Texture2D texture;
        [Min(1)] public int tilesX = 3;
        [Min(1)] public int tilesY = 3;
        [Tooltip("Primera casilla con dibujo (0 = la de arriba a la izquierda; algunas hojas empiezan vacías)")]
        [Min(0)] public int firstFrame;
        [Tooltip("Fotogramas con dibujo desde el primero (las últimas casillas de la rejilla pueden estar vacías)")]
        [Min(1)] public int frames = 9;
        [Tooltip("Lo que dura la animación (s)")]
        [Min(0.05f)] public float life = 0.3f;
        [Tooltip("Gira al azar en cada impacto (todas las capas del mismo impacto giran igual). Si no, siempre derecho")]
        public bool randomRotation = true;
        [Tooltip("Orden de pintado entre impactos: más alto, más delante (las sombras, por detrás)")]
        public int order;
        [Tooltip("Lo tapa lo que tenga delante (capa VFX). Si no, siempre por encima (capa VFXTop, como los impactos). " +
                 "Para las partículas que deja la peonza")]
        public bool depthTested;
        [Tooltip("Gravedad de las partículas (1 = la normal; negativo = suben). Solo para las que salen con velocidad")]
        public float gravity;
        [Tooltip("Frenado de las partículas (0 = nada, 1 = se paran casi enseguida)")]
        [Range(0f, 1f)] public float drag;
        [Tooltip("Lo crea Setup Impactos (sprites) al cambiar la textura, la rejilla o la duración")]
        public ParticleSystem prefab;
    }

    /// <summary>Una capa de un impacto: qué sprite, a qué tamaño (sobre el del impacto) y de qué color.</summary>
    [Serializable]
    public class ImpactLayer
    {
        [Tooltip("Id del sprite (lista Impact Sprites)")]
        public string sprite;
        [Tooltip("Tamaño respecto al del impacto (1 = igual)")]
        [Min(0f)] public float size = 1f;
        [Tooltip("Color según el daño (blanco → amarillo → rojizo). Si no, el color fijo de abajo")]
        public bool damageColor = true;
        [Tooltip("Color del jugador que hace el parry o el bloqueo (manda sobre los otros dos)")]
        public bool playerColor;
        [Tooltip("Color fijo, o solo su transparencia (alfa) si usa el color del daño o del jugador")]
        public Color color = Color.white;
        [Tooltip("Aclara el color hacia el blanco (0 = nada, 1 = blanco)")]
        [Range(0f, 1f)] public float lighten;
        [Tooltip("Giro extra respecto a las demás capas del impacto (grados)")]
        public float rotation;
    }

    /// <summary>
    /// Rastro tipo cinta pegado a la peonza (K7, 2026-10-17), teñido con el color del jugador. Aparece al pasar de cierta
    /// velocidad; cuanto más rápido, más larga, más ancha y más opaca. Hoy es una cinta **lisa** (material
    /// VFX_TrailBasic): la versión con el pixel art de los Bullet Trails (VFX_TrailGeneric, una tira de fotogramas
    /// animada) se descartó por ahora porque, tan larga, el dibujo se deformaba. Los materiales los crea *Setup Rastros*.
    /// Se lee en vivo: se puede ajustar en Play desde el Inspector.
    /// </summary>
    [Serializable]
    public class RibbonTrailStyle
    {
        [Tooltip("Material (shader FakeBlade/RibbonTrail): VFX_TrailBasic (lisa) o VFX_TrailGeneric (pixel art). Los crea Setup Rastros")]
        public Material material;
        [Tooltip("Fotogramas de la tira (en horizontal; 1 en la lisa)")]
        [Min(1)] public int frames = 1;
        [Tooltip("Da la vuelta al dibujo (con pixel art: la cabeza del proyectil tiene que quedar en la peonza)")]
        public bool flip = true;
        [Tooltip("Velocidad (m/s) a la que aparece (x) y a la que llega al máximo (y)")]
        public Vector2 speedRange = new Vector2(3f, 9f);
        [Tooltip("Fotogramas por segundo de la animación a la velocidad mínima (x) y máxima (y); solo con pixel art")]
        public Vector2 fps = new Vector2(8f, 24f);
        [Tooltip("Lo que dura cada punto de la cinta (s) a velocidad mínima (x) y máxima (y): más = más larga")]
        public Vector2 time = new Vector2(0.12f, 0.25f);
        [Tooltip("Ancho de la cinta (m) junto a la peonza, a velocidad mínima (x) y máxima (y)")]
        public Vector2 width = new Vector2(0.35f, 0.5f);
        [Tooltip("Ancho al final de la cinta, como fracción del de la cabeza (0 = acaba en punta)")]
        [Range(0f, 1f)] public float tailWidth = 0.2f;
        [Tooltip("Transparencia junto a la peonza a velocidad mínima (x) y máxima (y)")]
        public Vector2 alpha = new Vector2(0.55f, 0.85f);
        [Tooltip("Transparencia al final de la cinta, como fracción de la de la cabeza (0 = se desvanece)")]
        [Range(0f, 1f)] public float tailAlpha;
        [Tooltip("Aclara el color del jugador hacia el blanco (0 = nada, 1 = blanco). Sobre el suelo claro, mejor poco")]
        [Range(0f, 1f)] public float lighten;
        [Tooltip("Altura sobre la punta de la peonza (m)")]
        public float height = 0.04f;
        [Tooltip("Tumbada en el suelo. Si no, la cinta mira a la cámara")]
        public bool flat = true;

        [Header("Acelerón de ataques y dash (una segunda cinta igual, sumada a la normal)")]
        [Tooltip("Ancho de la cinta del acelerón respecto a la normal")]
        [Min(0f)] public float burstWidthScale = 2f;
        [Tooltip("Transparencia de la cinta del acelerón respecto a la normal")]
        [Range(0f, 1f)] public float burstAlphaScale = 0.8f;
    }

    /// <summary>
    /// Efecto en la punta durante el acelerón de ataques y dash (K7, 2026-10-17): varios cuencos (modelo ImpactPlane de
    /// Wallcoeur) abiertos hacia arriba, cada uno con su giro, su velocidad de giro, su escala y su color, con una hoja de
    /// fotogramas (ImpactShotValo2) que se repite en bucle, desfasada en cada cuenco; y partículas en cono hacia arriba
    /// que se quedan atrás como rastro. Malla y material los pone *Setup Rastros*. Se lee en vivo (también en Play).
    /// </summary>
    [Serializable]
    public class BurstTipStyle
    {
        [Tooltip("Modelo (ImpactPlane: rejilla curvada en cuenco, de 1,42 m de ancho)")]
        public Mesh mesh;
        [Tooltip("Material con la hoja de fotogramas (shader FakeBlade/FlipbookUnlit, pinta las dos caras)")]
        public Material material;
        [Min(1)] public int tilesX = 3;
        [Min(1)] public int tilesY = 3;
        [Tooltip("Fotogramas con dibujo desde el primero (como mucho, casillas en horizontal × en vertical: 9 en " +
                 "ImpactShotValo2). Para que vaya más rápida, bajar Cycle")]
        [Min(1)] public int frames = 9;
        [Tooltip("Lo que dura una vuelta de la animación (s); se repite mientras dura el acelerón")]
        [Min(0.05f)] public float cycle = 0.25f;
        [Tooltip("Ancho del cuenco (m). Más pequeño que la peonza, queda tapado por ella")]
        [Min(0f)] public float size = 2f;
        [Tooltip("Escala de todos los cuencos sobre ese tamaño: en ancho (x) y en alto (y). Cada cuenco tiene además la suya")]
        public Vector2 scale = Vector2.one;
        [Tooltip("Altura sobre la punta (m)")]
        public float height = 0.03f;
        [Tooltip("Transparencia de todos los cuencos (multiplica la del color de cada uno)")]
        [Range(0f, 1f)] public float alpha = 0.9f;
        [Tooltip("Aclara el color hacia el blanco (0 = nada, 1 = blanco)")]
        [Range(0f, 1f)] public float lighten;
        [Tooltip("Lo que tarda en aparecer (x) y en apagarse (y) al empezar y acabar el acelerón (s)")]
        public Vector2 fade = new Vector2(0.04f, 0.12f);
        [Tooltip("Cuencos (como mucho 8): cada uno con su giro, velocidad de giro, escala y color")]
        public List<BurstTipCup> cups = new List<BurstTipCup>();

        [Header("Partículas en cono hacia arriba (se quedan atrás: rastro)")]
        [Tooltip("Id del sprite (lista Impact Sprites: Valo8 = ImpatcValo8, Valo4 = ImpactValo4). Vacío = sin partículas. " +
                 "Su duración, gravedad y frenado por defecto están en el sprite (Setup Impactos)")]
        public string particleSprite = "Valo8";
        [Tooltip("Partículas por segundo durante el acelerón (antes del ajuste de Opciones → Partículas)")]
        [Min(0f)] public float particleRate = 30f;
        [Tooltip("Partículas por segundo fuera del acelerón, al ir por encima de Particle Idle Min Speed (0 = solo en el acelerón)")]
        [Min(0f)] public float particleIdleRate;
        [Tooltip("Velocidad (m/s) a partir de la que salen las partículas fuera del acelerón")]
        [Min(0f)] public float particleIdleMinSpeed = 6f;
        [Tooltip("Apertura del cono respecto a la vertical (grados)")]
        [Range(0f, 90f)] public float particleConeAngle = 25f;
        [Tooltip("Velocidad de salida (m/s), entre x e y")]
        public Vector2 particleSpeed = new Vector2(1.5f, 3f);
        [Tooltip("Parte de la velocidad de la peonza que se llevan (0 = se quedan donde salen; negativo = salen hacia atrás)")]
        [Range(-1f, 1f)] public float particleInheritVelocity;
        [Tooltip("Tamaño (m), entre x e y")]
        public Vector2 particleSize = new Vector2(0.5f, 0.9f);
        [Tooltip("Duración (s), entre x e y (0 = la del sprite). La animación de la hoja dura toda la vida")]
        public Vector2 particleLifetime = Vector2.zero;
        [Tooltip("Giro del dibujo (grados), entre x e y (0 = derecho)")]
        public Vector2 particleRotation = Vector2.zero;
        [Tooltip("Distancia al centro de la punta a la que salen (m)")]
        [Min(0f)] public float particleSpawnRadius = 0.3f;
        [Tooltip("Altura de salida sobre la punta (m)")]
        public float particleHeight;
        [Tooltip("Con el color del jugador. Si no, el color de abajo")]
        public bool particlePlayerColor = true;
        [Tooltip("Color fijo de las partículas (por defecto, el de las chispas contra el suelo)")]
        public Color particleColor = new Color(1f, 0.8f, 0.45f);
        [Tooltip("Aclara el color de las partículas hacia el blanco")]
        [Range(0f, 1f)] public float particleLighten;
        [Tooltip("Transparencia de las partículas")]
        [Range(0f, 1f)] public float particleAlpha = 0.9f;
    }

    /// <summary>Un cuenco del efecto de la punta (BurstTipStyle.cups).</summary>
    [Serializable]
    public class BurstTipCup
    {
        [Tooltip("Giro inicial alrededor del eje vertical (grados)")]
        public float angle;
        [Tooltip("Velocidad de giro alrededor del eje vertical (grados/s; negativo = hacia el otro lado)")]
        public float spinSpeed;
        [Tooltip("Escala sobre la general: en ancho (x) y en alto (y)")]
        public Vector2 scale = Vector2.one;
        [Tooltip("Con el color del jugador. Si no, el color de abajo")]
        public bool playerColor = true;
        [Tooltip("Color fijo (su alfa multiplica la transparencia general)")]
        public Color color = new Color(1f, 0.8f, 0.45f);
    }

    /// <summary>
    /// Biblioteca de efectos (GDD 7.3): un prefab de ParticleSystem por tipo y cuántas
    /// partículas emite una ráfaga normal. Vive en Resources para estar disponible en
    /// cualquier escena sin referencias.
    /// </summary>
    [CreateAssetMenu(fileName = "VfxLibrary", menuName = "FakeBlade/VFX Library")]
    public class VfxLibrary : ScriptableObject
    {
        public const string ResourcesPath = "VfxLibrary";

        [Serializable]
        public class Entry
        {
            public VfxType type;
            public ParticleSystem prefab;
            [Tooltip("Partículas por ráfaga a intensidad 1 (antes del ajuste de Opciones)")]
            [Min(1)] public int burstCount = 12;
        }

        public List<Entry> entries = new List<Entry>();

        // Los colores de los poderes están en su asset (SpecialAbilityData.color)

        [Header("Parry")]
        [Tooltip("Chispas del parry (blancas desde el 2026-10-17, con el impacto del parry)")]
        public Color parryColor = Color.white;

        [Header("Impactos (sprites animados, 2026-10-17)")]
        [Tooltip("Color del impacto con poco daño (por debajo de impactMidDamage)")]
        public Color impactColorLow = Color.white;
        [Tooltip("Color desde impactMidDamage: amarillo")]
        public Color impactColorMid = new Color(1f, 0.88f, 0.35f);
        [Tooltip("Color desde impactHighDamage: rojizo")]
        public Color impactColorHigh = new Color(1f, 0.45f, 0.35f);
        public float impactMidDamage = 10f;
        public float impactHighDamage = 30f;
        [Tooltip("Tamaño del impacto (m) con daño mínimo y con impactMaxDamage o más")]
        public Vector2 impactSize = new Vector2(1.1f, 2.4f);
        public float impactMaxDamage = 45f;
        [Tooltip("Los golpes cargados salen más grandes")]
        public float chargedImpactScale = 1.2f;
        [Tooltip("Daño mínimo para que salga el impacto (el roce no)")]
        public float impactMinDamage = 1f;
        [Tooltip("Altura del impacto sobre el suelo (m)")]
        public float impactHeight = 0.3f;
        [Tooltip("Cuánto se acerca el impacto a la cámara (m), para que se vea delante de las peonzas y no cortado por " +
                 "ellas. Más alto = sale más arriba en pantalla")]
        public float impactCameraOffset = 0.8f;
        [Tooltip("Tamaño del impacto del parry (las capas lo multiplican)")]
        public float parryImpactSize = 1.8f;
        [Tooltip("Tamaño del impacto del bloqueo (las capas lo multiplican)")]
        public float blockImpactSize = 1.8f;
        [Tooltip("Por debajo de este daño, el golpe usa las capas de golpe pequeño")]
        public float smallHitMaxDamage = 8f;

        [Header("Capas de cada impacto (se pintan todas a la vez, con la misma rotación)")]
        public List<ImpactSprite> impactSprites = new List<ImpactSprite>();
        public List<ImpactLayer> smallHitLayers = new List<ImpactLayer>();
        public List<ImpactLayer> hitLayers = new List<ImpactLayer>();
        public List<ImpactLayer> chargedLayers = new List<ImpactLayer>();
        public List<ImpactLayer> parryLayers = new List<ImpactLayer>();
        public List<ImpactLayer> blockLayers = new List<ImpactLayer>();

        public ImpactSprite GetImpactSprite(string id)
        {
            for (int i = 0; i < impactSprites.Count; i++)
                if (impactSprites[i] != null && impactSprites[i].id == id) return impactSprites[i];
            return null;
        }

        /// <summary>Color del impacto según el daño: blanco, amarillo desde el umbral medio y rojizo desde el alto.</summary>
        public Color ImpactColor(float damage)
        {
            if (damage >= impactHighDamage) return impactColorHigh;
            if (damage >= impactMidDamage)
                return Color.Lerp(impactColorMid, impactColorHigh, (damage - impactMidDamage) / Mathf.Max(1f, impactHighDamage - impactMidDamage) * 0.5f);
            return Color.Lerp(impactColorLow, impactColorMid, damage / Mathf.Max(1f, impactMidDamage) * 0.5f);
        }

        /// <summary>Tamaño del impacto según el daño.</summary>
        public float ImpactSize(float damage) =>
            Mathf.Lerp(impactSize.x, impactSize.y, Mathf.Clamp01(damage / Mathf.Max(1f, impactMaxDamage)));

        [Header("Estados alterados (partículas, icono y HUD)")]
        public Color burnColor = new Color(1f, 0.45f, 0.1f);
        public Color freezeColor = new Color(0.55f, 0.9f, 1f);
        public Color launchColor = new Color(1f, 0.95f, 0.3f);
        [Tooltip("Mojada (Rastro de agua): gotas e icono")]
        public Color wetColor = new Color(0.3f, 0.6f, 1f);
        [Tooltip("Partículas/s sobre la peonza con un estado alterado")]
        public float statusParticleRate = 22f;

        [Header("Ataque cargado")]
        [Tooltip("Color de las partículas en el nivel de carga máximo (el nivel 1 usa el color del jugador)")]
        public Color chargeHotColor = new Color(1f, 0.95f, 0.7f);
        [Tooltip("Partículas/s que convergen hacia la peonza en el nivel 1 (+50% por nivel)")]
        public float chargeGatherRate = 30f;
        [Tooltip("Llamas/s mientras la carga está al máximo")]
        public float chargeFlameRate = 26f;

        [Header("Rastro (cinta pegada a la peonza, K7)")]
        public RibbonTrailStyle genericTrail = new RibbonTrailStyle();
        [Tooltip("Efecto en la punta durante el acelerón de ataques y dash")]
        public BurstTipStyle burstTip = new BurstTipStyle();

        [Header("Estela (partículas)")]
        [Tooltip("Metros recorridos entre dos partículas de la estela (0,12 hasta K7; ahora la cinta es el rastro principal)")]
        public float trailSpacing = 0.45f;
        [Tooltip("Transparencia de las partículas de la estela (multiplica la de siempre; 1 = como antes de la cinta)")]
        [Range(0f, 1f)] public float trailAlpha = 0.4f;
        [Tooltip("Tamaño de las partículas de la estela (multiplica el de siempre)")]
        [Min(0f)] public float trailSize = 0.7f;
        [Tooltip("Velocidad mínima (m/s) para dejar estela")]
        public float trailMinSpeed = 2.5f;

        [Header("Chispas contra el suelo (giro rápido)")]
        public Color groundSparkColor = new Color(1f, 0.8f, 0.45f);
        [Tooltip("Chispas/s a velocidad y RPM altas")]
        public float groundSparkRate = 30f;
        [Tooltip("Velocidad (m/s) a partir de la que salen chispas")]
        public float groundSparkMinSpeed = 4f;
        [Tooltip("RPM (fracción) a partir de la que salen chispas")]
        [Range(0f, 1f)] public float groundSparkMinSpin = 0.5f;

        [Header("RPM bajas (umbral en CombatConfig.lowSpinThreshold)")]
        public Color smokeColor = new Color(0.32f, 0.32f, 0.36f, 0.85f);
        public Color lowSpinSparkColor = new Color(1f, 0.55f, 0.2f);
        [Tooltip("Bocanadas de humo/s cerca de 0 RPM")]
        public float lowSpinSmokeRate = 14f;
        [Tooltip("Chispas sueltas/s cerca de 0 RPM")]
        public float lowSpinSparkRate = 10f;

        public Entry Get(VfxType type)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].type == type) return entries[i];
            return null;
        }

        public Color GetStatusColor(StatusEffectType status)
        {
            switch (status)
            {
                case StatusEffectType.Burning: return burnColor;
                case StatusEffectType.Frozen: return freezeColor;
                case StatusEffectType.Launched: return launchColor;
                case StatusEffectType.Wet: return wetColor;
                default: return Color.white;
            }
        }
    }
}
