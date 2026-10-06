using FakeBlade.Core;
using UnityEngine;

namespace FakeBlade.UI
{
    /// <summary>
    /// Vista previa 3D de una peonza para la selección (GDD 9.2.1).
    /// Copia solo la parte visual del prefab (sin scripts ni físicas), la hace girar y la
    /// renderiza a una RenderTexture de baja resolución con filtrado Point → aspecto pixelado.
    /// Cada slot vive lejos de todo lo demás para que su cámara solo vea su peonza.
    /// </summary>
    public class BladePreviewStage : MonoBehaviour
    {
        private Camera _camera;
        private RenderTexture _texture;
        private Transform _tilt;
        private Transform _spin;
        private Transform _model;
        private BladeModelSettings _modelSettings = BladeModelSettings.Default;
        private BladePaint _paint;
        private BladeColorScheme _scheme;
        private bool _hasScheme;
        private Color _glowColor;
        private float _glowIntensity;
        private FakeBladeComponentData _tip, _body, _blade, _core;
        private bool _hasParts;
        /// <summary>Altura de la cámara respecto a su distancia (0 = de frente; antes 0,55).</summary>
        private const float CameraHeight = 0.32f;
        private float _angle;
        private float _time;
        private float _spinSpeed = 540f;

        public RenderTexture Texture => _texture;

        public static BladePreviewStage Create(int slot, GameObject playerPrefab, Color background, int resolution = 128)
        {
            var root = new GameObject($"PreviewStage_{slot}");
            root.transform.position = new Vector3(1000f + slot * 50f, -1000f, 0f);
            var stage = root.AddComponent<BladePreviewStage>();
            stage.Build(playerPrefab, background, resolution);
            return stage;
        }

        private void Build(GameObject playerPrefab, Color background, int resolution)
        {
            _tilt = new GameObject("Tilt").transform;
            _tilt.SetParent(transform, false);
            _spin = new GameObject("Spin").transform;
            _spin.SetParent(_tilt, false);

            if (playerPrefab != null)
            {
                Transform source = FindVisualRoot(playerPrefab.transform);
                if (source != null)
                {
                    GameObject model = Instantiate(source.gameObject, _spin, false);
                    model.name = "Model";
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.identity;
                    model.transform.localScale = Vector3.Scale(source.localScale, playerPrefab.transform.localScale);

                    // Solo visual: fuera colliders, partículas y scripts
                    foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
                    foreach (var p in model.GetComponentsInChildren<ParticleSystem>(true)) Destroy(p.gameObject);
                    foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);

                    _model = model.transform;
                    _paint = new BladePaint(_model);
                    if (playerPrefab.TryGetComponent(out FakeBladeController blade)) _modelSettings = blade.ModelSettings;
                }
            }

            _texture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32)
            {
                name = $"Preview_{name}",
                filterMode = FilterMode.Point,
                antiAliasing = 1
            };
            _texture.Create();

            var camObj = new GameObject("PreviewCamera");
            camObj.transform.SetParent(transform, false);

            _camera = camObj.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = background;
            _camera.fieldOfView = 30f;
            _camera.nearClipPlane = 0.05f;
            _camera.targetTexture = _texture;
            _camera.allowMSAA = false;
            SettingsService.ConfigureCamera(_camera, false);
            Frame();
        }

        private static Transform FindVisualRoot(Transform prefab)
        {
            Transform pivot = prefab.Find("SpinPivot");
            if (pivot != null) return pivot;
            for (int i = 0; i < prefab.childCount; i++)
            {
                Transform child = prefab.GetChild(i);
                if (child.GetComponentInChildren<Renderer>() != null && child.GetComponent<ParticleSystem>() == null)
                    return child;
            }
            return null;
        }

        /// <summary>
        /// Encuadra la peonza: la cámara apunta a su centro (no a su base) desde poco por encima,
        /// para verla casi de frente. Se mide sin giro ni bamboleo, con lo que se ve (renderers activos).
        /// </summary>
        private void Frame()
        {
            if (_camera == null) return;

            Quaternion tilt = _tilt.localRotation, spin = _spin.localRotation;
            _tilt.localRotation = _spin.localRotation = Quaternion.identity;

            float size = 1f, centerHeight = 0f;
            bool any = false;
            Bounds b = default;
            if (_model != null)
            {
                foreach (MeshRenderer r in _model.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!r.enabled) continue;
                    if (!any) b = r.bounds; else b.Encapsulate(r.bounds);
                    any = true;
                }
            }
            if (any)
            {
                size = Mathf.Max(b.size.x, b.size.z, 0.1f);
                centerHeight = b.center.y - transform.position.y;
            }
            _tilt.localRotation = tilt;
            _spin.localRotation = spin;

            float distance = Mathf.Max(0.5f, size * 2.4f);
            Vector3 focus = transform.position + Vector3.up * centerHeight;
            Transform cam = _camera.transform;
            cam.position = focus + new Vector3(0f, distance * CameraHeight, -distance);
            cam.LookAt(focus);
            _camera.farClipPlane = distance * 4f;
        }

        /// <summary>Monta el modelo con estas piezas (solo si han cambiado) y lo vuelve a pintar y encuadrar.</summary>
        public void SetParts(FakeBladeComponentData tip, FakeBladeComponentData body,
            FakeBladeComponentData blade, FakeBladeComponentData core)
        {
            if (_model == null) return;
            if (_hasParts && tip == _tip && body == _body && blade == _blade && core == _core) return;
            _hasParts = true;
            _tip = tip;
            _body = body;
            _blade = blade;
            _core = core;

            BladeModel.Build(_model, _modelSettings, tip, body, blade, core);
            _paint = new BladePaint(_model);
            if (_hasScheme) _paint.Apply(_scheme);
            _paint.SetCoreGlow(_glowColor, _glowIntensity);
            Frame();
        }

        /// <summary>Colores de cada pieza (paleta del color elegido).</summary>
        public void SetScheme(BladeColorScheme scheme)
        {
            _scheme = scheme;
            _hasScheme = true;
            _paint?.Apply(scheme);
        }

        /// <summary>Brillo del núcleo (color del poder), como en partida.</summary>
        public void SetCoreGlow(Color color, float intensity)
        {
            _glowColor = color;
            _glowIntensity = intensity;
            _paint?.SetCoreGlow(color, intensity);
        }

        /// <summary>Velocidad de giro visual (grados/s), p. ej. según las RPM máximas.</summary>
        public void SetSpinSpeed(float degreesPerSecond) => _spinSpeed = degreesPerSecond;

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _time += dt;
            _angle = (_angle + _spinSpeed * dt) % 360f;
            _spin.localRotation = Quaternion.Euler(0f, _angle, 0f);
            // Bamboleo suave
            _tilt.localRotation = Quaternion.Euler(Mathf.Sin(_time * 1.7f) * 6f, 0f, Mathf.Cos(_time * 1.3f) * 6f);
        }

        private void OnDestroy()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }
    }
}
