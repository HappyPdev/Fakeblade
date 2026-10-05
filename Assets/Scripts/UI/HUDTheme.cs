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

        /// <summary>Color por tramos (sin degradado) para mantener la estética pixel.</summary>
        public Color GetHealthColor(float t)
        {
            if (t <= lowThreshold) return healthLow;
            if (t <= midThreshold) return healthMid;
            return healthHigh;
        }

        public TMP_FontAsset ResolveFont() => font != null ? font : TMP_Settings.defaultFontAsset;

        public static HUDTheme CreateDefault()
        {
            var theme = CreateInstance<HUDTheme>();
            theme.name = "HUDTheme (Default)";
            theme.hideFlags = HideFlags.DontSave;
            return theme;
        }
    }
}
