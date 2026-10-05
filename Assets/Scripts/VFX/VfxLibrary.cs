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
        /// <summary>Aura genérica (poder sin aura propia).</summary>
        SpecialAura = 6,
        Respawn = 7,
        Parry = 8,

        // Aura de cada poder mientras está activo (GDD 5)
        AuraSpinBoost = 9,
        AuraShockWave = 10,
        AuraDefense = 11,
        AuraLightning = 12,

        // Ataque cargado (GDD 2.3)
        ChargeGather = 13,
        ChargeRing = 14,
        ChargeFlame = 15,

        // Peonza en movimiento y con RPM bajas (GDD 2.1)
        Trail = 16,
        GroundSpark = 17,
        LowSpinSmoke = 18,
        LowSpinSpark = 19,

        /// <summary>Destellos en estrella (carga al máximo, escudo...).</summary>
        Sparkle = 20
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

        // Los colores de los poderes están en su asset (SpecialAbilityData.color)

        [Header("Parry")]
        public Color parryColor = new Color(0.55f, 0.95f, 1f);

        [Header("Estados alterados (partículas, icono y HUD)")]
        public Color burnColor = new Color(1f, 0.45f, 0.1f);
        public Color freezeColor = new Color(0.55f, 0.9f, 1f);
        public Color launchColor = new Color(1f, 0.95f, 0.3f);
        [Tooltip("Partículas/s sobre la peonza con un estado alterado")]
        public float statusParticleRate = 22f;

        [Header("Ataque cargado")]
        [Tooltip("Color de las partículas en el nivel de carga máximo (el nivel 1 usa el color del jugador)")]
        public Color chargeHotColor = new Color(1f, 0.95f, 0.7f);
        [Tooltip("Partículas/s que convergen hacia la peonza en el nivel 1 (+50% por nivel)")]
        public float chargeGatherRate = 30f;
        [Tooltip("Llamas/s mientras la carga está al máximo")]
        public float chargeFlameRate = 26f;

        [Header("Estela")]
        [Tooltip("Metros recorridos entre dos partículas de la estela")]
        public float trailSpacing = 0.12f;
        [Tooltip("Velocidad mínima (m/s) para dejar estela")]
        public float trailMinSpeed = 2.5f;

        [Header("Chispas contra el suelo (giro rápido)")]
        public Color groundSparkColor = new Color(1f, 0.8f, 0.45f);
        [Tooltip("Chispas/s a velocidad y RPM altas")]
        public float groundSparkRate = 30f;
        [Tooltip("Velocidad (m/s) a partir de la que salen chispas")]
        public float groundSparkMinSpeed = 4f;
        [Tooltip("RPM (fracción) a partir de la que salen chispas")]
        [Range(0f, 1f)] public float groundSparkMinSpin = 0.5f;

        [Header("RPM bajas (umbral en CombatConfig.lowSpinThreshold)")]
        public Color smokeColor = new Color(0.32f, 0.32f, 0.36f, 0.85f);
        public Color lowSpinSparkColor = new Color(1f, 0.55f, 0.2f);
        [Tooltip("Bocanadas de humo/s cerca de 0 RPM")]
        public float lowSpinSmokeRate = 14f;
        [Tooltip("Chispas sueltas/s cerca de 0 RPM")]
        public float lowSpinSparkRate = 10f;

        public Entry Get(VfxType type)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].type == type) return entries[i];
            return null;
        }

        public Color GetStatusColor(StatusEffectType status)
        {
            switch (status)
            {
                case StatusEffectType.Burning: return burnColor;
                case StatusEffectType.Frozen: return freezeColor;
                case StatusEffectType.Launched: return launchColor;
                default: return Color.white;
            }
        }
    }
}
