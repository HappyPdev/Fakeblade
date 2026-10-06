using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Resolución de choques entre peonzas (GDD 2.5).
    ///
    /// Se comparan las velocidades de aproximación (la componente de la velocidad de cada
    /// peonza dirigida hacia la otra) justo antes del impacto:
    /// - Ambas reciben un daño base proporcional a la velocidad de cierre.
    /// - La más lenta recibe además daño por la diferencia de velocidad y el empuje principal.
    /// - La más rápida solo recibe una fracción del daño base.
    /// - Masa efectiva (con bonus de ataque), ataque, defensa y combo escalan el resultado.
    /// - El dash no tiene bonus propio: su ventaja es la velocidad (cuenta como ataque).
    ///
    /// PARRY: si una peonza está en los primeros instantes de un ataque rápido (ventana de parry)
    /// y la otra viene atacando (ataque o dash), se ignora la regla de velocidad: quien hace el
    /// parry no recibe daño y el atacante solo recibe su fracción del impacto y sale rebotado.
    /// Si ambas están en ventana de parry, se anulan sin daño.
    ///
    /// LANZADA (estado de Rayos): si solo una de las dos está lanzada, pierde la prioridad por
    /// velocidad: recibe el daño como la más lenta, la otra un choque parejo, y todo el daño
    /// cuenta para quien la lanzó.
    ///
    /// Se ejecuta UNA vez por pareja (lo invoca la peonza con menor InstanceID).
    /// </summary>
    public static class CollisionResolver
    {
        private const float MinMassRatio = 0.5f;
        private const float MaxMassRatio = 2f;

        /// <summary>Parry: (quien hace el parry, atacante, doble parry). Lo escucha el HUD.</summary>
        public static event System.Action<FakeBladeController, FakeBladeController, bool> OnParry;

        public static void Resolve(FakeBladeController a, FakeBladeController b, Vector3 contactPoint)
        {
            var cfg = CombatConfig.Active;

            Vector3 toB = b.Position - a.Position;
            toB.y = 0f;
            if (toB.sqrMagnitude < 0.0001f) return;
            toB.Normalize();

            Vector3 va = a.PreImpactVelocity;
            Vector3 vb = b.PreImpactVelocity;
            float approachA = Mathf.Max(0f, va.x * toB.x + va.z * toB.z);
            float approachB = Mathf.Max(0f, -(vb.x * toB.x + vb.z * toB.z));
            float closing = approachA + approachB;

            if (closing < cfg.minCollisionSpeed) return;

            float intensity = Mathf.Clamp01(closing / 15f);

            // Aliados sin fuego amigo: choque físico sin daño
            if (a.IsAllyOf(b) && !FriendlyFireEnabled())
            {
                a.PlayClashFeedback(contactPoint, intensity * 0.3f);
                return;
            }

            // Parry: tiene prioridad sobre la regla de la más rápida
            bool aParries = a.IsInParryWindow && b.IsAttacking;
            bool bParries = b.IsInParryWindow && a.IsAttacking;
            if (aParries || bParries)
            {
                ResolveParry(a, b, toB, closing, aParries, bParries, contactPoint);
                return;
            }

            // Peonza lanzada (Rayos): pierde la prioridad por velocidad
            bool aLaunched = a.Status.IsLaunched;
            if (aLaunched != b.Status.IsLaunched)
            {
                if (aLaunched) ResolveLaunched(a, b, toB, approachA, approachB, closing, contactPoint);
                else ResolveLaunched(b, a, -toB, approachB, approachA, closing, contactPoint);
                return;
            }

            bool aIsFaster = approachA >= approachB;
            FakeBladeController fast = aIsFaster ? a : b;
            FakeBladeController slow = aIsFaster ? b : a;
            Vector3 fastToSlow = aIsFaster ? toB : -toB;

            // Bonus de un poder activo (Rayos): más daño y empuje sobre el rival, según su velocidad hacia él
            ClashBonus fastBonus = fast.Special.GetClashBonus(slow, aIsFaster ? approachA : approachB);
            ClashBonus slowBonus = slow.Special.GetClashBonus(fast, aIsFaster ? approachB : approachA);

            float diff = Mathf.Abs(approachA - approachB);
            bool neutral = diff <= cfg.equalSpeedTolerance;

            float baseDamage = closing * cfg.damagePerImpactSpeed;
            float ratioFastOverSlow = MassRatio(fast, slow);
            float ratioSlowOverFast = 1f / ratioFastOverSlow;

            float damageToSlow;
            float damageToFast;
            if (neutral)
            {
                // Choque parejo: ambos pierden lo mismo en base a masas y ataque
                damageToSlow = baseDamage * fast.OffenseMultiplier * ratioFastOverSlow;
                damageToFast = baseDamage * slow.OffenseMultiplier * ratioSlowOverFast;
            }
            else
            {
                damageToSlow = (baseDamage + diff * cfg.damagePerSpeedDiff) * fast.OffenseMultiplier * ratioFastOverSlow;
                damageToFast = baseDamage * cfg.fasterDamageFraction * slow.OffenseMultiplier * ratioSlowOverFast;
            }

            float dealtToSlow = slow.ApplyDamage(damageToSlow * fastBonus.DamageMultiplier, fast);
            float dealtToFast = fast.ApplyDamage(damageToFast * slowBonus.DamageMultiplier, slow);

            // Empuje: velocidad instantánea (m/s), escalada por masas, cargas y resistencias
            float knockback = (cfg.knockbackBase + diff * cfg.knockbackPerSpeedDiff)
                              * fast.OutgoingKnockbackMultiplier
                              * ratioFastOverSlow;
            knockback = Mathf.Min(knockback, cfg.maxKnockback);

            float slowKnockback = knockback * slow.KnockbackResistance * fastBonus.KnockbackMultiplier;
            float fastKnockback = knockback * (neutral ? 1f : cfg.winnerKnockbackFraction) * fast.KnockbackResistance
                                  * slowBonus.KnockbackMultiplier;

            slow.ApplyKnockback(fastToSlow * slowKnockback);
            fast.ApplyKnockback(-fastToSlow * fastKnockback);

            fast.Special.NotifyClashBonusApplied(slow, fastBonus, contactPoint);
            slow.Special.NotifyClashBonusApplied(fast, slowBonus, contactPoint);

            // Energía del especial y combos: premia al que gana el choque
            if (!neutral)
            {
                fast.RegisterSuccessfulHit(dealtToSlow);
                // Dash acertado contra un enemigo: recupera parte de su coste
                if (!fast.IsAllyOf(slow)) fast.RegisterClashWon();
            }
            else
            {
                fast.RegisterSuccessfulHit(dealtToSlow * 0.5f);
                slow.RegisterSuccessfulHit(dealtToFast * 0.5f);
            }

            a.PlayClashFeedback(contactPoint, intensity);
            fast.NotifyClash(slow, dealtToFast);
            slow.NotifyClash(fast, dealtToSlow);
        }

        private static void ResolveParry(FakeBladeController a, FakeBladeController b, Vector3 toB, float closing,
            bool aParries, bool bParries, Vector3 contactPoint)
        {
            var cfg = CombatConfig.Active;

            // Doble parry: se anulan, sin daño ni recompensas; ambos rebotan y cortan su ataque
            if (aParries && bParries)
            {
                float bounce = cfg.parryKnockback * 0.6f;
                a.ParryRebound(-toB * bounce * a.KnockbackResistance, true, cfg.parryStagger);
                b.ParryRebound(toB * bounce * b.KnockbackResistance, true, cfg.parryStagger);
                a.PlayParryFeedback(contactPoint);
                a.NotifyClash(b, 0f);
                b.NotifyClash(a, 0f);
                OnParry?.Invoke(a, b, true);
                return;
            }

            FakeBladeController parrier = aParries ? a : b;
            FakeBladeController attacker = aParries ? b : a;
            Vector3 parrierToAttacker = aParries ? toB : -toB;

            // El atacante se sigue llevando su fracción del impacto (la misma que recibe
            // normalmente la peonza más rápida); quien hace el parry no recibe nada.
            float baseDamage = closing * cfg.damagePerImpactSpeed;
            float damage = baseDamage * cfg.fasterDamageFraction * parrier.OffenseMultiplier * MassRatio(parrier, attacker);
            float dealt = attacker.ApplyDamage(damage, parrier);

            // Rebote: el atacante sale despedido y su ataque/dash queda cortado (no puede volver
            // a golpear con él); quien hace el parry frena su embestida y casi no se mueve.
            attacker.ParryRebound(parrierToAttacker * cfg.parryKnockback * attacker.KnockbackResistance, true, cfg.parryStagger);
            parrier.ParryRebound(-parrierToAttacker * cfg.parryKnockback * cfg.parryDefenderKnockbackFraction, false);

            parrier.RegisterParry();
            parrier.PlayParryFeedback(contactPoint);
            parrier.NotifyClash(attacker, 0f);
            attacker.NotifyClash(parrier, dealt);
            OnParry?.Invoke(parrier, attacker, false);
        }

        /// <summary>
        /// Choque de una peonza lanzada por Rayos contra otra (GDD 5, estado Lanzada): la lanzada
        /// recibe el daño como si fuera la más lenta, la otra un choque parejo, y las dos salen
        /// empujadas como en un choque parejo. Todo el daño cuenta para quien la lanzó (efecto bolos).
        /// </summary>
        private static void ResolveLaunched(FakeBladeController launched, FakeBladeController other,
            Vector3 launchedToOther, float approachLaunched, float approachOther, float closing, Vector3 contactPoint)
        {
            var cfg = CombatConfig.Active;
            FakeBladeController thrower = launched.Status.Source;

            float diff = Mathf.Abs(approachLaunched - approachOther);
            float baseDamage = closing * cfg.damagePerImpactSpeed;
            float ratioOtherOverLaunched = MassRatio(other, launched);

            float damageToLaunched = (baseDamage + diff * cfg.damagePerSpeedDiff) * other.OffenseMultiplier * ratioOtherOverLaunched;
            float damageToOther = baseDamage * launched.OffenseMultiplier / ratioOtherOverLaunched;

            // Si la otra es quien la lanzó, su daño no cuenta como golpe propio
            FakeBladeController launchedSource = thrower != null ? thrower : other;
            FakeBladeController otherSource = thrower != null && thrower != other ? thrower : launched;
            float dealtToLaunched = launched.ApplyDamage(damageToLaunched, launchedSource);
            float dealtToOther = other.ApplyDamage(damageToOther, otherSource);

            float knockback = Mathf.Min(cfg.knockbackBase + diff * cfg.knockbackPerSpeedDiff, cfg.maxKnockback);
            launched.ApplyKnockback(-launchedToOther * knockback * launched.KnockbackResistance);
            other.ApplyKnockback(launchedToOther * knockback * other.KnockbackResistance);

            other.PlayClashFeedback(contactPoint, Mathf.Clamp01(closing / 15f));
            launched.NotifyClash(other, dealtToLaunched);
            other.NotifyClash(launched, dealtToOther);
        }

        private static float MassRatio(FakeBladeController a, FakeBladeController b)
        {
            float ratio = a.EffectiveMass / Mathf.Max(0.01f, b.EffectiveMass);
            return Mathf.Clamp(ratio, MinMassRatio, MaxMassRatio);
        }

        private static bool FriendlyFireEnabled()
        {
            var gm = GameManager.Instance;
            return gm == null || gm.Rules.friendlyFire;
        }
    }
}
