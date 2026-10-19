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

            // Energía necesaria y duración según GDD 5 (todos duran 5 s salvo Rayos, hasta 6 s)
            Create<SpinBoostData>("SpinBoost", "SPIN BOOST", "SPIN BOOST",
                new Color(0.3f, 1f, 0.6f), energy: 1f, burst: 1f, log);
            Create<ShockWaveData>("ShockWave", "ONDA DE CHOQUE", "SHOCKWAVE",
                new Color(1f, 0.55f, 0.15f), energy: 1f, burst: 2.5f, log, windup: 0.5f);
            Create<DefenseData>("Defense", "DEFENSA", "DEFENSE",
                new Color(0.45f, 0.55f, 1f), energy: 1.2f, burst: 1f, log, windup: 0.1f);
            Create<LightningData>("Lightning", "RAYOS", "LIGHTNING",
                new Color(1f, 0.95f, 0.25f), energy: 0.8f, burst: 1f, log, duration: 6f);
            Create<FireData>("Fire", "FUEGO", "FIRE",
                new Color(1f, 0.3f, 0.08f), energy: 1f, burst: 1.5f, log);
            Create<IceData>("Ice", "HIELO", "ICE",
                new Color(0.6f, 0.95f, 1f), energy: 1f, burst: 1.5f, log);
            // Fase L (poderes nuevos, 2026-10-13)
            Create<SpikesData>("Spikes", "PINCHOS", "SPIKES",
                new Color(0.8f, 0.4f, 1f), energy: 1f, burst: 1.5f, log);
            Create<BounceData>("Bounce", "REBOTE", "BOUNCE",
                new Color(1f, 0.45f, 0.75f), energy: 1f, burst: 1.5f, log);
            // Escudo: fase activa corta; los escudos se quedan hasta romperse (duración indefinida)
            Create<ShieldsData>("Shields", "ESCUDO", "SHIELD",
                new Color(1f, 0.78f, 0.2f), energy: 0.6f, burst: 1.5f, log, duration: 0.5f, windup: 0.1f);
            // Tornado: atrae 1,5 s (pullDuration) y empuja; la duración del asset, algo mayor para que el empujón llegue antes
            Create<TornadoData>("Tornado", "TORNADO", "TORNADO",
                new Color(0.55f, 0.9f, 0.85f), energy: 1f, burst: 1.5f, log, duration: 1.8f);
            // Salto: carga 0,3 s, salta y termina al caer; la duración solo es un tope
            Create<LeapData>("Leap", "SALTO", "LEAP",
                new Color(1f, 0.35f, 0.45f), energy: 1f, burst: 1f, log, duration: 2f);
            // Rastro de agua: 4 s dejando agua (que dura 3 s más en el suelo)
            Create<WaterTrailData>("WaterTrail", "RASTRO DE AGUA", "WATER TRAIL",
                new Color(0.3f, 0.6f, 1f), energy: 1f, burst: 1.5f, log, duration: 4f);
            // Fantasma (C5): la activación es corta; el clon se queda hasta que lo rompan
            Create<GhostData>("Ghost", "FANTASMA", "GHOST",
                new Color(0.8f, 0.85f, 1f), energy: 1.3f, burst: 1.5f, log, duration: 0.5f);
            FillGhost(log);
            // Embestida (2026-10-14): 6 s para usar las 3 embestidas
            Create<RamData>("Ram", "EMBESTIDA", "RAM",
                new Color(0.7f, 1f, 0.25f), energy: 1f, burst: 1.5f, log, duration: 6f);

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        /// <summary>El clon de Fantasma usa el prefab de los jugadores y las piezas del preset Agilidad del catálogo.</summary>
        private static void FillGhost(StringBuilder log)
        {
            var ghost = AssetDatabase.LoadAssetAtPath<GhostData>($"{Folder}/Ghost.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<FakeBladeCatalog>("Assets/Settings/FakeBladeCatalog.asset");
            if (ghost == null || catalog == null || (ghost.clonePrefab != null && ghost.cloneParts.Length > 0)) return;

            BladePreset agility = null;
            foreach (BladePreset preset in catalog.presets)
                if (preset.core != null && preset.core.Archetype == BladeArchetype.Agility) agility = preset;
            if (ghost.clonePrefab == null) ghost.clonePrefab = catalog.playerPrefab;
            if (ghost.cloneParts.Length == 0 && agility != null)
                ghost.cloneParts = new[] { agility.tip, agility.body, agility.blade, agility.core };
            EditorUtility.SetDirty(ghost);
            log.Append("(clon de Fantasma: prefab y piezas de Agilidad) ");
        }

        private static void Create<T>(string fileName, string nameEs, string nameEn, Color color,
            float energy, float burst, StringBuilder log, float duration = 5f, float windup = 0.3f) where T : SpecialAbilityData
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
            data.duration = duration;
            data.activationBurst = burst;
            // Carga al activarlo (L9): 0,3 s; muy corta (0,1) en Defensa y Escudo; Onda de choque, 0,5 s antes de cada onda
            data.windupTime = windup;
            AssetDatabase.CreateAsset(data, path);
            log.Append(fileName).Append(" (creado) ");
        }
    }
}
