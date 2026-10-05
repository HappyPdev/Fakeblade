using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// Utilidades para construir UI pixel-art por código:
    /// - Creación de RectTransforms/Images/Textos alineados a una rejilla de "píxeles de UI".
    /// - Sprites generados en runtime con filtrado Point (círculos, iconos) → bordes duros.
    /// - Cachés de strings numéricas para no generar basura (GC) al actualizar textos.
    /// </summary>
    public static class PixelUI
    {
        public const int UILayer = 5;

        #region Construction
        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>
        /// Coloca un rect anclado arriba-izquierda del padre, en unidades de píxel de UI.
        /// Con mirror, la X se refleja respecto al ancho del padre (paneles de la derecha).
        /// </summary>
        public static void Place(RectTransform rt, int x, int y, int w, int h, int pixel, bool mirror = false, int parentWidth = 0)
        {
            int px = mirror ? parentWidth - x - w : x;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(px * pixel, -y * pixel);
            rt.sizeDelta = new Vector2(w * pixel, h * pixel);
        }

        /// <summary>Estira el rect sobre el padre con un margen interior en píxeles de UI.</summary>
        public static void Stretch(RectTransform rt, int inset, int pixel)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset * pixel, inset * pixel);
            rt.offsetMax = new Vector2(-inset * pixel, -inset * pixel);
        }

        public static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            RectTransform rt = CreateRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image CreateFilledImage(string name, Transform parent, Color color, Sprite sprite,
            Image.FillMethod method, int origin)
        {
            Image image = CreateImage(name, parent, color, sprite != null ? sprite : WhiteSprite);
            image.type = Image.Type.Filled;
            image.fillMethod = method;
            image.fillOrigin = origin;
            image.fillAmount = 1f;
            return image;
        }

        /// <summary>Canvas de pantalla completa escalado a la resolución de referencia del tema.</summary>
        public static Canvas CreateOverlayCanvas(string name, Transform parent, HUDTheme theme, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            if (parent != null) go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = true;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = theme.referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Texto grande con sombra desplazada 1 píxel de UI (títulos).</summary>
        public static TextMeshProUGUI CreateShadowedTitle(string name, Transform parent, HUDTheme theme, float size, Color color)
        {
            int px = Mathf.Max(1, theme.pixelSize);
            RectTransform root = CreateRect(name, parent);

            var shadow = CreateText("Shadow", root, theme, size, TextAlignmentOptions.Center, theme.sphereOutline);
            Stretch(shadow.rectTransform, 0, px);
            shadow.rectTransform.anchoredPosition = new Vector2(px, -px);

            var text = CreateText("Text", root, theme, size, TextAlignmentOptions.Center, color);
            Stretch(text.rectTransform, 0, px);
            text.gameObject.AddComponent<TextShadowSync>().Shadow = shadow;
            return text;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, HUDTheme theme, float size,
            TextAlignmentOptions alignment, Color color)
        {
            RectTransform rt = CreateRect(name, parent);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = theme.ResolveFont();
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            text.text = string.Empty;
            return text;
        }
        #endregion

        #region Generated Sprites
        private static Sprite _white;
        private static readonly Dictionary<int, Sprite> DiscCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> RingCache = new Dictionary<int, Sprite>();
        private static Sprite _topIcon;

        /// <summary>Sprite blanco 1x1 (necesario para Images de tipo Filled).</summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (_white == null)
                {
                    var tex = NewTexture(1, 1);
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply(false, true);
                    _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
                    _white.name = "PixelUI_White";
                }
                return _white;
            }
        }

        /// <summary>Círculo relleno pixelado de diameter x diameter píxeles.</summary>
        public static Sprite Disc(int diameter) => GetCircle(DiscCache, diameter, false);

        /// <summary>Anillo pixelado de 1 píxel de grosor.</summary>
        public static Sprite Ring(int diameter) => GetCircle(RingCache, diameter, true);

        /// <summary>Icono de peonza 5x5 para las vidas.</summary>
        public static Sprite TopIcon
        {
            get
            {
                if (_topIcon == null)
                {
                    _topIcon = FromPattern("PixelUI_Top", new[]
                    {
                        ".###.",
                        "#####",
                        ".###.",
                        "..#..",
                        "..#.."
                    });
                }
                return _topIcon;
            }
        }

        private static Sprite GetCircle(Dictionary<int, Sprite> cache, int diameter, bool ring)
        {
            diameter = Mathf.Max(2, diameter);
            if (cache.TryGetValue(diameter, out Sprite cached) && cached != null) return cached;

            var tex = NewTexture(diameter, diameter);
            float r = diameter * 0.5f;
            var pixels = new Color32[diameter * diameter];
            var white = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool inside = d <= r - 0.05f;
                    bool on = ring ? inside && d > r - 1.05f : inside;
                    pixels[y * diameter + x] = on ? white : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            var sprite = Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = (ring ? "PixelUI_Ring_" : "PixelUI_Disc_") + diameter;
            cache[diameter] = sprite;
            return sprite;
        }

        /// <summary>
        /// Sprite pixel desde un patrón de texto: '#' = blanco (se tiñe con el color),
        /// 'o' = contorno negro (no se tiñe), cualquier otro = transparente. Se centra en un
        /// cuadrado si el patrón no lo es. pixelsPerUnit define su tamaño en el mundo.
        /// </summary>
        public static Sprite FromPattern(string name, string[] rows, float pixelsPerUnit = 100f)
        {
            int h = rows.Length;
            int w = 0;
            for (int i = 0; i < h; i++) w = Mathf.Max(w, rows[i].Length);
            int size = Mathf.Max(w, h);
            int offsetX = (size - w) / 2;
            int offsetY = (size - h) / 2;

            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            for (int y = 0; y < h; y++)
            {
                string row = rows[y];
                for (int x = 0; x < row.Length; x++)
                {
                    char c = row[x];
                    if (c != '#' && c != 'o') continue;
                    int py = size - 1 - (offsetY + y);
                    pixels[py * size + offsetX + x] = c == '#' ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sprite.name = name;
            return sprite;
        }

        private static Texture2D NewTexture(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
        }
        #endregion

        #region Cached Strings (zero GC)
        private static readonly string[] PercentStrings = BuildNumbered(0, 100, "", "%");
        private static readonly string[] TimesStrings = BuildNumbered(0, 99, "x", "");
        private static readonly string[] PlainNumbers = BuildNumbered(0, 199, "", "");

        public static string Percent(int value) => PercentStrings[Mathf.Clamp(value, 0, 100)];
        public static string Times(int value) => TimesStrings[Mathf.Clamp(value, 0, 99)];
        public static string Number(int value) =>
            value >= 0 && value < PlainNumbers.Length ? PlainNumbers[value] : value.ToString();

        private static string[] BuildNumbered(int from, int to, string prefix, string suffix)
        {
            var result = new string[to - from + 1];
            for (int i = from; i <= to; i++)
                result[i - from] = prefix + i + suffix;
            return result;
        }
        #endregion

        /// <summary>Onda cuadrada 0/1 (parpadeo por pasos, sin interpolación).</summary>
        public static bool Blink(float time, float frequency) => Mathf.FloorToInt(time * frequency * 2f) % 2 == 0;
    }
}
