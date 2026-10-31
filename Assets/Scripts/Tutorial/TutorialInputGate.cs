using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Aprende a jugar (GDD 6.7): se pone entre el mando del jugador y su peonza y deja pasar solo las
    /// acciones ya enseñadas. El movimiento siempre pasa. Con el ataque permitido pero sin la carga, el
    /// botón se «suelta» solo antes del umbral de carga, así que sale un ataque rápido aunque se mantenga.
    /// </summary>
    public sealed class TutorialInputGate : IBladeInputSource
    {
        private readonly IBladeInputSource _inner;
        private bool _wasHeld;
        private float _pressTime;

        public bool AllowDash;
        public bool AllowAttack;
        public bool AllowCharge;
        public bool AllowSpecial;

        public TutorialInputGate(IBladeInputSource inner)
        {
            _inner = inner;
        }

        public Vector2 MovementInput => _inner.MovementInput;

        public bool AttackHeld
        {
            get
            {
                bool held = _inner.AttackHeld;
                if (held && !_wasHeld) _pressTime = Time.time;
                _wasHeld = held;

                if (!held || !AllowAttack) return false;
                if (AllowCharge) return true;
                // Se suelta antes de que empiece la carga: ataque rápido
                return Time.time - _pressTime < CombatConfig.Active.chargeHoldThreshold * 0.75f;
            }
        }

        // Se consumen siempre, para que una pulsación bloqueada no salga más tarde
        public bool ConsumeDash() => _inner.ConsumeDash() && AllowDash;
        public bool ConsumeSpecial() => _inner.ConsumeSpecial() && AllowSpecial;

        public void ClearBuffers()
        {
            _inner.ClearBuffers();
            _wasHeld = false;
        }

        public void Vibrate(float lowFrequency, float highFrequency, float duration) =>
            _inner.Vibrate(lowFrequency, highFrequency, duration);
    }
}
