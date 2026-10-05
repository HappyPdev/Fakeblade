using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>Condición de victoria (GDD 6.1).</summary>
    public enum WinCondition
    {
        /// <summary>Sin reaparición: gana el último en pie.</summary>
        LastStanding = 0,
        /// <summary>Cada jugador tiene N vidas y reaparece al perder una.</summary>
        Stocks = 1,
        /// <summary>Todos contra todos por puntos: cada K.O. da un punto al último que golpeó. Reaparición infinita.</summary>
        Points = 2,
        /// <summary>Sandbox (campo de pruebas, GDD 6.3): reaparición infinita y la partida no termina sola.
        /// Antes se llamaba Practice (mismo valor).</summary>
        Sandbox = 3
    }

    /// <summary>
    /// Reglas de una partida (GDD 6.2). Crear un asset por modo en Assets/Settings/GameModes.
    /// Por defecto no hay límite de tiempo.
    /// </summary>
    [CreateAssetMenu(fileName = "MatchRules", menuName = "FakeBlade/Match Rules")]
    public class MatchRules : ScriptableObject
    {
        [Tooltip("Clave de localización del nombre del modo (ver Loc)")]
        public string displayNameKey = "MODE_LAST_STANDING";

        public WinCondition winCondition = WinCondition.LastStanding;

        [Tooltip("Partida por equipos (usa el TeamID de cada jugador)")]
        public bool teams = false;

        [Tooltip("Los compañeros de equipo se quitan RPM al chocar")]
        public bool friendlyFire = false;

        [Tooltip("Vidas por jugador (solo modo Stocks)")]
        [Min(1)] public int lives = 3;

        [Tooltip("Límite de tiempo en segundos. 0 = sin límite")]
        [Min(0f)] public float timeLimit = 0f;

        [Tooltip("Puntos para ganar (solo modo Points). 0 = sin objetivo, gana quien más tenga al acabar el tiempo")]
        [Min(0)] public int pointsToWin = 0;

        [Header("Reaparición")]
        [Min(0f)] public float respawnDelay = 2f;
        [Tooltip("Segundos de invulnerabilidad al reaparecer")]
        [Min(0f)] public float respawnInvulnerability = 1.5f;

        public bool UsesRespawn => winCondition != WinCondition.LastStanding;
        public bool HasTimeLimit => timeLimit > 0f;
        public bool ShowsLives => winCondition == WinCondition.Stocks;
        public bool ShowsScore => winCondition == WinCondition.Points;

        /// <summary>Vidas iniciales. 0 = infinitas (modo Points).</summary>
        public int StartingLives => winCondition switch
        {
            WinCondition.LastStanding => 1,
            WinCondition.Stocks => Mathf.Max(1, lives),
            _ => 0
        };

        public bool IsSandbox => winCondition == WinCondition.Sandbox;

        public static MatchRules CreateDefault()
        {
            var rules = CreateInstance<MatchRules>();
            rules.name = "MatchRules (Default)";
            rules.hideFlags = HideFlags.DontSave;
            return rules;
        }

        /// <summary>Copia en runtime (para configurar sin tocar el asset original).</summary>
        public MatchRules CloneRuntime()
        {
            var copy = Instantiate(this);
            copy.name = name + " (Runtime)";
            copy.hideFlags = HideFlags.DontSave;
            return copy;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (winCondition == WinCondition.Points && timeLimit <= 0f && pointsToWin <= 0)
                Debug.LogWarning($"[MatchRules] '{name}': modo Points sin tiempo ni puntos objetivo nunca termina.", this);
        }
#endif
    }
}
