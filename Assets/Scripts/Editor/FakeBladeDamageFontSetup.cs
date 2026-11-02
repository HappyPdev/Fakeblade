using System.IO;
using System.Text;
using FakeBlade.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Fuentes de los números de daño (2026-11-02): por cada TTF u OTF de Assets/ThirdParty/Free/Fonts crea su fuente TMP
    /// con solo las cifras 0-9 (atlas estático, con margen ancho para el contorno y el glow) en Assets/Fonts/DamageNumbers
    /// y la añade a HUDTheme → Damage Fonts, que es la lista de Opciones → Letra de los números. Solo crea lo que falta:
    /// para rehacer una fuente, borrar su asset y volver a pasarlo. Para añadir una fuente: su TTF (y su licencia) en una
    /// carpeta nueva de ThirdParty/Free/Fonts, y pasar este menú.
    /// </summary>
    public static class FakeBladeDamageFontSetup
    {
        private const string SourceFolder = "Assets/ThirdParty/Free/Fonts";
        private const string OutputFolder = "Assets/Fonts/DamageNumbers";
        private const string ThemePath = "Assets/Settings/HUDTheme.asset";
        private const string Characters = "0123456789";
        // Muestreo grande y margen ancho: el glow llega hasta el borde del margen
        private const int SamplingSize = 96;
        private const int Padding = 20;
        private const int AtlasSize = 512;
        /// <summary>La de por defecto: la primera de la lista (la favorita del usuario, 2026-11-02; antes Dokdo).</summary>
        private const string DefaultFamily = "Finger Paint";

        [MenuItem("FakeBlade/Setup Números de daño")]
        public static void Setup()
        {
            var theme = AssetDatabase.LoadAssetAtPath<HUDTheme>(ThemePath);
            if (theme == null)
            {
                Debug.LogError($"[FakeBlade] Setup Números de daño: no está {ThemePath}.");
                return;
            }
            EnsureFolder(OutputFolder);

            var log = new StringBuilder("[FakeBlade] Setup Números de daño:");
            foreach (string guid in AssetDatabase.FindAssets("t:Font", new[] { SourceFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font == null) continue;

                string outPath = $"{OutputFolder}/{Path.GetFileNameWithoutExtension(path)} SDF.asset";
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath);
                if (asset == null)
                {
                    asset = Create(font, outPath, out string missing);
                    log.Append($"\n- creada {outPath}{(string.IsNullOrEmpty(missing) ? "" : " (le faltan: " + missing + ")")}");
                }
                if (asset != null && !theme.damageFonts.Contains(asset))
                {
                    theme.damageFonts.Add(asset);
                    log.Append($"\n- {asset.faceInfo.familyName} añadida a HUDTheme");
                }
            }

            theme.damageFonts.RemoveAll(f => f == null);
            int first = theme.damageFonts.FindIndex(f => f.faceInfo.familyName == DefaultFamily);
            if (first > 0)
            {
                TMP_FontAsset d = theme.damageFonts[first];
                theme.damageFonts.RemoveAt(first);
                theme.damageFonts.Insert(0, d);
            }

            EnsureSpecialFx(theme, log);

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            log.Append($"\nEn Opciones: {theme.damageFonts.Count} fuentes.");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Efectos de los números por poder que falten (2026-11-02, primeros ejemplos, a juego y amarillos, elegidos por el
        /// usuario): Fuego, degradado amarillo-naranja con contorno claro y glow amarillo que late; Rayos, degradado
        /// blanco-amarillo con glow que parpadea rápido. Sin textura: el ruido gris multiplicaba el color y ensuciaba la cifra. Los demás poderes se añaden a mano en HUDTheme → Damage Numbers → Special Fx.
        /// </summary>
        private static void EnsureSpecialFx(HUDTheme theme, StringBuilder log)
        {
            DamageNumberStyle style = theme.damageNumbers;
            if (style.FindFx(SpecialAbilityType.Fire) == null)
            {
                style.specialFx.Add(new DamageNumberSpecialFx
                {
                    special = SpecialAbilityType.Fire,
                    gradientTop = new Color(1f, 0.95f, 0.45f),
                    gradientBottom = new Color(1f, 0.38f, 0.1f),
                    outline = new Color(1f, 0.96f, 0.82f),
                    glow = new Color(1f, 0.82f, 0.3f, 0.55f),
                    flicker = 0.4f,
                    flickerSpeed = 8f
                });
                log.Append("\n- efecto de Fuego añadido");
            }
            if (style.FindFx(SpecialAbilityType.Lightning) == null)
            {
                style.specialFx.Add(new DamageNumberSpecialFx
                {
                    special = SpecialAbilityType.Lightning,
                    gradientTop = new Color(1f, 1f, 0.88f),
                    gradientBottom = new Color(1f, 0.84f, 0.15f),
                    outline = Color.white,
                    glow = new Color(1f, 0.93f, 0.45f, 0.6f),
                    flicker = 0.7f,
                    flickerSpeed = 25f
                });
                log.Append("\n- efecto de Rayos añadido");
            }
        }

        private static TMP_FontAsset Create(Font font, string path, out string missing)
        {
            missing = null;
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, SamplingSize, Padding, GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            if (asset == null) return null;

            asset.TryAddCharacters(Characters, out missing);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset, path);

            Texture2D atlas = asset.atlasTextures[0];
            atlas.name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);

            // El shader completo: el de móvil no tiene glow
            Material material = asset.material;
            material.name = asset.name + " Material";
            Shader full = Shader.Find("TextMeshPro/Distance Field");
            if (full != null) material.shader = full;
            AssetDatabase.AddObjectToAsset(material, asset);

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
