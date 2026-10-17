using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Monta los efectos de pantalla en los renderers de URP (O2, GDD 7.6). Crea solo lo que falta:
    /// - La capa VFX (partículas, estelas e iconos de estado: ScreenPixelation.MoveToVfxLayer).
    /// - El material del pixelado (shader FakeBlade/Pixelate).
    /// - En PC_Renderer y Mobile_Renderer: la capa VFX fuera de las capas normales, el pase de pantalla completa
    ///   "FB Pixelate" (antes del postprocesado) y "FB VFX", que pinta la capa VFX después del pixelado, con prueba de
    ///   profundidad (se tapa con lo que tenga delante).
    /// </summary>
    public static class FakeBladeRenderSetup
    {
        private const string MaterialFolder = "Assets/Materials/Render";
        private const string PixelateMaterialPath = MaterialFolder + "/Pixelate.mat";
        private const string PixelateFeatureName = "FB Pixelate";
        private const string VfxFeatureName = "FB VFX";
        private const string VfxTopFeatureName = "FB VFX Encima";
        private static readonly string[] RendererPaths = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };

        [MenuItem("FakeBlade/Setup Render (pixelado)")]
        public static void Run() => Debug.Log("[RenderSetup] " + Setup());

        /// <summary>Hace el montaje y devuelve un resumen.</summary>
        public static string Setup()
        {
            int layer = EnsureLayer(ScreenPixelation.VfxLayerName);
            int topLayer = EnsureLayer(ScreenPixelation.VfxTopLayerName);
            if (layer < 0 || topLayer < 0) return "No hay capas libres para VFX y VFXTop.";

            Material material = EnsurePixelateMaterial();
            if (material == null) return "Falta el shader FakeBlade/Pixelate.";

            string summary = $"capa VFX = {layer}, VFXTop = {topLayer}";
            foreach (string path in RendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null)
                {
                    summary += $" | {path}: no está";
                    continue;
                }
                summary += $" | {Path.GetFileNameWithoutExtension(path)}: {SetupRenderer(data, layer, topLayer, material)}";
            }
            AssetDatabase.SaveAssets();
            return summary;
        }

        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                tagManager.ApplyModifiedProperties();
                return i;
            }
            return -1;
        }

        private static Material EnsurePixelateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PixelateMaterialPath);
            if (material != null) return material;
            Shader shader = Shader.Find("FakeBlade/Pixelate");
            if (shader == null) return null;
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Render");
            material = new Material(shader) { name = "Pixelate" };
            AssetDatabase.CreateAsset(material, PixelateMaterialPath);
            return material;
        }

        private static string SetupRenderer(UniversalRendererData data, int layer, int topLayer, Material material)
        {
            int bit = 1 << layer;
            int topBit = 1 << topLayer;
            data.opaqueLayerMask = data.opaqueLayerMask & ~bit & ~topBit;
            data.transparentLayerMask = data.transparentLayerMask & ~bit & ~topBit;

            string result = "capas";
            if (Find(data, PixelateFeatureName) == null)
            {
                var pixelate = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                pixelate.name = PixelateFeatureName;
                pixelate.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
                pixelate.fetchColorBuffer = true;
                pixelate.requirements = ScriptableRenderPassInput.Color;
                pixelate.passMaterial = material;
                Add(data, pixelate);
                result += ", pixelado";
            }
            if (Find(data, VfxFeatureName) == null)
            {
                // Después del pixelado (mismo momento, va detrás en la lista)
                var vfx = ScriptableObject.CreateInstance<RenderObjects>();
                vfx.name = VfxFeatureName;
                vfx.settings.Event = RenderPassEvent.BeforeRenderingPostProcessing;
                vfx.settings.filterSettings.RenderQueueType = RenderQueueType.Transparent;
                vfx.settings.filterSettings.LayerMask = bit;
                Add(data, vfx);
                result += ", VFX";
            }
            if (Find(data, VfxTopFeatureName) == null)
            {
                // Impactos: después de las partículas y sin prueba de profundidad (no los corta el suelo al bajarlos)
                var top = ScriptableObject.CreateInstance<RenderObjects>();
                top.name = VfxTopFeatureName;
                top.settings.Event = RenderPassEvent.BeforeRenderingPostProcessing;
                top.settings.filterSettings.RenderQueueType = RenderQueueType.Transparent;
                top.settings.filterSettings.LayerMask = topBit;
                top.settings.overrideDepthState = true;
                top.settings.depthCompareFunction = UnityEngine.Rendering.CompareFunction.Always;
                top.settings.enableWrite = false;
                Add(data, top);
                result += ", VFX encima";
            }
            EditorUtility.SetDirty(data);
            return result;
        }

        private static ScriptableRendererFeature Find(ScriptableRendererData data, string name)
        {
            foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                if (feature != null && feature.name == name) return feature;
            return null;
        }

        /// <summary>Como el botón «Add Renderer Feature»: subasset del renderer, en la lista y en su mapa de ids.</summary>
        private static void Add(ScriptableRendererData data, ScriptableRendererFeature feature)
        {
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            var serialized = new SerializedObject(data);
            SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
            SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedProperties();
        }
    }
}
