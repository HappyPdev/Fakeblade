using System.Text;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Tipo de slot donde encaja la pieza (GDD 3).
    /// </summary>
    public enum ComponentSlot
    {
        Tip,    // Punta - estabilidad y velocidad
        Body,   // Cuerpo - peso e inercia
        Blade,  // Disco - ataque y defensa
        Core    // Núcleo - RPM máximas y poder especial
    }

    /// <summary>
    /// Clase de peso de la pieza.
    /// </summary>
    public enum WeightClass
    {
        Light,   // Ligera: rápida, ágil, frágil
        Medium,  // Media: equilibrada
        Heavy    // Pesada: lenta, resistente, potente
    }

    /// <summary>
    /// ScriptableObject que define una pieza de peonza.
    /// Sus modificadores se suman a las stats base de FakeBladeStats.
    /// Valores POSITIVOS suben la stat y NEGATIVOS la bajan (permite trade-offs).
    /// </summary>
    [CreateAssetMenu(fileName = "New FakeBlade Component", menuName = "FakeBlade/Component Data")]
    public class FakeBladeComponentData : ScriptableObject
    {
        [Header("=== IDENTITY ===")]
        [SerializeField] private string componentName = "Component";
        [SerializeField][TextArea(2, 4)] private string description = "";
        [SerializeField] private ComponentSlot componentType = ComponentSlot.Body;
        [SerializeField] private WeightClass weightClass = WeightClass.Medium;
        [Tooltip("Arquetipo al que pertenece la pieza (se muestra en la selección). Coincide con el tipo de modelo: A Ataque, B Balanceada, C Defensa, D Agilidad")]
        [SerializeField] private BladeArchetype archetype = BladeArchetype.Balanced;
        [SerializeField] private Sprite icon;

        [Header("=== STAT MODIFIERS ===")]
        [Tooltip("Modifica las RPM máximas (+/-)")]
        [SerializeField] private float maxSpinModifier = 0f;

        [Tooltip("Modifica el desgaste de RPM. POSITIVO = pierde RPM más rápido. NEGATIVO = más estable.")]
        [SerializeField] private float spinDecayModifier = 0f;

        [Tooltip("Modifica la velocidad de movimiento (+/-)")]
        [SerializeField] private float moveSpeedModifier = 0f;

        [Tooltip("Modifica el peso (+/-). Más peso = más inercia y más fuerza en los choques.")]
        [SerializeField] private float weightModifier = 0f;

        [Tooltip("Modifica la potencia de ataque (+/-)")]
        [SerializeField] private float attackPowerModifier = 0f;

        [Tooltip("Modifica la defensa (+/-). Reduce daño y empuje recibidos.")]
        [SerializeField] private float defenseModifier = 0f;

        [Tooltip("Modifica la fuerza de dash (+/-)")]
        [SerializeField] private float dashForceModifier = 0f;

        [Tooltip("Modifica el número de cargas de ataque (+/-). Media de la peonza: 3.")]
        [SerializeField] private int attackChargesModifier = 0;

        [Tooltip("Modifica la ventana de parry en segundos (+/-). Pensado para que las piezas de " +
                 "agilidad la amplíen y las de defensa la reduzcan (base en CombatConfig).")]
        [SerializeField] private float parryWindowModifier = 0f;

        [Header("=== SPECIAL (solo Núcleo) ===")]
        [Tooltip("Poder especial que otorga este Núcleo")]
        [SerializeField] private SpecialAbilityType specialAbility = SpecialAbilityType.None;

        #region Public Properties
        public string ComponentName => componentName;
        public string Description => description;
        public ComponentSlot ComponentType => componentType;
        public WeightClass WeightClass => weightClass;
        public BladeArchetype Archetype => archetype;
        public Sprite Icon => icon;

        public float MaxSpinModifier => maxSpinModifier;
        public float SpinDecayModifier => spinDecayModifier;
        public float MoveSpeedModifier => moveSpeedModifier;
        public float WeightModifier => weightModifier;
        public float AttackPowerModifier => attackPowerModifier;
        public float DefenseModifier => defenseModifier;
        public float DashForceModifier => dashForceModifier;
        public int AttackChargesModifier => attackChargesModifier;
        public float ParryWindowModifier => parryWindowModifier;

        public SpecialAbilityType SpecialAbility => specialAbility;
        #endregion

        #region Utility
        /// <summary>
        /// Resumen legible de los modificadores distintos de cero (para UI de montaje).
        /// </summary>
        public string GetModifiersSummary()
        {
            var sb = new StringBuilder(96);
            Append(sb, "Spin", maxSpinModifier);
            Append(sb, "Decay", spinDecayModifier);
            Append(sb, "Speed", moveSpeedModifier);
            Append(sb, "Weight", weightModifier);
            Append(sb, "Atk", attackPowerModifier);
            Append(sb, "Def", defenseModifier);
            Append(sb, "Dash", dashForceModifier);
            Append(sb, "Charges", attackChargesModifier);
            Append(sb, "Parry", parryWindowModifier);
            return sb.Length > 0 ? sb.ToString() : "No modifiers";
        }

        private static void Append(StringBuilder sb, string label, float value)
        {
            if (Mathf.Approximately(value, 0f)) return;
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append(label).Append(':').Append(value > 0 ? "+" : "").Append(value.ToString("0.##"));
        }
        #endregion
    }
}
