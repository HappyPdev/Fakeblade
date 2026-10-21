using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Estela en espiral de un especial (Salto mientras vuela, K8, 2026-10-21; boceto del usuario): varias líneas que giran
    /// alrededor de la peonza, en el plano perpendicular a su marcha, y cada una deja su cinta (TrailRenderer). Juntas
    /// dibujan una hélice que sigue el arco del salto. Valores en VfxLibrary → Special Effects → Spiral Trail, leídos en vivo.
    ///
    /// Clase C# pura que posee FakeBladeController. Los objetos se crean una vez, la primera vez que hay estilo (en el
    /// primer frame de la peonza, no en combate); después, cero GC.
    /// </summary>
    public sealed class BladeSpiralTrail
    {
        private const int MaxStrands = 4;

        private readonly FakeBladeController _blade;
        private readonly TrailRenderer[] _trails = new TrailRenderer[MaxStrands];
        private readonly Transform[] _points = new Transform[MaxStrands];
        private bool _built;
        private bool _emitting;
        private float _angle;
        private Vector3 _axis = Vector3.forward;
        private float _tailWidth = -1f;

        public BladeSpiralTrail(FakeBladeController blade)
        {
            _blade = blade;
        }

        /// <summary>
        /// tipOffset: altura de la punta respecto al pivote (BladeParticles). style: los valores (null o sin material = no
        /// sale). active: si dejan rastro ahora. power: color del poder (Power Tint).
        /// </summary>
        public void Tick(float dt, float tipOffset, SpiralTrailStyle style, bool active, Color power)
        {
            if (style == null || style.material == null)
            {
                Stop();
                return;
            }
            if (!_built) Build();

            // Eje: hacia donde va la peonza (también hacia arriba o abajo); casi parada, el último
            Vector3 velocity = _blade.Velocity;
            if (velocity.sqrMagnitude > 1f) _axis = velocity.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, _axis);
            side = side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.right;
            Vector3 up = Vector3.Cross(_axis, side);

            _angle = Mathf.Repeat(_angle + style.turnsPerSecond * Mathf.PI * 2f * dt, Mathf.PI * 2f);
            Vector3 pos = _blade.Position;
            var center = new Vector3(pos.x, pos.y + tipOffset + style.height, pos.z);
            int strands = Mathf.Clamp(style.strands, 1, MaxStrands);
            bool start = active && !_emitting;
            if (style.tailWidth != _tailWidth)
            {
                // Solo al cambiarlo (la curva nueva reserva memoria)
                _tailWidth = style.tailWidth;
                AnimationCurve curve = AnimationCurve.Linear(0f, 1f, 1f, _tailWidth);
                for (int i = 0; i < MaxStrands; i++) _trails[i].widthCurve = curve;
            }

            for (int i = 0; i < MaxStrands; i++)
            {
                TrailRenderer trail = _trails[i];
                bool on = active && i < strands;
                if (i >= strands)
                {
                    trail.emitting = false;
                    continue;
                }
                float a = _angle + i * Mathf.PI * 2f / strands;
                _points[i].position = center + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * style.radius;
                // Al empezar, sin la cinta vieja (cruzaría la arena desde donde acabó la última)
                if (start) trail.Clear();
                trail.emitting = on;
                if (trail.sharedMaterial != style.material) trail.sharedMaterial = style.material;
                trail.time = style.time;
                trail.widthMultiplier = style.width;

                bool odd = (i & 1) == 1;
                Color color = Tinted(odd ? style.color2 : style.color, odd ? style.powerTint.y : style.powerTint.x, power);
                trail.startColor = color;
                color.a *= style.tailAlpha;
                trail.endColor = color;
            }
            _emitting = active;
        }

        /// <summary>Deja de dejar rastro (lo que ya hay se desvanece).</summary>
        public void Stop()
        {
            if (!_built) return;
            for (int i = 0; i < MaxStrands; i++) _trails[i].emitting = false;
            _emitting = false;
        }

        /// <summary>Borra las cintas (al reaparecer o al quedar fuera de combate).</summary>
        public void Hide()
        {
            if (!_built) return;
            for (int i = 0; i < MaxStrands; i++)
            {
                _trails[i].emitting = false;
                _trails[i].Clear();
            }
            _emitting = false;
        }

        private static Color Tinted(Color color, float tint, Color power)
        {
            if (tint <= 0f) return color;
            Color p = Color.Lerp(Color.white, power, tint);
            return new Color(color.r * p.r, color.g * p.g, color.b * p.b, color.a);
        }

        private void Build()
        {
            _built = true;
            var root = new GameObject("SpiralTrail");
            root.transform.SetParent(_blade.transform, false);
            for (int i = 0; i < MaxStrands; i++)
            {
                var go = new GameObject("Strand" + i);
                go.transform.SetParent(root.transform, false);
                ScreenPixelation.MoveToVfxLayer(go); // nítida sobre la escena pixelada (O2), como la cinta del rastro
                _points[i] = go.transform;
                TrailRenderer trail = go.AddComponent<TrailRenderer>();
                trail.alignment = LineAlignment.View;
                trail.minVertexDistance = 0.03f;
                trail.textureMode = LineTextureMode.Stretch;
                trail.numCapVertices = 0;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.generateLightingData = false;
                trail.emitting = false;
                _trails[i] = trail;
            }
        }
    }
}
