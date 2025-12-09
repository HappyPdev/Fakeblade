using UnityEngine;
using System;

namespace FakeBlade.Core
{
    /// <summary>
    /// Contenedor de estadísticas calculadas de una FakeBlade.
    /// Las estadísticas se calculan a partir de los componentes equipados.
    /// </summary>
    [Serializable]
    public class FakeBladeStats : MonoBehaviour
    {
        #region Base Stats Structure
        [Serializable]
        public struct StatBlock
        {
            [Range(0f, 100f)] public float attack;
            [Range(0f, 100f)] public float defense;
            [Range(0f, 100f)] public float speed;
            [Range(0f, 100f)] public float stamina;
            [Range(0f, 100f)] public float weight;

            public static StatBlock operator +(StatBlock a, StatBlock b)
            {
                return new StatBlock
                {
                    attack = a.attack + b.attack,
                    defense = a.defense + b.defense,
                    speed = a.speed + b.speed,
                    stamina = a.stamina + b.stamina,
                    weight = a.weight + b.weight
                };
            }

            public static StatBlock operator *(StatBlock a, float multiplier)
            {
                return new StatBlock
                {
                    attack = a.attack * multiplier,
                    defense = a.defense * multiplier,
                    speed = a.speed * multiplier,
                    stamina = a.stamina * multiplier,
                    weight = a.weight * multiplier
                };
            }
        }
        #endregion

        #region Component References
        [Header("Equipped Components")]
        [SerializeField] private FakeBladeComponentData tipComponent;
        [SerializeField] private FakeBladeComponentData bodyComponent;
        [SerializeField] private FakeBladeComponentData bladeComponent;
        [SerializeField] private FakeBladeComponentData coreComponent;
        #endregion

        #region Cached Calculated Stats
        private float _moveSpeed;
        private float _maxSpin;
        private float _attackPower;
        private float _weight;
        private float _dashForce;
        private float _spinDecay;
        private float _defense;
        private bool _isDirty = true;
        #endregion

        #region Stat Conversion Constants
        private const float STAT_TO_SPEED_MULTIPLIER = 0.1f;
        private const float STAT_TO_SPIN_MULTIPLIER = 15f;
        private const float STAT_TO_ATTACK_MULTIPLIER = 0.2f;
        private const float STAT_TO_WEIGHT_MULTIPLIER = 0.05f;
        private const float STAT_TO_DASH_MULTIPLIER = 0.3f;
        private const float STAT_TO_DECAY_MULTIPLIER = 0.1f;

        private const float BASE_SPEED = 3f;
        private const float BASE_SPIN = 500f;
        private const float BASE_ATTACK = 5f;
        private const float BASE_WEIGHT = 0.5f;
        private const float BASE_DASH = 10f;
        private const float BASE_DECAY = 3f;
        #endregion

        #region Properties
        public float MoveSpeed
        {
            get
            {
                if (_isDirty) RecalculateStats();
                return _moveSpeed;
            }
        }

        public float MaxSpin
        {
            get
            {
                if (_isDirty) RecalculateStats();
                return _maxSpin;
            }
        }

        public float AttackPower
        {
            get
            {
                if (_isDirty) RecalculateStats();
                return _attackPower;
            }
        }

        public float Weight
        {
            get
            {
                if (_isDirty) RecalculateStats();
                return _weight;
            }
        }

        public float DashForce
        {
            get
            {
                if (_isDirty) RecalculateStats();
                return _dashForce;
            }
        }

        public float SpinDecay
        {
            get
            {
                if (_isDirty) RecalculateStats();
                return _spinDecay;
            }
        }

        public float Defense
        {
            get
            {
                if (_isDirty) RecalculateStats();
                return _defense;
            }
        }

        // Componentes equipados
        public FakeBladeComponentData Tip => tipComponent;
        public FakeBladeComponentData Body => bodyComponent;
        public FakeBladeComponentData Blade => bladeComponent;
        public FakeBladeComponentData Core => coreComponent;
        #endregion

        #region Events
        public event Action OnStatsChanged;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            RecalculateStats();
        }

        private void OnValidate()
        {
            _isDirty = true;
        }
        #endregion

        #region Component Management
        public void SetComponent(ComponentSlot slot, FakeBladeComponentData component)
        {
            switch (slot)
            {
                case ComponentSlot.Tip:
                    tipComponent = component;
                    break;
                case ComponentSlot.Body:
                    bodyComponent = component;
                    break;
                case ComponentSlot.Blade:
                    bladeComponent = component;
                    break;
                case ComponentSlot.Core:
                    coreComponent = component;
                    break;
            }

            _isDirty = true;
            RecalculateStats();
            OnStatsChanged?.Invoke();
        }

        public FakeBladeComponentData GetComponent(ComponentSlot slot)
        {
            return slot switch
            {
                ComponentSlot.Tip => tipComponent,
                ComponentSlot.Body => bodyComponent,
                ComponentSlot.Blade => bladeComponent,
                ComponentSlot.Core => coreComponent,
                _ => null
            };
        }

        public bool HasAllComponents()
        {
            return tipComponent != null &&
                   bodyComponent != null &&
                   bladeComponent != null &&
                   coreComponent != null;
        }
        #endregion

        #region Stat Calculation
        public void RecalculateStats()
        {
            StatBlock totalStats = CalculateTotalStats();

            // Convertir stats base a valores de gameplay
            _moveSpeed = BASE_SPEED + (totalStats.speed * STAT_TO_SPEED_MULTIPLIER);
            _maxSpin = BASE_SPIN + (totalStats.stamina * STAT_TO_SPIN_MULTIPLIER);
            _attackPower = BASE_ATTACK + (totalStats.attack * STAT_TO_ATTACK_MULTIPLIER);
            _weight = BASE_WEIGHT + (totalStats.weight * STAT_TO_WEIGHT_MULTIPLIER);
            _dashForce = BASE_DASH + (totalStats.speed * STAT_TO_DASH_MULTIPLIER);
            _defense = totalStats.defense;

            // El decay se reduce con más stamina
            _spinDecay = Mathf.Max(1f, BASE_DECAY - (totalStats.stamina * STAT_TO_DECAY_MULTIPLIER * 0.5f));

            _isDirty = false;
        }

        private StatBlock CalculateTotalStats()
        {
            StatBlock total = new StatBlock();

            if (tipComponent != null) total += tipComponent.Stats;
            if (bodyComponent != null) total += bodyComponent.Stats;
            if (bladeComponent != null) total += bladeComponent.Stats;
            if (coreComponent != null) total += coreComponent.Stats;

            // Clamp valores
            total.attack = Mathf.Clamp(total.attack, 0f, 100f);
            total.defense = Mathf.Clamp(total.defense, 0f, 100f);
            total.speed = Mathf.Clamp(total.speed, 0f, 100f);
            total.stamina = Mathf.Clamp(total.stamina, 0f, 100f);
            total.weight = Mathf.Clamp(total.weight, 0f, 100f);

            return total;
        }

        public StatBlock GetTotalStats()
        {
            return CalculateTotalStats();
        }
        #endregion

        #region Utility
        public void CopyFrom(FakeBladeStats other)
        {
            if (other == null) return;

            tipComponent = other.tipComponent;
            bodyComponent = other.bodyComponent;
            bladeComponent = other.bladeComponent;
            coreComponent = other.coreComponent;

            _isDirty = true;
            RecalculateStats();
        }

        public override string ToString()
        {
            return $"FakeBladeStats: Speed={MoveSpeed:F1}, Spin={MaxSpin:F0}, Attack={AttackPower:F1}, Weight={Weight:F2}";
        }
        #endregion
    }

    #region Enums
    public enum ComponentSlot
    {
        Tip,
        Body,
        Blade,
        Core
    }
    #endregion
}