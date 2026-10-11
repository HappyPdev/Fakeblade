using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Perfil de Agilidad (GDD 6.4, D3): rodea al rival a media distancia, entra con dash o con un rápido y sale
    /// (golpe y fuera), y busca parrys: se guarda una carga para ellos aunque su nivel haga pocos. Casi no carga.
    /// Parry y esquiva ×1,3, reacción ×0,9. En Fácil solo se nota que rodea y casi no carga.
    /// </summary>
    public class AgilityAIBrain : AIBrain
    {
        /// <summary>Distancia entre centros a la que rodea al rival (a tiro, AttackRange = 4 m).</summary>
        private const float OrbitRadius = 3.6f;
        /// <summary>Probabilidad de entrar con un dash en vez de embestir para el rápido.</summary>
        private const float DashInChance = 0.3f;
        /// <summary>Probabilidad de salir con un dash al acabar su ataque, si sigue pegada al rival.</summary>
        private const float DashOutChance = 0.3f;
        /// <summary>Tiempo mínimo entre cambios de sentido al rodear (evita ir y venir junto a la pared).</summary>
        private const float FlipCooldown = 1f;

        private bool _wasAttacking;
        private float _nextFlip;

        public override BladeArchetype Profile => BladeArchetype.Agility;

        protected override float ParryScale => 1.3f;
        protected override float DodgeScale => 1.3f;
        protected override float ReactionScale => 0.9f;
        protected override float ChargedChanceScale => 0.5f;
        protected override float ApproachDashChance => FullStyle ? 0.3f : 0.15f;

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
                _nextFlip = Time.time + FlipCooldown;
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
            if (FullStyle && _wasAttacking && !attacking && !Busy && !Blade.IsDestroyed && p.Target != null &&
                p.Distance < KeepDistance && Blade.CanDash && Random.value < DashOutChance)
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
            if (FullStyle && Blade.CanDash && Random.value < DashInChance) DashTowards(AimDirection(), true);
            else base.BeginQuickStrike();
        }

        /// <summary>Lejos, como la Balanceada; cerca, da vueltas a OrbitRadius del rival.</summary>
        protected override Vector3 ApproachDirection()
        {
            AIPerception p = Perception;
            if (p.Distance > OrbitRadius + 3f) return base.ApproachDirection();
            float radial = Mathf.Clamp((p.Distance - OrbitRadius) * 0.8f, -0.8f, 0.8f);
            return (Perpendicular() + p.ToTarget * radial).normalized;
        }
    }
}
