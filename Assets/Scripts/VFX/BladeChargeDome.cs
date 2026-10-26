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
    /// Rebote (2026-10-21, boceto del usuario): una burbuja (esfera entera, sphere) que crece en la carga, se queda mientras
    /// dura el poder y se desvanece al acabar; en cada rebote, una Zbam se abre en ella desde el punto del golpe (Pulse).
    /// Defensa (2026-10-23, boceto del usuario): dos embudos con picos (el de dentro, alto; el de la base, una corona en
    /// zigzag) que suben en la carga y se quedan como aura mientras dura; la Zbam, al embestirla.
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
        private static readonly int HitTexId = Shader.PropertyToID("_HitTex");
        private static readonly int HitId = Shader.PropertyToID("_Hit");
        private static readonly int HitRotId = Shader.PropertyToID("_HitRot");
        private static readonly int HitColorId = Shader.PropertyToID("_HitColor");
        private static readonly int HitShadowColorId = Shader.PropertyToID("_HitShadowColor");
        private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        private static readonly int MaskId = Shader.PropertyToID("_Mask");
        private static readonly int MaskScrollId = Shader.PropertyToID("_MaskScroll");
        private static readonly int MaskTopId = Shader.PropertyToID("_MaskTop");
        private static readonly int MaskEdgeColorId = Shader.PropertyToID("_MaskEdgeColor");
        private static readonly int WobbleId = Shader.PropertyToID("_Wobble");
        private static readonly int GlitchId = Shader.PropertyToID("_Glitch");

        private static Mesh _mesh;
        private static Mesh _meshInside;
        private static Mesh _meshSphere;
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
        // Zbam del golpe (Rebote): lo que le queda, hacia dónde (desde el centro, en el espacio de la malla) y su giro
        private float _hit;
        private bool _hitPending;
        private Vector3 _hitDir = Vector3.right;
        private float _hitAngle;
        // Máscara e inestabilidad (Defensa): desliz de la máscara, latido de los huecos, ondas, parpadeo y glitch
        private Vector2 _maskPhase;
        private float _pulse;
        private float _pulseWait;
        private float _wobblePhase;
        private float _flicker;
        private float _glitch;
        private float _glitchSeed;

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
            // Con interior (insideAlpha), la malla con las caras de dentro; burbuja (Rebote), la esfera entera
            Mesh mesh = style.sphere ? SphereMesh() : DomeMesh(style.insideAlpha > 0f);
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

            // Zbam del golpe (Rebote): se hincha un poco y destella
            float hitPhase = -1f;
            if (_hitPending)
            {
                _hitPending = false;
                _hit = style.hitDuration;
            }
            if (_hit > 0f)
            {
                _hit -= dt;
                hitPhase = 1f - Mathf.Clamp01(_hit / Mathf.Max(0.05f, style.hitDuration));
                float wave = Mathf.Sin(hitPhase * Mathf.PI) * (1f - hitPhase);
                radius *= 1f + style.hitBump * wave;
                flash = Mathf.Max(flash, style.hitFlash * (1f - hitPhase) * (1f - hitPhase));
            }

            if (radius <= 0.001f || !VfxSystem.IsOnScreen(tip))
            {
                SetVisible(false);
                return;
            }

            float rootScale = Mathf.Max(0.001f, _blade.transform.lossyScale.x);
            float height = radius * style.heightScale * rise;
            // La burbuja, con la base en la punta (la malla va de -1 a 1)
            _transform.position = style.sphere ? tip + Vector3.up * height : tip;
            _transform.rotation = Quaternion.identity;
            _transform.localScale = new Vector3(radius, height, radius) / rootScale;

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
            SetHit(style, hitPhase);
            SetInstability(style, dt, alpha);
            _renderer.SetPropertyBlock(_block);
            SetVisible(true);
        }

        /// <summary>
        /// Golpe en la cúpula (Rebote, 2026-10-21): una Zbam que se abre desde el punto del golpe, como el anillo de la peonza
        /// golpeada, con un destello y un pequeño hinchazo. point: dónde ha sido, en el mundo. Solo si la cúpula se ve.
        /// </summary>
        public void Pulse(Vector3 point)
        {
            if (!_visible || _transform == null) return;
            Vector3 scale = _transform.lossyScale;
            Vector3 local = point - _transform.position;
            local = new Vector3(local.x / Mathf.Max(0.001f, scale.x), local.y / Mathf.Max(0.001f, scale.y),
                local.z / Mathf.Max(0.001f, scale.z));
            // El choque es a la altura de la punta (abajo, casi tapado por el suelo y la peonza): la Zbam, más arriba, en el lado
            // del golpe
            float flat = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            local.y = Mathf.Max(local.y, 0.35f * flat);
            if (local.sqrMagnitude < 0.0001f) local = Vector3.right;
            _hitDir = local.normalized;
            _hitAngle = Random.value * Mathf.PI * 2f;
            _hitPending = true; // el Tick siguiente le pone su duración
        }

        /// <summary>La Zbam del golpe: tamaño y transparencia según lo que lleva (hitPhase; -1 = ninguna).</summary>
        private void SetHit(ChargeDomeStyle style, float hitPhase)
        {
            if (style.hitTexture == null || hitPhase < 0f)
            {
                _block.SetVector(HitId, Vector4.zero);
                return;
            }
            float scale = Mathf.Lerp(style.hitScale.x, style.hitScale.y, hitPhase);
            // Entra en la sexta parte de su vida y se apaga en curva (como el anillo de la peonza)
            float fade = Mathf.Clamp01(hitPhase * 6f) * (1f - hitPhase * hitPhase);
            _block.SetTexture(HitTexId, style.hitTexture);
            _block.SetVector(HitId, new Vector4(_hitDir.x, _hitDir.y, _hitDir.z, scale));
            _block.SetVector(HitRotId, new Vector4(Mathf.Cos(_hitAngle), Mathf.Sin(_hitAngle), 1.12f, 0f));
            Color color = style.hitColor;
            color.a *= fade;
            _block.SetColor(HitColorId, color);
            Color shadow = style.hitShadowColor;
            shadow.a *= fade;
            _block.SetColor(HitShadowColorId, shadow);
        }

        /// <summary>
        /// Máscara e inestabilidad (Defensa, 2026-10-24): huecos que se deslizan y laten a golpes irregulares, ondas en la
        /// superficie, parpadeos y bandas desplazadas al azar. alpha: la transparencia de este frame (el parpadeo la baja).
        /// </summary>
        private void SetInstability(ChargeDomeStyle style, float dt, float alpha)
        {
            // Cada efecto con su interruptor (2026-10-26): apagado cuenta como 0, sin tocar sus valores
            float maskAmount = style.maskEnabled ? style.maskAmount : 0f;
            float topErode = style.topErodeEnabled ? style.topErode : 0f;
            float holePulse = style.holePulseEnabled && style.maskEnabled ? style.holePulse : 0f;
            float wobble = style.wobbleEnabled ? style.wobble : 0f;
            float flickerRate = style.flickerEnabled ? style.flickerRate : 0f;
            float glitchRate = style.glitchEnabled ? style.glitchRate : 0f;
            if (!style.flickerEnabled) _flicker = 0f;
            if (!style.glitchEnabled) _glitch = 0f;

            bool masked = maskAmount > 0f || topErode > 0f || holePulse > 0f;
            if (masked)
            {
                _maskPhase.x = Mathf.Repeat(_maskPhase.x + style.maskScroll.x * dt, 1f);
                _maskPhase.y = Mathf.Repeat(_maskPhase.y + style.maskScroll.y * dt, 64f);
                _pulse = Mathf.Max(0f, _pulse - dt / style.holePulseDecay);
                if (holePulse > 0f && (_pulseWait -= dt) <= 0f)
                {
                    _pulse = 1f;
                    _pulseWait = Random.Range(style.holePulseInterval.x, style.holePulseInterval.y);
                }
                bool hasTexture = style.maskTexture != null;
                if (hasTexture) _block.SetTexture(MaskTexId, style.maskTexture);
                _block.SetVector(MaskScrollId, new Vector4(_maskPhase.x, _maskPhase.y, hasTexture ? 1f : 0f, 0f));
                _block.SetVector(MaskTopId, new Vector4(topErode, style.topErodeFrom, 0f, 0f));
                _block.SetColor(MaskEdgeColorId, style.maskEdgeColor);
            }
            _block.SetVector(MaskId, new Vector4(masked ? maskAmount + holePulse * _pulse : -1f,
                Mathf.Max(1f, Mathf.Round(style.maskTiling.x)), style.maskTiling.y, style.maskEdge));

            _wobblePhase = Mathf.Repeat(_wobblePhase + style.wobbleSpeed * dt, Mathf.PI * 200f);
            _block.SetVector(WobbleId, new Vector4(wobble, Mathf.Max(1f, Mathf.Round(style.wobbleWaves.x)), style.wobbleWaves.y,
                _wobblePhase));

            // Parpadeo y glitch: a ratos al azar (de media, Rate por segundo), cada uno su tiempo
            if (_flicker > 0f) _flicker -= dt;
            else if (flickerRate > 0f && Random.value < flickerRate * dt) _flicker = style.flickerTime;
            if (_flicker > 0f) _block.SetFloat(AlphaId, alpha * style.flickerAlpha);
            if (_glitch > 0f) _glitch -= dt;
            else if (glitchRate > 0f && Random.value < glitchRate * dt)
            {
                _glitch = style.glitchTime;
                _glitchSeed = Random.Range(0f, 100f);
            }
            _block.SetVector(GlitchId, new Vector4(style.glitchBands, style.glitchOffset, _glitch > 0f ? 1f : 0f, _glitchSeed));
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
            _hit = 0f;
            _hitPending = false;
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
            // El embudo (Tornado) sube más que la media esfera, y la burbuja (Rebote) baja de 0: límites amplios para que no se
            // recorte al salir de la pantalla
            _renderer.localBounds = new Bounds(new Vector3(0f, 1.5f, 0f), new Vector3(4f, 5.2f, 4f));
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

        /// <summary>
        /// Esfera entera de radio 1 con el centro en 0 (burbuja del Rebote), caras hacia fuera. UV: x = vuelta, y = altura (0
        /// abajo, 1 arriba): la franja de la base queda en el polo de abajo, tapada por el suelo.
        /// </summary>
        private static Mesh SphereMesh()
        {
            if (_meshSphere != null) return _meshSphere;
            const int rings = Rings * 2;
            int columns = Segments + 1;
            var vertices = new Vector3[columns * (rings + 1)];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var uvs2 = new Vector2[vertices.Length];
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float phi = (v - 0.5f) * Mathf.PI;
                for (int s = 0; s <= Segments; s++)
                {
                    float u = s / (float)Segments;
                    float theta = u * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(theta));
                    int i = r * columns + s;
                    vertices[i] = p;
                    normals[i] = p;
                    uvs[i] = new Vector2(u, v);
                }
            }

            var triangles = new int[Segments * rings * 6];
            int t = 0;
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < Segments; s++)
            {
                int a = r * columns + s, b = a + 1, c = a + columns, d = c + 1;
                triangles[t++] = a;
                triangles[t++] = c;
                triangles[t++] = b;
                triangles[t++] = c;
                triangles[t++] = d;
                triangles[t++] = b;
            }

            _meshSphere = new Mesh { name = "ChargeDomeSphere", vertices = vertices, normals = normals, uv = uvs, uv2 = uvs2, triangles = triangles };
            _meshSphere.RecalculateBounds();
            return _meshSphere;
        }
    }
}
