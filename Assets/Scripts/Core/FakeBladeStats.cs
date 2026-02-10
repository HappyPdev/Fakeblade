using UnityEngine;
using System;
using System.Collections.Generic;

namespace FakeBlade.Core
{
    /// <summary>
    /// Sistema de estadísticas de la peonza.
    /// Calcula stats finales sumando base + componentes equipados.
    /// Cada componente modifica las stats según su tipo y tier (Light/Medium/Heavy).
    /// </summary>
    public class FakeBladeStats : MonoBehaviour
    {
        #region Events
        public event Action OnStatsChanged;
        #endregion

        #region Base Stats
        [Header("=== BASE STATS (sin componentes) ===")]
        [SerializeField] private float baseMaxSpin = 800f;
        [SerializeField] private float baseSpinDecay = 2f;
        [SerializeField] private float baseMoveSpeed = 8f;
        [SerializeField] private float baseWeight = 1f;
        [SerializeField] private float baseAttackPower = 10f;
        [SerializeField] private float baseDefense = 10f;
        [SerializeField] private float baseDashForce = 18f;
        #endregion

        #region Equipped Components
        [Header("=== EQUIPPED COMPONENTS ===")]
        [Tooltip("Punta: Afecta estabilidad (spinDecay) y velocidad de movimiento")]
        [SerializeField] private FakeBladeComponentData equippedTip;

        [Tooltip("Cuerpo: Afecta peso y resistencia al spin")]
        [SerializeField] private FakeBladeComponentData equippedBody;

        [Tooltip("Cuchilla/Disco: Afecta ataque y defensa")]
        [SerializeField] private FakeBladeComponentData equippedBlade;

        [Tooltip("Núcleo: Afecta spin máximo y habilidad especial")]
        [SerializeField] private FakeBladeComponentData equippedCore;
        #endregion

        #region Computed Stats (cached)
        private float _maxSpin;
        private float _spinDecay;
        private float _moveSpeed;
        private float _weight;
        private float _attackPower;
        private float _defense;
        private float _dashForce;
        #endregion

        #region Public Properties
        public float MaxSpin => _maxSpin;
        public float SpinDecay => _spinDecay;
        public float MoveSpeed => _moveSpeed;
        public float Weight => _weight;
        public float AttackPower => _attackPower;
        public float Defense => _defense;
        public float DashForce => _dashForce;

        // Component access
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
            // Recalcular en el editor cuando cambien valores
            RecalculateStats();
        }
        #endregion

        #region Stat Calculation
        /// <summary>
        /// Recalcula todas las stats sumando base + modificadores de cada componente equipado.
        /// </summary>
        public void RecalculateStats()
        {
            _maxSpin = baseMaxSpin;
            _spinDecay = baseSpinDecay;
            _moveSpeed = baseMoveSpeed;
            _weight = baseWeight;
            _attackPower = baseAttackPower;
            _defense = baseDefense;
            _dashForce = baseDashForce;

            // Sumar modificadores de cada componente
            ApplyComponentModifiers(equippedTip);
            ApplyComponentModifiers(equippedBody);
            ApplyComponentModifiers(equippedBlade);
            ApplyComponentModifiers(equippedCore);

            // Clamp valores mínimos de seguridad
            _maxSpin = Mathf.Max(100f, _maxSpin);
            _spinDecay = Mathf.Max(0.5f, _spinDecay);
            _moveSpeed = Mathf.Max(2f, _moveSpeed);
            _weight = Mathf.Max(0.3f, _weight);
            _attackPower = Mathf.Max(1f, _attackPower);
            _defense = Mathf.Clamp(_defense, 0f, 80f); // Máximo 80% reducción
            _dashForce = Mathf.Max(5f, _dashForce);

            OnStatsChanged?.Invoke();
        }

        private void ApplyComponentModifiers(FakeBladeComponentData component)
        {
            if (component == null) return;

            _maxSpin += component.MaxSpinModifier;
            _spinDecay += component.SpinDecayModifier;
            _moveSpeed += component.MoveSpeedModifier;
            _weight += component.WeightModifier;
            _attackPower += component.AttackPowerModifier;
            _defense += component.DefenseModifier;
            _dashForce += component.DashForceModifier;
        }
        #endregion

        #region Component Management
        /// <summary>
        /// Equipa un componente en su slot correspondiente.
        /// Reemplaza el componente anterior si había uno.
        /// </summary>
        public void EquipComponent(FakeBladeComponentData component)
        {
            if (component == null) return;

            switch (component.ComponentType)
            {
                case ComponentSlot.Tip:
                    equippedTip = component;
                    break;
                case ComponentSlot.Body:
                    equippedBody = component;
                    break;
                case ComponentSlot.Blade:
                    equippedBlade = component;
                    break;
                case ComponentSlot.Core:
                    equippedCore = component;
                    break;
            }

            RecalculateStats();
            Debug.Log($"[FakeBladeStats] Equipped {component.ComponentName} ({component.ComponentType}) - {component.WeightClass}");
        }

        /// <summary>
        /// Desequipa el componente de un slot específico.
        /// </summary>
        public void UnequipComponent(ComponentSlot slot)
        {
            switch (slot)
            {
                case ComponentSlot.Tip:
                    equippedTip = null;
                    break;
                case ComponentSlot.Body:
                    equippedBody = null;
                    break;
                case ComponentSlot.Blade:
                    equippedBlade = null;
                    break;
                case ComponentSlot.Core:
                    equippedCore = null;
                    break;
            }

            RecalculateStats();
        }

        /// <summary>
        /// Devuelve un resumen de stats para debug/UI.
        /// </summary>
        public string GetStatsSummary()
        {
            return $"MaxSpin:{_maxSpin:F0} Decay:{_spinDecay:F1} Speed:{_moveSpeed:F1} " +
                   $"Weight:{_weight:F1} Atk:{_attackPower:F1} Def:{_defense:F1} Dash:{_dashForce:F1}";
        }

        /// <summary>
        /// Lista todos los componentes equipados.
        /// </summary>
        public List<FakeBladeComponentData> GetEquippedComponents()
        {
            var list = new List<FakeBladeComponentData>();
            if (equippedTip != null) list.Add(equippedTip);
            if (equippedBody != null) list.Add(equippedBody);
            if (equippedBlade != null) list.Add(equippedBlade);
            if (equippedCore != null) list.Add(equippedCore);
            return list;
        }
        #endregion
    }
}