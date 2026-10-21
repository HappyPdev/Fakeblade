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
        [Tooltip("Tumbado en el suelo (mira hacia arriba) en vez de mirar a la cámara. Mejor con Depth Tested")]
        public bool flat;
        [Tooltip("Gravedad de las partículas (1 = la normal; negativo = suben). Solo para las que salen con velocidad")]
        public float gravity;
        [Tooltip("Frenado de las partículas (0 = nada, 1 = se paran casi enseguida)")]
        [Range(0f, 1f)] public float drag;
        [Tooltip("Parte final de la vida en la que se desvanece (0 = de golpe; 0,6 = el último 60%). Para las manchas")]
        [Range(0f, 1f)] public float fadeOut;
        [Tooltip("Parte del principio de la vida en la que aparece poco a poco (0 = de golpe). Para el vaho")]
        [Range(0f, 1f)] public float fadeIn;
        [Tooltip("Parte final de la vida en la que se encoge hasta desaparecer (0 = no se encoge; 0,7 = el último 70%). Para la ceniza")]
        [Range(0f, 1f)] public float shrinkOut;
        [Tooltip("Parte del principio de la vida en la que crece desde nada hasta su tamaño, frenando al final (0 = sale " +
                 "entero; 1 = crece toda la vida). Para la onda en el suelo de Onda de choque")]
        [Range(0f, 1f)] public float growIn;
        [Tooltip("Se estira en la dirección en la que va (0 = no; 0,05 = un poco). Para las líneas de velocidad")]
        [Min(0f)] public float stretch;
        [Tooltip("Mira hacia donde va: el dibujo se gira con su velocidad (también al caer con la gravedad), sin estirarse si " +
                 "Stretch es 0. El giro con el que sale (Rotation de la partícula) se suma: con un dibujo que apunta hacia arriba " +
                 "en la textura (ImpactValo7), -90 = la punta va delante; 90 = detrás. No usar con Align To Motion. Sin Drag (o " +
                 "muy poco): el frenado crece con el tamaño y la velocidad y la deja quieta, sin dirección")]
        public bool alignToVelocity;
        [Tooltip("La hoja se anima al revés (un círculo que se abre pasa a cerrarse). Para la carga de Onda de choque")]
        public bool reverse;
        [Tooltip("Cada partícula sale con un dibujo de la hoja al azar y no se anima (HumoComic: 4 nubes distintas)")]
        public bool randomFrame;
        [Header("Disolución y color a lo largo de la vida (humo de Onda de choque, como el de CFXR Explosion 1)")]
        [Tooltip("Ruido con el que se disuelve (shader FakeBlade/ParticleDissolve): al desvanecerse (Fade Out o el alfa del " +
                 "degradado), en vez de transparentarse se rompe en pedacitos que se van haciendo pequeños. Vacío = se transparenta")]
        public Texture2D dissolveTexture;
        [Tooltip("Borde del corte de la disolución (0 = duro, pixel art)")]
        [Range(0f, 1f)] public float dissolveSmooth = 0.05f;
        [Tooltip("Tiñe con un degradado a lo largo de la vida (multiplica el color con el que sale: blanco = los colores del " +
                 "degradado). Su alfa manda sobre Fade In y Fade Out")]
        public bool useColorOverLife;
        public Gradient colorOverLife = new Gradient();
        [Tooltip("Giro de cada partícula (grados/s; negativo = hacia el otro lado), que se frena hasta la quinta parte al " +
                 "final de su vida, como por inercia (0 = no gira). Para el vaho")]
        public float spin;
        [Tooltip("Cuántas puede haber a la vez (entre todas las peonzas)")]
        [Min(1)] public int maxParticles = 128;
        [Tooltip("Lo crea Setup Impactos (sprites) al cambiar la textura, la rejilla o la duración")]
        public ParticleSystem prefab;
    }

    /// <summary>
    /// Ráfaga de partículas animadas en cada impacto (2026-10-17): el mismo sprite que las del cono de la punta
    /// (ImpatcValo8), en cono hacia arriba desde el punto del choque. Más partículas cuanto más daño. Color del daño en
    /// golpes y cargados; el de quien para en parry y bloqueo. Se lee en vivo.
    /// </summary>
    [Serializable]
    public class ImpactParticleStyle
    {
        [Tooltip("Id del sprite (lista Impact Sprites). Vacío = sin partículas en los impactos")]
        public string sprite = "Valo8";
        [Tooltip("Partículas de un golpe con el daño mínimo (x) y con impactMaxDamage o más (y)")]
        public Vector2 hitCount = new Vector2(3f, 10f);
        [Tooltip("Multiplica las partículas de un golpe cargado")]
        [Min(0f)] public float chargedScale = 1.5f;
        [Tooltip("Partículas de un parry")]
        [Min(0)] public int parryCount = 8;
        [Tooltip("Partículas de un bloqueo")]
        [Min(0)] public int blockCount = 6;
        [Tooltip("Apertura del cono alrededor de su eje (grados). El eje sale hacia fuera de la peonza golpeada (o de " +
                 "quien para), por el lado del choque, levantado Elevation grados")]
        [Range(0f, 90f)] public float coneAngle = 35f;
        [Tooltip("Cuánto sube el eje del cono sobre el suelo (grados; 90 = vertical)")]
        [Range(0f, 90f)] public float elevation = 25f;
        [Tooltip("Velocidad de salida (m/s), entre x e y")]
        public Vector2 speed = new Vector2(2f, 6f);
        [Tooltip("Tamaño (m), entre x e y; los golpes con más daño, hacia y")]
        public Vector2 size = new Vector2(0.3f, 0.7f);
        [Tooltip("Duración (s), entre x e y (0 = la del sprite)")]
        public Vector2 lifetime = Vector2.zero;
        [Tooltip("Giro del dibujo (grados), entre x e y (0 = derecho)")]
        public Vector2 rotation = Vector2.zero;
        [Tooltip("Distancia al punto del choque a la que salen (m)")]
        [Min(0f)] public float spawnRadius = 0.2f;
        [Tooltip("Altura de salida sobre el suelo (m)")]
        public float height = 0.1f;
        [Tooltip("Aclara el color hacia el blanco")]
        [Range(0f, 1f)] public float lighten;
        [Tooltip("Transparencia")]
        [Range(0f, 1f)] public float alpha = 1f;
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
        [Tooltip("Primera casilla del bucle (0 = la de arriba a la izquierda): para repetir solo una parte de la hoja")]
        [Min(0)] public int firstFrame;
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
        [Header("Hacia delante (Embestida: como el fuego de entrar en la atmósfera)")]
        [Tooltip("Los cuencos miran hacia la marcha en vez de hacia arriba: abiertos hacia la peonza, delante de ella (sin los " +
                 "sprites del suelo, que se torcerían)")]
        public bool pointForward;
        [Tooltip("Metros hacia delante en la marcha a los que va el cuenco (con Point Forward)")]
        public float forwardOffset = 0.5f;
        [Tooltip("Solo mientras arranca el poder (Embestida: mientras embiste; Salto: mientras vuela)")]
        public bool onlyWhileRushing;
        [Tooltip("Con Point Forward, miran hacia el punto al que va el poder en vez de hacia la marcha (Salto: hacia donde va a " +
                 "caer, cada vez más inclinados al bajar). Sin punto, hacia la marcha")]
        public bool aimAtTarget;

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

        [Header("Sprite animado en el suelo, debajo de la punta (sin material = no sale)")]
        public TipGroundSprite ground = new TipGroundSprite();
        [Tooltip("Más sprites en el suelo, encima del de arriba (Hielo: aro que late y medias lunas que giran; K8). " +
                 "Como mucho 16 dibujos entre todos, contando las copias")]
        public List<TipGroundSprite> groundLayers = new List<TipGroundSprite>();
    }

    /// <summary>
    /// Sprite animado tumbado en el suelo debajo de la punta (BurstTipStyle.ground y groundLayers; K8, 2026-10-17): una
    /// hoja de fotogramas que se repite en bucle y gira. Puede salir varias veces, cada copia girada respecto a la
    /// anterior (4 copias = una cada 90°), y latir (crece y brilla a golpes, como un corazón). Sale y se apaga con los
    /// cuencos.
    /// </summary>
    [Serializable]
    public class TipGroundSprite
    {
        [Tooltip("Material con la hoja de fotogramas (shader FakeBlade/FlipbookUnlit). Vacío = no sale")]
        public Material material;
        [Min(1)] public int tilesX = 8;
        [Min(1)] public int tilesY = 8;
        [Tooltip("Primera casilla del bucle (0 = la de arriba a la izquierda)")]
        [Min(0)] public int firstFrame;
        [Tooltip("Fotogramas del bucle desde el primero")]
        [Min(1)] public int frames = 61;
        [Tooltip("Lo que dura una vuelta de la animación (s)")]
        [Min(0.05f)] public float cycle = 1f;
        [Tooltip("Ancho del sprite (m; la casilla entera, aunque el dibujo ocupe menos)")]
        [Min(0f)] public float size = 3f;
        [Tooltip("Altura sobre la punta (m)")]
        public float height = 0.02f;
        [Tooltip("Giro (grados/s; negativo = hacia el otro lado)")]
        public float spinSpeed = 90f;
        [Tooltip("Con el color del jugador. Si no, el color de abajo (blanco = los colores del dibujo)")]
        public bool playerColor;
        public Color color = Color.white;
        [Tooltip("Transparencia")]
        [Range(0f, 1f)] public float alpha = 1f;
        [Header("Copias y latido")]
        [Tooltip("Copias del sprite, cada una girada 360/copias grados respecto a la anterior (4 = una cada 90°)")]
        [Min(1)] public int copies = 1;
        [Tooltip("Latidos por segundo (0 = no late). Cada latido son dos golpes seguidos, como un corazón")]
        [Min(0f)] public float pulseRate;
        [Tooltip("Cuánto crece en cada golpe (0,2 = un 20% más grande)")]
        [Min(0f)] public float pulseScale = 0.2f;
        [Tooltip("Cuánto brilla en cada golpe: fuera de los golpes, la transparencia baja hasta (1 − esto)")]
        [Range(0f, 1f)] public float pulseAlpha = 0.4f;
    }

    /// <summary>
    /// Efecto sobre la peonza mientras su especial está activo (K8/K9, 2026-10-17): impulsos del borde brillante que
    /// suben de golpe y se apagan, a ritmo irregular (como el parpadeo de una llama), y un sprite animado proyectado
    /// desde arriba sobre la peonza. Lo aplica BladeOverlay.
    /// </summary>
    [Serializable]
    public class SpecialOverlayStyle
    {
        public bool enabled = true;
        [Tooltip("Con el color del poder (su asset). Si no, Color y Hot Color")]
        public bool powerColor;
        public Color color = new Color(1f, 0.45f, 0.1f);
        [Tooltip("Color en lo más alto de cada impulso (cada uno, al azar entre los dos)")]
        public Color hotColor = new Color(1f, 0.85f, 0.3f);
        [Header("Impulsos del borde")]
        [Tooltip("Intensidad del borde entre impulsos (0 = solo los impulsos)")]
        [Min(0f)] public float baseRim = 0.6f;
        [Tooltip("Cuánto tiñe toda la peonza siempre, sin impulsos (0-1; la escarcha quieta de la congelada)")]
        [Range(0f, 1f)] public float baseFlash;
        [Tooltip("Intensidad que suma cada impulso, al azar entre x e y")]
        public Vector2 pulseRim = new Vector2(1f, 2.5f);
        [Tooltip("Cuánto tiñe cada impulso toda la peonza, al azar entre x e y (0-1)")]
        public Vector2 pulseFlash = new Vector2(0f, 0.25f);
        [Tooltip("Tiempo entre impulsos (s), al azar entre x e y")]
        public Vector2 pulseInterval = new Vector2(0.12f, 0.35f);
        [Tooltip("Lo que tarda en apagarse cada impulso (s)")]
        [Min(0.01f)] public float pulseDecay = 0.25f;
        [Header("Sprite animado proyectado desde arriba (sin textura = no sale)")]
        public Texture2D texture;
        [Min(1)] public int tilesX = 8;
        [Min(1)] public int tilesY = 8;
        [Tooltip("Primera casilla del bucle (0 = la de arriba a la izquierda)")]
        [Min(0)] public int firstFrame;
        [Tooltip("Fotogramas del bucle desde el primero")]
        [Min(1)] public int frames = 61;
        [Tooltip("Lo que dura una vuelta de la animación (s)")]
        [Min(0.05f)] public float cycle = 1f;
        [Tooltip("Tamaño respecto al radio de la peonza (la casilla entera, aunque el dibujo ocupe menos)")]
        [Min(0f)] public float textureScale = 2.2f;
        [Tooltip("Giro (grados/s)")]
        public float textureSpin = 120f;
        [Tooltip("Teñido del sprite (blanco = los colores del dibujo); su alfa es la transparencia")]
        public Color textureColor = Color.white;
    }

    /// <summary>
    /// Efectos de un especial mientras está activo (K8/K9, 2026-10-17, GDD 7.6): punta (cuencos, sprite en el suelo y
    /// partículas en cono, como el acelerón), efecto sobre la peonza y si sustituye a su aura de partículas de antes. Uno
    /// por poder en VfxLibrary.specialEffects; los que no tienen entrada siguen con su aura. Se lee en vivo.
    /// </summary>
    [Serializable]
    public class SpecialVfxStyle
    {
        public SpecialAbilityType type;
        [Tooltip("Quita el aura de partículas que tenía el poder antes de estos efectos (BladeParticles)")]
        public bool replaceAura = true;
        [Tooltip("Punta: cuencos, sprite en el suelo y partículas en cono, mientras dura el poder")]
        public BurstTipStyle tip = new BurstTipStyle();
        [Tooltip("Sobre la peonza: impulsos del borde y sprite animado")]
        public SpecialOverlayStyle overlay = new SpecialOverlayStyle();
        [Tooltip("Partículas mientras dura el poder (chispas y ceniza en el Fuego), además de las del cono de la punta")]
        public List<SpecialParticleStyle> particles = new List<SpecialParticleStyle>();
        [Tooltip("Manchas en el suelo por donde pasa mientras dura el poder (sin sprite = no deja)")]
        public ScorchStyle scorch = new ScorchStyle();
        [Tooltip("Auras de partículas de paquetes (Status and Auras FREE) pegadas a la peonza mientras dura el poder")]
        public List<AuraStyle> auras = new List<AuraStyle>();
        [Header("Al activarlo (poderes con carga corta: Fuego)")]
        [Tooltip("Mientras dura la carga del principio: partículas en remolino alrededor de la peonza")]
        public List<SpiralParticleStyle> windup = new List<SpiralParticleStyle>();
        [Tooltip("Al encenderse (al acabar la carga, o al activarlo si no tiene): explosión de partículas en espiral")]
        public List<SpiralParticleStyle> activationBurst = new List<SpiralParticleStyle>();
        [Tooltip("Después de la carga, el remolino sigue mientras dura el poder con esta parte de su ritmo (0 = se " +
                 "apaga; 0,25 = la cuarta parte): parece que el poder se escapa en partículas")]
        [Range(0f, 1f)] public float windupSustain = 0.25f;
        [Tooltip("Otro remolino propio mientras dura el poder, además del de la carga (Hielo: vaho a ras de suelo que gira " +
                 "y se queda posado). Usa Rate, no Count")]
        public List<SpiralParticleStyle> swirl = new List<SpiralParticleStyle>();
        [Header("Al chocar (además del impacto genérico)")]
        [Tooltip("En cada choque con daño mientras dura el poder: un círculo de partículas iguales alrededor del choque " +
                 "(sin velocidad de giro, salen rectas hacia fuera). Usa Count, no Rate")]
        public List<SpiralParticleStyle> impact = new List<SpiralParticleStyle>();
        [Tooltip("En el golpe fuerte (Rayos, que gasta el poder): un círculo más grande. Usa Count, no Rate")]
        public List<SpiralParticleStyle> strongImpact = new List<SpiralParticleStyle>();
        [Header("Arcos eléctricos (Rayos)")]
        [Tooltip("Arcos que salen del núcleo alrededor de la peonza (lámpara de plasma), por el suelo al moverse y hacia la " +
                 "otra peonza al chocar, mientras dura el poder. Sin material = no salen")]
        public ElectricArcStyle arcs = new ElectricArcStyle();
        [Header("Cúpula de la carga (Onda de choque; Tornado: embudo y cúpula mientras atrae)")]
        [Tooltip("Media esfera que crece sobre la peonza durante cada carga y revienta al acabarla. Sin material = no sale")]
        public ChargeDomeStyle dome = new ChargeDomeStyle();
        [Tooltip("Otra cúpula a la vez, por delante de la primera (Embestida: cilindro bajo en la base con una Zbam). Sin " +
                 "material = no sale")]
        public ChargeDomeStyle baseDome = new ChargeDomeStyle();
        [Header("Arrancada (Embestida, mientras embiste; Salto, mientras vuela)")]
        [Tooltip("Partículas solo mientras arranca (Embestida: media luna de aire delante, trozos de suelo y Zbam detrás). Con " +
                 "Align To Motion y Forward Offset se colocan y giran según la marcha")]
        public List<SpecialParticleStyle> rush = new List<SpecialParticleStyle>();
        [Tooltip("Líneas en espiral que giran alrededor de la peonza y dejan su rastro (Salto: mientras vuela). Sin material = no salen")]
        public SpiralTrailStyle spiralTrail = new SpiralTrailStyle();
        [Tooltip("Al encenderse, en el punto al que va el poder (Salto: donde va a caer). Con Lifetime 0, duran hasta que llega: " +
                 "un círculo que se cierra acaba de cerrarse al caer. Usa Count, no Rate")]
        public List<SpiralParticleStyle> targetMark = new List<SpiralParticleStyle>();
        [Header("Empujón a los rivales (Onda de choque, Tornado, Salto)")]
        [Tooltip("Al soltar el empujón (Tornado; sustituye a las estrellas comunes) o al caer (Salto; sustituye al estallido y la " +
                 "onda comunes): explosión en espiral alrededor de la peonza. Usa Count, no Rate")]
        public List<SpiralParticleStyle> release = new List<SpiralParticleStyle>();
        [Tooltip("En cada rival que alcanza el empujón del poder (cada onda): un círculo de partículas en el lado del golpe. " +
                 "Usa Count, no Rate")]
        public List<SpiralParticleStyle> pushHit = new List<SpiralParticleStyle>();
        [Tooltip("Partículas de la peonza que este poder ha dejado lanzada, mientras vuela (estela de polvo), además de las " +
                 "de la lanzada")]
        public List<SpecialParticleStyle> launchedTrail = new List<SpecialParticleStyle>();
        [Tooltip("Cuando una peonza que este poder ha dejado lanzada choca contra la pared: un círculo de partículas en el " +
                 "punto del choque. Usa Count, no Rate")]
        public List<SpiralParticleStyle> launchedWallHit = new List<SpiralParticleStyle>();
        [Header("Estela en la golpeada (2026-10-19)")]
        [Tooltip("En cada choque con daño mientras dura el poder, la peonza golpeada deja este rastro mientras sale despedida " +
                 "(llamas en el Fuego, esquirlas y vaho en el Hielo)")]
        public List<SpecialParticleStyle> hitTrail = new List<SpecialParticleStyle>();
        [Tooltip("Segundos que deja la estela tras cada golpe")]
        [Min(0f)] public float hitTrailTime = 0.4f;
    }

    /// <summary>
    /// Estela en espiral (Salto, K8, 2026-10-21; boceto del usuario): varias líneas que giran alrededor de la peonza, en el
    /// plano perpendicular a su marcha, y dejan una cinta cada una (TrailRenderer): una hélice que sigue el arco del salto.
    /// Las pinta BladeSpiralTrail. Se lee en vivo.
    /// </summary>
    [Serializable]
    public class SpiralTrailStyle
    {
        [Tooltip("Material de las cintas (shader FakeBlade/RibbonTrail: VFX_TrailBasic, lisa). Vacío = no salen")]
        public Material material;
        [Tooltip("Solo mientras arranca el poder (Salto: mientras vuela). Si no, mientras dura el poder")]
        public bool onlyWhileRushing = true;
        [Tooltip("Líneas (como mucho 4), repartidas alrededor")]
        [Range(1, 4)] public int strands = 2;
        [Tooltip("Distancia al eje de la marcha (m)")]
        [Min(0f)] public float radius = 0.55f;
        [Tooltip("Vueltas por segundo (negativo = hacia el otro lado)")]
        public float turnsPerSecond = 3f;
        [Tooltip("Altura del eje sobre la punta (m)")]
        public float height = 0.3f;
        [Tooltip("Ancho de cada línea junto a la peonza (m)")]
        [Min(0f)] public float width = 0.09f;
        [Tooltip("Ancho al final, como fracción del de la cabeza (0 = acaba en punta)")]
        [Range(0f, 1f)] public float tailWidth = 0.3f;
        [Tooltip("Lo que dura cada punto de la línea (s): más = rastro más largo")]
        [Min(0.01f)] public float time = 0.35f;
        [Tooltip("Color de las líneas 1 y 3")]
        public Color color = Color.white;
        [Tooltip("Color de las líneas 2 y 4")]
        public Color color2 = Color.white;
        [Tooltip("Transparencia al final, como fracción de la de la cabeza (0 = se desvanece)")]
        [Range(0f, 1f)] public float tailAlpha;
        [Tooltip("Cuánto toma el color del poder cada color (0 = el suyo; 1 = multiplicado por el del poder): x en las líneas 1 y " +
                 "3, y en las 2 y 4")]
        public Vector2 powerTint = new Vector2(0f, 1f);
    }

    /// <summary>
    /// Cúpula de la carga (Onda de choque, K8, 2026-10-18; boceto del usuario): media esfera translúcida que crece sobre la
    /// peonza durante la carga de cada onda, con el borde de cómic, líneas en remolino que giran por dentro y un temblor al
    /// final; al acabar la carga se infla y revienta con un destello. Shader FakeBlade/ChargeDome (material VFX_ChargeDome,
    /// lo crea Setup Rastros). Lo aplica BladeChargeDome. Se lee en vivo.
    /// </summary>
    [Serializable]
    public class ChargeDomeStyle
    {
        [Tooltip("Material (shader FakeBlade/ChargeDome). Vacío = no sale")]
        public Material material;
        [Tooltip("Radio al acabar la carga (m)")]
        [Min(0f)] public float radius = 1.2f;
        [Tooltip("Alto respecto al radio (1 = media esfera; menos = más aplastada)")]
        [Min(0f)] public float heightScale = 0.85f;
        [Tooltip("Altura de la base sobre la punta (m)")]
        public float height;
        [Tooltip("Cómo crece con la carga (1 = igual todo el rato; más de 1 = deprisa al principio y luego despacio)")]
        [Min(0.1f)] public float growEase = 2f;
        [Header("Colores (blanco con contrastes negros y naranja)")]
        [Tooltip("Relleno; su alfa es la transparencia")]
        public Color bodyColor = new Color(1f, 0.97f, 0.9f, 0.18f);
        [Tooltip("Borde de la silueta")]
        public Color rimColor = new Color(1f, 0.55f, 0.15f, 0.95f);
        [Tooltip("Línea fina por fuera del borde (contraste de cómic)")]
        public Color outlineColor = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        [Tooltip("Líneas del remolino")]
        public Color swirlColor = new Color(1f, 1f, 1f, 0.8f);
        [Tooltip("Líneas del remolino, las de en medio (contraste)")]
        public Color swirlColor2 = new Color(0.05f, 0.05f, 0.05f, 0.6f);
        [Header("Borde")]
        [Tooltip("Ancho del borde de la silueta (0-1)")]
        [Range(0f, 1f)] public float rimWidth = 0.3f;
        [Tooltip("Ancho de la línea negra del borde (0-1)")]
        [Range(0f, 1f)] public float outlineWidth = 0.08f;
        [Header("Textura que recorre la cúpula (líneas de velocidad)")]
        [Tooltip("Textura blanca sobre transparente que se desliza alrededor de la cúpula, teñida con Swirl Color: " +
                 "LineasVelocidad (líneas horizontales, la crea Setup Impactos) o una Zbam del pack de Wallcoeur. Vacío = solo " +
                 "las líneas dibujadas")]
        public Texture2D linesTexture;
        [Tooltip("Veces que se repite: alrededor (x) y de abajo arriba (y)")]
        public Vector2 linesTiling = new Vector2(3f, 2f);
        [Tooltip("Vueltas por segundo de la textura alrededor (negativo = hacia el otro lado)")]
        public float linesSpeed = 1.2f;
        [Tooltip("Cuánto se enrosca la textura de abajo arriba (vueltas)")]
        public float linesTwist = 0.15f;
        [Header("Eje del giro")]
        [Tooltip("Inclinación del eje alrededor del que giran las líneas (grados; 0 = vertical, por el centro)")]
        [Range(0f, 80f)] public float axisTilt = 25f;
        [Tooltip("Vueltas por segundo que da el eje inclinado (bamboleo, como una peonza; 0 = quieto)")]
        public float axisPrecess = 0.8f;
        [Header("Líneas dibujadas (sin textura)")]
        [Tooltip("Líneas del remolino alrededor")]
        [Min(0f)] public float swirlLines = 5f;
        [Tooltip("Cuánto se enroscan las líneas de abajo arriba (vueltas)")]
        public float swirlTwist = 0.6f;
        [Tooltip("Vueltas por segundo del remolino (negativo = hacia el otro lado)")]
        public float swirlSpeed = 1.5f;
        [Tooltip("Grosor de las líneas del remolino (0-1)")]
        [Range(0f, 1f)] public float swirlWidth = 0.18f;
        [Header("Final de la carga y explosión")]
        [Tooltip("Parte final de la carga en la que tiembla (0,3 = el último 30%)")]
        [Range(0f, 1f)] public float shakeFrom = 0.6f;
        [Tooltip("Cuánto crece y encoge al temblar (0,08 = un 8%)")]
        [Min(0f)] public float shakeScale = 0.08f;
        [Tooltip("Temblores por segundo")]
        [Min(0f)] public float shakeRate = 22f;
        [Tooltip("Lo que dura la explosión (s): se infla y se desvanece")]
        [Min(0.01f)] public float popTime = 0.16f;
        [Tooltip("Tamaño al final de la explosión respecto al de la carga")]
        [Min(1f)] public float popScale = 1.7f;
        [Tooltip("Destello blanco al reventar (0-1)")]
        [Range(0f, 1f)] public float popFlash = 0.8f;

        [Header("Embudo y picos de arriba (Tornado)")]
        [Tooltip("Radio al empezar, en partes del final (0 = crece desde nada, como en Onda de choque)")]
        [Range(0f, 1f)] public float startRadius;
        [Tooltip("Parte del principio en la que es un embudo que sube desde el suelo (0 = sin embudo: cúpula desde el principio)")]
        [Range(0f, 1f)] public float funnelPhase;
        [Tooltip("Parte que tarda después en pasar de embudo a cúpula")]
        [Range(0.01f, 1f)] public float funnelMorph = 0.2f;
        [Tooltip("Forma del embudo: radio abajo (x) y arriba (y), en partes del radio, y alto (z), en partes del alto de la cúpula")]
        public Vector3 funnelShape = new Vector3(0.25f, 1f, 2.5f);
        [Tooltip("Parte de arriba cortada en picos (0 = cerrada; 0,2 = aro de picos abierto arriba)")]
        [Range(0f, 0.6f)] public float topCut;
        [Tooltip("Picos del corte de arriba")]
        [Min(1)] public int topSpikes = 10;
        [Tooltip("Vueltas por segundo de los picos (giran con el viento)")]
        public float topSpikesSpeed = 0.5f;
        [Tooltip("Interior: transparencia de las caras de dentro, que se ven por el corte de arriba (0 = no se pintan, como en " +
                 "Onda de choque)")]
        [Range(0f, 1f)] public float insideAlpha;
    }

    /// <summary>
    /// Aviso de usos (2026-10-18): en los poderes con varios usos (Onda de choque: sus ondas), una marca por uso que queda
    /// girando alrededor de la peonza; la del uso que se lanza se va con un destello. Lo aplica BladeUsesIndicator. Sirve
    /// para cualquier poder que tenga MaxUses. Se lee en vivo.
    /// </summary>
    [Serializable]
    public class UsesIndicatorStyle
    {
        public bool enabled = true;
        [Tooltip("Material (shader FakeBlade/FlipbookUnlit con la marca; VFX_UseMark, lo crea Setup Rastros). Vacío = no sale")]
        public Material material;
        [Tooltip("Tamaño de cada marca (m)")]
        [Min(0f)] public float size = 0.32f;
        [Tooltip("Distancia al centro de la peonza (m)")]
        [Min(0f)] public float radius = 0.85f;
        [Tooltip("Altura sobre la punta (m)")]
        public float height = 0.55f;
        [Tooltip("Vueltas por segundo alrededor de la peonza")]
        public float spin = 0.5f;
        [Tooltip("Con el color del poder (su asset). Si no, el de abajo")]
        public bool powerColor = true;
        public Color color = Color.white;
        [Tooltip("Latidos por segundo (crecen y encogen un poco para que se vean; 0 = quietas)")]
        [Min(0f)] public float pulseRate = 2f;
        [Tooltip("Cuánto crecen al latir (0,15 = un 15%)")]
        [Min(0f)] public float pulseScale = 0.15f;
        [Tooltip("Lo que tarda en irse la marca del uso que se lanza (s): crece y se desvanece")]
        [Min(0.01f)] public float popTime = 0.25f;
    }

    /// <summary>
    /// Arcos eléctricos (Rayos, K8, 2026-10-17; idea del usuario: como una lámpara de plasma de Tesla): líneas quebradas
    /// que se vuelven a dibujar al azar cada pocas centésimas. Alrededor de la peonza salen del núcleo hasta puntos que se
    /// desplazan despacio a su alrededor (unos en el borde, otros más lejos); al moverse rápido dejan arcos a ras de suelo
    /// entre los puntos por donde pasa, que se apagan enseguida; y al chocar saltan arcos hasta la otra peonza. Cada arco
    /// es un halo ancho y translúcido más un núcleo fino y brillante (LineRenderer). Lo aplica BladeArcs. Se lee en vivo.
    /// </summary>
    [Serializable]
    public class ElectricArcStyle
    {
        public bool enabled = true;
        [Tooltip("Material de las líneas (VFX_Arc: transparente, sin textura; lo crea Setup Rastros). Vacío = no salen")]
        public Material material;
        [Tooltip("Color del halo")]
        public Color color = new Color(1f, 0.75f, 0.05f);
        [Tooltip("Color del núcleo de la línea")]
        public Color coreColor = new Color(1f, 0.97f, 0.6f);
        [Tooltip("Ancho base del halo (m); el trazo de cómic lo multiplica a lo largo del rayo")]
        [Min(0f)] public float glowWidth = 0.14f;
        [Tooltip("Transparencia del halo")]
        [Range(0f, 1f)] public float glowAlpha = 0.6f;
        [Tooltip("Ancho base del núcleo (m); el trazo de cómic lo multiplica a lo largo del rayo")]
        [Min(0f)] public float coreWidth = 0.045f;
        [Tooltip("Grosor mínimo de cada arco, en parte del de arriba (que es el máximo): cada arco sale con un grosor al azar")]
        [Range(0f, 1f)] public float minWidth = 0.35f;
        [Header("Trazo de cómic (grosor a lo largo del rayo)")]
        [Tooltip("Grosor en cada quiebro del zigzag, en veces el ancho base (Glow y Core Width; fino = los tramos parecen " +
                 "trazos de pincel)")]
        [Min(0f)] public float kinkWidth = 0.25f;
        [Tooltip("Grosor en el medio de cada tramo, en veces el ancho base, al azar entre x e y (más de 1 = más grueso que la base)")]
        public Vector2 midWidth = new Vector2(0.45f, 1f);
        [Tooltip("Grosor en los dos extremos del rayo, en parte del de alrededor (0 = en punta)")]
        [Range(0f, 1f)] public float endWidth = 0.1f;
        [Tooltip("Tramos de cada arco (más = más quebrado)")]
        [Range(2, 16)] public int segments = 12;
        [Tooltip("Cuánto se aparta cada pico del camino recto (m, en un rayo de 1 m; los cortos, menos)")]
        [Min(0f)] public float jitter = 0.22f;
        [Tooltip("Zigzag: posibilidad de que cada pico se vaya al lado contrario que el anterior (1 = zigzag puro, como en el " +
                 "boceto del usuario; 0 = al azar)")]
        [Range(0f, 1f)] public float zigzag = 0.85f;
        [Tooltip("Cuánto varía la longitud de cada tramo (0 = todos iguales)")]
        [Range(0f, 0.45f)] public float unevenSegments = 0.35f;
        [Tooltip("Cada cuánto se vuelve a dibujar cada arco (s), al azar entre x e y")]
        public Vector2 refresh = new Vector2(0.02f, 0.05f);
        [Tooltip("Posibilidad de que un arco se apague un momento al volver a dibujarse (parpadeo)")]
        [Range(0f, 1f)] public float flicker = 0.15f;
        [Header("Ramas y chispas")]
        [Tooltip("Posibilidad de que cada rayo saque una rama más corta desde algún punto (al volver a dibujarse)")]
        [Range(0f, 1f)] public float branchChance = 0.5f;
        [Tooltip("Largo de las ramas (m), al azar entre x e y")]
        public Vector2 branchLength = new Vector2(0.25f, 0.6f);
        [Tooltip("Posibilidad de que salte una chispa de cuatro puntas cerca del rayo (al volver a dibujarse)")]
        [Range(0f, 1f)] public float sparkChance;
        [Tooltip("Tamaño de las chispas (m), al azar entre x e y")]
        public Vector2 sparkSize = new Vector2(0.12f, 0.22f);

        [Header("Alrededor (lámpara de plasma)")]
        [Tooltip("Rayos a la vez. Cada uno cae en un punto al azar, que se queda fijo mientras el otro extremo sigue al " +
                 "núcleo (se estira), y se apaga al acabar su vida; entonces cae otro en otro sitio")]
        [Range(0, 12)] public int aroundCount = 6;
        [Tooltip("Rayos a la vez durante la carga del principio del especial (0,3 s): muchos, para que se note que se enciende")]
        [Range(0, 12)] public int windupCount = 10;
        [Tooltip("Lo que dura cada rayo antes de caer en otro sitio (s), al azar entre x e y")]
        public Vector2 strikeLife = new Vector2(0.15f, 0.35f);
        [Tooltip("Arrastre: cuánto se queda atrás el medio del rayo al moverse la peonza (s de su velocidad)")]
        [Min(0f)] public float drag = 0.05f;
        [Tooltip("Si el rayo se estira más que esto (m), se corta antes y cae otro")]
        [Min(0.5f)] public float maxStretch = 2.5f;
        [Tooltip("Altura del núcleo, de donde salen, sobre el centro de la peonza (m)")]
        public float coreHeight = 0.4f;
        [Tooltip("Distancia al centro a la que caen (m), al azar entre x e y")]
        public Vector2 aroundRadius = new Vector2(0.6f, 1.3f);
        [Tooltip("Altura a la que caen sobre la punta (m), al azar entre x e y (0 = en el suelo)")]
        public Vector2 aroundHeight = new Vector2(0.15f, 0.45f);
        [Tooltip("Cuánto se curva cada arco hacia arriba (m, en el medio)")]
        public float arch = 0.6f;

        [Header("Rastro por el suelo")]
        [Range(0, 8)] public int trailCount = 5;
        [Tooltip("Metros recorridos entre dos arcos del rastro")]
        [Min(0.05f)] public float trailSpacing = 0.6f;
        [Tooltip("Lo que dura cada arco del rastro (s; se apaga poco a poco)")]
        [Min(0.05f)] public float trailLife = 0.45f;
        [Tooltip("Velocidad mínima (m/s) para dejar rastro")]
        [Min(0f)] public float trailMinSpeed = 3f;
        [Tooltip("Altura sobre la punta (m)")]
        public float trailHeight = 0.03f;

        [Header("Al chocar")]
        [Range(0, 6)] public int clashCount = 3;
        [Tooltip("Lo que duran los arcos hasta la otra peonza (s)")]
        [Min(0.05f)] public float clashDuration = 0.25f;
    }

    /// <summary>
    /// Aura de un paquete de partículas (Status and Auras FREE; K8, 2026-10-17): una copia del prefab por peonza, creada
    /// en su primer frame, que cuelga de la punta y se enciende y apaga con el efecto. Se queda en la capa normal (el
    /// pixelado de la escena la pasa a píxeles). Se puede acelerar (velocidad de simulación de todos sus sistemas) y girar
    /// alrededor de la vertical. Cambiar de prefab necesita volver a dar a Play.
    /// </summary>
    [Serializable]
    public class AuraStyle
    {
        [Tooltip("Solo para reconocerla en el Inspector")]
        public string name;
        [Tooltip("Prefab del paquete (Assets/ThirdParty/VFX/Status and Auras FREE/Prefab). Vacío = no sale")]
        public ParticleSystem prefab;
        [Tooltip("Escala (sobre la del prefab)")]
        [Min(0.01f)] public float scale = 1f;
        [Tooltip("Altura sobre la punta (m)")]
        public float height = 0.1f;
        [Tooltip("Velocidad de la animación (1 = la del paquete; 2 = el doble de rápida)")]
        [Min(0.05f)] public float simulationSpeed = 1f;
        [Tooltip("Giro alrededor de la vertical (grados/s; negativo = hacia el otro lado). Con simulación local (Aura " +
                 "Burn) gira todo el fuego; con simulación de mundo (Aura Smoke), solo por dónde salen")]
        public float spin;
        [Tooltip("Cambia el color de sus partículas: el de los sistemas que salen de un color, y el segundo de los que salen " +
                 "al azar entre dos (Aura Lightning: de azul a amarillo)")]
        public bool tint;
        public Color tintColor = new Color(1f, 0.85f, 0.15f);
        [Tooltip("Sistemas del prefab que no salen, por su nombre, separados por comas (Aura Lightning: Smoke, su humo)")]
        public string hideSystems = "";
    }

    /// <summary>
    /// Partículas en espiral alrededor de la peonza (K8, 2026-10-17): salen en un anillo y se mueven girando (velocidad
    /// tangencial), hacia fuera o hacia dentro y hacia arriba. Un remolino mientras dura la carga del principio (ritmo; y
    /// más flojo mientras dura el poder), una explosión en espiral al encenderse o un círculo al chocar (cantidad). Con un
    /// emisor de píxel de VfxSystem o un sprite animado de la lista Impact Sprites. Escaladas con Opciones → Partículas.
    /// </summary>
    [Serializable]
    public class SpiralParticleStyle
    {
        [Tooltip("Solo para reconocerlas en el Inspector")]
        public string name;
        [Tooltip("Emisor de píxel (ChargeFlame: llama; LowSpinSpark: chispa con gravedad; Sparkle: estrella)")]
        public VfxType emitter = VfxType.ChargeFlame;
        [Tooltip("Sprite animado de la lista Impact Sprites en vez del emisor (vacío = el emisor)")]
        public string sprite;
        [Tooltip("Varios sprites de la lista Impact Sprites: cada partícula sale con uno al azar (vacío = Sprite). Con el tamaño y " +
                 "el giro al azar, cada vez parece distinta (Grieta00-03 en Onda de choque; para el Salto al caer)")]
        public List<string> spriteVariants = new List<string>();
        [Tooltip("Giro del sprite al salir (grados), al azar entre x e y (0-360 = cualquiera; p. ej. 60-90 para que las grietas " +
                 "salgan parecidas pero no iguales)")]
        public Vector2 rotation = new Vector2(0f, 360f);
        [Tooltip("Gira el sprite hacia la marcha (o hacia donde sale despedido el golpeado): Rotation se suma a ese ángulo " +
                 "(Embestida: la media luna mira hacia delante)")]
        public bool alignToDirection;
        [Tooltip("Metros hacia delante en la marcha o el golpe (negativo = detrás) a los que se coloca el anillo")]
        public float forwardOffset;
        [Tooltip("Remolino: partículas por segundo mientras dura la carga")]
        [Min(0f)] public float rate = 100f;
        [Tooltip("Explosión al encenderse o choque: partículas de golpe (repartidas alrededor)")]
        [Min(0)] public int count = 40;
        [Tooltip("Distancia al centro a la que salen (m), al azar entre x e y")]
        public Vector2 radius = new Vector2(0.4f, 0.6f);
        [Tooltip("Altura de salida sobre la punta (m), al azar entre x e y")]
        public Vector2 height = new Vector2(0.05f, 0.3f);
        [Tooltip("Velocidad de giro alrededor de la peonza (m/s), entre x e y")]
        public Vector2 tangentialSpeed = new Vector2(6f, 9f);
        [Tooltip("Velocidad hacia fuera (m/s; negativo = hacia dentro, como un remolino que traga), entre x e y")]
        public Vector2 outwardSpeed = new Vector2(4f, 7f);
        [Tooltip("Velocidad hacia arriba (m/s), entre x e y")]
        public Vector2 upSpeed = new Vector2(2f, 4f);
        [Tooltip("Gira en el sentido de las agujas del reloj, visto desde arriba")]
        public bool clockwise = true;
        [Tooltip("Tamaño (m), entre x e y")]
        public Vector2 size = new Vector2(0.2f, 0.35f);
        [Tooltip("Duración (s), entre x e y (0 = la del emisor o el sprite)")]
        public Vector2 lifetime = new Vector2(0.35f, 0.6f);
        [Tooltip("Color de cada partícula, al azar entre este y el siguiente")]
        public Color color = new Color(1f, 0.85f, 0.3f);
        public Color color2 = new Color(1f, 0.35f, 0.05f);
        [Tooltip("Transparencia")]
        [Range(0f, 1f)] public float alpha = 1f;
        [Tooltip("Solo en la carga (remolino): no sigue con el ritmo que queda mientras dura el poder, y la primera sale nada " +
                 "más empezar la carga (los círculos que se cierran del Fuego y el Hielo)")]
        public bool windupOnly;
        [Tooltip("Cuánto toma el color del poder (0 = sus colores; 1 = multiplicados por el color del poder). La plantilla común " +
                 "de los especiales lo usa para teñirse con el de cada uno")]
        [Range(0f, 1f)] public float powerTint;
    }

    /// <summary>
    /// Partículas de un efecto continuo (K8, 2026-10-17): un emisor de píxel de VfxSystem (sus gravedad y forma son las
    /// del prefab) con un ritmo fijo y, si se quiere, chasquidos (varias de golpe a intervalos al azar, como la madera al
    /// crepitar). Salen alrededor de la punta, en cono hacia arriba. Escaladas con Opciones → Partículas.
    /// </summary>
    [Serializable]
    public class SpecialParticleStyle
    {
        [Tooltip("Solo para reconocerlas en el Inspector")]
        public string name;
        [Tooltip("Emisor (LowSpinSpark: chispa que cae con gravedad; SpecialAura: píxel que sube despacio; " +
                 "LowSpinSmoke: bocanada de humo)")]
        public VfxType emitter = VfxType.LowSpinSpark;
        [Tooltip("Sprite de la lista Impact Sprites en vez del emisor (vacío = el emisor; Vaho: nube del Hielo)")]
        public string sprite;
        [Tooltip("Giro del sprite al salir (grados), al azar entre x e y (0-360 = cualquiera)")]
        public Vector2 rotation = new Vector2(0f, 360f);
        [Tooltip("Gira el sprite hacia la marcha de la peonza: Rotation se suma a ese ángulo (tumbados: la media luna de la " +
                 "Embestida mira hacia delante)")]
        public bool alignToMotion;
        [Tooltip("Metros hacia delante en la marcha (negativo = detrás) a los que salen, al azar entre x e y")]
        public Vector2 forwardOffset = Vector2.zero;
        [Tooltip("Partículas por segundo, seguidas")]
        [Min(0f)] public float rate = 10f;
        [Tooltip("Tiempo entre chasquidos (s), al azar entre x e y (0 = sin chasquidos)")]
        public Vector2 burstInterval = Vector2.zero;
        [Tooltip("Partículas de cada chasquido, al azar entre x e y")]
        public Vector2 burstCount = new Vector2(3f, 6f);
        [Tooltip("Distancia al centro a la que salen (m), al azar entre x e y")]
        public Vector2 spawnRadius = new Vector2(0.2f, 0.6f);
        [Tooltip("Altura de salida sobre la punta (m), al azar entre x e y")]
        public Vector2 height = new Vector2(0.05f, 0.25f);
        [Tooltip("Apertura del cono respecto a la vertical (grados)")]
        [Range(0f, 90f)] public float coneAngle = 35f;
        [Tooltip("Velocidad de salida (m/s), entre x e y")]
        public Vector2 speed = new Vector2(3f, 6f);
        [Tooltip("Parte de la velocidad de la peonza que se llevan")]
        [Range(-1f, 1f)] public float inheritVelocity = 0.2f;
        [Tooltip("Tamaño (m), entre x e y")]
        public Vector2 size = new Vector2(0.05f, 0.09f);
        [Tooltip("Duración (s), entre x e y (0 = la del emisor)")]
        public Vector2 lifetime = new Vector2(0.4f, 0.8f);
        [Tooltip("Color de cada partícula, al azar entre este y el siguiente")]
        public Color color = new Color(1f, 0.85f, 0.3f);
        public Color color2 = new Color(1f, 0.4f, 0.05f);
        [Tooltip("Transparencia")]
        [Range(0f, 1f)] public float alpha = 1f;
        [Tooltip("Cuánto toma el color del poder (0 = sus colores; 1 = multiplicados por el color del poder)")]
        [Range(0f, 1f)] public float powerTint;
    }

    /// <summary>
    /// Manchas en el suelo por donde pasa la peonza (K8, 2026-10-17; quemado del Fuego): un sprite tumbado de la lista
    /// Impact Sprites cada pocos metros (y alguno de vez en cuando si está parada), con giro y tamaño al azar, que se
    /// desvanece (Fade Out del sprite).
    /// </summary>
    [Serializable]
    public class ScorchStyle
    {
        [Tooltip("Id del sprite (lista Impact Sprites; Quemado = mancha difusa que crea Setup Impactos). Vacío = sin manchas")]
        public string sprite;
        [Tooltip("Varios sprites: cada mancha sale con uno al azar (vacío = Sprite). Embestida: las grietas del usuario")]
        public List<string> spriteVariants = new List<string>();
        [Tooltip("Solo mientras arranca (Embestida: las grietas, solo al embestir)")]
        public bool onlyWhileRushing;
        [Tooltip("Gira cada mancha hacia la marcha: Rotation se suma a ese ángulo (las grietas siguen el camino)")]
        public bool alignToMotion;
        [Tooltip("Giro de cada mancha (grados), al azar entre x e y (0-360 = cualquiera)")]
        public Vector2 rotation = new Vector2(0f, 360f);
        [Tooltip("Metros recorridos entre dos manchas")]
        [Min(0.05f)] public float spacing = 0.4f;
        [Tooltip("Una mancha cada tantos segundos aunque esté parada (0 = solo al moverse)")]
        [Min(0f)] public float idleInterval = 0.5f;
        [Tooltip("Tamaño (m), al azar entre x e y")]
        public Vector2 size = new Vector2(1f, 1.5f);
        [Tooltip("Lo que dura cada mancha (s), con el desvanecido del sprite al final")]
        [Min(0.1f)] public float lifetime = 4f;
        [Tooltip("Color (el sprite es blanco: negro = quemado)")]
        public Color color = new Color(0.06f, 0.04f, 0.03f);
        [Tooltip("Transparencia")]
        [Range(0f, 1f)] public float alpha = 0.45f;
        [Tooltip("Altura sobre la punta (m): un poco, para que no parpadee con el suelo")]
        public float height = 0.01f;
    }

    /// <summary>
    /// Efectos de una peonza con un estado alterado (K8, 2026-10-17; GDD 7.6): uno para la quemada (burn) y otro para la
    /// congelada (frozen). Como la que tiene el poder, pero más simple. En la punta, un aro en el suelo (BurstTipStyle,
    /// como el del especial activo, con otro sprite y sin cuencos); el borde (impulsos naranjas en la quemada; escarcha
    /// quieta en la congelada); sus partículas y su remolino; si se quiere, auras de partículas de un paquete (Aura Burn y
    /// Aura Smoke de Status and Auras FREE), que se crean una vez por peonza y se encienden mientras dura; un círculo de
    /// partículas al chocar; y el giro visual del disco. Lo aplican BladeBurstTip, BladeOverlay, BladeSpecialFx y
    /// BladePartSpin. Se lee en vivo.
    /// </summary>
    [Serializable]
    public class StatusVfxStyle
    {
        public bool enabled = true;
        [Tooltip("Punta: aro en el suelo (Ground), cuencos y partículas en cono, mientras dura el estado")]
        public BurstTipStyle tip = new BurstTipStyle();
        [Header("Borde")]
        public SpecialOverlayStyle overlay = new SpecialOverlayStyle { baseRim = 0.4f, pulseRim = new Vector2(0.8f, 2f), pulseFlash = new Vector2(0f, 0.15f) };
        [Header("Partículas (sustituyen a las de antes del estado si hay alguna)")]
        public List<SpecialParticleStyle> particles = new List<SpecialParticleStyle>();
        [Tooltip("Remolino mientras dura el estado (como el del especial, con menos densidad y radio). Usa Rate, no Count")]
        public List<SpiralParticleStyle> swirl = new List<SpiralParticleStyle>();
        [Header("Auras de paquetes (opcionales)")]
        [Tooltip("Auras de partículas (Aura Burn, Aura Smoke...) pegadas a la peonza mientras dura el estado")]
        public List<AuraStyle> auras = new List<AuraStyle>();
        [Header("Al chocar (además del impacto genérico)")]
        [Tooltip("En cada choque con daño de la peonza con el estado: un círculo de partículas, más pequeño que el del " +
                 "especial activo (no sale si la peonza tiene un especial con su propio círculo). Usa Count, no Rate")]
        public List<SpiralParticleStyle> impact = new List<SpiralParticleStyle>();
        [Header("Giro")]
        [Tooltip("Giro visual del disco mientras dura (1 = normal; 0,5 = la mitad, como rígido por el hielo). Solo se ve")]
        [Range(0f, 1f)] public float discSpin = 1f;
    }

    /// <summary>Un destello sobre la peonza por un evento (golpe recibido, parry, bloqueo...): sube de golpe y se apaga.</summary>
    [Serializable]
    public class OverlayFlash
    {
        public bool enabled = true;
        [Tooltip("Con el color del jugador de esta peonza (en el parry y el bloqueo, el de quien para). Si no, el de abajo")]
        public bool playerColor;
        public Color color = Color.white;
        [Tooltip("Cuánto tiñe toda la peonza al empezar (0-1)")]
        [Range(0f, 1f)] public float flash = 0.6f;
        [Tooltip("Intensidad del borde brillante al empezar (más de 1 = más grueso y brillante)")]
        [Min(0f)] public float rim = 2f;
        [Tooltip("Lo que tarda en apagarse (s)")]
        [Min(0.01f)] public float duration = 0.2f;
    }

    /// <summary>
    /// Efectos sobre la propia peonza (2026-10-17, GDD 7.6, K9): un material de superposición (shader
    /// FakeBlade/BladeOverlay) encima de cada pieza, con un borde de corte duro y un contorno grueso (tipo cómic), un
    /// destello que tiñe toda la peonza y anillos con textura (Zbam) proyectados desde arriba. Cada evento tiene su
    /// preset: cargar un ataque (borde que crece con el nivel y late, anillos que salen del núcleo), ataque rápido
    /// (un anillo), golpe recibido (destello y anillo del color del daño desde el punto del choque), parry y bloqueo.
    /// Lo aplica BladeOverlay; el material lo crea *Setup Rastros*. Se lee en vivo.
    /// </summary>
    [Serializable]
    public class BladeOverlayStyle
    {
        [Tooltip("Material de superposición (shader FakeBlade/BladeOverlay). Lo crea Setup Rastros")]
        public Material material;
        [Header("Aspecto (tipo cómic)")]
        [Tooltip("Ancho del borde brillante: cuánto entra desde la silueta hacia dentro (0-1)")]
        [Range(0f, 1f)] public float rimWidth = 0.45f;
        [Tooltip("Suavizado del borde (poco = corte duro, tipo cómic)")]
        [Range(0.001f, 0.5f)] public float rimSoftness = 0.04f;
        [Tooltip("Grosor del contorno alrededor de la silueta (m), con el color del borde mientras hay borde")]
        [Min(0f)] public float outlineWidth = 0.05f;
        [Tooltip("Transparencia del contorno cuando el borde está al máximo (0 = sin contorno)")]
        [Range(0f, 1f)] public float outlineAlpha = 0.9f;

        [Header("Anillos con textura (Zbam: anillo de pinchos, proyectado desde arriba)")]
        [Tooltip("Textura de los anillos (su transparencia da la forma). La pone Setup Rastros (Zbam de Wallcoeur)")]
        public Texture2D ringTexture;
        [Tooltip("Copia más grande y oscura detrás de cada anillo, para que se lea sobre cualquier color (alfa 0 = sin sombra)")]
        public Color ringShadowColor = new Color(0f, 0f, 0f, 0.7f);
        [Tooltip("Tamaño de esa sombra respecto al anillo")]
        [Min(1f)] public float ringShadowScale = 1.12f;
        [Tooltip("Mientras se carga: un anillo que crece desde el núcleo hasta fuera de la peonza, una y otra vez")]
        public bool chargeRingEnabled = true;
        [Tooltip("Tamaño del anillo al empezar (x) y al acabar (y), respecto al radio de la peonza")]
        public Vector2 chargeRingScale = new Vector2(0.15f, 1.4f);
        [Tooltip("Lo que tarda cada anillo en crecer (s); más corto en los niveles altos (Charge Ring Cycle Max)")]
        [Min(0.05f)] public float chargeRingCycle = 0.6f;
        [Tooltip("Lo que tarda en el nivel de carga máximo (s)")]
        [Min(0.05f)] public float chargeRingCycleMax = 0.35f;
        [Tooltip("Transparencia del anillo de la carga")]
        [Range(0f, 1f)] public float chargeRingAlpha = 0.9f;
        [Tooltip("Giro del anillo (grados/s)")]
        public float chargeRingSpin = 90f;
        [Tooltip("Al lanzar un ataque rápido: un anillo igual que el de la carga, una vez")]
        public bool quickRingEnabled = true;
        [Tooltip("Lo que tarda el anillo del ataque rápido (s)")]
        [Min(0.05f)] public float quickRingDuration = 0.3f;
        [Tooltip("Al recibir un golpe: un anillo que se abre desde el punto del choque y recorre la peonza hasta la parte " +
                 "de atrás, con el color del daño (blanco → amarillo → rojizo, como los impactos)")]
        public bool impactRingEnabled = true;
        [Tooltip("Tamaño del anillo del golpe al empezar (x) y al acabar (y), respecto al radio de la peonza")]
        public Vector2 impactRingScale = new Vector2(0.2f, 2.4f);
        [Tooltip("Lo que tarda el anillo del golpe en recorrer la peonza (s)")]
        [Min(0.05f)] public float impactRingDuration = 0.35f;
        [Tooltip("Transparencia del anillo del golpe")]
        [Range(0f, 1f)] public float impactRingAlpha = 1f;
        [Tooltip("Aclara el color del daño del anillo del golpe hacia el blanco")]
        [Range(0f, 1f)] public float impactRingLighten;

        [Header("Cargar un ataque (mientras se carga)")]
        public bool chargeEnabled = true;
        [Tooltip("Empieza con el color del jugador y va hacia Charge Hot Color con el nivel. Si no, empieza con Charge " +
                 "Color (el del jugador apenas se ve sobre su propia peonza)")]
        public bool chargePlayerColor;
        public Color chargeColor = new Color(1f, 0.8f, 0.3f);
        [Tooltip("Color en el nivel de carga máximo")]
        public Color chargeHotColor = new Color(1f, 0.95f, 0.75f);
        [Tooltip("Intensidad del borde en el nivel 1 (x) y en el máximo (y)")]
        public Vector2 chargeRim = new Vector2(1f, 3f);
        [Tooltip("Latidos por segundo del borde mientras se carga")]
        [Min(0f)] public float chargePulseSpeed = 4f;
        [Tooltip("Cuánto baja el borde en cada latido (0 = no late)")]
        [Range(0f, 1f)] public float chargePulseAmount = 0.4f;
        [Tooltip("Destello al subir de nivel de carga")]
        public OverlayFlash chargeLevelUp = new OverlayFlash { color = Color.white, flash = 0.4f, rim = 3f, duration = 0.15f };

        [Header("Eventos")]
        [Tooltip("Al recibir daño de otra peonza (más fuerte cuanto más daño, hasta Damage For Full)")]
        public OverlayFlash damaged = new OverlayFlash { color = Color.white, flash = 0.8f, rim = 2f, duration = 0.18f };
        [Tooltip("Daño con el que el destello del golpe recibido sale entero (con menos, más suave)")]
        [Min(1f)] public float damageForFull = 30f;
        [Tooltip("Quien hace el parry")]
        public OverlayFlash parry = new OverlayFlash { color = Color.white, flash = 0.6f, rim = 4f, duration = 0.35f };
        [Tooltip("Quien bloquea")]
        public OverlayFlash block = new OverlayFlash { color = new Color(0.75f, 0.9f, 1f), flash = 0.45f, rim = 3f, duration = 0.3f };
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

        [Header("Partículas animadas en cada impacto (cono hacia arriba)")]
        public ImpactParticleStyle impactParticles = new ImpactParticleStyle();

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
        [Header("Anillo al subir de nivel de carga (medias lunas en el suelo)")]
        [Tooltip("Sprite de la lista Impact Sprites (CircleImpact04Anillo: la media luna de CircleImpact04 sin su primer " +
                 "dibujo, un punto; tumbada en el suelo, Flat). Vacío = el anillo pixelado plano de antes")]
        public string chargeRingSprite = "CircleImpact04Anillo";
        [Tooltip("Copias del sprite (2 = una media luna y otra girada Charge Ring Angle Step grados: cierran el círculo)")]
        [Range(1, 8)] public int chargeRingCount = 2;
        [Tooltip("Giro de cada copia respecto a la anterior (grados)")]
        public float chargeRingAngleStep = 180f;
        [Tooltip("Gira todo el anillo al azar en cada nivel. Si no, empieza siempre en Charge Ring Angle")]
        public bool chargeRingRandomAngle = true;
        [Tooltip("Giro de la primera copia si no es al azar (grados)")]
        public float chargeRingAngle;
        [Tooltip("Tamaño del anillo respecto al anillo pixelado de antes (crece con el nivel y es mayor en el máximo). La media luna solo ocupa parte de su casilla: 1,6 la deja algo mayor que el de antes")]
        [Min(0f)] public float chargeRingSizeScale = 1.6f;
        [Tooltip("Altura sobre la punta (m)")]
        public float chargeRingHeight = 0.05f;
        [Tooltip("Duración (s); 0 = la del sprite")]
        [Min(0f)] public float chargeRingLifetime;
        [Tooltip("Transparencia")]
        [Range(0f, 1f)] public float chargeRingAlpha = 1f;
        [Tooltip("Aclara el color (el de la carga: del jugador hacia Charge Hot Color) hacia el blanco")]
        [Range(0f, 1f)] public float chargeRingLighten;

        [Header("Rastro (cinta pegada a la peonza, K7)")]
        public RibbonTrailStyle genericTrail = new RibbonTrailStyle();
        [Tooltip("Efecto en la punta durante el acelerón de ataques y dash")]
        public BurstTipStyle burstTip = new BurstTipStyle();

        [Header("Efectos sobre la peonza (borde brillante y destello)")]
        public BladeOverlayStyle bladeOverlay = new BladeOverlayStyle();

        [Header("Efectos de cada especial mientras está activo (K8/K9)")]
        public List<SpecialVfxStyle> specialEffects = new List<SpecialVfxStyle>();
        [Tooltip("Plantilla común (2026-10-19): la base de efectos que se copia al empezar los de cada especial nuevo (carga con círculos, " +
                 "líneas y humo hacia dentro, encendido con círculos, líneas y humo hacia fuera y estela en la golpeada, teñidos " +
                 "con el color de cada poder (Power Tint). Su tipo no cuenta. Sin quitar las auras de antes (Replace Aura apagado)")]
        public SpecialVfxStyle genericSpecialEffects;
        [Tooltip("Los especiales sin entrada propia usan la plantilla común en juego. Apagado (2026-10-19): la plantilla es la base que se " +
                 "copia al empezar los efectos de cada especial nuevo, y los que aún no tienen siguen con su aura")]
        public bool useGenericSpecialEffects;
        [Tooltip("Peonza quemada: aro de fuego en el suelo, borde naranja, humo, chispas y ceniza y auras")]
        public StatusVfxStyle burn = new StatusVfxStyle();
        [Tooltip("Peonza congelada: aro de hielo en el suelo, escarcha quieta sobre la peonza, vaho, cristales y disco más lento")]
        public StatusVfxStyle frozen = new StatusVfxStyle();
        [Tooltip("Peonza lanzada (golpe fuerte de Rayos, onda de choque): borde amarillo que parpadea, chispas que saltan y aura")]
        public StatusVfxStyle launched = new StatusVfxStyle();
        [Tooltip("Aviso de usos de los poderes con varios usos (Onda de choque): marcas que giran alrededor de la peonza")]
        public UsesIndicatorStyle usesIndicator = new UsesIndicatorStyle();

        /// <summary>
        /// Efectos en juego del especial: los suyos o, si no tiene, la plantilla común (si está encendida); null si ninguno.
        /// </summary>
        public SpecialVfxStyle GetSpecialEffectsOrGeneric(SpecialAbilityType type) =>
            GetSpecialEffects(type) ?? (useGenericSpecialEffects ? genericSpecialEffects : null);

        /// <summary>Entrada propia del especial, o null si no tiene (para los setups; en juego, GetSpecialEffectsOrGeneric).</summary>
        public SpecialVfxStyle GetSpecialEffects(SpecialAbilityType type)
        {
            for (int i = 0; i < specialEffects.Count; i++)
                if (specialEffects[i] != null && specialEffects[i].type == type) return specialEffects[i];
            return null;
        }

        /// <summary>Efectos del estado (quemada o congelada), o null si no tiene o están apagados.</summary>
        public StatusVfxStyle GetStatusEffects(StatusEffectType status)
        {
            StatusVfxStyle style = status == StatusEffectType.Burning ? burn
                : status == StatusEffectType.Frozen ? frozen
                : status == StatusEffectType.Launched ? launched
                : null;
            return style != null && style.enabled ? style : null;
        }

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
