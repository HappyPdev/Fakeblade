using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Registro de los poderes: carga todos los <see cref="SpecialAbilityData"/> de
    /// Resources/SpecialAbilities la primera vez que se piden. Si falta el asset de un poder
    /// se usa Spin Boost (y si no hay ninguno, uno por defecto en memoria) con un aviso.
    /// </summary>
    public static class SpecialAbilities
    {
        public const string ResourcesFolder = "SpecialAbilities";

        private static readonly Dictionary<SpecialAbilityType, SpecialAbilityData> s_byType =
            new Dictionary<SpecialAbilityType, SpecialAbilityData>();
        private static readonly List<SpecialAbilityData> s_all = new List<SpecialAbilityData>();
        private static readonly HashSet<SpecialAbilityType> s_warned = new HashSet<SpecialAbilityType>();
        private static bool s_loaded;
        private static SpecialAbilityData s_fallback;

        /// <summary>Todos los poderes disponibles (para listas como el panel del sandbox).</summary>
        public static IReadOnlyList<SpecialAbilityData> All
        {
            get
            {
                EnsureLoaded();
                return s_all;
            }
        }

        /// <summary>Datos de un poder. Nunca devuelve null.</summary>
        public static SpecialAbilityData Get(SpecialAbilityType type)
        {
            EnsureLoaded();
            if (type == SpecialAbilityType.None) type = SpecialAbilityType.SpinBoost;
            if (s_byType.TryGetValue(type, out SpecialAbilityData data)) return data;

            if (s_warned.Add(type))
                Debug.LogWarning($"[SpecialAbilities] No hay asset para {type} en Resources/{ResourcesFolder}. " +
                                 "Se usa Spin Boost. Ejecuta 'FakeBlade/Setup Specials'.");
            return s_byType.TryGetValue(SpecialAbilityType.SpinBoost, out data) ? data : Fallback;
        }

        private static SpecialAbilityData Fallback
        {
            get
            {
                if (s_fallback == null)
                {
                    var spinBoost = ScriptableObject.CreateInstance<SpinBoostData>();
                    spinBoost.name = "SpinBoost (Default)";
                    spinBoost.hideFlags = HideFlags.DontSave;
                    s_fallback = spinBoost;
                }
                return s_fallback;
            }
        }

        private static void EnsureLoaded()
        {
            if (s_loaded) return;
            s_loaded = true;

            foreach (SpecialAbilityData data in Resources.LoadAll<SpecialAbilityData>(ResourcesFolder))
            {
                if (s_byType.ContainsKey(data.Type))
                {
                    Debug.LogWarning($"[SpecialAbilities] {data.name} repite el poder {data.Type}; se ignora.", data);
                    continue;
                }
                s_byType.Add(data.Type, data);
                s_all.Add(data);
            }
            s_all.Sort((a, b) => a.Type.CompareTo(b.Type));
        }

        /// <summary>Vacía la caché al entrar en Play (assets creados o editados desde la última vez).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            s_loaded = false;
            s_byType.Clear();
            s_all.Clear();
            s_warned.Clear();
        }
    }
}
