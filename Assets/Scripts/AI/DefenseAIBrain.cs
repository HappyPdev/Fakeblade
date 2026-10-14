using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Comportamiento de Defensa (GDD 6.4, D3): domina el centro de la arena y aguanta. Ataca poco salvo cuando el rival
    /// le abre hueco (acaba de atacar, está sin cargas o aturdido) o cuando puede empotrarlo contra la pared; como
    /// está en el centro, sus golpes empujan al rival hacia fuera. Solo deja el centro para contraatacar o empotrar
    /// a un rival cercano, si el rival no se acerca o si va muy por detrás. En Fácil solo se nota que se queda en el
    /// centro. Valores en su perfil (AIProfile, D2): los generales (parry ×1,1, esquiva ×0,5...) y los del centro (center).
    /// </summary>
    public class DefenseAIBrain : AIBrain
    {
        private float _counterUntil;
        private bool _targetWasAttacking;
        private float _lastCloseTime;

        protected override AIStyle DefaultStyle => AIStyle.Defense;

        private AICenterStyle Center => Profile.center;

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
            if (_targetWasAttacking && !p.TargetAttacking) _counterUntil = Time.time + Center.counterWindow;
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

        /// <summary>Sin el perfil entero (Fácil), la general del perfil; si no, según el hueco que deja el rival.</summary>
        protected override float AttackChanceScale()
        {
            if (!FullStyle) return base.AttackChanceScale();
            if (Opening) return Center.openingAttackScale;
            return CanPin ? Center.pinAttackScale : Center.waitingAttackScale;
        }

        /// <summary>
        /// En el centro, un poco hacia el rival, y quieta al llegar. Sale a por él para contraatacar o empotrarlo si
        /// lo tiene cerca, si el rival no se acerca (patience) o si la capa táctica pide arriesgar mucho.
        /// </summary>
        protected override Vector3 ApproachDirection()
        {
            AIPerception p = Perception;
            bool goForIt = Time.time - _lastCloseTime > Center.patience ||
                           (FullStyle && (((Counter || CanPin) && p.Distance < AttackRange) || Aggression >= Center.leaveCenterAggression));
            if (goForIt || p.TargetOutward == Vector3.zero) return base.ApproachDirection();
            return CenterGoal();
        }

        /// <summary>Con Pinchos o Rebote propios aguanta en el centro sin salir (D11): que la ataquen allí.</summary>
        protected override bool HoldsGround => base.HoldsGround || (FullStyle && ActivePower is SpikesAbility);

        protected override Vector3 HoldGroundDirection() =>
            Perception.TargetOutward == Vector3.zero ? base.HoldGroundDirection() : CenterGoal();

        /// <summary>Hacia su sitio: a centerHold m del centro, del lado del rival.</summary>
        private Vector3 CenterGoal()
        {
            AIPerception p = Perception;
            Vector3 toGoal = p.ToCenter * p.CenterDistance + p.TargetOutward * Center.centerHold;
            return Vector3.ClampMagnitude(toGoal, 1f);
        }
    }
}
