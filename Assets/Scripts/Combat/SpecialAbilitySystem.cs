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
        // Carga común al activarlo (L9): segundos que faltan; el poder empieza (OnActivate) al acabarla
        private float _windup;
        // Curación del efecto común: se reparte en specialHealTime; un golpe de ataque enemigo la corta
        private float _pendingHeal;
        private float _healRate;
        private bool _healCut;

        /// <summary>Datos del poder equipado (null hasta el primer Reset).</summary>
        public SpecialAbilityData Data => _ability?.Data;
        /// <summary>Comportamiento del poder equipado (para el aspecto: los escudos de Escudo).</summary>
        public SpecialAbility Ability => _ability;
        public SpecialAbilityType Type => _ability != null ? _ability.Data.Type : SpecialAbilityType.SpinBoost;
        /// <summary>Color del poder (aura, partículas y esfera del HUD).</summary>
        public Color Color => _ability != null ? _ability.Data.color : Color.white;

        /// <summary>Energía 0-1 (relleno de la esfera del HUD).</summary>
        public float Energy => _energy;
        public bool IsActive => _isActive;
        /// <summary>
        /// Activo y todavía en la carga corta del principio (los efectos del poder esperan a que acabe): la común (L9) o la
        /// del propio poder (Embestida y Onda de choque, también antes de cada uso).
        /// </summary>
        public bool IsWindingUp => _isActive && _ability != null && (_windup > 0f || _ability.IsWindingUp);
        /// <summary>En la carga común (L9): el poder aún no ha empezado (sin aura ni efecto).</summary>
        public bool IsInCommonWindup => _isActive && _windup > 0f;
        /// <summary>Activo y ya empezado (pasada la carga común): sus modificadores y su efecto cuentan.</summary>
        private bool IsOn => _isActive && _windup <= 0f;
        /// <summary>Lo que lleva de la carga (0-1; 1 si no está cargando).</summary>
        public float WindupProgress => !IsWindingUp ? 1f
            : _windup > 0f ? 1f - Mathf.Clamp01(_windup / Mathf.Max(0.01f, _ability.Data.windupTime)) : _ability.WindupProgress;
        /// <summary>Carga que pinta la cúpula (0-1; -1 = ninguna). No en la carga común (L9): ahí el poder aún no ha empezado.</summary>
        public float ChargeVisualProgress => !_isActive || _windup > 0f || _ability == null ? -1f : _ability.ChargeVisualProgress;
        /// <summary>Usos en total del poder activo con varios usos (0 = no tiene, o no está activo).</summary>
        public int MaxUses => _isActive && _ability != null ? _ability.MaxUses : 0;
        /// <summary>Usos que le quedan por lanzar al poder activo, contando el que se está cargando.</summary>
        public int UsesLeft => _isActive && _ability != null ? _ability.UsesLeft : 0;
        public bool IsReady => !_isActive && _energy >= 1f;

        /// <summary>Equipa un poder (o lo mantiene) y vacía la energía.</summary>
        public void Reset(FakeBladeController owner, SpecialAbilityType type)
        {
            Stop();
            _ability?.ClearLingering();
            _owner = owner;

            SpecialAbilityData data = SpecialAbilities.Get(type);
            if (_ability == null || _ability.Data != data)
                _ability = data.CreateRuntime(owner);

            _energy = 0f;
            _pendingHeal = 0f;
            _healCut = false;
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

            // Efecto común de todos los poderes (GDD 5): RPM poco a poco y cargas al momento
            var cfg = CombatConfig.Active;
            _healCut = false;
            _pendingHeal = _owner.MaxSpinSpeed * cfg.specialActivationSpinPct * _owner.Trait(PartTraitType.HealAmount);
            _healRate = _pendingHeal / Mathf.Max(0.05f, cfg.specialHealTime);
            _owner.Attack.RefillCharges();

            // Carga común (L9): el poder empieza al acabarla, salvo los que llevan la suya
            _windup = _ability.HandlesWindup ? 0f : _ability.Data.windupTime;
            if (_windup <= 0f) _ability.OnActivate();
            return true;
        }

        /// <summary>Especial pulsado con el poder activo: se lo pasa al poder (Onda de choque, F1). No durante la carga común.</summary>
        public bool TryPressActive() => IsOn && _ability.OnPressedWhileActive();

        /// <summary>Distancia al rival a la que conviene usarlo (0 = cualquiera). Para la IA.</summary>
        public float UseRange => _ability != null ? _ability.UseRange : 0f;

        /// <summary>Distancia mínima al rival a la que conviene activarlo (0 = sin mínimo). Para la IA.</summary>
        public float UseRangeMin => _ability != null ? _ability.UseRangeMin : 0f;

        /// <summary>Activo y con usos que se lanzan volviendo a pulsar Especial. Para la IA.</summary>
        public bool WantsRepeatPress => IsOn && _ability.WantsRepeatPress;

        /// <summary>Carga antes de cada uso que se lanza volviendo a pulsar (s; 0 = sin carga). Para la IA.</summary>
        public float RepeatWindup => _ability != null ? _ability.RepeatWindup : 0f;

        /// <summary>
        /// Curación de un poder (regeneración): solo si ningún golpe de ataque enemigo la ha cortado
        /// desde que se activó.
        /// </summary>
        public void Heal(float amount)
        {
            if (!_healCut && amount > 0f) _owner.AddSpin(amount * _owner.Trait(PartTraitType.HealAmount));
        }

        /// <summary>Hay curación del poder en curso (la de activación o la de un poder que regenera).</summary>
        public bool IsHealing => !_healCut && (_pendingHeal > 0f || (IsOn && _ability.Heals));

        /// <summary>
        /// Un golpe de ataque enemigo corta la curación que falte hasta el próximo poder.
        /// Devuelve true si había algo que cortar.
        /// </summary>
        public bool CutHeal()
        {
            if (!IsHealing) return false;
            _healCut = true;
            _pendingHeal = 0f;
            return true;
        }

        /// <summary>Avanza el poder activo. Devuelve true el frame en que termina.</summary>
        public bool Tick(float dt)
        {
            if (_pendingHeal > 0f)
            {
                float heal = Mathf.Min(_pendingHeal, _healRate * dt);
                _pendingHeal -= heal;
                _owner.AddSpin(heal);
            }

            if (!_isActive)
            {
                if (_ability != null && _ability.IsLingering) _ability.TickLingering(dt);
                return false;
            }

            // Carga común (L9): se frena hasta quedarse quieta y el poder empieza al acabarla. No gasta la duración, y lo
            // que dejó el uso anterior sigue (Escudo, Fantasma, Rastro de agua)
            if (_windup > 0f)
            {
                if (_ability.IsLingering) _ability.TickLingering(dt);
                Vector3 v = _owner.Velocity;
                v.y = 0f;
                _owner.ApplyPull(-v * Mathf.Min(1f, _ability.Data.windupBrake * dt));
                _windup -= dt;
                if (_windup <= 0f)
                {
                    _windup = 0f;
                    _ability.OnActivate();
                }
                return false;
            }

            _ability.Tick(dt);
            // El poder se ha terminado él mismo en su Tick (Tornado, al soltar el empujón): ya lo avisó EndSpecial
            if (!_isActive) return false;
            // Rasgo de duración del poder (H8)
            _energy -= dt / Mathf.Max(0.1f, _ability.Data.duration * _owner.Trait(PartTraitType.SpecialDuration));
            if (_energy > 0f) return false;
            // Una carga ya empezada se termina aunque se acabe el tiempo (Onda de choque: la última onda sale igual)
            if (_ability.IsWindingUp)
            {
                _energy = 0f;
                return false;
            }

            Stop();
            return true;
        }

        /// <summary>Corta el poder activo (K.O. o reinicio). Devuelve true si estaba activo.</summary>
        public bool Stop()
        {
            if (!_isActive) return false;
            _isActive = false;
            _energy = 0f;
            _windup = 0f;
            _ability.OnEnd();
            return true;
        }

        /// <summary>Ha quitado RPM a otra peonza en un choque (solo cuenta con el poder activo).</summary>
        public void NotifyClashDamageDealt(FakeBladeController target, float damage)
        {
            if (IsOn && target != null && damage > 0f)
                _ability.OnClashDamageDealt(target, damage);
        }

        /// <summary>Bonus del poder activo en un choque normal (ninguno si no está activo).</summary>
        public ClashBonus GetClashBonus(FakeBladeController target, float approachSpeed) =>
            IsOn && target != null ? _ability.GetClashBonus(target, approachSpeed) : ClashBonus.None;

        /// <summary>Efecto del poder que sigue tras terminar (Escudo, Rastro de agua, Fantasma).</summary>
        public bool IsLingering => _ability != null && _ability.IsLingering;

        /// <summary>Quita lo que el poder haya dejado puesto (al caer la peonza).</summary>
        public void ClearLingering() => _ability?.ClearLingering();

        /// <summary>Defensa del poder (activo, o con su efecto todavía puesto) cuando otra peonza la embiste.</summary>
        public ClashGuard GetClashGuard(FakeBladeController attacker, float attackerApproachSpeed) =>
            (IsOn || IsLingering) && attacker != null ? _ability.GetClashGuard(attacker, attackerApproachSpeed) : ClashGuard.None;

        /// <summary>Después de aplicar un choque con defensa.</summary>
        public void NotifyClashGuardApplied(FakeBladeController attacker, ClashGuard guard, Vector3 contactPoint)
        {
            if (!guard.IsNone && _ability != null) _ability.OnClashGuardApplied(attacker, contactPoint);
        }

        /// <summary>Después de aplicar un choque con bonus.</summary>
        public void NotifyClashBonusApplied(FakeBladeController target, ClashBonus bonus, Vector3 contactPoint)
        {
            if (IsOn && !bonus.IsNone) _ability.OnClashBonusApplied(target, bonus, contactPoint);
        }

        #region Modifiers (solo mientras está activo y pasada la carga común)
        /// <summary>Multiplicador del daño recibido.</summary>
        public float DamageTakenMultiplier => IsOn ? _ability.DamageTakenMultiplier : 1f;
        /// <summary>Multiplicador del daño que hace.</summary>
        public float DamageDealtMultiplier => IsOn ? _ability.DamageDealtMultiplier : 1f;
        /// <summary>Fracción de los ataques recibidos que devuelve (como Espinas).</summary>
        public float ReflectFraction => IsOn ? _ability.ReflectFraction : 0f;
        /// <summary>Multiplicador del empuje recibido.</summary>
        public float KnockbackTakenMultiplier => IsOn ? _ability.KnockbackTakenMultiplier : 1f;
        /// <summary>Multiplicador de la masa física.</summary>
        public float PhysicalMassMultiplier => IsOn ? _ability.PhysicalMassMultiplier : 1f;
        /// <summary>Multiplicador del desgaste de RPM con el tiempo.</summary>
        public float SpinDecayMultiplier => IsOn ? _ability.SpinDecayMultiplier : 1f;
        /// <summary>También con el efecto todavía puesto (Rastro de agua: patina sobre su agua mientras quede).</summary>
        public float MoveSpeedMultiplier => IsOn || IsLingering ? _ability.MoveSpeedMultiplier : 1f;
        public float AccelerationMultiplier => IsOn ? _ability.AccelerationMultiplier : 1f;
        /// <summary>Nivel mínimo con el que cuentan los ataques.</summary>
        public int MinAttackLevel => IsOn ? _ability.MinAttackLevel : 0;
        /// <summary>Velocidad de recarga de las cargas de ataque.</summary>
        public float AttackRechargeMultiplier => IsOn ? _ability.AttackRechargeMultiplier : 1f;
        /// <summary>El agua del suelo no la moja (Hielo activo).</summary>
        public bool ImmuneToWet => IsOn && _ability.ImmuneToWet;
        public float DashCooldownMultiplier => IsOn ? _ability.DashCooldownMultiplier : 1f;
        public float DashImpulseMultiplier => IsOn ? _ability.DashImpulseMultiplier : 1f;
        /// <summary>Multiplicador del coste en RPM del dash.</summary>
        public float DashCostMultiplier => IsOn ? _ability.DashCostMultiplier : 1f;
        public float ChargeSpeedMultiplier => IsOn ? _ability.ChargeSpeedMultiplier : 1f;
        /// <summary>El poder activo no deja atacar ni hacer dash (tampoco durante la carga común).</summary>
        public bool BlocksActions => _isActive && (_windup > 0f || _ability.BlocksActions);
        /// <summary>Con el poder activo, sus choques no admiten parry ni bloqueo.</summary>
        public bool PreventsParry => IsOn && _ability.PreventsParry;
        #endregion
    }
}
