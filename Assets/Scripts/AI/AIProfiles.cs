using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Perfiles de IA (D3, D2): los assets de Resources/AI/Profiles, qué clase de AIBrain juega cada comportamiento
    /// y cómo se pone en una peonza. Balanceada es AIBrain tal cual; Ataque, Defensa y Agilidad heredan de ella.
    /// </summary>
    public static class AIProfiles
    {
        private static List<AIProfile> _all;

        /// <summary>Todos los perfiles, por orden. Siempre hay uno de cada comportamiento (por defecto si falta su asset).</summary>
        public static IReadOnlyList<AIProfile> All
        {
            get
            {
                if (_all == null) Load();
                return _all;
            }
        }

        /// <summary>Olvida los perfiles cargados (al crear o borrar assets desde el editor).</summary>
        public static void ClearCache() => _all = null;

        private static void Load()
        {
            _all = new List<AIProfile>(Resources.LoadAll<AIProfile>(AIProfile.ResourcesFolder));
            foreach (AIStyle style in System.Enum.GetValues(typeof(AIStyle)))
            {
                if (_all.Exists(p => p.style == style)) continue;
                var profile = ScriptableObject.CreateInstance<AIProfile>();
                profile.name = $"AIProfile {style} (Default)";
                profile.hideFlags = HideFlags.DontSave;
                profile.ApplyDefaults(style);
                _all.Add(profile);
            }
            _all.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.name, b.name));
        }

        /// <summary>El primer perfil (por orden) de un comportamiento.</summary>
        public static AIProfile ForStyle(AIStyle style)
        {
            IReadOnlyList<AIProfile> all = All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].style == style) return all[i];
            return all[0];
        }

        /// <summary>El perfil «según peonza»: el primero del comportamiento de su arquetipo.</summary>
        public static AIProfile ForArchetype(BladeArchetype archetype) => ForStyle((AIStyle)(int)archetype);

        /// <summary>Perfil elegido en una lista con «según peonza» delante: 0 = el de su arquetipo; i = All[i - 1].</summary>
        public static AIProfile Resolve(int choice, FakeBladeStats stats)
        {
            if (choice > 0 && choice <= All.Count) return All[choice - 1];
            return ForArchetype(stats != null ? stats.Archetype : BladeArchetype.Balanced);
        }

        public static System.Type BrainType(AIStyle style)
        {
            switch (style)
            {
                case AIStyle.Attack: return typeof(AttackAIBrain);
                case AIStyle.Defense: return typeof(DefenseAIBrain);
                case AIStyle.Agility: return typeof(AgilityAIBrain);
                default: return typeof(AIBrain);
            }
        }

        /// <summary>
        /// Deja en la peonza una sola IA, la del comportamiento del perfil, con ese perfil: conserva la que ya tenga
        /// si es de esa clase y quita las demás. No se llama cada frame (GetComponents asigna).
        /// </summary>
        public static AIBrain Ensure(GameObject go, AIProfile profile)
        {
            if (profile == null) profile = ForStyle(AIStyle.Balanced);
            System.Type type = BrainType(profile.style);
            AIBrain kept = null;
            foreach (AIBrain brain in go.GetComponents<AIBrain>())
            {
                if (kept == null && brain.GetType() == type)
                {
                    kept = brain;
                    continue;
                }
                brain.ClearBuffers();
                brain.enabled = false;
                Object.Destroy(brain);
            }
            if (kept == null) kept = (AIBrain)go.AddComponent(type);
            kept.Profile = profile;
            return kept;
        }
    }
}
