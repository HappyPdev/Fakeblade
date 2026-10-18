using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Aviso de usos de los poderes con varios usos (Onda de choque: sus ondas; 2026-10-18; GDD 7.6), con los valores de
    /// VfxLibrary.usesIndicator: una marca por uso que queda, girando alrededor de la peonza, mirando a la cámara y latiendo;
    /// la del uso que se lanza se queda donde estaba, crece y se desvanece. Sirve para cualquier poder con MaxUses.
    ///
    /// Clase C# pura que posee FakeBladeController. Las marcas se crean una vez, la primera vez que hacen falta; después
    /// solo se mueven y cambian de color con un MaterialPropertyBlock: cero GC en combate. Se lee en vivo.
    /// </summary>
    public sealed class BladeUsesIndicator
    {
        private const int MaxMarks = 6;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int TilesXId = Shader.PropertyToID("_TilesX");
        private static readonly int TilesYId = Shader.PropertyToID("_TilesY");
        private static readonly int FrameId = Shader.PropertyToID("_Frame");
        private static Mesh _quad;

        private readonly FakeBladeController _blade;
        private readonly Transform[] _marks = new Transform[MaxMarks];
        private readonly MeshRenderer[] _renderers = new MeshRenderer[MaxMarks];
        private MaterialPropertyBlock _block;
        private bool _built;
        private int _shown;
        private float _angle;
        private float _time;
        // La marca del uso que se acaba de lanzar: dónde se quedó y lo que le queda de vida
        private Vector3 _popPosition;
        private float _pop;

        public BladeUsesIndicator(FakeBladeController blade)
        {
            _blade = blade;
        }

        /// <summary>usesLeft: usos que le quedan al poder activo (0 = ninguno o no está activo). color: el del poder.</summary>
        public void Tick(float dt, float tipOffset, UsesIndicatorStyle style, int usesLeft, Color color)
        {
            if (style == null || !style.enabled || style.material == null)
            {
                Hide();
                return;
            }
            if (!_built) Build();

            int count = Mathf.Clamp(usesLeft, 0, MaxMarks - 1);
            Vector3 pos = _blade.Position;
            var center = new Vector3(pos.x, pos.y + tipOffset + style.height, pos.z);
            _angle = Mathf.Repeat(_angle + style.spin * 360f * dt, 360f);
            _time += dt;

            // Se ha lanzado un uso: la última marca se queda donde estaba y se va
            if (count < _shown && _shown > 0)
            {
                _popPosition = MarkPosition(center, style, _shown - 1, _shown);
                _pop = style.popTime;
            }
            _shown = count;
            if (_pop > 0f) _pop -= dt;

            bool visible = VfxSystem.IsOnScreen(center);
            Camera cam = Camera.main;
            Quaternion facing = cam != null ? cam.transform.rotation : Quaternion.identity;
            Color tint = style.powerColor ? color : style.color;
            float pulse = 1f + Mathf.Max(0f, Mathf.Sin(_time * style.pulseRate * Mathf.PI * 2f)) * style.pulseScale;
            float rootScale = Mathf.Max(0.001f, _blade.transform.lossyScale.x);

            for (int i = 0; i < MaxMarks; i++)
            {
                bool popMark = i == MaxMarks - 1;
                bool on = visible && (popMark ? _pop > 0f : i < count);
                if (_renderers[i].enabled != on) _renderers[i].enabled = on;
                if (!on) continue;

                float size = style.size * pulse;
                Color c = tint;
                Vector3 position;
                if (popMark)
                {
                    float t = 1f - Mathf.Clamp01(_pop / Mathf.Max(0.01f, style.popTime));
                    size = style.size * (1f + t * 1.5f);
                    c.a *= 1f - t;
                    position = _popPosition;
                }
                else position = MarkPosition(center, style, i, count);

                Transform mark = _marks[i];
                mark.position = position;
                mark.rotation = facing;
                mark.localScale = Vector3.one * (size / rootScale);
                _block.SetColor(ColorId, c);
                _block.SetFloat(TilesXId, 1f);
                _block.SetFloat(TilesYId, 1f);
                _block.SetFloat(FrameId, 0f);
                _renderers[i].SetPropertyBlock(_block);
                if (_renderers[i].sharedMaterial != style.material) _renderers[i].sharedMaterial = style.material;
            }
        }

        /// <summary>Posición de la marca index de count, repartidas en círculo alrededor de la peonza.</summary>
        private Vector3 MarkPosition(Vector3 center, UsesIndicatorStyle style, int index, int count)
        {
            float a = (_angle + index * 360f / Mathf.Max(1, count)) * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * style.radius;
        }

        public void Hide()
        {
            _shown = 0;
            _pop = 0f;
            if (!_built) return;
            for (int i = 0; i < MaxMarks; i++)
                if (_renderers[i].enabled) _renderers[i].enabled = false;
        }

        private void Build()
        {
            _built = true;
            _block = new MaterialPropertyBlock();
            var root = new GameObject("UsesIndicator");
            root.transform.SetParent(_blade.transform, false);
            for (int i = 0; i < MaxMarks; i++)
            {
                var go = new GameObject("Mark" + i);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = Quad();
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.enabled = false;
                _marks[i] = go.transform;
                _renderers[i] = renderer;
            }
            ScreenPixelation.MoveToVfxLayer(root); // nítidas sobre la escena pixelada (O2)
        }

        /// <summary>Cuadrado de 1 × 1 m en el plano XY (mira a la cámara al girarlo como ella), con la UV entera.</summary>
        private static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "UseMarkQuad" };
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quad.RecalculateNormals();
            _quad.RecalculateBounds();
            return _quad;
        }
    }
}
