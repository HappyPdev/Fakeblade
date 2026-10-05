using System.Text;
using UnityEditor;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Crea un asset por poder especial en Resources/SpecialAbilities (GDD 5).
    /// Solo crea los que faltan: los existentes no se tocan, para no pisar ajustes hechos a mano.
    /// </summary>
    public static class FakeBladeSpecialsSetup
    {
        private const string Folder = "Assets/Resources/" + SpecialAbilities.ResourcesFolder;

        [MenuItem("FakeBlade/Setup Specials")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            var log = new StringBuilder("Poderes: ");
            FakeBladeAssetsMenu.EnsureFolder(Folder);

            // Energía necesaria según GDD 5: Storm Breaker y Dash eléctrico ya llevan la de
            // Defensa (1,2) y Rayos (0,8), los poderes que los sustituyen
            Create<SpinBoostData>("SpinBoost", "SPIN BOOST", "SPIN BOOST",
                new Color(0.3f, 1f, 0.6f), energy: 1f, burst: 1f, log);
            Create<ShockWaveData>("ShockWave", "ONDA DE CHOQUE", "SHOCKWAVE",
                new Color(1f, 0.55f, 0.15f), energy: 1f, burst: 2.5f, log);
            Create<StormBreakerData>("StormBreaker", "STORM BREAKER", "STORM BREAKER",
                new Color(0.45f, 0.55f, 1f), energy: 1.2f, burst: 1f, log);
            Create<ElectricDashData>("ElectricDash", "DASH ELÉCTRICO", "ELECTRIC DASH",
                new Color(1f, 0.95f, 0.25f), energy: 0.8f, burst: 1f, log);

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        private static void Create<T>(string fileName, string nameEs, string nameEn, Color color,
            float energy, float burst, StringBuilder log) where T : SpecialAbilityData
        {
            string path = $"{Folder}/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
            {
                log.Append(fileName).Append(" (ya existe) ");
                return;
            }

            var data = ScriptableObject.CreateInstance<T>();
            data.nameEs = nameEs;
            data.nameEn = nameEn;
            data.color = color;
            data.energyRequired = energy;
            data.duration = 5f;
            data.activationBurst = burst;
            AssetDatabase.CreateAsset(data, path);
            log.Append(fileName).Append(" (creado) ");
        }
    }
}
