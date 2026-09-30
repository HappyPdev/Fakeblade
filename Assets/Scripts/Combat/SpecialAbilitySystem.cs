using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Poder especial (GDD 5). Clase C# pura que posee FakeBladeController.
    ///
    /// - La energía (0-1) se carga al golpear con éxito o con powerups.
    /// - Con la energía llena se puede activar; mientras está activo se vacía
    ///   durante specialDuration y al llegar a 0 termina.
    /// - Los efectos continuos se exponen como multiplicadores que consultan
    ///   el movimiento y el combate. Los instantáneos (onda de choque) los ejecuta el controller.
    /// </summary>
    public sealed class SpecialAbilitySystem
    {
        private float _energy;
        private bool _isActive;

        public SpecialAbilityType Type { get; private set; } = SpecialAbilityType.SpinBoost;

        /// <summary>Energía 0-1 (relleno de la esfera del HUD).</summary>
        public float Energy => _energy;
        public bool IsActive => _isActive;
        public bool IsReady => !_isActive && _energy >= 1f;

        public void Reset(SpecialAbilityType type)
        {
            Type = type == SpecialAbilityType.None ? SpecialAbilityType.SpinBoost : type;
            _energy = 0f;
            _isActive = false;
        }

        public void AddEnergy(float amount)
        {
            if (_isActive || amount <= 0f) return;
            _energy = Mathf.Min(1f, _energy + amount);
        }

        public bool TryActivate()
        {
            if (!IsReady) return false;
            _isActive = true;
            return true;
        }

        /// <summary>Avanza el drenaje. Devuelve true el frame en que el poder termina.</summary>
        public bool Tick(float dt)
        {
            if (!_isActive) return false;

            float duration = Mathf.Max(0.1f, CombatConfig.Active.specialDuration);
            _energy -= dt / duration;
            if (_energy > 0f) return false;

            _energy = 0f;
            _isActive = false;
            return true;
        }

        #region Modifiers
        private bool Is(SpecialAbilityType t) => _isActive && Type == t;

        /// <summary>Multiplicador del daño recibido (Storm Breaker).</summary>
        public float DamageTakenMultiplier =>
            Is(SpecialAbilityType.Shield) ? 1f - CombatConfig.Active.stormBreakerDamageReduction : 1f;

        /// <summary>Multiplicador del empuje recibido (Storm Breaker).</summary>
        public float KnockbackTakenMultiplier =>
            Is(SpecialAbilityType.Shield) ? 1f - CombatConfig.Active.stormBreakerKnockbackResistance : 1f;

        public float MoveSpeedMultiplier =>
            Is(SpecialAbilityType.Shield) ? 1f + CombatConfig.Active.stormBreakerMoveBonus : 1f;

        /// <summary>Storm Breaker: los ataques rápidos cuentan como cargados de nivel 1.</summary>
        public int MinAttackLevel => Is(SpecialAbilityType.Shield) ? 1 : 0;

        public float DashCooldownMultiplier =>
            Is(SpecialAbilityType.Dash) ? CombatConfig.Active.electricDashCooldownMultiplier : 1f;

        public float DashImpulseMultiplier =>
            Is(SpecialAbilityType.Dash) ? CombatConfig.Active.electricDashImpulseMultiplier : 1f;

        public float ChargeSpeedMultiplier =>
            Is(SpecialAbilityType.Dash) ? CombatConfig.Active.electricChargeSpeedMultiplier : 1f;

        /// <summary>Fracción de RPM máximas recuperada por segundo (Spin Boost).</summary>
        public float SpinRegenPctPerSecond =>
            Is(SpecialAbilityType.SpinBoost) ? CombatConfig.Active.spinBoostPctPerSecond : 0f;
        #endregion
    }
}
