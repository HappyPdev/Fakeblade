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
    /// - Ataque, defensa y combo escalan el daño. La masa efectiva (peso + bonus de masa del ataque)
    ///   solo cuenta para el empuje, no para el daño (así es más fácil equilibrar las piezas).
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
        /// <summary>Parry: (quien hace el parry, atacante, doble parry). Lo escucha el HUD.</summary>
        public static event System.Action<FakeBladeController, FakeBladeController, bool> OnParry;

        /// <summary>Stuck: dos peonzas pegadas sin velocidad que se separan.</summary>
        public enum ClashKind { Normal, Even, Parry, DoubleParry, Launched, Stuck }

        /// <summary>Todo lo de un choque resuelto, para la grabación de datos del sandbox.</summary>
        public struct ClashReport
        {
            public ClashKind Kind;
            public FakeBladeController A, B;
            /// <summary>Velocidad de cada una hacia la otra justo antes del choque (m/s).</summary>
            public float ApproachA, ApproachB;
            /// <summary>RPM que ha perdido cada una.</summary>
            public float DamageToA, DamageToB;
            /// <summary>Empuje que ha recibido cada una (m/s).</summary>
            public float KnockbackToA, KnockbackToB;
            public int ChargeA, ChargeB;
            public bool DashA, DashB;
        }

        /// <summary>Choque resuelto (normal, parejo, parry o con una peonza lanzada).</summary>
        public static event System.Action<ClashReport> OnClashResolved;

        private static void Report(ClashKind kind, FakeBladeController a, FakeBladeController b,
            float approachA, float approachB, float damageToA, float damageToB, float knockbackToA, float knockbackToB,
            int chargeA, int chargeB, bool dashA, bool dashB)
        {
            OnClashResolved?.Invoke(new ClashReport
            {
                Kind = kind, A = a, B = b, ApproachA = approachA, ApproachB = approachB,
                DamageToA = damageToA, DamageToB = damageToB, KnockbackToA = knockbackToA, KnockbackToB = knockbackToB,
                ChargeA = chargeA, ChargeB = chargeB, DashA = dashA, DashB = dashB
            });
        }

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
                ResolveParry(a, b, toB, approachA, approachB, aParries, bParries, contactPoint);
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

            // Daño: sin la velocidad extra de la carga (el cargado hace el daño de uno rápido × su
            // multiplicador de nivel). Quién gana el choque y el empuje sí usan la velocidad real.
            float fastDamageSpeed = Mathf.Max(0f, (aIsFaster ? approachA : approachB) - fast.ChargeExtraSpeed);
            float slowDamageSpeed = Mathf.Max(0f, (aIsFaster ? approachB : approachA) - slow.ChargeExtraSpeed);
            float damageDiff = Mathf.Max(0f, fastDamageSpeed - slowDamageSpeed);

            float baseDamage = (fastDamageSpeed + slowDamageSpeed) * cfg.damagePerImpactSpeed;

            float damageToSlow;
            float damageToFast;
            if (neutral)
            {
                // Choque parejo: ambos pierden lo mismo en base al ataque
                damageToSlow = baseDamage * fast.OffenseMultiplier;
                damageToFast = baseDamage * slow.OffenseMultiplier;
            }
            else
            {
                damageToSlow = (baseDamage + damageDiff * cfg.damagePerSpeedDiff) * fast.OffenseMultiplier;
                damageToFast = baseDamage * cfg.fasterDamageFraction * slow.OffenseMultiplier;
            }
            // Nivel de carga y tipo de golpe (ataque, dash o sin atacar)
            damageToSlow *= fast.ChargeDamageMultiplier * fast.HitTypeDamageMultiplier;
            damageToFast *= slow.ChargeDamageMultiplier * slow.HitTypeDamageMultiplier;

            float dealtToSlow = slow.ApplyDamage(damageToSlow * fastBonus.DamageMultiplier, fast);
            float dealtToFast = fast.ApplyDamage(damageToFast * slowBonus.DamageMultiplier, slow);
            CutHealIfAttacked(slow, fast, dealtToSlow);
            CutHealIfAttacked(fast, slow, dealtToFast);

            // Empuje: velocidad instantánea (m/s), escalada por masas, cargas y resistencias
            float knockback = (cfg.knockbackBase + diff * cfg.knockbackPerSpeedDiff)
                              * fast.OutgoingKnockbackMultiplier
                              * KnockbackMassRatio(fast, slow);
            knockback = Mathf.Min(knockback, cfg.maxKnockback);

            float slowKnockback = knockback * slow.KnockbackResistance * fastBonus.KnockbackMultiplier;
            float fastKnockback = knockback * (neutral ? 1f : cfg.winnerKnockbackFraction) * fast.KnockbackResistance
                                  * slowBonus.KnockbackMultiplier;

            slow.ApplyKnockback(fastToSlow * slowKnockback);
            fast.ApplyKnockback(-fastToSlow * fastKnockback);

            fast.Special.NotifyClashBonusApplied(slow, fastBonus, contactPoint);
            slow.Special.NotifyClashBonusApplied(fast, slowBonus, contactPoint);

            // Energía del especial (por tipo de golpe) y combos: premia al que gana el choque.
            // Golpear a un aliado (fuego amigo) no da energía
            bool allies = fast.IsAllyOf(slow);
            if (!neutral)
            {
                if (!allies)
                {
                    fast.RegisterSuccessfulHit(false);
                    // Dash acertado contra un enemigo: recupera parte de su coste
                    fast.RegisterClashWon();
                }
            }
            else if (!allies)
            {
                fast.RegisterSuccessfulHit(true);
                slow.RegisterSuccessfulHit(true);
            }

            a.PlayClashFeedback(contactPoint, intensity);
            fast.NotifyClash(slow, dealtToFast);
            slow.NotifyClash(fast, dealtToSlow);

            Report(neutral ? ClashKind.Even : ClashKind.Normal, a, b, approachA, approachB,
                aIsFaster ? dealtToFast : dealtToSlow, aIsFaster ? dealtToSlow : dealtToFast,
                aIsFaster ? fastKnockback : slowKnockback, aIsFaster ? slowKnockback : fastKnockback,
                a.ChargeLevel, b.ChargeLevel, a.IsDashAttacking, b.IsDashAttacking);
        }

        private static void ResolveParry(FakeBladeController a, FakeBladeController b, Vector3 toB, float approachA,
            float approachB, bool aParries, bool bParries, Vector3 contactPoint)
        {
            var cfg = CombatConfig.Active;
            float closing = approachA + approachB;

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
                Report(ClashKind.DoubleParry, a, b, approachA, approachB, 0f, 0f, bounce * a.KnockbackResistance,
                    bounce * b.KnockbackResistance, 0, 0, false, false);
                return;
            }

            FakeBladeController parrier = aParries ? a : b;
            FakeBladeController attacker = aParries ? b : a;
            Vector3 parrierToAttacker = aParries ? toB : -toB;

            // El atacante se sigue llevando su fracción del impacto (la misma que recibe
            // normalmente la peonza más rápida); quien hace el parry no recibe nada.
            float baseDamage = closing * cfg.damagePerImpactSpeed;
            float damage = baseDamage * cfg.fasterDamageFraction * parrier.OffenseMultiplier;
            float dealt = attacker.ApplyDamage(damage, parrier);
            CutHealIfAttacked(attacker, parrier, dealt);

            int attackerCharge = attacker.ChargeLevel; // el rebote corta el ataque
            bool attackerDash = attacker.IsDashAttacking;

            // Rebote: el atacante sale despedido y su ataque/dash queda cortado (no puede volver
            // a golpear con él); quien hace el parry frena su embestida y casi no se mueve.
            attacker.ParryRebound(parrierToAttacker * cfg.parryKnockback * attacker.KnockbackResistance, true, cfg.parryStagger);
            parrier.ParryRebound(-parrierToAttacker * cfg.parryKnockback * cfg.parryDefenderKnockbackFraction, false);

            parrier.RegisterParry();
            parrier.PlayParryFeedback(contactPoint);
            parrier.NotifyClash(attacker, 0f);
            attacker.NotifyClash(parrier, dealt);
            OnParry?.Invoke(parrier, attacker, false);
            Report(ClashKind.Parry, parrier, attacker, aParries ? approachA : approachB, aParries ? approachB : approachA,
                0f, dealt, cfg.parryKnockback * cfg.parryDefenderKnockbackFraction, cfg.parryKnockback * attacker.KnockbackResistance,
                0, attackerCharge, false, attackerDash);
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

            float damageToLaunched = (baseDamage + diff * cfg.damagePerSpeedDiff) * other.OffenseMultiplier;
            float damageToOther = baseDamage * launched.OffenseMultiplier;

            // Si la otra es quien la lanzó, su daño no cuenta como golpe propio
            FakeBladeController launchedSource = thrower != null ? thrower : other;
            FakeBladeController otherSource = thrower != null && thrower != other ? thrower : launched;
            float dealtToLaunched = launched.ApplyDamage(damageToLaunched, launchedSource);
            float dealtToOther = other.ApplyDamage(damageToOther, otherSource);
            CutHealIfAttacked(launched, other, dealtToLaunched);
            CutHealIfAttacked(other, launched, dealtToOther);

            float knockback = Mathf.Min(cfg.knockbackBase + diff * cfg.knockbackPerSpeedDiff, cfg.maxKnockback);
            launched.ApplyKnockback(-launchedToOther * knockback * launched.KnockbackResistance);
            other.ApplyKnockback(launchedToOther * knockback * other.KnockbackResistance);

            other.PlayClashFeedback(contactPoint, Mathf.Clamp01(closing / 15f));
            launched.NotifyClash(other, dealtToLaunched);
            other.NotifyClash(launched, dealtToOther);
            Report(ClashKind.Launched, launched, other, approachLaunched, approachOther, dealtToLaunched, dealtToOther,
                knockback * launched.KnockbackResistance, knockback * other.KnockbackResistance,
                launched.ChargeLevel, other.ChargeLevel, launched.IsDashAttacking, other.IsDashAttacking);
        }

        /// <summary>
        /// Dos peonzas que siguen pegadas tras un choque (sin velocidad no hay choque nuevo): se
        /// separan con un empuje fijo, la más pesada empuja más, y reciben el daño de un choque
        /// parejo a stuckImpactSpeed. No da energía ni corta curaciones (nadie está atacando).
        /// </summary>
        public static void ResolveStuck(FakeBladeController a, FakeBladeController b, Vector3 contactPoint)
        {
            var cfg = CombatConfig.Active;

            Vector3 toB = b.Position - a.Position;
            toB.y = 0f;
            toB = toB.sqrMagnitude > 0.0001f ? toB.normalized : Vector3.right;

            float baseDamage = cfg.stuckImpactSpeed * cfg.damagePerImpactSpeed;
            float dealtToB = b.ApplyDamage(baseDamage * a.OffenseMultiplier, a);
            float dealtToA = a.ApplyDamage(baseDamage * b.OffenseMultiplier, b);

            // Las resistencias (Defensa) frenan el empuje, pero no tanto como para seguir pegadas
            float ratioAOverB = KnockbackMassRatio(a, b);
            float knockToA = cfg.stuckRepelSpeed / ratioAOverB * Mathf.Max(0.5f, a.KnockbackResistance);
            float knockToB = cfg.stuckRepelSpeed * ratioAOverB * Mathf.Max(0.5f, b.KnockbackResistance);
            a.ApplyKnockback(-toB * knockToA);
            b.ApplyKnockback(toB * knockToB);

            a.PlayClashFeedback(contactPoint, 0.25f);
            a.NotifyClash(b, dealtToA);
            b.NotifyClash(a, dealtToB);
            Report(ClashKind.Stuck, a, b, 0f, 0f, dealtToA, dealtToB, knockToA, knockToB, 0, 0, false, false);
        }

        /// <summary>
        /// Golpe de ataque enemigo (rápido, cargado o dash) que quita RPM: corta la curación del
        /// poder de quien lo recibe. Paredes, roce, estados y poderes no la cortan.
        /// </summary>
        private static void CutHealIfAttacked(FakeBladeController victim, FakeBladeController hitter, float dealt)
        {
            if (dealt > 0f && hitter.IsAttacking && !victim.IsAllyOf(hitter)) victim.InterruptHeal();
        }

        /// <summary>Relación de masas para el empuje (con el bonus de masa del ataque en curso). La masa no
        /// cuenta en el daño. Límites en CombatConfig.massRatioRange: el peso cuenta, pero no lo decide todo.</summary>
        private static float KnockbackMassRatio(FakeBladeController a, FakeBladeController b)
        {
            Vector2 range = CombatConfig.Active.massRatioRange;
            return Mathf.Clamp(a.EffectiveMass / Mathf.Max(0.01f, b.EffectiveMass), range.x, range.y);
        }

        private static bool FriendlyFireEnabled()
        {
            var gm = GameManager.Instance;
            return gm == null || gm.Rules.friendlyFire;
        }
    }
}
