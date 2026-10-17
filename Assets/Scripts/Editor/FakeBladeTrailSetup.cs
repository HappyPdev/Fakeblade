using System.IO;
using UnityEditor;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Rastro tipo cinta de las peonzas (K7, 2026-10-17, GDD 7.6).
    ///
    /// - Material liso VFX_TrailBasic (el color del jugador, sin dibujo): el que se usa. Se pone en
    ///   VfxLibrary.genericTrail si no tiene material.
    /// - Material VFX_TrailGeneric con pixel art: la hoja Part3_Column_2 de los Bullet Trails de BDragon1727, fila 2
    ///   (la blanca). Se probó y se descartó por ahora (tan larga, el dibujo se deformaba); se guarda para los rastros
    ///   de los especiales (K8). Copia esa fila (8 fotogramas de 64×32) en una tira horizontal en
    ///   Assets/VFX/Textures/Trails, con filtro de punto. La hoja original no se toca.
    /// - Cuencos de la punta en el acelerón de ataques y dash (VfxLibrary.burstTip): malla ImpactPlane y material
    ///   VFX_BurstTip con ImpactShotValo2, si no tienen.
    /// - Material de los efectos sobre la peonza (VfxLibrary.bladeOverlay): VFX_BladeOverlay (shader FakeBlade/BladeOverlay).
    ///   Lo demás del rastro lo ajusta el usuario en el Inspector de la biblioteca (se lee en vivo, también en Play).
    /// Lo llama también *Setup VFX*.
    /// </summary>
    public static class FakeBladeTrailSetup
    {
        private const string SourceSheet =
            "Assets/ThirdParty/Free/PixelArt by BDragon1727/Bullets Trails/Basic Bullet Trail Free/Part 3/Part3_Column_2.png";
        private const int CellWidth = 64;
        private const int CellHeight = 32;
        private const int Frames = 8;
        /// <summary>Fila de la hoja, contando desde arriba y desde 0 (la segunda: blanca).</summary>
        private const int Row = 1;

        private const string TextureFolder = "Assets/VFX/Textures/Trails";
        private const string TexturePath = TextureFolder + "/Trail_Part3_Fila2.png";
        private const string MaterialPath = "Assets/VFX/Materials/VFX_TrailGeneric.mat";
        private const string BasicMaterialPath = "Assets/VFX/Materials/VFX_TrailBasic.mat";

        private const string WallcoeurFolder = "Assets/ThirdParty/VFX/VFXPACK_IMPACT_WALLCOEUR_FreeVersion/";
        private const string TipModelPath = WallcoeurFolder + "04_Model/ImpactPlane.fbx";
        private const string TipTexturePath = WallcoeurFolder + "03_Texture/ImpactShotValo2.png";
        private const string TipMaterialPath = "Assets/VFX/Materials/VFX_BurstTip.mat";
        private const string TipShaderName = "FakeBlade/FlipbookUnlit";
        private const string OverlayMaterialPath = "Assets/VFX/Materials/VFX_BladeOverlay.mat";
        private const string OverlayShaderName = "FakeBlade/BladeOverlay";
        private const string ShaderName = "FakeBlade/RibbonTrail";
        private const string LibraryPath = "Assets/Resources/VfxLibrary.asset";

        [MenuItem("FakeBlade/Setup Rastros")]
        public static void RunFromMenu()
        {
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            Debug.Log(library == null ? "[Rastros] Falta la VfxLibrary: usa antes Setup VFX." : "[Rastros] " + Setup(library));
        }

        /// <summary>
        /// Crea los materiales que falten: el liso (el que se usa, 2026-10-17) y el de pixel art (descartado por ahora,
        /// se guarda para los rastros de los especiales, K8). Devuelve un resumen.
        /// </summary>
        public static string Setup(VfxLibrary library)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) return "rastro: falta el shader " + ShaderName;

            // Liso: sin textura (blanco), teñido con el color del jugador
            Material basic = GetMaterial(BasicMaterialPath, shader);
            basic.SetTexture("_MainTex", null);
            basic.SetFloat("_Frames", 1f);
            basic.SetFloat("_Flip", 0f);
            EditorUtility.SetDirty(basic);

            Texture2D strip = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (strip == null) strip = CreateStrip();
            if (strip != null)
            {
                Material pixelArt = GetMaterial(MaterialPath, shader);
                pixelArt.SetTexture("_MainTex", strip);
                pixelArt.SetFloat("_Frames", Frames);
                EditorUtility.SetDirty(pixelArt);
            }

            library.genericTrail ??= new RibbonTrailStyle();
            if (library.genericTrail.material == null)
            {
                library.genericTrail.material = basic;
                library.genericTrail.frames = 1;
            }

            string tip = SetupBurstTip(library) + "; " + SetupBladeOverlay(library);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return "rastro genérico: " + AssetDatabase.GetAssetPath(library.genericTrail.material) +
                   (strip == null ? " (no se encuentra " + SourceSheet + ")" : "") + "; " + tip;
        }

        /// <summary>
        /// Cuencos de la punta en el acelerón (2026-10-17): modelo ImpactPlane y hoja ImpactShotValo2 de Wallcoeur
        /// (3×3, blanca para teñirla), con el shader FakeBlade/FlipbookUnlit (dos caras: el modelo mira hacia dentro).
        /// </summary>
        private static string SetupBurstTip(VfxLibrary library)
        {
            Shader shader = Shader.Find(TipShaderName);
            if (shader == null) return "punta: falta el shader " + TipShaderName;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TipTexturePath);
            Mesh mesh = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(TipModelPath))
                if (asset is Mesh m) mesh = m;
            if (texture == null || mesh == null) return "punta: no se encuentra " + (texture == null ? TipTexturePath : TipModelPath);

            Material material = GetMaterial(TipMaterialPath, shader);
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_TilesX", 3f);
            material.SetFloat("_TilesY", 3f);
            EditorUtility.SetDirty(material);

            library.burstTip ??= new BurstTipStyle();
            if (library.burstTip.mesh == null) library.burstTip.mesh = mesh;
            if (library.burstTip.material == null) library.burstTip.material = material;
            return "punta: " + TipMaterialPath;
        }

        /// <summary>Material de los efectos sobre la peonza (borde brillante y destello; BladeOverlay).</summary>
        private static string SetupBladeOverlay(VfxLibrary library)
        {
            Shader shader = Shader.Find(OverlayShaderName);
            if (shader == null) return "peonza: falta el shader " + OverlayShaderName;
            Material material = GetMaterial(OverlayMaterialPath, shader);
            EditorUtility.SetDirty(material);
            library.bladeOverlay ??= new BladeOverlayStyle();
            if (library.bladeOverlay.material == null) library.bladeOverlay.material = material;
            // Anillos de pinchos (carga, ataque rápido y golpe recibido): Zbam, con transparencia propia
            if (library.bladeOverlay.ringTexture == null)
                library.bladeOverlay.ringTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WallcoeurFolder + "03_Texture/Zbam.png");
            return "peonza: " + OverlayMaterialPath + (library.bladeOverlay.ringTexture == null ? " (sin textura Zbam)" : "");
        }

        private static Material GetMaterial(string path, Shader shader)
        {
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
            return material;
        }

        /// <summary>Copia la fila elegida de la hoja en una tira horizontal (sin tocar la hoja original).</summary>
        private static Texture2D CreateStrip()
        {
            if (!File.Exists(SourceSheet)) return null;
            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sheet.LoadImage(File.ReadAllBytes(SourceSheet));

            // En la textura, y = 0 es la fila de abajo
            int y = sheet.height - (Row + 1) * CellHeight;
            Color32[] all = sheet.GetPixels32();
            var pixels = new Color32[CellWidth * Frames * CellHeight];
            for (int row = 0; row < CellHeight; row++)
            for (int x = 0; x < CellWidth * Frames; x++)
                pixels[row * CellWidth * Frames + x] = all[(y + row) * sheet.width + x];
            Object.DestroyImmediate(sheet);

            var strip = new Texture2D(CellWidth * Frames, CellHeight, TextureFormat.RGBA32, false);
            strip.SetPixels32(pixels);
            FakeBladeAssetsMenu.EnsureFolder(TextureFolder);
            File.WriteAllBytes(TexturePath, strip.EncodeToPNG());
            Object.DestroyImmediate(strip);
            AssetDatabase.ImportAsset(TexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }
    }
}
