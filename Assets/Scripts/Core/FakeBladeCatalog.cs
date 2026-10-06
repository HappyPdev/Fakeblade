using System;
using System.Collections.Generic;
using FakeBlade.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace FakeBlade.Core
{
    /// <summary>Peonza ya montada que se elige rápido en la selección (GDD 9.2.1).</summary>
    [Serializable]
    public class BladePreset
    {
        [Tooltip("Clave de localización del nombre (p. ej. PRESET_ATTACK)")]
        public string nameKey = "PRESET_BALANCED";
        public FakeBladeComponentData tip;
        public FakeBladeComponentData body;
        public FakeBladeComponentData blade;
        public FakeBladeComponentData core;
    }

    /// <summary>
    /// Catálogo del juego: todo el contenido seleccionable. Para añadir piezas, presets,
    /// colores o arenas basta con añadirlos aquí (GDD 7.2).
    /// </summary>
    [CreateAssetMenu(fileName = "FakeBladeCatalog", menuName = "FakeBlade/Catalog")]
    public class FakeBladeCatalog : ScriptableObject
    {
        [Header("=== PREFABS Y AJUSTES ===")]
        [Tooltip("Prefab de jugador (FakeBladeController + PlayerController + InputHandler...)")]
        public GameObject playerPrefab;
        public CombatConfig combatConfig;
        public HUDTheme uiTheme;
        public CreditsData credits;

        [Header("=== PIEZAS ===")]
        public List<FakeBladeComponentData> tips = new List<FakeBladeComponentData>();
        public List<FakeBladeComponentData> bodies = new List<FakeBladeComponentData>();
        public List<FakeBladeComponentData> blades = new List<FakeBladeComponentData>();
        public List<FakeBladeComponentData> cores = new List<FakeBladeComponentData>();

        [Header("=== PRESETS (arquetipos) ===")]
        public List<BladePreset> presets = new List<BladePreset>();

        [Header("=== COLORES (no se repiten entre jugadores) ===")]
        public Color[] palette =
        {
            new Color(0.2f, 0.5f, 1f),
            new Color(1f, 0.3f, 0.3f),
            new Color(0.3f, 1f, 0.35f),
            new Color(1f, 0.9f, 0.2f),
            new Color(0.7f, 0.35f, 1f),
            new Color(1f, 0.55f, 0.15f),
            new Color(0.2f, 0.95f, 0.95f),
            new Color(1f, 0.45f, 0.75f)
        };
        public Color dummyColor = new Color(0.55f, 0.55f, 0.6f);

        [Tooltip("Colores retro entre los que se elige el de cada pieza en Opciones → Colores de peonza. " +
                 "Incluye los de la paleta y el blanco y el negro por defecto (BladeColors)")]
        public Color[] paintColors =
        {
            // Paleta de jugadores
            new Color(0.2f, 0.5f, 1f),
            new Color(1f, 0.3f, 0.3f),
            new Color(0.3f, 1f, 0.35f),
            new Color(1f, 0.9f, 0.2f),
            new Color(0.7f, 0.35f, 1f),
            new Color(1f, 0.55f, 0.15f),
            new Color(0.2f, 0.95f, 0.95f),
            new Color(1f, 0.45f, 0.75f),
            // Tonos profundos
            new Color(0.11f, 0.17f, 0.33f),
            new Color(0.55f, 0.1f, 0.15f),
            new Color(0f, 0.45f, 0.28f),
            new Color(0.85f, 0.65f, 0.15f),
            new Color(0.35f, 0.15f, 0.4f),
            new Color(0.55f, 0.3f, 0.18f),
            new Color(0.07f, 0.33f, 0.35f),
            new Color(0.75f, 0.07f, 0.31f),
            new Color(0.66f, 0.9f, 0.18f),
            new Color(1f, 0.8f, 0.67f),
            // Neutros
            new Color(0.95f, 0.94f, 0.9f),
            new Color(0.76f, 0.76f, 0.78f),
            new Color(0.45f, 0.45f, 0.48f),
            new Color(0.25f, 0.24f, 0.27f),
            new Color(0.1f, 0.09f, 0.11f),
            new Color(0.62f, 0.5f, 0.38f)
        };
        public Color[] teamColors = { new Color(0.25f, 0.55f, 1f), new Color(1f, 0.35f, 0.3f) };

        [Header("=== ARENAS ===")]
        public List<ArenaData> arenas = new List<ArenaData>();

        [Header("=== REGLAS POR MODO ===")]
        public MatchRules lastStanding;
        public MatchRules stocks;
        public MatchRules points;
        public MatchRules teams;
        [Tooltip("Reglas del sandbox (GDD 6.3)")]
        [FormerlySerializedAs("practice")] public MatchRules sandbox;

        public List<FakeBladeComponentData> GetParts(ComponentSlot slot)
        {
            switch (slot)
            {
                case ComponentSlot.Tip: return tips;
                case ComponentSlot.Body: return bodies;
                case ComponentSlot.Blade: return blades;
                default: return cores;
            }
        }

        public BladePreset GetPreset(int index) =>
            presets.Count == 0 ? null : presets[Mathf.Abs(index) % presets.Count];

        public Color GetColor(int index) =>
            palette == null || palette.Length == 0 ? Color.white : palette[Mathf.Abs(index) % palette.Length];

        public Color GetTeamColor(int team) =>
            teamColors == null || teamColors.Length == 0 ? Color.white : teamColors[Mathf.Abs(team) % teamColors.Length];

        /// <summary>Stats base del prefab de jugador (sin piezas).</summary>
        public BladeBaseStats GetBaseStats()
        {
            if (playerPrefab != null && playerPrefab.TryGetComponent(out FakeBladeStats stats))
                return stats.BaseStats;
            return BladeBaseStats.Default;
        }
    }
}
