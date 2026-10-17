using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Rastro tipo cinta de una peonza (K7, GDD 7.6): un TrailRenderer pegado a la punta con el material de
    /// VfxLibrary.genericTrail (hoy liso; admite una tira de fotogramas de pixel art), teñido con el color del jugador.
    /// - Aparece al pasar de speedRange.x y se apaga un poco por debajo (para que no parpadee en el límite).
    /// - Cuanto más rápido va la peonza, más larga es la cinta, más ancha, más opaca y (con pixel art) más rápida la
    ///   animación.
    /// - Acelerón (burst = true): una segunda cinta igual, más ancha (burstWidthScale), que solo sale durante el acelerón
    ///   de ataques y dash y se suma a la normal.
    /// - Los valores se leen cada frame de la biblioteca: se pueden ajustar en Play desde el Inspector.
    ///
    /// Clase C# pura que posee FakeBladeController. El objeto y su material se crean una vez, la primera vez que se
    /// dibuja; después, cero GC.
    /// </summary>
    public sealed class BladeTrail
    {
        /// <summary>Qué rastro se ve (galería del Sandbox, para compararlos). En partida, los dos.</summary>
        public enum Display { RibbonAndParticles, RibbonOnly, ParticlesOnly, None }

        public static Display Mode = Display.RibbonAndParticles;
        public static bool ShowRibbon => Mode == Display.RibbonAndParticles || Mode == Display.RibbonOnly;
        public static bool ShowParticles => Mode == Display.RibbonAndParticles || Mode == Display.ParticlesOnly;

        /// <summary>Se apaga por debajo de esta fracción de la velocidad mínima.</summary>
        private const float StopSpeedFraction = 0.85f;

        private static readonly int FrameId = Shader.PropertyToID("_Frame");
        private static readonly int FramesId = Shader.PropertyToID("_Frames");
        private static readonly int FlipId = Shader.PropertyToID("_Flip");

        private readonly FakeBladeController _blade;
        private readonly bool _burst;
        private TrailRenderer _trail;
        private Transform _transform;
        private Material _material;
        private Material _sourceMaterial;
        private float _frame;
        private int _shownFrame = -1;
        private float _tailWidth = -1f;

        /// <summary>burst: la cinta del acelerón de ataques y dash (más ancha; solo durante el acelerón).</summary>
        public BladeTrail(FakeBladeController blade, bool burst = false)
        {
            _blade = blade;
            _burst = burst;
        }

        /// <summary>tipOffset: altura de la punta respecto al pivote de la peonza (la mide BladeParticles).</summary>
        public void Tick(float dt, float tipOffset)
        {
            VfxLibrary lib = VfxSystem.Library;
            RibbonTrailStyle style = lib != null ? lib.genericTrail : null;
            if (style == null || style.material == null)
            {
                if (_trail != null) _trail.emitting = false;
                return;
            }
            if (_trail == null) Build();
            if (_sourceMaterial != style.material) ApplyMaterial(style.material);

            Vector3 velocity = _blade.Velocity;
            float speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            float t = Mathf.InverseLerp(style.speedRange.x, style.speedRange.y, speed);

            bool emit = ShowRibbon && !_blade.IsDestroyed && (_burst
                ? _blade.IsBursting
                : speed >= style.speedRange.x * (_trail.emitting ? StopSpeedFraction : 1f));
            _trail.emitting = emit;

            // En metros del mundo (la raíz de la peonza puede estar escalada)
            Vector3 pos = _blade.Position;
            _transform.position = new Vector3(pos.x, pos.y + tipOffset + style.height, pos.z);
            _trail.alignment = style.flat ? LineAlignment.TransformZ : LineAlignment.View;
            _trail.time = Mathf.Lerp(style.time.x, style.time.y, t);
            _trail.widthMultiplier = Mathf.Lerp(style.width.x, style.width.y, t) * (_burst ? style.burstWidthScale : 1f);
            if (style.tailWidth != _tailWidth)
            {
                // Solo al cambiarlo (la curva nueva reserva memoria)
                _tailWidth = style.tailWidth;
                _trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, _tailWidth);
            }

            Color color = _blade.OwnerColor;
            if (style.lighten > 0f) color = Color.Lerp(color, Color.white, style.lighten);
            color.a = Mathf.Lerp(style.alpha.x, style.alpha.y, t) * (_burst ? style.burstAlphaScale : 1f);
            _trail.startColor = color;
            color.a *= style.tailAlpha;
            _trail.endColor = color;

            // Animación: más rápida cuanto más rápido va (también mientras se desvanece, a la última velocidad)
            int frames = Mathf.Max(1, style.frames);
            _frame = Mathf.Repeat(_frame + dt * Mathf.Lerp(style.fps.x, style.fps.y, t), frames);
            int frame = (int)_frame;
            if (frame != _shownFrame)
            {
                _shownFrame = frame;
                _material.SetFloat(FrameId, frame);
            }
            _material.SetFloat(FramesId, frames);
            _material.SetFloat(FlipId, style.flip ? 1f : 0f);
        }

        /// <summary>Borra la cinta (al reaparecer o teletransportarse, para que no cruce la arena).</summary>
        public void Clear()
        {
            if (_trail == null) return;
            _trail.Clear();
            _trail.emitting = false;
        }

        public void Stop()
        {
            if (_trail != null) _trail.emitting = false;
        }

        public void Dispose()
        {
            if (_material != null) Object.Destroy(_material);
            _material = null;
        }

        private void Build()
        {
            var go = new GameObject(_burst ? "BurstRibbonTrail" : "RibbonTrail");
            _transform = go.transform;
            _transform.SetParent(_blade.transform, false);
            // La raíz de la peonza no gira: con el eje Z hacia abajo, TransformZ deja la cinta tumbada en el suelo
            _transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ScreenPixelation.MoveToVfxLayer(go); // nítida sobre la escena pixelada (O2), tapada por lo que tenga delante

            _trail = go.AddComponent<TrailRenderer>();
            _trail.minVertexDistance = 0.08f;
            _trail.textureMode = LineTextureMode.Stretch; // el dibujo entero a lo largo de la cinta: se estira con ella
            _trail.numCapVertices = 0;
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            _trail.generateLightingData = false;
            _trail.emitting = false;
        }

        /// <summary>Copia propia del material (cada peonza anima su fotograma).</summary>
        private void ApplyMaterial(Material source)
        {
            if (_material != null) Object.Destroy(_material);
            _sourceMaterial = source;
            _material = new Material(source) { name = source.name + " (" + _blade.name + ")" };
            _trail.sharedMaterial = _material;
            _shownFrame = -1;
        }
    }
}
