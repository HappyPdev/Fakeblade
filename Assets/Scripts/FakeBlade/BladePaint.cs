using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FakeBlade.Core
{
    /// <summary>
    /// Pinta el modelo de una peonza con una <see cref="BladeColorScheme"/> (en partida y en las
    /// vistas previas). Cada renderer se asigna a una pieza por su nombre o el de sus padres:
    /// - "Tip" / "Punta" → punta; "Body" / "Cuerpo" → cuerpo; "Core" / "Nucleo" → núcleo;
    /// - el resto ("Ring", "Blade", "Disco", "Anilla" o sin nombre reconocible) → disco.
    /// El núcleo además brilla (emisión) con el color de su poder: ver <see cref="SetCoreGlow"/>.
    /// Usa MaterialPropertyBlock, así que los materiales se comparten entre peonzas.
    /// Con canFade, la peonza puede volverse transparente (Fantasma, 2026-10-27; ver <see cref="SetAlpha"/>).
    /// </summary>
    public sealed class BladePaint
    {
        private const int SlotCount = 4;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private const string EmissionKeyword = "_EMISSION";

        // Copia con emisión de cada material de núcleo que no la tenía (una por material, compartida)
        private static readonly Dictionary<Material, Material> s_glowMaterials = new Dictionary<Material, Material>();
        // Copia transparente de cada material de las piezas (una por material, compartida) y el camino de vuelta
        private static readonly Dictionary<Material, Material> s_fadeOf = new Dictionary<Material, Material>();
        private static readonly Dictionary<Material, Material> s_opaqueOf = new Dictionary<Material, Material>();
        // Lista de trabajo para cambiar los materiales sin crear arrays
        private static readonly List<Material> s_materials = new List<Material>();

        private readonly List<Renderer>[] _slots = new List<Renderer>[SlotCount];
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Color _glow = new Color(-1f, 0f, 0f);
        private readonly Color[] _colors = new Color[SlotCount];
        private bool _hasColors;
        private readonly bool _canFade;
        private float _alpha = 1f;
        private bool _faded;

        public BladePaint(Transform root, bool canFade = false)
        {
            for (int i = 0; i < SlotCount; i++) _slots[i] = new List<Renderer>();
            if (root == null) return;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                // Solo mallas: fuera partículas, estelas e iconos (sprites), y los planos con
                // imagen (p. ej. "Imagen de Nucleo"), que llevan su propio material
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (IsImagePlane(r.name)) continue;

                ComponentSlot slot = SlotOf(r.transform, root);
                _slots[(int)slot].Add(r);
                if (slot == ComponentSlot.Core) EnsureEmission(r);
                if (canFade) PrepareFade(r);
            }
            _canFade = canFade;
        }

        /// <summary>¿El modelo tiene núcleo propio? (si no, el brillo del poder no se ve)</summary>
        public bool HasCore => _slots[(int)ComponentSlot.Core].Count > 0;

        public void Apply(BladeColorScheme scheme)
        {
            for (int s = 0; s < SlotCount; s++) _colors[s] = scheme.Get((ComponentSlot)s);
            _hasColors = true;
            ApplyColors();
        }

        private void ApplyColors()
        {
            for (int s = 0; s < SlotCount; s++)
            {
                Color color = _colors[s];
                color.a *= _alpha;
                List<Renderer> renderers = _slots[s];
                for (int i = 0; i < renderers.Count; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, color);
                    r.SetPropertyBlock(_block);
                }
            }
        }

        /// <summary>
        /// Opacidad de la peonza (1 = opaca). Por debajo de 1 cambia los materiales de las piezas por sus copias transparentes
        /// (sin sombra) y la transparencia va en el alfa del color; a 1, vuelve a los opacos. Solo con canFade. Sin asignaciones:
        /// las copias se crean al montar el modelo.
        /// </summary>
        public void SetAlpha(float alpha)
        {
            if (!_canFade || !_hasColors) return;
            alpha = Mathf.Clamp01(alpha);
            bool faded = alpha < 0.999f;
            if (faded == _faded && Mathf.Abs(alpha - _alpha) < 0.002f) return;
            _alpha = alpha;
            if (faded != _faded) SwapMaterials(faded);
            ApplyColors();
        }

        private void SwapMaterials(bool faded)
        {
            _faded = faded;
            Dictionary<Material, Material> map = faded ? s_fadeOf : s_opaqueOf;
            for (int s = 0; s < SlotCount; s++)
            {
                List<Renderer> renderers = _slots[s];
                for (int i = 0; i < renderers.Count; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null) continue;
                    r.GetSharedMaterials(s_materials);
                    for (int m = 0; m < s_materials.Count; m++)
                        if (s_materials[m] != null && map.TryGetValue(s_materials[m], out Material swap)) s_materials[m] = swap;
                    r.SetSharedMaterials(s_materials);
                    r.shadowCastingMode = faded ? ShadowCastingMode.Off : ShadowCastingMode.On;
                }
            }
            s_materials.Clear();
        }

        /// <summary>Crea (una vez por material) la copia transparente de cada material del renderer.</summary>
        private static void PrepareFade(Renderer r)
        {
            Material[] materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material m = materials[i];
                if (m == null || BladeOverlay.IsOverlay(m) || s_opaqueOf.ContainsKey(m)) continue;
                if (s_fadeOf.TryGetValue(m, out Material fade) && fade != null) continue;
                fade = MakeTransparent(m);
                s_fadeOf[m] = fade;
                s_opaqueOf[fade] = m;
            }
        }

        /// <summary>
        /// Copia transparente de un material URP Lit (como si se cambiara Surface Type a Transparent en el Inspector). Sigue
        /// escribiendo la profundidad: la peonza se ve como un solo cuerpo de humo, sin las piezas de dentro.
        /// </summary>
        private static Material MakeTransparent(Material source)
        {
            var m = new Material(source) { name = source.name + " (Fade)" };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 1f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            return m;
        }

        /// <summary>Brillo del núcleo: color del poder × intensidad (0 = apagado; más de 1 da bloom).</summary>
        public void SetCoreGlow(Color color, float intensity)
        {
            Color glow = color * Mathf.Max(0f, intensity);
            glow.a = 1f;
            if (glow == _glow) return;
            _glow = glow;

            List<Renderer> renderers = _slots[(int)ComponentSlot.Core];
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, glow);
                r.SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// Intensidad del brillo del núcleo según el poder: crece con la carga de la esfera, late
        /// suave cuando está lista y fuerte mientras el poder está activo.
        /// </summary>
        public static float CoreGlow(float energy, bool ready, bool active, float time)
        {
            if (active) return 1.6f + 0.4f * Mathf.Sin(time * 12f);
            if (ready) return 1f + 0.25f * Mathf.Sin(time * 5f);
            return Mathf.Clamp01(energy) * 0.7f;
        }

        public static bool IsImagePlane(string objectName)
        {
            string n = objectName.ToLowerInvariant();
            return n.Contains("imagen") || n.Contains("image") || n.Contains("sprite") || n.Contains("icon");
        }

        private static ComponentSlot SlotOf(Transform t, Transform root)
        {
            for (Transform current = t; current != null; current = current.parent)
            {
                string n = current.name.ToLowerInvariant();
                if (n.Contains("tip") || n.Contains("punta")) return ComponentSlot.Tip;
                if (n.Contains("core") || n.Contains("nucleo") || n.Contains("núcleo")) return ComponentSlot.Core;
                if (n.Contains("body") || n.Contains("cuerpo")) return ComponentSlot.Body;
                if (n.Contains("ring") || n.Contains("blade") || n.Contains("disco") || n.Contains("anilla")) return ComponentSlot.Blade;
                if (current == root) break;
            }
            return ComponentSlot.Blade;
        }

        /// <summary>
        /// El brillo necesita la emisión activada en el material. Lo ideal es que el núcleo use
        /// BladeCoreMaterial (ya la tiene); si no, se usa una copia de su material con la emisión.
        /// </summary>
        private static void EnsureEmission(Renderer r)
        {
            Material[] materials = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material m = materials[i];
                if (m == null || m.IsKeywordEnabled(EmissionKeyword) || BladeOverlay.IsOverlay(m)) continue;

                if (!s_glowMaterials.TryGetValue(m, out Material glow) || glow == null)
                {
                    glow = new Material(m) { name = m.name + " (Glow)" };
                    glow.EnableKeyword(EmissionKeyword);
                    glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    s_glowMaterials[m] = glow;
                }
                materials[i] = glow;
                changed = true;
            }
            if (changed) r.sharedMaterials = materials;
        }
    }
}
