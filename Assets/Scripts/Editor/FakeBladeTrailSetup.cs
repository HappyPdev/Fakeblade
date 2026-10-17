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
        // Status and Auras FREE (Matthew Guz, Asset Store: fuera del repo, en ThirdParty). Sus materiales son del render
        // antiguo: los pasa a URP ConvertBuiltInParticles
        private const string AuraPackFolder = "Assets/ThirdParty/VFX/Status and Auras FREE";
        private const string AuraBurnPrefabPath = AuraPackFolder + "/Prefab/Aura Burn.prefab";
        private const string AuraSmokePrefabPath = AuraPackFolder + "/Prefab/Aura Smoke.prefab";

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
            fire.auras ??= new List<AuraStyle>();
            ParticleSystem smoke = AuraPrefab(AuraSmokePrefabPath);
            if (fire.auras.Count == 0 && smoke != null) fire.auras.Add(new AuraStyle { name = "Aura Smoke", prefab = smoke });
            return "especiales: Fuego" + (added ? " (nuevo)" : "") + "; " + SetupBurn(library, shader, firespin);
        }

        /// <summary>
        /// Peonza quemada (2026-10-17): como la que quema, más simple. Aro de fuego fino en el suelo (7_firespin, sin
        /// cuencos ni partículas en cono), humo, chispas y ceniza, y el Aura Burn de Status and Auras FREE si está en el
        /// proyecto. Solo rellena lo que esté vacío.
        /// </summary>
        private static string SetupBurn(VfxLibrary library, Shader shader, Texture2D firespin)
        {
            library.burn ??= new BurnEffectStyle();
            BurnEffectStyle burn = library.burn;
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

        /// <summary>Prefab de Status and Auras FREE (con sus materiales ya en URP), o null si el paquete no está.</summary>
        private static ParticleSystem AuraPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(path);
            if (prefab != null) ConvertBuiltInParticles(AuraPackFolder);
            return prefab;
        }

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

        private static SpiralParticleStyle[] FireActivationBurst() => new[]
        {
            new SpiralParticleStyle
            {
                name = "Explosión de llamas", emitter = VfxType.ChargeFlame, count = 50, radius = new Vector2(0.3f, 0.5f),
                height = new Vector2(0.05f, 0.3f), tangentialSpeed = new Vector2(6f, 8f), outwardSpeed = new Vector2(2.5f, 4f),
                upSpeed = new Vector2(0.8f, 1.6f), size = new Vector2(0.22f, 0.36f), lifetime = new Vector2(0.35f, 0.6f)
            },
            new SpiralParticleStyle
            {
                name = "Explosión de chispas", emitter = VfxType.LowSpinSpark, count = 35, radius = new Vector2(0.3f, 0.5f),
                height = new Vector2(0.05f, 0.3f), tangentialSpeed = new Vector2(6f, 9f), outwardSpeed = new Vector2(3.5f, 6f),
                upSpeed = new Vector2(1.5f, 3f), size = new Vector2(0.08f, 0.14f), lifetime = new Vector2(0.4f, 0.8f),
                color = new Color(1f, 0.95f, 0.6f), color2 = new Color(1f, 0.6f, 0.15f)
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
