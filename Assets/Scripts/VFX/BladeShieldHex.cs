using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Escudos del Escudo (K8, 2026-10-29; elegido por el usuario: panal de hexágonos, GDD 7.6). Cada escudo que queda es una
    /// franja curva de hexágonos dorados en su ángulo real (ShieldsAbility.CenterAngle, el mismo con el que se decide si para
    /// el choque), que gira con él. Un escudo nuevo crece hexágono a hexágono con un destello; uno que se rompe se queda donde
    /// estaba y se hace añicos hacia fuera del choque (el movimiento de cada hexágono lo hace el shader FakeBlade/ShieldHex).
    /// Los valores, de VfxLibrary.specialEffects (Escudo) → Shield Hex, en vivo.
    ///
    /// Clase C# pura que posee FakeBladeController. Un objeto por escudo, creados la primera vez que salen (no en un choque), y
    /// una malla compartida que solo se rehace si cambia el panal: cero GC en combate.
    /// </summary>
    public sealed class BladeShieldHex
    {
        private const int MaxShields = 8;

        private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int EdgeFromId = Shader.PropertyToID("_EdgeFrom");
        private static readonly int OutlineFromId = Shader.PropertyToID("_OutlineFrom");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int FxTimeId = Shader.PropertyToID("_FxTime");
        private static readonly int TwinkleId = Shader.PropertyToID("_Twinkle");
        private static readonly int TwinkleSpeedId = Shader.PropertyToID("_TwinkleSpeed");
        private static readonly int AppearId = Shader.PropertyToID("_Appear");
        private static readonly int ShatterId = Shader.PropertyToID("_Shatter");
        private static readonly int ShatterDirId = Shader.PropertyToID("_ShatterDir");
        private static readonly int ShatterParamsId = Shader.PropertyToID("_ShatterParams");

        /// <summary>Un escudo: su objeto y en qué está (apareciendo, puesto o haciéndose añicos).</summary>
        private sealed class Slot
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public MeshFilter Filter;
            public bool Up;
            public int Breaks;
            /// <summary>Segundos desde que apareció (crece en Appear Time).</summary>
            public float Appear;
            /// <summary>Segundos desde que se rompió (-1 = no se está rompiendo).</summary>
            public float Shatter = -1f;
            public Vector3 ShatterPosition;
            public Quaternion ShatterRotation;
            public Vector3 ShatterDirection;
        }

        private readonly FakeBladeController _blade;
        private readonly Slot[] _slots = new Slot[MaxShields];
        private MaterialPropertyBlock _block;
        private Transform _root;
        private Mesh _mesh;
        private int _meshKey;
        private float _time;
        private bool _built;
        // El Escudo que se pinta (al cambiar de poder o de piezas, los escudos empiezan de cero, sin añicos)
        private ShieldsAbility _shields;

        public BladeShieldHex(FakeBladeController blade)
        {
            _blade = blade;
        }

        /// <summary>
        /// tipOffset: altura de la punta respecto al pivote (BladeParticles). style: los escudos del poder (o null);
        /// shields: el Escudo de la peonza (o null si su poder es otro).
        /// </summary>
        public void Tick(float dt, float tipOffset, ShieldHexStyle style, ShieldsAbility shields)
        {
            if (style == null || style.material == null || shields == null)
            {
                Hide();
                return;
            }
            if (!_built) Build();
            // Otro poder u otra peonza montada: sin añicos de lo anterior
            if (shields != _shields)
            {
                _shields = shields;
                for (int i = 0; i < MaxShields; i++) ResetSlot(_slots[i], shields, i);
            }

            _time += dt;
            int count = shields.ShieldCount;
            float radius = shields.Radius * style.radiusScale;
            float arc = shields.ArcRadians;
            Mesh mesh = HexMesh(style, radius, arc);
            Vector3 pos = _blade.Position;
            var anchor = new Vector3(pos.x, pos.y + tipOffset + style.height, pos.z);
            float rootScale = Mathf.Max(0.001f, _blade.transform.lossyScale.x);
            var scale = new Vector3(1f / rootScale, 1f / rootScale, 1f / rootScale);
            bool destroyed = _blade.IsDestroyed;

            for (int i = 0; i < MaxShields; i++)
            {
                Slot slot = _slots[i];
                bool up = i < count && shields.IsUp(i) && !destroyed;
                int breaks = shields.BreakCount(i);
                // Se ha roto en un choque: se queda donde estaba y se hace añicos hacia fuera del choque
                if (slot.Up && !up && breaks != slot.Breaks && !destroyed)
                {
                    slot.Shatter = 0f;
                    slot.ShatterPosition = anchor;
                    slot.ShatterRotation = Quaternion.Euler(0f, -shields.BreakAngle(i) * Mathf.Rad2Deg, 0f);
                    slot.ShatterDirection = Quaternion.Inverse(slot.ShatterRotation) * shields.BreakDirection(i);
                }
                if (up && !slot.Up)
                {
                    slot.Appear = 0f;
                    slot.Shatter = -1f; // uno nuevo en su sitio corta los añicos del anterior
                }
                slot.Up = up;
                slot.Breaks = breaks;

                if (slot.Shatter >= 0f)
                {
                    slot.Shatter += dt;
                    if (slot.Shatter > style.shatterTime || destroyed) slot.Shatter = -1f;
                }
                bool shattering = slot.Shatter >= 0f;
                bool visible = up || shattering;
                if (slot.Renderer.enabled != visible) slot.Renderer.enabled = visible;
                if (!visible) continue;

                if (slot.Filter.sharedMesh != mesh) slot.Filter.sharedMesh = mesh;
                if (slot.Renderer.sharedMaterial != style.material) slot.Renderer.sharedMaterial = style.material;
                Transform t = slot.Transform;
                if (shattering)
                {
                    t.SetPositionAndRotation(slot.ShatterPosition, slot.ShatterRotation);
                }
                else
                {
                    slot.Appear += dt;
                    t.SetPositionAndRotation(anchor, Quaternion.Euler(0f, -shields.CenterAngle(i) * Mathf.Rad2Deg, 0f));
                }
                t.localScale = scale;
                SetBlock(style, slot, shattering);
            }
        }

        /// <summary>Valores del shader de un escudo: colores y brillo; al aparecer, crecimiento y destello; al romperse, añicos.</summary>
        private void SetBlock(ShieldHexStyle style, Slot slot, bool shattering)
        {
            _block.SetColor(FillColorId, style.fillColor);
            _block.SetColor(EdgeColorId, style.edgeColor);
            _block.SetColor(OutlineColorId, style.outlineColor);
            _block.SetFloat(EdgeFromId, style.edgeFrom);
            _block.SetFloat(OutlineFromId, style.outlineFrom);
            _block.SetFloat(AlphaId, style.alpha);
            _block.SetFloat(FxTimeId, _time);
            _block.SetFloat(TwinkleId, style.twinkle);
            _block.SetFloat(TwinkleSpeedId, style.twinkleSpeed);
            float appear = shattering ? 1f : Mathf.Clamp01(slot.Appear / style.appearTime);
            _block.SetFloat(AppearId, appear);
            // Destello blanco al aparecer, que se apaga mientras crece
            _block.SetFloat(FlashId, shattering ? 0f : 1f - appear);
            _block.SetVector(ShatterId, new Vector4(shattering ? slot.Shatter : 0f, style.shatterTime, shattering ? 1f : 0f, 0f));
            Vector3 d = slot.ShatterDirection;
            _block.SetVector(ShatterDirId, new Vector4(d.x, d.y, d.z, style.directionalSpeed));
            _block.SetVector(ShatterParamsId, new Vector4(style.outwardSpeed, style.upSpeed, style.gravity, style.spin));
            slot.Renderer.SetPropertyBlock(_block);
        }

        private static void ResetSlot(Slot slot, ShieldsAbility shields, int index)
        {
            slot.Up = false;
            slot.Shatter = -1f;
            slot.Breaks = shields != null ? shields.BreakCount(index) : 0;
        }

        public void Hide()
        {
            if (!_built) return;
            for (int i = 0; i < MaxShields; i++)
            {
                Slot slot = _slots[i];
                if (slot.Renderer.enabled) slot.Renderer.enabled = false;
                ResetSlot(slot, _shields, i);
            }
        }

        private void Build()
        {
            _built = true;
            _block = new MaterialPropertyBlock();
            _root = new GameObject("ShieldHex").transform;
            _root.SetParent(_blade.transform, false);
            for (int i = 0; i < MaxShields; i++)
            {
                var go = new GameObject("Shield" + i);
                go.transform.SetParent(_root, false);
                var slot = new Slot
                {
                    Transform = go.transform,
                    Filter = go.AddComponent<MeshFilter>(),
                    Renderer = go.AddComponent<MeshRenderer>()
                };
                MeshRenderer renderer = slot.Renderer;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.enabled = false;
                // Los añicos salen lejos del escudo: límites amplios para que no se recorten
                renderer.localBounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(8f, 6f, 8f));
                ScreenPixelation.MoveToVfxLayer(go); // nítidos sobre la escena pixelada (O2)
                _slots[i] = slot;
            }
        }

        /// <summary>
        /// Malla de un escudo: filas de hexágonos con la punta arriba sobre un trozo de cilindro (radio y ancho del escudo),
        /// centrado en +X e inclinado hacia fuera según Lean; las filas impares, desplazadas medio hexágono y con uno menos. Cada
        /// hexágono, plano y de cara hacia fuera, con su centro (uv3), su semilla (uv2.x) y la distancia al centro (uv.x: 0 en el centro, 1 en el borde). Se
        /// rehace solo si cambia el panal.
        /// </summary>
        private Mesh HexMesh(ShieldHexStyle style, float radius, float arc)
        {
            int rows = Mathf.Max(1, style.rows);
            float size = Mathf.Max(0.01f, style.hexSize);
            int key = rows;
            key = key * 397 ^ style.hexSize.GetHashCode();
            key = key * 397 ^ style.gap.GetHashCode();
            key = key * 397 ^ radius.GetHashCode();
            key = key * 397 ^ arc.GetHashCode();
            key = key * 397 ^ style.lean.GetHashCode();
            if (_mesh != null && key == _meshKey) return _mesh;
            // Inclinado hacia fuera (Lean): las filas suben por la pared inclinada y cada hexágono mira hacia fuera y arriba
            float lean = style.lean * Mathf.Deg2Rad;
            float leanUp = Mathf.Cos(lean), leanOut = Mathf.Sin(lean);
            _meshKey = key;

            float width = Mathf.Sqrt(3f) * size;
            float length = Mathf.Max(width, arc * radius);
            int cols = Mathf.Max(1, Mathf.FloorToInt(length / width));
            float spacing = length / cols;
            float drawn = size * (1f - style.gap);

            int hexes = 0;
            for (int r = 0; r < rows; r++) hexes += r % 2 == 0 ? cols : Mathf.Max(1, cols - 1);
            var vertices = new Vector3[hexes * 7];
            var normals = new Vector3[hexes * 7];
            var uv = new Vector2[hexes * 7];
            var uv2 = new Vector2[hexes * 7];
            var uv3 = new Vector3[hexes * 7];
            var triangles = new int[hexes * 18];

            int h = 0;
            for (int r = 0; r < rows; r++)
            {
                bool odd = r % 2 == 1;
                int inRow = odd ? Mathf.Max(1, cols - 1) : cols;
                float y = size + r * 1.5f * size;
                for (int c = 0; c < inRow; c++, h++)
                {
                    float along = (c - (inRow - 1) * 0.5f) * spacing;
                    float phi = along / radius;
                    var outward = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                    var tangent = new Vector3(-outward.z, 0f, outward.x);
                    Vector3 up = Vector3.up * leanUp + outward * leanOut;
                    Vector3 normal = outward * leanUp - Vector3.up * leanOut;
                    Vector3 center = outward * radius + up * y;
                    // Semilla fija por hexágono (la misma en cada malla): su brillo, su aparición y sus añicos
                    float seed = Mathf.Repeat(Mathf.Sin(h * 12.9898f + r * 78.233f) * 43758.547f, 1f);
                    int v = h * 7;
                    vertices[v] = center;
                    uv[v] = Vector2.zero;
                    for (int k = 0; k < 6; k++)
                    {
                        // Punta arriba: esquinas a 90º + 60º·k en el plano (tangente, arriba)
                        float a = (90f + 60f * k) * Mathf.Deg2Rad;
                        vertices[v + 1 + k] = center + (tangent * Mathf.Cos(a) + up * Mathf.Sin(a)) * drawn;
                        uv[v + 1 + k] = new Vector2(1f, 0f);
                    }
                    for (int k = 0; k < 7; k++)
                    {
                        normals[v + k] = normal;
                        uv2[v + k] = new Vector2(seed, 0f);
                        uv3[v + k] = center;
                    }
                    int tri = h * 18;
                    for (int k = 0; k < 6; k++)
                    {
                        triangles[tri + k * 3] = v;
                        triangles[tri + k * 3 + 1] = v + 1 + (k + 1) % 6;
                        triangles[tri + k * 3 + 2] = v + 1 + k;
                    }
                }
            }

            if (_mesh == null) _mesh = new Mesh { name = "ShieldHex" };
            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.normals = normals;
            _mesh.uv = uv;
            _mesh.uv2 = uv2;
            _mesh.SetUVs(2, uv3);
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
            return _mesh;
        }
    }
}
