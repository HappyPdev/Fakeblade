using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// IA y especiales (GDD 6.4, D11; primera parte, 2026-10-14): usar bien el poder propio y leer el del rival.
    ///
    /// Poder propio:
    /// - Salto y Embestida: durante la carga apunta adonde estará el rival (con su error de puntería y de predicción,
    ///   adelantando como mucho MaxPowerLead). El Salto solo se activa con el rival a la distancia del salto (UseRangeMin,
    ///   UseRange; si predice, la distancia que habrá) y no mientras el rival se mueve de lado deprisa (hasta LeapMaxWait).
    /// - Tornado: deja que la atracción acabe; con capa táctica, lo suelta antes si el empujón estampa al rival contra la pared.
    /// - Defensa, Pinchos y Rebote (poderes de guardia): «en buen momento», se guardan hasta que le vayan a atacar.
    ///   Con Defensa ataca sin miedo (no pierde RPM) y no hace parry ni esquiva; con Pinchos no esquiva (que la
    ///   golpeen le devuelve el daño); con Rebote se queda quieta, casi no ataca y no hace parry ni esquiva.
    /// - Escudo: no se reactiva mientras le quede más de un escudo.
    ///
    /// Poder del rival (lo que lee su nivel, AILevel.readsSpecials; reacciona con su tiempo de reacción y con
    /// specialDodgeChance):
    /// - Embestida: no admite parry; esquiva de lado con dash al acabar la carga o al salir la embestida.
    /// - Salto: en la carga se aparta de lado; en el aire sale de la onda del sitio donde va a caer (con dash si no llega).
    /// - Tornado: cerca del borde sale con dash; si no, se coloca del lado del centro para que el empujón no la estampe.
    /// - Onda de choque: mientras le queden ondas, si está junto a la pared dentro de su alcance, se coloca del lado del
    ///   centro (la onda la empuja hacia dentro) y mientras no ataca.
    /// - Defensa, Pinchos y Rebote: ataca menos (AIProfile.defensiveRivalAttackScale).
    /// Sin asignaciones por frame.
    /// </summary>
    public partial class AIBrain
    {
        /// <summary>Esquiva la Embestida cuando le faltan estos segundos de carga (antes, el rival aún puede reapuntar).</summary>
        private const float RamDodgeLead = 0.1f;
        /// <summary>Margen sobre el alcance de un poder del rival (m) a partir del que ya se aparta.</summary>
        private const float PowerMargin = 0.5f;
        /// <summary>Tornado propio: lo suelta antes (rival junto a la pared) solo con esta parte de la atracción hecha (fuerza 75%).</summary>
        private const float TornadoEarlyProgress = 0.5f;
        /// <summary>Con un poder de guardia lleno, lo usa como mucho a los tantos segundos aunque nadie la ataque.</summary>
        private const float GuardMaxWait = 6f;
        /// <summary>Con menos de esta vida usa ya el poder de guardia.</summary>
        private const float GuardLowSpin = 0.3f;
        /// <summary>Con Rebote propio, casi no ataca (sus ataques hacen lo de siempre; el poder es que la embistan).</summary>
        private const float BounceAttackScale = 0.2f;
        /// <summary>Con Defensa propia (no pierde RPM), ataca más y la capa táctica arriesga más.</summary>
        private const float DefenseAttackScale = 1.5f;
        private const float DefenseAggressionBonus = 0.3f;
        /// <summary>Onda de choque del rival: a menos de esto de la pared (m) se coloca del lado del centro.</summary>
        private const float ShockWaveWallDistance = 2.5f;
        /// <summary>
        /// Lo más que adelanta la puntería del Salto (s): el salto tarda 1,25 s y en ese tiempo el rival gira, así que
        /// adelantarlo todo en línea recta falla (medido el 2026-10-14: con J1 dando vueltas, caía a ~4 m de él).
        /// </summary>
        private const float MaxPowerLead = 0.6f;
        /// <summary>Salto propio: no salta si el rival se mueve de lado a más de esto (m/s)...</summary>
        private const float LeapMaxLateralSpeed = 3f;
        /// <summary>...salvo que lleve lleno este tiempo (s): no se lo guarda siempre.</summary>
        private const float LeapMaxWait = 4f;

        // Poder del rival que está leyendo: cada carga de la Embestida es una amenaza nueva
        private SpecialAbility _seenPower;
        private int _seenPowerPhase;
        private float _powerReactAt;
        private bool _powerDodges;
        private bool _powerDashUsed;
        // Dirección con la que se aparta este frame (manda sobre el resto del movimiento)
        private bool _evading;
        private Vector3 _evadeDir;
        // Poder de guardia lleno desde (para no guardarlo eternamente)
        private float _readySince = -1f;

        /// <summary>Probabilidad de intentar esquivar un poder del rival (nivel × esquiva del perfil).</summary>
        protected float SpecialDodgeChance => Mathf.Clamp01(Level.specialDodgeChance * Profile.dodgeScale);

        /// <summary>Se está apartando de un poder del rival: no empieza ataques.</summary>
        protected bool Evading => _evading;

        /// <summary>El poder propio en marcha (null si no hay ninguno activo).</summary>
        protected SpecialAbility ActivePower => Blade.Special.IsActive ? Blade.Special.Ability : null;

        private static bool IsGuardPower(SpecialAbility power) =>
            power is DefenseAbility || power is SpikesAbility || power is BounceAbility;

        #region Cada frame
        /// <summary>Cada frame, después de la defensa: el poder propio (Tornado) y el del rival (apartarse, esquivar).</summary>
        private void UpdateSpecials()
        {
            _evading = false;

            // Tornado propio: deja que la atracción acabe (empujón entero, con el rival pegado), salvo si el empujón lo
            // estampa ya contra la pared (medido el 2026-10-14: soltándolo al tener al rival pegado salía al 5-50% de la
            // atracción, con la mitad de fuerza, porque la IA ya lo tiene cerca al activarlo)
            if (ActivePower is TornadoAbility tornado && tornado.IsPulling && Level.tactical && Perception.Target != null &&
                tornado.PullProgress >= TornadoEarlyProgress && Perception.TargetWallDistance < PinWallDistance &&
                Vector3.Dot(Perception.ToTarget, Perception.TargetOutward) > 0.5f)
                PressSpecial();

            SpecialAbility power = Perception.TargetPower;
            if (power == null)
            {
                _seenPower = null;
                return;
            }

            // Amenaza nueva: espera su tiempo de reacción y decide una vez si intenta esquivarla
            int phase = power is RamAbility ram ? ram.ChargesLeft : 0;
            if (power != _seenPower || phase != _seenPowerPhase)
            {
                _seenPower = power;
                _seenPowerPhase = phase;
                _powerReactAt = Time.time + ReactionTime;
                _powerDodges = Random.value < SpecialDodgeChance;
                _powerDashUsed = false;
            }
            if (Time.time < _powerReactAt) return;

            switch (power)
            {
                case RamAbility rivalRam:
                    if (_powerDodges) ReadRam(rivalRam);
                    break;
                case LeapAbility leap:
                    if (_powerDodges) ReadLeap(leap);
                    break;
                case TornadoAbility rivalTornado:
                    if (_powerDodges) ReadTornado(rivalTornado);
                    break;
                case ShockWaveAbility wave:
                    ReadShockWave(wave);
                    break;
            }
        }

        /// <summary>Embestida del rival: sin parry posible, esquiva de lado con dash en el último momento de la carga.</summary>
        private void ReadRam(RamAbility ram)
        {
            if (!ram.IsWindingUp && !ram.IsLunging) return;
            AIPerception p = Perception;
            if (p.Distance > ram.UseRange + PowerMargin) return;

            // Ya embistiendo: solo si viene hacia ella
            Vector3 line = ram.IsLunging ? ram.LungeDirection : -p.ToTarget;
            if (ram.IsLunging && Vector3.Dot(line, -p.ToTarget) < 0.3f) return;

            Vector3 side = SideStep(line);
            Evade(side);
            if (!_powerDashUsed && (ram.IsLunging || ram.WindupLeft <= RamDodgeLead) && CanAct && Blade.CanDash)
            {
                DashTowards(side, false);
                _powerDashUsed = true;
            }
        }

        /// <summary>
        /// Salto del rival: en la carga (aún no se sabe adónde) se aparta de lado; en el aire sale de la onda del sitio
        /// donde va a caer y, si andando no le da tiempo, con dash.
        /// </summary>
        private void ReadLeap(LeapAbility leap)
        {
            AIPerception p = Perception;
            if (leap.IsWindingUp)
            {
                if (p.Distance < leap.JumpDistance + leap.WaveRadius + PowerMargin) Evade(SideStep(-p.ToTarget));
                return;
            }
            if (!leap.IsFlying) return;

            Vector3 away = Blade.Position - leap.LandingPoint;
            away.y = 0f;
            float distance = away.magnitude;
            float safe = leap.WaveRadius + p.BladeRadius + PowerMargin;
            if (distance >= safe) return;

            Vector3 dir = distance > 0.1f ? away / distance : SideStep(-p.ToTarget);
            // Junto a la pared, hacia dentro (si no, se queda pegada a ella dentro de la onda)
            if (p.WallDistance < 1.5f) dir = (dir + p.ToCenter).normalized;
            Evade(dir);
            if (!_powerDashUsed && (safe - distance > 1f || leap.TimeToLand < 0.4f) && CanAct && Blade.CanDash)
            {
                DashTowards(dir, false);
                _powerDashUsed = true;
            }
        }

        /// <summary>
        /// Tornado del rival: cerca del borde sale con dash; más dentro ya no escapa, así que se coloca del lado del
        /// centro de la arena para que el empujón la mande hacia dentro y no contra la pared.
        /// </summary>
        private void ReadTornado(TornadoAbility tornado)
        {
            if (!tornado.IsPulling) return;
            AIPerception p = Perception;
            if (p.Distance > tornado.Radius + PowerMargin) return;

            Vector3 away = -p.ToTarget;
            if (p.Distance > tornado.Radius * 0.7f)
            {
                Evade(away);
                if (!_powerDashUsed && CanAct && Blade.CanDash)
                {
                    DashTowards(away, false);
                    _powerDashUsed = true;
                }
                return;
            }

            // A 1,5 m del tornado, del lado del centro de la arena
            Vector3 dir = CenterSideOfTarget(1.5f);
            Evade(dir != Vector3.zero ? dir : away);
        }

        /// <summary>
        /// Onda de choque del rival: mientras le queden ondas y esté cerca de la pared dentro de su alcance, se pone del
        /// lado del centro de la arena respecto al rival, a la misma distancia. Así la onda la empuja hacia dentro y no la
        /// estampa contra la pared. Lejos de la pared juega como siempre: huir de un rival que la persigue no sirve en
        /// una arena de 7 m (medido el 2026-10-14, 18 ondas: huyendo 21 golpes, del lado del centro siempre 14, sin
        /// leerla 10).
        /// </summary>
        private void ReadShockWave(ShockWaveAbility wave)
        {
            if (wave.WavesLeft <= 0) return;
            AIPerception p = Perception;
            float safe = wave.Radius + PowerMargin;
            if (p.Distance >= safe || p.WallDistance > ShockWaveWallDistance) return;

            Vector3 dir = CenterSideOfTarget(Mathf.Clamp(p.Distance, 2.5f, safe));
            Evade(dir != Vector3.zero ? dir : -p.ToTarget);
        }

        /// <summary>
        /// Hacia el punto a <paramref name="distance"/> m del rival en dirección al centro de la arena (desde ahí, lo que
        /// la empuje desde el rival la manda hacia dentro). Cero si no hay arena o si el rival está en el centro.
        /// </summary>
        private Vector3 CenterSideOfTarget(float distance)
        {
            AIPerception p = Perception;
            if (p.ToCenter == Vector3.zero || p.Target == null) return Vector3.zero;
            Vector3 center = Blade.Position + p.ToCenter * p.CenterDistance;
            Vector3 rival = p.Target.Position;
            Vector3 toCenter = center - rival;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude < 0.25f) return Vector3.zero;
            Vector3 dir = rival + toCenter.normalized * distance - Blade.Position;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.01f ? Vector3.ClampMagnitude(dir, 1f) : Vector3.zero;
        }

        private void Evade(Vector3 direction)
        {
            _evading = true;
            _evadeDir = direction;
        }

        /// <summary>Puede atacar o hacer dash: ni aturdida ni con las acciones bloqueadas por su poder.</summary>
        private bool CanAct => !Blade.IsStaggered && !Blade.Special.BlocksActions;

        /// <summary>
        /// De lado respecto a una línea de ataque que viene hacia ella: hacia el lado en que ya está desviada y, si va
        /// justo por el medio, hacia el centro de la arena.
        /// </summary>
        private Vector3 SideStep(Vector3 line)
        {
            line.y = 0f;
            if (line.sqrMagnitude < 0.0001f) line = -Perception.ToTarget;
            line.Normalize();
            Vector3 side = new Vector3(-line.z, 0f, line.x);
            Vector3 offset = Blade.Position - (Perception.Target != null ? Perception.Target.Position : Blade.Position);
            offset.y = 0f;
            float lateral = Vector3.Dot(offset, side);
            if (Mathf.Abs(lateral) > 0.3f) return lateral > 0f ? side : -side;
            return Vector3.Dot(side, Perception.ToCenter) >= 0f ? side : -side;
        }
        #endregion

        #region Movimiento
        /// <summary>
        /// Movimiento que manda sobre todo lo demás: apuntar la carga del Salto o de la Embestida propios, o apartarse
        /// de un poder del rival.
        /// </summary>
        private bool TrySpecialSteer(out Vector3 direction)
        {
            SpecialAbility own = ActivePower;
            if (own is LeapAbility leap && leap.IsWindingUp)
            {
                direction = LeadAim(leap.WindupLeft + leap.AirTime);
                return true;
            }
            if (own is RamAbility ram && ram.IsWindingUp)
            {
                direction = LeadAim(ram.WindupLeft + Perception.Gap / Mathf.Max(1f, ram.LungeSpeed));
                return true;
            }
            direction = _evadeDir;
            return _evading;
        }

        /// <summary>Hacia el rival, adelantado lo que avance en <paramref name="seconds"/> si el nivel predice, con su error.</summary>
        private Vector3 LeadAim(float seconds)
        {
            AIPerception p = Perception;
            if (p.Target == null) return Blade.Facing;
            Vector3 dir = LeadPoint(seconds) - Blade.Position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : p.ToTarget;
            return Quaternion.AngleAxis(_aimError, Vector3.up) * dir;
        }

        /// <summary>Dónde estará el rival dentro de <paramref name="seconds"/> (como mucho MaxPowerLead) si el nivel predice.</summary>
        private Vector3 LeadPoint(float seconds)
        {
            Vector3 point = Perception.Target.Position;
            if (Level.predictsMovement)
                point += Perception.TargetVelocity * (Mathf.Min(seconds, MaxPowerLead) * _predictionScale);
            return point;
        }

        /// <summary>
        /// Distancia al rival con la que decide si activa el poder: la de ahora o, si el poder tiene una distancia mínima
        /// (Salto, que siempre recorre lo mismo) y el nivel predice, la que habrá cuando haga efecto.
        /// </summary>
        protected float SpecialDistance
        {
            get
            {
                AIPerception p = Perception;
                if (p.Target == null || Blade.Special.UseRangeMin <= 0f || !Level.predictsMovement) return p.Distance;
                Vector3 offset = LeadPoint(float.PositiveInfinity) - Blade.Position;
                offset.y = 0f;
                return offset.magnitude;
            }
        }

        /// <summary>Se queda en su sitio con su poder (Rebote: que la embistan).</summary>
        protected virtual bool HoldsGround => ActivePower is BounceAbility;

        /// <summary>Quedarse en su sitio: hacia el centro de la arena y, a 1 m de él, quieta.</summary>
        protected virtual Vector3 HoldGroundDirection()
        {
            AIPerception p = Perception;
            return p.ToCenter * (Mathf.Clamp01(p.CenterDistance - 1f) * 0.6f);
        }
        #endregion

        #region Decisiones
        /// <summary>Multiplica la probabilidad de atacar según los poderes en marcha (el propio y el del rival).</summary>
        protected float SpecialAttackScale()
        {
            float scale = 1f;
            SpecialAbility own = ActivePower;
            if (own is DefenseAbility) scale *= DefenseAttackScale;
            else if (own is BounceAbility) scale *= BounceAttackScale;

            // Rival con un poder de guardia activo: atacarle sirve de poco
            SpecialAbility rival = Perception.TargetPower;
            if (IsGuardPower(rival) && Perception.Target.Special.IsActive) scale *= Profile.defensiveRivalAttackScale;
            return scale;
        }

        /// <summary>Lo que suma su poder a la agresividad de la capa táctica (Defensa: no pierde RPM).</summary>
        protected float SpecialAggressionBonus() => ActivePower is DefenseAbility ? DefenseAggressionBonus : 0f;

        /// <summary>Su poder ya la protege del ataque que llega: ni parry ni esquiva (Defensa, Rebote).</summary>
        protected bool PowerTakesTheHit => ActivePower is DefenseAbility || ActivePower is BounceAbility;

        /// <summary>Con Pinchos, que la golpeen le devuelve el daño: no esquiva (el parry sí).</summary>
        protected bool PowerWelcomesHits => ActivePower is SpikesAbility;

        /// <summary>
        /// Un ataque llega y tiene lleno un poder de guardia que se guarda para esto (en buen momento): lo usa.
        /// Devuelve true si lo ha pulsado.
        /// </summary>
        protected bool TryGuardPower()
        {
            if (Level.specialUse != AISpecialUse.GoodMoment || !Blade.Special.IsReady || !IsGuardPower(Blade.Special.Ability))
                return false;
            PressSpecial();
            return true;
        }

        /// <summary>
        /// Se guarda el poder aunque lo tenga lleno: Escudo mientras le quede más de un escudo y, en buen momento, un
        /// poder de guardia hasta que la vayan a atacar (o tenga poca vida, o lleve GuardMaxWait s lleno).
        /// </summary>
        protected bool KeepsSpecial()
        {
            SpecialAbility own = Blade.Special.Ability;
            if (ShieldsStillUp) return true;
            if (_readySince < 0f) _readySince = Time.time;

            // Salto (si predice): no salta mientras el rival se mueve de lado deprisa; en lo que tarda el salto (1,25 s)
            // ya no está donde apuntó (medido el 2026-10-14: con J1 dando vueltas, 0 de 8)
            if (own is LeapAbility && Level.predictsMovement && Perception.Target != null)
            {
                Vector3 v = Perception.TargetVelocity;
                Vector3 lateral = v - Vector3.Dot(v, Perception.ToTarget) * Perception.ToTarget;
                return lateral.sqrMagnitude > LeapMaxLateralSpeed * LeapMaxLateralSpeed && !Perception.TargetStaggered &&
                       Time.time - _readySince < LeapMaxWait;
            }

            if (Level.specialUse != AISpecialUse.GoodMoment || !IsGuardPower(own)) return false;
            bool threatened = Perception.IncomingAttack || (Perception.TargetCharging && Perception.Gap < AttackRange);
            return !threatened && Blade.SpinSpeedPercentage > GuardLowSpin && Time.time - _readySince < GuardMaxWait;
        }

        /// <summary>Escudo con más de un escudo puesto: reactivarlo ahora casi no le da nada.</summary>
        protected bool ShieldsStillUp => Blade.Special.Ability is ShieldsAbility shields && shields.ShieldsUp > 1;
        #endregion
    }
}
