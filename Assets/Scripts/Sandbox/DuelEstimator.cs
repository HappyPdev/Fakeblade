using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Estimación de un duelo entre dos peonzas para el banco de equilibrio (C14, GDD 2.7): las dos a la vez,
    /// con el daño medio de sus golpes (medido por el banco), lo que cuestan los ataques y los dashes, el
    /// desgaste y la curación del especial. No cuenta los efectos propios de cada poder (salvo la curación
    /// de Spin Boost), las paredes ni el parry. Es un cálculo puro: lo usan el banco y los tests.
    /// </summary>
    public static class DuelEstimator
    {
        // Ritmo de juego medido en los registros del sandbox (2026-10-07 a 2026-10-10, jugador contra dummy):
        // por cada golpe que entra, 0,6-0,9 ataques (nivel medio ~1) y 0,05-0,2 dashes; 7 de 30 curaciones cortadas.
        public const float AttacksPerHit = 0.7f;
        public const float DashesPerHit = 0.1f;
        public const float PassiveHitsPerHit = 0.2f;
        /// <summary>Mitad rápidos y mitad cargados de nivel 2 (nivel medio 1): cargas gastadas por ataque.</summary>
        public const float ChargesPerAttack = 1.5f;
        public const float HealCutChance = 0.25f;
        /// <summary>Dashes que aciertan: devuelven dashHitRefundFraction de su coste.</summary>
        public const float DashLandShare = 0.5f;

        private const float Step = 0.05f;
        private const float MaxSeconds = 900f;

        /// <summary>Lo que hace falta de cada peonza para estimar el duelo.</summary>
        public struct Side
        {
            public float MaxSpin;
            /// <summary>RPM que pierde sola por segundo.</summary>
            public float Decay;
            /// <summary>Daño medio de un golpe suyo que entra (al rival).</summary>
            public float HitDamage;
            /// <summary>Daño medio que recibe ella misma al golpear (choque).</summary>
            public float SelfDamagePerHit;
            /// <summary>RPM que gasta en ataques y dashes por cada golpe suyo que entra.</summary>
            public float CostPerHit;
            /// <summary>Barra del especial (0-1) que gana por cada golpe suyo que entra.</summary>
            public float EnergyPerHit;
            /// <summary>Segundos que dura su especial (sin ganar energía mientras tanto).</summary>
            public float SpecialDuration;
            /// <summary>RPM que recupera cada vez que activa el especial (curación común + la del poder).</summary>
            public float HealPerSpecial;
        }

        public struct Result
        {
            public float Seconds;
            /// <summary>0 = gana A, 1 = gana B, −1 = empate o sin K.O. en el tiempo máximo.</summary>
            public int Winner;
            /// <summary>Fracción de RPM que le queda al ganador.</summary>
            public float WinnerSpinLeft;
            public int SpecialsA, SpecialsB;
        }

        /// <summary>
        /// Monta una peonza para el duelo a partir de sus estadísticas y los daños medidos. Los costes y la
        /// energía siguen las mismas reglas que FakeBladeController (TryAttack, TryDash, RegisterSuccessfulHit).
        /// </summary>
        public static Side BuildSide(CombatConfig cfg, BladeStatBlock s, SpecialAbilityData special, float hitDamage, float selfDamagePerHit)
        {
            PartTraits t = s.Traits ?? PartTraits.None;
            float attackCost = s.MaxSpin * cfg.quickAttackSpinCostPct * ChargesPerAttack * t.Multiplier(PartTraitType.AttackCost);
            float dashCost = s.MaxSpin * cfg.dashSpinCostPct * t.Multiplier(PartTraitType.DashCost) *
                             (1f - DashLandShare * cfg.dashHitRefundFraction);

            // Energía por golpe: los ataques, mitad rápidos y mitad cargados de nivel 2
            float attackEnergy = 0.5f * cfg.specialEnergyQuickHit * t.Multiplier(PartTraitType.SpecialEnergyQuickHit) +
                                 0.5f * (cfg.specialEnergyChargedHit + cfg.specialEnergyPerChargeLevel * 2f) * t.Multiplier(PartTraitType.SpecialEnergyChargedHit);
            float energy = AttacksPerHit * attackEnergy +
                           DashesPerHit * cfg.specialEnergyDashHit * t.Multiplier(PartTraitType.SpecialEnergyDashHit) +
                           PassiveHitsPerHit * cfg.specialEnergyPassiveHit;
            float required = special != null ? Mathf.Max(0.1f, special.energyRequired) : 1f;
            energy *= cfg.specialEnergyMultiplier * t.Multiplier(PartTraitType.SpecialEnergyAll) / required;

            float duration = (special != null ? special.duration : 5f) * t.Multiplier(PartTraitType.SpecialDuration);
            float heal = s.MaxSpin * cfg.specialActivationSpinPct;
            if (special is SpinBoostData spinBoost) heal += s.MaxSpin * spinBoost.regenPctPerSecond * duration;
            heal *= t.Multiplier(PartTraitType.HealAmount) * (1f - HealCutChance);

            return new Side
            {
                MaxSpin = s.MaxSpin,
                Decay = s.SpinDecay,
                HitDamage = hitDamage,
                SelfDamagePerHit = selfDamagePerHit,
                CostPerHit = AttacksPerHit * attackCost + DashesPerHit * dashCost,
                EnergyPerHit = energy,
                SpecialDuration = duration,
                HealPerSpecial = heal
            };
        }

        /// <summary>Simula el duelo: cada una mete un golpe cada secondsPerHit, a la vez.</summary>
        public static Result Simulate(Side a, Side b, float secondsPerHit)
        {
            float spinA = a.MaxSpin, spinB = b.MaxSpin;
            float energyA = 0f, energyB = 0f, activeA = 0f, activeB = 0f;
            float nextHit = secondsPerHit;
            var r = new Result { Winner = -1, Seconds = MaxSeconds };

            for (float t = Step; t <= MaxSeconds; t += Step)
            {
                spinA -= a.Decay * Step;
                spinB -= b.Decay * Step;
                activeA = Mathf.Max(0f, activeA - Step);
                activeB = Mathf.Max(0f, activeB - Step);

                if (t >= nextHit)
                {
                    nextHit += secondsPerHit;
                    spinA -= b.HitDamage + a.SelfDamagePerHit + a.CostPerHit;
                    spinB -= a.HitDamage + b.SelfDamagePerHit + b.CostPerHit;
                    if (activeA <= 0f) energyA += a.EnergyPerHit;
                    if (activeB <= 0f) energyB += b.EnergyPerHit;
                }

                if (energyA >= 1f && spinA > 0f) { energyA = 0f; activeA = a.SpecialDuration; spinA = Mathf.Min(a.MaxSpin, spinA + a.HealPerSpecial); r.SpecialsA++; }
                if (energyB >= 1f && spinB > 0f) { energyB = 0f; activeB = b.SpecialDuration; spinB = Mathf.Min(b.MaxSpin, spinB + b.HealPerSpecial); r.SpecialsB++; }

                bool deadA = spinA <= 0f, deadB = spinB <= 0f;
                if (!deadA && !deadB) continue;

                r.Seconds = t;
                if (deadA && deadB) r.Winner = -1;
                else if (deadB) { r.Winner = 0; r.WinnerSpinLeft = spinA / a.MaxSpin; }
                else { r.Winner = 1; r.WinnerSpinLeft = spinB / b.MaxSpin; }
                return r;
            }
            return r;
        }
    }
}
