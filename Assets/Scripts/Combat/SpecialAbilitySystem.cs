using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Poder especial (GDD 5). Clase C# pura que posee FakeBladeController.
    ///
    /// - La energía (0-1, relleno de la esfera) se carga al golpear con éxito o con powerups.
    ///   Lo ganado se divide por la energía que necesita el poder: los fuertes tardan más.
    /// - Con la esfera llena se puede activar. Efecto común de todos los poderes: recupera RPM y
    ///   rellena las cargas de ataque. Después se vacía durante la duración del poder y termina.
    /// - Lo propio de cada poder vive en su <see cref="SpecialAbility"/> (datos en un asset
    ///   <see cref="SpecialAbilityData"/>); aquí solo se expone a través de los modificadores.
    /// </summary>
    public sealed class SpecialAbilitySystem
    {
        private FakeBladeController _owner;
        private SpecialAbility _ability;
        private float _energy;
        private bool _isActive;

        /// <summary>Datos del poder equipado (null hasta el primer Reset).</summary>
        public SpecialAbilityData Data => _ability?.Data;
        public SpecialAbilityType Type => _ability != null ? _ability.Data.Type : SpecialAbilityType.SpinBoost;
        /// <summary>Color del poder (aura, partículas y esfera del HUD).</summary>
        public Color Color => _ability != null ? _ability.Data.color : Color.white;

        /// <summary>Energía 0-1 (relleno de la esfera del HUD).</summary>
        public float Energy => _energy;
        public bool IsActive => _isActive;
        public bool IsReady => !_isActive && _energy >= 1f;

        /// <summary>Equipa un poder (o lo mantiene) y vacía la energía.</summary>
        public void Reset(FakeBladeController owner, SpecialAbilityType type)
        {
            Stop();
            _owner = owner;

            SpecialAbilityData data = SpecialAbilities.Get(type);
            if (_ability == null || _ability.Data != data)
                _ability = data.CreateRuntime(owner);

            _energy = 0f;
        }

        /// <summary>Energía en unidades de la barra estándar (1 = llenar un poder de energía 1).</summary>
        public void AddEnergy(float amount)
        {
            if (_isActive || amount <= 0f || _ability == null) return;
            _energy = Mathf.Min(1f, _energy + amount / Mathf.Max(0.1f, _ability.Data.energyRequired));
        }

        /// <summary>Llena la esfera al instante (sandbox, trucos y pruebas).</summary>
        public void FillEnergy()
        {
            if (!_isActive) _energy = 1f;
        }

        public bool TryActivate()
        {
            if (!IsReady || _owner == null) return false;
            _isActive = true;

            // Efecto común de todos los poderes (GDD 5)
            _owner.AddSpin(_owner.MaxSpinSpeed * CombatConfig.Active.specialActivationSpinPct);
            _owner.Attack.RefillCharges();

            _ability.OnActivate();
            return true;
        }

        /// <summary>Avanza el poder activo. Devuelve true el frame en que termina.</summary>
        public bool Tick(float dt)
        {
            if (!_isActive) return false;

            _ability.Tick(dt);
            _energy -= dt / Mathf.Max(0.1f, _ability.Data.duration);
            if (_energy > 0f) return false;

            Stop();
            return true;
        }

        /// <summary>Corta el poder activo (K.O. o reinicio). Devuelve true si estaba activo.</summary>
        public bool Stop()
        {
            if (!_isActive) return false;
            _isActive = false;
            _energy = 0f;
            _ability.OnEnd();
            return true;
        }

        /// <summary>Ha quitado RPM a otra peonza en un choque (solo cuenta con el poder activo).</summary>
        public void NotifyClashDamageDealt(FakeBladeController target, float damage)
        {
            if (_isActive && target != null && damage > 0f)
                _ability.OnClashDamageDealt(target, damage);
        }

        #region Modifiers (solo mientras está activo)
        /// <summary>Multiplicador del daño recibido.</summary>
        public float DamageTakenMultiplier => _isActive ? _ability.DamageTakenMultiplier : 1f;
        /// <summary>Multiplicador del empuje recibido.</summary>
        public float KnockbackTakenMultiplier => _isActive ? _ability.KnockbackTakenMultiplier : 1f;
        /// <summary>Multiplicador del desgaste de RPM con el tiempo.</summary>
        public float SpinDecayMultiplier => _isActive ? _ability.SpinDecayMultiplier : 1f;
        public float MoveSpeedMultiplier => _isActive ? _ability.MoveSpeedMultiplier : 1f;
        /// <summary>Nivel mínimo con el que cuentan los ataques.</summary>
        public int MinAttackLevel => _isActive ? _ability.MinAttackLevel : 0;
        /// <summary>Velocidad de recarga de las cargas de ataque.</summary>
        public float AttackRechargeMultiplier => _isActive ? _ability.AttackRechargeMultiplier : 1f;
        public float DashCooldownMultiplier => _isActive ? _ability.DashCooldownMultiplier : 1f;
        public float DashImpulseMultiplier => _isActive ? _ability.DashImpulseMultiplier : 1f;
        /// <summary>Multiplicador del coste en RPM del dash.</summary>
        public float DashCostMultiplier => _isActive ? _ability.DashCostMultiplier : 1f;
        public float ChargeSpeedMultiplier => _isActive ? _ability.ChargeSpeedMultiplier : 1f;
        #endregion
    }
}
