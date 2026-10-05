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

    /// <summary>
    /// Ajustes del sandbox elegidos en su panel. Estáticos a propósito: se mantienen al reiniciar
    /// la partida o volver a entrar al sandbox durante la sesión.
    /// </summary>
    public static class SandboxSettings
    {
        public static readonly float[] Intervals = { 0.5f, 1f, 2f, 3f };

        public static int DummyCount = 1;
        public static DummyBehaviour Behaviour = DummyBehaviour.Idle;
        public static int IntervalIndex = 1;

        /// <summary>Segundos entre acciones de los dummies (atacar, dash, especial).</summary>
        public static float Interval => Intervals[UnityEngine.Mathf.Clamp(IntervalIndex, 0, Intervals.Length - 1)];
    }
}
