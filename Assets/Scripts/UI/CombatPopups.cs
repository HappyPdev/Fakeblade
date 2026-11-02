using System;
using System.Collections.Generic;
using FakeBlade.Core;
using TMPro;
using UnityEngine;

namespace FakeBlade.UI
{
    /// <summary>
    /// Números de daño en la arena (GDD 9.1), dentro del HUD de combate: lo que pierde cada peonza (1 RPM o más).
    /// Desde el 2026-11-02, solo la cifra (sin «-»), inclinada, con una fuente de pincel que se elige en Opciones, el
    /// relleno del color de quien golpea (gris si nadie lo causa), contorno blanco y un glow del color del daño (blanco,
    /// amarillo, rojo). Salen grandes y bajan a su tamaño con un solo asentamiento; el tamaño, el salto y la sacudida
    /// crecen con el daño. Después suben un poco y se desvanecen. Valores en HUDTheme → Números de daño. En Opciones →
    /// Números de daño: no, cada golpe o sumados (por defecto): los golpes que recibe una peonza seguidos (a menos de
    /// sumWindow del anterior) se suman en el mismo número, que vuelve a hacer el pop con cada uno.
    ///
    /// El parry y el bloqueo no tienen cartel: se ven con su impacto (VfxSystem.PlayImpact).
    /// Todo sale de una reserva creada al principio: sin instanciar ni GC en combate (los números con SetText). Cada
    /// número tiene su material (glow propio), que solo se crea al empezar o al cambiar la fuente en Opciones.
    /// </summary>
    public sealed class CombatPopups : MonoBehaviour
    {
        private const int NumberPool = 24;
        private const float MinDamage = 1f; // el roce continuo no llena la pantalla de números
        // Movimiento de la textura de dentro de la cifra (shader de TextMesh Pro; ShaderUtilities no los tiene)
        private static readonly int FaceSpeedX = Shader.PropertyToID("_FaceUVSpeedX");
        private static readonly int FaceSpeedY = Shader.PropertyToID("_FaceUVSpeedY");

        private sealed class Popup
        {
            public RectTransform Root;
            public TextMeshProUGUI Text;
            public Material Material;
            public Vector3 World;
            public float Age;
            /// <summary>Daño en la escala del estilo (0 = el mínimo, 1 = el máximo).</summary>
            public float Strength;
            /// <summary>Daño que enseña (con los golpes sumados, el total).</summary>
            public float Total;
            public Color Fill;
            public Color Glow;
            /// <summary>Efecto del poder que causó el daño (null = el normal).</summary>
            public DamageNumberSpecialFx Fx;
            /// <summary>Qué manda en su aspecto: 2 = un poder, 1 = un jugador, 0 = nadie (pared, roce, desgaste).</summary>
            public int Rank;
            /// <summary>Peonza golpeada (para sumarle los golpes siguientes).</summary>
            public Tracked Owner;
            public bool Active;
        }

        private sealed class Tracked
        {
            public FakeBladeController Blade;
            public Color Color;
            public Action<float, FakeBladeController> OnDamaged;
            /// <summary>Su último número (con los golpes sumados, el que crece).</summary>
            public Popup Last;
        }

        private HUDTheme _theme;
        private int _px;
        private RectTransform _root;
        private readonly Popup[] _numbers = new Popup[NumberPool];
        private int _nextNumber;
        private readonly List<Tracked> _tracked = new List<Tracked>(4);
        private Camera _camera;
        /// <summary>Fuente puesta en la reserva (índice de Opciones; -1 = ninguna todavía).</summary>
        private int _appliedFont = -1;
        /// <summary>Glow puesto en la reserva (Opciones → Brillo de los números).</summary>
        private bool _appliedGlow;

        private DamageNumberStyle Style => _theme.damageNumbers;

        public static CombatPopups Create(Transform parent, HUDTheme theme)
        {
            RectTransform root = PixelUI.CreateRect("Popups", parent);
            PixelUI.Stretch(root, 0, 1);
            var popups = root.gameObject.AddComponent<CombatPopups>();
            popups.Build(theme, root);
            return popups;
        }

        private void Build(HUDTheme theme, RectTransform root)
        {
            _theme = theme;
            _px = Mathf.Max(1, theme.pixelSize);
            _root = root;
            for (int i = 0; i < NumberPool; i++)
            {
                RectTransform rt = PixelUI.CreateRect("Damage", _root);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(40 * _px, 20 * _px);
                TextMeshProUGUI text = PixelUI.CreateText("Text", rt, theme, Style.sizeMin * _px, TextAlignmentOptions.Center, Color.white);
                PixelUI.Stretch(text.rectTransform, 0, _px);
                rt.gameObject.SetActive(false);
                _numbers[i] = new Popup { Root = rt, Text = text };
            }
            ApplyFont(SettingsService.Current.damageNumberFont);
        }

        private void OnDestroy()
        {
            Untrack();
            for (int i = 0; i < NumberPool; i++)
                if (_numbers[i] != null && _numbers[i].Material != null) Destroy(_numbers[i].Material);
        }

        /// <summary>
        /// Pone la fuente elegida en toda la reserva, con el material propio de cada número: contorno y glow (el color del
        /// glow cambia en cada número). Solo al empezar y al cambiar la fuente o el brillo en Opciones.
        /// </summary>
        private void ApplyFont(int index)
        {
            _appliedFont = index;
            _appliedGlow = SettingsService.Current.damageNumberGlow;
            TMP_FontAsset font = _theme.ResolveDamageFont(index);
            DamageNumberStyle style = Style;
            for (int i = 0; i < NumberPool; i++)
            {
                Popup p = _numbers[i];
                // El material propio solo se rehace con otra fuente: con la misma, el texto devolvía el que se acababa de
                // destruir (al cambiar solo el brillo, el número se quedaba sin material)
                if (p.Material == null || p.Text.font != font)
                {
                    if (p.Material != null) Destroy(p.Material);
                    p.Text.font = font;
                    p.Material = p.Text.fontMaterial;
                }
                p.Text.fontStyle = style.italic ? FontStyles.Italic : FontStyles.Normal;
                p.Text.characterSpacing = style.characterSpacing;
                Material m = p.Material;
                // En el material: el texto no lo vuelve a poner si el valor no cambia, y al cambiar de fuente el
                // material nuevo salía sin contorno
                p.Text.outlineWidth = style.outlineWidth;
                p.Text.outlineColor = style.outline;
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, style.outlineWidth);
                m.SetColor(ShaderUtilities.ID_OutlineColor, style.outline);
                m.SetFloat(ShaderUtilities.ID_FaceDilate, style.faceDilate);
                if (_appliedGlow) m.EnableKeyword(ShaderUtilities.Keyword_Glow);
                else m.DisableKeyword(ShaderUtilities.Keyword_Glow);
                // El glow empieza donde acaba el contorno (trazo engordado + contorno) y solo va hacia fuera: más cerca
                // tapaba el contorno, porque se pinta encima, y más lejos quedaba una raya entre los dos (2026-11-02)
                m.SetFloat(ShaderUtilities.ID_GlowOffset, style.faceDilate + style.outlineWidth + style.glowGap);
                m.SetFloat(ShaderUtilities.ID_GlowInner, 0f);
                m.SetFloat(ShaderUtilities.ID_GlowOuter, style.glowOuter);
                m.SetFloat(ShaderUtilities.ID_GlowPower, style.glowPower);
                ShaderUtilities.UpdateShaderRatios(m);
                p.Text.UpdateMeshPadding();
            }
        }

        #region Peonzas
        /// <summary>Sigue el daño de las peonzas de la partida (al prepararla).</summary>
        public void Track(IReadOnlyList<PlayerController> players)
        {
            Untrack();
            for (int i = 0; i < players.Count; i++) Add(players[i]);
            for (int i = 0; i < NumberPool; i++) Hide(_numbers[i]);
        }

        /// <summary>Peonza que entra con la partida empezada (dummies del Sandbox).</summary>
        public void Add(PlayerController player)
        {
            if (player == null || player.Blade == null) return;
            FakeBladeController blade = player.Blade;
            for (int i = 0; i < _tracked.Count; i++)
                if (_tracked[i].Blade == blade) return;
            var t = new Tracked { Blade = blade, Color = player.PlayerColor };
            t.OnDamaged = (amount, source) => ShowDamage(t, amount, source);
            blade.OnDamaged += t.OnDamaged;
            _tracked.Add(t);
        }

        public void Remove(PlayerController player)
        {
            if (player == null) return;
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                if (_tracked[i].Blade != player.Blade) continue;
                if (_tracked[i].Blade != null) _tracked[i].Blade.OnDamaged -= _tracked[i].OnDamaged;
                _tracked.RemoveAt(i);
            }
        }

        private void Untrack()
        {
            for (int i = 0; i < _tracked.Count; i++)
                if (_tracked[i].Blade != null) _tracked[i].Blade.OnDamaged -= _tracked[i].OnDamaged;
            _tracked.Clear();
        }

        /// <summary>Color de quien golpea (el clon de Fantasma ya llega como su dueña); false si nadie lo causa.</summary>
        private bool TryGetSourceColor(FakeBladeController source, out Color color)
        {
            if (source != null)
                for (int i = 0; i < _tracked.Count; i++)
                    if (_tracked[i].Blade == source)
                    {
                        color = _tracked[i].Color;
                        return true;
                    }
            color = default;
            return false;
        }
        #endregion

        #region Números
        /// <summary>
        /// Un golpe recibido: número nuevo o, con los golpes sumados (Opciones), lo suma al último número de esa peonza si
        /// llega a menos de sumWindow del anterior (el número cambia al total, toma el color de quien la acaba de golpear
        /// y vuelve a hacer el pop con el tamaño y el glow del total).
        /// </summary>
        private void ShowDamage(Tracked victim, float amount, FakeBladeController source)
        {
            GameSettings settings = SettingsService.Current;
            if (amount < MinDamage || !settings.damageNumbers) return;
            // Fuente o brillo cambiados en Opciones (también desde la pausa)
            if (_appliedFont != settings.damageNumberFont || _appliedGlow != settings.damageNumberGlow) ApplyFont(settings.damageNumberFont);

            DamageNumberStyle style = Style;
            Popup p = victim.Last;
            // Popup.Age vuelve a 0 en cada golpe: es el tiempo desde el último
            bool sum = settings.damageNumbersSummed && p != null && p.Active && p.Owner == victim && p.Age < style.sumWindow;
            if (sum)
            {
                p.Total += amount;
            }
            else
            {
                p = _numbers[_nextNumber];
                _nextNumber = (_nextNumber + 1) % NumberPool;
                p.Total = amount;
                p.Owner = victim;
                victim.Last = p;
            }

            Vector2 jitter = UnityEngine.Random.insideUnitCircle * 0.3f;
            p.World = victim.Blade.Position + new Vector3(jitter.x, 1.1f, jitter.y);
            p.Age = 0f;
            p.Strength = style.Strength(p.Total);
            p.Text.fontSize = Mathf.Lerp(style.sizeMin, style.sizeMax, p.Strength) * _px;
            p.Text.SetText("{0:0}", p.Total);

            // Aspecto: el de un poder manda sobre el color de un jugador, y este sobre el gris sin atacante. Al sumar, un
            // golpe nuevo solo lo cambia si no es de menos (2026-11-02, pedido por el usuario: azul y luego la pared,
            // sigue azul; con Rayos o Fuego, la suma se queda con su aspecto)
            DamageNumberSpecialFx fx = style.FindFx(victim.Blade.DamageSpecial);
            bool hasSource = TryGetSourceColor(source, out Color sourceColor);
            int rank = fx != null ? 2 : hasSource ? 1 : 0;
            if (!sum || rank >= p.Rank)
            {
                p.Rank = rank;
                ApplyFx(p, style, fx, hasSource ? sourceColor : style.noSourceFill);
            }
            // El glow normal sigue al total
            else if (p.Fx == null || p.Fx.glow.a <= 0f)
            {
                p.Glow = style.GlowColor(p.Total);
                p.Material.SetColor(ShaderUtilities.ID_GlowColor, p.Glow);
            }
            // El último encima de los demás
            p.Root.SetAsLastSibling();
            p.Active = true;
            p.Root.gameObject.SetActive(true);
            Animate(p);
        }

        /// <summary>
        /// Aspecto del número: el del poder que causa el daño (textura que se mueve dentro, contorno, glow y parpadeo) o el
        /// normal. Solo cambia valores del material propio del número (sin GC).
        /// </summary>
        private static void ApplyFx(Popup p, DamageNumberStyle style, DamageNumberSpecialFx fx, Color sourceColor)
        {
            Material m = p.Material;
            p.Fx = fx;
            // Degradado del poder (Fuego, Rayos): sustituye al relleno; el color del texto queda en blanco para el alfa
            bool gradient = fx != null && fx.gradientTop.a > 0f;
            p.Text.enableVertexGradient = gradient;
            if (gradient) p.Text.colorGradient = new VertexGradient(fx.gradientTop, fx.gradientTop, fx.gradientBottom, fx.gradientBottom);
            p.Fill = gradient ? Color.white : fx != null && fx.fill.a > 0f ? fx.fill : sourceColor;
            p.Glow = fx != null && fx.glow.a > 0f ? fx.glow : style.GlowColor(p.Total);
            m.SetColor(ShaderUtilities.ID_GlowColor, p.Glow);
            m.SetColor(ShaderUtilities.ID_OutlineColor, fx != null ? fx.outline : style.outline);
            m.SetTexture(ShaderUtilities.ID_FaceTex, fx != null && fx.faceTexture != null ? fx.faceTexture : Texture2D.whiteTexture);
            m.SetTextureScale(ShaderUtilities.ID_FaceTex, fx != null ? fx.faceTiling : Vector2.one);
            m.SetFloat(FaceSpeedX, fx != null ? fx.faceScroll.x : 0f);
            m.SetFloat(FaceSpeedY, fx != null ? fx.faceScroll.y : 0f);
        }

        private static void Hide(Popup p)
        {
            p.Active = false;
            p.Root.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            float dt = Time.unscaledDeltaTime;
            float life = Style.life;
            for (int i = 0; i < NumberPool; i++)
            {
                Popup p = _numbers[i];
                if (!p.Active) continue;
                p.Age += dt;
                if (p.Age >= life || _camera == null) Hide(p);
                else Animate(p);
            }
        }

        /// <summary>
        /// Pop: sale grande (×1,5 a ×2,4 de su tamaño final, según el daño) y baja a su tamaño con un solo asentamiento
        /// suave, sin rebote de goma. Los golpes fuertes tiemblan y giran un poco al salir. Después suben y se desvanecen.
        /// </summary>
        private void Animate(Popup p)
        {
            DamageNumberStyle style = Style;
            float t = p.Age / style.life;
            float k = p.Strength;

            float pop = Mathf.Lerp(style.popMin, style.popMax, k);
            float scale = 1f + pop * Mathf.Exp(-style.settleSpeed * p.Age) * Mathf.Cos(style.settleWobble * p.Age);
            p.Root.localScale = new Vector3(scale, scale, 1f);

            float shake = 0f, angle = 0f;
            float shakeTime = Mathf.Lerp(style.shakeTime.x, style.shakeTime.y, k);
            if (k >= style.shakeFrom && p.Age < shakeTime)
            {
                float a = (1f - p.Age / shakeTime) * k;
                shake = Mathf.Sin(p.Age * 110f) * style.shakeMax * a;
                angle = Mathf.Sin(p.Age * 70f) * style.shakeAngle * a;
            }
            p.Root.localRotation = Quaternion.Euler(0f, 0f, angle);

            if (_camera != null)
            {
                Vector3 screen = _camera.WorldToScreenPoint(p.World);
                if (screen.z > 0f)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
                    // Subida y temblor en píxeles de UI enteros, para no romper la rejilla pixel
                    p.Root.anchoredPosition = local + new Vector2(Mathf.Round(shake) * _px, Mathf.Round(t * style.rise) * _px);
                }
            }

            // El glow y el contorno se desvanecen con el alfa del texto
            Color color = p.Fill;
            color.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            p.Text.color = color;

            // Parpadeo del glow de algunos poderes (Rayos)
            if (p.Fx != null && p.Fx.flicker > 0f)
            {
                Color glow = p.Glow;
                glow.a *= 1f - p.Fx.flicker * Mathf.PerlinNoise(p.Age * p.Fx.flickerSpeed, p.Strength * 7f);
                p.Material.SetColor(ShaderUtilities.ID_GlowColor, glow);
            }
        }
        #endregion
    }
}
