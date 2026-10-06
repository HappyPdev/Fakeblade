using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Pinta el modelo de una peonza con una <see cref="BladeColorScheme"/> (en partida y en las
    /// vistas previas). Cada renderer se asigna a una pieza por su nombre o el de sus padres:
    /// - "Tip" / "Punta" → punta; "Body" / "Cuerpo" → cuerpo; "Core" / "Nucleo" → núcleo;
    /// - el resto ("Ring", "Blade", "Disco", "Anilla" o sin nombre reconocible) → disco.
    /// El núcleo además brilla (emisión) con el color de su poder: ver <see cref="SetCoreGlow"/>.
    /// Usa MaterialPropertyBlock, así que los materiales se comparten entre peonzas.
    /// </summary>
    public sealed class BladePaint
    {
        private const int SlotCount = 4;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private const string EmissionKeyword = "_EMISSION";

        // Copia con emisión de cada material de núcleo que no la tenía (una por material, compartida)
        private static readonly Dictionary<Material, Material> s_glowMaterials = new Dictionary<Material, Material>();

        private readonly List<Renderer>[] _slots = new List<Renderer>[SlotCount];
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Color _glow = new Color(-1f, 0f, 0f);

        public BladePaint(Transform root)
        {
            for (int i = 0; i < SlotCount; i++) _slots[i] = new List<Renderer>();
            if (root == null) return;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                // Solo mallas: fuera partículas, estelas e iconos (sprites)
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

                ComponentSlot slot = SlotOf(r.transform, root);
                _slots[(int)slot].Add(r);
                if (slot == ComponentSlot.Core) EnsureEmission(r);
            }
        }

        /// <summary>¿El modelo tiene núcleo propio? (si no, el brillo del poder no se ve)</summary>
        public bool HasCore => _slots[(int)ComponentSlot.Core].Count > 0;

        public void Apply(BladeColorScheme scheme)
        {
            for (int s = 0; s < SlotCount; s++)
            {
                Color color = scheme.Get((ComponentSlot)s);
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
                if (m == null || m.IsKeywordEnabled(EmissionKeyword)) continue;

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
