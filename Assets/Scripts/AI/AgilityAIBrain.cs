using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Comportamiento de Agilidad (GDD 6.4, D3): rodea al rival a media distancia, entra con dash o con un rápido y sale
    /// (golpe y fuera), y busca parrys: se guarda una carga para ellos aunque su nivel haga pocos. Casi no carga.
    /// En Fácil solo se nota que rodea y casi no carga. Valores en su perfil (AIProfile, D2): los generales
    /// (parry y esquiva ×1,3, reacción ×0,9...) y los de rodear (orbit).
    /// </summary>
    public class AgilityAIBrain : AIBrain
    {
        private bool _wasAttacking;
        private float _nextFlip;

        protected override AIStyle DefaultStyle => AIStyle.Agility;

        private AIOrbitStyle Orbit => Profile.orbit;

        protected override float ApproachDashChance => FullStyle ? Orbit.approachDashChance : base.ApproachDashChance;

        /// <summary>Busca parrys: se guarda una carga mientras el rival pueda atacar, aunque su nivel haga pocos.</summary>
        protected override int ReservedCharges() =>
            FullStyle && ParryChance > 0f && Perception.TargetCharges != 0 ? 1 : base.ReservedCharges();

        protected override void Think()
        {
            base.Think();
            AIPerception p = Perception;
            if (!FullStyle || p.Target == null) return;

            // Rodeando hacia la pared: cambia de sentido
            if (p.WallDistance < 2f && Time.time >= _nextFlip && Vector3.Dot(Perpendicular(), -p.ToCenter) > 0.5f)
            {
                FlipOrbit();
                _nextFlip = Time.time + Orbit.flipCooldown;
            }
        }

        /// <summary>
        /// Golpe y fuera: al acabar su ataque, si sigue pegada, sale con un dash de lado (hacia el centro).
        /// Va antes de base.Update (con la percepción del frame anterior) para que el dash salga ya con su dirección.
        /// </summary>
        protected override void Update()
        {
            AIPerception p = Perception;
            bool attacking = Blade.Attack.IsAttacking;
            if (FullStyle && _wasAttacking && !attacking && !Busy && !Lapsing && !Blade.IsDestroyed && p.Target != null &&
                p.Distance < KeepDistance && !Soaked && Blade.CanDash && Random.value < Orbit.dashOutChance)
            {
                Vector3 side = Perpendicular();
                if (Vector3.Dot(side, p.ToCenter) < 0f) side = -side;
                DashTowards((side - p.ToTarget * 0.5f).normalized, false);
            }
            _wasAttacking = attacking;
            base.Update();
        }

        /// <summary>Entra con un dash (también golpea) en vez de embestir.</summary>
        protected override void BeginQuickStrike()
        {
            if (FullStyle && Blade.CanDash && Random.value < Orbit.dashInChance) DashTowards(AimDirection(), true);
            else base.BeginQuickStrike();
        }

        /// <summary>Lejos, como la Balanceada; cerca, da vueltas a orbitRadius del rival.</summary>
        protected override Vector3 ApproachDirection()
        {
            AIPerception p = Perception;
            float radius = Orbit.orbitRadius;
            if (p.Distance > radius + 3f) return base.ApproachDirection();
            float radial = Mathf.Clamp((p.Distance - radius) * 0.8f, -0.8f, 0.8f);
            return (Perpendicular() + p.ToTarget * radial).normalized;
        }
    }
}
