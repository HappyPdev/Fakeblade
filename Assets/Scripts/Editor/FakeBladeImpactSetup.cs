using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Impactos con sprites animados (2026-10-17, GDD 7.6), del pack de Wallcoeur. Cada sprite (VfxLibrary.impactSprites)
    /// tiene su prefab de partículas con la hoja de fotogramas animada de principio a fin; las capas de cada tipo de
    /// impacto los combinan (VfxLibrary.hitLayers...).
    ///
    /// - Si la lista de sprites está vacía, la llena con los de por defecto; si las de capas están vacías, también.
    ///   Lo que el usuario cambie en el Inspector se respeta al volver a ejecutarlo.
    /// - Rehace el material y el prefab de cada sprite (después de cambiar su textura, rejilla, duración u orden).
    /// - Las texturas que no traen transparencia o no son blancas se copian adaptadas en Assets/VFX/Textures/Impacts.
    /// Lo llama también *Setup VFX*.
    /// </summary>
    public static class FakeBladeImpactSetup
    {
        private const string PackFolder = "Assets/ThirdParty/VFX/VFXPACK_IMPACT_WALLCOEUR_FreeVersion/03_Texture/";
        private const string ConvertedFolder = "Assets/VFX/Textures/Impacts";
        private const string MaterialFolder = "Assets/VFX/Materials";
        private const string PrefabFolder = "Assets/VFX/Prefabs";
        private const string LibraryPath = "Assets/Resources/VfxLibrary.asset";

        private enum Convert { None, White, LumaToAlpha }

        private struct DefaultSprite
        {
            public string Id, Texture;
            public Convert Convert;
            public int TilesX, TilesY, FirstFrame, Frames, Order;
            public float Life;
            /// <summary>Siempre derecho (sin giro al azar) y tapado por lo que tenga delante (partículas que deja la peonza).</summary>
            public bool Upright, DepthTested;
        }

        // Orden: sombras y fondos 0, capas sutiles 1, impacto principal 2, capas por delante del principal 3
        private static readonly DefaultSprite[] Defaults =
        {
            new DefaultSprite { Id = "Impact01", Texture = "impact_1.1.0/Impact01", TilesX = 3, TilesY = 3, Frames = 7, Life = 0.28f, Order = 2 },
            new DefaultSprite { Id = "Impact01Sombra", Texture = "impact_1.1.0/Impact01", TilesX = 3, TilesY = 3, Frames = 7, Life = 0.28f, Order = 0 },
            new DefaultSprite { Id = "Impact10", Texture = "impact_1.1.0/Impact10", TilesX = 3, TilesY = 3, Frames = 7, Life = 0.34f, Order = 2 },
            new DefaultSprite { Id = "Impact10Sombra", Texture = "impact_1.1.0/Impact10", TilesX = 3, TilesY = 3, Frames = 7, Life = 0.34f, Order = 0 },
            // El mismo Impact10, por delante (más claro, más pequeño y girado 90°: capas del cargado)
            new DefaultSprite { Id = "Impact10Encima", Texture = "impact_1.1.0/Impact10", TilesX = 3, TilesY = 3, Frames = 7, Life = 0.34f, Order = 3 },
            // Bloqueo: cruz rápida con un círculo detrás (2026-10-17)
            new DefaultSprite { Id = "CrossShoot", Texture = "CrossShoot", TilesX = 3, TilesY = 3, Frames = 9, Life = 0.22f, Order = 2 },
            new DefaultSprite { Id = "CircleImpact04", Texture = "CircleImpact04", TilesX = 3, TilesY = 3, FirstFrame = 1, Frames = 8, Life = 0.3f, Order = 0 },
            // Parry: anillos que se abren detrás, con el color de quien para
            new DefaultSprite { Id = "CircleImpact02", Texture = "CircleImpact02", TilesX = 3, TilesY = 3, Frames = 9, Life = 0.34f, Order = 0 },
            new DefaultSprite { Id = "Impact05", Texture = "impact_1.1.0/Impact05", TilesX = 3, TilesY = 3, Frames = 8, Life = 0.34f, Order = 2 },
            new DefaultSprite { Id = "Impact06", Texture = "impact_1.1.0/Impact06", TilesX = 3, TilesY = 2, Frames = 5, Life = 0.3f, Order = 2 },
            new DefaultSprite { Id = "Impact04", Texture = "impact_1.1.0/Impact04", TilesX = 3, TilesY = 3, Frames = 7, Life = 0.3f, Order = 1 },
            new DefaultSprite { Id = "Impact07", Texture = "impact_1.1.0/Impact07", TilesX = 3, TilesY = 3, Frames = 9, Life = 0.32f, Order = 1 },
            new DefaultSprite { Id = "Impact08", Texture = "impact_1.1.0/Impact08", TilesX = 3, TilesY = 3, Frames = 8, Life = 0.3f, Order = 1 },
            new DefaultSprite { Id = "ShotValo", Texture = "ImpactShotValo", TilesX = 3, TilesY = 3, Frames = 9, Life = 0.3f, Order = 1 },
            new DefaultSprite { Id = "ShotValo2", Texture = "ImpactShotValo2", TilesX = 3, TilesY = 3, Frames = 9, Life = 0.3f, Order = 1 },
            // Anillo naranja: copia en blanco para teñirlo (parry)
            new DefaultSprite { Id = "Valo5Blanco", Texture = "ImpactValo5", Convert = Convert.White, TilesX = 3, TilesY = 3, Frames = 9, Life = 0.34f, Order = 1 },
            // Fondo negro opaco: el brillo pasa a transparencia (choques pequeños)
            new DefaultSprite { Id = "MuzzleNeo", Texture = "MuzzleCanonNeo", Convert = Convert.LumaToAlpha, TilesX = 2, TilesY = 2, Frames = 4, Life = 0.16f, Order = 2 },
            // Llama hacia arriba: partículas de la punta en el acelerón de ataques y dash (K7, 2026-10-17)
            new DefaultSprite { Id = "Valo4", Texture = "ImpactValo4", TilesX = 3, TilesY = 3, Frames = 8, Life = 0.4f, Order = 1, Upright = true, DepthTested = true },
            // Tajo hacia arriba (ImpatcValo8, con la errata del pack; ImpactValo8 es otro, un rombo): partículas de la punta
            new DefaultSprite { Id = "Valo8", Texture = "ImpatcValo8", TilesX = 3, TilesY = 3, Frames = 9, Life = 0.4f, Order = 1, Upright = true, DepthTested = true },
        };

        [MenuItem("FakeBlade/Setup Impactos (sprites)")]
        public static void RunFromMenu()
        {
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            Debug.Log(library == null ? "[Impactos] Falta la VfxLibrary: usa antes Setup VFX." : "[Impactos] " + Setup(library));
        }

        /// <summary>Crea o rehace los impactos. Devuelve un resumen.</summary>
        public static string Setup(VfxLibrary library)
        {
            var log = new StringBuilder();
            RemoveOldEntries(library, log);

            // Los sprites por defecto que falten (por id); los que ya están, como los haya dejado el usuario
            int added = 0;
            foreach (DefaultSprite d in Defaults)
            {
                if (library.GetImpactSprite(d.Id) != null) continue;
                library.impactSprites.Add(new ImpactSprite
                {
                    id = d.Id, texture = GetTexture(d), tilesX = d.TilesX, tilesY = d.TilesY, firstFrame = d.FirstFrame,
                    frames = d.Frames, life = d.Life, order = d.Order, randomRotation = !d.Upright, depthTested = d.DepthTested
                });
                added++;
            }
            if (added > 0) log.Append(added).Append(" sprites nuevos; ");

            int built = 0;
            foreach (ImpactSprite sprite in library.impactSprites)
            {
                if (sprite == null || string.IsNullOrEmpty(sprite.id)) continue;
                if (sprite.texture == null)
                {
                    log.Append(sprite.id).Append(": sin textura; ");
                    continue;
                }
                sprite.prefab = CreateEffect(sprite, GetMaterial(sprite.texture));
                built++;
            }
            log.Append(built).Append(" sprites; ");

            if (library.smallHitLayers.Count + library.hitLayers.Count + library.chargedLayers.Count +
                library.parryLayers.Count + library.blockLayers.Count == 0)
            {
                AddDefaultLayers(library);
                log.Append("capas por defecto");
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        /// <summary>
        /// Capas por defecto (2026-10-17, ajustadas tras verlas el usuario): golpe con sombra oscura; cargado con el
        /// mismo Impact10 por delante, más claro, más pequeño y girado 90°; parry y bloqueo blancos con un fondo circular
        /// del color de quien para.
        /// </summary>
        public static void AddDefaultLayers(VfxLibrary lib)
        {
            Color shadow = new Color(0f, 0f, 0f, 0.45f);
            lib.smallHitLayers.Clear();
            lib.hitLayers.Clear();
            lib.chargedLayers.Clear();
            lib.parryLayers.Clear();
            lib.blockLayers.Clear();
            lib.smallHitLayers.Add(Layer("MuzzleNeo", 0.8f));
            lib.hitLayers.Add(Layer("Impact01Sombra", 1.08f, shadow));
            lib.hitLayers.Add(Layer("Impact01", 1f));
            lib.hitLayers.Add(Layer("Impact04", 1.15f, alpha: 0.6f));
            lib.chargedLayers.Add(Layer("Impact10Sombra", 1.08f, shadow));
            lib.chargedLayers.Add(Layer("Impact10", 1f));
            lib.chargedLayers.Add(Layer("ShotValo", 0.9f, alpha: 0.85f));
            ImpactLayer over = Layer("Impact10Encima", 0.7f, alpha: 0.6f);
            over.lighten = 0.5f;
            over.rotation = 90f;
            lib.chargedLayers.Add(over);
            lib.parryLayers.Add(PlayerLayer("CircleImpact02", 1.3f, 0.9f));
            lib.parryLayers.Add(Layer("Valo5Blanco", 1.15f, Color.white));
            lib.parryLayers.Add(Layer("Impact05", 1f, Color.white));
            lib.blockLayers.Add(PlayerLayer("CircleImpact04", 1.25f, 0.9f));
            lib.blockLayers.Add(Layer("CrossShoot", 1f, Color.white));
        }

        private static ImpactLayer PlayerLayer(string sprite, float size, float alpha) =>
            new ImpactLayer { sprite = sprite, size = size, damageColor = false, playerColor = true, color = new Color(1f, 1f, 1f, alpha) };

        /// <summary>Capa con el color del daño (alpha = transparencia) o con un color fijo.</summary>
        private static ImpactLayer Layer(string sprite, float size, Color? fixedColor = null, float alpha = 1f) =>
            new ImpactLayer
            {
                sprite = sprite, size = size, damageColor = fixedColor == null,
                color = fixedColor ?? new Color(1f, 1f, 1f, alpha)
            };

        /// <summary>El primer montaje usaba cuatro tipos de VfxType (21-24): se quitan sus entradas y prefabs.</summary>
        private static void RemoveOldEntries(VfxLibrary library, StringBuilder log)
        {
            int removed = library.entries.RemoveAll(e => e != null && (int)e.type > (int)VfxType.Sparkle);
            foreach (string old in new[] { "ImpactHit", "ImpactCharged", "ImpactParry", "ImpactBlock" })
            {
                string path = $"{PrefabFolder}/VFX_{old}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);
            }
            if (removed > 0) log.Append("quitadas ").Append(removed).Append(" entradas antiguas; ");
        }

        #region Texturas
        private static Texture2D GetTexture(DefaultSprite d)
        {
            string source = PackFolder + d.Texture + ".png";
            if (d.Convert == Convert.None) return AssetDatabase.LoadAssetAtPath<Texture2D>(source);

            string name = Path.GetFileName(d.Texture) + (d.Convert == Convert.White ? "_Blanco" : "_Alfa");
            string target = $"{ConvertedFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            if (existing != null) return existing;
            return ConvertTexture(source, target, d.Convert);
        }

        /// <summary>Copia adaptada: en blanco (conservando la transparencia) o con el brillo como transparencia.</summary>
        private static Texture2D ConvertTexture(string source, string target, Convert mode)
        {
            if (!File.Exists(source)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(source));
            Color32[] pixels = tex.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                byte alpha = mode == Convert.LumaToAlpha ? (byte)Mathf.Max(c.r, Mathf.Max(c.g, c.b)) : c.a;
                pixels[i] = new Color32(255, 255, 255, alpha);
            }
            tex.SetPixels32(pixels);

            FakeBladeAssetsMenu.EnsureFolder(ConvertedFolder);
            File.WriteAllBytes(target, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(target);

            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }
        #endregion

        #region Material y prefab
        private static Material GetMaterial(Texture2D texture)
        {
            string path = $"{MaterialFolder}/VFX_{texture.name}.mat";
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

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Transparent);
            material.SetFloat("_Blend", (float)BaseShaderGUI.BlendMode.Alpha);
            material.SetFloat("_ColorMode", 0f); // Multiply: el color de la capa tiñe el sprite blanco
            material.SetFloat("_SoftParticlesEnabled", 0f);
            material.SetFloat("_CameraFadingEnabled", 0f);
            material.SetFloat("_DistortionEnabled", 0f);
            material.SetFloat("_FlipbookBlending", 0f);
            BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ParticleSystem CreateEffect(ImpactSprite s, Material material)
        {
            var go = new GameObject("VFX_Impact_" + s.id);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = s.life;
            main.startSpeed = 0f;
            main.startSize = 1.5f;
            main.startColor = Color.white;
            main.gravityModifier = s.gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 128; // también las partículas de la punta de varias peonzas a la vez
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;

            // Frenado (partículas que salen con velocidad, como las de la punta en el acelerón)
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = s.drag > 0f;
            limit.limit = 1000f;
            limit.drag = s.drag * 10f;

            // La hoja entera, una vez, de principio a fin de la vida de la partícula
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = s.tilesX;
            sheet.numTilesY = s.tilesY;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.cycleCount = 1;
            int tiles = s.tilesX * s.tilesY;
            float first = Mathf.Clamp(s.firstFrame, 0, tiles - 1) / (float)tiles;
            float last = (Mathf.Min(s.firstFrame + s.frames, tiles) - 0.01f) / tiles;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, first, 1f, last));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 1f;
            renderer.sortingOrder = s.order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            string path = $"{PrefabFolder}/VFX_Impact_{s.id}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ParticleSystem>();
        }
        #endregion
    }
}
