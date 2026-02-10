namespace FakeBlade.Core
{
    /// <summary>
    /// Tipos de habilidades especiales disponibles para las peonzas.
    /// Asignadas a través del componente Core equipado.
    /// </summary>
    public enum SpecialAbilityType
    {
        /// <summary>Sin habilidad especial.</summary>
        None = 0,

        /// <summary>Recupera velocidad de spin.</summary>
        SpinBoost = 1,

        /// <summary>Onda de choque que empuja a enemigos cercanos.</summary>
        ShockWave = 2,

        /// <summary>Escudo temporal que reduce daño recibido.</summary>
        Shield = 3,

        /// <summary>Dash extra instantáneo.</summary>
        Dash = 4
    }
}