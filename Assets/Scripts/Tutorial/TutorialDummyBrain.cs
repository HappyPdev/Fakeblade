using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>Lo que hace el dummy de Aprende a jugar según el paso (GDD 6.7).</summary>
    public enum TutorialDummyMode
    {
        /// <summary>Quieto en el centro.</summary>
        Idle,
        /// <summary>Paso del parry: un ataque rápido cada segundo a su objetivo, con aviso antes.</summary>
        QuickAttacks,
        /// <summary>Paso del bloqueo: ataques cargados de nivel 2 (lo que se bloquea en vez de hacer parry).</summary>
        ChargedAttacks
    }

    /// <summary>
    /// Dummy de Aprende a jugar (GDD 6.7). Pulsa los mismos botones que un jugador (IBladeInputSource).
    /// Se queda en el centro: si lo sacan, espera a que deje de recibir golpes y vuelve solo a su sitio, frenando al
    /// llegar. En los pasos del parry y del bloqueo ataca a su objetivo (Target, lo elige TutorialController) si está
    /// cerca: rápidos cada segundo con un anillo de aviso antes, o cargados de nivel 2. Antes de cada ataque se le
    /// rellenan las cargas, para que el ritmo no dependa de su recarga.
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

        /// <summary>Un ataque cada segundo (GDD 6.7).</summary>
        private const float AttackInterval = 1f;
        /// <summary>El anillo de aviso sale este tiempo antes del ataque rápido.</summary>
        private const float WarningTime = 0.4f;
        /// <summary>Solo ataca si su objetivo está a esta distancia o menos (m): más lejos, el ataque no llega.</summary>
        private const float AttackRange = 4.5f;
        /// <summary>Lo que dura pulsado el ataque rápido: menos que el umbral de carga.</summary>
        private const float AttackTapTime = 0.06f;
        /// <summary>Nivel de los cargados del paso del bloqueo: desde 2, el parry es un bloqueo (GDD 2.5).</summary>
        private const int BlockChargeLevel = 2;
        /// <summary>Tope de lo que mantiene la carga (s): el nivel 2 llega en ~1 s (0,18 + 2 × 0,4).</summary>
        private const float MaxChargeTime = 2f;
        private static readonly Color WarningColor = new Color(1f, 0.45f, 0.15f);

        private FakeBladeController _blade;
        private Vector2 _move;
        private bool _attackHeld;
        private float _attackRelease;
        private float _timer;
        private bool _warned;
        private bool _charging;
        private float _chargeTime;

        /// <summary>Su sitio, en el centro de la arena.</summary>
        public Vector3 Home { get; set; }

        /// <summary>
        /// Vuelve solo a su sitio. En el paso de paredes no: desde el centro un golpe no lo lleva hasta la pared
        /// (un empujón de 12 m/s lo mueve unos 3 m y la pared está a unos 7), así que se le va empujando.
        /// </summary>
        public bool ReturnHome { get; set; } = true;

        public TutorialDummyMode Mode { get; private set; }

        /// <summary>Peonza a la que ataca en los pasos del parry y del bloqueo (null = nadie).</summary>
        public FakeBladeController Target { get; set; }

        public Vector2 MovementInput => _move;
        public bool AttackHeld => _attackHeld || _attackRelease > 0f;
        public bool ConsumeDash() => false;
        public bool ConsumeSpecial() => false;

        public void ClearBuffers()
        {
            _move = Vector2.zero;
            _attackHeld = false;
            _attackRelease = 0f;
        }

        public void Vibrate(float lowFrequency, float highFrequency, float duration) { }

        public void SetMode(TutorialDummyMode mode)
        {
            Mode = mode;
            _timer = 0f;
            _warned = false;
            _charging = false;
            ClearBuffers();
        }

        private void Awake()
        {
            _blade = GetComponent<FakeBladeController>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _move = Vector2.zero;
            if (dt <= 0f || _blade.IsDestroyed) return;
            if (_attackRelease > 0f) _attackRelease -= dt;

            if (Mode != TutorialDummyMode.Idle && UpdateAttacks(dt)) return;
            ReturnToHome();
        }

        /// <summary>Ataques de los pasos del parry y del bloqueo. Devuelve true si este frame apunta al objetivo.</summary>
        private bool UpdateAttacks(float dt)
        {
            if (_blade.IsStaggered)
            {
                // Tras un parry o un bloqueo queda aturdido: suelta la carga y vuelve a empezar
                _charging = _attackHeld = false;
                _timer = 0f;
                _warned = false;
                return false;
            }

            if (_charging)
            {
                Aim();
                _chargeTime += dt;
                // Suelta en el nivel 2; si el ataque ya salió solo o algo lo cortó, no se queda esperando
                if (_blade.Attack.ChargeLevel >= BlockChargeLevel || _chargeTime > MaxChargeTime)
                {
                    _attackHeld = false;
                    _charging = false;
                    _timer = 0f;
                }
                return true;
            }

            if (_blade.Attack.IsAttacking) return false;

            bool inRange = TargetInRange();
            _timer = Mathf.Min(_timer + dt, AttackInterval);

            if (Mode == TutorialDummyMode.QuickAttacks && !_warned && inRange && _timer >= AttackInterval - WarningTime)
            {
                _warned = true;
                VfxSystem.Play(VfxType.ChargeRing, _blade.Position, Vector3.up, WarningColor, 1f);
            }

            if (_timer < AttackInterval || !inRange) return false;

            _blade.Attack.RefillCharges();
            _timer = 0f;
            _warned = false;
            Aim();
            if (Mode == TutorialDummyMode.QuickAttacks) _attackRelease = AttackTapTime;
            else
            {
                _charging = _attackHeld = true;
                _chargeTime = 0f;
            }
            return true;
        }

        private bool TargetInRange()
        {
            if (Target == null || Target.IsDestroyed) return false;
            Vector3 offset = Target.Position - _blade.Position;
            offset.y = 0f;
            return offset.sqrMagnitude <= AttackRange * AttackRange;
        }

        /// <summary>El ataque sale hacia donde apunta el stick (GDD 2.4).</summary>
        private void Aim()
        {
            if (Target == null) return;
            Vector3 offset = Target.Position - _blade.Position;
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.0001f) return;
            offset.Normalize();
            _move = new Vector2(offset.x, offset.z);
        }

        private void ReturnToHome()
        {
            if (!ReturnHome || _blade.IsStaggered) return;
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
