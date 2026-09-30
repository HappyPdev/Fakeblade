using System;
using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>Efectos de partículas del juego.</summary>
    public enum VfxType
    {
        Clash = 0,
        WallHit = 1,
        Dash = 2,
        Attack = 3,
        SpinOut = 4,
        SpecialBurst = 5,
        SpecialAura = 6,
        Respawn = 7,
        Parry = 8
    }

    /// <summary>
    /// Biblioteca de efectos (GDD 7.3): un prefab de ParticleSystem por tipo y cuántas
    /// partículas emite una ráfaga normal. Vive en Resources para estar disponible en
    /// cualquier escena sin referencias.
    /// </summary>
    [CreateAssetMenu(fileName = "VfxLibrary", menuName = "FakeBlade/VFX Library")]
    public class VfxLibrary : ScriptableObject
    {
        public const string ResourcesPath = "VfxLibrary";

        [Serializable]
        public class Entry
        {
            public VfxType type;
            public ParticleSystem prefab;
            [Tooltip("Partículas por ráfaga a intensidad 1 (antes del ajuste de Opciones)")]
            [Min(1)] public int burstCount = 12;
        }

        public List<Entry> entries = new List<Entry>();

        [Header("Colores de los poderes")]
        public Color spinBoostColor = new Color(0.3f, 1f, 0.6f);
        public Color shockWaveColor = new Color(1f, 0.55f, 0.15f);
        public Color stormBreakerColor = new Color(0.45f, 0.55f, 1f);
        public Color electricDashColor = new Color(1f, 0.95f, 0.25f);

        [Header("Parry")]
        public Color parryColor = new Color(0.55f, 0.95f, 1f);

        public Entry Get(VfxType type)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].type == type) return entries[i];
            return null;
        }

        public Color GetAbilityColor(SpecialAbilityType type)
        {
            switch (type)
            {
                case SpecialAbilityType.ShockWave: return shockWaveColor;
                case SpecialAbilityType.Shield: return stormBreakerColor;
                case SpecialAbilityType.Dash: return electricDashColor;
                default: return spinBoostColor;
            }
        }
    }
}
