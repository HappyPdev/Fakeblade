using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Dummy de Aprende a jugar (GDD 6.7). Pulsa los mismos botones que un jugador (IBladeInputSource).
    /// Se queda en el centro: si lo sacan, espera a que deje de recibir golpes y vuelve solo a su sitio,
    /// frenando al llegar. Lo que hace en cada paso (atacar para el parry, cargar para el bloqueo) llega con P4-P6.
    /// </summary>
    [RequireComponent(typeof(FakeBladeController))]
    public class TutorialDummyBrain : MonoBehaviour, IBladeInputSource
    {
        /// <summary>Segundos sin recibir daño antes de volver (para que se vea el empujón).</summary>
        private const float ReturnDelay = 0.8f;
        /// <summary>A esta distancia del centro ya está en su sitio (m).</summary>
        private const float ArriveRadius = 0.25f;
        /// <summary>Desde aquí frena poco a poco al acercarse (m).</summary>
        private const float SlowRadius = 2f;
        /// <summary>Stick al volver: sin prisa, para que no parezca que ataca.</summary>
        private const float ReturnInput = 0.7f;
        /// <summary>Stick mínimo: por debajo de la zona muerta de la peonza (0,2) no se movería.</summary>
        private const float MinInput = 0.3f;

        private FakeBladeController _blade;
        private Vector2 _move;

        /// <summary>Su sitio, en el centro de la arena.</summary>
        public Vector3 Home { get; set; }

        public Vector2 MovementInput => _move;
        public bool AttackHeld => false;
        public bool ConsumeDash() => false;
        public bool ConsumeSpecial() => false;
        public void ClearBuffers() => _move = Vector2.zero;
        public void Vibrate(float lowFrequency, float highFrequency, float duration) { }

        private void Awake()
        {
            _blade = GetComponent<FakeBladeController>();
        }

        private void Update()
        {
            _move = Vector2.zero;
            if (Time.deltaTime <= 0f || _blade.IsDestroyed || _blade.IsStaggered) return;
            if (Time.time - _blade.LastDamageTime < ReturnDelay) return;

            Vector3 offset = Home - _blade.Position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance < ArriveRadius) return;

            float strength = Mathf.Max(MinInput, ReturnInput * Mathf.Clamp01(distance / SlowRadius));
            Vector3 dir = offset / distance;
            _move = new Vector2(dir.x, dir.z) * strength;
        }
    }
}
