using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// IA sencilla: persigue al enemigo más cercano rodeándolo, hace ataques rápidos
    /// o cargados cuando está cerca, usa el dash de vez en cuando y el especial al llenarse.
    ///
    /// Se usa en el fondo del menú principal y es la base de los futuros rivales con IA (GDD 10).
    /// Decide cada reactionTime segundos (no cada frame) para parecer humana y ser barata.
    /// </summary>
    [RequireComponent(typeof(FakeBladeController))]
    public class SimpleAIBrain : MonoBehaviour, IBladeInputSource
    {
        [Tooltip("0 = pasiva, 1 = muy agresiva")]
        [Range(0f, 1f)] public float aggression = 0.6f;
        [Tooltip("Segundos entre decisiones")]
        [Range(0.05f, 1f)] public float reactionTime = 0.25f;
        [Tooltip("Distancia a la que intenta atacar")]
        public float attackRange = 4.5f;

        private FakeBladeController _blade;
        private Vector2 _move;
        private bool _attackHeld;
        private float _holdTimer;
        private bool _dash;
        private bool _special;
        private float _thinkTimer;
        private float _orbitSign = 1f;

        public Vector2 MovementInput => _move;
        public bool AttackHeld => _attackHeld;

        private void Awake()
        {
            _blade = GetComponent<FakeBladeController>();
            _orbitSign = Random.value < 0.5f ? -1f : 1f;
            _thinkTimer = Random.Range(0f, reactionTime);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_holdTimer > 0f)
            {
                _holdTimer -= dt;
                _attackHeld = _holdTimer > 0f;
            }

            _thinkTimer -= dt;
            if (_thinkTimer > 0f) return;
            _thinkTimer = reactionTime * Random.Range(0.7f, 1.3f);

            Think();
        }

        private void Think()
        {
            if (_blade.IsDestroyed)
            {
                _move = Vector2.zero;
                return;
            }

            FakeBladeController target = _blade.FindNearestEnemy();
            if (target == null)
            {
                // Sin rivales: deambular suavemente
                Vector2 wander = Random.insideUnitCircle;
                _move = wander.sqrMagnitude > 0.01f ? wander.normalized * 0.5f : Vector2.zero;
                return;
            }

            Vector3 offset = target.Position - _blade.Position;
            Vector2 toTarget = new Vector2(offset.x, offset.z);
            float distance = toTarget.magnitude;
            Vector2 dir = distance > 0.01f ? toTarget / distance : Vector2.up;

            // Cerca: rodea al rival (mezcla de perpendicular y avance)
            Vector2 perpendicular = new Vector2(-dir.y, dir.x) * _orbitSign;
            float orbitWeight = distance < attackRange ? 0.6f : 0.15f;
            _move = (dir * (1f - orbitWeight) + perpendicular * orbitWeight).normalized;
            if (Random.value < 0.05f) _orbitSign = -_orbitSign;

            // Ataques
            bool canAttack = _holdTimer <= 0f && _blade.Attack.CurrentCharges > 0;
            if (canAttack && distance < attackRange && Random.value < aggression)
            {
                bool charged = _blade.Attack.CurrentCharges >= 2 && Random.value < 0.3f;
                _holdTimer = charged ? Random.Range(0.6f, 1.2f) : 0.05f;
                _attackHeld = true;
            }

            // Dash para cerrar distancia o escapar si va bajo de RPM
            if (_blade.CanDash)
            {
                bool lowSpin = _blade.SpinSpeedPercentage < 0.25f;
                if (lowSpin && distance < 3f && Random.value < 0.3f)
                {
                    _move = -dir;
                    _dash = true;
                }
                else if (distance < attackRange * 1.5f && Random.value < aggression * 0.25f)
                {
                    _dash = true;
                }
            }

            if (_blade.Special.IsReady && Random.value < 0.5f)
                _special = true;
        }

        public bool ConsumeDash()
        {
            if (!_dash) return false;
            _dash = false;
            return true;
        }

        public bool ConsumeSpecial()
        {
            if (!_special) return false;
            _special = false;
            return true;
        }

        public void ClearBuffers()
        {
            _dash = false;
            _special = false;
        }

        public void Vibrate(float lowFrequency, float highFrequency, float duration) { }
    }
}
