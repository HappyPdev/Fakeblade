using UnityEngine;
using UnityEngine.Rendering;

namespace FakeBlade.Core
{
    /// <summary>
    /// Pixelado de la escena 3D (O2, GDD 7.6), configurable en Opciones → Pixelado (No, Suave, Medio y Fuerte).
    ///
    /// El pixelado lo hace un pase de pantalla completa de los renderers de URP ("FB Pixelate", shader
    /// FakeBlade/Pixelate; lo monta *FakeBlade → Setup Render*). Las partículas, estelas e iconos van en la capa
    /// VFX, que se pinta después del pixelado ("FB VFX"): se ven nítidas y se siguen tapando con lo que tengan delante.
    /// La interfaz (Overlay) no se toca.
    ///
    /// Antes de pintar cada cámara se fija el tamaño de bloque: solo las cámaras de juego que pintan en pantalla
    /// (batalla, Sandbox y fondo del menú); las que pintan en una textura (vista previa de la selección, que tiene su
    /// propio pixelado) y la vista de escena van sin pixelar. El tamaño depende de la altura de la pantalla, para que
    /// se vea igual en 1080p y en 1440p. Sin GC.
    /// </summary>
    public static class ScreenPixelation
    {
        public const string VfxLayerName = "VFX";
        /// <summary>Capa de los impactos: se pintan sin prueba de profundidad (no los tapan el suelo ni las peonzas).</summary>
        public const string VfxTopLayerName = "VFXTop";

        /// <summary>Alto virtual de la escena por nivel (0 = sin pixelar): Suave 540, Medio 360 y Fuerte 270 líneas.</summary>
        private static readonly int[] VirtualLines = { 0, 540, 360, 270 };
        private static readonly int PixelSizeId = Shader.PropertyToID("_FB_PixelSize");
        private static int _vfxLayer = -2;

        public static int LevelCount => VirtualLines.Length;

        /// <summary>Capa de los efectos que no se pixelan (-1 si falta: entonces se pixelan con la escena).</summary>
        public static int VfxLayer
        {
            get
            {
                if (_vfxLayer == -2) _vfxLayer = LayerMask.NameToLayer(VfxLayerName);
                return _vfxLayer;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _vfxLayer = -2;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            Application.quitting -= Reset;
            Application.quitting += Reset;
        }

        /// <summary>Al salir (también al parar el Play en el editor), la escena del editor vuelve a verse sin pixelar.</summary>
        private static void Reset()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            Shader.SetGlobalFloat(PixelSizeId, 1f);
        }

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            bool gameCamera = camera.cameraType == CameraType.Game && camera.targetTexture == null;
            Shader.SetGlobalFloat(PixelSizeId, gameCamera ? PixelSize(camera.pixelHeight) : 1f);
        }

        /// <summary>Tamaño del bloque en píxeles de pantalla con el nivel elegido en Opciones.</summary>
        public static float PixelSize(int screenHeight)
        {
            int level = Mathf.Clamp(SettingsService.Current.pixelation, 0, VirtualLines.Length - 1);
            int lines = VirtualLines[level];
            return lines <= 0 ? 1f : Mathf.Max(1f, Mathf.Round(screenHeight / (float)lines));
        }

        /// <summary>Pasa un objeto y sus hijos a la capa VFX (no se pixelan). Se llama al crearlos, no cada frame.</summary>
        public static void MoveToVfxLayer(GameObject go)
        {
            int layer = VfxLayer;
            if (go == null || layer < 0) return;
            SetLayer(go.transform, layer);
        }

        /// <summary>
        /// Pasa un objeto a la capa VFXTop (impactos): nítido y siempre por delante, sin prueba de profundidad (pase
        /// «FB VFX Encima»). Si falta la capa, se queda en VFX.
        /// </summary>
        public static void MoveToVfxTopLayer(GameObject go)
        {
            int layer = LayerMask.NameToLayer(VfxTopLayerName);
            if (go == null) return;
            if (layer < 0) MoveToVfxLayer(go);
            else SetLayer(go.transform, layer);
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }
    }
}
