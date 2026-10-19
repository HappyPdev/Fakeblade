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
            /// <summary>Textura generada de Onda de choque (AroPicos, OndaChoque, NubeComic, Grietas; ver Generated).</summary>
            public string Generated;
            /// <summary>La hoja se anima al revés (círculos que se cierran).</summary>
            public bool Reverse;
            /// <summary>Textura generada de ruido con la que se disuelve (shader FakeBlade/ParticleDissolve).</summary>
            public string Dissolve;
            /// <summary>Degradado del humo de una explosión (blanco, amarillo, naranja y gris), como CFXR Explosion 1.</summary>
            public bool ExplosionColors;
            /// <summary>Degradado del humo sin fuego (blanco que pasa a gris): el del Tornado.</summary>
            public bool SmokeColors;
            /// <summary>Dibujada por el usuario en la carpeta de impactos (Grieta00-03): se usa tal cual, importada como pixel art.</summary>
            public string UserDrawn;
            /// <summary>Cada partícula, un dibujo de la hoja al azar, sin animar.</summary>
            public bool RandomFrame;
            public float FadeIn, FadeOut, Gravity, Drag, Spin, ShrinkOut, GrowIn, Stretch;
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
            // Onda de choque (K8, 2026-10-18; boceto del usuario; texturas generadas, blancas con contorno negro para teñirlas):
            // aro de picos que se cierra sobre la peonza en la carga, líneas de velocidad hacia dentro, nubes de cómic que se
            // tragan y que salen despedidas en la explosión, la onda que se abre por el suelo hasta el alcance y grietas
            new DefaultSprite { Id = "AroPicos", Generated = "AroPicos", TilesX = 1, TilesY = 1, Frames = 1, Life = 0.4f, Order = -1, DepthTested = true, Flat = true, FadeIn = 0.25f, ShrinkOut = 0.99f, Max = 64 },
            new DefaultSprite { Id = "OndaChoque", Generated = "OndaChoque", TilesX = 1, TilesY = 1, Frames = 1, Life = 0.45f, Order = -1, DepthTested = true, Flat = true, FadeOut = 0.5f, GrowIn = 1f, Max = 32 },
            new DefaultSprite { Id = "NubeComic", Generated = "NubeComic", Dissolve = "NubeComicDisuelve", TilesX = 1, TilesY = 1, Frames = 1, Life = 0.8f, Order = 1, DepthTested = true, FadeOut = 0.6f, Drag = 0.06f, Spin = 40f, Max = 512 },
            // Humo de la explosión (2026-10-18; copiado de CFXR Explosion 1): sale disparado y frena, crece, pasa de blanco a
            // amarillo, naranja y gris, y al final se rompe en pedacitos (disolución)
            new DefaultSprite { Id = "HumoExplosion", Generated = "HumoComic", Dissolve = "HumoComicDisuelve", ExplosionColors = true, RandomFrame = true, TilesX = 2, TilesY = 2, Frames = 4, Life = 1.1f, Order = 1, DepthTested = true, GrowIn = 0.4f, Drag = 0.08f, Spin = 30f, Max = 512 },
            new DefaultSprite { Id = "NubeComicTraga", Generated = "NubeComic", TilesX = 1, TilesY = 1, Frames = 1, Life = 0.4f, Order = 1, DepthTested = true, FadeIn = 0.3f, ShrinkOut = 0.6f, Spin = 90f, Max = 256 },
            new DefaultSprite { Id = "Grietas", Generated = "Grietas", TilesX = 1, TilesY = 1, Frames = 1, Life = 1.2f, Order = -2, DepthTested = true, Flat = true, FadeOut = 0.6f, Max = 32 },
            // Grietas dibujadas por el usuario (2026-10-19; Grieta, GrietaInicio y GrietaCircular + número): una al azar en cada
            // onda, con tamaño y giro al azar (Sprite Variants), para que cada golpe deje una distinta; también para el Salto al caer
            new DefaultSprite { Id = "Grieta00", UserDrawn = "Grieta00", TilesX = 1, TilesY = 1, Frames = 1, Life = 1.2f, Order = -2, DepthTested = true, Flat = true, FadeOut = 0.6f, Max = 32 },
            new DefaultSprite { Id = "Grieta01", UserDrawn = "Grieta01", TilesX = 1, TilesY = 1, Frames = 1, Life = 1.2f, Order = -2, DepthTested = true, Flat = true, FadeOut = 0.6f, Max = 32 },
            new DefaultSprite { Id = "Grieta02", UserDrawn = "Grieta02", TilesX = 1, TilesY = 1, Frames = 1, Life = 1.2f, Order = -2, DepthTested = true, Flat = true, FadeOut = 0.6f, Max = 32 },
            new DefaultSprite { Id = "Grieta03", UserDrawn = "Grieta03", TilesX = 1, TilesY = 1, Frames = 1, Life = 1.2f, Order = -2, DepthTested = true, Flat = true, FadeOut = 0.6f, Max = 32 },
            new DefaultSprite { Id = "GrietaInicio00", UserDrawn = "GrietaInicio00", TilesX = 1, TilesY = 1, Frames = 1, Life = 1.2f, Order = -2, DepthTested = true, Flat = true, FadeOut = 0.6f, Max = 32 },
            new DefaultSprite { Id = "GrietaCircular01", UserDrawn = "GrietaCircular01", TilesX = 1, TilesY = 1, Frames = 1, Life = 1.2f, Order = -2, DepthTested = true, Flat = true, FadeOut = 0.6f, Max = 32 },
            new DefaultSprite { Id = "GrietaCircular02", UserDrawn = "GrietaCircular02", TilesX = 1, TilesY = 1, Frames = 1, Life = 1.2f, Order = -2, DepthTested = true, Flat = true, FadeOut = 0.6f, Max = 32 },
            new DefaultSprite { Id = "GrietaCircular03", UserDrawn = "GrietaCircular03", TilesX = 1, TilesY = 1, Frames = 1, Life = 1.2f, Order = -2, DepthTested = true, Flat = true, FadeOut = 0.6f, Max = 32 },
            new DefaultSprite { Id = "Linea", GeneratedDot = true, TilesX = 1, TilesY = 1, Frames = 1, Life = 0.3f, Order = 1, Upright = true, DepthTested = true, FadeIn = 0.2f, FadeOut = 0.3f, Stretch = 0.08f, Max = 256 },
            // Círculos del pack tumbados en el suelo (2026-10-18, pedido por el usuario: que no sean círculos exactos): abriéndose
            // en la explosión de Onda de choque y al revés (cerrándose) en su carga, en capas a distinta altura y tamaño
            new DefaultSprite { Id = "CircleImpact02Suelo", Texture = "CircleImpact02", TilesX = 3, TilesY = 3, Frames = 9, Life = 0.4f, Order = -1, DepthTested = true, Flat = true },
            new DefaultSprite { Id = "FP_CircleImpactSuelo", Texture = "FP_CircleImpact", TilesX = 3, TilesY = 3, Frames = 9, Life = 0.4f, Order = -1, DepthTested = true, Flat = true },
            new DefaultSprite { Id = "ShotValo2Suelo", Texture = "ImpactShotValo2", TilesX = 3, TilesY = 3, Frames = 9, Life = 0.35f, Order = -1, DepthTested = true, Flat = true },
            new DefaultSprite { Id = "CircleImpact02Cierra", Texture = "CircleImpact02", TilesX = 3, TilesY = 3, Frames = 9, Life = 0.4f, Order = -1, DepthTested = true, Flat = true, Reverse = true },
            new DefaultSprite { Id = "FP_CircleImpactCierra", Texture = "FP_CircleImpact", TilesX = 3, TilesY = 3, Frames = 9, Life = 0.4f, Order = -1, DepthTested = true, Flat = true, Reverse = true },
            // Tornado (K8, 2026-10-18, pedido por el usuario): la media luna de CircleImpact04 cerrándose (carga), ImpactShotValo
            // tumbado en el suelo abriéndose (al soltar) y el humo de la explosión sin el fuego: uno que se traga hacia la
            // peonza mientras se forma el tornado y otro que sale despedido y se disuelve al soltarlo
            new DefaultSprite { Id = "CircleImpact04Cierra", Texture = "CircleImpact04", TilesX = 3, TilesY = 3, FirstFrame = 2, Frames = 7, Life = 0.4f, Order = -1, DepthTested = true, Flat = true, Reverse = true },
            new DefaultSprite { Id = "ShotValoSuelo", Texture = "ImpactShotValo", TilesX = 3, TilesY = 3, Frames = 9, Life = 0.35f, Order = -1, DepthTested = true, Flat = true },
            new DefaultSprite { Id = "HumoTraga", Generated = "HumoComic", SmokeColors = true, RandomFrame = true, TilesX = 2, TilesY = 2, Frames = 4, Life = 0.5f, Order = 1, DepthTested = true, FadeIn = 0.3f, ShrinkOut = 0.6f, Spin = 90f, Max = 256 },
            new DefaultSprite { Id = "HumoViento", Generated = "HumoComic", Dissolve = "HumoComicDisuelve", SmokeColors = true, RandomFrame = true, TilesX = 2, TilesY = 2, Frames = 4, Life = 1.1f, Order = 1, DepthTested = true, GrowIn = 0.4f, Drag = 0.08f, Spin = 30f, Max = 512 },
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
                    fadeOut = d.FadeOut, fadeIn = d.FadeIn, shrinkOut = d.ShrinkOut, growIn = d.GrowIn, stretch = d.Stretch, reverse = d.Reverse, randomFrame = d.RandomFrame, gravity = d.Gravity, drag = d.Drag, spin = d.Spin,
                    maxParticles = d.Max > 0 ? d.Max : 128
                });
                ImpactSprite created = library.impactSprites[library.impactSprites.Count - 1];
                if (!string.IsNullOrEmpty(d.Dissolve)) created.dissolveTexture = GeneratedTexture(d.Dissolve);
                if (d.ExplosionColors)
                {
                    created.useColorOverLife = true;
                    created.colorOverLife = ExplosionGradient();
                }
                if (d.SmokeColors)
                {
                    created.useColorOverLife = true;
                    created.colorOverLife = SmokeGradient();
                }
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
                Material material = sprite.dissolveTexture != null
                    ? GetDissolveMaterial(sprite.texture, sprite.dissolveTexture, sprite.dissolveSmooth)
                    : GetMaterial(sprite.texture);
                sprite.prefab = CreateEffect(sprite, material);
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
            if (!string.IsNullOrEmpty(d.UserDrawn)) return UserTexture(d.UserDrawn);
            if (!string.IsNullOrEmpty(d.Generated)) return GeneratedTexture(d.Generated);
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

        /// <summary>
        /// Texturas de Onda de choque (K8, 2026-10-18; boceto del usuario), en pixel art (filtro de punto, sin degradados),
        /// blancas con el contorno negro para teñirlas (el negro se queda negro): AroPicos, un aro con picos hacia dentro (el
        /// contorno dentado del boceto); OndaChoque, la onda de cómic que se abre por el suelo; NubeComic, una nube de cómic
        /// (círculos juntos, con sombra abajo); Grietas, grietas que salen del centro (blancas, se tiñen de oscuro).
        /// Siempre las mismas (semilla fija).
        /// </summary>
        /// <summary>
        /// Textura dibujada por el usuario en la carpeta de impactos (Grieta00-03): no se genera ni se sobrescribe; solo se
        /// importa como las generadas (pixel art: sin suavizado, sin compresión ni mipmaps). Null si no existe.
        /// </summary>
        private static Texture2D UserTexture(string name)
        {
            string path = $"{ConvertedFolder}/{name}.png";
            if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                (importer.filterMode != FilterMode.Point || importer.mipmapEnabled || !importer.alphaIsTransparency ||
                 importer.textureCompression != TextureImporterCompression.Uncompressed || importer.wrapMode != TextureWrapMode.Clamp))
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D GeneratedTexture(string name, bool force = false)
        {
            string target = $"{ConvertedFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
            if (existing != null && !force) return existing;
            switch (name)
            {
                case "AroPicos": return SaveTexture(target, 64, SpikeRingPixels(64));
                case "OndaChoque": return SaveTexture(target, 64, ShockRingPixels(64));
                case "NubeComic": return SaveTexture(target, 64, ComicCloudPixels(64));
                case "Grietas": return SaveTexture(target, 64, CrackPixels(64));
                case "LineasVelocidad": return SaveTexture(target, 128, SpeedLinePixels(128, 64), 64, TextureWrapMode.Repeat);
                case "MarcaUso": return SaveTexture(target, 16, UseMarkPixels(16));
                case "NubeComicDisuelve": return SaveTexture(target, 64, DissolveNoisePixels(64));
                case "HumoComic": return SaveTexture(target, 128, SmokeSheetPixels(64));
                case "HumoComicDisuelve": return SaveTexture(target, 128, TileSheet(64, (size, index) => DissolveNoisePixels(size, 27 + index)));
                default: return DotTexture();
            }
        }

        /// <summary>
        /// Vuelve a generar una textura de las de arriba encima del mismo archivo (mismo GUID: los materiales y prefabs que la
        /// usan la siguen teniendo). Para cambiar su dibujo desde el código.
        /// </summary>
        public static Texture2D RegenerateTexture(string name) => GeneratedTexture(name, true);

        /// <summary>Líneas de velocidad horizontales (textura que recorre la cúpula de Onda de choque; la crea si falta).</summary>
        public static Texture2D SpeedLinesTexture() => GeneratedTexture("LineasVelocidad");

        /// <summary>
        /// Marca del aviso de usos (2026-10-18): círculo de 16 × 16 blanco con contorno negro y un brillo, para teñirlo con el
        /// color del poder. La usa el material VFX_UseMark (Setup Rastros).
        /// </summary>
        public static Texture2D UseMarkTexture() => GeneratedTexture("MarcaUso");

        /// <summary>
        /// Marca del aviso de usos (2026-10-18; la primera, con el contorno negro, no gustó): orbe de luz sin contorno, con el
        /// centro blanco, un aro del color del poder (gris claro, se tiñe) y el borde translúcido, en pixel art.
        /// </summary>
        private static Color32[] UseMarkPixels(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / (size * 0.5f); // 0 en el centro, 1 en el borde
                Color32 c = Clear;
                if (d <= 1f) c = new Color32(200, 200, 200, 110);
                if (d <= 0.78f) c = new Color32(215, 215, 215, 230);
                if (d <= 0.45f) c = Paper;
                pixels[y * size + x] = c;
            }
            return pixels;
        }

        private static readonly Color32 Clear = new Color32(255, 255, 255, 0);
        private static readonly Color32 Ink = new Color32(13, 13, 13, 255);
        private static readonly Color32 Paper = new Color32(255, 255, 255, 255);
        private static readonly Color32 Shade = new Color32(190, 190, 190, 255);

        /// <summary>Aro con 14 picos hacia dentro y el borde de fuera un poco irregular, blanco con contorno negro de 2 px.</summary>
        private static Color32[] SpikeRingPixels(int size)
        {
            var random = new System.Random(5);
            const int spikes = 14;
            var jitter = new float[spikes];
            for (int i = 0; i < spikes; i++) jitter[i] = (float)random.NextDouble();
            var pixels = new Color32[size * size];
            float half = size * 0.5f, line = 2f / half;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float turn = (Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 1f) * spikes;
                int index = (int)turn % spikes;
                float f = turn - Mathf.Floor(turn);
                // Diente de sierra: cada pico entra hacia el centro una longitud distinta
                float tooth = 1f - Mathf.Abs(f - 0.5f) * 2f;
                float inner = 0.78f - tooth * (0.18f + jitter[index] * 0.12f);
                float outer = 0.93f + (jitter[(index + 3) % spikes] - 0.5f) * 0.04f;
                Color32 c = Clear;
                if (d >= inner && d <= outer)
                    c = d - inner < line * 1.2f || outer - d < line ? Ink : Paper;
                pixels[y * size + x] = c;
            }
            return pixels;
        }

        /// <summary>
        /// Onda de cómic: banda blanca gruesa con contorno negro, y por dentro arcos sueltos más finos y translúcidos (como
        /// líneas de movimiento).
        /// </summary>
        private static Color32[] ShockRingPixels(int size)
        {
            var random = new System.Random(9);
            float[,] noise = RandomLattice(random, 9);
            var pixels = new Color32[size * size];
            float half = size * 0.5f, line = 1.5f / half;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float wobble = (Lattice(noise, u * 8f, v * 8f) - 0.5f) * 0.05f;
                float inner = 0.8f + wobble, outer = 0.97f;
                Color32 c = Clear;
                if (d >= inner && d <= outer) c = d - inner < line || outer - d < line ? Ink : Paper;
                else if (d >= 0.62f && d < inner - line * 1.5f)
                {
                    // Arcos sueltos por dentro: solo donde el ruido lo deja
                    float arc = Lattice(noise, u * 8f + 2.3f, v * 8f + 4.1f);
                    if (arc > 0.55f && d > 0.66f && d < 0.72f) c = new Color32(255, 255, 255, 150);
                }
                pixels[y * size + x] = c;
            }
            return pixels;
        }

        /// <summary>
        /// Nube de cómic: cuatro círculos juntos, blanca con sombra gris abajo y un contorno fino (1 px de 64) en gris medio, no
        /// negro: al teñirla, el contorno toma el color de la nube y no queda tan marcado (2026-10-18, a petición del usuario;
        /// la primera, de 32 px con el contorno negro, era demasiado gruesa).
        /// </summary>
        private static Color32[] ComicCloudPixels(int size)
        {
            // Centros (u, v) y radios, en partes de la textura
            var circles = new[]
            {
                new Vector3(0.5f, 0.42f, 0.27f), new Vector3(0.3f, 0.5f, 0.2f),
                new Vector3(0.7f, 0.5f, 0.2f), new Vector3(0.52f, 0.66f, 0.2f)
            };
            var outline = new Color32(120, 120, 120, 255);
            var pixels = new Color32[size * size];
            float line = 1.1f / size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                // Distancia con signo a la unión de círculos (positiva dentro)
                float inside = -1f;
                for (int i = 0; i < circles.Length; i++)
                {
                    float du = u - circles[i].x, dv = v - circles[i].y;
                    inside = Mathf.Max(inside, circles[i].z - Mathf.Sqrt(du * du + dv * dv));
                }
                Color32 c = Clear;
                if (inside > 0f)
                {
                    if (inside < line) c = outline;
                    // Sombra abajo (en la textura, v pequeña), con un borde en curva
                    else if (v < 0.36f + (u - 0.5f) * (u - 0.5f) * 0.6f) c = Shade;
                    else c = Paper;
                }
                pixels[y * size + x] = c;
            }
            return pixels;
        }

        /// <summary>
        /// Ruido para disolver las nubes (2026-10-18; la idea del humo de CFXR Explosion 1): manchas redondeadas de ruido de dos
        /// escalas, más claras en el centro, en 8 niveles. Al disolverse, primero se va el borde y lo oscuro, y quedan
        /// pedacitos sueltos que se hacen pequeños. Nunca llega a 0 (con la nube entera no se come nada).
        /// </summary>
        private static Color32[] DissolveNoisePixels(int size, int seed = 27)
        {
            var random = new System.Random(seed);
            float[,] coarse = RandomLattice(random, 7), fine = RandomLattice(random, 13);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                float noise = Lattice(coarse, u * 6f, v * 6f) * 0.6f + Lattice(fine, u * 12f, v * 12f) * 0.4f;
                float value = noise * 0.7f + (1f - Mathf.Clamp01(d)) * 0.3f;
                value = Mathf.Lerp(0.05f, 1f, Mathf.Round(Mathf.Clamp01(value) * 8f) / 8f);
                byte b = (byte)(value * 255f);
                pixels[y * size + x] = new Color32(b, b, b, 255);
            }
            return pixels;
        }

        /// <summary>
        /// Hoja de 2 × 2 nubes de humo distintas (humo de la explosión de Onda de choque, 2026-10-18; idea de la hoja «smoke cloud
        /// x4» de CFXR Explosion 1): cada una, de 5 a 7 círculos al azar alrededor del centro, blanca, con una sombra suave
        /// (gris claro, solo una media luna abajo) y un contorno fino en gris medio. Con formas distintas y solapadas, el humo no
        /// parece piedras.
        /// </summary>
        private static Color32[] SmokeSheetPixels(int cell) => TileSheet(cell, (size, index) =>
        {
            var random = new System.Random(41 + index * 7);
            int count = 5 + random.Next(3);
            var circles = new Vector3[count];
            // Uno grande en el centro y los demás alrededor, más pequeños
            circles[0] = new Vector3(0.5f, 0.47f, 0.28f + (float)random.NextDouble() * 0.05f);
            for (int i = 1; i < count; i++)
            {
                float angle = (i / (float)(count - 1) + (float)random.NextDouble() * 0.15f) * Mathf.PI * 2f;
                float distance = 0.15f + (float)random.NextDouble() * 0.09f;
                circles[i] = new Vector3(0.5f + Mathf.Cos(angle) * distance, 0.5f + Mathf.Sin(angle) * distance * 0.85f,
                    0.14f + (float)random.NextDouble() * 0.08f);
            }
            var outline = new Color32(140, 140, 140, 255);
            var shade = new Color32(212, 212, 212, 255);
            var pixels = new Color32[size * size];
            float line = 1.1f / size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float inside = -1f, insideShifted = -1f;
                for (int i = 0; i < count; i++)
                {
                    float du = u - circles[i].x, dv = v - circles[i].y;
                    inside = Mathf.Max(inside, circles[i].z - Mathf.Sqrt(du * du + dv * dv));
                    // La misma nube un poco más abajo: lo que queda fuera de ella es la parte de arriba (luz)
                    float dvs = v - circles[i].y + 0.06f;
                    insideShifted = Mathf.Max(insideShifted, circles[i].z - Mathf.Sqrt(du * du + dvs * dvs));
                }
                Color32 c = Clear;
                if (inside > 0f)
                {
                    if (inside < line) c = outline;
                    // Sombra: la media luna de abajo (dentro de la nube pero fuera de la copia subida)
                    else if (insideShifted <= 0f && v < 0.5f) c = shade;
                    else c = Paper;
                }
                pixels[y * size + x] = c;
            }
            return pixels;
        });

        /// <summary>Junta 4 dibujos de cell × cell en una hoja de 2 × 2 (el primero arriba a la izquierda, como las hojas).</summary>
        private static Color32[] TileSheet(int cell, System.Func<int, int, Color32[]> draw)
        {
            int size = cell * 2;
            var sheet = new Color32[size * size];
            for (int index = 0; index < 4; index++)
            {
                Color32[] tile = draw(cell, index);
                // En la textura, y = 0 es la fila de abajo: el dibujo 0 va arriba a la izquierda
                int ox = (index % 2) * cell, oy = (1 - index / 2) * cell;
                for (int y = 0; y < cell; y++)
                for (int x = 0; x < cell; x++)
                    sheet[(oy + y) * size + ox + x] = tile[y * cell + x];
            }
            return sheet;
        }

        /// <summary>
        /// Degradado del humo de una explosión, copiado de CFXR Explosion 1: blanco, amarillo (al 10 %), naranja (17 %) y gris
        /// (desde el 33 %); entero hasta el 60 % de su vida y después se disuelve.
        /// </summary>
        private static Gradient ExplosionGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0.03f), new GradientColorKey(new Color(1f, 0.81f, 0f), 0.1f),
                    new GradientColorKey(new Color(1f, 0.38f, 0.12f), 0.17f), new GradientColorKey(new Color(0.53f, 0.53f, 0.53f), 0.33f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        /// <summary>
        /// Degradado del humo del Tornado (2026-10-18): el de la explosión sin el fuego, de blanco a gris claro (desde el 35 %);
        /// entero hasta el 60 % de su vida y después se disuelve.
        /// </summary>
        private static Gradient SmokeGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.05f), new GradientColorKey(new Color(0.72f, 0.72f, 0.72f), 0.35f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        /// <summary>
        /// Líneas de velocidad (cúpula de Onda de choque, 2026-10-18): trazos horizontales blancos de distinto largo y grosor, con
        /// las puntas más finas y translúcidas, sobre transparente. Se repite sin costuras (los trazos dan la vuelta).
        /// </summary>
        private static Color32[] SpeedLinePixels(int width, int height)
        {
            var random = new System.Random(21);
            var alpha = new byte[width * height];
            int y = 1;
            while (y < height - 1)
            {
                int strokes = 1 + random.Next(2);
                for (int k = 0; k < strokes; k++)
                {
                    int length = 12 + random.Next(width / 2);
                    int start = random.Next(width);
                    int thick = random.NextDouble() < 0.35 ? 2 : 1;
                    for (int i = 0; i < length; i++)
                    {
                        float t = i / (float)(length - 1);
                        // Puntas: el primer y el último cuarto, más translúcidas; el centro, opaco
                        byte a = t < 0.15f || t > 0.85f ? (byte)110 : t < 0.3f || t > 0.7f ? (byte)190 : (byte)255;
                        int x = (start + i) % width;
                        for (int r = 0; r < thick && y + r < height; r++)
                        {
                            int index = (y + r) * width + x;
                            if (a > alpha[index]) alpha[index] = a;
                        }
                    }
                }
                y += 3 + random.Next(4);
            }
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, alpha[i]);
            return pixels;
        }

        /// <summary>Grietas: 7 líneas quebradas que salen del centro y se afinan, blancas (se tiñen de oscuro al usarlas).</summary>
        private static Color32[] CrackPixels(int size)
        {
            var random = new System.Random(13);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Clear;
            float half = size * 0.5f;
            const int cracks = 7;
            for (int k = 0; k < cracks; k++)
            {
                float angle = (k + (float)random.NextDouble() * 0.6f) / cracks * Mathf.PI * 2f;
                float length = half * (0.6f + (float)random.NextDouble() * 0.35f);
                float r = half * 0.18f;
                var p = new Vector2(half + Mathf.Cos(angle) * r, half + Mathf.Sin(angle) * r);
                while (r < length)
                {
                    // Quiebros: el rumbo cambia un poco en cada paso
                    angle += ((float)random.NextDouble() - 0.5f) * 0.9f;
                    p += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    r = Vector2.Distance(p, new Vector2(half, half));
                    int width = r < length * 0.55f ? 2 : 1;
                    for (int oy = 0; oy < width; oy++)
                    for (int ox = 0; ox < width; ox++)
                    {
                        int px = (int)p.x + ox, py = (int)p.y + oy;
                        if (px >= 0 && py >= 0 && px < size && py < size) pixels[py * size + px] = Paper;
                    }
                }
            }
            return pixels;
        }

        /// <summary>Guarda los píxeles como PNG y lo importa en pixel art (filtro de punto, sin mipmaps ni compresión). height 0 = cuadrada.</summary>
        private static Texture2D SaveTexture(string target, int size, Color32[] pixels, int height = 0,
            TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var tex = new Texture2D(size, height > 0 ? height : size, TextureFormat.RGBA32, false);
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
            importer.wrapMode = wrap;
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

        /// <summary>
        /// Material de un sprite que se disuelve (shader FakeBlade/ParticleDissolve): VFX_[textura]_Disuelve, con la textura y su
        /// ruido. Si falta el shader, el de siempre.
        /// </summary>
        private static Material GetDissolveMaterial(Texture2D texture, Texture2D dissolve, float smooth)
        {
            Shader shader = Shader.Find("FakeBlade/ParticleDissolve");
            if (shader == null) return GetMaterial(texture);
            string path = $"{MaterialFolder}/VFX_{texture.name}_Disuelve.mat";
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
            material.SetTexture("_MainTex", texture);
            material.SetTexture("_DissolveTex", dissolve);
            material.SetFloat("_DissolveSmooth", smooth);
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
            fade.enabled = s.fadeOut > 0f || s.fadeIn > 0f || s.useColorOverLife;
            if (s.useColorOverLife && s.colorOverLife != null) fade.color = s.colorOverLife;
            else if (fade.enabled)
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

            // Crece desde nada al principio, frenando (onda de Onda de choque), y se encoge hasta desaparecer al final de la
            // vida (ceniza)
            var shrink = ps.sizeOverLifetime;
            shrink.enabled = s.shrinkOut > 0f || s.growIn > 0f;
            if (shrink.enabled)
            {
                var curve = new AnimationCurve();
                float grown = Mathf.Clamp(s.growIn, 0f, 1f);
                float start = Mathf.Clamp(1f - s.shrinkOut, grown, 1f);
                if (grown > 0f)
                {
                    // Sale a toda velocidad y frena al llegar a su tamaño
                    curve.AddKey(new Keyframe(0f, 0f, 2f / grown, 2f / grown));
                    curve.AddKey(new Keyframe(grown, 1f, 0f, 0f));
                }
                else curve.AddKey(new Keyframe(0f, 1f));
                if (s.shrinkOut > 0f)
                {
                    if (start > grown + 0.001f) curve.AddKey(new Keyframe(start, 1f));
                    curve.AddKey(new Keyframe(1f, 0f));
                }
                else if (grown < 1f) curve.AddKey(new Keyframe(1f, 1f));
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
            if (s.randomFrame)
            {
                // Un dibujo al azar por partícula, quieto (las 4 nubes de HumoComic)
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                sheet.startFrame = new ParticleSystem.MinMaxCurve(Mathf.Clamp(s.firstFrame, 0, tiles - 1),
                    Mathf.Min(s.firstFrame + s.frames, tiles) - 0.01f);
            }
            // Al revés: del último dibujo al primero (círculos que se cierran)
            else
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, s.reverse
                ? AnimationCurve.Linear(0f, last, 1f, first) : AnimationCurve.Linear(0f, first, 1f, last));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = s.flat ? ParticleSystemRenderMode.HorizontalBillboard
                : s.stretch > 0f ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (s.stretch > 0f && !s.flat)
            {
                // Líneas de velocidad: largas según lo rápido que van
                renderer.velocityScale = s.stretch;
                renderer.lengthScale = 1f;
            }
            // Tumbados en el suelo pueden ser más grandes que la pantalla (la onda llega a 10 m de ancho)
            renderer.maxParticleSize = s.flat ? 10f : 1f;
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
