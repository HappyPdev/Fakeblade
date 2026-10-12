using System.Text;
using UnityEditor;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Crea los assets de la IA (GDD 6.4, D2): un nivel por dificultad en Resources/AI/Levels y un perfil por
    /// arquetipo en Resources/AI/Profiles, con los valores por defecto del código. Solo crea los que faltan:
    /// los existentes no se tocan, para no pisar ajustes hechos a mano.
    /// </summary>
    public static class FakeBladeAISetup
    {
        private const string LevelsFolder = "Assets/Resources/" + AILevel.ResourcesFolder;
        private const string ProfilesFolder = "Assets/Resources/" + AIProfile.ResourcesFolder;

        [MenuItem("FakeBlade/Setup AI")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            var log = new StringBuilder("IA: ");
            FakeBladeAssetsMenu.EnsureFolder(LevelsFolder);
            FakeBladeAssetsMenu.EnsureFolder(ProfilesFolder);

            foreach (AIDifficulty difficulty in System.Enum.GetValues(typeof(AIDifficulty)))
            {
                string path = $"{LevelsFolder}/AILevel_{difficulty}.asset";
                if (AssetDatabase.LoadAssetAtPath<AILevel>(path) != null)
                {
                    log.Append(difficulty).Append(" (ya existe) ");
                    continue;
                }
                var level = ScriptableObject.CreateInstance<AILevel>();
                level.ApplyDefaults(difficulty);
                AssetDatabase.CreateAsset(level, path);
                log.Append(difficulty).Append(" (creado) ");
            }

            foreach (AIStyle style in System.Enum.GetValues(typeof(AIStyle)))
            {
                string path = $"{ProfilesFolder}/AIProfile_{style}.asset";
                if (AssetDatabase.LoadAssetAtPath<AIProfile>(path) != null)
                {
                    log.Append("perfil ").Append(style).Append(" (ya existe) ");
                    continue;
                }
                var profile = ScriptableObject.CreateInstance<AIProfile>();
                profile.ApplyDefaults(style);
                AssetDatabase.CreateAsset(profile, path);
                log.Append("perfil ").Append(style).Append(" (creado) ");
            }

            AssetDatabase.SaveAssets();
            AILevel.ClearCache();
            AIProfiles.ClearCache();
            return log.ToString();
        }
    }
}
