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
    /// Segunda parte (D11.3, 2026-10-15):
    /// - Rastro de agua: mientras lo usa, rodea al rival a TrailOrbitRadius para dejarle el agua alrededor; con el rival
    ///   mojado (resbala), ataca más.
    /// - Rayos: con capa táctica, lo guarda hasta que el rival esté junto a la pared (o lleve LeapMaxWait s lleno) y,
    ///   activo, coge carrerilla antes de cada golpe para pasar de la velocidad del golpe fuerte.
    /// - Fantasma: lo vuelve a sacar cuando se rompe el clon (no lo reactiva con el clon fuera) y ataca más mientras
    ///   el rival pelea con el clon.
    /// - Fuego, Hielo y Spin Boost: más agresiva mientras dura el suyo.
    ///
    /// Poder del rival (lo que lee su nivel, AILevel.readsSpecials; reacciona con su tiempo de reacción y con
    /// specialDodgeChance). Los que alcanzan desde lejos también los lee de un rival que no sea el más cercano:
    /// - Embestida: no admite parry; esquiva de lado con dash al acabar la carga o al salir la embestida.
    /// - Salto: en la carga se aparta de lado; en el aire sale de la onda del sitio donde va a caer (con dash si no llega).
    /// - Tornado: cerca del borde sale con dash; si no, se coloca del lado del centro para que el empujón no la estampe.
    /// - Onda de choque: mientras le queden ondas, si está junto a la pared dentro de su alcance, se coloca del lado del
    ///   centro (la onda la empuja hacia dentro) y mientras no ataca.
    /// - Defensa, Pinchos y Rebote: ataca menos (AIProfile.defensiveRivalAttackScale).
    /// Desde que lee todos (Difícil):
    /// - Rastro de agua: se aparta de los charcos de cualquier rival; mojada, no ataca ni hace dash (salvo para
    ///   esquivar un poder).
    /// - Escudo: ataca por los huecos (el rápido y el cargado salen cuando, al llegar, no habrá un escudo delante).
    /// - Fantasma: va a por la peonza de verdad, no a por el clon (AIPerception).
    /// - Fuego, Hielo y Spin Boost: evita el contacto (guarda distancia y ataca menos) mientras dura el del rival.
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
        /// Lo más que adelanta la puntería del Salto (s): el salto tarda casi 1 s (1,25 s hasta el 2026-10-15) y en ese tiempo el rival gira, así que
        /// adelantarlo todo en línea recta falla (medido el 2026-10-14: con J1 dando vueltas, caía a ~4 m de él).
        /// </summary>
        private const float MaxPowerLead = 0.6f;
        /// <summary>Salto propio: no salta si el rival se mueve de lado a más de esto (m/s)...</summary>
        private const float LeapMaxLateralSpeed = 3f;
        /// <summary>...salvo que lleve lleno este tiempo (s): no se lo guarda siempre.</summary>
        private const float LeapMaxWait = 4f;
        /// <summary>Rastro de agua propio: rodea al rival a esta distancia entre centros (m) para dejarle el agua alrededor.</summary>
        private const float TrailOrbitRadius = 2.8f;
        /// <summary>Con el rival mojado (resbala, no puede girar), ataca más.</summary>
        private const float WetRivalAttackScale = 1.5f;
        /// <summary>Mientras deja agua, y el rival aún seco, ataca menos (lo importante es rodearlo).</summary>
        private const float TrailingAttackScale = 0.5f;
        /// <summary>Charcos del rival: los esquiva mirando dónde estará dentro de estos segundos...</summary>
        private const float PuddleLookAhead = 0.35f;
        /// <summary>...y a esta distancia de más sobre el radio del charco (m).</summary>
        private const float PuddleMargin = 0.5f;
        /// <summary>Rayos propio (capa táctica): lo guarda hasta tener al rival a esta distancia de la pared (m)...</summary>
        private const float LightningWallDistance = 2.5f;
        /// <summary>Fuego, Hielo, Spin Boost y Fantasma propios: ataca más mientras duran.</summary>
        private const float PowerAttackScale = 1.4f;
        private const float PowerAggressionBonus = 0.2f;
        /// <summary>Fantasma: el rival «se entretiene» con el clon si lo tiene a menos de esto (m).</summary>
        private const float CloneBusyDistance = 3f;
        /// <summary>Fuego, Hielo o Spin Boost del rival: guarda esta distancia entre centros (m).</summary>
        private const float AvoidContactDistance = 5f;
        /// <summary>Escudo del rival: no ataca si, al llegar, tendrá un escudo a menos de estos grados de margen.</summary>
        private const float ShieldGapMargin = 20f;
        /// <summary>Escudo del rival sin hueco al decidir el rápido: lo espera, mirando cada frame, hasta estos segundos.</summary>
        private const float ShieldGapWait = 0.6f;
        /// <summary>...y retiene el cargado esperando un hueco solo con más de esto entre bordes (m).</summary>
        private const float ShieldHoldGap = 1f;
        /// <summary>
        /// Lo que tarda el golpe de más sobre hueco / impulso (s): sale un frame después de decidirlo y el impulso es un
        /// acelerón, no instantáneo (medido el 2026-10-15: rápido a 1,7 m, 0,1-0,28 s; cargado de nivel 2, 0,13 s).
        /// </summary>
        private const float StrikeDelay = 0.08f;

        // Poder del rival que está leyendo: cada carga de la Embestida es una amenaza nueva
        private SpecialAbility _seenPower;
        private int _seenPowerPhase;
        private float _powerReactAt;
        private bool _powerDodges;
        private bool _powerDashUsed;
        // Quién usa ese poder (el rival más cercano u otro) y dónde está respecto a la IA
        private FakeBladeController _powerOwner;
        private Vector3 _toOwner;
        private float _ownerDistance;
        // Dirección con la que se aparta este frame (manda sobre el resto del movimiento)
        private bool _evading;
        private Vector3 _evadeDir;
        // Poder de guardia lleno desde (para no guardarlo eternamente)
        private float _readySince = -1f;
        // Esperando un hueco del Escudo del rival para soltar el rápido
        private float _shieldGapUntil;

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

            // El del rival más cercano o, si ese no amenaza desde lejos, el de otro rival que sí (D11.3)
            SpecialAbility power = Perception.TargetPower;
            FakeBladeController owner = Perception.Target;
            if (!AIPerception.IsRangedThreat(power) && Perception.OtherPower != null)
            {
                power = Perception.OtherPower;
                owner = Perception.OtherPowerOwner;
            }
            if (power == null)
            {
                _seenPower = null;
                return;
            }
            SetPowerOwner(owner);

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
            if (_ownerDistance > ram.UseRange + PowerMargin) return;

            // Ya embistiendo: solo si viene hacia ella
            Vector3 line = ram.IsLunging ? ram.LungeDirection : -_toOwner;
            if (ram.IsLunging && Vector3.Dot(line, -_toOwner) < 0.3f) return;

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
                if (_ownerDistance < leap.JumpDistance + leap.WaveRadius + PowerMargin) Evade(SideStep(-_toOwner));
                return;
            }
            if (!leap.IsFlying) return;

            Vector3 away = Blade.Position - leap.LandingPoint;
            away.y = 0f;
            float distance = away.magnitude;
            float safe = leap.WaveRadius + p.BladeRadius + PowerMargin;
            if (distance >= safe) return;

            Vector3 dir = distance > 0.1f ? away / distance : SideStep(-_toOwner);
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
            if (_ownerDistance > tornado.Radius + PowerMargin) return;

            Vector3 away = -_toOwner;
            if (_ownerDistance > tornado.Radius * 0.7f)
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
            if (_ownerDistance >= safe || p.WallDistance > ShockWaveWallDistance) return;

            Vector3 dir = CenterSideOfTarget(Mathf.Clamp(_ownerDistance, 2.5f, safe));
            Evade(dir != Vector3.zero ? dir : -_toOwner);
        }

        /// <summary>
        /// Hacia el punto a <paramref name="distance"/> m del rival en dirección al centro de la arena (desde ahí, lo que
        /// la empuje desde el rival la manda hacia dentro). Cero si no hay arena o si el rival está en el centro.
        /// </summary>
        private Vector3 CenterSideOfTarget(float distance)
        {
            AIPerception p = Perception;
            if (p.ToCenter == Vector3.zero || _powerOwner == null) return Vector3.zero;
            Vector3 center = Blade.Position + p.ToCenter * p.CenterDistance;
            Vector3 rival = _powerOwner.Position;
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

        /// <summary>
        /// Quién usa el poder que lee: con el rival más cercano, lo que ve de él (con su retraso); con otro rival, su
        /// posición de ahora (no lleva historial de los demás).
        /// </summary>
        private void SetPowerOwner(FakeBladeController owner)
        {
            _powerOwner = owner;
            AIPerception p = Perception;
            if (owner == p.Target)
            {
                _toOwner = p.ToTarget;
                _ownerDistance = p.Distance;
                return;
            }
            Vector3 offset = owner.Position - Blade.Position;
            offset.y = 0f;
            _ownerDistance = offset.magnitude;
            _toOwner = _ownerDistance > 0.01f ? offset / _ownerDistance : Vector3.forward;
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
            if (line.sqrMagnitude < 0.0001f) line = -_toOwner;
            line.Normalize();
            Vector3 side = new Vector3(-line.z, 0f, line.x);
            Vector3 offset = Blade.Position - (_powerOwner != null ? _powerOwner.Position : Blade.Position);
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

        /// <summary>
        /// Movimiento sin atacar con los poderes en cuenta: quedarse en su sitio (Rebote), rodear al rival dejando agua
        /// (Rastro de agua propio), guardar distancia (Fuego, Hielo o Spin Boost del rival) o el de su perfil; y, desde
        /// Difícil, apartándose de los charcos de los rivales.
        /// </summary>
        private Vector3 SpecialApproachDirection()
        {
            Vector3 dir;
            if (HoldsGround) dir = HoldGroundDirection();
            else if (ActivePower is WaterTrailAbility && Perception.Target != null) dir = TrailOrbitDirection();
            else if (AvoidsContact) dir = AvoidContactDirection();
            else dir = ApproachDirection();
            return AvoidPuddles(dir);
        }

        /// <summary>Rastro de agua propio: rodea al rival a TrailOrbitRadius (así le deja el agua alrededor).</summary>
        private Vector3 TrailOrbitDirection()
        {
            AIPerception p = Perception;
            float radial = Mathf.Clamp((p.Distance - TrailOrbitRadius) * 0.8f, -0.8f, 0.8f);
            return (Perpendicular() + p.ToTarget * radial).normalized;
        }

        /// <summary>Fuego, Hielo o Spin Boost del rival: lo rodea a AvoidContactDistance y, junto a la pared, hacia dentro.</summary>
        private Vector3 AvoidContactDirection()
        {
            AIPerception p = Perception;
            float radial = Mathf.Clamp((p.Distance - AvoidContactDistance) * 0.6f, -1f, 0.4f);
            Vector3 dir = Perpendicular() * 0.8f + p.ToTarget * radial;
            if (p.WallDistance < 2f) dir += p.ToCenter * 0.6f;
            return dir.normalized;
        }

        /// <summary>
        /// Desde que lee todos los poderes: se aparta de los charcos de agua de los rivales que pisaría en
        /// PuddleLookAhead s (los del suyo no: sobre ellos va más rápida).
        /// </summary>
        private Vector3 AvoidPuddles(Vector3 dir)
        {
            if (Level.readsSpecials != AISpecialReading.All) return dir;
            Vector3 ahead = Blade.Position + Blade.Velocity * PuddleLookAhead;
            Vector3 push = Vector3.zero;
            var blades = FakeBladeController.ActiveBlades;
            for (int b = 0; b < blades.Count; b++)
            {
                FakeBladeController other = blades[b];
                if (other == Blade || other.IsGhost || !other.CanHarm(Blade)) continue;
                if (!(other.Special.Ability is WaterTrailAbility trail) || (!other.Special.IsActive && !other.Special.IsLingering))
                    continue;
                float reach = trail.PuddleRadius + PuddleMargin;
                for (int i = 0; i < trail.PuddleCapacity; i++)
                {
                    if (!trail.IsPuddleAlive(i)) continue;
                    Vector3 offset = ahead - trail.PuddlePosition(i);
                    offset.y = 0f;
                    float distance = offset.magnitude;
                    if (distance >= reach || distance < 0.001f) continue;
                    push += offset / distance * (1f - distance / reach);
                }
            }
            if (push == Vector3.zero) return dir;
            // Hacia fuera de los charcos, sin dejar de ir hacia donde iba (si no, se queda dando vueltas)
            return (dir + Vector3.ClampMagnitude(push, 1f) * 1.5f).normalized;
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
            else if (OwnPowerPushes) scale *= PowerAttackScale;
            // Rastro de agua: con el rival mojado, a por él; mientras lo moja, a rodearlo
            if (Blade.Special.Ability is WaterTrailAbility && Perception.Target != null)
            {
                if (Perception.Target.Status.IsWet) scale *= WetRivalAttackScale;
                else if (own is WaterTrailAbility) scale *= TrailingAttackScale;
            }

            // Rival con un poder de guardia activo: atacarle sirve de poco; con Fuego, Hielo o Spin Boost, mejor no tocarlo
            SpecialAbility rival = Perception.TargetPower;
            if ((IsGuardPower(rival) && Perception.Target.Special.IsActive) || AvoidsContact)
                scale *= Profile.defensiveRivalAttackScale;
            return scale;
        }

        /// <summary>Lo que suma su poder a la agresividad de la capa táctica (Defensa: no pierde RPM; Fuego, Hielo, Spin Boost, Fantasma).</summary>
        protected float SpecialAggressionBonus() =>
            ActivePower is DefenseAbility ? DefenseAggressionBonus : OwnPowerPushes ? PowerAggressionBonus : 0f;

        /// <summary>
        /// Su poder premia ir a por el rival: Fuego, Hielo o Spin Boost activos, o el clon del Fantasma entreteniendo al
        /// rival (lo tiene a menos de CloneBusyDistance y más cerca que a ella).
        /// </summary>
        private bool OwnPowerPushes
        {
            get
            {
                SpecialAbility own = ActivePower;
                if (own is FireAbility || own is IceAbility || own is SpinBoostAbility) return true;
                FakeBladeController clone = Blade.GhostClone;
                if (!GhostOut || Perception.Target == null) return false;
                float toClone = (clone.Position - Perception.Target.Position).sqrMagnitude;
                return toClone < CloneBusyDistance * CloneBusyDistance && toClone < Perception.Distance * Perception.Distance;
            }
        }

        /// <summary>El rival tiene activo Fuego, Hielo o Spin Boost (y la IA no tiene uno de esos): evita el contacto.</summary>
        protected bool AvoidsContact
        {
            get
            {
                SpecialAbility rival = Perception.TargetPower;
                if (!(rival is FireAbility || rival is IceAbility || rival is SpinBoostAbility) || !Perception.Target.Special.IsActive)
                    return false;
                SpecialAbility own = ActivePower;
                return !(own is FireAbility || own is IceAbility || own is SpinBoostAbility);
            }
        }

        /// <summary>Mojada (resbala): desde que lee todos los poderes, ni ataca ni hace dash (salvo para esquivar un poder).</summary>
        protected bool Soaked => Level.readsSpecials == AISpecialReading.All && Blade.Status.IsWet;

        /// <summary>
        /// Escudo del rival (desde que lee todos los poderes): un golpe que llegue dentro de <paramref name="seconds"/> s
        /// desde donde está la IA chocaría con un escudo. Espera a que delante haya un hueco.
        /// </summary>
        protected bool RivalShieldBlocks(float seconds) =>
            Level.readsSpecials == AISpecialReading.All && Perception.TargetPower is ShieldsAbility shields &&
            shields.ShieldsUp > 0 && shields.CoversAt(Blade.Position, seconds, ShieldGapMargin);

        /// <summary>
        /// Escudo del rival sin hueco al decidir un rápido: lo espera unos instantes (UpdateShieldGap). Mirándolo solo al
        /// decidir, atacaba en cuanto entraba en el hueco, así que llegaba al borde y un golpe algo más rápido daba en el
        /// escudo (medido el 2026-10-15: leyendo, más escudos rotos que sin leer).
        /// </summary>
        protected void WaitForShieldGap() => _shieldGapUntil = Time.time + ShieldGapWait;

        /// <summary>Cada frame, mientras espera el hueco del Escudo: suelta el rápido en cuanto, al llegar, no habrá escudo.</summary>
        private void UpdateShieldGap()
        {
            if (Time.time >= _shieldGapUntil) return;
            if (Busy || Evading || Soaked || Blade.Attack.CurrentCharges <= 0 || Perception.Target == null ||
                Perception.Distance > StrikeDistance + 0.5f)
                return;
            if (RivalShieldBlocks(StrikeTime(0))) return;
            _shieldGapUntil = 0f;
            StartAttack(0);
        }

        /// <summary>
        /// Cargado listo contra el Escudo del rival: lo sigue cargando hasta tener un hueco delante, mientras quede sitio
        /// (más cerca, al seguir yendo hacia él chocaría contra el escudo sin soltarlo).
        /// </summary>
        protected bool HoldsForShieldGap() => Perception.Gap > ShieldHoldGap && RivalShieldBlocks(StrikeTime(Mathf.Max(2, Blade.Attack.ChargeLevel)));

        /// <summary>
        /// Lo que tardará en llegar al rival un golpe de este nivel que salga ya: hueco / (impulso del nivel + lo que el
        /// rival viene hacia ella) + StrikeDelay. La velocidad que ya lleva no cuenta (el acelerón la sustituye; sumándola
        /// salía 0,1 s corto, medio hueco del Escudo). El hueco que ve llega con su retraso: lo adelanta con lo que se
        /// acerca el rival en ese tiempo (sin eso, con el rival viniendo, salía hasta 0,15 s largo).
        /// </summary>
        protected float StrikeTime(int level)
        {
            AIPerception p = Perception;
            float rivalToward = Mathf.Max(0f, Vector3.Dot(p.TargetVelocity, -p.ToTarget));
            float gap = Mathf.Max(0f, p.Gap - rivalToward * Level.perceptionDelay);
            var cfg = CombatConfig.Active;
            return gap / (cfg.quickAttackImpulse * (1f + cfg.chargedImpulsePerLevel * level) + rivalToward) + StrikeDelay;
        }

        /// <summary>Rayos activo: carrerilla antes de cada golpe (el golpe fuerte pide llegar a 6 m/s), si hay sitio.</summary>
        protected bool LightningRunUp => ActivePower is LightningAbility && Perception.Distance < RunUpDistance && HasRoomBehind();

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
            if (PowerStillOut) return true;
            if (_readySince < 0f) _readySince = Time.time;

            // Rayos (capa táctica): hasta tener al rival junto a la pared (el golpe fuerte lo lanza contra ella)
            if (own is LightningAbility && Level.tactical && Perception.Target != null)
                return Perception.TargetWallDistance > LightningWallDistance && Time.time - _readySince < LeapMaxWait;

            // Salto (si predice): no salta mientras el rival se mueve de lado deprisa; en lo que tarda el salto (casi 1 s)
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

        /// <summary>
        /// Su poder sigue fuera y reactivarlo ahora casi no le da nada: Escudo con más de un escudo puesto o Fantasma con
        /// el clon todavía entero (lo vuelve a sacar al romperse).
        /// </summary>
        protected bool PowerStillOut => ShieldsStillUp || GhostOut;

        /// <summary>Escudo con más de un escudo puesto.</summary>
        private bool ShieldsStillUp => Blade.Special.Ability is ShieldsAbility shields && shields.ShieldsUp > 1;

        /// <summary>El clon de su Fantasma está en la arena.</summary>
        private bool GhostOut => Blade.GhostClone != null && Blade.GhostClone.gameObject.activeSelf;
        #endregion
    }
}
