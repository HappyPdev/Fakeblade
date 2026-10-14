using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Comportamiento de Ataque (GDD 6.4, D3): presión constante. Se queda más cerca, ataca más y prefiere los cargados,
    /// no se guarda carga para el parry, coge carrerilla siempre que tiene sitio detrás y usa el especial en cuanto
    /// lo tiene. En Fácil solo se nota que se acerca, ataca y carga más. Valores en su perfil (AIProfile, D2):
    /// los generales (parry ×0,7, esquiva ×0,8, distancia 2,2 m...) y los de presión (pressure).
    /// </summary>
    public class AttackAIBrain : AIBrain
    {
        protected override AIStyle DefaultStyle => AIStyle.Attack;

        /// <summary>Busca los cargados: espera a juntar cargas también sin capa táctica (Normal).</summary>
        protected override bool WaitsForCharged => FullStyle;

        /// <summary>Gasta las cargas en atacar en vez de guardarlas para el parry.</summary>
        protected override int ReservedCharges() => FullStyle ? 0 : base.ReservedCharges();

        /// <summary>Con sitio detrás, el cargado se prepara con carrerilla aunque esté cerca.</summary>
        protected override bool RoomToCharge() => base.RoomToCharge() || (FullStyle && HasRoomBehind());

        /// <summary>Carrerilla para los cargados siempre que haya sitio, y a veces para los rápidos.</summary>
        protected override bool WantsRunUp(int level) =>
            base.WantsRunUp(level) ||
            (FullStyle && Perception.Distance < RunUpDistance && HasRoomBehind() && (level > 0 || Random.value < Profile.pressure.quickRunUpChance));

        protected override void TryUseSpecial()
        {
            // En cuanto lo tiene, si el rival está donde el poder sirve (Onda de choque: dentro de la onda)
            if (FullStyle && Blade.Special.IsReady && InSpecialRange) PressSpecial();
            else base.TryUseSpecial();
        }

        /// <summary>Como la Balanceada, pero pegada al rival: solo se separa por debajo de su distancia (2,2 m, más corta).</summary>
        protected override Vector3 ApproachDirection()
        {
            AIPerception p = Perception;
            Vector3 perpendicular = Perpendicular();
            if (p.Distance < KeepDistance && Level.keepsDistance)
                return (perpendicular * 0.8f - p.ToTarget * 0.2f).normalized;
            if (p.Distance < AttackRange)
                return (perpendicular * 0.4f + p.ToTarget * 0.6f).normalized;
            return p.ToTarget;
        }
    }
}
