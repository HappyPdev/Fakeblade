using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Dummy de práctica (GDD 6.1): no se mueve ni ataca. Sirve de saco de golpes.
    /// </summary>
    public class DummyBrain : MonoBehaviour, IBladeInputSource
    {
        public Vector2 MovementInput => Vector2.zero;
        public bool AttackHeld => false;
        public bool ConsumeDash() => false;
        public bool ConsumeSpecial() => false;
        public void ClearBuffers() { }
        public void Vibrate(float lowFrequency, float highFrequency, float duration) { }
    }
}
