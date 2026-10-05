using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Dummy del sandbox (GDD 6.3). Pulsa los mismos botones que un jugador (IBladeInputSource)
    /// según el comportamiento elegido en el panel (SandboxSettings), que se aplica al momento:
    /// - Quieto: no hace nada.
    /// - Moverse: va de un punto al azar de la arena a otro.
    /// - Atacar: cada X s, un ataque rápido hacia el jugador más cercano (sin dirección, el
    ///   ataque apunta solo al enemigo más cercano). Sirve para practicar el parry.
    /// - Dash hacia ti: cada X s, un dash hacia el jugador más cercano.
    /// - Especial: cada X s llena su esfera y activa su poder.
    /// </summary>
    [RequireComponent(typeof(FakeBladeController))]
    public class SandboxDummyBrain : MonoBehaviour, IBladeInputSource
    {
        /// <summary>Lo que dura pulsado el ataque: menos que el umbral de carga, así es un ataque rápido.</summary>
        private const float AttackTapTime = 0.06f;
        private const float WanderRetarget = 1.6f;
        private const float WanderArrive = 0.6f;

        private FakeBladeController _blade;
        private ArenaDefinition _arena;
        private DummyBehaviour _lastBehaviour;
        private float _actionTimer;
        private float _attackRelease;
        private bool _dash;
        private bool _special;
        private Vector2 _move;
        private Vector3 _wanderTarget;
        private float _wanderTimer;

        public Vector2 MovementInput => _move;
        public bool AttackHeld => _attackRelease > 0f;

        public bool ConsumeDash()
        {
            bool value = _dash;
            _dash = false;
            return value;
        }

        public bool ConsumeSpecial()
        {
            bool value = _special;
            _special = false;
            return value;
        }

        public void ClearBuffers()
        {
            _dash = _special = false;
            _attackRelease = 0f;
            _move = Vector2.zero;
        }

        public void Vibrate(float lowFrequency, float highFrequency, float duration) { }

        private void Awake()
        {
            _blade = GetComponent<FakeBladeController>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            DummyBehaviour behaviour = SandboxSettings.Behaviour;
            if (behaviour != _lastBehaviour)
            {
                _lastBehaviour = behaviour;
                _actionTimer = 0f;
                ClearBuffers();
            }

            if (_attackRelease > 0f) _attackRelease -= dt;
            _move = Vector2.zero;

            switch (behaviour)
            {
                case DummyBehaviour.Move:
                    Wander(dt);
                    break;
                case DummyBehaviour.Attack:
                case DummyBehaviour.DashAtYou:
                case DummyBehaviour.Special:
                    _actionTimer += dt;
                    // Si la acción aún no está disponible (cooldown, sin cargas, poder activo),
                    // se intenta cada frame hasta que lo esté: el intervalo cuenta desde que la hace
                    if (_actionTimer >= SandboxSettings.Interval && TryAct(behaviour))
                        _actionTimer = 0f;
                    break;
            }
        }

        private bool TryAct(DummyBehaviour behaviour)
        {
            if (_blade.IsDestroyed || _blade.IsStaggered) return false;
            switch (behaviour)
            {
                case DummyBehaviour.Attack:
                    if (_blade.Attack.CurrentCharges <= 0 || _blade.Attack.IsAttacking) return false;
                    _attackRelease = AttackTapTime;
                    return true;
                case DummyBehaviour.DashAtYou:
                    if (!_blade.CanDash) return false;
                    _dash = true;
                    return true;
                case DummyBehaviour.Special:
                    if (_blade.Special.IsActive) return false;
                    _blade.Special.FillEnergy();
                    _special = true;
                    return true;
                default:
                    return true;
            }
        }

        private void Wander(float dt)
        {
            if (_arena == null) _arena = FindAnyObjectByType<ArenaDefinition>();
            if (_arena == null) return;

            Vector3 offset = _wanderTarget - _blade.Position;
            offset.y = 0f;
            _wanderTimer -= dt;
            if (_wanderTimer <= 0f || offset.sqrMagnitude < WanderArrive * WanderArrive)
            {
                _wanderTarget = _arena.RandomPoint(0.6f);
                _wanderTimer = WanderRetarget;
                offset = _wanderTarget - _blade.Position;
                offset.y = 0f;
            }

            if (offset.sqrMagnitude > 0.0001f)
            {
                Vector3 dir = offset.normalized;
                _move = new Vector2(dir.x, dir.z);
            }
        }
    }
}
