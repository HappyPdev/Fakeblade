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
            /// <summary>Tumbado en el suelo (anillo de la carga).</summary>
            public bool Flat;
            /// <summary>Hoja de CodeManu (nombre del archivo sin .png) en vez de una de Wallcoeur.</summary>
            public string CodeManu;
            /// <summary>Textura generada aquí en vez de una del pack (Quemado: mancha difusa).</summary>
            public bool GeneratedScorch;
            /// <summary>Textura generada: nube de vaho con jirones (Hielo).</summary>
            public bool GeneratedMist;
            /// <summary>Textura generada: un punto pixel (ceniza).</summary>
            public bool GeneratedDot;
            /// <summary>Textura recortada: el pentágono de la segunda casilla de Impact09 (Wallcoeur), en pixel art (ceniza).</summary>
            public bool CutShard;
            public float FadeIn, FadeOut, Gravity, Drag, Spin, ShrinkOut;
            public int Max;
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
            // La misma media luna desde su segundo dibujo (el primero es un punto), tumbada en el suelo: anillo al subir de nivel
            // de carga (2 copias, la segunda girada 180°)
            new DefaultSprite { Id = "CircleImpact04Anillo", Texture = "CircleImpact04", TilesX = 3, TilesY = 3, FirstFrame = 2, Frames = 7, Life = 0.35f, Order = 0, Flat = true, DepthTested = true },
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
            // Llama hacia arriba de CodeManu (11_fire, 61 dibujos en 8 × 8): llamas en cono del Fuego activo (K8, 2026-10-17)
            new DefaultSprite { Id = "Llama11", CodeManu = "11_fire_spritesheet", TilesX = 8, TilesY = 8, Frames = 61, Life = 0.6f, Order = 1, Upright = true, DepthTested = true },
            // Mancha difusa de quemado en el suelo (textura generada, blanca para teñirla): rastro del Fuego (K8, 2026-10-17)
            new DefaultSprite { Id = "Quemado", GeneratedScorch = true, TilesX = 1, TilesY = 1, Frames = 1, Life = 4f, Order = -1, Upright = true, DepthTested = true, Flat = true, FadeOut = 0.6f, Max = 600 },
            // Vaho del Hielo (K8, 2026-10-17; textura generada, blanca para teñirla): nube que cae y se arrastra, y la misma
            // tumbada a ras de suelo, que gira y se frena como por la inercia de la peonza y se queda posada unos instantes
            new DefaultSprite { Id = "Vaho", GeneratedMist = true, TilesX = 1, TilesY = 1, Frames = 1, Life = 1.6f, Order = 0, Upright = true, DepthTested = true, FadeIn = 0.25f, FadeOut = 0.6f, Gravity = 0.03f, Drag = 0.3f, Spin = 25f, Max = 256 },
            new DefaultSprite { Id = "VahoSuelo", GeneratedMist = true, TilesX = 1, TilesY = 1, Frames = 1, Life = 2f, Order = -1, Upright = true, DepthTested = true, Flat = true, FadeIn = 0.2f, FadeOut = 0.6f, Drag = 0.45f, Spin = 40f, Max = 256 },
            // Ceniza (Rayos, 2026-10-18): el pentágono de Impact09 en pixel art (blanco para teñirlo), girado al azar, que sube un
            // poco, cae despacio y se encoge hasta desaparecer
            new DefaultSprite { Id = "Ceniza", CutShard = true, TilesX = 1, TilesY = 1, Frames = 1, Life = 1.6f, Order = 0, DepthTested = true, ShrinkOut = 0.7f, Gravity = 0.12f, Drag = 0.5f, Max = 512 },
            // Chispa simple (Rayos, 2026-10-18): el mismo punto pixel, que cae con gravedad (la textura VFX_Spark es una estrella
            // de cuatro puntas, que no gusta)
            new DefaultSprite { Id = "Chispa", GeneratedDot = true, TilesX = 1, TilesY = 1, Frames = 1, Life = 0.45f, Order = 1, Upright = true, DepthTested = true, FadeOut = 0.3f, Gravity = 1f, Drag = 0.15f, Max = 512 },
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
                    frames = d.Frames, life = d.Life, order = d.Order, randomRotation = !d.Upright, depthTested = d.DepthTested, flat = d.Flat,
                    fadeOut = d.FadeOut, fadeIn = d.FadeIn, shrinkOut = d.ShrinkOut, gravity = d.Gravity, drag = d.Drag, spin = d.Spin,
                    maxParticles = d.Max > 0 ? d.Max : 128
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
            if (!string.IsNullOrEmpty(d.CodeManu)) return FakeBladeTrailSetup.CodeManuSheet(d.CodeManu);
            if (d.GeneratedScorch) return ScorchTexture();
            if (d.GeneratedMist) return MistTexture();
            if (d.GeneratedDot) return DotTexture();
            if (d.CutShard) return ShardTexture();
            string source = PackFolder + d.Texture + ".png";
            if (d.Convert == Convert.None) return AssetDatabase.LoadAssetAtPath<Texture2D>(source);

            string name = Path.GetFileName(d.Texture) + (d.Convert == Convert.White ? "_Blanco" : "_Alfa");
            string target = $"{ConvertedFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            if (existing != null) return existing;
            return ConvertTexture(source, target, d.Convert);
        }

        /// <summary>
        /// Mancha difusa de quemado (K8, 2026-10-17): blanca (se tiñe de negro al usarla), con la transparencia de un
        /// círculo que se apaga hacia fuera y bordes irregulares por ruido, en 4 niveles y 48 × 48 con filtro de punto
        /// (pixel art). Siempre la misma (semilla fija); el giro y el tamaño al azar de cada mancha la hacen variar.
        /// </summary>
        private static Texture2D ScorchTexture()
        {
            string target = ConvertedFolder + "/Quemado.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            if (existing != null) return existing;

            const int size = 48;
            var random = new System.Random(17);
            var lattice = new float[9, 9];
            for (int y = 0; y < 9; y++)
            for (int x = 0; x < 9; x++)
                lattice[x, y] = (float)random.NextDouble();
            var lattice2 = new float[17, 17];
            for (int y = 0; y < 17; y++)
            for (int x = 0; x < 17; x++)
                lattice2[x, y] = (float)random.NextDouble();

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f; // 0 en el centro, 1 en el borde
                float noise = Lattice(lattice, u * 8f, v * 8f) * 0.65f + Lattice(lattice2, u * 16f, v * 16f) * 0.35f;
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.95f, d + (noise - 0.5f) * 0.45f));
                a = Mathf.Round(Mathf.Clamp01(a) * 4f) / 4f; // 4 niveles: pixel art
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            FakeBladeAssetsMenu.EnsureFolder(ConvertedFolder);
            File.WriteAllBytes(target, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(target);

            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }

        /// <summary>
        /// Nube de vaho (Hielo, K8, 2026-10-17): blanca (se tiñe al usarla), con la forma redondeada de una nube que se
        /// apaga hacia fuera, bordes deshilachados y jirones por dentro (tres capas de ruido), en 6 niveles de
        /// transparencia y 64 × 64 con filtro de punto (pixel art con más detalle que el humo de 10 × 10). Siempre la
        /// misma (semilla fija); el giro y el tamaño de cada partícula la hacen variar.
        /// </summary>
        private static Texture2D MistTexture()
        {
            string target = ConvertedFolder + "/Vaho.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            if (existing != null) return existing;

            const int size = 64;
            var random = new System.Random(31);
            float[,] coarse = RandomLattice(random, 5), medium = RandomLattice(random, 9), fine = RandomLattice(random, 17);

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f; // 0 en el centro, 1 en el borde
                float shapeNoise = Lattice(coarse, u * 4f, v * 4f) * 0.6f + Lattice(medium, u * 8f, v * 8f) * 0.4f;
                // Forma: nube redonda con el borde deshilachado
                float shape = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.95f, d + (shapeNoise - 0.5f) * 0.7f));
                // Jirones: zonas más densas y huecos por dentro
                float wisps = Lattice(medium, u * 8f + 3.1f, v * 8f + 1.7f) * 0.55f + Lattice(fine, u * 16f, v * 16f) * 0.45f;
                float a = shape * Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0.25f, 0.75f, wisps));
                a = Mathf.Round(Mathf.Clamp01(a) * 6f) / 6f; // 6 niveles: pixel art
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            FakeBladeAssetsMenu.EnsureFolder(ConvertedFolder);
            File.WriteAllBytes(target, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(target);

            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }

        /// <summary>
        /// Pentágono de ceniza (Rayos, 2026-10-18; elegido por el usuario): el dibujo de la segunda casilla de Impact09 (hoja de
        /// 3 × 3 de Wallcoeur, fuera del repo), recortado a su tamaño y reducido a 16 × 16 en pixel art, blanco para teñirlo.
        /// </summary>
        private static Texture2D ShardTexture()
        {
            string target = ConvertedFolder + "/Ceniza_Impact09.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            if (existing != null) return existing;
            string source = PackFolder + "impact_1.1.0/Impact09.png";
            if (!File.Exists(source)) return DotTexture();
            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sheet.LoadImage(File.ReadAllBytes(source));
            int cell = sheet.width / 3;
            // Segunda casilla de la fila de arriba (en la textura, y = 0 es la fila de abajo)
            int x0 = cell, y0 = sheet.height - cell;
            Color32[] all = sheet.GetPixels32();
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = y0; y < y0 + cell; y++)
            for (int x = x0; x < x0 + cell; x++)
            {
                if (all[y * sheet.width + x].a < 80) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            if (maxX < 0)
            {
                Object.DestroyImmediate(sheet);
                return DotTexture();
            }
            // Cuadrado alrededor del dibujo, con un poco de margen
            int side = Mathf.Max(maxX - minX, maxY - minY) + 1;
            int margin = side / 10;
            side += margin * 2;
            int cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
            const int size = 16;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // El píxel de la hoja en el centro de cada casilla de 16 × 16 (pixel art nítido)
                int sx = cx - side / 2 + (int)((x + 0.5f) * side / size);
                int sy = cy - side / 2 + (int)((y + 0.5f) * side / size);
                byte a = sx < 0 || sy < 0 || sx >= sheet.width || sy >= sheet.height ? (byte)0 : all[sy * sheet.width + sx].a;
                pixels[y * size + x] = new Color32(255, 255, 255, a >= 128 ? (byte)255 : (byte)0);
            }
            Object.DestroyImmediate(sheet);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            FakeBladeAssetsMenu.EnsureFolder(ConvertedFolder);
            File.WriteAllBytes(target, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(target);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }

        /// <summary>Punto pixel de 4 × 4, blanco y opaco, con filtro de punto (ceniza; se tiñe al usarlo).</summary>
        private static Texture2D DotTexture()
        {
            string target = ConvertedFolder + "/Punto.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            if (existing != null) return existing;
            const int size = 4;
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            FakeBladeAssetsMenu.EnsureFolder(ConvertedFolder);
            File.WriteAllBytes(target, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(target);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }

        /// <summary>Rejilla de (cells + 1) × (cells + 1) valores al azar para Lattice.</summary>
        private static float[,] RandomLattice(System.Random random, int points)
        {
            var values = new float[points, points];
            for (int y = 0; y < points; y++)
            for (int x = 0; x < points; x++)
                values[x, y] = (float)random.NextDouble();
            return values;
        }

        /// <summary>Ruido de valor: interpolación suave entre los puntos de una rejilla de valores al azar.</summary>
        private static float Lattice(float[,] values, float x, float y)
        {
            int max = values.GetLength(0) - 1;
            int x0 = Mathf.Clamp((int)x, 0, max - 1), y0 = Mathf.Clamp((int)y, 0, max - 1);
            float fx = Mathf.SmoothStep(0f, 1f, x - x0), fy = Mathf.SmoothStep(0f, 1f, y - y0);
            float top = Mathf.Lerp(values[x0, y0 + 1], values[x0 + 1, y0 + 1], fx);
            float bottom = Mathf.Lerp(values[x0, y0], values[x0 + 1, y0], fx);
            return Mathf.Lerp(bottom, top, fy);
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
            // 128 por defecto (también si el sprite es de antes de este campo): las de la punta de varias peonzas a la vez
            main.maxParticles = s.maxParticles > 0 ? s.maxParticles : 128;
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

            // Aparece poco a poco al principio (vaho) y se desvanece al final de la vida (manchas, vaho)
            var fade = ps.colorOverLifetime;
            fade.enabled = s.fadeOut > 0f || s.fadeIn > 0f;
            if (fade.enabled)
            {
                float fadeIn = Mathf.Clamp(s.fadeIn, 0f, 0.95f);
                float fadeOutStart = Mathf.Max(fadeIn + 0.01f, 1f - s.fadeOut);
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[]
                    {
                        new GradientAlphaKey(fadeIn > 0f ? 0f : 1f, 0f), new GradientAlphaKey(1f, Mathf.Max(0.001f, fadeIn)),
                        new GradientAlphaKey(1f, Mathf.Min(0.999f, fadeOutStart)), new GradientAlphaKey(s.fadeOut > 0f ? 0f : 1f, 1f)
                    });
                fade.color = gradient;
            }

            // Se encoge hasta desaparecer al final de la vida (ceniza)
            var shrink = ps.sizeOverLifetime;
            shrink.enabled = s.shrinkOut > 0f;
            if (shrink.enabled)
            {
                float start = Mathf.Clamp(1f - s.shrinkOut, 0f, 0.99f);
                var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(start, 1f), new Keyframe(1f, 0f));
                shrink.size = new ParticleSystem.MinMaxCurve(1f, curve);
            }

            // Giro que se frena hasta la quinta parte al final de la vida, como por inercia (vaho)
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = s.spin != 0f;
            if (rotation.enabled)
                rotation.z = new ParticleSystem.MinMaxCurve(s.spin * Mathf.Deg2Rad, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

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
            renderer.renderMode = s.flat ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
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
