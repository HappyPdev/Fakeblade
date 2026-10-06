namespace FakeBlade.Core
{
    /// <summary>Qué hacen los dummies del sandbox (GDD 6.3). Uno para todos a la vez.</summary>
    public enum DummyBehaviour
    {
        Idle = 0,
        Move = 1,
        /// <summary>Ataque rápido hacia el jugador más cercano cada X s (para practicar parry).</summary>
        Attack = 2,
        DashAtYou = 3,
        Special = 4
    }

    /// <summary>A quién se aplica un truco del sandbox (GDD 6.3). Los valores se muestran en ese orden.</summary>
    public enum CheatTarget
    {
        Off = 0,
        Players = 1,
        Dummies = 2,
        All = 3
    }

    /// <summary>
    /// Ajustes del sandbox elegidos en su panel. Estáticos a propósito: se mantienen al reiniciar
    /// la partida o volver a entrar al sandbox durante la sesión.
    /// </summary>
    public static class SandboxSettings
    {
        // Trucos (E2)
        public static CheatTarget InfiniteSpin = CheatTarget.Off;
        public static CheatTarget FullSpecial = CheatTarget.Off;
        public static CheatTarget InfiniteCharges = CheatTarget.Off;
        public static CheatTarget NoDashCooldown = CheatTarget.Off;
        public static CheatTarget Invulnerable = CheatTarget.Off;

        public static bool Applies(CheatTarget target, bool isDummy) =>
            target == CheatTarget.All || (isDummy ? target == CheatTarget.Dummies : target == CheatTarget.Players);

        // Debug visual (E4) y registro (E5): todo apagado al empezar
        public static bool ShowParryWindow;
        public static bool ShowVelocity;
        public static bool ShowDamageNumbers;
        public static bool ShowStatus;
        public static bool ShowFps;
        public static bool ShowEventLog;
        /// <summary>Guardar los datos de la sesión en un CSV (SandboxRecorder). Activado por defecto.</summary>
        public static bool RecordData = true;

        // Tiempo (E6): x1, x0,5, x0,25 y pausa (avance frame a frame)
        public static readonly float[] Speeds = { 1f, 0.5f, 0.25f, 0f };
        public static int SpeedIndex;
        public static float Speed => Speeds[UnityEngine.Mathf.Clamp(SpeedIndex, 0, Speeds.Length - 1)];

        // Ajustes (E7): multiplicadores temporales de valores de CombatConfig (índice en TuningFactors)
        public static readonly float[] TuningFactors = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };
        public const int NeutralFactor = 2;
        public static readonly int[] Tuning = { NeutralFactor, NeutralFactor, NeutralFactor, NeutralFactor, NeutralFactor, NeutralFactor, NeutralFactor, NeutralFactor, NeutralFactor, NeutralFactor };
        /// <summary>Copia de CombatConfig que usa el sandbox (los retoques no tocan el asset).</summary>
        public static CombatConfig TunedConfig;
        public static CombatConfig OriginalConfig;

        /// <summary>Al salir del sandbox: velocidad normal, valores originales y sin debug de tiempo.</summary>
        public static void ResetSession()
        {
            SpeedIndex = 0;
            for (int i = 0; i < Tuning.Length; i++) Tuning[i] = NeutralFactor;
            if (TunedConfig != null) UnityEngine.Object.Destroy(TunedConfig);
            TunedConfig = null;
            OriginalConfig = null;
        }

        public static readonly float[] Intervals = { 0.5f, 1f, 2f, 3f };

        public static int DummyCount = 1;
        public static DummyBehaviour Behaviour = DummyBehaviour.Idle;
        public static int IntervalIndex = 1;

        /// <summary>Segundos entre acciones de los dummies (atacar, dash, especial).</summary>
        public static float Interval => Intervals[UnityEngine.Mathf.Clamp(IntervalIndex, 0, Intervals.Length - 1)];
    }
}
