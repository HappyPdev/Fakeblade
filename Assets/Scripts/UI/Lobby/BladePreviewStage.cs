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
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Camera _camera;
        private RenderTexture _texture;
        private Transform _tilt;
        private Transform _spin;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
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
            _block = new MaterialPropertyBlock();

            _tilt = new GameObject("Tilt").transform;
            _tilt.SetParent(transform, false);
            _spin = new GameObject("Spin").transform;
            _spin.SetParent(_tilt, false);

            float size = 1f;
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

                    _renderers = model.GetComponentsInChildren<Renderer>(true);
                    size = MeasureSize(_renderers);
                }
            }
            if (_renderers == null) _renderers = new Renderer[0];

            _texture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32)
            {
                name = $"Preview_{name}",
                filterMode = FilterMode.Point,
                antiAliasing = 1
            };
            _texture.Create();

            var camObj = new GameObject("PreviewCamera");
            camObj.transform.SetParent(transform, false);
            float distance = Mathf.Max(0.5f, size * 2.4f);
            camObj.transform.localPosition = new Vector3(0f, distance * 0.55f, -distance);
            camObj.transform.LookAt(transform.position + Vector3.up * size * 0.05f);

            _camera = camObj.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = background;
            _camera.fieldOfView = 30f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = distance * 4f;
            _camera.targetTexture = _texture;
            _camera.allowMSAA = false;
            SettingsService.ConfigureCamera(_camera, false);
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

        private float MeasureSize(Renderer[] renderers)
        {
            if (renderers.Length == 0) return 1f;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return Mathf.Max(b.size.x, b.size.z, 0.1f);
        }

        public void SetColor(Color color)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(EmissionColorId, color * 0.2f);
                r.SetPropertyBlock(_block);
            }
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
