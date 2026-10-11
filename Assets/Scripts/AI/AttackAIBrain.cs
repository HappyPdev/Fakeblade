using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Perfil de Ataque (GDD 6.4, D3): presión constante. Se queda más cerca, ataca más y prefiere los cargados,
    /// no se guarda carga para el parry, coge carrerilla siempre que tiene sitio detrás y usa el especial en cuanto
    /// lo tiene. Para menos (parry ×0,7) y esquiva menos (×0,8). En Fácil solo se nota que se acerca, ataca y carga más.
    /// </summary>
    public class AttackAIBrain : AIBrain
    {
        /// <summary>Distancia que guarda sin atacar (la Balanceada, KeepDistance = 2,8 m).</summary>
        private const float PressureDistance = 2.2f;
        /// <summary>Probabilidad de coger carrerilla también para un ataque rápido.</summary>
        private const float QuickRunUpChance = 0.4f;

        public override BladeArchetype Profile => BladeArchetype.Attack;

        protected override float ParryScale => 0.7f;
        protected override float DodgeScale => 0.8f;
        protected override float ChargedChanceScale => 1.6f;
        protected override float AggressionBias => 0.15f;
        protected override float AttackChanceScale() => 1.25f;
        /// <summary>Busca los cargados: espera a juntar cargas también sin capa táctica (Normal).</summary>
        protected override bool WaitsForCharged => FullStyle;

        /// <summary>Gasta las cargas en atacar en vez de guardarlas para el parry.</summary>
        protected override int ReservedCharges() => FullStyle ? 0 : base.ReservedCharges();

        /// <summary>Con sitio detrás, el cargado se prepara con carrerilla aunque esté cerca.</summary>
        protected override bool RoomToCharge() => base.RoomToCharge() || (FullStyle && HasRoomBehind());

        /// <summary>Carrerilla para los cargados siempre que haya sitio, y a veces para los rápidos.</summary>
        protected override bool WantsRunUp(int level) =>
            base.WantsRunUp(level) ||
            (FullStyle && Perception.Distance < RunUpDistance && HasRoomBehind() && (level > 0 || Random.value < QuickRunUpChance));

        protected override void TryUseSpecial()
        {
            if (FullStyle && Blade.Special.IsReady) PressSpecial();
            else base.TryUseSpecial();
        }

        /// <summary>Como la Balanceada, pero pegada al rival: solo se separa por debajo de PressureDistance.</summary>
        protected override Vector3 ApproachDirection()
        {
            AIPerception p = Perception;
            Vector3 perpendicular = Perpendicular();
            if (p.Distance < PressureDistance && Level.keepsDistance)
                return (perpendicular * 0.8f - p.ToTarget * 0.2f).normalized;
            if (p.Distance < AttackRange)
                return (perpendicular * 0.4f + p.ToTarget * 0.6f).normalized;
            return p.ToTarget;
        }
    }
}
