using System;
using System.Collections.Generic;
using FakeBlade.Core;
using TMPro;
using UnityEngine;

namespace FakeBlade.UI
{
    /// <summary>
    /// Tema visual del HUD (estilo retro pixel-art, GDD 9).
    /// Todo lo que un artista querría tocar sin abrir código: fuente, escala de píxel y colores.
    ///
    /// Fuente recomendada: una pixel font (p. ej. "Press Start 2P" o "Silkscreen", licencia OFL)
    /// convertida a TMP Font Asset con Render Mode "RASTER" y filtrado Point.
    /// </summary>
    [CreateAssetMenu(fileName = "HUDTheme", menuName = "FakeBlade/HUD Theme")]
    public class HUDTheme : ScriptableObject
    {
        [Header("=== FUENTE Y ESCALA ===")]
        [Tooltip("Fuente TMP. Vacío = fuente TMP por defecto")]
        public TMP_FontAsset font;
        [Tooltip("Tamaño del 'píxel' de la UI en unidades de referencia. Todo se alinea a esta rejilla")]
        [Min(1)] public int pixelSize = 4;
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [Tooltip("Margen desde el borde de la pantalla (en píxeles de UI)")]
        [Min(0)] public int screenMarginPixels = 6;

        [Header("=== PANEL ===")]
        public Color panelBackground = new Color(0.06f, 0.06f, 0.11f, 0.92f);
        public Color panelShadow = new Color(0f, 0f, 0f, 0.55f);
        public Color textColor = new Color(0.96f, 0.96f, 1f, 1f);
        public Color textDimColor = new Color(0.55f, 0.58f, 0.7f, 1f);
        public Color overlayColor = new Color(0f, 0f, 0f, 0.7f);

        [Header("=== BARRA DE RPM ===")]
        public Color barBackground = new Color(0.02f, 0.02f, 0.05f, 1f);
        public Color healthHigh = new Color(0.25f, 0.95f, 0.4f);
        public Color healthMid = new Color(1f, 0.8f, 0.15f);
        public Color healthLow = new Color(1f, 0.22f, 0.25f);
        [Tooltip("Capa inferior que sigue a la barra con retraso")]
        public Color healthTrail = new Color(1f, 0.93f, 0.78f, 1f);
        [Range(0f, 1f)] public float lowThreshold = 0.3f;
        [Range(0f, 1f)] public float midThreshold = 0.6f;
        [Tooltip("Segundos que la capa inferior espera antes de bajar")]
        public float trailDelay = 0.35f;
        [Tooltip("Velocidad de bajada de la capa inferior (fracción de barra por segundo)")]
        public float trailSpeed = 0.6f;

        [Header("=== CARGAS DE ATAQUE ===")]
        public Color chargeReady = new Color(0.35f, 0.8f, 1f);
        public Color chargeEmpty = new Color(0.12f, 0.14f, 0.22f);
        public Color chargeBorder = new Color(0.02f, 0.02f, 0.05f);
        public Color chargeBlink = Color.white;
        public Color chargeCharging = new Color(1f, 0.95f, 0.6f);
        [Tooltip("Progreso de recarga a partir del cual parpadea el contorno")]
        [Range(0f, 1f)] public float blinkBeforeReady = 0.75f;

        [Header("=== DASH ===")]
        public Color dashReady = new Color(1f, 0.85f, 0.2f);
        public Color dashCooldown = new Color(0.35f, 0.3f, 0.12f);

        [Header("=== ESFERA DE ESPECIAL ===")]
        public Color sphereOutline = new Color(0.02f, 0.02f, 0.05f);
        public Color sphereEmpty = new Color(0.1f, 0.1f, 0.18f);
        // El color de relleno es el del poder equipado (SpecialAbilityData.color)

        [Header("=== ARQUETIPOS (selección de peonzas) ===")]
        public Color archetypeAttack = new Color(1f, 0.4f, 0.35f);
        public Color archetypeBalanced = new Color(0.45f, 0.95f, 0.5f);
        public Color archetypeDefense = new Color(0.45f, 0.65f, 1f);
        public Color archetypeAgility = new Color(1f, 0.85f, 0.3f);

        [Header("=== BARRAS DE STATS POR TRAMOS (selección de peonzas) ===")]
        [Tooltip("Lo que da la peonza sin piezas")]
        public Color statBase = new Color(0.45f, 0.48f, 0.58f);
        [Tooltip("Lo que resta una pieza (al final de la barra)")]
        public Color statLoss = new Color(0.75f, 0.18f, 0.22f);

        [Header("=== NÚMEROS DE DAÑO (CombatPopups) ===")]
        [Tooltip("Fuentes que se eligen en Opciones → Fuente de los números (la primera es la de por defecto). Solo llevan las " +
                 "cifras 0-9; las crea FakeBlade → Setup Números de daño desde los TTF de ThirdParty/Free/Fonts")]
        public List<TMP_FontAsset> damageFonts = new List<TMP_FontAsset>();
        public DamageNumberStyle damageNumbers = new DamageNumberStyle();

        public Color GetArchetypeColor(BladeArchetype archetype)
        {
            switch (archetype)
            {
                case BladeArchetype.Attack: return archetypeAttack;
                case BladeArchetype.Defense: return archetypeDefense;
                case BladeArchetype.Agility: return archetypeAgility;
                default: return archetypeBalanced;
            }
        }

        /// <summary>Color por tramos (sin degradado) para mantener la estética pixel.</summary>
        public Color GetHealthColor(float t)
        {
            if (t <= lowThreshold) return healthLow;
            if (t <= midThreshold) return healthMid;
            return healthHigh;
        }

        public TMP_FontAsset ResolveFont() => font != null ? font : TMP_Settings.defaultFontAsset;

        /// <summary>Fuente de los números de daño elegida en Opciones (la primera si no existe; la del HUD si no hay ninguna).</summary>
        public TMP_FontAsset ResolveDamageFont(int index)
        {
            if (damageFonts == null || damageFonts.Count == 0) return ResolveFont();
            TMP_FontAsset f = damageFonts[Mathf.Clamp(index, 0, damageFonts.Count - 1)];
            return f != null ? f : ResolveFont();
        }

        public static HUDTheme CreateDefault()
        {
            var theme = CreateInstance<HUDTheme>();
            theme.name = "HUDTheme (Default)";
            theme.hideFlags = HideFlags.DontSave;
            return theme;
        }
    }

    /// <summary>
    /// Aspecto de los números de daño (GDD 9.1, 2026-11-02): solo la cifra, inclinada, con el relleno del color de quien
    /// golpea, contorno blanco y un glow del color del daño. Salen grandes y bajan a su tamaño con un solo asentamiento;
    /// el tamaño, el salto y la sacudida crecen con el daño, de minDamage a maxDamage.
    /// </summary>
    [Serializable]
    public class DamageNumberStyle
    {
        [Tooltip("Daño con el que el número sale al mínimo (tamaño, salto y sacudida)")]
        [Min(0f)] public float minDamage = 5f;
        [Tooltip("Daño con el que sale al máximo (60 = el tope de un golpe, GDD 2.7)")]
        [Min(1f)] public float maxDamage = 60f;
        [Tooltip("Tamaño de letra con el daño mínimo, en píxeles de UI")]
        [Min(1f)] public float sizeMin = 5.2f;
        [Tooltip("Tamaño de letra con el daño máximo, en píxeles de UI")]
        [Min(1f)] public float sizeMax = 10.4f;

        [Header("Colores")]
        [Tooltip("Relleno cuando nadie lo causa (pared, roce, desgaste). Si no, el color de quien golpea")]
        public Color noSourceFill = new Color(0.78f, 0.78f, 0.8f);
        public Color outline = Color.white;
        [Range(0f, 1f)] public float outlineWidth = 0.07f;
        [Tooltip("Engorda el trazo de la cifra (las fuentes de pincel finas se quedaban en el contorno blanco)")]
        [Range(-1f, 1f)] public float faceDilate = 0.12f;
        [Tooltip("Espacio entre cifras (unidades de TextMesh Pro): con el trazo engordado y la cursiva se pisaban")]
        public float characterSpacing = 8f;
        [Tooltip("Glow por debajo de midDamage")]
        public Color glowLow = new Color(1f, 1f, 1f, 0.4f);
        [Tooltip("Glow desde midDamage")]
        public Color glowMid = new Color(1f, 0.88f, 0.5f, 0.5f);
        [Tooltip("Glow desde highDamage")]
        public Color glowHigh = new Color(1f, 0.5f, 0.45f, 0.5f);
        [Min(0f)] public float midDamage = 10f;
        [Min(0f)] public float highDamage = 30f;
        [Tooltip("Hasta dónde llega el glow (0-1 del margen de la fuente): más = más difuminado")]
        [Range(0f, 1f)] public float glowOuter = 1f;
        [Tooltip("Separación entre el contorno y el glow (0 = pegado al contorno). El glow empieza donde acaba el contorno " +
                 "(faceDilate + outlineWidth): más cerca lo tapa, porque se pinta encima, y más lejos queda una raya entre los dos")]
        [Range(-0.5f, 0.5f)] public float glowGap = 0f;
        [Tooltip("Caída del glow (1 = la más suave y ancha; menos = más fino). Su transparencia, en el alfa de cada color")]
        [Range(0f, 1f)] public float glowPower = 1f;

        [Header("Suma de golpes (Opciones → Números de daño → Sumados)")]
        [Tooltip("Los golpes que recibe una peonza a menos de estos segundos del anterior se suman en el mismo número")]
        [Min(0f)] public float sumWindow = 0.6f;

        [Header("Animación")]
        [Tooltip("Inclinada (cursiva de TextMesh Pro)")]
        public bool italic = true;
        [Tooltip("Segundos en pantalla")]
        [Min(0.1f)] public float life = 0.95f;
        [Tooltip("Lo que sube mientras dura, en píxeles de UI")]
        public float rise = 14f;
        [Tooltip("Tamaño extra al salir con el daño mínimo (0,5 = sale a ×1,5 de su tamaño final)")]
        [Min(0f)] public float popMin = 0.5f;
        [Tooltip("Tamaño extra al salir con el daño máximo")]
        [Min(0f)] public float popMax = 1.4f;
        [Tooltip("Rapidez con la que baja a su tamaño (más = más rápido)")]
        [Min(0.1f)] public float settleSpeed = 11f;
        [Tooltip("Oscilación al bajar (poca = un solo asentamiento suave; mucha = rebote de goma)")]
        [Min(0f)] public float settleWobble = 9f;
        [Tooltip("Daño (0-1 de la escala) desde el que tiembla al salir")]
        [Range(0f, 1f)] public float shakeFrom = 0.25f;
        [Tooltip("Temblor lateral máximo, en píxeles de UI")]
        [Min(0f)] public float shakeMax = 1.5f;
        [Tooltip("Giro máximo del temblor, en grados")]
        [Min(0f)] public float shakeAngle = 8f;
        [Tooltip("Lo que dura el temblor con el daño mínimo (x) y con el máximo (y), en segundos")]
        public Vector2 shakeTime = new Vector2(0.08f, 0.18f);

        [Header("Efectos por poder (daño del propio poder)")]
        [Tooltip("Aspecto de los números cuando el daño lo causa un poder: onda, embestida, caída del Salto, empujón del " +
                 "Tornado, golpe fuerte de Rayos, quemadura del Fuego, reflejo de Pinchos. Setup Números de daño crea los que faltan")]
        public List<DamageNumberSpecialFx> specialFx = new List<DamageNumberSpecialFx>();

        /// <summary>Efecto del poder que causa el daño (null si no tiene o no es de un poder).</summary>
        public DamageNumberSpecialFx FindFx(SpecialAbilityType special)
        {
            if (special == SpecialAbilityType.None || specialFx == null) return null;
            for (int i = 0; i < specialFx.Count; i++)
                if (specialFx[i] != null && specialFx[i].special == special) return specialFx[i];
            return null;
        }

        /// <summary>Posición del daño en la escala (0 = minDamage, 1 = maxDamage).</summary>
        public float Strength(float damage) => Mathf.Clamp01((damage - minDamage) / Mathf.Max(0.01f, maxDamage - minDamage));

        public Color GlowColor(float damage) => damage >= highDamage ? glowHigh : damage >= midDamage ? glowMid : glowLow;
    }

    /// <summary>
    /// Aspecto de los números de daño de un poder (2026-11-02), con el propio shader de TextMesh Pro: un degradado en la
    /// cifra, una textura que se mueve dentro (opcional), el contorno y el glow del poder y un parpadeo del glow. Sin los
    /// colores (alfa 0) se quedan los normales: relleno de quien golpea y glow del daño.
    /// </summary>
    [Serializable]
    public class DamageNumberSpecialFx
    {
        public SpecialAbilityType special;
        [Tooltip("Degradado de la cifra: arriba (alfa 0 = sin degradado; se queda el relleno). Sustituye al color de quien golpea")]
        public Color gradientTop = new Color(1f, 1f, 1f, 0f);
        [Tooltip("Degradado de la cifra: abajo")]
        public Color gradientBottom = new Color(1f, 1f, 1f, 0f);
        [Tooltip("Textura dentro de la cifra. Ojo: multiplica el color, solo oscurece (con un ruido gris, la cifra se ensucia; " +
                 "2026-11-02). Vacío = sin textura")]
        public Texture2D faceTexture;
        [Tooltip("Repeticiones de la textura en cada cifra")]
        public Vector2 faceTiling = Vector2.one;
        [Tooltip("Velocidad de la textura (x, y), en vueltas por segundo. Negativa en y = sube")]
        public Vector2 faceScroll = new Vector2(0f, -1f);
        [Tooltip("Relleno en vez del color de quien golpea (alfa 0 = el de quien golpea)")]
        public Color fill = new Color(1f, 1f, 1f, 0f);
        public Color outline = Color.white;
        [Tooltip("Glow en vez del color del daño (alfa 0 = el del daño); su alfa es su transparencia")]
        public Color glow = new Color(1f, 1f, 1f, 0f);
        [Tooltip("Parpadeo del glow (0 = nada, 1 = se apaga del todo a ratos)")]
        [Range(0f, 1f)] public float flicker;
        [Tooltip("Rapidez del parpadeo")]
        [Min(0f)] public float flickerSpeed = 20f;
    }
}
