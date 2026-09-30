using FakeBlade.UI;
using UnityEditor;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Crea los assets de configuración por defecto:
    /// - Assets/Settings/CombatConfig.asset
    /// - Assets/Settings/HUDTheme.asset
    /// - Assets/Settings/GameModes/*.asset (modos del GDD 6.1)
    /// y los asigna al GameManager / CombatHUDManager de la escena abierta.
    /// </summary>
    public static class FakeBladeAssetsMenu
    {
        private const string SettingsPath = "Assets/Settings";
        private const string ModesPath = "Assets/Settings/GameModes";

        [MenuItem("FakeBlade/Create Default Config Assets")]
        public static void CreateDefaultAssets()
        {
            EnsureFolder(SettingsPath);
            EnsureFolder(ModesPath);

            var combat = LoadOrCreate<CombatConfig>($"{SettingsPath}/CombatConfig.asset");
            var theme = LoadOrCreate<HUDTheme>($"{SettingsPath}/HUDTheme.asset");

            var lastStanding = CreateRules("LastStanding", "MODE_LAST_STANDING", WinCondition.LastStanding);
            CreateRules("Stocks_3", "MODE_STOCKS", WinCondition.Stocks, lives: 3);
            CreateRules("FreeForAll_Points", "MODE_POINTS", WinCondition.Points, timeLimit: 180f);
            CreateRules("Teams_2v2", "MODE_TEAMS", WinCondition.LastStanding, teams: true);

            AssetDatabase.SaveAssets();

            AssignToScene(combat, theme, lastStanding);
            Debug.Log("[FakeBlade] Assets de configuración creados en Assets/Settings.");
        }

        private static MatchRules CreateRules(string fileName, string nameKey, WinCondition condition,
            int lives = 3, float timeLimit = 0f, bool teams = false)
        {
            string path = $"{ModesPath}/{fileName}.asset";
            var rules = AssetDatabase.LoadAssetAtPath<MatchRules>(path);
            if (rules != null) return rules;

            rules = ScriptableObject.CreateInstance<MatchRules>();
            rules.displayNameKey = nameKey;
            rules.winCondition = condition;
            rules.lives = lives;
            rules.timeLimit = timeLimit;
            rules.teams = teams;
            AssetDatabase.CreateAsset(rules, path);
            return rules;
        }

        private static void AssignToScene(CombatConfig combat, HUDTheme theme, MatchRules rules)
        {
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                var so = new SerializedObject(gm);
                SetIfEmpty(so, "combatConfig", combat);
                SetIfEmpty(so, "rules", rules);
                so.ApplyModifiedProperties();
            }

            var hud = Object.FindFirstObjectByType<CombatHUDManager>();
            if (hud != null)
            {
                var so = new SerializedObject(hud);
                SetIfEmpty(so, "theme", theme);
                so.ApplyModifiedProperties();
            }
        }

        private static void SetIfEmpty(SerializedObject so, string property, Object value)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop != null && prop.objectReferenceValue == null)
                prop.objectReferenceValue = value;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
