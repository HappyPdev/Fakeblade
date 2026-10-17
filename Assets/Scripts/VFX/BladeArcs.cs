using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Arcos eléctricos de un especial (Rayos, K8, 2026-10-17; GDD 7.6), como una lámpara de plasma de Tesla y con el estilo
    /// del boceto del usuario (zigzag de picos marcados, tramos desiguales, ramas, chispas de cuatro puntas y trazo de cómic:
    /// fino en cada quiebro y en los extremos, más grueso y desigual en el medio de cada tramo), con los valores de su
    /// ElectricArcStyle (VfxLibrary.specialEffects[...].arcs):
    /// - Alrededor: cada rayo cae en un punto al azar alrededor de la peonza, que se queda fijo mientras el otro extremo sigue
    ///   al núcleo (se estira), con el medio un poco arrastrado por la velocidad; al acabar su vida se apaga y cae otro.
    /// - Rastro: al moverse rápido, rayos a ras de suelo entre los puntos por donde pasa, que se apagan enseguida.
    /// - Choque: rayos hasta la otra peonza durante un instante, que siguen a las dos (PlayClash, desde VfxSystem).
    /// Cada rayo se vuelve a quebrar al azar cada pocas centésimas (y a veces se apaga un momento: parpadea), con un grosor
    /// al azar, a veces con una rama más corta y una chispa cerca. Cada línea son dos LineRenderer: un halo ancho y
    /// translúcido y un núcleo fino y brillante, con la misma curva de grosor.
    ///
    /// Clase C# pura que posee FakeBladeController. Los LineRenderer se crean una vez, en el primer frame de una peonza
    /// cuyo especial tiene arcos, y después solo se mueven sus puntos y su curva de grosor (al volver a quebrarse): cero GC
    /// en combate. Se lee en vivo de la biblioteca.
    /// </summary>
    public sealed class BladeArcs
    {
        private const int MaxAround = 12;
        private const int MaxTrail = 8;
        private const int MaxClash = 6;
        private const int MaxArcs = MaxAround + MaxTrail + MaxClash;
        private const int MaxSegments = 16;
        /// <summary>Puntos de una línea: cada tramo del zigzag lleva un punto en medio (para el grosor de cómic).</summary>
        private const int MaxPoints = MaxSegments * 2 + 1;
        /// <summary>Largo (m) con el que el zigzag tiene su tamaño entero: los rayos más cortos se quiebran menos.</summary>
        private const float JitterLength = 1f;

        /// <summary>Una línea quebrada: sus dos LineRenderer, lo que se aparta cada quiebro y su grosor a lo largo.</summary>
        private sealed class Bolt
        {
            public LineRenderer Glow, Core;
            /// <summary>Por quiebro: x = a un lado, y = arriba o abajo, z = adelante o atrás (tramos desiguales).</summary>
            public readonly Vector3[] Offsets = new Vector3[MaxSegments + 1];
            /// <summary>Grosor de cada punto (quiebros y medios de tramo), en parte del máximo.</summary>
            public readonly float[] Widths = new float[MaxPoints];
            public readonly AnimationCurve Curve = new AnimationCurve();
            public Keyframe[] Keys;
            public Vector3[] World;
            public bool WidthsDirty;
            public bool Shown;
        }

        private sealed class Arc
        {
            public readonly Bolt Main = new Bolt();
            public readonly Bolt Branch = new Bolt();
            public float Refresh;
            public bool Lit;
            public bool Redrawn;
            /// <summary>Grosor de este rayo, en parte del máximo (al azar en cada refresco).</summary>
            public float Width = 1f;
            // Rama: desde qué quiebro, hacia qué lado, cuánto sube y su largo
            public bool HasBranch;
            public int BranchPoint;
            public float BranchSide, BranchUp, BranchLength;
            // Vida (alrededor, rastro y choque) y extremos
            public float Age, Life;
            public Vector3 From, To;
            public FakeBladeController Target;
        }

        private readonly FakeBladeController _blade;
        private readonly Arc[] _arcs = new Arc[MaxArcs];
        private bool _built;
        private Material _material;
        private int _nextTrail, _nextClash;
        private Vector3 _lastTrailPoint;
        private Vector3 _prevGround;
        private bool _hasTrailPoint;
        private float _trailDistance;

        public BladeArcs(FakeBladeController blade)
        {
            _blade = blade;
            for (int i = 0; i < MaxArcs; i++) _arcs[i] = new Arc();
        }

        /// <summary>
        /// Choque de una peonza con el especial encendido: si tiene arcos, saltan hasta la otra (VfxSystem, en cualquier
        /// choque, también los pequeños). evenIfEnded: también si el poder se acaba de gastar en ese golpe (el golpe fuerte
        /// de Rayos, que lo gasta antes de avisar del choque).
        /// </summary>
        public static void PlayClash(FakeBladeController blade, FakeBladeController other, bool evenIfEnded = false)
        {
            if (blade == null || other == null || blade.Arcs == null) return;
            SpecialAbilitySystem special = blade.Special;
            if (special == null || blade.IsDestroyed || (!evenIfEnded && (!special.IsActive || special.IsWindingUp))) return;
            VfxLibrary lib = VfxSystem.Library;
            SpecialVfxStyle effects = lib != null ? lib.GetSpecialEffects(special.Type) : null;
            ElectricArcStyle style = effects != null ? effects.arcs : null;
            if (style == null || !style.enabled || style.material == null) return;
            blade.Arcs.StartClash(other, style);
        }

        private void StartClash(FakeBladeController other, ElectricArcStyle style)
        {
            if (!_built) return;
            int count = Mathf.Min(style.clashCount, MaxClash);
            for (int i = 0; i < count; i++)
            {
                Arc arc = _arcs[MaxAround + MaxTrail + _nextClash];
                _nextClash = (_nextClash + 1) % MaxClash;
                arc.Target = other;
                arc.Age = 0f;
                arc.Life = style.clashDuration;
                arc.Refresh = 0f;
            }
        }

        /// <summary>
        /// tipOffset: altura de la punta respecto al pivote (BladeParticles). style: los arcos del especial (o null).
        /// active: el poder está encendido (no en la carga del principio). windingUp: en la carga del principio (sin rastro
        /// ni choques; alrededor, windupCount rayos).
        /// </summary>
        public void Tick(float dt, float tipOffset, ElectricArcStyle style, bool active, bool windingUp = false)
        {
            if (style == null || !style.enabled || style.material == null)
            {
                Hide();
                return;
            }
            if (!_built) Build(style);
            if (_material != style.material) SetMaterial(style.material);

            Vector3 pos = _blade.Position;
            var tip = new Vector3(pos.x, pos.y + tipOffset, pos.z);
            Vector3 core = pos + Vector3.up * style.coreHeight;
            bool visible = VfxSystem.IsOnScreen(tip);
            int segments = Mathf.Clamp(style.segments, 2, MaxSegments);
            Vector3 velocity = _blade.Velocity;
            var drag = new Vector3(-velocity.x, 0f, -velocity.z) * style.drag;

            // Alrededor: cada rayo cae en un punto fijo, el otro extremo sigue al núcleo; al acabar su vida, otro
            int around = !visible ? 0 : Mathf.Min(active ? style.aroundCount : windingUp ? style.windupCount : 0, MaxAround);
            float maxStretch = style.maxStretch * style.maxStretch;
            for (int i = 0; i < MaxAround; i++)
            {
                Arc arc = _arcs[i];
                if (i >= around)
                {
                    arc.Life = 0f;
                    Show(arc, false);
                    continue;
                }
                arc.Age += dt;
                if (arc.Life <= 0f || arc.Age >= arc.Life || (arc.To - core).sqrMagnitude > maxStretch)
                {
                    float angle = Random.value * Mathf.PI * 2f;
                    float radius = Random.Range(style.aroundRadius.x, style.aroundRadius.y);
                    arc.To = new Vector3(tip.x + Mathf.Cos(angle) * radius, tip.y + Random.Range(style.aroundHeight.x, style.aroundHeight.y),
                        tip.z + Mathf.Sin(angle) * radius);
                    arc.Life = Random.Range(style.strikeLife.x, Mathf.Max(style.strikeLife.x, style.strikeLife.y));
                    arc.Age = 0f;
                    arc.Refresh = 0f;
                }
                Rejitter(arc, style, dt, segments, flat: false);
                // Sale entero y se apaga en el último 40% de su vida
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, arc.Age / arc.Life));
                Draw(arc, style, core, arc.To, style.arch, drag, segments, fade);
            }

            // Rastro: un rayo nuevo cada pocos metros recorridos deprisa, entre el punto anterior y el de ahora
            var ground = new Vector3(tip.x, tip.y + style.trailHeight, tip.z);
            bool fast = active && velocity.x * velocity.x + velocity.z * velocity.z >= style.trailMinSpeed * style.trailMinSpeed &&
                        !_blade.IsLeaping;
            if (!fast)
            {
                _hasTrailPoint = false;
                _trailDistance = 0f;
            }
            else if (!_hasTrailPoint)
            {
                _hasTrailPoint = true;
                _lastTrailPoint = _prevGround = ground;
            }
            else
            {
                // Lo recorrido en este frame (no la distancia al último punto, que crece sola)
                _trailDistance += Vector3.Distance(ground, _prevGround);
                _prevGround = ground;
                if (_trailDistance >= style.trailSpacing && style.trailCount > 0)
                {
                    Arc arc = _arcs[MaxAround + _nextTrail];
                    _nextTrail = (_nextTrail + 1) % Mathf.Min(style.trailCount, MaxTrail);
                    arc.From = _lastTrailPoint;
                    arc.To = ground;
                    arc.Age = 0f;
                    arc.Life = style.trailLife;
                    arc.Refresh = 0f;
                    _trailDistance = 0f;
                    _lastTrailPoint = ground;
                }
            }
            for (int i = 0; i < MaxTrail; i++)
            {
                Arc arc = _arcs[MaxAround + i];
                if (!Alive(arc, dt) || !visible)
                {
                    Show(arc, false);
                    continue;
                }
                Rejitter(arc, style, dt, segments, flat: true);
                Draw(arc, style, arc.From, arc.To, 0f, Vector3.zero, segments, 1f - arc.Age / arc.Life);
            }

            // Choque: hasta la otra peonza, que sigue mientras dura
            for (int i = 0; i < MaxClash; i++)
            {
                Arc arc = _arcs[MaxAround + MaxTrail + i];
                if (!Alive(arc, dt) || arc.Target == null || arc.Target.IsDestroyed || !visible)
                {
                    arc.Target = null;
                    Show(arc, false);
                    continue;
                }
                Rejitter(arc, style, dt, segments, flat: false);
                Vector3 to = arc.Target.Position + Vector3.up * style.coreHeight * 0.5f;
                Draw(arc, style, core, to, style.arch * 0.5f, Vector3.zero, segments, 1f - 0.5f * arc.Age / arc.Life);
            }
        }

        /// <summary>Avanza la vida de un rayo del rastro o del choque. false = apagado.</summary>
        private static bool Alive(Arc arc, float dt)
        {
            if (arc.Life <= 0f) return false;
            arc.Age += dt;
            if (arc.Age < arc.Life) return true;
            arc.Life = 0f;
            return false;
        }

        /// <summary>
        /// Cuenta hasta el próximo refresco y, al llegar, vuelve a sortear el zigzag (cada pico, al lado contrario que el
        /// anterior casi siempre, con tramos de largo desigual), el grosor a lo largo, el grosor del rayo, si se ve
        /// (parpadeo) y la rama. flat: a ras de suelo (casi sin picos hacia arriba).
        /// </summary>
        private static void Rejitter(Arc arc, ElectricArcStyle style, float dt, int segments, bool flat)
        {
            arc.Refresh -= dt;
            if (arc.Refresh > 0f && arc.Main.Shown) return;
            arc.Refresh = Random.Range(style.refresh.x, Mathf.Max(style.refresh.x, style.refresh.y));
            arc.Lit = Random.value >= style.flicker;
            arc.Width = Random.Range(Mathf.Clamp01(style.minWidth), 1f);
            arc.Redrawn = true;
            Zigzag(arc.Main, segments, style, flat);

            arc.HasBranch = !flat && Random.value < style.branchChance;
            if (!arc.HasBranch) return;
            arc.BranchPoint = Random.Range(Mathf.Max(1, segments * 3 / 10), Mathf.Max(2, segments * 3 / 4));
            arc.BranchSide = Random.value < 0.5f ? -1f : 1f;
            arc.BranchUp = Random.Range(-0.2f, 0.5f);
            arc.BranchLength = Random.Range(style.branchLength.x, Mathf.Max(style.branchLength.x, style.branchLength.y));
            Zigzag(arc.Branch, BranchSegments(segments), style, flat);
        }

        private static int BranchSegments(int segments) => Mathf.Max(3, segments / 2);

        /// <summary>
        /// El zigzag de una línea y su grosor a lo largo, como un trazo de cómic: fino en cada quiebro, más grueso y
        /// desigual en el medio de cada tramo, y afilado hacia los dos extremos de la línea.
        /// </summary>
        private static void Zigzag(Bolt bolt, int segments, ElectricArcStyle style, bool flat)
        {
            Vector3[] offsets = bolt.Offsets;
            float side = Random.value < 0.5f ? -1f : 1f;
            offsets[0] = offsets[segments] = Vector3.zero;
            for (int i = 1; i < segments; i++)
            {
                side = Random.value < style.zigzag ? -side : (Random.value < 0.5f ? -1f : 1f);
                // Picos muy desiguales (del 15% al 100%): si no, el zigzag sale regular, como una sierra
                offsets[i] = new Vector3(side * Random.Range(0.15f, 1f), Random.Range(-0.5f, 0.5f) * (flat ? 0.15f : 1f),
                    Random.Range(-style.unevenSegments, style.unevenSegments));
            }

            int count = segments * 2 + 1;
            float midMin = Mathf.Min(style.midWidth.x, style.midWidth.y), midMax = Mathf.Max(style.midWidth.x, style.midWidth.y);
            for (int j = 0; j < count; j++)
            {
                float t = (float)j / (count - 1);
                // Max(0): en t = 1 el seno sale un poco negativo y la raíz daría NaN (la curva perdía su última clave: punta plana)
                float envelope = Mathf.Lerp(style.endWidth, 1f, Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI))));
                float width = j % 2 == 0 ? style.kinkWidth * Random.Range(0.7f, 1.3f) : Random.Range(midMin, midMax);
                // Sin tope: más de 1 = más grueso que el ancho base
                bolt.Widths[j] = Mathf.Max(0f, width * envelope);
            }
            bolt.WidthsDirty = true;
        }

        /// <summary>
        /// Pone los puntos del rayo de from a to (el camino recto, curvado hacia arriba con arch y arrastrado con drag, más
        /// en el medio, con el zigzag a los lados) y los de su rama, con su color y la transparencia fade; y, si se acaba
        /// de volver a quebrar, a veces una chispa cerca.
        /// </summary>
        private void Draw(Arc arc, ElectricArcStyle style, Vector3 from, Vector3 to, float arch, Vector3 drag, int segments, float fade)
        {
            bool on = arc.Lit && fade > 0f;
            Show(arc.Main, on);
            if (!on)
            {
                Show(arc.Branch, false);
                return;
            }
            int count = segments * 2 + 1;
            Ensure(arc.Main, count); // solo crece al cambiar los tramos en el Inspector
            float jitter = style.jitter * Mathf.Clamp(Vector3.Distance(from, to) / JitterLength, 0.3f, 1.5f);
            Points(arc.Main.Offsets, arc.Main.World, segments, from, to, arch, drag, jitter);
            Set(arc.Main, count, style, arc.Width, fade);

            // Rama: desde un quiebro del rayo, hacia un lado y algo hacia delante, más corta y más fina
            bool branch = arc.HasBranch && arc.BranchPoint < segments;
            Show(arc.Branch, branch);
            if (branch)
            {
                int branchSegments = BranchSegments(segments);
                int branchCount = branchSegments * 2 + 1;
                Ensure(arc.Branch, branchCount);
                Vector3 start = arc.Main.World[arc.BranchPoint * 2];
                Vector3 axis = to - from;
                Vector3 dir = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(dir, Vector3.up);
                if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
                side.Normalize();
                Vector3 end = start + (side * arc.BranchSide * 0.8f + dir * 0.6f + Vector3.up * arc.BranchUp).normalized * arc.BranchLength;
                Points(arc.Branch.Offsets, arc.Branch.World, branchSegments, start, end, 0f, Vector3.zero, jitter * 0.6f);
                Set(arc.Branch, branchCount, style, arc.Width * 0.6f, fade);
            }

            // Chispa de cuatro puntas cerca del rayo (al volver a quebrarse, a veces)
            if (arc.Redrawn)
            {
                arc.Redrawn = false;
                if (Random.value < style.sparkChance)
                {
                    Vector3 p = arc.Main.World[Random.Range(1, count)] + Random.insideUnitSphere * 0.15f;
                    Color c = Random.value < 0.5f ? style.coreColor : style.color;
                    VfxSystem.EmitParticle(VfxType.Sparkle, p, Vector3.zero, c,
                        Random.Range(style.sparkSize.x, Mathf.Max(style.sparkSize.x, style.sparkSize.y)), Random.Range(0.12f, 0.25f));
                }
            }
        }

        /// <summary>Los arrays de puntos y de claves de grosor, del tamaño justo (el LineRenderer usa todos).</summary>
        private static void Ensure(Bolt bolt, int count)
        {
            if (bolt.World == null || bolt.World.Length != count) bolt.World = new Vector3[count];
            if (bolt.Keys == null || bolt.Keys.Length != count)
            {
                bolt.Keys = new Keyframe[count];
                bolt.WidthsDirty = true;
            }
        }

        /// <summary>
        /// Los puntos de una línea quebrada de from a to con su zigzag (offsets), su curva y su arrastre: los quiebros en
        /// los puntos pares y, en los impares, el medio de cada tramo (que sigue siendo recto).
        /// </summary>
        private static void Points(Vector3[] offsets, Vector3[] world, int segments, Vector3 from, Vector3 to, float arch, Vector3 drag,
            float jitter)
        {
            Vector3 axis = to - from;
            Vector3 dir = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
            side.Normalize();
            Vector3 lift = Vector3.Cross(side, dir);
            float step = 1f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float t = i * step;
                Vector3 o = offsets[i];
                float along = Mathf.Clamp01(t + o.z * step);
                float bulge = 4f * t * (1f - t);
                // Los picos crecen deprisa desde los extremos (sqrt), la curva y el arrastre, más en el medio
                float spike = Mathf.Sqrt(bulge) * jitter;
                world[i * 2] = from + axis * along + side * (o.x * spike) + lift * (o.y * spike) + Vector3.up * (arch * bulge) + drag * bulge;
            }
            for (int i = 0; i < segments; i++)
                world[i * 2 + 1] = (world[i * 2] + world[i * 2 + 2]) * 0.5f;
        }

        /// <summary>Puntos, color y grosor de la línea; la curva de grosor solo cuando se ha vuelto a quebrar.</summary>
        private static void Set(Bolt bolt, int count, ElectricArcStyle style, float width, float fade)
        {
            if (bolt.WidthsDirty)
            {
                bolt.WidthsDirty = false;
                float last = count - 1;
                for (int j = 0; j < count; j++) bolt.Keys[j] = new Keyframe(j / last, bolt.Widths[j], 0f, 0f);
                bolt.Curve.keys = bolt.Keys;
                bolt.Glow.widthCurve = bolt.Curve;
                bolt.Core.widthCurve = bolt.Curve;
            }
            Line(bolt.Glow, bolt.World, count, style.glowWidth * width,
                new Color(style.color.r, style.color.g, style.color.b, style.glowAlpha * fade));
            Line(bolt.Core, bolt.World, count, style.coreWidth * width,
                new Color(style.coreColor.r, style.coreColor.g, style.coreColor.b, fade));
        }

        private static void Line(LineRenderer line, Vector3[] points, int count, float width, Color color)
        {
            if (line.positionCount != count) line.positionCount = count;
            line.SetPositions(points);
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
        }

        private static void Show(Arc arc, bool on)
        {
            Show(arc.Main, on);
            if (!on) Show(arc.Branch, false);
        }

        private static void Show(Bolt bolt, bool on)
        {
            if (bolt.Shown == on || bolt.Glow == null) return;
            bolt.Shown = on;
            bolt.Glow.enabled = on;
            bolt.Core.enabled = on;
        }

        public void Hide()
        {
            _hasTrailPoint = false;
            _trailDistance = 0f;
            for (int i = 0; i < MaxArcs; i++)
            {
                _arcs[i].Life = 0f;
                _arcs[i].Target = null;
                Show(_arcs[i], false);
            }
        }

        private void Build(ElectricArcStyle style)
        {
            _built = true;
            int segments = Mathf.Clamp(style.segments, 2, MaxSegments);
            var root = new GameObject("Arcs");
            root.transform.SetParent(_blade.transform, false);
            for (int i = 0; i < MaxArcs; i++)
            {
                Arc arc = _arcs[i];
                arc.Main.Glow = CreateLine(root.transform, "ArcGlow" + i, 0);
                arc.Main.Core = CreateLine(root.transform, "ArcCore" + i, 1);
                Ensure(arc.Main, segments * 2 + 1);
                arc.Branch.Glow = CreateLine(root.transform, "BranchGlow" + i, 0);
                arc.Branch.Core = CreateLine(root.transform, "BranchCore" + i, 1);
                Ensure(arc.Branch, BranchSegments(segments) * 2 + 1);
            }
            ScreenPixelation.MoveToVfxLayer(root); // nítido sobre la escena pixelada (O2)
        }

        private void SetMaterial(Material material)
        {
            _material = material;
            for (int i = 0; i < MaxArcs; i++)
            {
                Arc arc = _arcs[i];
                arc.Main.Glow.sharedMaterial = arc.Main.Core.sharedMaterial = material;
                arc.Branch.Glow.sharedMaterial = arc.Branch.Core.sharedMaterial = material;
            }
        }

        private static LineRenderer CreateLine(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.positionCount = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            line.sortingOrder = order;
            line.enabled = false;
            return line;
        }
    }
}
