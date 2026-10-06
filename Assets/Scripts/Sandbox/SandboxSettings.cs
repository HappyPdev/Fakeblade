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

        public static readonly float[] Intervals = { 0.5f, 1f, 2f, 3f };

        public static int DummyCount = 1;
        public static DummyBehaviour Behaviour = DummyBehaviour.Idle;
        public static int IntervalIndex = 1;

        /// <summary>Segundos entre acciones de los dummies (atacar, dash, especial).</summary>
        public static float Interval => Intervals[UnityEngine.Mathf.Clamp(IntervalIndex, 0, Intervals.Length - 1)];
    }
}
