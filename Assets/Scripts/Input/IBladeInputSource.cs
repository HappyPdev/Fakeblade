using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Fuente de órdenes de una peonza (GDD 7.2): jugador humano (InputHandler),
    /// dummy de práctica o IA. PlayerController solo habla con esta interfaz.
    /// </summary>
    public interface IBladeInputSource
    {
        Vector2 MovementInput { get; }
        bool AttackHeld { get; }
        /// <summary>True una vez por pulsación de dash.</summary>
        bool ConsumeDash();
        /// <summary>True una vez por pulsación de especial.</summary>
        bool ConsumeSpecial();
        void ClearBuffers();
        void Vibrate(float lowFrequency, float highFrequency, float duration);
    }
}
