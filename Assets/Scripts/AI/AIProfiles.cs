using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>Perfil de IA elegido (GDD 6.4, D3): Auto = el arquetipo de su peonza.</summary>
    public enum AIProfileChoice
    {
        Auto = 0,
        Balanced = 1,
        Attack = 2,
        Defense = 3,
        Agility = 4
    }

    /// <summary>
    /// Perfiles de IA por arquetipo (D3): qué clase de AIBrain juega cada uno y cómo se pone en una peonza.
    /// Balanceada es AIBrain tal cual; Ataque, Defensa y Agilidad heredan de ella.
    /// </summary>
    public static class AIProfiles
    {
        /// <summary>Arquetipo que juega la IA: el elegido o, en Auto, el de su peonza.</summary>
        public static BladeArchetype Resolve(AIProfileChoice choice, FakeBladeStats stats)
        {
            if (choice != AIProfileChoice.Auto) return (BladeArchetype)((int)choice - 1);
            return stats != null ? stats.Archetype : BladeArchetype.Balanced;
        }

        public static System.Type BrainType(BladeArchetype profile)
        {
            switch (profile)
            {
                case BladeArchetype.Attack: return typeof(AttackAIBrain);
                case BladeArchetype.Defense: return typeof(DefenseAIBrain);
                case BladeArchetype.Agility: return typeof(AgilityAIBrain);
                default: return typeof(AIBrain);
            }
        }

        /// <summary>
        /// Deja en la peonza una sola IA, la del perfil pedido: conserva la que ya tenga si es de ese perfil
        /// y quita las demás. No se llama cada frame (GetComponents asigna).
        /// </summary>
        public static AIBrain Ensure(GameObject go, BladeArchetype profile)
        {
            System.Type type = BrainType(profile);
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
            return kept;
        }
    }
}
