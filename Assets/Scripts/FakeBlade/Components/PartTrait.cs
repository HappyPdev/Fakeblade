using System;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Qué cambia un rasgo de pieza (GDD 3, "Rasgos de las piezas"). Son porcentajes sobre un valor
    /// del combate, no estadísticas. Los valores numéricos se serializan: no reordenar; los nuevos,
    /// al final (y subir <see cref="PartTraits.Count"/> si hace falta).
    /// </summary>
    public enum PartTraitType
    {
        // Energía del especial ganada por cada tipo de golpe acertado
        SpecialEnergyQuickHit = 0,
        SpecialEnergyDashHit = 1,
        SpecialEnergyChargedHit = 2,
        SpecialEnergyParry = 3,
        /// <summary>Toda la energía del especial.</summary>
        SpecialEnergyAll = 4,

        // Dash
        /// <summary>Tiempo de espera entre dashes (−0,2 = un 20% menos de espera).</summary>
        DashCooldown = 5,
        /// <summary>RPM que cuesta el dash.</summary>
        DashCost = 6,

        // Ataques
        /// <summary>Tiempo de recarga de cada carga de ataque.</summary>
        AttackRechargeTime = 7,
        /// <summary>RPM que cuestan los ataques.</summary>
        AttackCost = 8,
        /// <summary>Tiempo para subir cada nivel del ataque cargado.</summary>
        ChargeTime = 9
    }

    /// <summary>
    /// Rasgo de una pieza: un porcentaje sobre un valor del combate (+0,25 = +25%, −0,1 = −10%).
    /// Una pieza puede tener varios (por ejemplo, dash con menos espera pero más caro).
    /// </summary>
    [Serializable]
    public struct PartTrait
    {
        public PartTraitType type;
        [Tooltip("Cambio en tanto por uno: 0,25 = +25%, -0,1 = −10%")]
        public float percent;
    }

    /// <summary>
    /// Suma de los rasgos de las piezas equipadas. Cada tipo da un multiplicador 1 + suma de sus
    /// porcentajes (nunca por debajo de 0,1). Sin rasgos, todo es 1.
    /// </summary>
    public sealed class PartTraits
    {
        public const int Count = 10;
        private const float MinMultiplier = 0.1f;

        public static readonly PartTraits None = new PartTraits();

        private readonly float[] _sum = new float[Count];

        public void Add(PartTrait trait)
        {
            int index = (int)trait.type;
            if (index >= 0 && index < Count) _sum[index] += trait.percent;
        }

        public float Multiplier(PartTraitType type)
        {
            int index = (int)type;
            return index >= 0 && index < Count ? Mathf.Max(MinMultiplier, 1f + _sum[index]) : 1f;
        }

        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < Count; i++)
                    if (_sum[i] != 0f) return false;
                return true;
            }
        }

        /// <summary>"DashCooldown=-20%|DashCost=+10%" (registro del sandbox).</summary>
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Count; i++)
            {
                if (_sum[i] == 0f) continue;
                if (sb.Length > 0) sb.Append('|');
                sb.Append((PartTraitType)i).Append('=').Append(_sum[i] > 0f ? "+" : "")
                    .Append(Mathf.RoundToInt(_sum[i] * 100f)).Append('%');
            }
            return sb.ToString();
        }
    }
}
