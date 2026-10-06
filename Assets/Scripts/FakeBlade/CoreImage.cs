using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>Cómo se orienta la imagen del núcleo hacia la cámara.</summary>
    public enum CoreImageMode
    {
        /// <summary>Tumbada sobre el núcleo (se inclina con la peonza), con su "arriba" hacia el de la pantalla.</summary>
        Decal = 0,
        /// <summary>De pie y siempre de frente a la cámara, un poco por encima del núcleo.</summary>
        Billboard = 1
    }

    /// <summary>
    /// Imagen del núcleo (GDD 3, Quehaceres H6): el plano "Imagen de Nucleo" con el icono del poder,
    /// teñido con su color. No gira con la peonza: va en el pivote de inclinación y cada frame se
    /// orienta hacia la cámara. La orientación de la textura sale de las UV de la propia malla,
    /// así que da igual cómo estén giradas en Blender.
    /// </summary>
    public sealed class CoreImage : MonoBehaviour
    {
        public const string ObjectName = "CoreImage";

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Camera _camera;
        private CoreImageMode _mode;
        private Quaternion _meshToBasis = Quaternion.identity;
        private Vector3 _restPosition;
        private float _liftHeight;

        /// <summary>
        /// Crea la imagen bajo parent (lo que no gira). center = centro del plano en el espacio de
        /// parent; worldScale = escala de las piezas en el mundo. camera null = Camera.main.
        /// </summary>
        public static CoreImage Create(Transform parent, GameObject planeModel, Material material, float worldScale,
            CoreImageMode mode, Camera camera, Sprite icon, Color color)
        {
            var pivot = new GameObject(ObjectName).transform;
            pivot.SetParent(parent, false);
            var image = pivot.gameObject.AddComponent<CoreImage>();
            image._camera = camera;
            image._mode = mode;

            GameObject plane = Instantiate(planeModel, pivot, false);
            plane.name = planeModel.name; // "Imagen..." → BladePaint no lo pinta
            foreach (Collider c in plane.GetComponentsInChildren<Collider>(true)) Destroy(c);

            var filter = plane.GetComponentInChildren<MeshFilter>();
            var renderer = plane.GetComponentInChildren<MeshRenderer>();
            if (filter == null || renderer == null || filter.sharedMesh == null)
            {
                Destroy(pivot.gameObject);
                return null;
            }

            // Plano centrado en el pivote, a la escala de las piezas
            float parentScale = Mathf.Max(0.0001f, parent.lossyScale.x);
            float scale = (worldScale > 0f ? worldScale : 1f) / parentScale;
            Mesh mesh = filter.sharedMesh;
            Vector3 center = mesh.bounds.center;
            plane.transform.localScale = Vector3.one * scale;
            plane.transform.localPosition = -center * scale;
            image._restPosition = new Vector3(0f, center.y * scale, 0f);
            image._liftHeight = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.z) * scale;
            pivot.localPosition = image._restPosition;

            image._meshToBasis = MeshBasis(mesh, plane.transform);

            if (material != null) renderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (icon != null) block.SetTexture(MainTexId, icon.texture);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);

            image.ApplyVisibility();
            image.Orient();
            return image;
        }

        // Se ve o no según Opciones (Imagen del núcleo); se aplica al momento al cambiarla
        private void OnEnable()
        {
            SettingsService.OnApplied += ApplyVisibility;
            ApplyVisibility();
        }

        private void OnDisable() => SettingsService.OnApplied -= ApplyVisibility;

        private void ApplyVisibility()
        {
            bool visible = SettingsService.Current.showCoreImage;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        private void LateUpdate() => Orient();

        private void Orient()
        {
            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return;
            Transform c = cam.transform;
            Transform parent = transform.parent;
            Vector3 up = parent != null ? parent.up : Vector3.up;

            Quaternion target;
            if (_mode == CoreImageMode.Billboard)
            {
                transform.localPosition = _restPosition + Vector3.up * _liftHeight;
                target = Quaternion.LookRotation(c.up, -c.forward);
            }
            else
            {
                transform.localPosition = _restPosition;
                Vector3 screenUp = Vector3.ProjectOnPlane(c.forward, up);
                if (screenUp.sqrMagnitude < 0.0001f) screenUp = Vector3.ProjectOnPlane(c.up, up);
                target = Quaternion.LookRotation(screenUp.normalized, up);
            }
            transform.rotation = target * _meshToBasis;
        }

        /// <summary>
        /// Rotación que lleva el "arriba" de la textura (+V) a +Z y la normal del plano a +Y. Se
        /// calcula con el gradiente de las UV sobre la malla. Si las UV están espejadas, se corrige
        /// con escala negativa en el eje de U.
        /// </summary>
        private static Quaternion MeshBasis(Mesh mesh, Transform plane)
        {
            if (!mesh.isReadable)
            {
                Debug.LogWarning($"[CoreImage] La malla {mesh.name} no tiene Read/Write activado en su importación: " +
                                 "no se pueden leer sus UV y la imagen puede salir girada.", plane);
                return Quaternion.identity;
            }

            Vector3[] v = mesh.vertices;
            Vector2[] uv = mesh.uv;
            Vector3[] n = mesh.normals;
            if (uv == null || uv.Length != v.Length || v.Length < 3) return Quaternion.identity;

            Vector3 normal = Vector3.zero;
            for (int i = 0; i < n.Length; i++) normal += n[i];
            normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;

            // Gradiente de U y V sobre el plano XZ (mínimos cuadrados)
            Vector3 c = mesh.bounds.center;
            Vector2 uc = Vector2.zero;
            for (int i = 0; i < uv.Length; i++) uc += uv[i];
            uc /= uv.Length;
            double sxx = 0, sxz = 0, szz = 0, sux = 0, suz = 0, svx = 0, svz = 0;
            for (int i = 0; i < v.Length; i++)
            {
                double x = v[i].x - c.x, z = v[i].z - c.z, du = uv[i].x - uc.x, dv = uv[i].y - uc.y;
                sxx += x * x; sxz += x * z; szz += z * z;
                sux += du * x; suz += du * z; svx += dv * x; svz += dv * z;
            }
            double det = sxx * szz - sxz * sxz;
            if (System.Math.Abs(det) < 1e-12) return Quaternion.identity;
            var uDir = new Vector3((float)((sux * szz - suz * sxz) / det), 0f, (float)((suz * sxx - sux * sxz) / det));
            var vDir = new Vector3((float)((svx * szz - svz * sxz) / det), 0f, (float)((svz * sxx - svx * sxz) / det));
            if (vDir.sqrMagnitude < 1e-8f) return Quaternion.identity;

            // LookRotation(vDir, normal) deja la derecha en cross(normal, vDir): si U va al revés, espejo
            if (Vector3.Dot(uDir, Vector3.Cross(normal, vDir)) < 0f)
            {
                Vector3 s = plane.localScale;
                if (Mathf.Abs(uDir.x) >= Mathf.Abs(uDir.z)) s.x = -s.x; else s.z = -s.z;
                plane.localScale = s;
                plane.localPosition = Vector3.Scale(plane.localPosition, new Vector3(Mathf.Sign(s.x), 1f, Mathf.Sign(s.z)));
            }
            return Quaternion.Inverse(Quaternion.LookRotation(vDir.normalized, normal));
        }
    }
}
