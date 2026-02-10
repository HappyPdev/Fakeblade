using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Tipo de slot donde encaja el componente.
    /// </summary>
    public enum ComponentSlot
    {
        Tip,    // Punta - afecta estabilidad y velocidad
        Body,   // Cuerpo - afecta peso y resistencia
        Blade,  // Disco/Cuchilla - afecta ataque y defensa
        Core    // Núcleo - afecta spin máximo y habilidad especial
    }

    /// <summary>
    /// Clase de peso del componente. Define el "tier" general.
    /// </summary>
    public enum WeightClass
    {
        Light,   // Ligero: rápido, ágil, frágil
        Medium,  // Medio: equilibrado
        Heavy    // Pesado: lento, resistente, poderoso
    }

    /// <summary>
    /// ScriptableObject que define las propiedades de un componente de peonza.
    /// Cada componente tiene modificadores que se suman a las stats base.
    /// 
    /// DISEÑO: Valores POSITIVOS aumentan la stat, NEGATIVOS la reducen.
    /// Esto permite trade-offs: un cuerpo pesado aumenta peso y defensa
    /// pero puede reducir velocidad de movimiento.
    /// </summary>
    [CreateAssetMenu(fileName = "New FakeBlade Component", menuName = "FakeBlade/Component Data")]
    public class FakeBladeComponentData : ScriptableObject
    {
        [Header("=== IDENTITY ===")]
        [SerializeField] private string componentName = "Component";
        [SerializeField][TextArea(2, 4)] private string description = "";
        [SerializeField] private ComponentSlot componentType = ComponentSlot.Body;
        [SerializeField] private WeightClass weightClass = WeightClass.Medium;
        [SerializeField] private Sprite icon;

        [Header("=== STAT MODIFIERS ===")]
        [Tooltip("Modifica el spin máximo (+/-)")]
        [SerializeField] private float maxSpinModifier = 0f;

        [Tooltip("Modifica el decay del spin. POSITIVO = pierde spin más rápido. NEGATIVO = más estable.")]
        [SerializeField] private float spinDecayModifier = 0f;

        [Tooltip("Modifica la velocidad de movimiento (+/-)")]
        [SerializeField] private float moveSpeedModifier = 0f;

        [Tooltip("Modifica el peso (+/-). Más peso = más inercia, más daño de choque.")]
        [SerializeField] private float weightModifier = 0f;

        [Tooltip("Modifica la potencia de ataque (+/-)")]
        [SerializeField] private float attackPowerModifier = 0f;

        [Tooltip("Modifica la defensa (+/-). Reduce daño recibido por porcentaje.")]
        [SerializeField] private float defenseModifier = 0f;

        [Tooltip("Modifica la fuerza de dash (+/-)")]
        [SerializeField] private float dashForceModifier = 0f;

        [Header("=== SPECIAL ===")]
        [Tooltip("Solo para Core: tipo de habilidad especial que otorga")]
        [SerializeField] private SpecialAbilityType specialAbility = SpecialAbilityType.None;

        [Tooltip("Solo para Core: usos máximos de la habilidad especial por partida")]
        [SerializeField] private int specialAbilityUses = 3;

        #region Public Properties
        public string ComponentName => componentName;
        public string Description => description;
        public ComponentSlot ComponentType => componentType;
        public WeightClass WeightClass => weightClass;
        public Sprite Icon => icon;

        public float MaxSpinModifier => maxSpinModifier;
        public float SpinDecayModifier => spinDecayModifier;
        public float MoveSpeedModifier => moveSpeedModifier;
        public float WeightModifier => weightModifier;
        public float AttackPowerModifier => attackPowerModifier;
        public float DefenseModifier => defenseModifier;
        public float DashForceModifier => dashForceModifier;

        public SpecialAbilityType SpecialAbility => specialAbility;
        public int SpecialAbilityUses => specialAbilityUses;
        #endregion

        #region Utility
        /// <summary>
        /// Devuelve un resumen legible de los modificadores.
        /// Solo muestra los que no son cero.
        /// </summary>
        public string GetModifiersSummary()
        {
            var parts = new System.Collections.Generic.List<string>();

            if (maxSpinModifier != 0) parts.Add($"Spin:{FormatModifier(maxSpinModifier)}");
            if (spinDecayModifier != 0) parts.Add($"Decay:{FormatModifier(spinDecayModifier)}");
            if (moveSpeedModifier != 0) parts.Add($"Speed:{FormatModifier(moveSpeedModifier)}");
            if (weightModifier != 0) parts.Add($"Weight:{FormatModifier(weightModifier)}");
            if (attackPowerModifier != 0) parts.Add($"Atk:{FormatModifier(attackPowerModifier)}");
            if (defenseModifier != 0) parts.Add($"Def:{FormatModifier(defenseModifier)}");
            if (dashForceModifier != 0) parts.Add($"Dash:{FormatModifier(dashForceModifier)}");

            return parts.Count > 0 ? string.Join(" | ", parts) : "No modifiers";
        }

        private string FormatModifier(float value)
        {
            return value > 0 ? $"+{value:F0}" : $"{value:F0}";
        }
        #endregion
    }
}