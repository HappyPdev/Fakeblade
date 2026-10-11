using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Perfil de Defensa (GDD 6.4, D3): domina el centro de la arena y aguanta. Ataca poco salvo cuando el rival
    /// le abre hueco (acaba de atacar, está sin cargas o aturdido) o cuando puede empotrarlo contra la pared; como
    /// está en el centro, sus golpes empujan al rival hacia fuera. Solo deja el centro para contraatacar o empotrar
    /// a un rival cercano, si el rival no se acerca o si va muy por detrás.
    /// Parry ×1,1 y casi no esquiva (×0,5): aguanta el golpe. En Fácil solo se nota que se queda en el centro.
    /// </summary>
    public class DefenseAIBrain : AIBrain
    {
        /// <summary>Se coloca a esta distancia del centro, del lado del rival (le corta el paso).</summary>
        private const float CenterHold = 1f;
        /// <summary>Segundos tras el ataque del rival en que contraataca.</summary>
        private const float CounterWindow = 0.8f;
        /// <summary>Si el rival no se acerca en estos segundos, sale a por él.</summary>
        private const float Patience = 4f;
        /// <summary>Con esta agresividad o más (va muy por detrás, rival quieto...) deja el centro. Alta: el rival junto a
        /// la pared ya suma agresividad (+0,2) y se trata aparte (CanPin).</summary>
        private const float LeaveCenterAggression = 0.9f;

        private float _counterUntil;
        private bool _targetWasAttacking;
        private float _lastCloseTime;

        public override BladeArchetype Profile => BladeArchetype.Defense;

        protected override float ParryScale => 1.1f;
        protected override float DodgeScale => 0.5f;
        protected override float ChargedChanceScale => 1.2f;
        protected override float AggressionBias => -0.1f;

        protected override void OnEnable()
        {
            base.OnEnable();
            _lastCloseTime = Time.time;
        }

        protected override void Update()
        {
            base.Update();
            AIPerception p = Perception;
            // El rival acaba de soltar su ataque (ha gastado carga y no puede hacer parry enseguida): contraataque
            if (_targetWasAttacking && !p.TargetAttacking) _counterUntil = Time.time + CounterWindow;
            _targetWasAttacking = p.TargetAttacking;
            if (p.Distance < AttackRange * 1.5f) _lastCloseTime = Time.time;
        }

        /// <summary>Contraataque: el rival acaba de atacar o está aturdido.</summary>
        private bool Counter => Time.time < _counterUntil || Perception.TargetStaggered;

        /// <summary>
        /// El rival le deja hueco: contraataque o sin cargas (si las lee). Sin cargas solo sube el ataque a tiro, no la
        /// saca del centro: con la recarga lenta (C16) el rival está sin cargas casi siempre.
        /// </summary>
        private bool Opening => Counter || Perception.TargetCharges == 0;

        /// <summary>El rival está junto a la pared y la IA entre él y el centro: un golpe lo empotra.</summary>
        private bool CanPin =>
            Perception.TargetWallDistance < PinWallDistance && Vector3.Dot(Perception.ToTarget, Perception.TargetOutward) > 0.5f;

        protected override float AttackChanceScale()
        {
            if (!FullStyle) return 0.8f;
            if (Opening) return 1.6f;
            return CanPin ? 1.4f : 0.5f;
        }

        /// <summary>
        /// En el centro, un poco hacia el rival, y quieta al llegar. Sale a por él para contraatacar o empotrarlo si
        /// lo tiene cerca, si el rival no se acerca (Patience) o si la capa táctica pide arriesgar mucho.
        /// </summary>
        protected override Vector3 ApproachDirection()
        {
            AIPerception p = Perception;
            bool goForIt = Time.time - _lastCloseTime > Patience ||
                           (FullStyle && (((Counter || CanPin) && p.Distance < AttackRange) || Aggression >= LeaveCenterAggression));
            if (goForIt || p.TargetOutward == Vector3.zero) return base.ApproachDirection();

            Vector3 toGoal = p.ToCenter * p.CenterDistance + p.TargetOutward * CenterHold;
            return Vector3.ClampMagnitude(toGoal, 1f);
        }
    }
}
