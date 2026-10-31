namespace FakeBlade.Core
{
    /// <summary>Pasos de Aprende a jugar, en orden (GDD 6.7).</summary>
    public enum TutorialStep
    {
        Move,
        Dash,
        QuickAttack,
        ChargedAttack,
        WallsAndDash,
        Parry,
        Block,
        Special,
        Fight,
        /// <summary>Tutorial completado: juego libre hasta salir.</summary>
        Done
    }

    /// <summary>Botón que enseña cada paso en la ventana de indicaciones.</summary>
    public enum TutorialButton
    {
        None,
        Move,
        Dash,
        Attack,
        /// <summary>Mantener ataque.</summary>
        HoldAttack,
        Special
    }

    /// <summary>
    /// Datos de cada paso (GDD 6.7): textos por Loc (TUT_{CLAVE}_TITLE y TUT_{CLAVE}_BODY), objetivo por jugador,
    /// botón que se enseña y acciones desbloqueadas. Las cifras de los objetivos son propuesta: se ajustan probándolo.
    /// </summary>
    public static class TutorialSteps
    {
        /// <summary>Pasos con objetivo (sin contar Done).</summary>
        public const int Count = (int)TutorialStep.Done;

        public static string Key(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Move: return "MOVE";
                case TutorialStep.Dash: return "DASH";
                case TutorialStep.QuickAttack: return "ATTACK";
                case TutorialStep.ChargedAttack: return "CHARGED";
                case TutorialStep.WallsAndDash: return "WALLS";
                case TutorialStep.Parry: return "PARRY";
                case TutorialStep.Block: return "BLOCK";
                case TutorialStep.Special: return "SPECIAL";
                case TutorialStep.Fight: return "FIGHT";
                default: return "DONE";
            }
        }

        /// <summary>Lo que tiene que conseguir cada jugador (segundos en Moverse; veces en los demás).</summary>
        public static int Goal(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Move: return 3;
                case TutorialStep.Dash: return 2;
                case TutorialStep.QuickAttack: return 5;
                case TutorialStep.ChargedAttack: return 3;
                case TutorialStep.WallsAndDash: return 2; // un golpe de pared al dummy y un dash que gane un choque
                case TutorialStep.Parry: return 3;
                case TutorialStep.Block: return 3;
                case TutorialStep.Special: return 1;
                case TutorialStep.Fight: return 1;
                default: return 0;
            }
        }

        public static TutorialButton Button(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Move: return TutorialButton.Move;
                case TutorialStep.Dash: return TutorialButton.Dash;
                case TutorialStep.QuickAttack: return TutorialButton.Attack;
                case TutorialStep.ChargedAttack: return TutorialButton.HoldAttack;
                case TutorialStep.WallsAndDash: return TutorialButton.Dash;
                case TutorialStep.Parry: return TutorialButton.Attack;
                case TutorialStep.Block: return TutorialButton.Attack;
                case TutorialStep.Special: return TutorialButton.Special;
                default: return TutorialButton.None;
            }
        }

        public static bool AllowsDash(TutorialStep step) => step >= TutorialStep.Dash;
        public static bool AllowsAttack(TutorialStep step) => step >= TutorialStep.QuickAttack;
        public static bool AllowsCharge(TutorialStep step) => step >= TutorialStep.ChargedAttack;
        /// <summary>Hasta su paso, el especial ni se carga ni se activa.</summary>
        public static bool AllowsSpecial(TutorialStep step) => step >= TutorialStep.Special;
    }
}
