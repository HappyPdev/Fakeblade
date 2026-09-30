using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Genera los efectos de partículas por defecto, estilo pixel art con brillo:
    /// - Texturas pixel (chispa, estrella, cruz, llama, anillo, humo, rayo, píxel) con filtro Point.
    /// - Un material URP Particles/Unlit por forma, con transparencia normal y color HDR (&gt;1)
    ///   para que el bloom lo haga brillar. No es aditivo a propósito: sobre el suelo claro de la
    ///   arena el aditivo satura a blanco y se pierde el color de cada efecto.
    /// - Un prefab de ParticleSystem por VfxType y la VfxLibrary en Resources.
    /// Idempotente: vuelve a escribir texturas, materiales y prefabs. Se pueden retocar a mano
    /// después en Assets/VFX (volver a ejecutar el menú sobrescribe esos cambios).
    /// </summary>
    public static class FakeBladeVfxSetup
    {
        private const string PrefabFolder = "Assets/VFX/Prefabs";
        private const string MaterialFolder = "Assets/VFX/Materials";
        private const string TextureFolder = "Assets/VFX/Textures";
        private const string LegacyMaterialPath = MaterialFolder + "/VFX_PixelSquare.mat";
        private const string LibraryPath = "Assets/Resources/VfxLibrary.asset";

        private enum PixelSprite { Pixel, Spark, Star, Plus, Flame, Ring, Smoke, Bolt }
        private enum SizeCurve { Shrink, Grow, Puff, Constant }

        private struct Spec
        {
            public VfxType Type;
            public PixelSprite Sprite;
            public ParticleSystemRenderMode Render;
            public SizeCurve Size;
            public bool Fade;
            public ParticleSystemShapeType Shape;
            public float Radius, Angle;
            public float SpeedMin, SpeedMax, LifeMin, LifeMax, SizeMin, SizeMax, Gravity;
            public int Burst, Max;
        }

        #region Pixel art
        // '#' = opaco, '+' = medio, '.' = tenue, ' ' = vacío. Se centran en una textura cuadrada.
        private static readonly Dictionary<PixelSprite, string[]> Patterns = new Dictionary<PixelSprite, string[]>
        {
            [PixelSprite.Pixel] = new[] { "##", "##" },
            [PixelSprite.Spark] = new[]
            {
                "    .    ",
                "    +    ",
                "    #    ",
                "   +#+   ",
                ".+#####+.",
                "   +#+   ",
                "    #    ",
                "    +    ",
                "    .    ",
            },
            [PixelSprite.Star] = new[]
            {
                "     .     ",
                "     +     ",
                "     #     ",
                "  .  #  .  ",
                "    ###    ",
                ".+#######+.",
                "    ###    ",
                "  .  #  .  ",
                "     #     ",
                "     +     ",
                "     .     ",
            },
            [PixelSprite.Plus] = new[]
            {
                "  ###  ",
                "  ###  ",
                "#######",
                "#######",
                "#######",
                "  ###  ",
                "  ###  ",
            },
            [PixelSprite.Flame] = new[]
            {
                "    .    ",
                "    +    ",
                "   .#    ",
                "   +#+   ",
                "  .###.  ",
                "  +###+  ",
                " .#####. ",
                " +#####+ ",
                " ####### ",
                " ####### ",
                "  #####  ",
                "   ###   ",
            },
            [PixelSprite.Ring] = new[]
            {
                "      ####      ",
                "    ##....##    ",
                "   #.      .#   ",
                "  #          #  ",
                " #.          .# ",
                " #            # ",
                "#.            .#",
                "#              #",
                "#              #",
                "#.            .#",
                " #            # ",
                " #.          .# ",
                "  #          #  ",
                "   #.      .#   ",
                "    ##....##    ",
                "      ####      ",
            },
            [PixelSprite.Smoke] = new[]
            {
                "   ++++   ",
                "  +####+  ",
                " +######+ ",
                "+########+",
                "+########+",
                "+########+",
                "+########+",
                " +######+ ",
                "  +####+  ",
                "   ++++   ",
            },
            [PixelSprite.Bolt] = new[]
            {
                "      ##",
                "     ## ",
                "    ##  ",
                "   ##   ",
                "  ##    ",
                " #######",
                "     ## ",
                "    ##  ",
                "   ##   ",
                "  ##    ",
                " ##     ",
                "##      ",
            },
        };

        /// <summary>
        /// Multiplicador HDR del color del material. Más de 1 = lo recoge el bloom (umbral 0,9);
        /// moderado para que el canal dominante sature pero se conserve el tono del efecto.
        /// </summary>
        private static float GlowIntensity(PixelSprite sprite)
        {
            switch (sprite)
            {
                case PixelSprite.Bolt: return 2f;
                case PixelSprite.Star: return 1.8f;
                case PixelSprite.Spark: return 1.7f;
                case PixelSprite.Ring:
                case PixelSprite.Flame: return 1.5f;
                case PixelSprite.Plus: return 1.4f;
                case PixelSprite.Smoke: return 1f; // el humo no brilla
                default: return 1.3f;
            }
        }
        #endregion

        [MenuItem("FakeBlade/Setup VFX")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            var log = new StringBuilder();
            FakeBladeAssetsMenu.EnsureFolder(PrefabFolder);
            FakeBladeAssetsMenu.EnsureFolder(MaterialFolder);
            FakeBladeAssetsMenu.EnsureFolder(TextureFolder);
            FakeBladeAssetsMenu.EnsureFolder("Assets/Resources");

            const ParticleSystemShapeType Sphere = ParticleSystemShapeType.Sphere;
            const ParticleSystemShapeType Cone = ParticleSystemShapeType.Cone;
            const ParticleSystemShapeType Circle = ParticleSystemShapeType.Circle;
            const ParticleSystemRenderMode Flat = ParticleSystemRenderMode.HorizontalBillboard;
            const ParticleSystemRenderMode Stretch = ParticleSystemRenderMode.Stretch;

            // Los efectos continuos de las peonzas (auras, carga, estela, chispas, humo) fijan posición
            // y velocidad al emitir, así que su forma solo se usa si se lanzan como ráfaga con Play().
            Spec[] specs =
            {
                // --- Golpes y acciones ---
                new Spec { Type = VfxType.Clash, Sprite = PixelSprite.Spark, Shape = Sphere, Radius = 0.1f,
                    SpeedMin = 3f, SpeedMax = 7f, LifeMin = 0.2f, LifeMax = 0.4f, SizeMin = 0.1f, SizeMax = 0.18f, Gravity = 1.5f, Burst = 18, Max = 300 },
                new Spec { Type = VfxType.WallHit, Sprite = PixelSprite.Spark, Shape = Cone, Radius = 0.05f, Angle = 35f,
                    SpeedMin = 2f, SpeedMax = 4f, LifeMin = 0.15f, LifeMax = 0.3f, SizeMin = 0.09f, SizeMax = 0.14f, Gravity = 1f, Burst = 8, Max = 120 },
                new Spec { Type = VfxType.Dash, Sprite = PixelSprite.Pixel, Render = Stretch, Shape = Cone, Radius = 0.2f, Angle = 20f,
                    SpeedMin = 3f, SpeedMax = 6f, LifeMin = 0.25f, LifeMax = 0.4f, SizeMin = 0.07f, SizeMax = 0.1f, Gravity = 0f, Burst = 16, Max = 200 },
                new Spec { Type = VfxType.Attack, Sprite = PixelSprite.Spark, Shape = Cone, Radius = 0.15f, Angle = 30f,
                    SpeedMin = 2f, SpeedMax = 4f, LifeMin = 0.2f, LifeMax = 0.35f, SizeMin = 0.1f, SizeMax = 0.16f, Gravity = 0f, Burst = 10, Max = 200 },
                new Spec { Type = VfxType.SpinOut, Sprite = PixelSprite.Pixel, Shape = Sphere, Radius = 0.3f,
                    SpeedMin = 2f, SpeedMax = 8f, LifeMin = 0.5f, LifeMax = 1f, SizeMin = 0.08f, SizeMax = 0.16f, Gravity = 2f, Burst = 40, Max = 200 },
                new Spec { Type = VfxType.SpecialBurst, Sprite = PixelSprite.Star, Shape = Circle, Radius = 0.4f,
                    SpeedMin = 4f, SpeedMax = 6f, LifeMin = 0.35f, LifeMax = 0.5f, SizeMin = 0.16f, SizeMax = 0.26f, Gravity = 0f, Burst = 36, Max = 300 },
                new Spec { Type = VfxType.SpecialAura, Sprite = PixelSprite.Pixel, Shape = Circle, Radius = 0.55f,
                    SpeedMin = 0.3f, SpeedMax = 0.8f, LifeMin = 0.4f, LifeMax = 0.7f, SizeMin = 0.05f, SizeMax = 0.1f, Gravity = -0.5f, Burst = 1, Max = 400 },
                new Spec { Type = VfxType.Respawn, Sprite = PixelSprite.Star, Shape = Circle, Radius = 0.6f,
                    SpeedMin = 0.5f, SpeedMax = 1.5f, LifeMin = 0.5f, LifeMax = 0.8f, SizeMin = 0.12f, SizeMax = 0.2f, Gravity = -1.5f, Burst = 30, Max = 200 },
                // Parry: estallido rápido de estrellas grandes y brillantes
                new Spec { Type = VfxType.Parry, Sprite = PixelSprite.Star, Shape = Sphere, Radius = 0.05f,
                    SpeedMin = 6f, SpeedMax = 10f, LifeMin = 0.12f, LifeMax = 0.28f, SizeMin = 0.2f, SizeMax = 0.34f, Gravity = 0f, Burst = 28, Max = 200 },

                // --- Auras de los poderes ---
                new Spec { Type = VfxType.AuraSpinBoost, Sprite = PixelSprite.Plus, Shape = Circle, Radius = 0.5f,
                    SpeedMin = 1f, SpeedMax = 2f, LifeMin = 0.55f, LifeMax = 0.65f, SizeMin = 0.09f, SizeMax = 0.13f, Burst = 1, Max = 300 },
                new Spec { Type = VfxType.AuraShockWave, Sprite = PixelSprite.Ring, Render = Flat, Size = SizeCurve.Grow, Fade = true,
                    Shape = Sphere, Radius = 0.01f, LifeMin = 0.45f, LifeMax = 0.5f, SizeMin = 1.6f, SizeMax = 1.6f, Burst = 1, Max = 30 },
                new Spec { Type = VfxType.AuraStormBreaker, Sprite = PixelSprite.Pixel, Shape = Circle, Radius = 0.6f,
                    LifeMin = 0.2f, LifeMax = 0.26f, SizeMin = 0.09f, SizeMax = 0.11f, Burst = 1, Max = 400 },
                new Spec { Type = VfxType.AuraElectric, Sprite = PixelSprite.Bolt, Size = SizeCurve.Constant, Shape = Sphere, Radius = 0.5f,
                    LifeMin = 0.07f, LifeMax = 0.12f, SizeMin = 0.25f, SizeMax = 0.4f, Burst = 2, Max = 80 },

                // --- Ataque cargado ---
                new Spec { Type = VfxType.ChargeGather, Sprite = PixelSprite.Spark, Shape = Sphere, Radius = 1f,
                    LifeMin = 0.3f, LifeMax = 0.3f, SizeMin = 0.08f, SizeMax = 0.12f, Burst = 1, Max = 400 },
                new Spec { Type = VfxType.ChargeRing, Sprite = PixelSprite.Ring, Render = Flat, Size = SizeCurve.Grow, Fade = true,
                    Shape = Sphere, Radius = 0.01f, LifeMin = 0.28f, LifeMax = 0.3f, SizeMin = 1.5f, SizeMax = 1.5f, Burst = 1, Max = 30 },
                new Spec { Type = VfxType.ChargeFlame, Sprite = PixelSprite.Flame, Shape = Circle, Radius = 0.35f,
                    SpeedMin = 1.2f, SpeedMax = 2.2f, LifeMin = 0.3f, LifeMax = 0.45f, SizeMin = 0.14f, SizeMax = 0.22f, Gravity = -0.3f, Burst = 6, Max = 200 },

                // --- Peonza en movimiento / RPM bajas ---
                new Spec { Type = VfxType.Trail, Sprite = PixelSprite.Pixel, Fade = true, Shape = Sphere, Radius = 0.05f,
                    LifeMin = 0.3f, LifeMax = 0.45f, SizeMin = 0.1f, SizeMax = 0.1f, Burst = 1, Max = 800 },
                new Spec { Type = VfxType.GroundSpark, Sprite = PixelSprite.Pixel, Render = Stretch, Shape = Cone, Radius = 0.05f, Angle = 40f,
                    SpeedMin = 2f, SpeedMax = 4f, LifeMin = 0.2f, LifeMax = 0.35f, SizeMin = 0.05f, SizeMax = 0.07f, Gravity = 3f, Burst = 8, Max = 400 },
                new Spec { Type = VfxType.LowSpinSmoke, Sprite = PixelSprite.Smoke, Size = SizeCurve.Puff, Fade = true,
                    Shape = Sphere, Radius = 0.2f, SpeedMin = 0.5f, SpeedMax = 1.2f, LifeMin = 0.7f, LifeMax = 1.1f, SizeMin = 0.18f, SizeMax = 0.28f,
                    Gravity = -0.1f, Burst = 10, Max = 300 },
                new Spec { Type = VfxType.LowSpinSpark, Sprite = PixelSprite.Spark, Shape = Sphere, Radius = 0.2f,
                    SpeedMin = 1.5f, SpeedMax = 3.5f, LifeMin = 0.3f, LifeMax = 0.5f, SizeMin = 0.07f, SizeMax = 0.1f, Gravity = 3f, Burst = 3, Max = 150 },
                new Spec { Type = VfxType.Sparkle, Sprite = PixelSprite.Star, Shape = Sphere, Radius = 0.4f,
                    SpeedMin = 1f, SpeedMax = 3f, LifeMin = 0.25f, LifeMax = 0.4f, SizeMin = 0.16f, SizeMax = 0.26f, Burst = 14, Max = 200 },
            };

            var materials = new Dictionary<PixelSprite, Material>();
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<VfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.entries.Clear();
            foreach (Spec spec in specs)
            {
                if (!materials.TryGetValue(spec.Sprite, out Material material))
                {
                    material = GetMaterial(spec.Sprite);
                    materials.Add(spec.Sprite, material);
                }
                ParticleSystem prefab = CreateEffect(spec, material);
                library.entries.Add(new VfxLibrary.Entry { type = spec.Type, prefab = prefab, burstCount = spec.Burst });
                log.Append(spec.Type).Append(' ');
            }

            // Material del sistema anterior (cuadrado opaco sin textura), ya sin uso
            if (AssetDatabase.LoadAssetAtPath<Material>(LegacyMaterialPath) != null)
                AssetDatabase.DeleteAsset(LegacyMaterialPath);

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            log.Insert(0, "VFX generados: ").AppendLine()
               .Append("Materiales: ").Append(materials.Count).AppendLine()
               .Append("Biblioteca: ").Append(LibraryPath);
            return log.ToString();
        }

        #region Textures & materials
        private static Texture2D GetTexture(PixelSprite sprite)
        {
            string path = $"{TextureFolder}/VFX_{sprite}.png";
            string[] rows = Patterns[sprite];
            int height = rows.Length;
            int width = 0;
            foreach (string row in rows) width = Mathf.Max(width, row.Length);
            int size = Mathf.Max(width, height);
            int offsetX = (size - width) / 2;
            int offsetY = (size - height) / 2;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < height; y++)
            {
                string row = rows[y];
                for (int x = 0; x < row.Length; x++)
                {
                    byte alpha = row[x] == '#' ? (byte)255 : row[x] == '+' ? (byte)140 : row[x] == '.' ? (byte)64 : (byte)0;
                    // La fila 0 del patrón es la de arriba; en la textura, y = 0 es abajo
                    int px = offsetX + x;
                    int py = size - 1 - (offsetY + y);
                    pixels[py * size + px] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material GetMaterial(PixelSprite sprite)
        {
            string path = $"{MaterialFolder}/VFX_{sprite}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", GetTexture(sprite));
            float glow = GlowIntensity(sprite);
            material.SetColor("_BaseColor", new Color(glow, glow, glow, 1f));
            material.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Transparent);
            material.SetFloat("_Blend", (float)BaseShaderGUI.BlendMode.Alpha);
            material.SetFloat("_ColorMode", 0f); // Multiply: el color de la partícula tiñe la textura blanca
            material.SetFloat("_SoftParticlesEnabled", 0f);
            material.SetFloat("_CameraFadingEnabled", 0f);
            material.SetFloat("_DistortionEnabled", 0f);
            material.SetFloat("_FlipbookBlending", 0f);
            BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);

            EditorUtility.SetDirty(material);
            return material;
        }
        #endregion

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

            if (s.Size != SizeCurve.Constant)
            {
                var sizeOverLifetime = ps.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, SizeCurveFor(s.Size));
            }

            if (s.Fade)
            {
                // Desvanecido por pasos (estética pixel): 100% → 60% → 25%
                var gradient = new Gradient { mode = GradientMode.Fixed };
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0.45f), new GradientAlphaKey(0.6f, 0.75f), new GradientAlphaKey(0.25f, 1f) });
                var colorOverLifetime = ps.colorOverLifetime;
                colorOverLifetime.enabled = true;
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = s.Render;
            if (s.Render == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = 0.05f;
                renderer.lengthScale = 1f;
            }
            renderer.maxParticleSize = s.Render == ParticleSystemRenderMode.HorizontalBillboard ? 2f : 0.5f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (s.Sprite == PixelSprite.Smoke) renderer.sortMode = ParticleSystemSortMode.Distance;

            string path = $"{PrefabFolder}/VFX_{s.Type}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ParticleSystem>();
        }

        private static AnimationCurve SizeCurveFor(SizeCurve kind)
        {
            switch (kind)
            {
                case SizeCurve.Grow:
                    // Onda que se expande deprisa y frena al final
                    return new AnimationCurve(new Keyframe(0f, 0.15f, 2.5f, 2.5f), new Keyframe(1f, 1f, 0.3f, 0.3f));
                case SizeCurve.Puff:
                    return new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f));
                default:
                    // Encoger por pasos (4 tramos) → estética pixel
                    return new AnimationCurve(
                        new Keyframe(0f, 1f, 0f, 0f), new Keyframe(0.25f, 1f, 0f, 0f),
                        new Keyframe(0.26f, 0.75f, 0f, 0f), new Keyframe(0.5f, 0.75f, 0f, 0f),
                        new Keyframe(0.51f, 0.5f, 0f, 0f), new Keyframe(0.75f, 0.5f, 0f, 0f),
                        new Keyframe(0.76f, 0.25f, 0f, 0f), new Keyframe(1f, 0.25f, 0f, 0f));
            }
        }
    }
}
