using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
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
    /// - Efectos de los especiales activos (VfxLibrary.specialEffects, K8/K9): el Fuego, con hojas de CodeManu copiadas
    ///   a Assets/VFX/Textures/CodeManu.
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

        // Hojas de CodeManu (dominio público, casillas de 100 × 100): la carpeta del pack está fuera del repo, así que
        // las que se usan se copian a la nuestra
        private const string CodeManuFolder = "Assets/ThirdParty/Free/PixelArt by CodeManu/Free Pixel Effects Pack/";
        private const string CodeManuCopyFolder = "Assets/VFX/Textures/CodeManu";
        private const string FireCupMaterialPath = "Assets/VFX/Materials/VFX_FireTipCup.mat";
        private const string FireGroundMaterialPath = "Assets/VFX/Materials/VFX_FireGround.mat";
        private const string BurnGroundMaterialPath = "Assets/VFX/Materials/VFX_BurnGround.mat";
        // Hielo: cuencos con el sprite 06 de BDragon (la hoja, fuera del repo, se copia a la nuestra)
        private const string IceCupSource = "Assets/ThirdParty/Free/PixelArt by BDragon1727/Super Package Retro Pixel Effects 32x32 pack 2 Free/06.png";
        private const string BDragonCopyFolder = "Assets/VFX/Textures/BDragon";
        private const string IceCupTexturePath = BDragonCopyFolder + "/RetroPack2_06.png";
        private const string IceCupMaterialPath = "Assets/VFX/Materials/VFX_IceTipCup.mat";
        private const string ArcMaterialPath = "Assets/VFX/Materials/VFX_Arc.mat";
        // Onda de choque: cúpula de la carga (shader propio) y marca del aviso de usos (2026-10-18)
        private const string ChargeDomeMaterialPath = "Assets/VFX/Materials/VFX_ChargeDome.mat";
        private const string ChargeDomeShaderName = "FakeBlade/ChargeDome";
        private const string UseMarkMaterialPath = "Assets/VFX/Materials/VFX_UseMark.mat";
        // Status and Auras FREE (Matthew Guz, Asset Store: fuera del repo, en ThirdParty). Sus materiales son del render
        // antiguo: los pasa a URP ConvertBuiltInParticles
        private const string AuraPackFolder = "Assets/ThirdParty/VFX/Status and Auras FREE";
        private const string AuraBurnPrefabPath = AuraPackFolder + "/Prefab/Aura Burn.prefab";
        private const string AuraSmokePrefabPath = AuraPackFolder + "/Prefab/Aura Smoke.prefab";
        private const string AuraLightningPrefabPath = AuraPackFolder + "/Prefab/Aura Lightning.prefab";

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

            string tip = SetupBurstTip(library) + "; " + SetupBladeOverlay(library) + "; " + SetupSpecialEffects(library);
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

        /// <summary>
        /// Efectos de los especiales mientras están activos (K8/K9, 2026-10-17). Hoy, el Fuego (idea del usuario): aro de
        /// fuego en el suelo (16_sunburn), cuencos con la parte del aro de 6_flamelash, chispas que crepitan y ceniza,
        /// manchas de quemado en el suelo (sprite Quemado de Setup Impactos) e impulsos naranjas del borde con 7_firespin
        /// girando sobre la peonza. Solo añade la entrada si falta y rellena lo que esté vacío: lo demás lo ajusta el
        /// usuario en el Inspector.
        /// </summary>
        private static string SetupSpecialEffects(VfxLibrary library)
        {
            Shader shader = Shader.Find(TipShaderName);
            if (shader == null) return "especiales: falta el shader " + TipShaderName;
            Texture2D flamelash = CodeManuSheet("6_flamelash_spritesheet");
            Texture2D sunburn = CodeManuSheet("16_sunburn_spritesheet");
            Texture2D firespin = CodeManuSheet("7_firespin_spritesheet");
            if (flamelash == null || sunburn == null || firespin == null)
                return "especiales: no se encuentran las hojas de CodeManu en " + CodeManuFolder;

            Material cup = GetMaterial(FireCupMaterialPath, shader);
            cup.SetTexture("_MainTex", flamelash);
            cup.SetFloat("_TilesX", 7f);
            cup.SetFloat("_TilesY", 7f);
            EditorUtility.SetDirty(cup);
            Material ground = GetMaterial(FireGroundMaterialPath, shader);
            ground.SetTexture("_MainTex", sunburn);
            ground.SetFloat("_TilesX", 8f);
            ground.SetFloat("_TilesY", 8f);
            EditorUtility.SetDirty(ground);

            SpecialVfxStyle fire = library.GetSpecialEffects(SpecialAbilityType.Fire);
            bool added = fire == null;
            if (added)
            {
                fire = DefaultFire();
                library.specialEffects.Add(fire);
            }
            fire.tip ??= new BurstTipStyle();
            fire.tip.ground ??= new TipGroundSprite();
            fire.overlay ??= new SpecialOverlayStyle();
            if (fire.tip.mesh == null) fire.tip.mesh = library.burstTip != null ? library.burstTip.mesh : null;
            if (fire.tip.material == null) fire.tip.material = cup;
            if (fire.tip.ground.material == null) fire.tip.ground.material = ground;
            if (fire.overlay.texture == null) fire.overlay.texture = firespin;
            // Chispas que crepitan y ceniza en vez de las llamas en cono (al usuario no le gustaron, 2026-10-17)
            fire.particles ??= new List<SpecialParticleStyle>();
            if (fire.particles.Count == 0)
            {
                fire.particles.AddRange(FireParticles());
                fire.tip.particleSprite = "";
            }
            fire.scorch ??= new ScorchStyle();
            if (string.IsNullOrEmpty(fire.scorch.sprite)) fire.scorch.sprite = "Quemado";
            // Remolino al activarlo, explosión en espiral al encenderse y Aura Smoke mientras dura (2026-10-17)
            fire.windup ??= new List<SpiralParticleStyle>();
            if (fire.windup.Count == 0) fire.windup.AddRange(FireWindup());
            fire.activationBurst ??= new List<SpiralParticleStyle>();
            if (fire.activationBurst.Count == 0) fire.activationBurst.AddRange(FireActivationBurst());
            // Carga y encendido más llamativos, como Tornado y Onda de choque (2026-10-19): se añaden si faltan, sin tocar lo demás
            AddMissing(fire.windup, FireWindupInward(), s => s.name);
            AddMissing(fire.activationBurst, FireBurstOutward(), s => s.name);
            // Estela en la golpeada (2026-10-19)
            fire.hitTrail ??= new List<SpecialParticleStyle>();
            if (fire.hitTrail.Count == 0) fire.hitTrail.AddRange(FireHitTrail());
            // Círculo de llamas, chispas y ceniza en cada choque (2026-10-17)
            fire.impact ??= new List<SpiralParticleStyle>();
            if (fire.impact.Count == 0) fire.impact.AddRange(FireImpact());
            fire.auras ??= new List<AuraStyle>();
            ParticleSystem smoke = AuraPrefab(AuraSmokePrefabPath);
            if (fire.auras.Count == 0 && smoke != null) fire.auras.Add(new AuraStyle { name = "Aura Smoke", prefab = smoke });
            return "especiales: Fuego" + (added ? " (nuevo)" : "") + "; " + SetupBurn(library, shader, firespin) + "; " +
                   SetupIce(library, shader) + "; " + SetupLightning(library) + "; " + SetupShockWave(library, shader) + "; " +
                   SetupTornado(library) + "; " + SetupRam(library) + "; " + SetupLeap(library) + "; " + SetupBounce(library) + "; " +
                   SetupDefense(library) + "; " + SetupWaterTrail(library) + "; " + SetupWet(library) + "; " +
                   SetupSpinBoost(library) + "; " + SetupGhost(library) + "; " + SetupGenericSpecial(library);
        }

        /// <summary>
        /// Peonza quemada (2026-10-17): como la que quema, más simple. Aro de fuego fino en el suelo (7_firespin, sin
        /// cuencos ni partículas en cono), humo, chispas y ceniza, y el Aura Burn de Status and Auras FREE si está en el
        /// proyecto. Solo rellena lo que esté vacío.
        /// </summary>
        private static string SetupBurn(VfxLibrary library, Shader shader, Texture2D firespin)
        {
            library.burn ??= new StatusVfxStyle();
            StatusVfxStyle burn = library.burn;
            burn.tip ??= new BurstTipStyle();
            burn.tip.ground ??= new TipGroundSprite();

            Material ground = GetMaterial(BurnGroundMaterialPath, shader);
            ground.SetTexture("_MainTex", firespin);
            ground.SetFloat("_TilesX", 8f);
            ground.SetFloat("_TilesY", 8f);
            EditorUtility.SetDirty(ground);
            if (burn.tip.ground.material == null)
            {
                // Primera vez: solo el aro (sin cuencos ni el cono de la punta del acelerón)
                burn.tip.ground = new TipGroundSprite
                {
                    material = ground, tilesX = 8, tilesY = 8, frames = 61, cycle = 0.8f, size = 2.2f, height = 0.02f,
                    spinSpeed = 90f, alpha = 0.9f
                };
                burn.tip.cups.Clear();
                burn.tip.particleSprite = "";
                burn.tip.fade = new Vector2(0.1f, 0.3f);
            }

            burn.particles ??= new List<SpecialParticleStyle>();
            if (burn.particles.Find(p => p != null && p.name == "Humo") == null) burn.particles.Add(BurnSmoke());
            burn.impact ??= new List<SpiralParticleStyle>();
            if (burn.impact.Count == 0) burn.impact.AddRange(BurnImpact());
            burn.swirl ??= new List<SpiralParticleStyle>();
            if (burn.swirl.Count == 0) burn.swirl.AddRange(BurnSwirl());

            // Aura Burn más rápida y girando (2026-10-17) y Aura Smoke
            burn.auras ??= new List<AuraStyle>();
            ParticleSystem auraBurn = AuraPrefab(AuraBurnPrefabPath);
            ParticleSystem auraSmoke = AuraPrefab(AuraSmokePrefabPath);
            if (burn.auras.Count == 0 && auraBurn != null)
            {
                burn.auras.Add(new AuraStyle { name = "Aura Burn", prefab = auraBurn, simulationSpeed = 1.8f, spin = 150f });
                if (auraSmoke != null) burn.auras.Add(new AuraStyle { name = "Aura Smoke", prefab = auraSmoke });
            }
            return "quemadura" + (auraBurn != null ? " con auras" : " (sin Status and Auras FREE en ThirdParty/VFX)");
        }

        /// <summary>
        /// Hielo (2026-10-17, ideas del usuario): en la punta, cuencos con el sprite 06 de BDragon (Super Package Retro
        /// Pixel Effects 32x32 pack 2: un cristal de hielo que crece, estalla y se deshace), como los del Fuego (en el suelo
        /// no gustaron 12_nebula, 19_freezing ni los proyectiles de Bullet 24x24); borde cian con destellos cortos,
        /// cristales que brillan y vaho que cae y se arrastra (sprite Vaho de Setup Impactos), vaho a ras de suelo que gira
        /// y se queda posado (VahoSuelo), manchas de escarcha sutiles, remolino de cristales y vaho al activarlo que se
        /// traga hacia dentro, explosión compacta de esquirlas y círculo de esquirlas, destellos y vaho al chocar. Y la
        /// peonza congelada (library.frozen), sin nada en el suelo. Solo añade la entrada si falta y rellena lo que esté
        /// vacío.
        /// </summary>
        private static string SetupIce(VfxLibrary library, Shader shader)
        {
            Texture2D sheet = PackSheet(IceCupSource, IceCupTexturePath);
            if (sheet == null) return "Hielo: no se encuentra " + IceCupSource;
            Material cup = GetMaterial(IceCupMaterialPath, shader);
            cup.SetTexture("_MainTex", sheet);
            cup.SetFloat("_TilesX", 6f);
            cup.SetFloat("_TilesY", 2f);
            EditorUtility.SetDirty(cup);

            SpecialVfxStyle ice = library.GetSpecialEffects(SpecialAbilityType.Ice);
            bool added = ice == null;
            if (added)
            {
                ice = DefaultIce();
                library.specialEffects.Add(ice);
            }
            ice.tip ??= new BurstTipStyle();
            if (ice.tip.cups.Count == 0) SetIceCups(ice.tip);
            if (ice.tip.mesh == null) ice.tip.mesh = library.burstTip != null ? library.burstTip.mesh : null;
            if (ice.tip.material == null) ice.tip.material = cup;
            ice.particles ??= new List<SpecialParticleStyle>();
            if (ice.particles.Count == 0) ice.particles.AddRange(IceParticles());
            ice.scorch ??= new ScorchStyle();
            if (string.IsNullOrEmpty(ice.scorch.sprite)) ice.scorch = IceFrost();
            ice.windup ??= new List<SpiralParticleStyle>();
            if (ice.windup.Count == 0) ice.windup.AddRange(IceWindup());
            ice.activationBurst ??= new List<SpiralParticleStyle>();
            if (ice.activationBurst.Count == 0) ice.activationBurst.AddRange(IceActivationBurst());
            AddMissing(ice.windup, IceWindupInward(), s => s.name);
            AddMissing(ice.activationBurst, IceBurstOutward(), s => s.name);
            ice.hitTrail ??= new List<SpecialParticleStyle>();
            if (ice.hitTrail.Count == 0) ice.hitTrail.AddRange(IceHitTrail());
            ice.swirl ??= new List<SpiralParticleStyle>();
            if (ice.swirl.Count == 0) ice.swirl.AddRange(IceGroundMist());
            ice.impact ??= new List<SpiralParticleStyle>();
            if (ice.impact.Count == 0) ice.impact.AddRange(IceImpact());

            library.frozen ??= new StatusVfxStyle();
            StatusVfxStyle frozen = library.frozen;
            frozen.tip ??= new BurstTipStyle();
            frozen.swirl ??= new List<SpiralParticleStyle>();
            if (frozen.swirl.Count == 0)
            {
                // Primera vez: nada en el suelo (eso solo para la peonza con el especial), escarcha quieta y disco lento
                frozen.tip.ground = new TipGroundSprite();
                frozen.tip.cups.Clear();
                frozen.tip.particleSprite = "";
                frozen.overlay = FrozenOverlay();
                frozen.discSpin = 0.5f;
                frozen.swirl.Add(FrozenCrystals());
            }
            frozen.particles ??= new List<SpecialParticleStyle>();
            if (frozen.particles.Count == 0) frozen.particles.Add(FrozenMist());
            frozen.impact ??= new List<SpiralParticleStyle>();
            if (frozen.impact.Count == 0) frozen.impact.AddRange(FrozenImpact());
            return "Hielo" + (added ? " (nuevo)" : "") + " y congelada";
        }

        /// <summary>
        /// Cuencos del Hielo: el sprite 06 entero (10 dibujos en 6 × 2) en bucle, en tres cuencos con su giro, su velocidad
        /// y su escala; BladeBurstTip desfasa la animación de cada uno, así que los estallidos se turnan.
        /// </summary>
        private static void SetIceCups(BurstTipStyle tip)
        {
            tip.tilesX = 6;
            tip.tilesY = 2;
            tip.firstFrame = 0;
            tip.frames = 10;
            tip.cycle = 0.7f;
            tip.size = 2f;
            tip.scale = new Vector2(1f, 0.7f);
            tip.alpha = 0.95f;
            tip.lighten = 0f;
            tip.cups.Add(new BurstTipCup { angle = 0f, spinSpeed = 180f, scale = new Vector2(0.8f, 0.9f), playerColor = false, color = Color.white });
            tip.cups.Add(new BurstTipCup { angle = 120f, spinSpeed = -140f, scale = Vector2.one, playerColor = false, color = new Color(0.85f, 0.97f, 1f, 0.9f) });
            tip.cups.Add(new BurstTipCup { angle = 240f, spinSpeed = 110f, scale = new Vector2(0.9f, 1.2f), playerColor = false, color = new Color(1f, 1f, 1f, 0.8f) });
        }

        /// <summary>
        /// Copia de una hoja de un pack de fuera del repo en Assets/VFX/Textures/BDragon, como textura normal (no como
        /// sprites cortados) y con filtro de punto. Si ya está copiada, la de la copia.
        /// </summary>
        private static Texture2D PackSheet(string source, string target)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            if (existing != null) return existing;
            if (!File.Exists(source)) return null;

            FakeBladeAssetsMenu.EnsureFolder(BDragonCopyFolder);
            File.Copy(source, target);
            AssetDatabase.ImportAsset(target);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
        }

        private static readonly Color IceCyan = new Color(0.55f, 0.9f, 1f);
        private static readonly Color IcePale = new Color(0.85f, 0.97f, 1f);

        /// <summary>Valores de partida del Hielo (para ajustarlos jugando con mando).</summary>
        private static SpecialVfxStyle DefaultIce()
        {
            var ice = new SpecialVfxStyle { type = SpecialAbilityType.Ice, replaceAura = true, windupSustain = 0.25f };
            BurstTipStyle tip = ice.tip;
            // Sin partículas en cono: cuencos con el sprite 06 de BDragon (SetIceCups, en SetupIce)
            tip.cups.Clear();
            tip.particleSprite = "";
            tip.fade = new Vector2(0.1f, 0.25f);
            // Borde cian con destellos cortos de brillo de hielo (en vez de los impulsos del fuego)
            ice.overlay = new SpecialOverlayStyle
            {
                color = IceCyan, hotColor = Color.white, baseRim = 0.35f, pulseRim = new Vector2(1.5f, 3f),
                pulseFlash = new Vector2(0.05f, 0.25f), pulseInterval = new Vector2(0.25f, 0.8f), pulseDecay = 0.08f
            };
            ice.particles.AddRange(IceParticles());
            ice.scorch = IceFrost();
            return ice;
        }

        /// <summary>Cristales que brillan alrededor y vaho frío que cae y se arrastra (se lleva parte de la velocidad).</summary>
        private static SpecialParticleStyle[] IceParticles() => new[]
        {
            new SpecialParticleStyle
            {
                name = "Cristales", emitter = VfxType.Sparkle, rate = 14f, burstInterval = new Vector2(0.3f, 0.9f),
                burstCount = new Vector2(2f, 4f), spawnRadius = new Vector2(0.3f, 0.8f), height = new Vector2(0.05f, 0.5f),
                coneAngle = 60f, speed = new Vector2(0.1f, 0.4f), inheritVelocity = 0f, size = new Vector2(0.12f, 0.22f),
                lifetime = new Vector2(0.3f, 0.5f), color = IceCyan, color2 = Color.white, alpha = 1f
            },
            new SpecialParticleStyle
            {
                name = "Vaho que cae", sprite = "Vaho", rate = 7f, spawnRadius = new Vector2(0.2f, 0.5f),
                height = new Vector2(0.3f, 0.6f), coneAngle = 30f, speed = new Vector2(0.2f, 0.5f), inheritVelocity = 0.3f,
                size = new Vector2(0.5f, 0.9f), lifetime = new Vector2(1.2f, 1.8f), color = IcePale, color2 = Color.white, alpha = 0.45f
            }
        };

        /// <summary>Manchas de escarcha: la del quemado, en cian claro, sutiles y algo más cortas.</summary>
        private static ScorchStyle IceFrost() => new ScorchStyle
        {
            sprite = "Quemado", spacing = 0.5f, idleInterval = 0.6f, size = new Vector2(0.9f, 1.3f), lifetime = 3f,
            color = new Color(0.75f, 0.95f, 1f), alpha = 0.22f, height = 0.012f
        };

        /// <summary>Al activarlo: remolino de cristales y vaho que gira y se traga hacia dentro.</summary>
        private static SpiralParticleStyle[] IceWindup() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Remolino de cristales", emitter = VfxType.Sparkle, rate = 120f, radius = new Vector2(0.8f, 1.2f),
                height = new Vector2(0f, 0.3f), tangentialSpeed = new Vector2(5f, 8f), outwardSpeed = new Vector2(-3f, -2f),
                upSpeed = new Vector2(0.5f, 1.5f), size = new Vector2(0.14f, 0.24f), lifetime = new Vector2(0.25f, 0.4f),
                color = IceCyan, color2 = Color.white
            },
            new SpiralParticleStyle
            {
                name = "Remolino de vaho", sprite = "Vaho", rate = 30f, radius = new Vector2(0.9f, 1.3f),
                height = new Vector2(0f, 0.2f), tangentialSpeed = new Vector2(3f, 5f), outwardSpeed = new Vector2(-2.5f, -1.5f),
                upSpeed = new Vector2(0f, 0.4f), size = new Vector2(0.6f, 1f), lifetime = new Vector2(0.3f, 0.5f),
                color = IcePale, color2 = Color.white, alpha = 0.5f
            }
        };

        /// <summary>Al encenderse: explosión compacta de esquirlas, destellos y una bocanada de vaho a ras de suelo.</summary>
        private static SpiralParticleStyle[] IceActivationBurst() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Esquirlas", emitter = VfxType.LowSpinSpark, count = 30, radius = new Vector2(0.3f, 0.5f),
                height = new Vector2(0.1f, 0.3f), tangentialSpeed = new Vector2(2.5f, 3.5f), outwardSpeed = new Vector2(1.2f, 2.2f),
                upSpeed = new Vector2(1f, 2f), size = new Vector2(0.08f, 0.14f), lifetime = new Vector2(0.25f, 0.45f),
                color = Color.white, color2 = IceCyan
            },
            new SpiralParticleStyle
            {
                name = "Destellos", emitter = VfxType.Sparkle, count = 24, radius = new Vector2(0.4f, 0.7f),
                height = new Vector2(0.05f, 0.4f), tangentialSpeed = new Vector2(2f, 3f), outwardSpeed = new Vector2(0.6f, 1.2f),
                upSpeed = new Vector2(0.3f, 0.8f), size = new Vector2(0.18f, 0.3f), lifetime = new Vector2(0.25f, 0.4f),
                color = Color.white, color2 = IceCyan
            },
            new SpiralParticleStyle
            {
                name = "Bocanada de vaho", sprite = "VahoSuelo", count = 8, radius = new Vector2(0.3f, 0.5f),
                height = new Vector2(0.02f, 0.05f), tangentialSpeed = new Vector2(1.5f, 2.5f), outwardSpeed = new Vector2(1f, 1.8f),
                upSpeed = Vector2.zero, size = new Vector2(1f, 1.5f), lifetime = new Vector2(1f, 1.6f),
                color = IcePale, color2 = Color.white, alpha = 0.5f
            }
        };

        /// <summary>Mientras dura: vaho a ras de suelo que gira (frenándose, como con la inercia de la peonza) y se queda posado.</summary>
        private static SpiralParticleStyle[] IceGroundMist() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Vaho a ras de suelo", sprite = "VahoSuelo", rate = 6f, radius = new Vector2(0.4f, 0.8f),
                height = new Vector2(0.02f, 0.05f), tangentialSpeed = new Vector2(1.2f, 2f), outwardSpeed = new Vector2(0.2f, 0.6f),
                upSpeed = Vector2.zero, size = new Vector2(1f, 1.6f), lifetime = new Vector2(1.5f, 2.2f),
                color = IcePale, color2 = Color.white, alpha = 0.35f
            }
        };

        /// <summary>Al chocar: esquirlas que salen rectas a ras de suelo, destellos de cristal y una bocanada de vaho.</summary>
        private static SpiralParticleStyle[] IceImpact() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Esquirlas", emitter = VfxType.LowSpinSpark, count = 22, radius = new Vector2(0.1f, 0.2f),
                height = new Vector2(0.05f, 0.15f), tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(5f, 8f),
                upSpeed = new Vector2(0.5f, 1.5f), size = new Vector2(0.09f, 0.16f), lifetime = new Vector2(0.35f, 0.6f),
                color = Color.white, color2 = IceCyan
            },
            new SpiralParticleStyle
            {
                name = "Destellos", emitter = VfxType.Sparkle, count = 12, radius = new Vector2(0.2f, 0.4f),
                height = new Vector2(0.1f, 0.4f), tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(1.5f, 3f),
                upSpeed = new Vector2(0.5f, 1.5f), size = new Vector2(0.2f, 0.32f), lifetime = new Vector2(0.25f, 0.45f),
                color = Color.white, color2 = IceCyan
            },
            new SpiralParticleStyle
            {
                name = "Bocanada de vaho", sprite = "Vaho", count = 8, radius = new Vector2(0.1f, 0.3f),
                height = new Vector2(0.1f, 0.3f), tangentialSpeed = new Vector2(0.3f, 0.6f), outwardSpeed = new Vector2(1.5f, 2.5f),
                upSpeed = new Vector2(0.2f, 0.6f), size = new Vector2(0.8f, 1.2f), lifetime = new Vector2(0.8f, 1.3f),
                color = IcePale, color2 = Color.white, alpha = 0.5f
            }
        };

        /// <summary>Peonza congelada: borde y tinte cian quietos (sin latir), como cubierta de escarcha.</summary>
        private static SpecialOverlayStyle FrozenOverlay() => new SpecialOverlayStyle
        {
            color = new Color(0.6f, 0.92f, 1f), hotColor = new Color(0.6f, 0.92f, 1f), baseRim = 0.8f, baseFlash = 0.35f,
            pulseRim = Vector2.zero, pulseFlash = Vector2.zero, pulseInterval = new Vector2(0.5f, 1f), pulseDecay = 0.1f
        };

        /// <summary>Peonza congelada: vaho que cae.</summary>
        private static SpecialParticleStyle FrozenMist() => new SpecialParticleStyle
        {
            name = "Vaho que cae", sprite = "Vaho", rate = 5f, spawnRadius = new Vector2(0.2f, 0.5f),
            height = new Vector2(0.25f, 0.5f), coneAngle = 20f, speed = new Vector2(0.1f, 0.3f), inheritVelocity = 0.2f,
            size = new Vector2(0.4f, 0.7f), lifetime = new Vector2(1f, 1.5f), color = IcePale, color2 = Color.white, alpha = 0.4f
        };

        /// <summary>Peonza congelada: remolino lento y pequeño de cristales.</summary>
        private static SpiralParticleStyle FrozenCrystals() => new SpiralParticleStyle
        {
            name = "Cristales lentos", emitter = VfxType.Sparkle, rate = 12f, radius = new Vector2(0.4f, 0.6f),
            height = new Vector2(0f, 0.3f), tangentialSpeed = new Vector2(1f, 1.6f), outwardSpeed = new Vector2(-0.3f, 0f),
            upSpeed = new Vector2(0.1f, 0.4f), size = new Vector2(0.1f, 0.18f), lifetime = new Vector2(0.4f, 0.7f),
            color = IceCyan, color2 = Color.white
        };

        /// <summary>Choque de una peonza congelada: un círculo pequeño de esquirlas y destellos.</summary>
        private static SpiralParticleStyle[] FrozenImpact() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Esquirlas", emitter = VfxType.LowSpinSpark, count = 10, radius = new Vector2(0.1f, 0.2f),
                height = new Vector2(0.05f, 0.15f), tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(3f, 5f),
                upSpeed = new Vector2(0.5f, 1.2f), size = new Vector2(0.07f, 0.12f), lifetime = new Vector2(0.3f, 0.5f),
                color = Color.white, color2 = IceCyan
            },
            new SpiralParticleStyle
            {
                name = "Destellos", emitter = VfxType.Sparkle, count = 5, radius = new Vector2(0.2f, 0.3f),
                height = new Vector2(0.1f, 0.3f), tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(1f, 2f),
                upSpeed = new Vector2(0.3f, 1f), size = new Vector2(0.16f, 0.26f), lifetime = new Vector2(0.25f, 0.4f),
                color = Color.white, color2 = IceCyan
            }
        };

        /// <summary>
        /// Rayos (2026-10-17, idea del usuario): arcos eléctricos como una lámpara de plasma de Tesla, alrededor de la
        /// peonza, por el suelo al moverse y hasta la otra peonza al chocar (BladeArcs), con el material VFX_Arc; y el borde
        /// amarillo parpadeando a golpes muy cortos e irregulares, como un cortocircuito; y el Aura Lightning de Status and Auras
        /// FREE en amarillo. La carga de 0,3 s con su remolino y su explosión, los impactos (pequeño y fuerte) y la peonza
        /// lanzada (2026-10-18). Sustituye a su aura de
        /// partículas de antes. Solo añade la entrada si falta y rellena lo que esté vacío.
        /// </summary>
        private static string SetupLightning(VfxLibrary library)
        {
            Material arc = ArcMaterial();
            SpecialVfxStyle lightning = library.GetSpecialEffects(SpecialAbilityType.Lightning);
            bool added = lightning == null;
            if (added)
            {
                lightning = DefaultLightning();
                library.specialEffects.Add(lightning);
            }
            lightning.arcs ??= new ElectricArcStyle();
            if (lightning.arcs.material == null) lightning.arcs.material = arc;
            // Complemento de los arcos: el Aura Lightning del paquete, de azul a amarillo (2026-10-17)
            lightning.auras ??= new List<AuraStyle>();
            ParticleSystem auraLightning = AuraPrefab(AuraLightningPrefabPath);
            if (lightning.auras.Count == 0 && auraLightning != null)
                lightning.auras.Add(new AuraStyle { name = "Aura Lightning", prefab = auraLightning, scale = 1.1f, tint = true, tintColor = new Color(1f, 0.85f, 0.15f), hideSystems = "Smoke" });
            // Carga de 0,3 s (2026-10-18, tras verla el usuario): el remolino del Fuego y del Hielo en amarillo anaranjado, con
            // ceniza que cae despacio para darle volumen (sin rayitos pixel: los rayos ya los dibujan los arcos), explosión
            // compacta, impactos y marcas negras en el suelo por donde pasa, como el Fuego
            lightning.windup ??= new List<SpiralParticleStyle>();
            if (lightning.windup.Count == 0) lightning.windup.AddRange(LightningWindup());
            lightning.activationBurst ??= new List<SpiralParticleStyle>();
            if (lightning.activationBurst.Count == 0) lightning.activationBurst.AddRange(LightningBurst());
            lightning.impact ??= new List<SpiralParticleStyle>();
            if (lightning.impact.Count == 0) lightning.impact.Add(LightningSparks(12, 3f, 5f, new Vector2(0.05f, 0.08f)));
            lightning.strongImpact ??= new List<SpiralParticleStyle>();
            if (lightning.strongImpact.Count == 0)
            {
                lightning.strongImpact.Add(LightningSparks(36, 6f, 10f, new Vector2(0.06f, 0.11f)));
                lightning.strongImpact.Add(LightningAsh(16, 1.5f, 3f));
            }
            lightning.scorch ??= new ScorchStyle();
            if (string.IsNullOrEmpty(lightning.scorch.sprite)) lightning.scorch = new ScorchStyle { sprite = "Quemado" };

            // Peonza lanzada: borde amarillo que parpadea, chispas que saltan y un círculo pequeño de chispas al chocar (el
            // Stun del paquete se quitó: sus estrellas de cuatro puntas no gustan)
            library.launched ??= new StatusVfxStyle();
            StatusVfxStyle launched = library.launched;
            launched.particles ??= new List<SpecialParticleStyle>();
            if (launched.particles.Count == 0)
            {
                launched.tip ??= new BurstTipStyle();
                launched.tip.cups.Clear();
                launched.tip.particleSprite = "";
                launched.tip.ground = new TipGroundSprite();
                launched.overlay = new SpecialOverlayStyle
                {
                    color = new Color(1f, 0.85f, 0.2f), hotColor = Color.white, baseRim = 0.4f, pulseRim = new Vector2(1.5f, 3.5f),
                    pulseFlash = new Vector2(0.1f, 0.35f), pulseInterval = new Vector2(0.03f, 0.12f), pulseDecay = 0.05f
                };
                launched.particles.Add(new SpecialParticleStyle
                {
                    name = "Chispas que saltan", sprite = "Chispa", rate = 20f, burstInterval = new Vector2(0.05f, 0.15f),
                    burstCount = new Vector2(2f, 5f), spawnRadius = new Vector2(0.3f, 0.6f), height = new Vector2(0.05f, 0.35f),
                    coneAngle = 60f, speed = new Vector2(3f, 6f), inheritVelocity = 0.3f, size = new Vector2(0.05f, 0.08f),
                    lifetime = new Vector2(0.2f, 0.4f), color = Color.white, color2 = new Color(1f, 0.8f, 0.1f)
                });
                launched.impact ??= new List<SpiralParticleStyle>();
                if (launched.impact.Count == 0) launched.impact.Add(LightningSparks(10, 3f, 5f, new Vector2(0.04f, 0.07f)));
            }
            return "Rayos" + (added ? " (nuevo)" : "");
        }

        /// <summary>
        /// Onda de choque (K8, 2026-10-18; boceto del usuario: blanco con contrastes negros y naranja). En la carga de cada
        /// onda, aros de picos que se cierran sobre la peonza, líneas de velocidad, nubes y chispas que se tragan, y la cúpula
        /// con remolino que crece y tiembla; al acabarla, revienta: la onda se abre por el suelo hasta el alcance, nubes de
        /// cómic y chispas salen despedidas y quedan grietas. Mientras dura, chispas a ras de suelo (las de su aura de antes,
        /// sin el aro pixelado, que no gusta) y el aviso de ondas que quedan. En los rivales: nubes en el lado del golpe,
        /// estela de polvo mientras vuelan lanzados y polvo al chocar contra la pared. Solo añade la entrada si falta y
        /// rellena lo que esté vacío.
        /// </summary>
        private static string SetupShockWave(VfxLibrary library, Shader flipbook)
        {
            SpecialVfxStyle wave = library.GetSpecialEffects(SpecialAbilityType.ShockWave);
            bool added = wave == null;
            if (added)
            {
                wave = new SpecialVfxStyle { type = SpecialAbilityType.ShockWave, replaceAura = true, windupSustain = 0f };
                wave.tip.cups.Clear();
                wave.tip.particleSprite = "";
                wave.tip.ground = new TipGroundSprite();
                wave.overlay = new SpecialOverlayStyle
                {
                    color = ShockOrange, hotColor = Color.white, baseRim = 0.2f, pulseRim = new Vector2(0.6f, 1.5f),
                    pulseFlash = new Vector2(0f, 0.1f), pulseInterval = new Vector2(0.15f, 0.4f), pulseDecay = 0.2f
                };
                library.specialEffects.Add(wave);
            }

            wave.dome ??= new ChargeDomeStyle();
            if (wave.dome.material == null)
            {
                Shader domeShader = Shader.Find(ChargeDomeShaderName);
                if (domeShader != null) wave.dome.material = GetMaterial(ChargeDomeMaterialPath, domeShader);
            }
            if (wave.dome.linesTexture == null) wave.dome.linesTexture = FakeBladeImpactSetup.SpeedLinesTexture();

            wave.windup ??= new List<SpiralParticleStyle>();
            if (wave.windup.Count == 0) wave.windup.AddRange(ShockWindup());
            wave.activationBurst ??= new List<SpiralParticleStyle>();
            if (wave.activationBurst.Count == 0) wave.activationBurst.AddRange(ShockBurst());
            // El remolino de la carga sigue, más flojo, mientras dura el poder (como en los anteriores; 2026-10-18)
            wave.swirl ??= new List<SpiralParticleStyle>();
            if (wave.swirl.Count == 0) wave.swirl.AddRange(ShockSwirl());
            wave.particles ??= new List<SpecialParticleStyle>();
            if (wave.particles.Count == 0)
                wave.particles.Add(new SpecialParticleStyle
                {
                    name = "Chispas a ras de suelo", emitter = VfxType.GroundSpark, rate = 14f, spawnRadius = new Vector2(0.3f, 0.4f),
                    height = new Vector2(0.02f, 0.05f), coneAngle = 80f, speed = new Vector2(2.5f, 4f), inheritVelocity = 0f,
                    size = Vector2.zero, lifetime = Vector2.zero, color = ShockOrange, color2 = ShockOrange
                });
            wave.impact ??= new List<SpiralParticleStyle>();
            if (wave.impact.Count == 0) wave.impact.Add(ShockClouds("Nubes", 8, 2f, 3f, new Vector2(0.3f, 0.5f), new Vector2(0.4f, 0.55f)));
            wave.pushHit ??= new List<SpiralParticleStyle>();
            if (wave.pushHit.Count == 0)
            {
                wave.pushHit.Add(ShockClouds("Nubes del golpe", 10, 2f, 4f, new Vector2(0.4f, 0.7f), new Vector2(0.45f, 0.6f)));
                wave.pushHit.Add(ShockSparks(12, 3f, 6f));
            }
            wave.launchedTrail ??= new List<SpecialParticleStyle>();
            if (wave.launchedTrail.Count == 0)
                wave.launchedTrail.Add(new SpecialParticleStyle
                {
                    name = "Estela de polvo", sprite = "NubeComic", rate = 30f, spawnRadius = new Vector2(0.2f, 0.4f),
                    height = new Vector2(0f, 0.15f), coneAngle = 70f, speed = new Vector2(0.3f, 0.8f), inheritVelocity = -0.1f,
                    size = new Vector2(0.35f, 0.6f), lifetime = new Vector2(0.4f, 0.6f), color = Color.white, color2 = ShockDust,
                    alpha = 0.85f
                });
            wave.launchedWallHit ??= new List<SpiralParticleStyle>();
            if (wave.launchedWallHit.Count == 0)
            {
                wave.launchedWallHit.Add(ShockClouds("Polvo contra la pared", 18, 3f, 5f, new Vector2(0.5f, 0.9f), new Vector2(0.6f, 0.8f)));
                wave.launchedWallHit.Add(ShockSparks(20, 4f, 8f));
            }

            // Aviso de usos (para todos los poderes con varios usos): la marca de MarcaUso con el shader de las hojas
            library.usesIndicator ??= new UsesIndicatorStyle();
            if (library.usesIndicator.material == null && flipbook != null)
            {
                Material mark = GetMaterial(UseMarkMaterialPath, flipbook);
                mark.SetTexture("_MainTex", FakeBladeImpactSetup.UseMarkTexture());
                mark.SetFloat("_TilesX", 1f);
                mark.SetFloat("_TilesY", 1f);
                EditorUtility.SetDirty(mark);
                library.usesIndicator.material = mark;
            }
            return "Onda de choque" + (added ? " (nueva)" : "") + (wave.dome.material == null ? " (falta el shader de la cúpula)" : "");
        }

        private static readonly Color ShockOrange = new Color(1f, 0.55f, 0.15f);
        private static readonly Color ShockDust = new Color(0.93f, 0.87f, 0.75f);
        /// <summary>Grietas dibujadas por el usuario (2026-10-19): una al azar en cada golpe al suelo (Onda de choque; Salto al caer).</summary>
        private static readonly string[] CrackVariants =
        {
            "Grieta00", "Grieta01", "Grieta02", "Grieta03", "GrietaInicio00", "GrietaCircular01", "GrietaCircular02", "GrietaCircular03"
        };

        /// <summary>
        /// Carga de cada onda: aros de picos que se cierran sobre la peonza, líneas de velocidad hacia dentro, nubes que se
        /// tragan girando y chispas.
        /// </summary>
        private static SpiralParticleStyle[] ShockWindup() => new[]
        {
            ShockCircle("Círculo que se cierra (suelo)", "CircleImpact02Cierra", 5f, 0.03f, 0.4f, Color.white, 0.9f, rate: 4f),
            ShockCircle("Círculo que se cierra (alto)", "FP_CircleImpactCierra", 3.6f, 0.45f, 0.4f, ShockOrange, 0.8f, rate: 3f),
            new SpiralParticleStyle
            {
                name = "Líneas de velocidad", sprite = "Linea", rate = 50f, radius = new Vector2(2.2f, 3f),
                height = new Vector2(0.05f, 0.6f), tangentialSpeed = new Vector2(1f, 2f), outwardSpeed = new Vector2(-9f, -7f),
                upSpeed = Vector2.zero, size = new Vector2(0.06f, 0.09f), lifetime = new Vector2(0.22f, 0.3f),
                color = Color.white, color2 = ShockOrange
            },
            new SpiralParticleStyle
            {
                name = "Nubes que se tragan", sprite = "NubeComicTraga", rate = 40f, radius = new Vector2(2f, 2.8f),
                height = new Vector2(0f, 0.3f), tangentialSpeed = new Vector2(3f, 5f), outwardSpeed = new Vector2(-6f, -4.5f),
                upSpeed = new Vector2(0f, 0.5f), size = new Vector2(0.25f, 0.45f), lifetime = new Vector2(0.35f, 0.45f),
                color = Color.white, color2 = ShockDust, alpha = 0.9f
            },
            new SpiralParticleStyle
            {
                name = "Chispas que se tragan", sprite = "Chispa", rate = 40f, radius = new Vector2(1.2f, 1.8f),
                height = new Vector2(0f, 0.2f), tangentialSpeed = new Vector2(4f, 6f), outwardSpeed = new Vector2(-4f, -3f),
                upSpeed = new Vector2(1f, 2f), size = new Vector2(0.05f, 0.08f), lifetime = new Vector2(0.3f, 0.45f),
                color = Color.white, color2 = ShockOrange
            }
        };

        /// <summary>
        /// Explosión de cada onda: ondas en capas (círculos del pack a distinta altura y tamaño), humo de explosión despedido (alto
        /// y bajo: de blanco a naranja y gris, y se rompe en pedacitos, como CFXR Explosion 1), chispas y grietas en el suelo.
        /// </summary>
        private static SpiralParticleStyle[] ShockBurst() => new[]
        {
            ShockCircle("Onda en el suelo", "CircleImpact02Suelo", 10.5f, 0.03f, 0.45f, Color.white, 0.95f),
            ShockCircle("Onda baja", "FP_CircleImpactSuelo", 8f, 0.25f, 0.4f, ShockOrange, 0.85f),
            ShockCircle("Onda alta", "ShotValo2Suelo", 5.5f, 0.6f, 0.35f, Color.white, 0.8f),
            ShockCircle("Medias lunas", "CircleImpact04Anillo", 7f, 0.12f, 0.4f, ShockOrange, 0.8f, count: 2),
            new SpiralParticleStyle
            {
                name = "Humo despedido", sprite = "HumoExplosion", count = 34, radius = new Vector2(0.5f, 0.8f),
                height = new Vector2(0.45f, 0.75f), tangentialSpeed = new Vector2(0.5f, 1.5f), outwardSpeed = new Vector2(6f, 13f),
                upSpeed = new Vector2(0.4f, 1f), size = new Vector2(0.8f, 1.3f), lifetime = new Vector2(0.9f, 1.3f),
                color = Color.white, color2 = Color.white
            },
            new SpiralParticleStyle
            {
                name = "Humo bajo", sprite = "HumoExplosion", count = 20, radius = new Vector2(0.4f, 0.6f),
                height = new Vector2(0.4f, 0.55f), tangentialSpeed = new Vector2(0.3f, 1f), outwardSpeed = new Vector2(3f, 7f),
                upSpeed = new Vector2(0.1f, 0.4f), size = new Vector2(0.9f, 1.3f), lifetime = new Vector2(1f, 1.4f),
                color = Color.white, color2 = ShockDust
            },
            ShockSparks(40, 6f, 10f),
            new SpiralParticleStyle
            {
                name = "Grietas", sprite = "Grietas", count = 1, radius = Vector2.zero, height = new Vector2(0.02f, 0.02f),
                tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = Vector2.zero, size = new Vector2(3f, 5f),
                lifetime = new Vector2(1.2f, 1.2f), color = new Color(0.1f, 0.1f, 0.1f), color2 = new Color(0.1f, 0.1f, 0.1f), alpha = 0.85f,
                // Una de las grietas del usuario al azar, con tamaño al azar y girada entre 60 y 90 grados: cada onda deja una
                // distinta (2026-10-19)
                spriteVariants = new List<string>(CrackVariants), rotation = new Vector2(60f, 90f)
            }
        };

        /// <summary>
        /// Un círculo del pack tumbado (CircleImpact02, FP_CircleImpact, ImpactShotValo2, CircleImpact04: no son círculos
        /// exactos), quieto sobre la peonza, de size metros de ancho y a height metros de altura (capas a distinta altura y
        /// tamaño). count: de golpe (explosión); rate: por segundo (carga). Gira al azar.
        /// </summary>
        private static SpiralParticleStyle ShockCircle(string name, string sprite, float size, float height, float lifetime, Color color,
            float alpha, int count = 1, float rate = 0f) => new SpiralParticleStyle
        {
            name = name, sprite = sprite, count = count, rate = rate, radius = Vector2.zero, height = new Vector2(height, height),
            tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = Vector2.zero, size = new Vector2(size, size),
            lifetime = new Vector2(lifetime, lifetime), color = color, color2 = color, alpha = alpha
        };

        /// <summary>Mientras dura el poder: las nubes y chispas de la carga, que siguen girando y tragándose más despacio.</summary>
        private static SpiralParticleStyle[] ShockSwirl()
        {
            SpiralParticleStyle[] windup = ShockWindup();
            var swirl = new[] { windup[3], windup[4] };
            swirl[0].rate = 10f;
            swirl[1].rate = 12f;
            return swirl;
        }

        /// <summary>Círculo de nubes de cómic que salen hacia fuera.</summary>
        private static SpiralParticleStyle ShockClouds(string name, int count, float minSpeed, float maxSpeed, Vector2 size, Vector2 lifetime) => new SpiralParticleStyle
        {
            name = name, sprite = "NubeComic", count = count, radius = new Vector2(0.1f, 0.2f), height = new Vector2(0.05f, 0.25f),
            tangentialSpeed = new Vector2(0.3f, 0.8f), outwardSpeed = new Vector2(minSpeed, maxSpeed), upSpeed = new Vector2(0.5f, 1f),
            size = size, lifetime = lifetime, color = Color.white, color2 = ShockDust
        };

        /// <summary>Círculo de chispas blancas y naranjas que salen rectas, saltan y caen.</summary>
        private static SpiralParticleStyle ShockSparks(int count, float minSpeed, float maxSpeed) => new SpiralParticleStyle
        {
            name = "Chispas", sprite = "Chispa", count = count, radius = new Vector2(0.2f, 0.4f), height = new Vector2(0.05f, 0.25f),
            tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(minSpeed, maxSpeed), upSpeed = new Vector2(1.5f, 3f),
            size = new Vector2(0.06f, 0.1f), lifetime = new Vector2(0.3f, 0.55f), color = Color.white, color2 = ShockOrange
        };

        /// <summary>
        /// Tornado (K8, 2026-10-18; boceto del usuario en cuatro viñetas, ajustado tras verlo): 1) en la carga, círculos que se
        /// cierran (CircleImpact02, FP_CircleImpact y CircleImpact04 al revés), líneas de velocidad y humo que se tragan hacia la
        /// peonza; 2) al empezar a atraer, un embudo que sube desde el suelo; 3) que se ensancha hasta ser una cúpula de viento
        /// con un aro de picos abierto arriba (se ve por dentro), mientras las rayas de píxel traen el viento desde todo el
        /// alcance, se siguen cerrando círculos y se traga humo; 4) al soltar el empujón, la cúpula revienta, se abren círculos
        /// (con ImpactShotValo) y salen líneas y humo que se disuelve. Blanco con toques del verde agua del poder. Solo añade la
        /// entrada si falta y rellena lo que esté vacío.
        /// </summary>
        private static string SetupTornado(VfxLibrary library)
        {
            SpecialVfxStyle tornado = library.GetSpecialEffects(SpecialAbilityType.Tornado);
            bool added = tornado == null;
            if (added)
            {
                tornado = new SpecialVfxStyle { type = SpecialAbilityType.Tornado, replaceAura = true, windupSustain = 0f };
                tornado.tip.cups.Clear();
                tornado.tip.particleSprite = "";
                tornado.tip.ground = new TipGroundSprite();
                tornado.overlay = new SpecialOverlayStyle
                {
                    color = TornadoTeal, hotColor = Color.white, baseRim = 0.2f, pulseRim = new Vector2(0.5f, 1.2f),
                    pulseFlash = new Vector2(0f, 0.08f), pulseInterval = new Vector2(0.15f, 0.35f), pulseDecay = 0.2f
                };
                tornado.dome = TornadoDome();
                library.specialEffects.Add(tornado);
            }

            tornado.dome ??= TornadoDome();
            if (tornado.dome.material == null)
            {
                Shader domeShader = Shader.Find(ChargeDomeShaderName);
                if (domeShader != null) tornado.dome.material = GetMaterial(ChargeDomeMaterialPath, domeShader);
            }
            if (tornado.dome.linesTexture == null) tornado.dome.linesTexture = FakeBladeImpactSetup.SpeedLinesTexture();

            tornado.windup ??= new List<SpiralParticleStyle>();
            if (tornado.windup.Count == 0) tornado.windup.AddRange(TornadoWindup());
            tornado.activationBurst ??= new List<SpiralParticleStyle>();
            if (tornado.activationBurst.Count == 0) tornado.activationBurst.AddRange(TornadoRise());
            tornado.swirl ??= new List<SpiralParticleStyle>();
            if (tornado.swirl.Count == 0) tornado.swirl.AddRange(TornadoPull());
            tornado.release ??= new List<SpiralParticleStyle>();
            if (tornado.release.Count == 0) tornado.release.AddRange(TornadoRelease());
            tornado.impact ??= new List<SpiralParticleStyle>();
            if (tornado.impact.Count == 0) tornado.impact.Add(TornadoLines("Líneas", 12, 4f, 7f));
            tornado.pushHit ??= new List<SpiralParticleStyle>();
            if (tornado.pushHit.Count == 0) tornado.pushHit.AddRange(TornadoPushHit());
            tornado.launchedTrail ??= new List<SpecialParticleStyle>();
            if (tornado.launchedTrail.Count == 0) tornado.launchedTrail.Add(TornadoLaunchedTrail());
            tornado.launchedWallHit ??= new List<SpiralParticleStyle>();
            if (tornado.launchedWallHit.Count == 0) tornado.launchedWallHit.AddRange(TornadoWallHit());
            return "Tornado" + (added ? " (nuevo)" : "") + (tornado.dome.material == null ? " (falta el shader de la cúpula)" : "");
        }

        private static readonly Color TornadoTeal = new Color(0.4f, 0.85f, 0.63f);
        private static readonly Color TornadoPale = new Color(0.85f, 1f, 0.95f);

        /// <summary>
        /// Embudo que se hace cúpula mientras atrae (1,5 s), con los valores que dejó el usuario (2026-10-18): embudo ancho y alto
        /// que sube durante el 65% de la atracción y se ensancha hasta la cúpula en el resto; cinco picos arriba que giran
        /// deprisa; se ve por dentro a través del corte.
        /// </summary>
        private static ChargeDomeStyle TornadoDome() => new ChargeDomeStyle
        {
            radius = 1.5f, heightScale = 0.9f, height = 0f, growEase = 1.6f, startRadius = 0.703f,
            funnelPhase = 0.651f, funnelMorph = 0.386f, funnelShape = new Vector3(0.6f, 2f, 3f),
            topCut = 0.228f, topSpikes = 5, topSpikesSpeed = 3f, insideAlpha = 0.7f,
            bodyColor = new Color(0.9f, 1f, 0.97f, 0.14f), rimColor = new Color(TornadoTeal.r, TornadoTeal.g, TornadoTeal.b, 0.9f),
            outlineColor = new Color(0.47f, 0.47f, 0.47f, 0.8f), swirlColor = new Color(1f, 1f, 1f, 0.85f),
            swirlColor2 = new Color(TornadoTeal.r, TornadoTeal.g, TornadoTeal.b, 0.6f), rimWidth = 0.25f, outlineWidth = 0.05f,
            linesTiling = new Vector2(4f, 2f), linesSpeed = 1.8f, linesTwist = 0.3f, axisTilt = 12f, axisPrecess = 1.2f,
            swirlLines = 6f, swirlTwist = 0.9f, swirlSpeed = 2.5f, swirlWidth = 0.14f,
            shakeFrom = 0.7f, shakeScale = 0.06f, shakeRate = 20f, popTime = 0.2f, popScale = 1.9f, popFlash = 0.6f
        };

        /// <summary>Viñeta 1, la carga (0,3 s): círculos que se cierran, líneas de velocidad y humo que se tragan hacia la peonza.</summary>
        private static SpiralParticleStyle[] TornadoWindup() => new[]
        {
            ShockCircle("Círculo que se cierra (suelo)", "CircleImpact02Cierra", 10f, 0.03f, 0.4f, Color.white, 0.9f, rate: 10f),
            ShockCircle("Círculo que se cierra (alto)", "FP_CircleImpactCierra", 5f, 0.45f, 0.4f, TornadoTeal, 0.8f, rate: 8f),
            ShockCircle("Medias lunas que se cierran", "CircleImpact04Cierra", 7f, 0.15f, 0.4f, TornadoPale, 0.8f, rate: 8f),
            TornadoInwardLines("Líneas de velocidad", 60f),
            TornadoInwardSmoke("Humo que se traga", 40f, new Vector2(2f, 2.8f))
        };

        /// <summary>Viñeta 2, al empezar a atraer (sube el embudo): humo que sale de la base.</summary>
        private static SpiralParticleStyle[] TornadoRise() => new[]
        {
            TornadoSmoke("Humo de la base", 10, 1.5f, 3f, new Vector2(0.5f, 0.8f), new Vector2(0.7f, 0.9f))
        };

        /// <summary>
        /// Viñeta 3, mientras atrae: las rayas de píxel de antes, que traen el viento desde todo el alcance (se ve hasta dónde
        /// atrae), un círculo grande que se cierra de vez en cuando y humo que se traga.
        /// </summary>
        private static SpiralParticleStyle[] TornadoPull() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Rayas del alcance", emitter = VfxType.Dash, rate = 110f, radius = new Vector2(1f, 6f),
                height = new Vector2(0.1f, 0.5f), tangentialSpeed = new Vector2(6f, 8f), outwardSpeed = new Vector2(-5.5f, -4.5f),
                upSpeed = Vector2.zero, size = new Vector2(0.09f, 0.09f), lifetime = new Vector2(0.3f, 0.3f),
                color = TornadoTeal, color2 = TornadoPale
            },
            ShockCircle("Círculo que se cierra (alcance)", "CircleImpact02Cierra", 12f, 0.03f, 0.4f, Color.white, 0.6f, rate: 2.5f),
            TornadoInwardSmoke("Humo que se traga", 18f, new Vector2(2f, 4.5f))
        };

        /// <summary>Viñeta 4, al soltar el empujón: círculos que se abren (con ImpactShotValo), líneas y humo que sale despedido.</summary>
        private static SpiralParticleStyle[] TornadoRelease() => new[]
        {
            ShockCircle("Onda en el suelo", "CircleImpact02Suelo", 12f, 0.03f, 0.45f, Color.white, 0.95f),
            ShockCircle("Onda baja", "FP_CircleImpactSuelo", 9f, 0.25f, 0.4f, TornadoTeal, 0.85f),
            ShockCircle("ImpactShotValo", "ShotValoSuelo", 6.5f, 0.5f, 0.35f, Color.white, 0.85f),
            TornadoLines("Líneas que salen", 40, 14f, 20f),
            new SpiralParticleStyle
            {
                name = "Humo despedido", sprite = "HumoViento", count = 30, radius = new Vector2(0.5f, 0.8f),
                height = new Vector2(0.45f, 0.75f), tangentialSpeed = new Vector2(1f, 2.5f), outwardSpeed = new Vector2(6f, 13f),
                upSpeed = new Vector2(0.4f, 1f), size = new Vector2(0.8f, 1.3f), lifetime = new Vector2(0.9f, 1.3f),
                color = Color.white, color2 = TornadoPale
            },
            TornadoSmoke("Humo bajo", 18, 3f, 7f, new Vector2(0.9f, 1.3f), new Vector2(1f, 1.4f))
        };

        /// <summary>En cada rival que alcanza el empujón: humo y líneas en el lado del golpe.</summary>
        private static SpiralParticleStyle[] TornadoPushHit() => new[]
        {
            TornadoSmoke("Humo del golpe", 8, 2f, 4f, new Vector2(0.5f, 0.8f), new Vector2(0.6f, 0.8f)),
            TornadoLines("Líneas del golpe", 12, 5f, 8f)
        };

        /// <summary>Peonza lanzada por el Tornado, mientras vuela: estela de nubes (como la de Onda de choque).</summary>
        private static SpecialParticleStyle TornadoLaunchedTrail() => new SpecialParticleStyle
        {
            name = "Estela de humo", sprite = "NubeComic", rate = 30f, spawnRadius = new Vector2(0.2f, 0.4f),
            height = new Vector2(0f, 0.15f), coneAngle = 70f, speed = new Vector2(0.3f, 0.8f), inheritVelocity = -0.1f,
            size = new Vector2(0.35f, 0.6f), lifetime = new Vector2(0.4f, 0.6f), color = Color.white, color2 = TornadoPale,
            alpha = 0.85f
        };

        /// <summary>Peonza lanzada por el Tornado que choca contra la pared: humo y líneas.</summary>
        private static SpiralParticleStyle[] TornadoWallHit() => new[]
        {
            TornadoSmoke("Humo contra la pared", 14, 3f, 5f, new Vector2(0.5f, 0.9f), new Vector2(0.6f, 0.8f)),
            TornadoLines("Líneas contra la pared", 14, 5f, 8f)
        };

        /// <summary>Líneas de velocidad (el punto estirado) que se enroscan hacia la peonza.</summary>
        private static SpiralParticleStyle TornadoInwardLines(string name, float rate) => new SpiralParticleStyle
        {
            name = name, sprite = "Linea", rate = rate, radius = new Vector2(2.5f, 3.5f), height = new Vector2(0.05f, 0.6f),
            tangentialSpeed = new Vector2(3f, 5f), outwardSpeed = new Vector2(-10f, -8f), upSpeed = Vector2.zero,
            size = new Vector2(0.06f, 0.09f), lifetime = new Vector2(0.22f, 0.3f), color = Color.white, color2 = TornadoPale
        };

        /// <summary>Humo que gira y se traga hacia la peonza (se encoge al llegar).</summary>
        private static SpiralParticleStyle TornadoInwardSmoke(string name, float rate, Vector2 radius) => new SpiralParticleStyle
        {
            name = name, sprite = "HumoTraga", rate = rate, radius = radius, height = new Vector2(0f, 0.4f),
            tangentialSpeed = new Vector2(3f, 5f), outwardSpeed = new Vector2(-6f, -4.5f), upSpeed = new Vector2(0f, 0.5f),
            size = new Vector2(0.45f, 0.75f), lifetime = new Vector2(0.4f, 0.5f), color = Color.white, color2 = TornadoPale, alpha = 0.9f
        };

        /// <summary>Círculo de líneas de velocidad que salen rectas hacia fuera.</summary>
        private static SpiralParticleStyle TornadoLines(string name, int count, float minSpeed, float maxSpeed) => new SpiralParticleStyle
        {
            name = name, sprite = "Linea", count = count, radius = new Vector2(0.6f, 1f), height = new Vector2(0.05f, 0.5f),
            tangentialSpeed = new Vector2(0.5f, 1.5f), outwardSpeed = new Vector2(minSpeed, maxSpeed), upSpeed = Vector2.zero,
            size = new Vector2(0.07f, 0.1f), lifetime = new Vector2(0.25f, 0.35f), color = Color.white, color2 = TornadoPale
        };

        /// <summary>Círculo de humo que sale hacia fuera y se disuelve.</summary>
        private static SpiralParticleStyle TornadoSmoke(string name, int count, float minSpeed, float maxSpeed, Vector2 size, Vector2 lifetime) =>
            new SpiralParticleStyle
            {
                name = name, sprite = "HumoViento", count = count, radius = new Vector2(0.3f, 0.6f), height = new Vector2(0.3f, 0.6f),
                tangentialSpeed = new Vector2(0.3f, 1f), outwardSpeed = new Vector2(minSpeed, maxSpeed), upSpeed = new Vector2(0.1f, 0.5f),
                size = size, lifetime = lifetime, color = Color.white, color2 = TornadoPale
            };

        private static readonly Color RamLime = new Color(0.7f, 1f, 0.25f);
        private static readonly Color RamPale = new Color(0.92f, 1f, 0.78f);
        private static readonly Color RamDebris = new Color(0.72f, 0.72f, 0.68f);
        private static readonly Color CrackDark = new Color(0.1f, 0.1f, 0.1f);
        /// <summary>Giro que pone mirando hacia la marcha la parte de fuera de la media luna (SlashOne la tiene arriba a la derecha).</summary>
        private const float CrescentForward = -45f;
        /// <summary>Giro que deja las grietas del usuario (dibujadas del centro hacia arriba a la derecha) hacia atrás en el camino.</summary>
        private const float CrackBackward = 135f;
        /// <summary>Corona de pinchos en horizontal dibujada por el usuario (2026-10-20): la Zbam que recorre los cilindros de la Embestida.</summary>
        private const string RamZbamTexturePath = "Assets/VFX/Textures/Impacts/Zbam HORIZONTAL.png";
        /// <summary>Las grietas rectas del usuario (no las circulares): el rastro de la Embestida.</summary>
        private static readonly string[] LineCrackVariants = { "Grieta00", "Grieta01", "Grieta02", "Grieta03" };

        /// <summary>
        /// Embestida (K8, 2026-10-20; bocetos del usuario en tres viñetas). Se empieza copiando la plantilla común (carga,
        /// encendido y estela en la golpeada; con los círculos finos de CircleImpact04) y se le añade lo suyo: 1) en la carga de
        /// cada embestida, un cilindro más ancho arriba y abierto en picos, que sube desde el suelo, con la Zbam recorriéndolo
        /// en horizontal y líneas de velocidad pequeñas que suben; en la base, otro cilindro pequeño con la Zbam más marcada;
        /// humo y partículas pequeñas que se tragan; 2) al embestir, el efecto de la punta delante de la peonza (fuego de
        /// entrar en la atmósfera), trozos de suelo y alguna Zbam detrás y un rastro de grietas; 3) en el golpe, un tajo curvo
        /// que envuelve al rival, Zbam, trozos y chispas, y la golpeada sale lanzada con su estela. Verde lima (su color) y
        /// blanco, con el contorno gris fino. Solo añade la entrada si falta y rellena lo que esté vacío.
        /// </summary>
        private static string SetupRam(VfxLibrary library)
        {
            SpecialVfxStyle ram = library.GetSpecialEffects(SpecialAbilityType.Ram);
            bool added = ram == null;
            if (added)
            {
                ram = new SpecialVfxStyle { type = SpecialAbilityType.Ram, replaceAura = true, windupSustain = 0f };
                ram.tip.cups.Clear();
                ram.tip.particleSprite = "";
                ram.tip.ground = new TipGroundSprite();
                ram.overlay = new SpecialOverlayStyle
                {
                    color = RamLime, hotColor = Color.white, baseRim = 0.2f, pulseRim = new Vector2(0.5f, 1.2f),
                    pulseFlash = new Vector2(0f, 0.08f), pulseInterval = new Vector2(0.15f, 0.35f), pulseDecay = 0.2f
                };
                ram.dome = RamDome();
                ram.baseDome = RamBaseDome();
                library.specialEffects.Add(ram);
            }

            ram.dome ??= RamDome();
            ram.baseDome ??= RamBaseDome();
            Shader domeShader = Shader.Find(ChargeDomeShaderName);
            Material domeMaterial = domeShader != null ? GetMaterial(ChargeDomeMaterialPath, domeShader) : null;
            if (ram.dome.material == null) ram.dome.material = domeMaterial;
            if (ram.baseDome.material == null) ram.baseDome.material = domeMaterial;
            // La Zbam que recorre en horizontal los dos cilindros (2026-10-20, corregido por el usuario: no Zbam que suben): la
            // corona de pinchos que dibujó el usuario (Zbam HORIZONTAL) o, si no está, Zbam2 del pack
            var zbam = AssetDatabase.LoadAssetAtPath<Texture2D>(RamZbamTexturePath) ??
                       AssetDatabase.LoadAssetAtPath<Texture2D>(WallcoeurFolder + "03_Texture/Zbam2.png");
            if (ram.dome.linesTexture == null) ram.dome.linesTexture = zbam;
            if (ram.baseDome.linesTexture == null) ram.baseDome.linesTexture = zbam;
            // Cuenco delante mientras embiste: el efecto de la punta en el acelerón, girado hacia la marcha y en verde lima
            ram.tip ??= new BurstTipStyle();
            if (ram.tip.cups.Count == 0) RamTip(ram.tip, library.burstTip);

            // La plantilla común como base (se copia al empezar cada especial, 2026-10-19) y lo propio detrás
            ram.windup ??= new List<SpiralParticleStyle>();
            if (ram.windup.Count == 0)
            {
                ram.windup.AddRange(GenericWindup());
                foreach (SpiralParticleStyle style in ram.windup)
                {
                    // Más humo que se traga que en la plantilla (lo pidió el usuario para la carga)
                    if (style.name == "Humo que se traga") style.rate = 45f;
                    // Círculos más finos que los de la plantilla (2026-10-20): la media luna fina de CircleImpact04
                    if (style.sprite == "CircleImpact02Cierra" || style.sprite == "FP_CircleImpactCierra") style.sprite = "CircleImpact04Cierra";
                }
                ram.windup.AddRange(RamWindup());
            }
            ram.activationBurst ??= new List<SpiralParticleStyle>();
            if (ram.activationBurst.Count == 0)
            {
                ram.activationBurst.AddRange(GenericBurst());
                foreach (SpiralParticleStyle style in ram.activationBurst)
                    if (style.sprite == "CircleImpact02Suelo")
                    {
                        // Dos medias lunas finas que se abren (CircleImpact02 empieza con un círculo relleno)
                        style.sprite = "CircleImpact04Anillo";
                        style.count = 2;
                    }
                ram.activationBurst.AddRange(RamLaunch());
            }
            ram.hitTrail ??= new List<SpecialParticleStyle>();
            if (ram.hitTrail.Count == 0) ram.hitTrail.AddRange(GenericHitTrail());
            ram.rush ??= new List<SpecialParticleStyle>();
            if (ram.rush.Count == 0) ram.rush.AddRange(RamRush());
            ram.scorch ??= new ScorchStyle();
            if (string.IsNullOrEmpty(ram.scorch.sprite) && (ram.scorch.spriteVariants == null || ram.scorch.spriteVariants.Count == 0))
                ram.scorch = RamCracks();
            ram.pushHit ??= new List<SpiralParticleStyle>();
            if (ram.pushHit.Count == 0) ram.pushHit.AddRange(RamHit());
            ram.launchedTrail ??= new List<SpecialParticleStyle>();
            if (ram.launchedTrail.Count == 0) ram.launchedTrail.AddRange(GenericHitTrail());
            ram.launchedWallHit ??= new List<SpiralParticleStyle>();
            if (ram.launchedWallHit.Count == 0) ram.launchedWallHit.AddRange(RamWallHit());
            return "Embestida" + (added ? " (nueva)" : "") + (ram.dome.material == null ? " (falta el shader de la cúpula)" : "");
        }

        /// <summary>
        /// Viñeta 1: cilindro más ancho arriba (el embudo del Tornado, que no pasa a cúpula), abierto en picos, con líneas
        /// verticales finas y la Zbam (la corona Zbam HORIZONTAL del usuario) recorriéndolo en horizontal; sube desde el suelo
        /// durante la carga y revienta al salir. Lines Tiling y = 2: la cúpula solo usa la mitad de arriba de la altura, así la
        /// Zbam ocupa todo el alto.
        /// </summary>
        private static ChargeDomeStyle RamDome() => new ChargeDomeStyle
        {
            radius = 1f, heightScale = 1f, height = 0f, growEase = 1.5f, startRadius = 0.6f,
            funnelPhase = 1f, funnelMorph = 0.2f, funnelShape = new Vector3(0.8f, 1.6f, 2f),
            topCut = 0.18f, topSpikes = 7, topSpikesSpeed = 0.6f, insideAlpha = 0.3f,
            bodyColor = new Color(0.75f, 1f, 0.45f, 0.2f), rimColor = new Color(RamLime.r, RamLime.g, RamLime.b, 0.9f),
            outlineColor = new Color(0.47f, 0.47f, 0.47f, 0.8f), swirlColor = new Color(1f, 1f, 1f, 0.85f),
            swirlColor2 = new Color(RamLime.r, RamLime.g, RamLime.b, 0.5f), rimWidth = 0.22f, outlineWidth = 0.05f,
            linesTiling = new Vector2(5f, 2f), linesSpeed = 0.8f, linesTwist = 0f, axisTilt = 0f, axisPrecess = 0f,
            swirlLines = 6f, swirlTwist = 0f, swirlSpeed = 0.4f, swirlWidth = 0.05f,
            shakeFrom = 0.6f, shakeScale = 0.05f, shakeRate = 24f, popTime = 0.14f, popScale = 1.25f, popFlash = 0.7f
        };

        /// <summary>
        /// Viñeta 1, en la base: cilindro bajo, más ancho que la peonza (si no, ella lo tapa) y que la base del de arriba, con la
        /// Zbam (Zbam HORIZONTAL) más pequeña y marcada recorriéndolo en horizontal hacia el otro lado. Borde fino: el ancho lo
        /// tapaba entero.
        /// </summary>
        private static ChargeDomeStyle RamBaseDome() => new ChargeDomeStyle
        {
            radius = 1.25f, heightScale = 0.36f, height = 0f, growEase = 3f, startRadius = 0.85f,
            funnelPhase = 1f, funnelMorph = 0.2f, funnelShape = new Vector3(1f, 1.1f, 1f),
            topCut = 0f, insideAlpha = 0f,
            bodyColor = new Color(RamLime.r, RamLime.g, RamLime.b, 0.4f), rimColor = new Color(1f, 1f, 1f, 0.95f),
            outlineColor = new Color(0.3f, 0.3f, 0.3f, 0.9f), swirlColor = Color.white, swirlColor2 = new Color(0f, 0f, 0f, 0f),
            rimWidth = 0.08f, outlineWidth = 0.04f,
            linesTiling = new Vector2(6f, 2f), linesSpeed = -1.2f, linesTwist = 0f, axisTilt = 0f, axisPrecess = 0f,
            swirlLines = 0f, swirlTwist = 0f, swirlSpeed = 0f, swirlWidth = 0f,
            shakeFrom = 0.5f, shakeScale = 0.1f, shakeRate = 26f, popTime = 0.12f, popScale = 1.6f, popFlash = 0.9f
        };

        /// <summary>
        /// Viñeta 1, la carga de cada embestida: líneas de velocidad pequeñas que suben por el cilindro (2026-10-20: mucho más
        /// pequeñas que las primeras) y partículas pequeñas que se tragan.
        /// </summary>
        private static SpiralParticleStyle[] RamWindup() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Líneas que suben", sprite = "Linea", rate = 70f, radius = new Vector2(0.6f, 1.2f),
                height = new Vector2(0f, 0.2f), tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(0.3f, 0.8f),
                upSpeed = new Vector2(3f, 4.5f), size = new Vector2(0.025f, 0.04f), lifetime = new Vector2(0.12f, 0.18f),
                color = Color.white, color2 = RamLime
            },
            new SpiralParticleStyle
            {
                name = "Partículas que se tragan", sprite = "Chispa", rate = 50f, radius = new Vector2(1.4f, 2f),
                height = new Vector2(0f, 0.3f), tangentialSpeed = new Vector2(2f, 4f), outwardSpeed = new Vector2(-6f, -4.5f),
                upSpeed = new Vector2(0f, 0.6f), size = new Vector2(0.05f, 0.08f), lifetime = new Vector2(0.3f, 0.4f),
                color = Color.white, color2 = RamLime
            }
        };

        /// <summary>Al salir cada embestida: la grieta de salida y trozos de suelo.</summary>
        private static SpiralParticleStyle[] RamLaunch() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Grieta de salida", sprite = "GrietaInicio00", count = 1, radius = Vector2.zero, height = new Vector2(0.02f, 0.02f),
                tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = Vector2.zero, alignToDirection = true,
                rotation = new Vector2(CrackBackward - 15f, CrackBackward + 15f), size = new Vector2(2.2f, 2.8f),
                lifetime = new Vector2(1.4f, 1.4f), color = CrackDark, color2 = CrackDark, alpha = 0.85f
            },
            RamDebrisBurst("Trozos de salida", 12, 1.5f, 3f, -0.4f)
        };

        /// <summary>
        /// Viñeta 2, mientras embiste (el cuenco delante va en la punta, RamTip): trozos de suelo y alguna Zbam pequeña detrás y
        /// las rayas de píxel que quedan atrás (las de su aura de antes).
        /// </summary>
        private static SpecialParticleStyle[] RamRush() => new[]
        {
            new SpecialParticleStyle
            {
                name = "Trozos de suelo", sprite = "Trozo", rate = 35f, spawnRadius = new Vector2(0.2f, 0.5f), height = new Vector2(0.02f, 0.1f),
                coneAngle = 35f, speed = new Vector2(3f, 5f), inheritVelocity = -0.15f, forwardOffset = new Vector2(-0.5f, -0.2f),
                size = new Vector2(0.08f, 0.16f), lifetime = new Vector2(0.6f, 0.9f), color = RamDebris, color2 = RamPale
            },
            new SpecialParticleStyle
            {
                name = "Zbam detrás", sprite = "ZbamSube", rate = 8f, spawnRadius = new Vector2(0.1f, 0.3f), height = new Vector2(0.3f, 0.6f),
                coneAngle = 20f, speed = new Vector2(0.5f, 1.2f), inheritVelocity = -0.1f, forwardOffset = new Vector2(-0.7f, -0.5f),
                size = new Vector2(0.2f, 0.3f), lifetime = new Vector2(0.3f, 0.4f), color = Color.white, color2 = RamPale
            },
            new SpecialParticleStyle
            {
                name = "Rayas", emitter = VfxType.Dash, rate = 90f, spawnRadius = new Vector2(0.2f, 0.5f), height = new Vector2(0.1f, 0.5f),
                coneAngle = 90f, speed = new Vector2(0.2f, 0.6f), inheritVelocity = -0.3f, size = Vector2.zero, lifetime = Vector2.zero,
                color = RamLime, color2 = Color.white
            }
        };

        /// <summary>
        /// Viñeta 2 (2026-10-20, pedido por el usuario en vez de la media luna): el efecto de la punta en el acelerón (cuencos
        /// ImpactPlane con la hoja ImpactShotValo2) delante de la peonza, abierto hacia ella y en verde lima y blanco, solo
        /// mientras embiste: el fuego de algo que entra en la atmósfera muy rápido. Tres cuencos que giran a distinta velocidad.
        /// </summary>
        private static void RamTip(BurstTipStyle tip, BurstTipStyle accel)
        {
            tip.mesh = accel != null ? accel.mesh : null;
            tip.material = accel != null ? accel.material : null;
            tip.tilesX = 3;
            tip.tilesY = 3;
            tip.firstFrame = 0;
            tip.frames = 9;
            tip.cycle = 0.2f;
            tip.size = 2.1f;
            tip.scale = Vector2.one;
            tip.height = 0.25f;
            tip.alpha = 1f;
            tip.lighten = 0f;
            tip.fade = new Vector2(0.04f, 0.12f);
            tip.pointForward = true;
            // El fondo del cuenco por delante del borde de la peonza (con menos, ella lo tapa) y largo hacia atrás, envolviéndola
            tip.forwardOffset = 1.1f;
            tip.onlyWhileRushing = true;
            tip.particleSprite = "";
            tip.ground = new TipGroundSprite();
            tip.groundLayers.Clear();
            tip.cups.Clear();
            tip.cups.Add(new BurstTipCup { angle = 0f, spinSpeed = 360f, scale = new Vector2(1f, 1.2f), playerColor = false, color = Color.white });
            tip.cups.Add(new BurstTipCup { angle = 120f, spinSpeed = -280f, scale = new Vector2(1.1f, 1.5f), playerColor = false, color = RamLime });
            tip.cups.Add(new BurstTipCup { angle = 240f, spinSpeed = 220f, scale = new Vector2(1.25f, 1.8f), playerColor = false, color = new Color(RamLime.r, RamLime.g, RamLime.b, 0.85f) });
        }

        /// <summary>Viñeta 2: rastro de grietas del usuario solo mientras embiste, a lo largo del camino.</summary>
        private static ScorchStyle RamCracks() => new ScorchStyle
        {
            sprite = "", spriteVariants = new List<string>(LineCrackVariants), onlyWhileRushing = true, alignToMotion = true,
            rotation = new Vector2(CrackBackward - 12f, CrackBackward + 12f), spacing = 0.9f, idleInterval = 0f,
            size = new Vector2(1.6f, 2.2f), lifetime = 1.4f, color = CrackDark, alpha = 0.85f, height = 0.02f
        };

        /// <summary>
        /// Viñeta 3, el golpe: un tajo curvo grande que envuelve al rival (mirando hacia donde sale despedido), el tajo animado,
        /// Zbam, trozos de suelo y chispas.
        /// </summary>
        private static SpiralParticleStyle[] RamHit() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Tajo curvo", sprite = "MediaLuna", count = 1, radius = Vector2.zero, height = new Vector2(0.3f, 0.3f),
                tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = Vector2.zero, forwardOffset = 0.3f,
                alignToDirection = true, rotation = new Vector2(CrescentForward, CrescentForward), size = new Vector2(2.8f, 2.8f),
                lifetime = new Vector2(0.25f, 0.25f), color = Color.white, color2 = Color.white, alpha = 0.95f
            },
            new SpiralParticleStyle
            {
                name = "Tajo animado", sprite = "Tajo", count = 2, radius = new Vector2(0f, 0.1f), height = new Vector2(0.3f, 0.5f),
                tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = Vector2.zero, size = new Vector2(1.6f, 2.2f),
                lifetime = new Vector2(0.3f, 0.3f), color = Color.white, color2 = RamPale
            },
            RamZbamBurst("Zbam del golpe", 5, 2f, 4f),
            RamDebrisBurst("Trozos del golpe", 14, 2.5f, 5f, 0f),
            new SpiralParticleStyle
            {
                name = "Chispas", sprite = "Chispa", count = 16, radius = new Vector2(0.2f, 0.4f), height = new Vector2(0.05f, 0.25f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(4f, 8f), upSpeed = new Vector2(1.5f, 3f),
                size = new Vector2(0.06f, 0.1f), lifetime = new Vector2(0.3f, 0.5f), color = Color.white, color2 = RamLime
            }
        };

        /// <summary>Peonza lanzada por la Embestida que choca contra la pared: Zbam y trozos.</summary>
        private static SpiralParticleStyle[] RamWallHit() => new[]
        {
            RamZbamBurst("Zbam contra la pared", 4, 1.5f, 3f),
            RamDebrisBurst("Trozos contra la pared", 10, 2f, 4f, 0f)
        };

        /// <summary>Círculo de Zbam (Zbam2 y Zbam3) que saltan hacia fuera.</summary>
        private static SpiralParticleStyle RamZbamBurst(string name, int count, float minSpeed, float maxSpeed) => new SpiralParticleStyle
        {
            name = name, spriteVariants = new List<string> { "ZbamSube", "Zbam3Sube" }, count = count, radius = new Vector2(0.2f, 0.4f),
            height = new Vector2(0.2f, 0.5f), tangentialSpeed = new Vector2(0.5f, 1f), outwardSpeed = new Vector2(minSpeed, maxSpeed),
            upSpeed = new Vector2(1f, 2f), size = new Vector2(0.35f, 0.6f), lifetime = new Vector2(0.3f, 0.4f),
            color = Color.white, color2 = RamPale
        };

        /// <summary>Círculo de trozos de suelo que saltan, giran y caen (forward: metros hacia delante en la marcha; negativo = detrás).</summary>
        private static SpiralParticleStyle RamDebrisBurst(string name, int count, float minSpeed, float maxSpeed, float forward) => new SpiralParticleStyle
        {
            name = name, sprite = "Trozo", count = count, radius = new Vector2(0.2f, 0.4f), height = new Vector2(0.05f, 0.2f),
            tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(minSpeed, maxSpeed), upSpeed = new Vector2(3f, 5.5f),
            forwardOffset = forward, size = new Vector2(0.1f, 0.18f), lifetime = new Vector2(0.6f, 0.9f), color = RamDebris, color2 = RamPale
        };

        // Salto: el rosa de su asset (Leap) y uno claro
        private static readonly Color LeapPink = new Color(1f, 0.35f, 0.45f);
        private static readonly Color LeapPale = new Color(1f, 0.82f, 0.86f);

        /// <summary>
        /// Salto (K8, 2026-10-21; boceto del usuario en cuatro viñetas). Se empieza copiando la plantilla común (con los círculos
        /// finos de CircleImpact04, como la Embestida) y se le añade lo suyo: 1) carga: círculos que se cierran mientras bajan
        /// desde arriba hasta el suelo (la carga se concentra en el suelo) y un aro de picos en la base; 2) despegue: bocanada de
        /// humo, una línea que sube, chispas y escombros; 3) vuelo: dos líneas en espiral que giran y dejan su rastro, los
        /// cuencos de la Embestida delante y apuntando adonde va a caer, semicírculos que se abren y se quedan atrás, chispas,
        /// humo y escombros; en el suelo, una marca que se cierra justo al caer; 4) caída: aro de picos que crece, onda, grieta
        /// al azar, humo que sube, el golpe, chispas, escombros y semicírculos. Rosa (su color) y blanco. Conserva sus
        /// partículas de antes (Replace Aura apagado; la onda pixelada de la caída ya no sale). Solo añade la entrada si falta
        /// y rellena lo que esté vacío.
        /// </summary>
        private static string SetupLeap(VfxLibrary library)
        {
            SpecialVfxStyle leap = library.GetSpecialEffects(SpecialAbilityType.Leap);
            bool added = leap == null;
            if (added)
            {
                leap = new SpecialVfxStyle { type = SpecialAbilityType.Leap, replaceAura = false, windupSustain = 0f };
                leap.tip.cups.Clear();
                leap.tip.particleSprite = "";
                leap.tip.ground = new TipGroundSprite();
                leap.overlay = new SpecialOverlayStyle
                {
                    color = LeapPink, hotColor = Color.white, baseRim = 0.2f, pulseRim = new Vector2(0.5f, 1.2f),
                    pulseFlash = new Vector2(0f, 0.08f), pulseInterval = new Vector2(0.15f, 0.35f), pulseDecay = 0.2f
                };
                library.specialEffects.Add(leap);
            }

            leap.tip ??= new BurstTipStyle();
            if (leap.tip.cups.Count == 0) LeapTip(leap.tip, library);
            leap.spiralTrail ??= new SpiralTrailStyle();
            if (leap.spiralTrail.material == null) leap.spiralTrail.material = AssetDatabase.LoadAssetAtPath<Material>(BasicMaterialPath);

            // La plantilla común como base y lo propio detrás
            leap.windup ??= new List<SpiralParticleStyle>();
            if (leap.windup.Count == 0)
            {
                leap.windup.AddRange(GenericWindup());
                foreach (SpiralParticleStyle style in leap.windup)
                    if (style.sprite == "CircleImpact02Cierra" || style.sprite == "FP_CircleImpactCierra") style.sprite = "CircleImpact04Cierra";
                leap.windup.AddRange(WindupOnly(LeapWindup()));
            }
            leap.activationBurst ??= new List<SpiralParticleStyle>();
            if (leap.activationBurst.Count == 0)
            {
                leap.activationBurst.AddRange(GenericBurst());
                foreach (SpiralParticleStyle style in leap.activationBurst)
                    if (style.sprite == "CircleImpact02Suelo")
                    {
                        style.sprite = "CircleImpact04Anillo";
                        style.count = 2;
                    }
                leap.activationBurst.AddRange(LeapTakeOff());
            }
            leap.rush ??= new List<SpecialParticleStyle>();
            if (leap.rush.Count == 0) leap.rush.AddRange(LeapFlight());
            leap.targetMark ??= new List<SpiralParticleStyle>();
            if (leap.targetMark.Count == 0) leap.targetMark.AddRange(LeapMark());
            leap.release ??= new List<SpiralParticleStyle>();
            if (leap.release.Count == 0) leap.release.AddRange(LeapLanding());
            leap.pushHit ??= new List<SpiralParticleStyle>();
            if (leap.pushHit.Count == 0) leap.pushHit.AddRange(LeapHit());
            leap.launchedTrail ??= new List<SpecialParticleStyle>();
            if (leap.launchedTrail.Count == 0) leap.launchedTrail.AddRange(GenericHitTrail());
            leap.launchedWallHit ??= new List<SpiralParticleStyle>();
            if (leap.launchedWallHit.Count == 0) leap.launchedWallHit.AddRange(LeapWallHit());
            return "Salto" + (added ? " (nuevo)" : "") + (leap.spiralTrail.material == null ? " (falta VFX_TrailBasic)" : "");
        }

        /// <summary>
        /// Viñeta 3: los cuencos de la Embestida (copiados de su entrada, con lo que haya ajustado el usuario) delante de la
        /// peonza, en rosa y blanco, mientras vuela y apuntando adonde va a caer: cada vez más inclinados al bajar.
        /// </summary>
        private static void LeapTip(BurstTipStyle tip, VfxLibrary library)
        {
            SpecialVfxStyle ram = library.GetSpecialEffects(SpecialAbilityType.Ram);
            if (ram != null && ram.tip != null && ram.tip.cups.Count > 0)
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(ram.tip), tip);
            else
                RamTip(tip, library.burstTip);
            tip.pointForward = true;
            tip.onlyWhileRushing = true;
            tip.aimAtTarget = true;
            for (int i = 0; i < tip.cups.Count; i++)
            {
                float alpha = tip.cups[i].color.a;
                Color color = i == 0 ? Color.white : LeapPink;
                tip.cups[i].playerColor = false;
                tip.cups[i].color = new Color(color.r, color.g, color.b, alpha);
            }
        }

        /// <summary>
        /// Viñeta 1, la carga: círculos finos que se cierran mientras bajan desde arriba hasta el suelo (la carga se concentra en
        /// el suelo, pedido por el usuario) y un aro de picos en la base que se encoge.
        /// </summary>
        private static SpiralParticleStyle[] LeapWindup() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Círculos que bajan", sprite = "CircleImpact04Cierra", rate = 12f, radius = Vector2.zero, height = new Vector2(1.3f, 2f),
                tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = new Vector2(-6.5f, -4.5f),
                size = new Vector2(2.6f, 3.4f), lifetime = new Vector2(0.3f, 0.35f), color = Color.white, color2 = LeapPink, alpha = 0.9f
            },
            new SpiralParticleStyle
            {
                name = "Aro de picos (base)", sprite = "AroPicos", rate = 8f, radius = Vector2.zero, height = new Vector2(0.03f, 0.03f),
                tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = Vector2.zero, size = new Vector2(1.5f, 1.9f),
                lifetime = new Vector2(0.15f, 0.2f), color = LeapPink, color2 = Color.white, alpha = 0.9f
            }
        };

        /// <summary>Viñeta 2, el despegue: bocanada de humo en el suelo, una línea que sube detrás de ella, chispas y escombros.</summary>
        private static SpiralParticleStyle[] LeapTakeOff() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Bocanada del despegue", sprite = "HumoViento", count = 14, radius = new Vector2(0.3f, 0.5f),
                height = new Vector2(0.05f, 0.2f), tangentialSpeed = new Vector2(0.3f, 0.8f), outwardSpeed = new Vector2(2f, 4f),
                upSpeed = new Vector2(0.3f, 1f), size = new Vector2(0.6f, 0.9f), lifetime = new Vector2(0.7f, 0.9f),
                color = Color.white, color2 = LeapPale
            },
            new SpiralParticleStyle
            {
                name = "Línea que sube", sprite = "Linea", count = 3, radius = new Vector2(0f, 0.15f), height = new Vector2(0.1f, 0.3f),
                tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = new Vector2(7f, 9f),
                size = new Vector2(0.06f, 0.09f), lifetime = new Vector2(0.22f, 0.28f), color = Color.white, color2 = LeapPink
            },
            LeapSparks("Chispas del despegue", 18, 2f, 4f),
            LeapDebrisBurst("Escombros del despegue", 10, 1.5f, 3f)
        };

        /// <summary>
        /// Viñeta 3, mientras vuela (las espirales van en Spiral Trail y los cuencos en la punta): semicírculos que se abren y se
        /// quedan atrás, chispas que caen, humo y escombros pequeños y variados.
        /// </summary>
        private static SpecialParticleStyle[] LeapFlight() => new[]
        {
            new SpecialParticleStyle
            {
                name = "Semicírculos que se abren", sprite = "CircleImpact04", rate = 9f, spawnRadius = new Vector2(0f, 0.2f),
                height = new Vector2(0.1f, 0.4f), coneAngle = 0f, speed = Vector2.zero, inheritVelocity = 0f,
                forwardOffset = new Vector2(-0.3f, -0.1f), size = new Vector2(0.7f, 1f), lifetime = new Vector2(0.25f, 0.3f),
                color = Color.white, color2 = LeapPale, alpha = 0.9f
            },
            new SpecialParticleStyle
            {
                name = "Chispas", sprite = "Chispa", rate = 35f, spawnRadius = new Vector2(0.2f, 0.5f), height = new Vector2(0f, 0.4f),
                coneAngle = 90f, speed = new Vector2(0.5f, 1.5f), inheritVelocity = -0.1f, size = new Vector2(0.05f, 0.08f),
                lifetime = new Vector2(0.3f, 0.45f), color = Color.white, color2 = LeapPink
            },
            new SpecialParticleStyle
            {
                name = "Humo", sprite = "NubeComic", rate = 10f, spawnRadius = new Vector2(0.1f, 0.3f), height = new Vector2(0f, 0.3f),
                coneAngle = 60f, speed = new Vector2(0.2f, 0.5f), inheritVelocity = -0.1f, size = new Vector2(0.25f, 0.4f),
                lifetime = new Vector2(0.35f, 0.5f), color = Color.white, color2 = LeapPale, alpha = 0.8f
            },
            new SpecialParticleStyle
            {
                name = "Escombros", sprite = "Trozo", rate = 12f, spawnRadius = new Vector2(0.2f, 0.4f), height = new Vector2(0f, 0.2f),
                coneAngle = 60f, speed = new Vector2(1f, 2f), inheritVelocity = -0.15f, size = new Vector2(0.07f, 0.12f),
                lifetime = new Vector2(0.5f, 0.7f), color = RamDebris, color2 = LeapPale
            }
        };

        /// <summary>
        /// Viñeta 3, extra: la marca donde va a caer, al despegar. Lifetime 0: duran todo el vuelo, así el círculo (del ancho de
        /// la onda) acaba de cerrarse y el aro de picos de encogerse justo al caer.
        /// </summary>
        private static SpiralParticleStyle[] LeapMark() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Marca de caída", sprite = "CircleImpact04Cierra", count = 1, radius = Vector2.zero, height = new Vector2(0.03f, 0.03f),
                tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = Vector2.zero, size = new Vector2(8.3f, 8.3f),
                lifetime = Vector2.zero, color = LeapPink, color2 = LeapPink, alpha = 0.6f
            },
            new SpiralParticleStyle
            {
                name = "Picos de la marca", sprite = "AroPicos", count = 1, radius = Vector2.zero, height = new Vector2(0.03f, 0.03f),
                tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = Vector2.zero, size = new Vector2(1.4f, 1.4f),
                lifetime = Vector2.zero, color = Color.white, color2 = Color.white, alpha = 0.7f
            }
        };

        /// <summary>
        /// Viñeta 4, al caer (sustituye al estallido común y su onda pixelada): aro de picos que crece por el suelo (la elipse
        /// con picos del boceto), onda, una grieta al azar, humo que sube en bocanadas, el golpe, chispas, escombros y
        /// semicírculos.
        /// </summary>
        private static SpiralParticleStyle[] LeapLanding() => new[]
        {
            ShockCircle("Aro de picos", "AroPicosCrece", 9f, 0.04f, 0.5f, LeapPink, 0.95f),
            ShockCircle("Onda", "CircleImpact04Anillo", 9.75f, 0.06f, 0.4f, Color.white, 0.9f, count: 2),
            new SpiralParticleStyle
            {
                name = "Grieta", count = 1, radius = Vector2.zero, height = new Vector2(0.02f, 0.02f), tangentialSpeed = Vector2.zero,
                outwardSpeed = Vector2.zero, upSpeed = Vector2.zero, size = new Vector2(3f, 4.5f), lifetime = new Vector2(1.4f, 1.4f),
                color = CrackDark, color2 = CrackDark, alpha = 0.85f, spriteVariants = new List<string>(CrackVariants)
            },
            new SpiralParticleStyle
            {
                name = "Humo que sube", sprite = "HumoViento", count = 22, radius = new Vector2(0.4f, 0.8f), height = new Vector2(0.1f, 0.4f),
                tangentialSpeed = new Vector2(0.3f, 1f), outwardSpeed = new Vector2(2f, 4.5f), upSpeed = new Vector2(1.5f, 3f),
                size = new Vector2(0.8f, 1.2f), lifetime = new Vector2(0.9f, 1.2f), color = Color.white, color2 = LeapPale
            },
            new SpiralParticleStyle
            {
                name = "Humo bajo", sprite = "HumoViento", count = 14, radius = new Vector2(0.4f, 0.6f), height = new Vector2(0.05f, 0.2f),
                tangentialSpeed = new Vector2(0.3f, 1f), outwardSpeed = new Vector2(5f, 9f), upSpeed = new Vector2(0.1f, 0.4f),
                size = new Vector2(0.9f, 1.3f), lifetime = new Vector2(1f, 1.3f), color = Color.white, color2 = LeapPale
            },
            ShockCircle("Golpe", "Impact10", 2.6f, 0.4f, 0f, LeapPale, 1f),
            LeapSparks("Chispas", 30, 4f, 8f),
            LeapDebrisBurst("Escombros", 18, 2.5f, 5f),
            LeapCrescents("Semicírculos", 4)
        };

        /// <summary>Cada rival alcanzado por la caída: chispas, escombros y un semicírculo.</summary>
        private static SpiralParticleStyle[] LeapHit() => new[]
        {
            LeapSparks("Chispas", 14, 3f, 6f),
            LeapDebrisBurst("Escombros", 8, 2f, 4f),
            LeapCrescents("Semicírculo", 1)
        };

        /// <summary>Peonza lanzada por el Salto que choca contra la pared: escombros y chispas.</summary>
        private static SpiralParticleStyle[] LeapWallHit() => new[]
        {
            LeapDebrisBurst("Escombros contra la pared", 10, 2f, 4f),
            LeapSparks("Chispas contra la pared", 10, 2f, 4f)
        };

        private static SpiralParticleStyle LeapSparks(string name, int count, float minSpeed, float maxSpeed) => new SpiralParticleStyle
        {
            name = name, sprite = "Chispa", count = count, radius = new Vector2(0.2f, 0.4f), height = new Vector2(0.05f, 0.3f),
            tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(minSpeed, maxSpeed), upSpeed = new Vector2(2.5f, 4.5f),
            size = new Vector2(0.05f, 0.09f), lifetime = new Vector2(0.35f, 0.55f), color = Color.white, color2 = LeapPink
        };

        private static SpiralParticleStyle LeapDebrisBurst(string name, int count, float minSpeed, float maxSpeed)
        {
            SpiralParticleStyle debris = RamDebrisBurst(name, count, minSpeed, maxSpeed, 0f);
            debris.color2 = LeapPale;
            return debris;
        }

        /// <summary>Semicírculos que se abren (CircleImpact04 de pie) un poco por encima del suelo y saliendo despacio.</summary>
        private static SpiralParticleStyle LeapCrescents(string name, int count) => new SpiralParticleStyle
        {
            name = name, sprite = "CircleImpact04", count = count, radius = new Vector2(0.8f, 1.2f), height = new Vector2(0.3f, 0.6f),
            tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(1f, 2f), upSpeed = new Vector2(0.2f, 0.6f),
            size = new Vector2(1f, 1.4f), lifetime = new Vector2(0.3f, 0.35f), color = Color.white, color2 = LeapPale, alpha = 0.9f
        };

        // Rebote: el rosa de su asset (Bounce) y uno claro
        private static readonly Color BouncePink = new Color(1f, 0.45f, 0.75f);
        private static readonly Color BouncePale = new Color(1f, 0.85f, 0.94f);
        private const string BounceGroundMaterialPath = "Assets/VFX/Materials/VFX_BounceGround.mat";
        private const string SpikeRingTexturePath = "Assets/VFX/Textures/Impacts/AroPicos.png";
        private const string SpeedLinesTexturePath = "Assets/VFX/Textures/Impacts/LineasVelocidad.png";

        /// <summary>
        /// Rebote (K8, 2026-10-21; boceto del usuario en cuatro partes). 1-2) Al activarlo, la plantilla común (círculos finos) y
        /// líneas que suben; la burbuja (cúpula en esfera entera) crece durante la carga. Cargada: la burbuja rosa translúcida con
        /// líneas de velocidad que la recorren inclinadas sobre un eje que se bambolea y un aro de picos en el suelo que gira y
        /// late. A1-A2) En cada rebote, una Zbam (la del anillo de la peonza golpeada) se abre en la burbuja desde el punto del
        /// golpe, con destello y un hinchazo; círculos, un estallido y chispas en el lado del rival, que sale despedido con su
        /// estela. B1-B3) Al terminar, la burbuja se infla un poco y se desvanece, y salen trozos, humo y líneas. Quita su aura
        /// de antes. Solo añade la entrada si falta y rellena lo que esté vacío.
        /// </summary>
        private static string SetupBounce(VfxLibrary library)
        {
            SpecialVfxStyle bounce = library.GetSpecialEffects(SpecialAbilityType.Bounce);
            bool added = bounce == null;
            if (added)
            {
                bounce = new SpecialVfxStyle { type = SpecialAbilityType.Bounce, replaceAura = true, windupSustain = 0f };
                bounce.tip.cups.Clear();
                bounce.tip.particleSprite = "";
                bounce.tip.ground = new TipGroundSprite();
                bounce.overlay = new SpecialOverlayStyle
                {
                    color = BouncePink, hotColor = Color.white, baseRim = 0.2f, pulseRim = new Vector2(0.4f, 1f),
                    pulseFlash = new Vector2(0f, 0.05f), pulseInterval = new Vector2(0.2f, 0.45f), pulseDecay = 0.25f
                };
                bounce.dome = BounceBubble();
                bounce.hitTrailTime = 0.45f;
                library.specialEffects.Add(bounce);
            }

            bounce.dome ??= BounceBubble();
            Shader domeShader = Shader.Find(ChargeDomeShaderName);
            if (bounce.dome.material == null && domeShader != null) bounce.dome.material = GetMaterial(ChargeDomeMaterialPath, domeShader);
            if (bounce.dome.linesTexture == null) bounce.dome.linesTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SpeedLinesTexturePath);
            // La Zbam de la peonza golpeada (la misma textura que BladeOverlay)
            if (bounce.dome.hitTexture == null)
                bounce.dome.hitTexture = library.bladeOverlay.ringTexture != null
                    ? library.bladeOverlay.ringTexture
                    : AssetDatabase.LoadAssetAtPath<Texture2D>(WallcoeurFolder + "03_Texture/Zbam.png");

            // Aura en el suelo: el aro de picos tumbado, que gira y late
            bounce.tip ??= new BurstTipStyle();
            Shader flipbook = Shader.Find(TipShaderName);
            if (bounce.tip.ground.material == null && flipbook != null)
            {
                Material ground = GetMaterial(BounceGroundMaterialPath, flipbook);
                ground.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SpikeRingTexturePath));
                ground.SetFloat("_TilesX", 1f);
                ground.SetFloat("_TilesY", 1f);
                EditorUtility.SetDirty(ground);
                bounce.tip.ground = new TipGroundSprite
                {
                    material = ground, tilesX = 1, tilesY = 1, frames = 1, cycle = 1f, size = 2.9f, height = 0.02f, spinSpeed = 40f,
                    color = BouncePink, alpha = 0.85f, pulseRate = 0.8f, pulseScale = 0.08f, pulseAlpha = 0.3f
                };
                bounce.tip.fade = new Vector2(0.1f, 0.3f);
            }

            bounce.windup ??= new List<SpiralParticleStyle>();
            if (bounce.windup.Count == 0)
            {
                bounce.windup.AddRange(GenericWindup());
                foreach (SpiralParticleStyle style in bounce.windup)
                    if (style.sprite == "CircleImpact02Cierra" || style.sprite == "FP_CircleImpactCierra") style.sprite = "CircleImpact04Cierra";
                bounce.windup.AddRange(WindupOnly(BounceWindup()));
            }
            bounce.activationBurst ??= new List<SpiralParticleStyle>();
            if (bounce.activationBurst.Count == 0)
            {
                bounce.activationBurst.AddRange(GenericBurst());
                foreach (SpiralParticleStyle style in bounce.activationBurst)
                    if (style.sprite == "CircleImpact02Suelo")
                    {
                        style.sprite = "CircleImpact04Anillo";
                        style.count = 2;
                    }
            }
            bounce.pushHit ??= new List<SpiralParticleStyle>();
            if (bounce.pushHit.Count == 0) bounce.pushHit.AddRange(BounceHit());
            bounce.hitTrail ??= new List<SpecialParticleStyle>();
            if (bounce.hitTrail.Count == 0) bounce.hitTrail.AddRange(BounceTrail());
            bounce.endBurst ??= new List<SpiralParticleStyle>();
            if (bounce.endBurst.Count == 0) bounce.endBurst.AddRange(BounceEnd());
            return "Rebote" + (added ? " (nuevo)" : "") + (bounce.dome.material == null ? " (falta el shader de la cúpula)" : "");
        }

        /// <summary>
        /// La burbuja: esfera entera rosa translúcida con el borde fino y el contorno gris, líneas de velocidad que la recorren
        /// sobre un eje inclinado 35° que se bambolea; crece desde pequeña en la carga y al acabar se infla un poco y se
        /// desvanece. En cada rebote, la Zbam (hitTexture) desde el punto del golpe.
        /// </summary>
        private static ChargeDomeStyle BounceBubble() => new ChargeDomeStyle
        {
            sphere = true, radius = 1.1f, heightScale = 1f, height = 0f, growEase = 2.5f, startRadius = 0.15f,
            funnelPhase = 0f, topCut = 0f, insideAlpha = 0f,
            bodyColor = new Color(1f, 0.6f, 0.85f, 0.16f), rimColor = new Color(BouncePink.r, BouncePink.g, BouncePink.b, 0.9f),
            outlineColor = new Color(0.47f, 0.47f, 0.47f, 0.8f), swirlColor = new Color(1f, 1f, 1f, 0.8f),
            swirlColor2 = new Color(BouncePink.r, BouncePink.g, BouncePink.b, 0.5f), rimWidth = 0.16f, outlineWidth = 0.04f,
            linesTiling = new Vector2(3f, 1f), linesSpeed = 0.7f, linesTwist = 0.3f, axisTilt = 35f, axisPrecess = 0.25f,
            swirlLines = 0f, swirlTwist = 0f, swirlSpeed = 0f, swirlWidth = 0f,
            shakeFrom = 1f, shakeScale = 0f, shakeRate = 0f, popTime = 0.35f, popScale = 1.15f, popFlash = 0.4f,
            hitColor = new Color(1f, 0.92f, 0.97f, 1f), hitShadowColor = new Color(0.3f, 0.3f, 0.3f, 0.6f),
            hitScale = new Vector2(0.15f, 1.1f), hitDuration = 0.35f, hitFlash = 0.5f, hitBump = 0.12f
        };

        /// <summary>Viñetas 1-2, la carga: líneas que suben alrededor desde el suelo y chispas que se tragan.</summary>
        private static SpiralParticleStyle[] BounceWindup() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Líneas que suben", sprite = "Linea", rate = 60f, radius = new Vector2(0.7f, 1.2f), height = new Vector2(0f, 0.2f),
                tangentialSpeed = new Vector2(0.5f, 1f), outwardSpeed = new Vector2(-0.6f, -0.2f), upSpeed = new Vector2(3f, 4.5f),
                size = new Vector2(0.03f, 0.05f), lifetime = new Vector2(0.15f, 0.22f), color = Color.white, color2 = BouncePink
            },
            new SpiralParticleStyle
            {
                name = "Chispas que se tragan", sprite = "Chispa", rate = 40f, radius = new Vector2(1.4f, 1.9f), height = new Vector2(0f, 0.8f),
                tangentialSpeed = new Vector2(2f, 3.5f), outwardSpeed = new Vector2(-5.5f, -4f), upSpeed = new Vector2(0f, 0.5f),
                size = new Vector2(0.05f, 0.08f), lifetime = new Vector2(0.3f, 0.4f), color = Color.white, color2 = BouncePink
            }
        };

        /// <summary>A1-A2, cada rebote en el lado del rival: dos círculos que se abren, un estallido de cómic, chispas y Zbam.</summary>
        private static SpiralParticleStyle[] BounceHit() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Círculos", sprite = "CircleImpact04", count = 2, radius = new Vector2(0f, 0.15f), height = new Vector2(0.2f, 0.45f),
                tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = Vector2.zero, size = new Vector2(1.3f, 1.8f),
                lifetime = new Vector2(0.3f, 0.35f), color = Color.white, color2 = BouncePale, alpha = 0.95f
            },
            new SpiralParticleStyle
            {
                name = "Estallido", sprite = "Impact10", count = 1, radius = Vector2.zero, height = new Vector2(0.35f, 0.35f),
                tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = Vector2.zero, size = new Vector2(1.6f, 1.6f),
                lifetime = Vector2.zero, color = BouncePale, color2 = BouncePale
            },
            new SpiralParticleStyle
            {
                name = "Chispas", sprite = "Chispa", count = 14, radius = new Vector2(0.1f, 0.3f), height = new Vector2(0.1f, 0.4f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(3f, 6f), upSpeed = new Vector2(1.5f, 3f),
                size = new Vector2(0.05f, 0.09f), lifetime = new Vector2(0.3f, 0.45f), color = Color.white, color2 = BouncePink
            },
            new SpiralParticleStyle
            {
                name = "Zbam", spriteVariants = new List<string> { "ZbamSube", "Zbam3Sube" }, count = 3, radius = new Vector2(0.2f, 0.4f),
                height = new Vector2(0.2f, 0.5f), tangentialSpeed = new Vector2(0.5f, 1f), outwardSpeed = new Vector2(1.5f, 3f),
                upSpeed = new Vector2(1f, 2f), size = new Vector2(0.35f, 0.55f), lifetime = new Vector2(0.3f, 0.4f),
                color = Color.white, color2 = BouncePale
            }
        };

        /// <summary>A2: la peonza rebotada deja humo y chispas rosas mientras sale disparada.</summary>
        private static SpecialParticleStyle[] BounceTrail()
        {
            SpecialParticleStyle[] trail = GenericHitTrail();
            foreach (SpecialParticleStyle style in trail)
            {
                style.powerTint = 0f;
                style.color2 = style.sprite == "Chispa" ? BouncePink : BouncePale;
            }
            return trail;
        }

        /// <summary>
        /// B1-B3, al terminar: la burbuja se rompe en trozos rosas y blancos que saltan y caen, humo que se disuelve, líneas que
        /// salen y dos círculos que se abren.
        /// </summary>
        private static SpiralParticleStyle[] BounceEnd() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Trozos de la burbuja", sprite = "Trozo", count = 26, radius = new Vector2(0.8f, 1.1f), height = new Vector2(0.2f, 1.7f),
                tangentialSpeed = new Vector2(0f, 0.5f), outwardSpeed = new Vector2(1.5f, 3.5f), upSpeed = new Vector2(1.5f, 3.5f),
                size = new Vector2(0.12f, 0.22f), lifetime = new Vector2(0.6f, 0.9f), color = BouncePale, color2 = Color.white
            },
            new SpiralParticleStyle
            {
                name = "Humo", sprite = "HumoViento", count = 12, radius = new Vector2(0.6f, 0.9f), height = new Vector2(0.2f, 1.2f),
                tangentialSpeed = new Vector2(0.2f, 0.6f), outwardSpeed = new Vector2(1f, 2.5f), upSpeed = new Vector2(0.3f, 1f),
                size = new Vector2(0.6f, 0.9f), lifetime = new Vector2(0.8f, 1.1f), color = Color.white, color2 = BouncePale
            },
            OutwardLines("Líneas que salen", 20, Color.white, BouncePink),
            ShockCircle("Círculos", "CircleImpact04Anillo", 3.6f, 0.05f, 0.4f, BouncePink, 0.9f, count: 2)
        };

        // Defensa: el azul de su asset (Defense) y uno claro
        private static readonly Color DefenseBlue = new Color(0.45f, 0.6f, 1f);
        private static readonly Color DefensePale = new Color(0.82f, 0.9f, 1f);

        /// <summary>
        /// Defensa (K8, 2026-10-23; boceto del usuario en tres viñetas, Fortress Core). Carga vertical y hacia fuera (0,5 s de
        /// efectos con 0,3 de juego, visualWindupTime): dos embudos con el borde en picos que suben desde el suelo (corona en
        /// zigzag), líneas y partículas que suben y salen, humo que sale y círculos que se abren en el suelo. Cargada (los 5 s):
        /// los embudos se quedan como aura vertical alrededor de la peonza, con líneas verticales que giran; a ras de suelo,
        /// vaho y partículas que giran y se abren hacia fuera (espirales), líneas que suben y un círculo que se abre de vez en
        /// cuando. Al embestirla, la Zbam en el aura y chispas (guardHit); al terminar, trozos, humo y líneas. Quita su aura de
        /// antes. Solo añade la entrada si falta y rellena lo que esté vacío.
        /// </summary>
        private static string SetupDefense(VfxLibrary library)
        {
            SpecialVfxStyle defense = library.GetSpecialEffects(SpecialAbilityType.Defense);
            bool added = defense == null;
            if (added)
            {
                defense = new SpecialVfxStyle
                {
                    type = SpecialAbilityType.Defense, replaceAura = true, windupSustain = 0f, visualWindupTime = 0.5f
                };
                defense.tip.cups.Clear();
                defense.tip.particleSprite = "";
                defense.tip.ground = new TipGroundSprite();
                defense.overlay = new SpecialOverlayStyle
                {
                    color = DefenseBlue, hotColor = Color.white, baseRim = 0.2f, pulseRim = new Vector2(0.4f, 0.9f),
                    pulseFlash = new Vector2(0f, 0.04f), pulseInterval = new Vector2(0.25f, 0.5f), pulseDecay = 0.3f
                };
                defense.dome = DefenseAura();
                defense.baseDome = DefenseCrown();
                library.specialEffects.Add(defense);
            }

            defense.dome ??= DefenseAura();
            defense.baseDome ??= DefenseCrown();
            AddDefenseInstability(defense);
            Shader domeShader = Shader.Find(ChargeDomeShaderName);
            Material domeMaterial = domeShader != null ? GetMaterial(ChargeDomeMaterialPath, domeShader) : null;
            if (defense.dome.material == null) defense.dome.material = domeMaterial;
            if (defense.baseDome.material == null) defense.baseDome.material = domeMaterial;
            // La Zbam de la peonza golpeada (la misma textura que BladeOverlay), en el embudo de dentro
            if (defense.dome.hitTexture == null)
                defense.dome.hitTexture = library.bladeOverlay.ringTexture != null
                    ? library.bladeOverlay.ringTexture
                    : AssetDatabase.LoadAssetAtPath<Texture2D>(WallcoeurFolder + "03_Texture/Zbam.png");

            defense.windup ??= new List<SpiralParticleStyle>();
            if (defense.windup.Count == 0) defense.windup.AddRange(WindupOnly(DefenseWindup()));
            defense.activationBurst ??= new List<SpiralParticleStyle>();
            if (defense.activationBurst.Count == 0) defense.activationBurst.AddRange(DefenseBurst());
            defense.swirl ??= new List<SpiralParticleStyle>();
            if (defense.swirl.Count == 0) defense.swirl.AddRange(DefenseSwirl());
            defense.guardHit ??= new List<SpiralParticleStyle>();
            if (defense.guardHit.Count == 0) defense.guardHit.AddRange(DefenseGuardHit());
            defense.endBurst ??= new List<SpiralParticleStyle>();
            if (defense.endBurst.Count == 0) defense.endBurst.AddRange(DefenseEnd());
            return "Defensa" + (added ? " (nueva)" : "") + (defense.dome.material == null ? " (falta el shader de la cúpula)" : "");
        }

        /// <summary>
        /// Aura de dentro: embudo alto y ceñido a la peonza (el del Tornado, que no pasa a cúpula), un poco más ancho arriba y
        /// abierto en picos, con líneas verticales que giran despacio. Sube desde el suelo en la carga y se queda; al terminar,
        /// se infla un poco y se desvanece.
        /// </summary>
        private static ChargeDomeStyle DefenseAura() => new ChargeDomeStyle
        {
            radius = 0.8f, heightScale = 1f, height = 0f, growEase = 1.5f, startRadius = 0.6f,
            funnelPhase = 1f, funnelMorph = 0.2f, funnelShape = new Vector3(0.9f, 1.15f, 1.9f),
            topCut = 0.22f, topSpikes = 9, topSpikesSpeed = 0.3f, insideAlpha = 0.3f,
            bodyColor = new Color(0.55f, 0.7f, 1f, 0.14f), rimColor = new Color(DefenseBlue.r, DefenseBlue.g, DefenseBlue.b, 0.85f),
            outlineColor = new Color(0.47f, 0.47f, 0.47f, 0.8f), swirlColor = new Color(1f, 1f, 1f, 0.8f),
            swirlColor2 = new Color(DefenseBlue.r, DefenseBlue.g, DefenseBlue.b, 0.5f), rimWidth = 0.2f, outlineWidth = 0.05f,
            linesTiling = new Vector2(5f, 2f), linesSpeed = 0f, linesTwist = 0f, axisTilt = 0f, axisPrecess = 0f,
            swirlLines = 7f, swirlTwist = 0.05f, swirlSpeed = 0.35f, swirlWidth = 0.06f,
            shakeFrom = 1f, shakeScale = 0f, shakeRate = 0f, popTime = 0.3f, popScale = 1.2f, popFlash = 0.5f,
            hitColor = new Color(0.9f, 0.95f, 1f, 1f), hitShadowColor = new Color(0.3f, 0.3f, 0.3f, 0.6f),
            hitScale = new Vector2(0.15f, 0.9f), hitDuration = 0.3f, hitFlash = 0.5f, hitBump = 0.08f
        };

        /// <summary>
        /// Huecos e inestabilidad del aura (2026-10-24, pedido del usuario): huecos de ruido que suben y laten, más arriba
        /// (el borde de arriba se deshace: no parece un cilindro), ondas, parpadeos y bandas desplazadas.
        /// </summary>
        private static void DefenseAuraInstability(ChargeDomeStyle d)
        {
            d.maskAmount = 0.25f;
            d.maskTiling = new Vector2(5f, 2f);
            d.maskScroll = new Vector2(0.08f, 0.5f);
            d.maskEdge = 0.05f;
            d.maskEdgeColor = new Color(0.85f, 0.92f, 1f, 0.9f);
            d.topErode = 0.55f;
            d.topErodeFrom = 0.45f;
            d.holePulse = 0.2f;
            d.holePulseInterval = new Vector2(0.15f, 0.5f);
            d.holePulseDecay = 0.2f;
            d.wobble = 0.04f;
            d.wobbleWaves = new Vector2(5f, 2f);
            d.wobbleSpeed = 5f;
            d.flickerRate = 0.8f;
            d.flickerAlpha = 0.35f;
            d.flickerTime = 0.05f;
            d.glitchRate = 0.6f;
            d.glitchBands = 8;
            d.glitchOffset = 0.04f;
            d.glitchTime = 0.08f;
        }

        /// <summary>La corona de la base, más tranquila: pocos huecos, algo de ondas y algún parpadeo.</summary>
        private static void DefenseCrownInstability(ChargeDomeStyle d)
        {
            d.maskAmount = 0.15f;
            d.maskTiling = new Vector2(7f, 1f);
            d.maskScroll = new Vector2(-0.1f, 0.3f);
            d.maskEdge = 0.05f;
            d.maskEdgeColor = new Color(1f, 1f, 1f, 0.9f);
            d.topErode = 0.3f;
            d.topErodeFrom = 0.5f;
            d.holePulse = 0.15f;
            d.wobble = 0.03f;
            d.flickerRate = 0.4f;
            d.flickerAlpha = 0.35f;
        }

        /// <summary>
        /// Pone la máscara y la inestabilidad de la Defensa en sus cúpulas si aún no las tienen (todo a 0: se crearon antes del
        /// 2026-10-24). Lo demás, como lo haya dejado el usuario.
        /// </summary>
        private static void AddDefenseInstability(SpecialVfxStyle defense)
        {
            bool Missing(ChargeDomeStyle d) => d.maskAmount <= 0f && d.topErode <= 0f && d.holePulse <= 0f && d.wobble <= 0f &&
                                               d.flickerRate <= 0f && d.glitchRate <= 0f;
            if (Missing(defense.dome)) DefenseAuraInstability(defense.dome);
            if (Missing(defense.baseDome)) DefenseCrownInstability(defense.baseDome);
        }

        /// <summary>
        /// Viñeta 2, en la base: corona en zigzag, un embudo bajo y más ancho que el de dentro con picos grandes que giran hacia
        /// el otro lado.
        /// </summary>
        private static ChargeDomeStyle DefenseCrown() => new ChargeDomeStyle
        {
            radius = 1.2f, heightScale = 0.45f, height = 0f, growEase = 2f, startRadius = 0.7f,
            funnelPhase = 1f, funnelMorph = 0.2f, funnelShape = new Vector3(0.95f, 1.2f, 1.2f),
            topCut = 0.45f, topSpikes = 14, topSpikesSpeed = -0.4f, insideAlpha = 0.25f,
            bodyColor = new Color(DefenseBlue.r, DefenseBlue.g, DefenseBlue.b, 0.22f), rimColor = new Color(1f, 1f, 1f, 0.9f),
            outlineColor = new Color(0.3f, 0.3f, 0.3f, 0.85f), swirlColor = new Color(1f, 1f, 1f, 0.6f),
            swirlColor2 = new Color(0f, 0f, 0f, 0f), rimWidth = 0.12f, outlineWidth = 0.04f,
            linesTiling = new Vector2(6f, 2f), linesSpeed = 0f, linesTwist = 0f, axisTilt = 0f, axisPrecess = 0f,
            swirlLines = 10f, swirlTwist = 0f, swirlSpeed = -0.25f, swirlWidth = 0.04f,
            shakeFrom = 1f, shakeScale = 0f, shakeRate = 0f, popTime = 0.25f, popScale = 1.4f, popFlash = 0.6f
        };

        /// <summary>
        /// Viñetas 1-2, la carga (hacia fuera y vertical): círculos que se abren en el suelo, líneas que suben, partículas que
        /// suben y salen, y humo que sale.
        /// </summary>
        private static SpiralParticleStyle[] DefenseWindup() => new[]
        {
            ShockCircle("Círculo que se abre (suelo)", "CircleImpact04Anillo", 3.5f, 0.03f, 0.4f, DefensePale, 0.9f, rate: 6f),
            new SpiralParticleStyle
            {
                name = "Líneas que suben", sprite = "Linea", rate = 70f, radius = new Vector2(0.5f, 0.9f), height = new Vector2(0f, 0.2f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(0.5f, 1.2f), upSpeed = new Vector2(4f, 6f),
                size = new Vector2(0.03f, 0.05f), lifetime = new Vector2(0.15f, 0.22f), color = Color.white, color2 = DefenseBlue
            },
            new SpiralParticleStyle
            {
                name = "Partículas que suben y salen", sprite = "Chispa", rate = 40f, radius = new Vector2(0.4f, 0.7f),
                height = new Vector2(0f, 0.4f), tangentialSpeed = new Vector2(0.5f, 1f), outwardSpeed = new Vector2(1.5f, 3f),
                upSpeed = new Vector2(2f, 4f), size = new Vector2(0.05f, 0.08f), lifetime = new Vector2(0.35f, 0.5f),
                color = Color.white, color2 = DefenseBlue
            },
            new SpiralParticleStyle
            {
                name = "Humo que sale", sprite = "HumoViento", rate = 18f, radius = new Vector2(0.5f, 0.8f), height = new Vector2(0f, 0.3f),
                tangentialSpeed = new Vector2(0.5f, 1.5f), outwardSpeed = new Vector2(2f, 3.5f), upSpeed = new Vector2(0.3f, 0.8f),
                size = new Vector2(0.4f, 0.65f), lifetime = new Vector2(0.5f, 0.7f), color = Color.white, color2 = DefensePale,
                alpha = 0.85f
            }
        };

        /// <summary>Al encenderse, todo hacia fuera: dos círculos que se abren, líneas, humo y chispas.</summary>
        private static SpiralParticleStyle[] DefenseBurst() => new[]
        {
            ShockCircle("Onda", "CircleImpact04Anillo", 5f, 0.03f, 0.4f, DefensePale, 0.9f, count: 2),
            OutwardLines("Líneas que salen", 30, Color.white, DefenseBlue),
            OutwardSmokeBurst("Humo despedido", "HumoViento", Color.white, DefensePale),
            new SpiralParticleStyle
            {
                name = "Chispas que saltan", sprite = "Chispa", count = 20, radius = new Vector2(0.4f, 0.6f), height = new Vector2(0.1f, 0.5f),
                tangentialSpeed = new Vector2(0.5f, 1.5f), outwardSpeed = new Vector2(3f, 5f), upSpeed = new Vector2(1.5f, 3f),
                size = new Vector2(0.05f, 0.08f), lifetime = new Vector2(0.4f, 0.55f), color = Color.white, color2 = DefenseBlue
            }
        };

        /// <summary>
        /// Viñeta 3, mientras dura: a ras de suelo, vaho y partículas que giran y se abren hacia fuera (espirales) y un círculo
        /// que se abre de vez en cuando; alrededor, líneas que suben por el aura.
        /// </summary>
        private static SpiralParticleStyle[] DefenseSwirl() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Vaho que gira y sale", sprite = "VahoSuelo", rate = 14f, radius = new Vector2(0.6f, 0.9f),
                height = new Vector2(0.02f, 0.05f), tangentialSpeed = new Vector2(2f, 3f), outwardSpeed = new Vector2(1f, 1.8f),
                upSpeed = Vector2.zero, size = new Vector2(0.6f, 0.9f), lifetime = new Vector2(0.8f, 1.1f), color = Color.white,
                color2 = DefensePale, alpha = 0.75f
            },
            // Líneas y no chispas: las chispas tienen gravedad y a ras de suelo se hunden enseguida
            new SpiralParticleStyle
            {
                name = "Espiral de líneas", sprite = "Linea", rate = 40f, radius = new Vector2(0.6f, 0.9f),
                height = new Vector2(0.03f, 0.08f), tangentialSpeed = new Vector2(5f, 7f), outwardSpeed = new Vector2(1.5f, 2.5f),
                upSpeed = Vector2.zero, size = new Vector2(0.04f, 0.06f), lifetime = new Vector2(0.3f, 0.45f), color = Color.white,
                color2 = DefenseBlue
            },
            new SpiralParticleStyle
            {
                name = "Partículas que salen", sprite = "Chispa", rate = 12f, radius = new Vector2(0.6f, 0.9f),
                height = new Vector2(0.05f, 0.3f), tangentialSpeed = new Vector2(1f, 2f), outwardSpeed = new Vector2(1.5f, 3f),
                upSpeed = new Vector2(0.5f, 1.5f), size = new Vector2(0.04f, 0.07f), lifetime = new Vector2(0.4f, 0.6f),
                color = Color.white, color2 = DefenseBlue
            },
            new SpiralParticleStyle
            {
                name = "Líneas que suben", sprite = "Linea", rate = 25f, radius = new Vector2(0.55f, 0.85f), height = new Vector2(0f, 0.3f),
                tangentialSpeed = new Vector2(0.3f, 0.8f), outwardSpeed = new Vector2(0.2f, 0.5f), upSpeed = new Vector2(3f, 4.5f),
                size = new Vector2(0.03f, 0.05f), lifetime = new Vector2(0.15f, 0.22f), color = Color.white, color2 = DefenseBlue
            },
            ShockCircle("Círculo que se abre (suelo)", "CircleImpact04Anillo", 3f, 0.03f, 0.4f, DefensePale, 0.7f, rate: 1.5f)
        };

        /// <summary>Al embestirla: chispas que saltan y un círculo pequeño en el choque (la Zbam sale en el aura).</summary>
        private static SpiralParticleStyle[] DefenseGuardHit() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Chispas que saltan", sprite = "Chispa", count = 12, radius = new Vector2(0.05f, 0.2f), height = new Vector2(0.1f, 0.4f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(3f, 6f), upSpeed = new Vector2(1.5f, 3f),
                size = new Vector2(0.05f, 0.09f), lifetime = new Vector2(0.3f, 0.45f), color = Color.white, color2 = DefenseBlue
            },
            new SpiralParticleStyle
            {
                name = "Círculo", sprite = "CircleImpact04", count = 1, radius = Vector2.zero, height = new Vector2(0.25f, 0.25f),
                tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = Vector2.zero, size = new Vector2(1.1f, 1.4f),
                lifetime = new Vector2(0.25f, 0.3f), color = Color.white, color2 = DefensePale, alpha = 0.9f
            }
        };

        /// <summary>Al terminar: el aura se deshace en trozos, humo y líneas que salen, y un círculo que se abre.</summary>
        private static SpiralParticleStyle[] DefenseEnd() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Trozos del aura", sprite = "Trozo", count = 18, radius = new Vector2(0.6f, 0.9f), height = new Vector2(0.2f, 1.5f),
                tangentialSpeed = new Vector2(0f, 0.5f), outwardSpeed = new Vector2(1.5f, 3f), upSpeed = new Vector2(1f, 3f),
                size = new Vector2(0.1f, 0.2f), lifetime = new Vector2(0.6f, 0.9f), color = DefensePale, color2 = Color.white
            },
            new SpiralParticleStyle
            {
                name = "Humo", sprite = "HumoViento", count = 10, radius = new Vector2(0.5f, 0.8f), height = new Vector2(0.2f, 1.2f),
                tangentialSpeed = new Vector2(0.2f, 0.6f), outwardSpeed = new Vector2(1f, 2.5f), upSpeed = new Vector2(0.3f, 1f),
                size = new Vector2(0.6f, 0.9f), lifetime = new Vector2(0.8f, 1.1f), color = Color.white, color2 = DefensePale
            },
            OutwardLines("Líneas que salen", 18, Color.white, DefenseBlue),
            ShockCircle("Círculo", "CircleImpact04Anillo", 3.4f, 0.05f, 0.4f, DefensePale, 0.9f)
        };

        // Spin Boost: el verde de su asset y uno pastel
        private static readonly Color SpinGreen = new Color(0.3f, 1f, 0.6f);
        private static readonly Color SpinPale = new Color(0.8f, 1f, 0.88f);
        private static readonly Color HealCutGrey = new Color(0.62f, 0.62f, 0.62f);
        /// <summary>
        /// Cuencos del Spin Boost: la fila verde de la hoja 01 de Pixel Holy Spell Effect Pack 3 (BDragon1727), cortada en una tira
        /// en Assets/VFX/Textures/BDragon (HolyPack3_2_Verde). Las demás filas tienen su material VFX_HolyTipCup_N para probarlas.
        /// </summary>
        private const string SpinBoostCupMaterialPath = "Assets/VFX/Materials/VFX_GreenBoostTipCup.mat";

        /// <summary>
        /// Spin Boost (K8, 2026-10-26; planteamiento del usuario, «acelerón + curación»). Carga (0,3 s): círculos en el suelo que
        /// se cierran y líneas que se tragan; al encenderse, un círculo que se abre (cierra la carga) y una explosión pequeña
        /// hacia fuera y hacia arriba. Mientras dura: un tornado pequeño de líneas y puntos pegado a la peonza (sin cúpula), que
        /// se espesa y gira más deprisa al correr; cruces que suben solo mientras cura (al cortarse la curación, cruces grises
        /// que caen); cuencos verdes y blancos en la punta; rayas que se quedan atrás al correr; la cinta del acelerón, corta y
        /// verde; y el borde verde. Al chocar, las gotas del Rastro de agua en verde (caen con su plic) y líneas en todas
        /// direcciones. Al terminar, el tornado se deshace hacia arriba. Solo añade la entrada si falta y rellena lo vacío.
        /// </summary>
        private static string SetupSpinBoost(VfxLibrary library)
        {
            SpecialVfxStyle spin = library.GetSpecialEffects(SpecialAbilityType.SpinBoost);
            bool added = spin == null;
            if (added)
            {
                spin = new SpecialVfxStyle
                {
                    type = SpecialAbilityType.SpinBoost, replaceAura = true, windupSustain = 0f,
                    burstRibbon = true, ribbonPowerColor = true, ribbonTime = 0.15f
                };
                spin.overlay = new SpecialOverlayStyle
                {
                    color = SpinGreen, hotColor = Color.white, baseRim = 0.2f, pulseRim = new Vector2(0.5f, 1.1f),
                    pulseFlash = new Vector2(0f, 0.06f), pulseInterval = new Vector2(0.2f, 0.4f), pulseDecay = 0.25f
                };
                spin.scorch = new ScorchStyle { sprite = "" };
                SpinBoostTip(spin.tip, library.burstTip);
                library.specialEffects.Add(spin);
            }

            spin.windup ??= new List<SpiralParticleStyle>();
            if (spin.windup.Count == 0) spin.windup.AddRange(SpinBoostWindup());
            spin.activationBurst ??= new List<SpiralParticleStyle>();
            if (spin.activationBurst.Count == 0) spin.activationBurst.AddRange(SpinBoostBurst());
            spin.swirl ??= new List<SpiralParticleStyle>();
            if (spin.swirl.Count == 0) spin.swirl.AddRange(SpinBoostTornado());
            spin.particles ??= new List<SpecialParticleStyle>();
            if (spin.particles.Count == 0) spin.particles.AddRange(SpinBoostStreaks());
            spin.impact ??= new List<SpiralParticleStyle>();
            if (spin.impact.Count == 0) spin.impact.AddRange(SpinBoostImpact());
            spin.endBurst ??= new List<SpiralParticleStyle>();
            if (spin.endBurst.Count == 0) spin.endBurst.AddRange(SpinBoostEnd());
            spin.healCutBurst ??= new List<SpiralParticleStyle>();
            if (spin.healCutBurst.Count == 0) spin.healCutBurst.AddRange(SpinBoostHealCut());
            spin.hitTrail ??= new List<SpecialParticleStyle>();
            if (spin.hitTrail.Count == 0) spin.hitTrail.AddRange(GenericHitTrail());
            return "Spin Boost" + (added ? " (nuevo)" : "");
        }

        /// <summary>
        /// Cuencos de la punta, hacia arriba y pegados a la punta todo el tiempo que dura, girando a distinta velocidad para dar
        /// fondo al tornado. Con la fila verde de Pixel Holy Spell Effect Pack 3 de BDragon (2026-10-26, elegida por el usuario:
        /// VFX_GreenBoostTipCup, tira de 11 × 1 con 7 dibujos) o, si falta, la hoja del acelerón. El dibujo ya es verde: los
        /// cuencos van en blanco y verde pálido para no saturarlo.
        /// </summary>
        private static void SpinBoostTip(BurstTipStyle tip, BurstTipStyle accel)
        {
            var green = AssetDatabase.LoadAssetAtPath<Material>(SpinBoostCupMaterialPath);
            tip.mesh = accel != null ? accel.mesh : null;
            tip.material = green != null ? green : accel != null ? accel.material : null;
            tip.tilesX = green != null ? 11 : accel != null ? accel.tilesX : 3;
            tip.tilesY = green != null ? 1 : accel != null ? accel.tilesY : 3;
            tip.firstFrame = green != null ? 0 : accel != null ? accel.firstFrame : 0;
            tip.frames = green != null ? 7 : accel != null ? accel.frames : 9;
            tip.cycle = green != null ? 0.5f : 0.3f;
            tip.size = 2f;
            tip.scale = new Vector2(1f, 0.8f);
            tip.height = 0.03f;
            tip.alpha = 0.9f;
            tip.lighten = 0f;
            tip.fade = new Vector2(0.06f, 0.15f);
            tip.pointForward = false;
            tip.onlyWhileRushing = false;
            tip.particleSprite = "";
            tip.ground = new TipGroundSprite();
            tip.groundLayers.Clear();
            tip.cups.Clear();
            tip.cups.Add(new BurstTipCup { angle = 0f, spinSpeed = 320f, scale = new Vector2(0.6f, 0.7f), playerColor = false, color = Color.white });
            tip.cups.Add(new BurstTipCup { angle = 120f, spinSpeed = -260f, scale = new Vector2(0.8f, 1f), playerColor = false, color = green != null ? SpinPale : SpinGreen });
            tip.cups.Add(new BurstTipCup { angle = 240f, spinSpeed = 200f, scale = new Vector2(1f, 1.3f), playerColor = false, color = new Color(1f, 1f, 1f, 0.8f) });
        }

        /// <summary>Carga: círculos en el suelo que se cierran (uno grueso y otro fino) y algunas líneas que se tragan.</summary>
        private static SpiralParticleStyle[] SpinBoostWindup() => WindupOnly(new[]
        {
            ShockCircle("Círculo que se cierra (suelo)", "CircleImpact02Cierra", 4.5f, 0.03f, 0.35f, SpinGreen, 0.9f, rate: 9f),
            ShockCircle("Círculo fino que se cierra (suelo)", "CircleImpact04Cierra", 3.2f, 0.05f, 0.3f, SpinPale, 0.85f, rate: 7f),
            InwardLines("Líneas que se tragan", 35f, Color.white, SpinGreen)
        });

        /// <summary>
        /// Al encenderse: un círculo que se abre en el suelo (cierra la carga) y una explosión pequeña hacia fuera y hacia
        /// arriba de líneas, cruces, chispas y algo de humo.
        /// </summary>
        private static SpiralParticleStyle[] SpinBoostBurst()
        {
            SpiralParticleStyle smoke = OutwardSmokeBurst("Humo despedido", "HumoViento", Color.white, SpinPale);
            smoke.count = 8;
            smoke.size = new Vector2(0.4f, 0.7f);
            smoke.upSpeed = new Vector2(1f, 2f);
            return new[]
            {
                ShockCircle("Onda", "CircleImpact02Suelo", 4.5f, 0.03f, 0.35f, SpinGreen, 0.9f),
                new SpiralParticleStyle
                {
                    name = "Líneas hacia fuera y arriba", sprite = "Linea", count = 26, radius = new Vector2(0.3f, 0.5f),
                    height = new Vector2(0.05f, 0.4f), tangentialSpeed = new Vector2(0.5f, 1.5f), outwardSpeed = new Vector2(2.5f, 4f),
                    upSpeed = new Vector2(2f, 3.5f), size = new Vector2(0.035f, 0.05f), lifetime = new Vector2(0.15f, 0.22f),
                    color = Color.white, color2 = SpinGreen
                },
                new SpiralParticleStyle
                {
                    name = "Cruces", sprite = "Cruz", count = 10, radius = new Vector2(0.2f, 0.4f), height = new Vector2(0.2f, 0.5f),
                    tangentialSpeed = new Vector2(0.5f, 1f), outwardSpeed = new Vector2(2f, 3.5f), upSpeed = new Vector2(2.5f, 4f),
                    rotation = Vector2.zero, size = new Vector2(0.12f, 0.18f), lifetime = new Vector2(0.5f, 0.7f),
                    color = SpinGreen, color2 = Color.white
                },
                new SpiralParticleStyle
                {
                    name = "Chispas", sprite = "Chispa", count = 16, radius = new Vector2(0.2f, 0.4f), height = new Vector2(0.1f, 0.3f),
                    tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(3f, 6f), upSpeed = new Vector2(3f, 5f),
                    size = new Vector2(0.06f, 0.1f), lifetime = new Vector2(0.4f, 0.6f), color = Color.white, color2 = SpinGreen
                },
                smoke
            };
        }

        /// <summary>
        /// Mientras dura: el tornado pequeño (líneas y puntos que giran y suben pegados a la peonza, abriéndose un poco hacia
        /// arriba), más espeso y más rápido al correr (Rate Per Speed y Speed Per Speed), y las cruces que suben solo mientras
        /// cura (Only While Healing).
        /// </summary>
        private static SpiralParticleStyle[] SpinBoostTornado() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Tornado de líneas", sprite = "Linea", rate = 70f, ratePerSpeed = 8f, speedPerSpeed = 0.08f,
                radius = new Vector2(0.35f, 0.5f), height = new Vector2(0f, 0.15f), tangentialSpeed = new Vector2(2.5f, 3.5f),
                outwardSpeed = new Vector2(-0.6f, -0.3f), upSpeed = new Vector2(1.5f, 2.2f), size = new Vector2(0.025f, 0.04f),
                lifetime = new Vector2(0.22f, 0.3f), color = Color.white, color2 = SpinGreen
            },
            new SpiralParticleStyle
            {
                name = "Tornado de puntos", emitter = VfxType.AuraSpinBoost, rate = 50f, ratePerSpeed = 5f, speedPerSpeed = 0.08f,
                radius = new Vector2(0.3f, 0.45f), height = new Vector2(0f, 0.1f), tangentialSpeed = new Vector2(2.5f, 3.5f),
                outwardSpeed = new Vector2(-0.5f, -0.2f), upSpeed = new Vector2(1.2f, 2f), size = new Vector2(0.06f, 0.09f),
                lifetime = new Vector2(0.25f, 0.35f), color = SpinGreen, color2 = SpinPale
            },
            new SpiralParticleStyle
            {
                name = "Cruces de curación", sprite = "Cruz", onlyWhileHealing = true, rate = 12f, radius = new Vector2(0.25f, 0.55f),
                height = new Vector2(0.2f, 0.6f), tangentialSpeed = new Vector2(0.8f, 1.5f), outwardSpeed = new Vector2(0.1f, 0.3f),
                upSpeed = new Vector2(0.8f, 1.3f), rotation = Vector2.zero, size = new Vector2(0.1f, 0.15f),
                lifetime = new Vector2(0.7f, 0.9f), color = SpinGreen, color2 = Color.white
            }
        };

        /// <summary>Al correr: rayas que se quedan atrás (el acelerón), más cuanto más deprisa va.</summary>
        private static SpecialParticleStyle[] SpinBoostStreaks() => new[]
        {
            new SpecialParticleStyle
            {
                name = "Rayas del acelerón", emitter = VfxType.Dash, rate = 0f, ratePerSpeed = 12f, spawnRadius = new Vector2(0.2f, 0.45f),
                height = new Vector2(0.05f, 0.4f), coneAngle = 90f, speed = new Vector2(0.2f, 0.5f), inheritVelocity = -0.3f,
                size = Vector2.zero, lifetime = Vector2.zero, color = SpinGreen, color2 = Color.white
            }
        };

        /// <summary>
        /// Al chocar (pedido del usuario): las gotas del Rastro de agua en verde, que caen hasta el suelo con su plic, más hacia
        /// el golpe; líneas en todas direcciones y un círculo en el suelo.
        /// </summary>
        private static SpiralParticleStyle[] SpinBoostImpact() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Gotas grandes", sprite = "Gota", count = 20, radius = new Vector2(0f, 0.2f), height = new Vector2(0.2f, 0.4f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(1.5f, 3f), upSpeed = new Vector2(3f, 6f),
                directionalSpeed = new Vector2(1.5f, 3.5f), size = new Vector2(0.08f, 0.12f), lifetime = new Vector2(1.5f, 1.9f),
                color = SpinPale, color2 = SpinGreen
            },
            new SpiralParticleStyle
            {
                name = "Gotas rápidas", sprite = "Gota", count = 18, radius = new Vector2(0f, 0.15f), height = new Vector2(0.15f, 0.3f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(4f, 7f), upSpeed = new Vector2(1f, 3f),
                directionalSpeed = new Vector2(2f, 4f), size = new Vector2(0.04f, 0.06f), lifetime = new Vector2(1f, 1.3f),
                color = Color.white, color2 = SpinGreen
            },
            new SpiralParticleStyle
            {
                name = "Líneas en todas direcciones", sprite = "Linea", count = 24, radius = new Vector2(0.1f, 0.3f),
                height = new Vector2(0.1f, 0.4f), tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(3.5f, 5.5f),
                upSpeed = new Vector2(-2f, 4f), size = new Vector2(0.04f, 0.06f), lifetime = new Vector2(0.14f, 0.2f),
                color = Color.white, color2 = SpinGreen
            },
            ShockCircle("Círculo", "CircleImpact02Suelo", 1.6f, 0.03f, 0.35f, SpinGreen, 0.9f)
        };

        /// <summary>Al terminar: el tornado se deshace hacia arriba (líneas y cruces que suben y algo de humo) y se apaga.</summary>
        private static SpiralParticleStyle[] SpinBoostEnd() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Líneas que suben", sprite = "Linea", count = 24, radius = new Vector2(0.3f, 0.5f), height = new Vector2(0f, 0.3f),
                tangentialSpeed = new Vector2(2f, 3f), outwardSpeed = new Vector2(0.3f, 0.8f), upSpeed = new Vector2(2.5f, 3.5f),
                size = new Vector2(0.035f, 0.05f), lifetime = new Vector2(0.2f, 0.3f), color = Color.white, color2 = SpinGreen
            },
            new SpiralParticleStyle
            {
                name = "Cruces que suben", sprite = "Cruz", count = 6, radius = new Vector2(0.2f, 0.45f), height = new Vector2(0.2f, 0.5f),
                tangentialSpeed = new Vector2(0.5f, 1f), outwardSpeed = new Vector2(0.2f, 0.6f), upSpeed = new Vector2(1.5f, 2.5f),
                rotation = Vector2.zero, size = new Vector2(0.1f, 0.15f), lifetime = new Vector2(0.7f, 0.9f),
                color = SpinGreen, color2 = Color.white
            },
            new SpiralParticleStyle
            {
                name = "Humo", sprite = "HumoViento", count = 6, radius = new Vector2(0.2f, 0.4f), height = new Vector2(0.2f, 0.5f),
                tangentialSpeed = new Vector2(1f, 2f), outwardSpeed = new Vector2(0.5f, 1f), upSpeed = new Vector2(0.8f, 1.5f),
                size = new Vector2(0.4f, 0.6f), lifetime = new Vector2(0.8f, 1f), color = Color.white, color2 = SpinPale, alpha = 0.7f
            }
        };

        /// <summary>Al cortar un golpe la curación (pedido del usuario): cruces grises que se rompen y caen girando, y chispas grises.</summary>
        private static SpiralParticleStyle[] SpinBoostHealCut() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Cruces que caen", sprite = "CruzCae", count = 6, radius = new Vector2(0.2f, 0.4f), height = new Vector2(0.4f, 0.7f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(1f, 2f), upSpeed = new Vector2(1f, 2.5f),
                size = new Vector2(0.12f, 0.16f), lifetime = new Vector2(0.7f, 0.8f), color = HealCutGrey, color2 = new Color(0.4f, 0.4f, 0.4f)
            },
            new SpiralParticleStyle
            {
                name = "Chispas grises", sprite = "Chispa", count = 8, radius = new Vector2(0.2f, 0.4f), height = new Vector2(0.3f, 0.6f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(1.5f, 3f), upSpeed = new Vector2(1f, 2f),
                size = new Vector2(0.05f, 0.08f), lifetime = new Vector2(0.4f, 0.5f), color = HealCutGrey, color2 = Color.white
            }
        };

        // Fantasma (2026-10-27, decidido con el usuario): colores muy claros y pálidos, fríos (el lila de su asset)
        private static readonly Color GhostLilac = new Color(0.8f, 0.85f, 1f);
        private static readonly Color GhostPale = new Color(0.93f, 0.95f, 1f);
        private static readonly Color GhostViolet = new Color(0.74f, 0.7f, 1f);
        private static readonly Color GhostIce = new Color(0.82f, 0.96f, 1f);
        /// <summary>Cuencos del Fantasma: la luna gris de Pixel Holy Spell Effect Pack 3 (hoja 01, fila 4; 11 × 1 con 9 dibujos).</summary>
        private const string GhostCupMaterialPath = "Assets/VFX/Materials/VFX_HolyTipCup_4_LunaGris.mat";

        /// <summary>
        /// Fantasma (K8, 2026-10-27; planteamiento del usuario, «como el Spin Boost, simple»). Carga (0,3 s): una burbuja de humo
        /// (esfera que parpadea y hace glitch, con humo que se arremolina y se aprieta dentro). Al encenderse, la burbuja revienta
        /// (humo hacia fuera y un anillo en el suelo) y una bocanada sale de la peonza hacia donde aparece el clon, que aparece
        /// desde invisible. Mientras el clon está fuera (While Lingering), la dueña y el clon llevan una espiral de destellos y
        /// humo, un embudo corto con máscara, cuencos con la luna gris y un rastro de humo bajo, y se vuelven transparentes (la
        /// dueña un poco, el clon más, con algún parpadeo). Al chocar, una explosión pequeña de humo y destellos que salen hacia
        /// el golpe y se apagan enseguida. Cuando rompen el clon, su humo sigue la inercia que llevaba; la dueña suelta humo
        /// hacia arriba y recupera la opacidad poco a poco. Solo añade la entrada si falta y rellena lo vacío.
        /// </summary>
        private static string SetupGhost(VfxLibrary library)
        {
            SpecialVfxStyle ghost = library.GetSpecialEffects(SpecialAbilityType.Ghost);
            bool added = ghost == null;
            Shader domeShader = Shader.Find(ChargeDomeShaderName);
            Material domeMaterial = domeShader != null ? GetMaterial(ChargeDomeMaterialPath, domeShader) : null;
            if (added)
            {
                ghost = new SpecialVfxStyle
                {
                    type = SpecialAbilityType.Ghost, replaceAura = true, windupSustain = 0f, whileLingering = true, cloneEffects = true
                };
                ghost.overlay = new SpecialOverlayStyle
                {
                    color = GhostLilac, hotColor = Color.white, baseRim = 0.25f, pulseRim = new Vector2(0.4f, 0.9f),
                    pulseFlash = new Vector2(0f, 0.05f), pulseInterval = new Vector2(0.25f, 0.6f), pulseDecay = 0.3f
                };
                ghost.scorch = new ScorchStyle { sprite = "" };
                GhostTip(ghost.tip, library.burstTip);
                // La máscara de ruido del mini tornado del Spin Boost (la que eligió el usuario), si está
                SpecialVfxStyle spin = library.GetSpecialEffects(SpecialAbilityType.SpinBoost);
                Texture2D noise = spin != null && spin.dome != null ? spin.dome.maskTexture : null;
                ghost.dome = GhostBubble(noise);
                ghost.baseDome = GhostFunnel(spin != null && spin.dome != null && spin.dome.material != null ? spin.dome : null, noise);
                ghost.bodyFade = new BladeFadeStyle { enabled = true };
                library.specialEffects.Add(ghost);
            }
            if (ghost.dome.material == null) ghost.dome.material = domeMaterial;
            if (ghost.baseDome.material == null) ghost.baseDome.material = domeMaterial;

            ghost.windup ??= new List<SpiralParticleStyle>();
            if (ghost.windup.Count == 0) ghost.windup.AddRange(GhostWindup());
            ghost.activationBurst ??= new List<SpiralParticleStyle>();
            if (ghost.activationBurst.Count == 0) ghost.activationBurst.AddRange(GhostBurst());
            ghost.cloneSplit ??= new List<SpiralParticleStyle>();
            if (ghost.cloneSplit.Count == 0) ghost.cloneSplit.AddRange(GhostSplit());
            ghost.cloneAppear ??= new List<SpiralParticleStyle>();
            if (ghost.cloneAppear.Count == 0) ghost.cloneAppear.AddRange(GhostAppear());
            ghost.swirl ??= new List<SpiralParticleStyle>();
            if (ghost.swirl.Count == 0) ghost.swirl.AddRange(GhostSwirl());
            ghost.particles ??= new List<SpecialParticleStyle>();
            if (ghost.particles.Count == 0) ghost.particles.AddRange(GhostLowSmoke());
            ghost.impact ??= new List<SpiralParticleStyle>();
            if (ghost.impact.Count == 0) ghost.impact.AddRange(GhostImpact());
            ghost.endBurst ??= new List<SpiralParticleStyle>();
            if (ghost.endBurst.Count == 0) ghost.endBurst.AddRange(GhostEnd());
            ghost.cloneVanish ??= new List<SpiralParticleStyle>();
            if (ghost.cloneVanish.Count == 0) ghost.cloneVanish.AddRange(GhostVanish());
            ghost.swapBurst ??= new List<SpiralParticleStyle>();
            if (ghost.swapBurst.Count == 0) ghost.swapBurst.AddRange(GhostSwap());
            return "Fantasma" + (added ? " (nuevo)" : "") + (ghost.dome.material == null ? " (falta el shader de la cúpula)" : "");
        }

        /// <summary>
        /// Cuencos de la punta, hacia arriba, girando a distinta velocidad y en sentidos contrarios: la luna gris de BDragon en
        /// blanco, lila, violeta y hielo pálidos (o, si falta, la hoja del acelerón).
        /// </summary>
        private static void GhostTip(BurstTipStyle tip, BurstTipStyle accel)
        {
            var moon = AssetDatabase.LoadAssetAtPath<Material>(GhostCupMaterialPath);
            tip.mesh = accel != null ? accel.mesh : null;
            tip.material = moon != null ? moon : accel != null ? accel.material : null;
            tip.tilesX = moon != null ? 11 : accel != null ? accel.tilesX : 3;
            tip.tilesY = moon != null ? 1 : accel != null ? accel.tilesY : 3;
            tip.firstFrame = 0;
            tip.frames = moon != null ? 9 : accel != null ? accel.frames : 9;
            tip.cycle = 0.7f;
            tip.size = 1.8f;
            tip.scale = new Vector2(1f, 0.6f);
            tip.height = 0.03f;
            tip.alpha = 0.75f;
            tip.lighten = 0f;
            tip.fade = new Vector2(0.1f, 0.2f);
            tip.pointForward = false;
            tip.onlyWhileRushing = false;
            tip.particleSprite = "";
            tip.ground = new TipGroundSprite();
            tip.groundLayers.Clear();
            tip.cups.Clear();
            tip.cups.Add(new BurstTipCup { angle = 0f, spinSpeed = 240f, scale = new Vector2(0.6f, 1.4f), playerColor = false, color = new Color(1f, 1f, 1f, 0.85f) });
            tip.cups.Add(new BurstTipCup { angle = 90f, spinSpeed = -180f, scale = new Vector2(0.85f, 1f), playerColor = false, color = GhostLilac });
            tip.cups.Add(new BurstTipCup { angle = 180f, spinSpeed = 140f, scale = new Vector2(1.1f, 0.7f), playerColor = false, color = new Color(GhostViolet.r, GhostViolet.g, GhostViolet.b, 0.7f) });
            tip.cups.Add(new BurstTipCup { angle = 270f, spinSpeed = -320f, scale = new Vector2(0.45f, 1.9f), playerColor = false, color = new Color(GhostIce.r, GhostIce.g, GhostIce.b, 0.8f) });
        }

        /// <summary>
        /// La burbuja de la carga (Windup Only): esfera lila pálida que crece desde pequeña, con huecos de ruido, parpadeo, glitch
        /// y un temblor al final; al encenderse se infla y revienta con un destello.
        /// </summary>
        private static ChargeDomeStyle GhostBubble(Texture2D noise)
        {
            ChargeDomeStyle d = BounceBubble();
            d.timing = DomeTiming.WindupOnly;
            d.radius = 0.95f;
            d.startRadius = 0.25f;
            d.growEase = 2f;
            d.bodyColor = new Color(GhostPale.r, GhostPale.g, GhostPale.b, 0.24f);
            d.rimColor = new Color(GhostLilac.r, GhostLilac.g, GhostLilac.b, 0.9f);
            d.outlineColor = new Color(0.55f, 0.55f, 0.65f, 0.8f);
            d.swirlColor = new Color(1f, 1f, 1f, 0.7f);
            d.swirlColor2 = new Color(GhostViolet.r, GhostViolet.g, GhostViolet.b, 0.5f);
            d.linesSpeed = 1.1f;
            d.shakeFrom = 0.6f;
            d.shakeScale = 0.06f;
            d.shakeRate = 20f;
            d.popTime = 0.25f;
            d.popScale = 1.6f;
            d.popFlash = 0.7f;
            d.maskTexture = noise;
            d.maskAmount = noise != null ? 0.18f : 0f;
            d.maskTiling = new Vector2(2f, 1f);
            d.maskScroll = new Vector2(0.15f, 0.6f);
            d.maskEdge = 0.03f;
            d.maskEdgeColor = new Color(GhostPale.r, GhostPale.g, GhostPale.b, 0.9f);
            d.wobble = 0.04f;
            d.wobbleSpeed = 6f;
            d.flickerRate = 3f;
            d.flickerAlpha = 0.4f;
            d.flickerTime = 0.04f;
            d.glitchRate = 2f;
            d.glitchBands = 8;
            d.glitchOffset = 0.05f;
            d.glitchTime = 0.06f;
            return d;
        }

        /// <summary>
        /// Embudo corto mientras el clon está fuera (After Windup): el mini tornado del Spin Boost tal como lo dejó el usuario
        /// (máscara de ruido que sube), más bajo y en lila pálido, que se queda en embudo; sin él, uno nuevo con la misma idea.
        /// </summary>
        private static ChargeDomeStyle GhostFunnel(ChargeDomeStyle spinDome, Texture2D noise)
        {
            ChargeDomeStyle d = spinDome != null ? CloneDome(spinDome) : DefenseCrown();
            d.timing = DomeTiming.AfterWindup;
            d.growTime = 0.35f;
            d.sphere = false;
            d.radius = 0.85f;
            d.heightScale = 0.55f;
            d.startRadius = 0.4f;
            d.appearFrom = 0f;
            d.funnelPhase = 1f;
            d.funnelMorph = 0.2f;
            d.funnelShape = new Vector3(0.55f, 1.35f, 1.1f);
            d.topCut = 0.12f;
            d.shakeFrom = 1f;
            d.shakeScale = 0f;
            d.popTime = 0.3f;
            d.popScale = 1.3f;
            d.popFall = 0f;
            d.bodyColor = new Color(GhostLilac.r, GhostLilac.g, GhostLilac.b, 0.3f);
            d.rimColor = new Color(GhostPale.r, GhostPale.g, GhostPale.b, 0.9f);
            d.outlineColor = new Color(1f, 1f, 1f, 0.7f);
            d.swirlColor = new Color(1f, 1f, 1f, 0.7f);
            d.swirlColor2 = new Color(GhostViolet.r, GhostViolet.g, GhostViolet.b, 0.5f);
            d.maskEdgeColor = new Color(GhostIce.r, GhostIce.g, GhostIce.b, 0.9f);
            if (spinDome == null)
            {
                d.maskTexture = noise;
                d.maskAmount = noise != null ? 0.45f : 0.25f;
                d.maskTiling = Vector2.one;
                d.maskScroll = new Vector2(0.8f, 2.5f);
                d.topErode = 0.3f;
                d.topErodeFrom = 0.4f;
                d.wobble = 0.03f;
            }
            d.flickerEnabled = true;
            d.flickerRate = 0.6f;
            d.flickerAlpha = 0.4f;
            d.flickerTime = 0.05f;
            d.glitchEnabled = true;
            d.glitchRate = 0.4f;
            d.glitchOffset = 0.04f;
            return d;
        }

        /// <summary>Carga: humo que se arremolina y se aprieta hacia la peonza, dentro de la burbuja, y un círculo que se cierra.</summary>
        private static SpiralParticleStyle[] GhostWindup() => WindupOnly(new[]
        {
            new SpiralParticleStyle
            {
                name = "Humo que se aprieta", sprite = "VahoFantasma", rate = 32f, radius = new Vector2(0.7f, 0.95f),
                height = new Vector2(0.05f, 0.8f), tangentialSpeed = new Vector2(2f, 3.5f), outwardSpeed = new Vector2(-2.2f, -1.4f),
                upSpeed = new Vector2(0f, 0.3f), size = new Vector2(0.35f, 0.55f), lifetime = new Vector2(0.3f, 0.4f),
                color = GhostPale, color2 = GhostLilac, alpha = 0.8f
            },
            new SpiralParticleStyle
            {
                name = "Destellos que giran", emitter = VfxType.Sparkle, rate = 25f, radius = new Vector2(0.6f, 0.85f),
                height = new Vector2(0.1f, 0.8f), tangentialSpeed = new Vector2(3f, 4.5f), outwardSpeed = new Vector2(-1.5f, -0.8f),
                upSpeed = new Vector2(-0.2f, 0.3f), size = new Vector2(0.08f, 0.12f), lifetime = new Vector2(0.25f, 0.35f),
                color = Color.white, color2 = GhostIce
            },
            ShockCircle("Círculo que se cierra (suelo)", "CircleImpact04Cierra", 3f, 0.04f, 0.3f, GhostLilac, 0.85f, rate: 7f)
        });

        /// <summary>Al encenderse, la burbuja revienta: humo hacia fuera, un anillo que se abre en el suelo y destellos.</summary>
        private static SpiralParticleStyle[] GhostBurst()
        {
            SpiralParticleStyle smoke = OutwardSmokeBurst("Humo de la burbuja", "VahoFantasma", GhostPale, GhostLilac);
            smoke.count = 14;
            smoke.size = new Vector2(0.5f, 0.8f);
            smoke.outwardSpeed = new Vector2(2.5f, 4.5f);
            smoke.upSpeed = new Vector2(0.3f, 1f);
            smoke.alpha = 0.85f;
            return new[]
            {
                ShockCircle("Anillo", "CircleImpact02Suelo", 4f, 0.03f, 0.35f, GhostLilac, 0.85f),
                smoke,
                new SpiralParticleStyle
                {
                    name = "Destellos", emitter = VfxType.Sparkle, count = 18, radius = new Vector2(0.4f, 0.7f),
                    height = new Vector2(0.1f, 0.7f), tangentialSpeed = new Vector2(0.5f, 1.5f), outwardSpeed = new Vector2(2.5f, 4.5f),
                    upSpeed = new Vector2(0.5f, 2f), size = new Vector2(0.1f, 0.16f), lifetime = new Vector2(0.3f, 0.45f),
                    color = Color.white, color2 = GhostLilac
                }
            };
        }

        /// <summary>
        /// La bocanada que se desdobla: humo y destellos que salen de la peonza hacia donde aparece el clon (1,6 m a un lado;
        /// Directional Speed los lleva hasta él en unas dos décimas).
        /// </summary>
        private static SpiralParticleStyle[] GhostSplit() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Bocanada hacia el clon", sprite = "VahoFantasma", count = 10, radius = new Vector2(0f, 0.2f),
                height = new Vector2(0.1f, 0.45f), tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(0f, 0.4f),
                upSpeed = new Vector2(0f, 0.3f), directionalSpeed = new Vector2(6f, 8.5f), size = new Vector2(0.35f, 0.55f),
                lifetime = new Vector2(0.25f, 0.35f), color = GhostPale, color2 = GhostLilac, alpha = 0.85f
            },
            new SpiralParticleStyle
            {
                name = "Destellos hacia el clon", emitter = VfxType.Sparkle, count = 10, radius = new Vector2(0f, 0.2f),
                height = new Vector2(0.1f, 0.5f), tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(0f, 0.5f),
                upSpeed = new Vector2(-0.2f, 0.4f), directionalSpeed = new Vector2(6.5f, 9f), size = new Vector2(0.08f, 0.13f),
                lifetime = new Vector2(0.2f, 0.28f), color = Color.white, color2 = GhostIce
            }
        };

        /// <summary>Donde aparece el clon: una nube de humo que se abre y algunos destellos.</summary>
        private static SpiralParticleStyle[] GhostAppear()
        {
            SpiralParticleStyle smoke = OutwardSmokeBurst("Nube del clon", "VahoFantasma", GhostPale, GhostLilac);
            smoke.count = 10;
            smoke.radius = new Vector2(0.2f, 0.4f);
            smoke.size = new Vector2(0.45f, 0.7f);
            smoke.outwardSpeed = new Vector2(1.5f, 3f);
            smoke.lifetime = new Vector2(0.6f, 0.85f);
            smoke.alpha = 0.8f;
            return new[]
            {
                smoke,
                new SpiralParticleStyle
                {
                    name = "Destellos del clon", emitter = VfxType.Sparkle, count = 10, radius = new Vector2(0.2f, 0.4f),
                    height = new Vector2(0.1f, 0.6f), tangentialSpeed = new Vector2(0.5f, 1f), outwardSpeed = new Vector2(1.5f, 3f),
                    upSpeed = new Vector2(0.5f, 1.5f), size = new Vector2(0.08f, 0.13f), lifetime = new Vector2(0.3f, 0.4f),
                    color = Color.white, color2 = GhostLilac
                }
            };
        }

        /// <summary>Mientras el clon está fuera (la dueña y el clon): espiral de destellos y de humo que sube, más rápida al correr.</summary>
        private static SpiralParticleStyle[] GhostSwirl() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Espiral de destellos", emitter = VfxType.Sparkle, rate = 30f, ratePerSpeed = 2f, speedPerSpeed = 0.05f,
                radius = new Vector2(0.45f, 0.6f), height = new Vector2(0f, 0.15f), tangentialSpeed = new Vector2(2.5f, 3.5f),
                outwardSpeed = new Vector2(-0.3f, -0.1f), upSpeed = new Vector2(0.7f, 1.1f), size = new Vector2(0.07f, 0.11f),
                lifetime = new Vector2(0.45f, 0.6f), color = Color.white, color2 = GhostLilac
            },
            new SpiralParticleStyle
            {
                name = "Espiral de humo", sprite = "VahoFantasma", rate = 12f, ratePerSpeed = 1f, speedPerSpeed = 0.05f,
                radius = new Vector2(0.4f, 0.6f), height = new Vector2(0f, 0.2f), tangentialSpeed = new Vector2(1.5f, 2.5f),
                outwardSpeed = new Vector2(0f, 0.2f), upSpeed = new Vector2(0.4f, 0.8f), size = new Vector2(0.3f, 0.45f),
                lifetime = new Vector2(0.6f, 0.8f), color = GhostPale, color2 = GhostViolet, alpha = 0.5f
            }
        };

        /// <summary>Rastro de humo bajo: bocanadas a ras de suelo que se quedan atrás y se deshacen despacio, más al correr.</summary>
        private static SpecialParticleStyle[] GhostLowSmoke() => new[]
        {
            new SpecialParticleStyle
            {
                name = "Rastro de humo bajo", sprite = "VahoSuelo", rate = 5f, ratePerSpeed = 2.5f, spawnRadius = new Vector2(0.15f, 0.45f),
                height = new Vector2(0f, 0.06f), coneAngle = 90f, speed = new Vector2(0.05f, 0.25f), inheritVelocity = -0.1f,
                size = new Vector2(0.45f, 0.75f), lifetime = new Vector2(0.9f, 1.3f), color = GhostPale, color2 = GhostLilac,
                alpha = 0.45f
            }
        };

        /// <summary>
        /// Al chocar (pedido del usuario): una explosión pequeña de humo y destellos que salen hacia el golpe, brillan al salir y
        /// se apagan enseguida.
        /// </summary>
        private static SpiralParticleStyle[] GhostImpact()
        {
            SpiralParticleStyle smoke = OutwardSmokeBurst("Humo del golpe", "Vaho", GhostPale, GhostLilac);
            smoke.count = 6;
            smoke.radius = new Vector2(0f, 0.15f);
            smoke.height = new Vector2(0.1f, 0.35f);
            smoke.outwardSpeed = new Vector2(1f, 2.2f);
            smoke.size = new Vector2(0.3f, 0.5f);
            smoke.lifetime = new Vector2(0.45f, 0.65f);
            smoke.alpha = 0.8f;
            return new[]
            {
                smoke,
                new SpiralParticleStyle
                {
                    name = "Destellos hacia el golpe", emitter = VfxType.Sparkle, count = 12, radius = new Vector2(0f, 0.1f),
                    height = new Vector2(0.1f, 0.4f), tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(0.3f, 1.2f),
                    upSpeed = new Vector2(0.3f, 1.5f), directionalSpeed = new Vector2(4f, 7f), size = new Vector2(0.1f, 0.16f),
                    lifetime = new Vector2(0.15f, 0.28f), color = Color.white, color2 = GhostIce
                }
            };
        }

        /// <summary>
        /// Al romperse el clon, en la dueña (End Burst): humo que sube, destellos y líneas hacia arriba; después recupera la
        /// opacidad poco a poco (Body Fade → Recover Time).
        /// </summary>
        private static SpiralParticleStyle[] GhostEnd() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Humo que sube", sprite = "VahoFantasma", count = 10, radius = new Vector2(0.2f, 0.4f), height = new Vector2(0.1f, 0.4f),
                tangentialSpeed = new Vector2(0.5f, 1.2f), outwardSpeed = new Vector2(0.2f, 0.5f), upSpeed = new Vector2(2f, 3f),
                size = new Vector2(0.45f, 0.7f), lifetime = new Vector2(0.8f, 1.1f), color = GhostPale, color2 = GhostLilac, alpha = 0.75f
            },
            new SpiralParticleStyle
            {
                name = "Destellos que suben", emitter = VfxType.Sparkle, count = 14, radius = new Vector2(0.2f, 0.45f),
                height = new Vector2(0.1f, 0.4f), tangentialSpeed = new Vector2(1f, 2f), outwardSpeed = new Vector2(0.3f, 0.8f),
                upSpeed = new Vector2(2.5f, 4f), size = new Vector2(0.08f, 0.13f), lifetime = new Vector2(0.35f, 0.5f),
                color = Color.white, color2 = GhostIce
            },
            new SpiralParticleStyle
            {
                name = "Líneas que suben", sprite = "Linea", count = 14, radius = new Vector2(0.3f, 0.5f), height = new Vector2(0f, 0.3f),
                tangentialSpeed = new Vector2(1.5f, 2.5f), outwardSpeed = new Vector2(0.2f, 0.6f), upSpeed = new Vector2(2.5f, 3.5f),
                size = new Vector2(0.03f, 0.045f), lifetime = new Vector2(0.2f, 0.3f), color = Color.white, color2 = GhostLilac
            }
        };

        /// <summary>
        /// El clon se rompe (al tercer golpe): se deshace en humo y destellos que siguen la inercia que llevaba al recibir el golpe
        /// (Inherit Velocity) y se frenan.
        /// </summary>
        private static SpiralParticleStyle[] GhostVanish() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Humo del clon", sprite = "Vaho", count = 14, radius = new Vector2(0f, 0.35f), height = new Vector2(0.1f, 0.5f),
                tangentialSpeed = new Vector2(0.3f, 1f), outwardSpeed = new Vector2(0.5f, 1.5f), upSpeed = new Vector2(0.2f, 0.8f),
                inheritVelocity = 0.8f, size = new Vector2(0.5f, 0.8f), lifetime = new Vector2(0.8f, 1.2f), color = GhostPale,
                color2 = GhostLilac, alpha = 0.85f
            },
            new SpiralParticleStyle
            {
                name = "Destellos del clon", emitter = VfxType.Sparkle, count = 12, radius = new Vector2(0f, 0.3f),
                height = new Vector2(0.1f, 0.5f), tangentialSpeed = new Vector2(0.5f, 1.5f), outwardSpeed = new Vector2(0.8f, 2f),
                upSpeed = new Vector2(0.5f, 1.5f), inheritVelocity = 1f, size = new Vector2(0.08f, 0.13f),
                lifetime = new Vector2(0.3f, 0.45f), color = Color.white, color2 = GhostIce
            }
        };

        /// <summary>
        /// Al cambiar de sitio con el clon (pedido del usuario, 2026-10-28), en los dos sitios: una explosión de vaho hacia fuera,
        /// un anillo en el suelo y destellos.
        /// </summary>
        private static SpiralParticleStyle[] GhostSwap()
        {
            SpiralParticleStyle smoke = OutwardSmokeBurst("Humo del cambio", "VahoFantasma", GhostPale, GhostLilac);
            smoke.count = 14;
            smoke.radius = new Vector2(0.1f, 0.35f);
            smoke.height = new Vector2(0.1f, 0.6f);
            smoke.outwardSpeed = new Vector2(2f, 4f);
            smoke.upSpeed = new Vector2(0.3f, 1.2f);
            smoke.size = new Vector2(0.5f, 0.8f);
            smoke.lifetime = new Vector2(0.6f, 0.9f);
            smoke.alpha = 0.85f;
            return new[]
            {
                smoke,
                ShockCircle("Anillo del cambio", "CircleImpact02Suelo", 3f, 0.03f, 0.3f, GhostLilac, 0.85f),
                new SpiralParticleStyle
                {
                    name = "Destellos del cambio", emitter = VfxType.Sparkle, count = 14, radius = new Vector2(0.2f, 0.4f),
                    height = new Vector2(0.1f, 0.6f), tangentialSpeed = new Vector2(0.5f, 1.5f), outwardSpeed = new Vector2(2f, 3.5f),
                    upSpeed = new Vector2(0.5f, 2f), size = new Vector2(0.09f, 0.14f), lifetime = new Vector2(0.3f, 0.45f),
                    color = Color.white, color2 = GhostIce
                }
            };
        }

        // Rastro de agua (2026-10-26, decidido con el usuario): azul y cian con espuma blanca
        private static readonly Color WaterBlue = new Color(0.25f, 0.6f, 1f);
        private static readonly Color WaterCyan = new Color(0.5f, 0.95f, 1f);
        private static readonly Color WaterPale = new Color(0.85f, 0.97f, 1f);

        /// <summary>
        /// Rastro de agua (K8, 2026-10-26; boceto del usuario en cuatro viñetas, Tide Core), empezado copiando la Defensa (sus
        /// cúpulas, tal como las dejó el usuario). Carga (0,5 s de efectos con 0,3 de juego): círculos que se cierran en el
        /// suelo, gotas que suben y vaho y humo que se tragan; al encenderse, la salpicadura (corona de gotas, ondas que se abren
        /// y vaho), y después suben los dos cilindros de agua con franjas onduladas que suben sin parar. Mientras dura: salpica
        /// más cuanto más deprisa va, deja manchas de agua donde están los charcos y gotas y vaho alrededor. Al chocar, muchas
        /// gotas que caen con gravedad hasta el suelo (plic), más hacia el golpe, vaho y gotas rápidas. Al terminar, los
        /// cilindros caen en seco al suelo y queda el charco grande (su mancha, en End Burst). Sobre los charcos, brillos y alguna
        /// gotita que salta (Puddles; las ondas de antes se quedan, con Replace Aura apagado). Solo añade la entrada si falta y
        /// rellena lo que esté vacío.
        /// </summary>
        private static string SetupWaterTrail(VfxLibrary library)
        {
            SpecialVfxStyle water = library.GetSpecialEffects(SpecialAbilityType.WaterTrail);
            bool added = water == null;
            if (added)
            {
                water = new SpecialVfxStyle
                {
                    type = SpecialAbilityType.WaterTrail, replaceAura = false, windupSustain = 0f, visualWindupTime = 0.5f,
                    visualWindupDomesOnly = true
                };
                water.tip.cups.Clear();
                water.tip.particleSprite = "";
                water.tip.ground = new TipGroundSprite();
                water.overlay = new SpecialOverlayStyle
                {
                    color = WaterCyan, hotColor = Color.white, baseRim = 0.2f, pulseRim = new Vector2(0.4f, 0.8f),
                    pulseFlash = new Vector2(0f, 0.04f), pulseInterval = new Vector2(0.3f, 0.6f), pulseDecay = 0.3f
                };
                // Las cúpulas de la Defensa, como las dejó el usuario (o las de por defecto si no hay)
                SpecialVfxStyle defense = library.GetSpecialEffects(SpecialAbilityType.Defense);
                water.dome = CloneDome(defense != null && defense.dome != null ? defense.dome : DefenseAura());
                water.baseDome = CloneDome(defense != null && defense.baseDome != null ? defense.baseDome : DefenseCrown());
                WaterCylinder(water.dome, "FranjasAgua", 0.4f, new Vector2(1f, 1f), new Vector2(0f, 0.5f), 0.5f, 0.05f, 0.22f);
                WaterCylinder(water.baseDome, "FranjasAguaRotas", 0.35f, new Vector2(2f, 1f), new Vector2(0.05f, 0.7f), 0.3f, 0.04f, 0.3f);
                water.scorch = new ScorchStyle
                {
                    sprite = "Charco", spacing = 0.4f, idleInterval = 0.5f, size = new Vector2(1.4f, 1.8f), lifetime = 3f,
                    color = WaterBlue, alpha = 0.45f, height = 0.01f
                };
                library.specialEffects.Add(water);
            }

            Shader domeShader = Shader.Find(ChargeDomeShaderName);
            Material domeMaterial = domeShader != null ? GetMaterial(ChargeDomeMaterialPath, domeShader) : null;
            water.dome ??= new ChargeDomeStyle();
            water.baseDome ??= new ChargeDomeStyle();
            if (water.dome.material == null) water.dome.material = domeMaterial;
            if (water.baseDome.material == null) water.baseDome.material = domeMaterial;

            water.windup ??= new List<SpiralParticleStyle>();
            if (water.windup.Count == 0) water.windup.AddRange(WindupOnly(WaterWindup()));
            water.activationBurst ??= new List<SpiralParticleStyle>();
            if (water.activationBurst.Count == 0) water.activationBurst.AddRange(WaterSplash());
            water.swirl ??= new List<SpiralParticleStyle>();
            if (water.swirl.Count == 0) water.swirl.AddRange(WaterSwirl());
            water.particles ??= new List<SpecialParticleStyle>();
            if (water.particles.Count == 0) water.particles.AddRange(WaterSpray());
            water.impact ??= new List<SpiralParticleStyle>();
            if (water.impact.Count == 0) water.impact.AddRange(WaterImpact());
            water.endBurst ??= new List<SpiralParticleStyle>();
            if (water.endBurst.Count == 0) water.endBurst.AddRange(WaterEnd());
            water.puddles ??= new List<SpecialParticleStyle>();
            if (water.puddles.Count == 0) water.puddles.AddRange(WaterPuddleDrops());
            return "Rastro de agua" + (added ? " (nuevo)" : "") + (water.dome.material == null ? " (falta el shader de la cúpula)" : "");
        }

        /// <summary>Copia de una cúpula (con sus texturas y materiales), para empezar otra a partir de ella.</summary>
        private static ChargeDomeStyle CloneDome(ChargeDomeStyle source)
        {
            var copy = new ChargeDomeStyle();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), copy);
            return copy;
        }

        /// <summary>
        /// Una cúpula copiada de la Defensa, en agua: azul con algo de transparencia, orilla cian y espuma blanca en el borde de
        /// los huecos; máscara de franjas onduladas que suben sin parar (Mask Scroll Y) y ondulación; sin latido, parpadeo ni
        /// glitch. Sale después de la salpicadura (el último 40% de la carga) y al terminar cae en seco al suelo.
        /// </summary>
        private static void WaterCylinder(ChargeDomeStyle d, string mask, float amount, Vector2 tiling, Vector2 scroll, float topErode,
            float wobble, float bodyAlpha)
        {
            d.bodyColor = new Color(0.35f, 0.7f, 1f, bodyAlpha);
            d.rimColor = new Color(WaterCyan.r, WaterCyan.g, WaterCyan.b, 0.9f);
            d.outlineColor = new Color(0.15f, 0.3f, 0.6f, 0.75f);
            d.swirlColor = new Color(1f, 1f, 1f, 0.7f);
            d.swirlColor2 = new Color(WaterCyan.r, WaterCyan.g, WaterCyan.b, 0.5f);
            d.maskEnabled = true;
            d.maskTexture = FakeBladeImpactSetup.WaterMaskTexture(mask);
            d.maskAmount = amount;
            d.maskTiling = tiling;
            d.maskScroll = scroll;
            d.maskEdge = 0.06f;
            d.maskEdgeColor = new Color(1f, 1f, 1f, 0.95f);
            d.topErodeEnabled = true;
            d.topErode = topErode;
            d.topErodeFrom = 0.5f;
            d.holePulse = 0f;
            d.wobbleEnabled = true;
            d.wobble = wobble;
            d.wobbleWaves = new Vector2(6f, 2f);
            d.wobbleSpeed = 5f;
            d.flickerRate = 0f;
            d.glitchRate = 0f;
            d.appearFrom = 0.6f;
            d.popTime = 0.4f;
            d.popScale = 1.05f;
            d.popFlash = 0.15f;
            d.popFall = 1f;
        }

        /// <summary>
        /// Viñeta 1, la carga: círculos que se cierran hacia la peonza en el suelo, gotas que salen hacia arriba (y caen con su
        /// plic) y vaho y humo blanco que se tragan hacia el centro, para dar profundidad.
        /// </summary>
        private static SpiralParticleStyle[] WaterWindup() => new[]
        {
            ShockCircle("Círculo que se cierra (suelo)", "CircleImpact02Cierra", 3.5f, 0.03f, 0.4f, WaterPale, 0.9f, rate: 5f),
            ShockCircle("Media luna que se cierra (suelo)", "CircleImpact04Cierra", 2.6f, 0.05f, 0.4f, WaterCyan, 0.8f, rate: 4f),
            new SpiralParticleStyle
            {
                name = "Gotas que suben", sprite = "Gota", rate = 70f, radius = new Vector2(0.2f, 0.5f), height = new Vector2(0.1f, 0.4f),
                tangentialSpeed = new Vector2(0f, 0.5f), outwardSpeed = new Vector2(0.3f, 1.2f), upSpeed = new Vector2(3f, 5.5f),
                size = new Vector2(0.05f, 0.08f), lifetime = new Vector2(1.2f, 1.6f), color = WaterPale, color2 = WaterCyan
            },
            new SpiralParticleStyle
            {
                name = "Vaho que se traga", sprite = "Vaho", rate = 22f, radius = new Vector2(1.2f, 1.6f), height = new Vector2(0.05f, 0.4f),
                tangentialSpeed = new Vector2(1f, 2f), outwardSpeed = new Vector2(-3f, -2f), upSpeed = new Vector2(0f, 0.3f),
                size = new Vector2(0.4f, 0.6f), lifetime = new Vector2(0.4f, 0.55f), color = Color.white, color2 = WaterPale, alpha = 0.8f
            },
            new SpiralParticleStyle
            {
                name = "Humo blanco que se traga", sprite = "HumoTraga", rate = 12f, radius = new Vector2(1.3f, 1.8f),
                height = new Vector2(0.1f, 0.5f), tangentialSpeed = new Vector2(1f, 2f), outwardSpeed = new Vector2(-4f, -3f),
                upSpeed = Vector2.zero, size = new Vector2(0.35f, 0.55f), lifetime = new Vector2(0.4f, 0.5f), color = Color.white,
                color2 = WaterPale, alpha = 0.85f
            }
        };

        /// <summary>
        /// Viñeta 2, la salpicadura al encenderse (como algo que cae al agua): corona de gotas que suben y caen alrededor, gotas
        /// pequeñas y rápidas, ondas que se abren en el suelo y vaho que sale.
        /// </summary>
        private static SpiralParticleStyle[] WaterSplash() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Corona de gotas", sprite = "Gota", count = 40, radius = new Vector2(0.3f, 0.5f), height = new Vector2(0.1f, 0.3f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(1.5f, 3f), upSpeed = new Vector2(4f, 6.5f),
                size = new Vector2(0.06f, 0.1f), lifetime = new Vector2(1.3f, 1.7f), color = WaterPale, color2 = WaterCyan
            },
            new SpiralParticleStyle
            {
                name = "Gotas rápidas", sprite = "Gota", count = 30, radius = new Vector2(0.2f, 0.4f), height = new Vector2(0.1f, 0.3f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(3f, 5f), upSpeed = new Vector2(2f, 4f),
                size = new Vector2(0.04f, 0.06f), lifetime = new Vector2(1f, 1.3f), color = Color.white, color2 = WaterCyan
            },
            ShockCircle("Onda", "CircleImpact02Suelo", 4f, 0.03f, 0.45f, WaterPale, 0.9f, count: 2),
            ShockCircle("Onda pequeña", "CircleImpact04Anillo", 2.6f, 0.04f, 0.4f, WaterCyan, 0.8f, count: 2),
            OutwardSmokeBurst("Vaho que sale", "Vaho", Color.white, WaterPale)
        };

        /// <summary>Mientras dura, alrededor: vaho a ras de suelo que gira y sale, y gotas que saltan de los cilindros.</summary>
        private static SpiralParticleStyle[] WaterSwirl() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Vaho a ras de suelo", sprite = "VahoSuelo", rate = 8f, radius = new Vector2(0.6f, 0.9f),
                height = new Vector2(0.02f, 0.05f), tangentialSpeed = new Vector2(1.5f, 2.5f), outwardSpeed = new Vector2(0.8f, 1.5f),
                upSpeed = Vector2.zero, size = new Vector2(0.6f, 0.9f), lifetime = new Vector2(0.8f, 1.1f), color = Color.white,
                color2 = WaterPale, alpha = 0.6f
            },
            new SpiralParticleStyle
            {
                name = "Gotas que saltan", sprite = "Gota", rate = 12f, radius = new Vector2(0.6f, 0.9f), height = new Vector2(0.2f, 0.8f),
                tangentialSpeed = new Vector2(0.5f, 1f), outwardSpeed = new Vector2(0.5f, 1.5f), upSpeed = new Vector2(1.5f, 3f),
                size = new Vector2(0.04f, 0.07f), lifetime = new Vector2(1f, 1.3f), color = WaterPale, color2 = WaterCyan
            }
        };

        /// <summary>
        /// Viñeta 4, al moverse: salpica muchas gotas, más cuanto más deprisa va (Rate Per Speed), hacia atrás y a los lados, y
        /// algo de espuma a ras de suelo (la estela de una lancha).
        /// </summary>
        private static SpecialParticleStyle[] WaterSpray() => new[]
        {
            new SpecialParticleStyle
            {
                name = "Salpicadura al moverse", sprite = "Gota", rate = 3f, ratePerSpeed = 7f, spawnRadius = new Vector2(0.3f, 0.6f),
                height = new Vector2(0.05f, 0.2f), coneAngle = 60f, speed = new Vector2(2f, 4f), inheritVelocity = -0.15f,
                forwardOffset = new Vector2(-0.3f, 0f), size = new Vector2(0.04f, 0.08f), lifetime = new Vector2(0.9f, 1.3f),
                rotation = Vector2.zero, color = WaterPale, color2 = WaterCyan
            },
            new SpecialParticleStyle
            {
                name = "Espuma", sprite = "VahoSuelo", rate = 0f, ratePerSpeed = 1.2f, spawnRadius = new Vector2(0.2f, 0.5f),
                height = new Vector2(0.02f, 0.04f), coneAngle = 90f, speed = new Vector2(0.3f, 0.8f), inheritVelocity = 0f,
                forwardOffset = new Vector2(-0.5f, -0.2f), size = new Vector2(0.5f, 0.8f), lifetime = new Vector2(0.6f, 0.9f),
                color = Color.white, color2 = WaterPale, alpha = 0.6f
            }
        };

        /// <summary>
        /// Viñeta 4, al chocar: muchas gotas grandes en todas direcciones, más hacia el golpe (Directional Speed), que caen con
        /// gravedad y viven hasta llegar al suelo (plic); gotas pequeñas más rápidas, vaho y un círculo en el suelo.
        /// </summary>
        private static SpiralParticleStyle[] WaterImpact() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Gotas grandes", sprite = "Gota", count = 30, radius = new Vector2(0f, 0.2f), height = new Vector2(0.2f, 0.4f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(1.5f, 3f), upSpeed = new Vector2(3f, 6f),
                directionalSpeed = new Vector2(1.5f, 3.5f), size = new Vector2(0.08f, 0.12f), lifetime = new Vector2(1.5f, 1.9f),
                color = WaterPale, color2 = WaterCyan
            },
            new SpiralParticleStyle
            {
                name = "Gotas rápidas", sprite = "Gota", count = 25, radius = new Vector2(0f, 0.15f), height = new Vector2(0.15f, 0.3f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(4f, 7f), upSpeed = new Vector2(1f, 3f),
                directionalSpeed = new Vector2(2f, 4f), size = new Vector2(0.04f, 0.06f), lifetime = new Vector2(1f, 1.3f),
                color = Color.white, color2 = WaterCyan
            },
            new SpiralParticleStyle
            {
                name = "Vaho", sprite = "Vaho", count = 6, radius = new Vector2(0f, 0.2f), height = new Vector2(0.2f, 0.4f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(1f, 2f), upSpeed = new Vector2(0.3f, 0.8f),
                directionalSpeed = new Vector2(0.5f, 1f), size = new Vector2(0.4f, 0.6f), lifetime = new Vector2(0.6f, 0.9f),
                color = Color.white, color2 = WaterPale, alpha = 0.8f
            },
            ShockCircle("Círculo", "CircleImpact02Suelo", 1.6f, 0.03f, 0.35f, WaterPale, 0.9f)
        };

        /// <summary>
        /// Sobre los charcos (2026-10-26, decidido con el usuario): brillos quietos y pequeños con el dibujo de la gota
        /// (GotaBrillo: sin gravedad ni plic, aparecen y se apagan) y, de vez en cuando, una gotita que salta y cae con su plic
        /// (Gota). Rate por charco; sustituyen a los destellos cuadrados de 0,2 m de antes.
        /// </summary>
        private static SpecialParticleStyle[] WaterPuddleDrops() => new[]
        {
            new SpecialParticleStyle
            {
                name = "Brillos", sprite = "GotaBrillo", rate = 14f, spawnRadius = new Vector2(0f, 0.5f),
                height = new Vector2(0.02f, 0.06f), coneAngle = 0f, speed = new Vector2(0f, 0.1f), inheritVelocity = 0f,
                size = new Vector2(0.04f, 0.07f), lifetime = new Vector2(0.4f, 0.7f), rotation = Vector2.zero,
                color = Color.white, color2 = WaterPale
            },
            new SpecialParticleStyle
            {
                name = "Gotitas que saltan", sprite = "Gota", rate = 1.2f, spawnRadius = new Vector2(0f, 0.5f),
                height = new Vector2(0.02f, 0.05f), coneAngle = 25f, speed = new Vector2(1.2f, 2f), inheritVelocity = 0f,
                size = new Vector2(0.03f, 0.05f), lifetime = new Vector2(0.6f, 0.9f), rotation = Vector2.zero,
                color = WaterPale, color2 = WaterCyan
            }
        };

        /// <summary>
        /// Al terminar: el agua de los cilindros cae al suelo (gotas que bajan), queda el charco grande (la mancha: mismo tamaño
        /// y duración que el charco de juego del asset WaterTrail, 2 m de radio y 6 s), vaho a ras de suelo y ondas.
        /// </summary>
        private static SpiralParticleStyle[] WaterEnd() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Agua que cae", sprite = "Gota", count = 50, radius = new Vector2(0.5f, 0.9f), height = new Vector2(0.3f, 1.2f),
                tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(0.2f, 0.8f), upSpeed = new Vector2(-0.5f, 0.5f),
                size = new Vector2(0.06f, 0.1f), lifetime = new Vector2(1.2f, 1.5f), color = WaterPale, color2 = WaterCyan
            },
            new SpiralParticleStyle
            {
                name = "Charco grande", sprite = "Charco", count = 1, radius = Vector2.zero, height = new Vector2(0.012f, 0.012f),
                tangentialSpeed = Vector2.zero, outwardSpeed = Vector2.zero, upSpeed = Vector2.zero, size = new Vector2(4f, 4f),
                lifetime = new Vector2(6f, 6f), color = WaterBlue, color2 = WaterBlue, alpha = 0.5f
            },
            new SpiralParticleStyle
            {
                name = "Vaho a ras de suelo", sprite = "VahoSuelo", count = 8, radius = new Vector2(0.5f, 0.9f),
                height = new Vector2(0.02f, 0.05f), tangentialSpeed = new Vector2(0.5f, 1f), outwardSpeed = new Vector2(1f, 2f),
                upSpeed = Vector2.zero, size = new Vector2(0.7f, 1f), lifetime = new Vector2(1f, 1.4f), color = Color.white,
                color2 = WaterPale, alpha = 0.6f
            },
            ShockCircle("Ondas", "CircleImpact02Suelo", 4.5f, 0.03f, 0.5f, WaterPale, 0.85f, count: 2)
        };

        /// <summary>
        /// Peonza mojada (2026-10-26, decidido con el usuario): borde azul, gotas que salen despedidas hacia fuera (con su plic)
        /// y un rastro de mojado pequeño que no moja y se seca mucho antes que el agua del poder. Va aparte del estado principal.
        /// Solo la rellena (y la enciende) si está vacía.
        /// </summary>
        private static string SetupWet(VfxLibrary library)
        {
            library.wet ??= new StatusVfxStyle { enabled = false };
            StatusVfxStyle wet = library.wet;
            if (wet.particles.Count > 0 || wet.swirl.Count > 0) return "mojada";
            wet.enabled = true;
            wet.tip.cups.Clear();
            wet.tip.particleSprite = "";
            wet.tip.ground = new TipGroundSprite();
            wet.overlay = new SpecialOverlayStyle
            {
                color = WaterBlue, hotColor = WaterCyan, baseRim = 0.35f, pulseRim = new Vector2(0.5f, 0.9f),
                pulseFlash = new Vector2(0f, 0.05f), pulseInterval = new Vector2(0.3f, 0.6f), pulseDecay = 0.3f
            };
            wet.particles.Add(new SpecialParticleStyle
            {
                name = "Gotas que salen despedidas", sprite = "Gota", rate = 16f, ratePerSpeed = 2f, spawnRadius = new Vector2(0.3f, 0.5f),
                height = new Vector2(0.15f, 0.3f), coneAngle = 80f, speed = new Vector2(2.5f, 4f), inheritVelocity = 0.3f,
                size = new Vector2(0.04f, 0.07f), lifetime = new Vector2(0.8f, 1.1f), rotation = Vector2.zero,
                color = WaterPale, color2 = WaterCyan
            });
            wet.scorch = new ScorchStyle
            {
                sprite = "Charco", spacing = 0.5f, idleInterval = 0f, size = new Vector2(0.5f, 0.8f), lifetime = 0.8f,
                color = WaterBlue, alpha = 0.35f, height = 0.01f
            };
            return "mojada (nueva)";
        }

        private static readonly Color LightningYellow = new Color(1f, 0.85f, 0.25f);
        private static readonly Color LightningOrange = new Color(1f, 0.5f, 0.05f);

        /// <summary>
        /// Carga de Rayos: el remolino del Fuego (llamas y chispas que giran y se tragan hacia dentro) en amarillo anaranjado,
        /// y ceniza negra y gris que sube un poco y cae despacio (sprite Ceniza de Setup Impactos).
        /// </summary>
        private static SpiralParticleStyle[] LightningWindup() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Remolino de llamas", emitter = VfxType.ChargeFlame, rate = 160f, radius = new Vector2(0.8f, 1.2f),
                height = new Vector2(0f, 0.15f), tangentialSpeed = new Vector2(6f, 9f), outwardSpeed = new Vector2(-3f, -2f),
                upSpeed = new Vector2(2f, 4f), size = new Vector2(0.16f, 0.26f), lifetime = new Vector2(0.25f, 0.4f),
                color = LightningYellow, color2 = LightningOrange
            },
            new SpiralParticleStyle
            {
                name = "Remolino de chispas", sprite = "Chispa", rate = 70f, radius = new Vector2(0.6f, 1f),
                height = new Vector2(0f, 0.2f), tangentialSpeed = new Vector2(7f, 10f), outwardSpeed = new Vector2(-2f, -1f),
                upSpeed = new Vector2(3f, 5f), size = new Vector2(0.05f, 0.08f), lifetime = new Vector2(0.3f, 0.5f),
                color = Color.white, color2 = LightningYellow
            },
            new SpiralParticleStyle
            {
                name = "Ceniza", sprite = "Ceniza", rate = 30f, radius = new Vector2(0.5f, 1f),
                height = new Vector2(0.1f, 0.4f), tangentialSpeed = new Vector2(1f, 2f), outwardSpeed = new Vector2(-0.5f, 0.3f),
                upSpeed = new Vector2(0.8f, 1.6f), size = new Vector2(0.12f, 0.22f), lifetime = new Vector2(1.2f, 2f),
                color = new Color(0.08f, 0.08f, 0.08f), color2 = new Color(0.45f, 0.45f, 0.45f), alpha = 0.9f
            }
        };

        /// <summary>Al encenderse Rayos: la explosión compacta del Fuego en amarillo anaranjado, con ceniza.</summary>
        private static SpiralParticleStyle[] LightningBurst() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Explosión de llamas", emitter = VfxType.ChargeFlame, count = 50, radius = new Vector2(0.3f, 0.5f),
                height = new Vector2(0.05f, 0.3f), tangentialSpeed = new Vector2(2.5f, 3.5f), outwardSpeed = new Vector2(0.8f, 1.5f),
                upSpeed = new Vector2(0.6f, 1.2f), size = new Vector2(0.22f, 0.36f), lifetime = new Vector2(0.22f, 0.35f),
                color = LightningYellow, color2 = LightningOrange
            },
            new SpiralParticleStyle
            {
                name = "Explosión de chispas", sprite = "Chispa", count = 35, radius = new Vector2(0.3f, 0.5f),
                height = new Vector2(0.05f, 0.3f), tangentialSpeed = new Vector2(3f, 4.5f), outwardSpeed = new Vector2(1.2f, 2.2f),
                upSpeed = new Vector2(1f, 2f), size = new Vector2(0.05f, 0.09f), lifetime = new Vector2(0.25f, 0.45f),
                color = Color.white, color2 = LightningYellow
            },
            LightningAsh(20, 0.5f, 1.5f)
        };

        /// <summary>Círculo de chispas que salen rectas a ras de suelo, saltan y caen (impactos de Rayos y de la lanzada).</summary>
        private static SpiralParticleStyle LightningSparks(int count, float minSpeed, float maxSpeed, Vector2 size) => new SpiralParticleStyle
        {
            name = "Chispas", sprite = "Chispa", count = count, radius = new Vector2(0.1f, 0.2f),
            height = new Vector2(0.05f, 0.2f), tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(minSpeed, maxSpeed),
            upSpeed = new Vector2(1f, 2.5f), size = size, lifetime = new Vector2(0.25f, 0.5f), color = Color.white, color2 = LightningYellow
        };

        /// <summary>Círculo de ceniza negra y gris que sale despacio, sube un poco y cae lenta (sprite Ceniza).</summary>
        private static SpiralParticleStyle LightningAsh(int count, float minSpeed, float maxSpeed) => new SpiralParticleStyle
        {
            name = "Ceniza", sprite = "Ceniza", count = count, radius = new Vector2(0.2f, 0.4f),
            height = new Vector2(0.1f, 0.4f), tangentialSpeed = new Vector2(0.3f, 0.8f), outwardSpeed = new Vector2(minSpeed, maxSpeed),
            upSpeed = new Vector2(1f, 2f), size = new Vector2(0.12f, 0.22f), lifetime = new Vector2(1.2f, 2f),
            color = new Color(0.08f, 0.08f, 0.08f), color2 = new Color(0.45f, 0.45f, 0.45f), alpha = 0.9f
        };

        /// <summary>Valores de partida de Rayos: sin nada en la punta, borde que parpadea y los arcos.</summary>
        private static SpecialVfxStyle DefaultLightning()
        {
            var lightning = new SpecialVfxStyle { type = SpecialAbilityType.Lightning, replaceAura = true, windupSustain = 0.25f };
            lightning.tip.cups.Clear();
            lightning.tip.particleSprite = "";
            lightning.tip.ground = new TipGroundSprite();
            lightning.overlay = new SpecialOverlayStyle
            {
                color = new Color(1f, 0.85f, 0.2f), hotColor = Color.white, baseRim = 0.25f, pulseRim = new Vector2(1f, 3f),
                pulseFlash = new Vector2(0f, 0.2f), pulseInterval = new Vector2(0.03f, 0.2f), pulseDecay = 0.05f
            };
            lightning.arcs = new ElectricArcStyle();
            return lightning;
        }

        /// <summary>Material de los arcos: partículas de URP sin luz, transparente, sin textura (el color lo ponen las líneas).</summary>
        private static Material ArcMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(ArcMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(ArcMaterialPath) };
                AssetDatabase.CreateAsset(material, ArcMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Transparent);
            material.SetFloat("_Blend", (float)BaseShaderGUI.BlendMode.Alpha); // aditivo, sobre el suelo gris, el amarillo salía beis
            material.SetFloat("_ColorMode", 0f);
            material.SetFloat("_SoftParticlesEnabled", 0f);
            material.SetFloat("_CameraFadingEnabled", 0f);
            material.SetFloat("_DistortionEnabled", 0f);
            BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Prefab de Status and Auras FREE (con sus materiales ya en URP), o null si el paquete no está.</summary>
        private static ParticleSystem AuraPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(path);
            if (prefab != null) ConvertBuiltInParticles(AuraPackFolder);
            return prefab;
        }

        /// <summary>Añade a una lista las partículas que no estén ya (por nombre): las que dejó el usuario no se tocan.</summary>
        private static int AddMissing<T>(List<T> list, IEnumerable<T> styles, System.Func<T, string> name)
        {
            int added = 0;
            foreach (T style in styles)
            {
                bool found = false;
                foreach (T existing in list)
                    if (existing != null && name(existing) == name(style)) found = true;
                if (found) continue;
                list.Add(style);
                added++;
            }
            return added;
        }

        private static readonly Color FireOrange = new Color(1f, 0.45f, 0.1f);
        private static readonly Color FireYellow = new Color(1f, 0.85f, 0.35f);

        /// <summary>
        /// Carga del Fuego más llamativa (2026-10-19, pedido por el usuario, como Tornado y Onda de choque): círculos naranjas y
        /// amarillos que se cierran, llamas y líneas que se tragan hacia la peonza. Solo durante la carga.
        /// </summary>
        private static SpiralParticleStyle[] FireWindupInward() => WindupOnly(new[]
        {
            ShockCircle("Círculo que se cierra (suelo)", "CircleImpact02Cierra", 5f, 0.03f, 0.4f, FireOrange, 0.9f, rate: 8f),
            ShockCircle("Círculo que se cierra (alto)", "FP_CircleImpactCierra", 3f, 0.4f, 0.4f, FireYellow, 0.8f, rate: 6f),
            new SpiralParticleStyle
            {
                name = "Llamas que se tragan", emitter = VfxType.ChargeFlame, rate = 120f, radius = new Vector2(2f, 2.8f),
                height = new Vector2(0f, 0.3f), tangentialSpeed = new Vector2(2f, 4f), outwardSpeed = new Vector2(-8f, -6f),
                upSpeed = new Vector2(0f, 0.5f), size = new Vector2(0.18f, 0.28f), lifetime = new Vector2(0.25f, 0.35f)
            },
            InwardLines("Líneas de velocidad (fuego)", 50f, FireYellow, FireOrange)
        });

        /// <summary>Al encenderse el Fuego: un círculo naranja que se abre, medias lunas y líneas que salen.</summary>
        private static SpiralParticleStyle[] FireBurstOutward() => new[]
        {
            ShockCircle("Onda de fuego", "CircleImpact02Suelo", 5f, 0.03f, 0.4f, FireOrange, 0.9f),
            ShockCircle("Medias lunas de fuego", "CircleImpact04Anillo", 4f, 0.15f, 0.4f, FireYellow, 0.8f, count: 2),
            OutwardLines("Líneas que salen (fuego)", 30, FireYellow, FireOrange),
            InwardSmokeBurst("Humo que se traga (fuego)", "HumoTraga", Color.white, FireYellow),
            OutwardSmokeBurst("Humo despedido (fuego)", "HumoExplosion", Color.white, Color.white)
        };

        /// <summary>Carga del Hielo más llamativa (2026-10-19): círculos cian y blancos que se cierran, cristales y líneas que se tragan.</summary>
        private static SpiralParticleStyle[] IceWindupInward() => WindupOnly(new[]
        {
            ShockCircle("Círculo que se cierra (suelo)", "CircleImpact02Cierra", 5f, 0.03f, 0.4f, IceCyan, 0.9f, rate: 8f),
            ShockCircle("Círculo que se cierra (alto)", "FP_CircleImpactCierra", 3f, 0.4f, 0.4f, Color.white, 0.8f, rate: 6f),
            new SpiralParticleStyle
            {
                name = "Cristales que se tragan", emitter = VfxType.Sparkle, rate = 90f, radius = new Vector2(2f, 2.8f),
                height = new Vector2(0f, 0.4f), tangentialSpeed = new Vector2(2f, 4f), outwardSpeed = new Vector2(-8f, -6f),
                upSpeed = new Vector2(0f, 0.4f), size = new Vector2(0.14f, 0.22f), lifetime = new Vector2(0.25f, 0.35f),
                color = IceCyan, color2 = Color.white
            },
            InwardLines("Líneas de velocidad (hielo)", 50f, Color.white, IceCyan)
        });

        /// <summary>Al encenderse el Hielo: un círculo cian que se abre, otro blanco más alto y líneas que salen.</summary>
        private static SpiralParticleStyle[] IceBurstOutward() => new[]
        {
            ShockCircle("Onda de hielo", "CircleImpact02Suelo", 5f, 0.03f, 0.4f, IceCyan, 0.9f),
            ShockCircle("Onda alta de hielo", "ShotValo2Suelo", 3.5f, 0.45f, 0.35f, Color.white, 0.8f),
            OutwardLines("Líneas que salen (hielo)", 30, Color.white, IceCyan),
            InwardSmokeBurst("Humo que se traga (hielo)", "HumoTraga", Color.white, IcePale),
            OutwardSmokeBurst("Humo despedido (hielo)", "HumoViento", Color.white, IcePale)
        };

        private static SpiralParticleStyle[] WindupOnly(SpiralParticleStyle[] styles)
        {
            foreach (SpiralParticleStyle style in styles) style.windupOnly = true;
            return styles;
        }

        /// <summary>Al encenderse: un anillo de humo que llega desde fuera y se traga hacia la peonza.</summary>
        private static SpiralParticleStyle InwardSmokeBurst(string name, string sprite, Color color, Color color2) => new SpiralParticleStyle
        {
            name = name, sprite = sprite, count = 12, radius = new Vector2(2.2f, 2.8f), height = new Vector2(0.1f, 0.5f),
            tangentialSpeed = new Vector2(2f, 3f), outwardSpeed = new Vector2(-6f, -5f), upSpeed = new Vector2(0f, 0.3f),
            size = new Vector2(0.45f, 0.7f), lifetime = new Vector2(0.4f, 0.5f), color = color, color2 = color2, alpha = 0.9f
        };

        /// <summary>Al encenderse: humo que sale despedido, frena y se disuelve.</summary>
        private static SpiralParticleStyle OutwardSmokeBurst(string name, string sprite, Color color, Color color2) => new SpiralParticleStyle
        {
            name = name, sprite = sprite, count = 14, radius = new Vector2(0.4f, 0.6f), height = new Vector2(0.3f, 0.6f),
            tangentialSpeed = new Vector2(0.5f, 1.5f), outwardSpeed = new Vector2(3f, 6f), upSpeed = new Vector2(0.2f, 0.6f),
            size = new Vector2(0.6f, 1f), lifetime = new Vector2(0.8f, 1.1f), color = color, color2 = color2
        };

        /// <summary>Estela del Fuego en la golpeada (2026-10-19): llamas y chispas que se quedan atrás mientras sale despedida.</summary>
        private static SpecialParticleStyle[] FireHitTrail() => new[]
        {
            new SpecialParticleStyle
            {
                name = "Llamas de la golpeada", emitter = VfxType.ChargeFlame, rate = 70f, spawnRadius = new Vector2(0.2f, 0.4f),
                height = new Vector2(0.05f, 0.3f), coneAngle = 60f, speed = new Vector2(0.5f, 1.5f), inheritVelocity = -0.1f,
                size = new Vector2(0.15f, 0.25f), lifetime = new Vector2(0.25f, 0.4f), color = FireYellow, color2 = FireOrange
            },
            new SpecialParticleStyle
            {
                name = "Chispas de la golpeada", emitter = VfxType.LowSpinSpark, rate = 30f, spawnRadius = new Vector2(0.2f, 0.4f),
                height = new Vector2(0.1f, 0.3f), coneAngle = 70f, speed = new Vector2(1f, 2.5f), inheritVelocity = -0.1f,
                size = new Vector2(0.06f, 0.1f), lifetime = new Vector2(0.3f, 0.5f), color = new Color(1f, 0.95f, 0.6f), color2 = FireOrange
            }
        };

        /// <summary>Estela del Hielo en la golpeada (2026-10-19): esquirlas y vaho que se quedan atrás.</summary>
        private static SpecialParticleStyle[] IceHitTrail() => new[]
        {
            new SpecialParticleStyle
            {
                name = "Esquirlas de la golpeada", emitter = VfxType.Sparkle, rate = 50f, spawnRadius = new Vector2(0.2f, 0.4f),
                height = new Vector2(0.05f, 0.35f), coneAngle = 70f, speed = new Vector2(0.5f, 1.5f), inheritVelocity = -0.1f,
                size = new Vector2(0.12f, 0.2f), lifetime = new Vector2(0.25f, 0.4f), color = IceCyan, color2 = Color.white
            },
            new SpecialParticleStyle
            {
                name = "Vaho de la golpeada", sprite = "Vaho", rate = 15f, spawnRadius = new Vector2(0.1f, 0.3f),
                height = new Vector2(0.05f, 0.2f), coneAngle = 70f, speed = new Vector2(0.2f, 0.5f), inheritVelocity = -0.1f,
                size = new Vector2(0.5f, 0.8f), lifetime = new Vector2(0.5f, 0.8f), color = IcePale, color2 = Color.white, alpha = 0.5f
            }
        };

        /// <summary>
        /// Plantilla común (2026-10-19, pedida por el usuario) para los especiales sin entrada propia: carga con círculos,
        /// líneas y humo hacia dentro; al encenderse, un círculo que se abre, líneas y humo hacia fuera y hacia dentro; estela
        /// en la golpeada. Blanca, teñida con el color de cada poder (Power Tint), sin quitar su aura ni poner nada sobre la
        /// peonza o en la punta. Solo la rellena si está vacía.
        /// </summary>
        private static string SetupGenericSpecial(VfxLibrary library)
        {
            SpecialVfxStyle g = library.genericSpecialEffects ??= new SpecialVfxStyle();
            if (g.windup.Count > 0 || g.activationBurst.Count > 0 || g.hitTrail.Count > 0) return "plantilla común (ya está)";

            g.replaceAura = false;
            g.windupSustain = 0f;
            g.tip.cups.Clear();
            g.tip.particleSprite = "";
            g.tip.ground = new TipGroundSprite();
            g.overlay.enabled = false;
            g.scorch.sprite = "";
            g.windup.AddRange(GenericWindup());
            g.activationBurst.AddRange(GenericBurst());
            g.hitTrail.AddRange(GenericHitTrail());
            g.hitTrailTime = 0.4f;
            return "plantilla común (nueva)";
        }

        private static SpiralParticleStyle[] GenericWindup() => WindupOnly(new[]
        {
            PowerTint(ShockCircle("Círculo que se cierra (suelo)", "CircleImpact02Cierra", 5f, 0.03f, 0.4f, Color.white, 0.9f, rate: 8f), 1f),
            PowerTint(ShockCircle("Círculo que se cierra (alto)", "FP_CircleImpactCierra", 3f, 0.4f, 0.4f, Color.white, 0.8f, rate: 6f), 0.6f),
            PowerTint(InwardLines("Líneas de velocidad", 50f, Color.white, Color.white), 0.5f),
            PowerTint(new SpiralParticleStyle
            {
                name = "Humo que se traga", sprite = "HumoTraga", rate = 25f, radius = new Vector2(1.6f, 2.2f), height = new Vector2(0f, 0.4f),
                tangentialSpeed = new Vector2(2f, 4f), outwardSpeed = new Vector2(-5f, -4f), upSpeed = new Vector2(0f, 0.4f),
                size = new Vector2(0.4f, 0.65f), lifetime = new Vector2(0.4f, 0.5f), color = Color.white, color2 = Color.white, alpha = 0.85f
            }, 0.3f)
        });

        private static SpiralParticleStyle[] GenericBurst() => new[]
        {
            PowerTint(ShockCircle("Onda", "CircleImpact02Suelo", 5f, 0.03f, 0.4f, Color.white, 0.9f), 1f),
            PowerTint(OutwardLines("Líneas que salen", 30, Color.white, Color.white), 0.5f),
            PowerTint(InwardSmokeBurst("Humo que se traga", "HumoTraga", Color.white, Color.white), 0.3f),
            PowerTint(OutwardSmokeBurst("Humo despedido", "HumoViento", Color.white, Color.white), 0.3f)
        };

        private static SpecialParticleStyle[] GenericHitTrail() => new[]
        {
            new SpecialParticleStyle
            {
                name = "Humo de la golpeada", sprite = "NubeComic", rate = 25f, spawnRadius = new Vector2(0.2f, 0.4f),
                height = new Vector2(0f, 0.2f), coneAngle = 70f, speed = new Vector2(0.3f, 0.8f), inheritVelocity = -0.1f,
                size = new Vector2(0.3f, 0.5f), lifetime = new Vector2(0.35f, 0.5f), color = Color.white, color2 = Color.white,
                alpha = 0.85f, powerTint = 0.4f
            },
            new SpecialParticleStyle
            {
                name = "Chispas de la golpeada", sprite = "Chispa", rate = 30f, spawnRadius = new Vector2(0.2f, 0.4f),
                height = new Vector2(0.05f, 0.3f), coneAngle = 70f, speed = new Vector2(1f, 2.5f), inheritVelocity = -0.1f,
                size = new Vector2(0.06f, 0.1f), lifetime = new Vector2(0.3f, 0.45f), color = Color.white, color2 = Color.white,
                powerTint = 1f
            }
        };

        private static SpiralParticleStyle PowerTint(SpiralParticleStyle style, float tint)
        {
            style.powerTint = tint;
            return style;
        }

        /// <summary>Líneas de velocidad (el punto estirado) que se tragan girando hacia la peonza.</summary>
        private static SpiralParticleStyle InwardLines(string name, float rate, Color color, Color color2) => new SpiralParticleStyle
        {
            name = name, sprite = "Linea", rate = rate, radius = new Vector2(1.3f, 1.8f), height = new Vector2(0.05f, 0.6f),
            tangentialSpeed = new Vector2(1f, 2f), outwardSpeed = new Vector2(-5f, -4f), upSpeed = Vector2.zero,
            size = new Vector2(0.06f, 0.09f), lifetime = new Vector2(0.25f, 0.32f), color = color, color2 = color2
        };

        /// <summary>Círculo de líneas de velocidad que salen rectas hacia fuera.</summary>
        private static SpiralParticleStyle OutwardLines(string name, int count, Color color, Color color2) => new SpiralParticleStyle
        {
            name = name, sprite = "Linea", count = count, radius = new Vector2(0.4f, 0.7f), height = new Vector2(0.05f, 0.5f),
            tangentialSpeed = new Vector2(0.5f, 1.5f), outwardSpeed = new Vector2(6f, 9f), upSpeed = Vector2.zero,
            size = new Vector2(0.07f, 0.1f), lifetime = new Vector2(0.18f, 0.26f), color = color, color2 = color2
        };

        /// <summary>
        /// Al activar el Fuego: remolino de llamas y chispas que gira y se traga hacia dentro durante la carga, y explosión
        /// en espiral al encenderse (2026-10-17).
        /// </summary>
        private static SpiralParticleStyle[] FireWindup() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Remolino de llamas", emitter = VfxType.ChargeFlame, rate = 160f, radius = new Vector2(0.8f, 1.2f),
                height = new Vector2(0f, 0.15f), tangentialSpeed = new Vector2(6f, 9f), outwardSpeed = new Vector2(-3f, -2f),
                upSpeed = new Vector2(2f, 4f), size = new Vector2(0.16f, 0.26f), lifetime = new Vector2(0.25f, 0.4f)
            },
            new SpiralParticleStyle
            {
                name = "Remolino de chispas", emitter = VfxType.LowSpinSpark, rate = 70f, radius = new Vector2(0.6f, 1f),
                height = new Vector2(0f, 0.2f), tangentialSpeed = new Vector2(7f, 10f), outwardSpeed = new Vector2(-2f, -1f),
                upSpeed = new Vector2(3f, 5f), size = new Vector2(0.08f, 0.12f), lifetime = new Vector2(0.3f, 0.5f),
                color = new Color(1f, 0.95f, 0.6f), color2 = new Color(1f, 0.6f, 0.15f)
            }
        };

        /// <summary>
        /// Explosión al encenderse, compacta alrededor de la peonza (2026-10-17: antes salían muy lejos; las partículas van
        /// en línea recta, así que la velocidad de giro también las aleja).
        /// </summary>
        private static SpiralParticleStyle[] FireActivationBurst() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Explosión de llamas", emitter = VfxType.ChargeFlame, count = 50, radius = new Vector2(0.3f, 0.5f),
                height = new Vector2(0.05f, 0.3f), tangentialSpeed = new Vector2(2.5f, 3.5f), outwardSpeed = new Vector2(0.8f, 1.5f),
                upSpeed = new Vector2(0.6f, 1.2f), size = new Vector2(0.22f, 0.36f), lifetime = new Vector2(0.22f, 0.35f)
            },
            new SpiralParticleStyle
            {
                name = "Explosión de chispas", emitter = VfxType.LowSpinSpark, count = 35, radius = new Vector2(0.3f, 0.5f),
                height = new Vector2(0.05f, 0.3f), tangentialSpeed = new Vector2(3f, 4.5f), outwardSpeed = new Vector2(1.2f, 2.2f),
                upSpeed = new Vector2(1f, 2f), size = new Vector2(0.08f, 0.14f), lifetime = new Vector2(0.25f, 0.45f),
                color = new Color(1f, 0.95f, 0.6f), color2 = new Color(1f, 0.6f, 0.15f)
            }
        };

        /// <summary>
        /// Choque con el Fuego activo (2026-10-17): un círculo de llamas que salen rectas a ras de suelo, chispas grandes que
        /// saltan y caen y ceniza que sube y flota, más exageradas que las del aura.
        /// </summary>
        private static SpiralParticleStyle[] FireImpact() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Aro de llamas", emitter = VfxType.ChargeFlame, count = 18, radius = new Vector2(0.15f, 0.25f),
                height = new Vector2(0.05f, 0.15f), tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(4f, 5f),
                upSpeed = new Vector2(0.3f, 0.8f), size = new Vector2(0.28f, 0.4f), lifetime = new Vector2(0.25f, 0.35f)
            },
            new SpiralParticleStyle
            {
                name = "Chispas", emitter = VfxType.LowSpinSpark, count = 24, radius = new Vector2(0.1f, 0.2f),
                height = new Vector2(0.1f, 0.3f), tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(4f, 7f),
                upSpeed = new Vector2(3f, 6f), size = new Vector2(0.1f, 0.18f), lifetime = new Vector2(0.5f, 0.9f),
                color = new Color(1f, 0.95f, 0.6f), color2 = new Color(1f, 0.5f, 0.1f)
            },
            new SpiralParticleStyle
            {
                name = "Ceniza", emitter = VfxType.SpecialAura, count = 16, radius = new Vector2(0.2f, 0.4f),
                height = new Vector2(0.2f, 0.4f), tangentialSpeed = new Vector2(0.3f, 0.6f), outwardSpeed = new Vector2(1.2f, 2.4f),
                upSpeed = new Vector2(0.8f, 1.6f), size = new Vector2(0.1f, 0.18f), lifetime = new Vector2(1.2f, 2f),
                color = new Color(0.25f, 0.23f, 0.22f), color2 = new Color(0.55f, 0.52f, 0.5f), alpha = 0.85f
            }
        };

        /// <summary>
        /// Remolino de la peonza quemada mientras dura la quemadura (2026-10-17): el de la carga del Fuego con menos
        /// densidad (hacia la cuarta parte) y más cerca de la peonza.
        /// </summary>
        private static SpiralParticleStyle[] BurnSwirl() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Remolino de llamas", emitter = VfxType.ChargeFlame, rate = 35f, radius = new Vector2(0.45f, 0.7f),
                height = new Vector2(0f, 0.15f), tangentialSpeed = new Vector2(4f, 6f), outwardSpeed = new Vector2(-2f, -1.2f),
                upSpeed = new Vector2(1.5f, 3f), size = new Vector2(0.12f, 0.2f), lifetime = new Vector2(0.25f, 0.4f)
            },
            new SpiralParticleStyle
            {
                name = "Remolino de chispas", emitter = VfxType.LowSpinSpark, rate = 15f, radius = new Vector2(0.35f, 0.6f),
                height = new Vector2(0f, 0.2f), tangentialSpeed = new Vector2(4.5f, 6.5f), outwardSpeed = new Vector2(-1.2f, -0.6f),
                upSpeed = new Vector2(2f, 3.5f), size = new Vector2(0.07f, 0.1f), lifetime = new Vector2(0.3f, 0.5f),
                color = new Color(1f, 0.95f, 0.6f), color2 = new Color(1f, 0.6f, 0.15f)
            }
        };

        /// <summary>Choque de una peonza quemada: como el del Fuego, más pequeño y sin el aro de llamas.</summary>
        private static SpiralParticleStyle[] BurnImpact() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Chispas", emitter = VfxType.LowSpinSpark, count = 10, radius = new Vector2(0.1f, 0.2f),
                height = new Vector2(0.1f, 0.25f), tangentialSpeed = Vector2.zero, outwardSpeed = new Vector2(3f, 5f),
                upSpeed = new Vector2(2f, 4f), size = new Vector2(0.07f, 0.12f), lifetime = new Vector2(0.4f, 0.7f),
                color = new Color(1f, 0.95f, 0.6f), color2 = new Color(1f, 0.5f, 0.1f)
            },
            new SpiralParticleStyle
            {
                name = "Ceniza", emitter = VfxType.SpecialAura, count = 6, radius = new Vector2(0.2f, 0.3f),
                height = new Vector2(0.2f, 0.35f), tangentialSpeed = new Vector2(0.2f, 0.4f), outwardSpeed = new Vector2(1f, 2f),
                upSpeed = new Vector2(0.6f, 1.2f), size = new Vector2(0.07f, 0.12f), lifetime = new Vector2(1f, 1.6f),
                color = new Color(0.25f, 0.23f, 0.22f), color2 = new Color(0.55f, 0.52f, 0.5f), alpha = 0.85f
            }
        };

        /// <summary>Humo gris que sube despacio de la peonza quemada.</summary>
        private static SpecialParticleStyle BurnSmoke() => new SpecialParticleStyle
        {
            name = "Humo", emitter = VfxType.LowSpinSmoke, rate = 6f, spawnRadius = new Vector2(0.1f, 0.4f),
            height = new Vector2(0.2f, 0.4f), coneAngle = 20f, speed = new Vector2(0.5f, 1.2f), inheritVelocity = 0f,
            size = new Vector2(0.25f, 0.45f), lifetime = new Vector2(0.8f, 1.4f), color = new Color(0.2f, 0.2f, 0.2f),
            color2 = new Color(0.4f, 0.4f, 0.42f), alpha = 0.6f
        };

        /// <summary>
        /// Pasa a URP (Particles/Unlit) los materiales de partículas del render antiguo (Particles/Standard Unlit) de una
        /// carpeta de un paquete: textura, color (los HDR se quedan), transparencia y modo de mezcla (Fade → Alpha,
        /// Additive, Modulate → Multiply). Los que ya son de URP no se tocan.
        /// </summary>
        public static void ConvertBuiltInParticles(string folder)
        {
            Shader urp = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urp == null) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null || m.shader == urp || m.shader == null || !m.shader.name.StartsWith("Particles/")) continue;
                Texture tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 2f;
                float colorMode = m.HasProperty("_ColorMode") ? m.GetFloat("_ColorMode") : 0f;

                m.shader = urp;
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", color);
                if (mode == 0f)
                {
                    m.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Opaque);
                }
                else
                {
                    m.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Transparent);
                    var blend = mode == 4f ? BaseShaderGUI.BlendMode.Additive
                        : mode == 6f ? BaseShaderGUI.BlendMode.Multiply
                        : BaseShaderGUI.BlendMode.Alpha;
                    m.SetFloat("_Blend", (float)blend);
                }
                m.SetFloat("_ColorMode", colorMode);
                m.SetFloat("_SoftParticlesEnabled", 0f);
                m.SetFloat("_CameraFadingEnabled", 0f);
                m.SetFloat("_DistortionEnabled", 0f);
                BaseShaderGUI.SetMaterialKeywords(m, null,
                    ParticleGUI.SetMaterialKeywords);
                EditorUtility.SetDirty(m);
            }
        }

        /// <summary>Chispas de fuego que crepitan (saltan a chasquidos y caen) y ceniza que sube despacio.</summary>
        private static SpecialParticleStyle[] FireParticles() => new[]
        {
            new SpecialParticleStyle
            {
                name = "Chispas", emitter = VfxType.LowSpinSpark, rate = 10f, burstInterval = new Vector2(0.15f, 0.6f),
                burstCount = new Vector2(3f, 7f), spawnRadius = new Vector2(0.2f, 0.6f), height = new Vector2(0.05f, 0.25f),
                coneAngle = 35f, speed = new Vector2(3f, 6f), inheritVelocity = 0.2f, size = new Vector2(0.08f, 0.14f),
                lifetime = new Vector2(0.4f, 0.8f), color = new Color(1f, 0.85f, 0.3f), color2 = new Color(1f, 0.4f, 0.05f), alpha = 1f
            },
            new SpecialParticleStyle
            {
                name = "Ceniza", emitter = VfxType.SpecialAura, rate = 8f, spawnRadius = new Vector2(0.3f, 0.7f),
                height = new Vector2(0.15f, 0.4f), coneAngle = 25f, speed = new Vector2(0.4f, 1.2f), inheritVelocity = 0f,
                size = new Vector2(0.05f, 0.1f), lifetime = new Vector2(1.2f, 2f), color = new Color(0.25f, 0.23f, 0.22f),
                color2 = new Color(0.55f, 0.52f, 0.5f), alpha = 0.85f
            }
        };

        /// <summary>Valores de partida del Fuego (para ajustarlos jugando con mando).</summary>
        private static SpecialVfxStyle DefaultFire()
        {
            var fire = new SpecialVfxStyle { type = SpecialAbilityType.Fire, replaceAura = true };
            BurstTipStyle tip = fire.tip;
            // 6_flamelash: 46 dibujos en 7 × 7; del 12 al 27, el aro cerrado (antes crece y después se deshace en morado)
            tip.tilesX = 7;
            tip.tilesY = 7;
            tip.firstFrame = 12;
            tip.frames = 16;
            tip.cycle = 0.6f;
            tip.size = 2f;
            tip.scale = new Vector2(1f, 0.6f);
            tip.alpha = 0.95f;
            tip.fade = new Vector2(0.1f, 0.25f);
            tip.cups.Add(new BurstTipCup { angle = 0f, spinSpeed = 200f, scale = new Vector2(0.7f, 0.8f), playerColor = false, color = Color.white });
            tip.cups.Add(new BurstTipCup { angle = 120f, spinSpeed = -160f, scale = Vector2.one, playerColor = false, color = new Color(1f, 0.85f, 0.6f, 0.9f) });
            tip.cups.Add(new BurstTipCup { angle = 240f, spinSpeed = 120f, scale = new Vector2(0.85f, 1.2f), playerColor = false, color = new Color(1f, 1f, 1f, 0.8f) });
            // Sin llamas en cono (Llama11 sigue en la lista de sprites): chispas y ceniza, en particles
            tip.particleSprite = "";
            // 16_sunburn: aro de fuego en bucle, 61 dibujos en 8 × 8
            tip.ground = new TipGroundSprite { tilesX = 8, tilesY = 8, frames = 61, cycle = 1f, size = 3f, height = 0.02f, spinSpeed = 60f };
            // 7_firespin: aro que gira, 61 dibujos en 8 × 8
            fire.overlay = new SpecialOverlayStyle
            {
                tilesX = 8, tilesY = 8, frames = 61, cycle = 0.8f, textureScale = 2.2f, textureSpin = 120f,
                textureColor = new Color(1f, 1f, 1f, 0.9f)
            };
            fire.particles.AddRange(FireParticles());
            fire.scorch = new ScorchStyle { sprite = "Quemado" };
            return fire;
        }

        /// <summary>
        /// Copia de una hoja de CodeManu en Assets/VFX/Textures/CodeManu (la del pack no se sube), con filtro de punto
        /// para que el pixel art se vea nítido. Si ya está copiada, la de la copia.
        /// </summary>
        public static Texture2D CodeManuSheet(string name)
        {
            string target = $"{CodeManuCopyFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            if (existing != null) return existing;
            string source = CodeManuFolder + name + ".png";
            if (!File.Exists(source)) return null;

            FakeBladeAssetsMenu.EnsureFolder(CodeManuCopyFolder);
            File.Copy(source, target);
            AssetDatabase.ImportAsset(target);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
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
