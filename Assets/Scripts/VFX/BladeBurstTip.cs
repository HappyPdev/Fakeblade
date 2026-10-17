using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Efecto en la punta mientras dura algo (K7 y K8, GDD 7.6): el acelerón de ataques y dash (VfxLibrary.burstTip) o
    /// un especial activo (VfxLibrary.specialEffects[...].tip). Con los valores de su BurstTipStyle:
    /// - Cuencos (ImpactPlane) abiertos hacia arriba, uno por entrada de la lista cups: cada uno con su giro inicial, su
    ///   velocidad de giro alrededor del eje vertical, su escala (ancho y alto) y su color (fijo o el del jugador). La
    ///   hoja de fotogramas (ImpactShotValo2 en el acelerón) se repite en bucle desde firstFrame, desfasada en cada
    ///   cuenco para que no parpadeen a la vez. Aparecen y se apagan con un fundido corto.
    /// - Sprite animado tumbado en el suelo debajo de la punta (ground), que gira; sale y se apaga con los cuencos.
    /// - Partículas en cono hacia arriba (ImpatcValo8 en el acelerón) del pool compartido de VfxSystem: se quedan en el
    ///   mundo y dejan rastro. Salen mientras está activo y, si se quiere, también al ir rápido (particleIdleRate).
    ///
    /// Clase C# pura que posee FakeBladeController (una para el acelerón y otra para el especial). Los cuencos se crean
    /// una vez, la primera vez que hacen falta, y se apagan (renderer.enabled = false) cuando no se usan: cero GC. Los
    /// valores se leen en vivo de la biblioteca.
    /// </summary>
    public sealed class BladeBurstTip
    {
        private const int MaxCups = 8;
        private const int MaxParticlesPerFrame = 6;
        /// <summary>Ancho del modelo ImpactPlane a escala 1 (m).</summary>
        private const float MeshWidth = 1.42f;

        private static readonly int FrameId = Shader.PropertyToID("_Frame");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int TilesXId = Shader.PropertyToID("_TilesX");
        private static readonly int TilesYId = Shader.PropertyToID("_TilesY");

        /// <summary>Cuadrado de 1 m tumbado en el suelo (XZ) para el sprite del suelo; lo comparten todas las peonzas.</summary>
        private static Mesh _groundMesh;

        private readonly FakeBladeController _blade;
        private readonly string _name;
        private readonly MeshRenderer[] _renderers = new MeshRenderer[MaxCups];
        private readonly MeshFilter[] _filters = new MeshFilter[MaxCups];
        private MeshRenderer _ground;
        private MaterialPropertyBlock _block;
        private Transform _root;
        private bool _built;
        private bool _visible;
        private float _intensity;
        private float _time;
        private float _particleAcc;

        /// <summary>name: nombre del objeto que cuelga de la peonza (para distinguirlos en la jerarquía).</summary>
        public BladeBurstTip(FakeBladeController blade, string name = "BurstTip")
        {
            _blade = blade;
            _name = name;
        }

        /// <summary>
        /// tipOffset: altura de la punta respecto al pivote de la peonza (la mide BladeParticles). style: los valores
        /// (null = no sale nada). active: si está en marcha (el acelerón o el especial).
        /// </summary>
        public void Tick(float dt, float tipOffset, BurstTipStyle style, bool active)
        {
            if (style == null)
            {
                Hide();
                _particleAcc = 0f;
                return;
            }

            // En metros del mundo (la raíz de la peonza puede estar escalada)
            Vector3 pos = _blade.Position;
            var tip = new Vector3(pos.x, pos.y + tipOffset, pos.z);
            UpdateParticles(style, tip, active, dt);

            bool cups = style.mesh != null && style.material != null && style.cups.Count > 0;
            TipGroundSprite groundStyle = style.ground;
            bool ground = groundStyle != null && groundStyle.material != null;
            if (!cups && !ground)
            {
                Hide();
                return;
            }
            float fade = active ? style.fade.x : style.fade.y;
            _intensity = fade <= 0f ? (active ? 1f : 0f)
                : Mathf.MoveTowards(_intensity, active ? 1f : 0f, dt / fade);
            if (_intensity <= 0f)
            {
                Hide();
                _time = 0f;
                return;
            }

            if (!_built) Build();
            _time += dt;

            _root.position = new Vector3(tip.x, tip.y + style.height, tip.z);
            float rootScale = Mathf.Max(0.001f, _blade.transform.lossyScale.x);
            float scale = style.size / MeshWidth / rootScale;

            int count = cups ? Mathf.Min(style.cups.Count, MaxCups) : 0;
            // Sin pasar de las casillas de la hoja (las de más saldrían vacías o repetidas)
            int tiles = Mathf.Max(1, style.tilesX * style.tilesY);
            int first = Mathf.Clamp(style.firstFrame, 0, tiles - 1);
            int frames = Mathf.Clamp(style.frames, 1, tiles - first);
            float cycle = Mathf.Max(0.05f, style.cycle);
            Color owner = _blade.OwnerColor;
            _block.SetFloat(TilesXId, style.tilesX);
            _block.SetFloat(TilesYId, style.tilesY);
            for (int i = 0; i < MaxCups; i++)
            {
                MeshRenderer renderer = _renderers[i];
                BurstTipCup cup = i < count ? style.cups[i] : null;
                bool on = cup != null;
                if (renderer.enabled != on) renderer.enabled = on;
                if (!on) continue;

                if (_filters[i].sharedMesh != style.mesh) _filters[i].sharedMesh = style.mesh;
                if (renderer.sharedMaterial != style.material) renderer.sharedMaterial = style.material;
                Transform t = renderer.transform;
                t.localRotation = Quaternion.Euler(0f, cup.angle + cup.spinSpeed * _time, 0f);
                t.localScale = new Vector3(scale * style.scale.x * cup.scale.x, scale * style.scale.y * cup.scale.y,
                    scale * style.scale.x * cup.scale.x);

                Color color = cup.playerColor ? new Color(owner.r, owner.g, owner.b, cup.color.a) : cup.color;
                if (style.lighten > 0f)
                {
                    Color light = Color.Lerp(color, Color.white, style.lighten);
                    color = new Color(light.r, light.g, light.b, color.a);
                }
                color.a *= style.alpha * _intensity;
                _block.SetColor(ColorId, color);
                // Desfase: cada cuenco empieza la animación en otro punto del bucle
                float phase = Mathf.Repeat(_time / cycle + (float)i / count, 1f);
                _block.SetFloat(FrameId, first + Mathf.Min(frames - 1, (int)(phase * frames)));
                renderer.SetPropertyBlock(_block);
            }

            UpdateGround(ground ? groundStyle : null, style.height, rootScale, owner);
            _visible = true;
        }

        /// <summary>Sprite animado tumbado en el suelo, debajo de la punta; gira y se repite en bucle.</summary>
        private void UpdateGround(TipGroundSprite style, float tipHeight, float rootScale, Color owner)
        {
            bool on = style != null;
            if (_ground.enabled != on) _ground.enabled = on;
            if (!on) return;

            if (_ground.sharedMaterial != style.material) _ground.sharedMaterial = style.material;
            Transform t = _ground.transform;
            // El sprite va a su altura sobre la punta, no a la de los cuencos
            t.localPosition = new Vector3(0f, (style.height - tipHeight) / rootScale, 0f);
            t.localRotation = Quaternion.Euler(0f, style.spinSpeed * _time, 0f);
            float size = style.size / rootScale;
            t.localScale = new Vector3(size, 1f, size);

            int tiles = Mathf.Max(1, style.tilesX * style.tilesY);
            int first = Mathf.Clamp(style.firstFrame, 0, tiles - 1);
            int frames = Mathf.Clamp(style.frames, 1, tiles - first);
            float phase = Mathf.Repeat(_time / Mathf.Max(0.05f, style.cycle), 1f);
            Color color = style.playerColor ? new Color(owner.r, owner.g, owner.b, style.color.a) : style.color;
            color.a *= style.alpha * _intensity;
            _block.SetFloat(TilesXId, style.tilesX);
            _block.SetFloat(TilesYId, style.tilesY);
            _block.SetFloat(FrameId, first + Mathf.Min(frames - 1, (int)(phase * frames)));
            _block.SetColor(ColorId, color);
            _ground.SetPropertyBlock(_block);
        }

        /// <summary>
        /// Partículas en cono hacia arriba desde la punta: salen del pool compartido de VfxSystem, en el mundo, así que se
        /// quedan atrás y dejan rastro. Escaladas con Opciones → Partículas.
        /// </summary>
        private void UpdateParticles(BurstTipStyle style, Vector3 tip, bool active, float dt)
        {
            Vector3 velocity = _blade.Velocity;
            float rate = active ? style.particleRate : 0f;
            if (!active && style.particleIdleRate > 0f && !_blade.IsDestroyed &&
                velocity.x * velocity.x + velocity.z * velocity.z >= style.particleIdleMinSpeed * style.particleIdleMinSpeed)
                rate = style.particleIdleRate;
            if (rate <= 0f || string.IsNullOrEmpty(style.particleSprite) || !VfxSystem.IsOnScreen(tip))
            {
                _particleAcc = 0f;
                return;
            }

            _particleAcc += dt * rate * SettingsService.ParticleMultiplier;
            int count = Mathf.Min(MaxParticlesPerFrame, (int)_particleAcc);
            if (count <= 0) return;
            _particleAcc -= count;

            Color color = style.particlePlayerColor ? _blade.OwnerColor : style.particleColor;
            if (style.particleLighten > 0f) color = Color.Lerp(color, Color.white, style.particleLighten);
            color.a = style.particleAlpha;
            Vector3 inherited = new Vector3(velocity.x, 0f, velocity.z) * style.particleInheritVelocity;
            for (int i = 0; i < count; i++)
            {
                // Dirección al azar dentro del cono (desde la vertical); salen repartidas alrededor de la punta
                float tilt = Random.Range(0f, style.particleConeAngle) * Mathf.Deg2Rad;
                float turn = Random.value * Mathf.PI * 2f;
                float sin = Mathf.Sin(tilt);
                var direction = new Vector3(sin * Mathf.Cos(turn), Mathf.Cos(tilt), sin * Mathf.Sin(turn));
                var around = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
                Vector3 position = tip + around * style.particleSpawnRadius + Vector3.up * style.particleHeight;
                VfxSystem.EmitSprite(style.particleSprite, position,
                    direction * Random.Range(style.particleSpeed.x, style.particleSpeed.y) + inherited, color,
                    Random.Range(style.particleSize.x, style.particleSize.y),
                    Random.Range(style.particleRotation.x, style.particleRotation.y),
                    Random.Range(style.particleLifetime.x, style.particleLifetime.y));
            }
        }

        public void Hide()
        {
            _intensity = 0f;
            if (!_visible) return;
            _visible = false;
            for (int i = 0; i < MaxCups; i++)
                if (_renderers[i] != null) _renderers[i].enabled = false;
            if (_ground != null) _ground.enabled = false;
        }

        private void Build()
        {
            _built = true;
            _block = new MaterialPropertyBlock();
            var root = new GameObject(_name);
            _root = root.transform;
            _root.SetParent(_blade.transform, false);
            for (int i = 0; i < MaxCups; i++)
            {
                var cup = new GameObject("Cup" + i);
                cup.transform.SetParent(_root, false);
                _filters[i] = cup.AddComponent<MeshFilter>();
                _renderers[i] = CreateRenderer(cup);
            }

            var ground = new GameObject("Ground");
            ground.transform.SetParent(_root, false);
            ground.AddComponent<MeshFilter>().sharedMesh = GroundMesh();
            _ground = CreateRenderer(ground);
            ScreenPixelation.MoveToVfxLayer(root); // nítido sobre la escena pixelada (O2)
        }

        private static MeshRenderer CreateRenderer(GameObject go)
        {
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.enabled = false;
            return renderer;
        }

        /// <summary>Cuadrado de 1 × 1 m en el plano XZ, mirando hacia arriba, con la UV entera.</summary>
        private static Mesh GroundMesh()
        {
            if (_groundMesh != null) return _groundMesh;
            _groundMesh = new Mesh { name = "TipGroundQuad" };
            _groundMesh.SetVertices(new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f)
            });
            _groundMesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            _groundMesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            _groundMesh.RecalculateNormals();
            _groundMesh.RecalculateBounds();
            return _groundMesh;
        }
    }
}
