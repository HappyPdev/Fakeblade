using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FakeBlade.Core
{
    /// <summary>Cómo se monta el modelo de la peonza a partir de sus piezas (en el prefab, FakeBladeController).</summary>
    [Serializable]
    public struct BladeModelSettings
    {
        [Tooltip("Escala de las piezas en el mundo. Los modelos están hechos a la escala del juego: 1")]
        public float worldScale;
        [Tooltip("Material de punta, cuerpo y disco (se tiñe con BladePaint). Vacío = el del FBX")]
        public Material partMaterial;
        [Tooltip("Material del núcleo: necesita la emisión activada para brillar (BladeCoreMaterial)")]
        public Material coreMaterial;

        [Header("Imagen del núcleo (H6, en pruebas)")]
        [Tooltip("Plano con el icono del poder sobre el núcleo (Imagen_de_Nucleo). Vacío = sin imagen")]
        public GameObject coreImage;
        [Tooltip("Material del plano (sprite: textura y color por MaterialPropertyBlock)")]
        public Material coreImageMaterial;
        public CoreImageMode coreImageMode;

        public static BladeModelSettings Default => new BladeModelSettings { worldScale = 1f };
    }

    /// <summary>
    /// Monta el modelo de una peonza con los modelos de sus piezas (GDD 3): uno por pieza, todos
    /// con el mismo origen (el punto de apoyo), bajo un contenedor dentro del pivote de giro.
    /// Si ninguna pieza tiene modelo se queda el modelo antiguo del prefab; si alguna lo tiene,
    /// el antiguo se oculta. Cada instancia se nombra con su hueco delante para que BladePaint
    /// sepa qué pieza es ("Tip_...", "Body_...", "Blade_...", "Core_...").
    /// </summary>
    public static class BladeModel
    {
        public const string ContainerName = "PartModels";

        /// <summary>
        /// Monta (o vuelve a montar) el modelo. Devuelve el contenedor, o null si se usa el antiguo.
        /// imageParent = lo que no gira (pivote de inclinación) para la imagen del núcleo;
        /// imageCamera = cámara a la que mira (null = Camera.main).
        /// </summary>
        public static Transform Build(Transform visualRoot, Transform imageParent, BladeModelSettings settings,
            FakeBladeComponentData tip, FakeBladeComponentData body, FakeBladeComponentData blade,
            FakeBladeComponentData core, Camera imageCamera = null)
        {
            if (visualRoot == null) return null;

            Remove(visualRoot.Find(ContainerName));
            if (imageParent != null) Remove(imageParent.Find(CoreImage.ObjectName));

            Transform container = null;
            Add(ref container, visualRoot, settings, ComponentSlot.Tip, tip);
            Add(ref container, visualRoot, settings, ComponentSlot.Body, body);
            Add(ref container, visualRoot, settings, ComponentSlot.Blade, blade);
            Add(ref container, visualRoot, settings, ComponentSlot.Core, core);

            SetLegacyVisible(visualRoot, container == null);

            if (container != null && core != null && core.Model != null && settings.coreImage != null && imageParent != null)
            {
                SpecialAbilityType power = core.SpecialAbility != SpecialAbilityType.None ? core.SpecialAbility : SpecialAbilityType.SpinBoost;
                CoreImage.Create(imageParent, settings.coreImage, settings.coreImageMaterial, settings.worldScale,
                    settings.coreImageMode, imageCamera, PowerIcons.Get(power), SpecialAbilities.Get(power).color);
            }
            return container;
        }

        private static void Remove(Transform previous)
        {
            if (previous == null) return;
            // Fuera de la jerarquía ya, para que nadie lo recoja este frame
            previous.SetParent(null, false);
            if (Application.isPlaying) Object.Destroy(previous.gameObject);
            else Object.DestroyImmediate(previous.gameObject);
        }

        private static void Add(ref Transform container, Transform visualRoot, BladeModelSettings settings,
            ComponentSlot slot, FakeBladeComponentData part)
        {
            if (part == null || part.Model == null) return;

            if (container == null)
            {
                container = new GameObject(ContainerName).transform;
                container.SetParent(visualRoot, false);
                float parentScale = Mathf.Max(0.0001f, visualRoot.lossyScale.x);
                float scale = settings.worldScale > 0f ? settings.worldScale : 1f;
                container.localScale = Vector3.one * (scale / parentScale);
            }

            GameObject instance = Object.Instantiate(part.Model, container, false);
            instance.name = $"{slot}_{part.Model.name}";

            // Solo visual: la física es el collider del prefab
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);

            Material material = slot == ComponentSlot.Core ? settings.coreMaterial : settings.partMaterial;
            if (material != null)
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                    if (!BladePaint.IsImagePlane(r.name)) r.sharedMaterial = material;
        }

        /// <summary>El modelo antiguo del prefab (lo que hay en el pivote fuera del contenedor).</summary>
        private static void SetLegacyVisible(Transform visualRoot, bool visible)
        {
            for (int i = 0; i < visualRoot.childCount; i++)
            {
                Transform child = visualRoot.GetChild(i);
                if (child.name == ContainerName) continue;
                foreach (Renderer r in child.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = visible;
            }
        }
    }
}
