using System;
using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Arquetipo resultante de la combinación de piezas (GDD 3).
    /// No es una peonza fija: se deduce de las stats finales.
    /// </summary>
    public enum BladeArchetype
    {
        Balanced,
        Attack,
        Defense,
        Agility
    }

    /// <summary>Stats base de una peonza sin piezas.</summary>
    [Serializable]
    public struct BladeBaseStats
    {
        public float maxSpin;
        public float spinDecay;
        public float moveSpeed;
        public float weight;
        public float attackPower;
        public float defense;
        public float dashForce;
        public int attackCharges;

        public static BladeBaseStats Default => new BladeBaseStats
        {
            maxSpin = 400f, spinDecay = 1f, moveSpeed = 4f, weight = 1f,
            attackPower = 10f, defense = 10f, dashForce = 18f, attackCharges = 3
        };
    }

    /// <summary>Stats finales (base + piezas) ya limitadas a rangos seguros.</summary>
    public struct BladeStatBlock
    {
        public float MaxSpin;
        public float SpinDecay;
        public float MoveSpeed;
        public float Weight;
        public float AttackPower;
        public float Defense;
        public float DashForce;
        public int AttackCharges;
        /// <summary>Segundos que las piezas suman (o restan) a la ventana de parry base.</summary>
        public float ParryWindowBonus;
        public SpecialAbilityType Special;
        public BladeArchetype Archetype;
        /// <summary>Rasgos sumados de las piezas (multiplicadores sobre valores del combate).</summary>
        public PartTraits Traits;
    }

    /// <summary>
    /// Estadísticas de la peonza: base + suma de los modificadores de las piezas equipadas.
    /// El cálculo es estático (Calculate) para poder usarlo sin instanciar la peonza (selección).
    /// </summary>
    public class FakeBladeStats : MonoBehaviour
    {
        #region Events
        public event Action OnStatsChanged;
        #endregion

        #region Base Stats
        [Header("=== BASE STATS (sin piezas) ===")]
        [SerializeField] private float baseMaxSpin = 800f;
        [SerializeField] private float baseSpinDecay = 2f;
        [SerializeField] private float baseMoveSpeed = 8f;
        [SerializeField] private float baseWeight = 1f;
        [SerializeField] private float baseAttackPower = 10f;
        [SerializeField] private float baseDefense = 10f;
        [SerializeField] private float baseDashForce = 18f;
        [Tooltip("Cargas de ataque sin piezas. La media del juego es 3.")]
        [SerializeField] private int baseAttackCharges = 3;
        #endregion

        #region Equipped Components
        [Header("=== PIEZAS EQUIPADAS ===")]
        [Tooltip("Punta: estabilidad (desgaste) y velocidad de movimiento")]
        [SerializeField] private FakeBladeComponentData equippedTip;

        [Tooltip("Cuerpo: peso e inercia")]
        [SerializeField] private FakeBladeComponentData equippedBody;

        [Tooltip("Disco: ataque y defensa")]
        [SerializeField] private FakeBladeComponentData equippedBlade;

        [Tooltip("Núcleo: RPM máximas y poder especial")]
        [SerializeField] private FakeBladeComponentData equippedCore;
        #endregion

        private BladeStatBlock _stats;

        #region Public Properties
        public float MaxSpin => _stats.MaxSpin;
        public float SpinDecay => _stats.SpinDecay;
        public float MoveSpeed => _stats.MoveSpeed;
        public float Weight => _stats.Weight;
        public float AttackPower => _stats.AttackPower;
        /// <summary>Defensa 0-80: % de reducción de daño (y la mitad en empuje).</summary>
        public float Defense => _stats.Defense;
        public float DashForce => _stats.DashForce;
        public int AttackCharges => _stats.AttackCharges;
        public float ParryWindowBonus => _stats.ParryWindowBonus;
        public SpecialAbilityType SpecialAbility => _stats.Special;
        public BladeArchetype Archetype => _stats.Archetype;
        /// <summary>Multiplicador de un rasgo de las piezas (1 si ninguna lo tiene).</summary>
        public float Trait(PartTraitType type) => _stats.Traits != null ? _stats.Traits.Multiplier(type) : 1f;
        public PartTraits Traits => _stats.Traits ?? PartTraits.None;
        public BladeStatBlock Block => _stats;

        public BladeBaseStats BaseStats => new BladeBaseStats
        {
            maxSpin = baseMaxSpin,
            spinDecay = baseSpinDecay,
            moveSpeed = baseMoveSpeed,
            weight = baseWeight,
            attackPower = baseAttackPower,
            defense = baseDefense,
            dashForce = baseDashForce,
            attackCharges = baseAttackCharges
        };

        public FakeBladeComponentData EquippedTip => equippedTip;
        public FakeBladeComponentData EquippedBody => equippedBody;
        public FakeBladeComponentData EquippedBlade => equippedBlade;
        public FakeBladeComponentData EquippedCore => equippedCore;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            RecalculateStats();
        }

        private void OnValidate()
        {
            RecalculateStats();
        }
        #endregion

        #region Stat Calculation
        public void RecalculateStats()
        {
            _stats = Calculate(BaseStats, equippedTip, equippedBody, equippedBlade, equippedCore);
            OnStatsChanged?.Invoke();
        }

        /// <summary>Stats finales: base + modificadores de las piezas, con mínimos de seguridad.</summary>
        public static BladeStatBlock Calculate(BladeBaseStats b, FakeBladeComponentData tip,
            FakeBladeComponentData body, FakeBladeComponentData blade, FakeBladeComponentData core)
        {
            var s = new BladeStatBlock
            {
                MaxSpin = b.maxSpin,
                SpinDecay = b.spinDecay,
                MoveSpeed = b.moveSpeed,
                Weight = b.weight,
                AttackPower = b.attackPower,
                Defense = b.defense,
                DashForce = b.dashForce,
                AttackCharges = b.attackCharges
            };

            Apply(ref s, tip);
            Apply(ref s, body);
            Apply(ref s, blade);
            Apply(ref s, core);

            s.Traits = new PartTraits();
            AddTraits(s.Traits, tip);
            AddTraits(s.Traits, body);
            AddTraits(s.Traits, blade);
            AddTraits(s.Traits, core);

            s.MaxSpin = Mathf.Max(100f, s.MaxSpin);
            s.SpinDecay = Mathf.Max(0.5f, s.SpinDecay);
            s.MoveSpeed = Mathf.Max(2f, s.MoveSpeed);
            s.Weight = Mathf.Max(0.3f, s.Weight);
            s.AttackPower = Mathf.Max(1f, s.AttackPower);
            s.Defense = Mathf.Clamp(s.Defense, 0f, 80f);
            s.DashForce = Mathf.Max(5f, s.DashForce);
            s.AttackCharges = Mathf.Clamp(s.AttackCharges, 1, AttackSystem.MaxSupportedCharges);

            s.Special = core != null && core.SpecialAbility != SpecialAbilityType.None
                ? core.SpecialAbility
                : SpecialAbilityType.SpinBoost;
            s.Archetype = ComputeArchetype(s);
            return s;
        }

        private static void AddTraits(PartTraits traits, FakeBladeComponentData c)
        {
            if (c == null) return;
            for (int i = 0; i < c.Traits.Count; i++) traits.Add(c.Traits[i]);
        }

        private static void Apply(ref BladeStatBlock s, FakeBladeComponentData c)
        {
            if (c == null) return;
            s.MaxSpin += c.MaxSpinModifier;
            s.SpinDecay += c.SpinDecayModifier;
            s.MoveSpeed += c.MoveSpeedModifier;
            s.Weight += c.WeightModifier;
            s.AttackPower += c.AttackPowerModifier;
            s.Defense += c.DefenseModifier;
            s.DashForce += c.DashForceModifier;
            s.AttackCharges += c.AttackChargesModifier;
            s.ParryWindowBonus += c.ParryWindowModifier;
        }

        /// <summary>
        /// Deduce el arquetipo comparando ataque, defensa (peso + defensa) y agilidad
        /// normalizados respecto a una build "todo Medio". Si ninguno destaca claramente, es Balanceada.
        /// </summary>
        private static BladeArchetype ComputeArchetype(in BladeStatBlock s)
        {
            const float margin = 1.15f;
            const float refAttack = 15f, refCharges = 3f, refDefense = 20f, refWeight = 1.5f, refSpeed = 5f;

            float attack = (s.AttackPower / refAttack + s.AttackCharges / refCharges) * 0.5f;
            float defense = (s.Defense / refDefense + s.Weight / refWeight) * 0.5f;
            float agility = s.MoveSpeed / refSpeed;

            if (attack > defense * margin && attack > agility * margin) return BladeArchetype.Attack;
            if (defense > attack * margin && defense > agility * margin) return BladeArchetype.Defense;
            if (agility > attack * margin && agility > defense * margin) return BladeArchetype.Agility;
            return BladeArchetype.Balanced;
        }
        #endregion

        #region Component Management
        /// <summary>Equipa una pieza en su slot. Reemplaza la anterior si la había.</summary>
        public void EquipComponent(FakeBladeComponentData component)
        {
            if (component == null) return;

            switch (component.ComponentType)
            {
                case ComponentSlot.Tip: equippedTip = component; break;
                case ComponentSlot.Body: equippedBody = component; break;
                case ComponentSlot.Blade: equippedBlade = component; break;
                case ComponentSlot.Core: equippedCore = component; break;
            }

            RecalculateStats();
        }

        public void UnequipComponent(ComponentSlot slot)
        {
            switch (slot)
            {
                case ComponentSlot.Tip: equippedTip = null; break;
                case ComponentSlot.Body: equippedBody = null; break;
                case ComponentSlot.Blade: equippedBlade = null; break;
                case ComponentSlot.Core: equippedCore = null; break;
            }

            RecalculateStats();
        }

        public string GetStatsSummary()
        {
            return $"MaxSpin:{_stats.MaxSpin:F0} Decay:{_stats.SpinDecay:F1} Speed:{_stats.MoveSpeed:F1} " +
                   $"Weight:{_stats.Weight:F1} Atk:{_stats.AttackPower:F1} Def:{_stats.Defense:F1} Dash:{_stats.DashForce:F1} " +
                   $"Charges:{_stats.AttackCharges} Special:{_stats.Special} ({_stats.Archetype})";
        }

        public List<FakeBladeComponentData> GetEquippedComponents()
        {
            var list = new List<FakeBladeComponentData>(4);
            if (equippedTip != null) list.Add(equippedTip);
            if (equippedBody != null) list.Add(equippedBody);
            if (equippedBlade != null) list.Add(equippedBlade);
            if (equippedCore != null) list.Add(equippedCore);
            return list;
        }
        #endregion
    }
}
