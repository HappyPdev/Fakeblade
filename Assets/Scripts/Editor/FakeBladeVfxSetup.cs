using System.Text;
using UnityEditor;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Genera los efectos de partículas por defecto (estilo pixel: cuadrados planos sin textura),
    /// su material y la VfxLibrary en Resources. Idempotente: vuelve a escribir los prefabs.
    /// Los prefabs se pueden retocar a mano después en Assets/VFX/Prefabs.
    /// </summary>
    public static class FakeBladeVfxSetup
    {
        private const string PrefabFolder = "Assets/VFX/Prefabs";
        private const string MaterialFolder = "Assets/VFX/Materials";
        private const string MaterialPath = MaterialFolder + "/VFX_PixelSquare.mat";
        private const string LibraryPath = "Assets/Resources/VfxLibrary.asset";

        private struct Spec
        {
            public VfxType Type;
            public ParticleSystemShapeType Shape;
            public float Radius, Angle;
            public float SpeedMin, SpeedMax, LifeMin, LifeMax, SizeMin, SizeMax, Gravity;
            public int Burst, Max;
        }

        [MenuItem("FakeBlade/Setup VFX")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            var log = new StringBuilder();
            FakeBladeAssetsMenu.EnsureFolder(PrefabFolder);
            FakeBladeAssetsMenu.EnsureFolder(MaterialFolder);
            FakeBladeAssetsMenu.EnsureFolder("Assets/Resources");

            Material material = GetMaterial();

            Spec[] specs =
            {
                new Spec { Type = VfxType.Clash, Shape = ParticleSystemShapeType.Sphere, Radius = 0.1f,
                    SpeedMin = 3f, SpeedMax = 7f, LifeMin = 0.2f, LifeMax = 0.4f, SizeMin = 0.06f, SizeMax = 0.12f, Gravity = 1.5f, Burst = 18, Max = 300 },
                new Spec { Type = VfxType.WallHit, Shape = ParticleSystemShapeType.Cone, Radius = 0.05f, Angle = 35f,
                    SpeedMin = 2f, SpeedMax = 4f, LifeMin = 0.15f, LifeMax = 0.3f, SizeMin = 0.05f, SizeMax = 0.08f, Gravity = 1f, Burst = 8, Max = 120 },
                new Spec { Type = VfxType.Dash, Shape = ParticleSystemShapeType.Cone, Radius = 0.2f, Angle = 20f,
                    SpeedMin = 3f, SpeedMax = 6f, LifeMin = 0.25f, LifeMax = 0.4f, SizeMin = 0.08f, SizeMax = 0.14f, Gravity = 0f, Burst = 16, Max = 200 },
                new Spec { Type = VfxType.Attack, Shape = ParticleSystemShapeType.Cone, Radius = 0.15f, Angle = 30f,
                    SpeedMin = 2f, SpeedMax = 4f, LifeMin = 0.2f, LifeMax = 0.35f, SizeMin = 0.06f, SizeMax = 0.1f, Gravity = 0f, Burst = 10, Max = 200 },
                new Spec { Type = VfxType.SpinOut, Shape = ParticleSystemShapeType.Sphere, Radius = 0.3f,
                    SpeedMin = 2f, SpeedMax = 8f, LifeMin = 0.5f, LifeMax = 1f, SizeMin = 0.08f, SizeMax = 0.16f, Gravity = 2f, Burst = 40, Max = 200 },
                new Spec { Type = VfxType.SpecialBurst, Shape = ParticleSystemShapeType.Circle, Radius = 0.4f,
                    SpeedMin = 4f, SpeedMax = 6f, LifeMin = 0.35f, LifeMax = 0.5f, SizeMin = 0.1f, SizeMax = 0.16f, Gravity = 0f, Burst = 36, Max = 300 },
                new Spec { Type = VfxType.SpecialAura, Shape = ParticleSystemShapeType.Circle, Radius = 0.55f,
                    SpeedMin = 0.3f, SpeedMax = 0.8f, LifeMin = 0.4f, LifeMax = 0.7f, SizeMin = 0.05f, SizeMax = 0.1f, Gravity = -0.5f, Burst = 1, Max = 400 },
                new Spec { Type = VfxType.Respawn, Shape = ParticleSystemShapeType.Circle, Radius = 0.6f,
                    SpeedMin = 0.5f, SpeedMax = 1.5f, LifeMin = 0.5f, LifeMax = 0.8f, SizeMin = 0.08f, SizeMax = 0.12f, Gravity = -1.5f, Burst = 30, Max = 200 },
                // Parry: estallido rápido en estrella, partículas grandes y brillantes
                new Spec { Type = VfxType.Parry, Shape = ParticleSystemShapeType.Sphere, Radius = 0.05f,
                    SpeedMin = 6f, SpeedMax = 10f, LifeMin = 0.12f, LifeMax = 0.28f, SizeMin = 0.12f, SizeMax = 0.2f, Gravity = 0f, Burst = 28, Max = 200 },
            };

            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<VfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.entries.Clear();
            foreach (Spec spec in specs)
            {
                ParticleSystem prefab = CreateEffect(spec, material);
                library.entries.Add(new VfxLibrary.Entry { type = spec.Type, prefab = prefab, burstCount = spec.Burst });
                log.Append(spec.Type).Append(' ');
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            log.Insert(0, "VFX generados: ").AppendLine().Append("Biblioteca: ").Append(LibraryPath);
            return log.ToString();
        }

        private static Material GetMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            material = new Material(shader) { name = "VFX_PixelSquare" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static ParticleSystem CreateEffect(Spec s, Material material)
        {
            var go = new GameObject("VFX_" + s.Type);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.LifeMin, s.LifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.SpeedMin, s.SpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(s.SizeMin, s.SizeMax);
            main.startColor = Color.white;
            main.gravityModifier = s.Gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = s.Max;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = s.Shape;
            shape.radius = s.Radius;
            if (s.Shape == ParticleSystemShapeType.Cone) shape.angle = s.Angle;
            if (s.Shape == ParticleSystemShapeType.Circle) shape.arc = 360f;

            // Encoger por pasos (4 tramos) → estética pixel
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var curve = new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f), new Keyframe(0.25f, 1f, 0f, 0f),
                new Keyframe(0.26f, 0.75f, 0f, 0f), new Keyframe(0.5f, 0.75f, 0f, 0f),
                new Keyframe(0.51f, 0.5f, 0f, 0f), new Keyframe(0.75f, 0.5f, 0f, 0f),
                new Keyframe(0.76f, 0.25f, 0f, 0f), new Keyframe(1f, 0.25f, 0f, 0f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 0.5f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            string path = $"{PrefabFolder}/VFX_{s.Type}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ParticleSystem>();
        }
    }
}
