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
                   SetupTornado(library) + "; " + SetupGenericSpecial(library);
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
