using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Cúpula de la carga de un especial (Onda de choque, K8, 2026-10-18; boceto del usuario; GDD 7.6), con los valores de su
    /// ChargeDomeStyle (VfxLibrary.specialEffects[...].dome): durante cada carga, una media esfera translúcida crece sobre
    /// la peonza (deprisa al principio y luego despacio), con el borde de cómic y líneas en remolino que giran por dentro, y
    /// tiembla al final; al acabar la carga se infla y se desvanece con un destello (la onda explota).
    ///
    /// Clase C# pura que posee FakeBladeController. La malla (compartida) y el objeto se crean una vez, en el primer frame
    /// de una peonza cuyo especial tiene cúpula; después solo se mueve, se escala y cambian sus valores con un
    /// MaterialPropertyBlock: cero GC en combate. Se lee en vivo de la biblioteca.
    /// Tornado (2026-10-18, boceto del usuario): la misma pieza mientras atrae, con la fuerza del empujón como carga: sale
    /// como un embudo que sube desde el suelo, se ensancha hasta ser la cúpula con un aro de picos abierto arriba y revienta
    /// al soltar el empujón (funnelPhase, topCut... en ChargeDomeStyle).
    /// </summary>
    public sealed class BladeChargeDome
    {
        private const int Segments = 32;
        private const int Rings = 10;

        private static readonly int BodyColorId = Shader.PropertyToID("_BodyColor");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int SwirlColorId = Shader.PropertyToID("_SwirlColor");
        private static readonly int SwirlColor2Id = Shader.PropertyToID("_SwirlColor2");
        private static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int SwirlLinesId = Shader.PropertyToID("_SwirlLines");
        private static readonly int SwirlTwistId = Shader.PropertyToID("_SwirlTwist");
        private static readonly int SwirlPhaseId = Shader.PropertyToID("_SwirlPhase");
        private static readonly int SwirlWidthId = Shader.PropertyToID("_SwirlWidth");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int LinesTexId = Shader.PropertyToID("_LinesTex");
        private static readonly int LinesId = Shader.PropertyToID("_Lines");
        private static readonly int AxisId = Shader.PropertyToID("_Axis");
        private static readonly int AxisT1Id = Shader.PropertyToID("_AxisT1");
        private static readonly int AxisT2Id = Shader.PropertyToID("_AxisT2");
        private static readonly int FunnelId = Shader.PropertyToID("_Funnel");
        private static readonly int FunnelShapeId = Shader.PropertyToID("_FunnelShape");
        private static readonly int TopCutId = Shader.PropertyToID("_TopCut");

        private static Mesh _mesh;
        private static Mesh _meshInside;
        private static readonly int InsideAlphaId = Shader.PropertyToID("_InsideAlpha");

        private readonly FakeBladeController _blade;
        private Transform _transform;
        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private MaterialPropertyBlock _block;
        private bool _built;
        private bool _visible;
        private bool _wasWindingUp;
        private float _phase;
        private float _linesPhase;
        private float _precess;
        private float _time;
        // Explosión: tiempo que le queda (0 = no está explotando) y tamaño de la cúpula al reventar
        private float _pop;
        private float _popRadius;
        private float _lastRadius;
        // Embudo (Tornado): cuánto es embudo (0-1) y cuánto ha subido, al reventar; y giro de los picos de arriba
        private float _lastFunnel;
        private float _lastRise = 1f;
        private float _topPhase;

        private readonly string _name;
        private readonly int _sortingOrder;

        /// <summary>
        /// name: nombre del objeto. sortingOrder: orden entre las cúpulas de la misma peonza (la de la base de la Embestida,
        /// 1: se pinta después, por delante de la grande).
        /// </summary>
        public BladeChargeDome(FakeBladeController blade, string name = "ChargeDome", int sortingOrder = 0)
        {
            _blade = blade;
            _name = name;
            _sortingOrder = sortingOrder;
        }

        /// <summary>
        /// tipOffset: altura de la punta respecto al pivote (BladeParticles). style: la cúpula del especial (o null).
        /// windingUp: está en una carga; progress: lo que lleva de ella (0-1).
        /// </summary>
        public void Tick(float dt, float tipOffset, ChargeDomeStyle style, bool windingUp, float progress)
        {
            if (style == null || style.material == null)
            {
                Hide();
                return;
            }
            if (!_built) Build();
            if (_renderer.sharedMaterial != style.material) _renderer.sharedMaterial = style.material;
            // Con interior (insideAlpha), la malla con las caras de dentro
            Mesh mesh = DomeMesh(style.insideAlpha > 0f);
            if (_filter.sharedMesh != mesh) _filter.sharedMesh = mesh;

            // Al acabar la carga, la cúpula revienta (también si el poder termina con ella: la última onda)
            if (_wasWindingUp && !windingUp && !_blade.IsDestroyed)
            {
                _pop = style.popTime;
                _popRadius = _lastRadius;
            }
            _wasWindingUp = windingUp;
            if (_blade.IsDestroyed) _pop = 0f;

            if (!windingUp && _pop <= 0f)
            {
                Hide();
                return;
            }

            Vector3 pos = _blade.Position;
            var tip = new Vector3(pos.x, pos.y + tipOffset + style.height, pos.z);
            _time += dt;
            _phase = Mathf.Repeat(_phase + style.swirlSpeed * dt, 1f);
            _linesPhase = Mathf.Repeat(_linesPhase + style.linesSpeed * dt, 1f);
            _precess = Mathf.Repeat(_precess + style.axisPrecess * dt, 1f);
            _topPhase = Mathf.Repeat(_topPhase + style.topSpikesSpeed * dt, 1f);

            float radius, alpha = 1f, flash = 0f, funnel, rise;
            if (windingUp)
            {
                float p = Mathf.Clamp01(progress);
                radius = style.radius * Mathf.Lerp(style.startRadius, 1f, 1f - Mathf.Pow(1f - p, style.growEase));
                // Tornado: al principio, embudo que sube desde el suelo; después se ensancha y baja hasta ser la cúpula
                funnel = 0f;
                rise = 1f;
                if (style.funnelPhase > 0f)
                {
                    float up = Mathf.Clamp01(p / style.funnelPhase);
                    rise = 1f - (1f - up) * (1f - up);
                    funnel = 1f - Mathf.SmoothStep(0f, 1f, (p - style.funnelPhase) / style.funnelMorph);
                }
                _lastFunnel = funnel;
                _lastRise = rise;
                // Tiembla al final de la carga, cada vez más
                if (p > style.shakeFrom && style.shakeFrom < 1f)
                {
                    float k = (p - style.shakeFrom) / (1f - style.shakeFrom);
                    radius *= 1f + Mathf.Sin(_time * style.shakeRate * Mathf.PI * 2f) * style.shakeScale * k;
                }
                _lastRadius = radius;
            }
            else
            {
                // Explosión: se infla deprisa (frenando) y se desvanece
                _pop -= dt;
                float t = 1f - Mathf.Clamp01(_pop / Mathf.Max(0.01f, style.popTime));
                float ease = 1f - (1f - t) * (1f - t);
                radius = _popRadius * Mathf.Lerp(1f, style.popScale, ease);
                funnel = _lastFunnel;
                rise = _lastRise;
                alpha = 1f - t;
                flash = style.popFlash * (1f - t);
            }

            if (radius <= 0.001f || !VfxSystem.IsOnScreen(tip))
            {
                SetVisible(false);
                return;
            }

            float rootScale = Mathf.Max(0.001f, _blade.transform.lossyScale.x);
            _transform.position = tip;
            _transform.rotation = Quaternion.identity;
            _transform.localScale = new Vector3(radius, radius * style.heightScale * rise, radius) / rootScale;

            _block.SetColor(BodyColorId, style.bodyColor);
            _block.SetColor(RimColorId, style.rimColor);
            _block.SetColor(OutlineColorId, style.outlineColor);
            _block.SetColor(SwirlColorId, style.swirlColor);
            _block.SetColor(SwirlColor2Id, style.swirlColor2);
            _block.SetFloat(RimWidthId, style.rimWidth);
            _block.SetFloat(OutlineWidthId, style.outlineWidth);
            _block.SetFloat(SwirlLinesId, style.swirlLines);
            _block.SetFloat(SwirlTwistId, style.swirlTwist);
            _block.SetFloat(SwirlPhaseId, _phase);
            _block.SetFloat(SwirlWidthId, style.swirlWidth);
            _block.SetFloat(FlashId, flash);
            _block.SetFloat(AlphaId, alpha);
            _block.SetFloat(InsideAlphaId, style.insideAlpha);
            _block.SetFloat(FunnelId, funnel);
            _block.SetVector(FunnelShapeId, style.funnelShape);
            _block.SetVector(TopCutId, new Vector4(style.topCut, style.topSpikes, _topPhase * style.topSpikes, 0f));
            SetLines(style);
            _renderer.SetPropertyBlock(_block);
            SetVisible(true);
        }

        /// <summary>
        /// Textura que recorre la cúpula y eje del giro: inclinado axisTilt grados y dando vueltas (bamboleo), con dos ejes
        /// perpendiculares a él para que el shader mida la vuelta alrededor y la altura a lo largo del eje.
        /// </summary>
        private void SetLines(ChargeDomeStyle style)
        {
            bool hasTexture = style.linesTexture != null;
            if (hasTexture) _block.SetTexture(LinesTexId, style.linesTexture);
            _block.SetVector(LinesId, new Vector4(style.linesTiling.x, style.linesTiling.y, _linesPhase,
                hasTexture ? style.linesTwist : -1f));

            float tilt = style.axisTilt * Mathf.Deg2Rad;
            float turn = _precess * Mathf.PI * 2f;
            var axis = new Vector3(Mathf.Sin(tilt) * Mathf.Cos(turn), Mathf.Cos(tilt), Mathf.Sin(tilt) * Mathf.Sin(turn));
            Vector3 t1 = Vector3.Cross(axis, Vector3.forward);
            if (t1.sqrMagnitude < 0.0001f) t1 = Vector3.Cross(axis, Vector3.right);
            t1.Normalize();
            Vector3 t2 = Vector3.Cross(axis, t1);
            _block.SetVector(AxisId, axis);
            _block.SetVector(AxisT1Id, t1);
            _block.SetVector(AxisT2Id, t2);
        }

        public void Hide()
        {
            _pop = 0f;
            _wasWindingUp = false;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            if (_renderer != null) _renderer.enabled = visible;
        }

        private void Build()
        {
            _built = true;
            _block = new MaterialPropertyBlock();
            var go = new GameObject(_name);
            _transform = go.transform;
            _transform.SetParent(_blade.transform, false);
            _filter = go.AddComponent<MeshFilter>();
            _filter.sharedMesh = DomeMesh(false);
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _renderer.enabled = false;
            _renderer.sortingOrder = _sortingOrder;
            // El embudo (Tornado) sube más que la media esfera: límites amplios para que no se recorte al salir de la pantalla
            _renderer.localBounds = new Bounds(new Vector3(0f, 2f, 0f), new Vector3(4f, 4.2f, 4f));
            ScreenPixelation.MoveToVfxLayer(go); // nítida sobre la escena pixelada (O2)
        }

        /// <summary>
        /// Media esfera de radio 1 con la base en y = 0, caras hacia fuera y la normal de la esfera. UV: x = vuelta alrededor
        /// (0-1), y = altura (0 en la base, 1 arriba): el shader dibuja con ellas el remolino. Con interior (Tornado, que se ve
        /// por dentro a través del corte de arriba): primero una copia con las caras hacia dentro (uv2.x = 1), para que se
        /// pinten antes que las de fuera y se mezclen bien siendo transparentes.
        /// </summary>
        private static Mesh DomeMesh(bool inside)
        {
            Mesh cached = inside ? _meshInside : _mesh;
            if (cached != null) return cached;
            int columns = Segments + 1;
            int count = columns * (Rings + 1);
            int copies = inside ? 2 : 1;
            var vertices = new Vector3[count * copies];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var uvs2 = new Vector2[vertices.Length];
            for (int copy = 0; copy < copies; copy++)
            for (int r = 0; r <= Rings; r++)
            {
                float v = r / (float)Rings;
                float phi = v * Mathf.PI * 0.5f;
                for (int s = 0; s <= Segments; s++)
                {
                    float u = s / (float)Segments;
                    float theta = u * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(theta));
                    int i = copy * count + r * columns + s;
                    vertices[i] = p;
                    normals[i] = p;
                    uvs[i] = new Vector2(u, v);
                    uvs2[i] = new Vector2(inside && copy == 0 ? 1f : 0f, 0f);
                }
            }

            var triangles = new int[Segments * Rings * 6 * copies];
            int t = 0;
            for (int copy = 0; copy < copies; copy++)
            {
                bool inner = inside && copy == 0;
                for (int r = 0; r < Rings; r++)
                for (int s = 0; s < Segments; s++)
                {
                    int a = copy * count + r * columns + s, b = a + 1, c = a + columns, d = c + 1;
                    // Las de dentro, con el orden de los vértices al revés (miran hacia dentro)
                    triangles[t++] = a;
                    triangles[t++] = inner ? b : c;
                    triangles[t++] = inner ? c : b;
                    triangles[t++] = c;
                    triangles[t++] = inner ? b : d;
                    triangles[t++] = inner ? d : b;
                }
            }

            var mesh = new Mesh { name = inside ? "ChargeDomeInside" : "ChargeDome" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.uv2 = uvs2;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            if (inside) _meshInside = mesh;
            else _mesh = mesh;
            return mesh;
        }
    }
}
