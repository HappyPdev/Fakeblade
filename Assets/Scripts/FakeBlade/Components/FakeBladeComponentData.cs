using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// ScriptableObject base para datos de componentes de FakeBlade.
    /// Permite crear componentes desde el editor de Unity.
    /// </summary>
    [CreateAssetMenu(fileName = "New FakeBlade Component", menuName = "FakeBlade/Component Data")]
    public class FakeBladeComponentData : ScriptableObject
    {
        #region Component Info
        [Header("Component Info")]
        [SerializeField] private string componentName = "New Component";
        [SerializeField] private ComponentSlot slot;
        [SerializeField] private ComponentRarity rarity = ComponentRarity.Common;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject prefab;
        #endregion

        #region Stats
        [Header("Base Stats")]
        [SerializeField] private FakeBladeStats.StatBlock stats;
        #endregion

        #region Special Ability
        [Header("Special Ability (Core Only)")]
        [SerializeField] private SpecialAbilityType specialAbility = SpecialAbilityType.None;
        [SerializeField] private int maxAbilityUses = 3;
        [SerializeField] private float abilityCooldown = 5f;
        [SerializeField] private float abilityPower = 1f;
        #endregion

        #region Properties
        public string ComponentName => componentName;
        public ComponentSlot Slot => slot;
        public ComponentRarity Rarity => rarity;
        public string Description => description;
        public Sprite Icon => icon;
        public GameObject Prefab => prefab;
        public FakeBladeStats.StatBlock Stats => stats;
        public SpecialAbilityType SpecialAbility => specialAbility;
        public int MaxAbilityUses => maxAbilityUses;
        public float AbilityCooldown => abilityCooldown;
        public float AbilityPower => abilityPower;
        #endregion

        #region Validation
        private void OnValidate()
        {
            // Auto-ajustar stats según rareza
            if (Application.isPlaying) return;

            float rarityMultiplier = GetRarityMultiplier();
            // Los stats se muestran escalados visualmente pero se almacenan base
        }

        private float GetRarityMultiplier()
        {
            return rarity switch
            {
                ComponentRarity.Common => 1f,
                ComponentRarity.Uncommon => 1.15f,
                ComponentRarity.Rare => 1.3f,
                ComponentRarity.Epic => 1.5f,
                ComponentRarity.Legendary => 1.75f,
                _ => 1f
            };
        }
        #endregion

        #region Utility
        public Color GetRarityColor()
        {
            return rarity switch
            {
                ComponentRarity.Common => Color.gray,
                ComponentRarity.Uncommon => Color.green,
                ComponentRarity.Rare => Color.blue,
                ComponentRarity.Epic => new Color(0.5f, 0f, 0.5f), // Purple
                ComponentRarity.Legendary => new Color(1f, 0.65f, 0f), // Orange
                _ => Color.white
            };
        }

        public FakeBladeStats.StatBlock GetScaledStats()
        {
            return stats * GetRarityMultiplier();
        }
        #endregion
    }

    #region Enums
    public enum ComponentRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum SpecialAbilityType
    {
        None,
        SpinBoost,      // Recupera velocidad de giro
        ShockWave,      // Empuja enemigos cercanos
        Shield,         // Reduce daño temporalmente
        Dash,           // Dash extra potente
        Drain,          // Roba spin al enemigo
        Berserk,        // Aumenta ataque pero reduce defensa
        Anchor,         // Aumenta peso temporalmente
        Phantom         // Breve invulnerabilidad
    }
    #endregion
}