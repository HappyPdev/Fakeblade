using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Sistema de ataque (GDD 2.3). Clase C# pura que posee FakeBladeController.
    ///
    /// - Pulsación corta  → ataque rápido (nivel 0, gasta 1 carga).
    /// - Mantener pulsado → ataque cargado; sube de nivel cada chargeTimePerLevel hasta
    ///   el nº de cargas disponibles. Gasta tantas cargas como niveles. Si se mantiene
    ///   en el máximo durante chargedAutoReleaseTime se lanza solo.
    /// - Las cargas se recuperan de una en una con el tiempo.
    ///
    /// No aplica fuerzas: devuelve peticiones de lanzamiento y el controller las ejecuta.
    /// </summary>
    public sealed class AttackSystem
    {
        public const int MaxSupportedCharges = 6;
        public const int NoLaunch = -1;

        #region State
        private int _maxCharges = 3;
        private int _charges = 3;
        private float _rechargeTimer;

        private bool _wasHeld;
        private bool _pressing;
        private float _holdTime;
        private bool _isCharging;
        private int _chargeLevel;
        private float _levelProgress;
        private float _timeAtMaxLevel;
        private float _chargedCooldown;

        private int _attackLevel;
        private bool _launchedAsQuick;
        // Marcas de tiempo (Time.time): exactas también dentro de los callbacks de física,
        // aunque haya varios pasos de física en un mismo frame
        private float _attackStartTime = float.NegativeInfinity;
        private float _attackEndTime = float.NegativeInfinity;

        private int _comboHits;
        private float _comboTimer;
        #endregion

        #region Read-only State (HUD / combate)
        public int MaxCharges => _maxCharges;
        public int CurrentCharges => _charges;
        /// <summary>Progreso 0-1 de la siguiente carga (0 si están todas llenas).</summary>
        public float RechargeProgress
        {
            get
            {
                if (_charges >= _maxCharges) return 0f;
                float t = CombatConfig.Active.attackRechargeTime;
                return t > 0f ? Mathf.Clamp01(_rechargeTimer / t) : 1f;
            }
        }

        public bool IsCharging => _isCharging;
        /// <summary>Nivel de carga actual (1..cargas) mientras se carga.</summary>
        public int ChargeLevel => _chargeLevel;
        /// <summary>Progreso 0-1 hacia el siguiente nivel de carga.</summary>
        public float ChargeLevelProgress => _levelProgress;

        public bool IsAttacking => Time.time < _attackEndTime;
        public int AttackLevel => _attackLevel;

        /// <summary>
        /// Parry (GDD 2.5): el ataque en curso es un ataque RÁPIDO (pulsación, aunque Storm Breaker
        /// lo haga contar como cargado) y se lanzó hace como mucho <paramref name="window"/> segundos.
        /// </summary>
        public bool IsInParryWindow(float window) => IsAttacking && _launchedAsQuick && Time.time - _attackStartTime <= window;

        /// <summary>Bonus de masa del ataque en curso (0 si no ataca).</summary>
        public float MassBonus
        {
            get
            {
                if (!IsAttacking) return 0f;
                var cfg = CombatConfig.Active;
                return cfg.quickAttackMassBonus + cfg.chargedMassBonusPerLevel * _attackLevel;
            }
        }

        /// <summary>Multiplicador de empuje del ataque en curso.</summary>
        public float KnockbackMultiplier
        {
            get
            {
                if (!IsAttacking) return 1f;
                return 1f + CombatConfig.Active.chargedKnockbackPerLevel * _attackLevel;
            }
        }

        /// <summary>Multiplicador por golpes encadenados.</summary>
        public float ComboMultiplier => 1f + CombatConfig.Active.comboBonusPerHit * _comboHits;
        public int ComboHits => _comboHits;
        #endregion

        public void Reset(int maxCharges)
        {
            _maxCharges = Mathf.Clamp(maxCharges, 1, MaxSupportedCharges);
            _charges = _maxCharges;
            _rechargeTimer = 0f;
            CancelCharge();
            _chargedCooldown = 0f;
            _attackStartTime = float.NegativeInfinity;
            _attackLevel = 0;
            _attackEndTime = float.NegativeInfinity;
            _launchedAsQuick = false;
            _comboHits = 0;
            _comboTimer = 0f;
            _wasHeld = false;
        }

        /// <summary>Cambia el máximo de cargas sin reiniciar la partida (p. ej. al cambiar piezas).</summary>
        public void SetMaxCharges(int maxCharges)
        {
            _maxCharges = Mathf.Clamp(maxCharges, 1, MaxSupportedCharges);
            _charges = Mathf.Min(_charges, _maxCharges);
        }

        public void CancelCharge()
        {
            _pressing = false;
            _isCharging = false;
            _chargeLevel = 0;
            _levelProgress = 0f;
            _holdTime = 0f;
            _timeAtMaxLevel = 0f;
        }

        /// <summary>
        /// Avanza temporizadores y procesa el botón.
        /// Devuelve el nivel a lanzar (0 = rápido, 1+ = cargado) o NoLaunch.
        /// </summary>
        public int Tick(float dt, bool held, float chargeSpeedMultiplier)
        {
            var cfg = CombatConfig.Active;

            TickRecharge(dt, cfg);
            if (_chargedCooldown > 0f) _chargedCooldown -= dt;
            if (_comboTimer > 0f)
            {
                _comboTimer -= dt;
                if (_comboTimer <= 0f) _comboHits = 0;
            }

            int launch = NoLaunch;
            bool pressedNow = held && !_wasHeld;
            bool releasedNow = !held && _wasHeld;
            _wasHeld = held;

            if (pressedNow && _charges > 0)
            {
                _pressing = true;
                _holdTime = 0f;
            }

            if (!_pressing) return NoLaunch;

            if (held)
            {
                _holdTime += dt;
                bool canCharge = _chargedCooldown <= 0f;
                if (canCharge && _holdTime >= cfg.chargeHoldThreshold)
                {
                    _isCharging = true;
                    float chargeTime = (_holdTime - cfg.chargeHoldThreshold) * Mathf.Max(0.01f, chargeSpeedMultiplier);
                    float levelTime = Mathf.Max(0.01f, cfg.chargeTimePerLevel);
                    int maxLevel = Mathf.Max(1, _charges);
                    float rawLevel = 1f + chargeTime / levelTime;

                    _chargeLevel = Mathf.Min(maxLevel, Mathf.FloorToInt(rawLevel));
                    _levelProgress = _chargeLevel >= maxLevel ? 1f : rawLevel - Mathf.Floor(rawLevel);

                    if (_chargeLevel >= maxLevel)
                    {
                        _timeAtMaxLevel += dt;
                        if (_timeAtMaxLevel >= cfg.chargedAutoReleaseTime)
                            launch = _chargeLevel;
                    }
                }
            }
            else if (releasedNow)
            {
                launch = _isCharging ? _chargeLevel : 0;
            }

            if (launch != NoLaunch)
                _pressing = false; // el controller confirma con Commit o cancela

            return launch;
        }

        /// <summary>Coste en cargas de un nivel de ataque.</summary>
        public static int ChargeCost(int level) => Mathf.Max(1, level);

        public bool CanAfford(int level) => _charges >= ChargeCost(level);

        /// <summary>Confirma el lanzamiento: gasta cargas y activa el estado de ataque.</summary>
        public void Commit(int requestedLevel, int effectiveLevel)
        {
            var cfg = CombatConfig.Active;
            int cost = ChargeCost(requestedLevel);
            bool wasFull = _charges >= _maxCharges;
            _charges = Mathf.Max(0, _charges - cost);
            if (wasFull) _rechargeTimer = 0f;

            _attackLevel = Mathf.Max(0, effectiveLevel);
            _attackStartTime = Time.time;
            _attackEndTime = Time.time + cfg.quickAttackDuration * (1f + 0.25f * _attackLevel);
            _launchedAsQuick = requestedLevel == 0;

            if (requestedLevel > 0)
                _chargedCooldown = cfg.chargedAttackCooldown;

            CancelCharge();
        }

        /// <summary>Corta el ataque en curso (p. ej. cuando se lo bloquean con un parry).</summary>
        public void EndAttack()
        {
            if (IsAttacking) _attackEndTime = Time.time;
        }

        /// <summary>Devuelve una carga de ataque (recompensa del parry).</summary>
        public void RefundCharge()
        {
            if (_charges >= _maxCharges) return;
            _charges++;
            if (_charges >= _maxCharges) _rechargeTimer = 0f;
        }

        /// <summary>Llamar cuando un ataque de esta peonza gana un choque.</summary>
        public void RegisterHit()
        {
            var cfg = CombatConfig.Active;
            _comboHits = Mathf.Min(_comboHits + 1, cfg.maxComboHits);
            _comboTimer = cfg.comboWindow;
        }

        private void TickRecharge(float dt, CombatConfig cfg)
        {
            if (_charges >= _maxCharges)
            {
                _rechargeTimer = 0f;
                return;
            }

            _rechargeTimer += dt;
            if (_rechargeTimer >= cfg.attackRechargeTime)
            {
                _rechargeTimer = 0f;
                _charges++;
            }
        }
    }
}
