using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Efectos de partículas continuos de una peonza (GDD 7.3):
    /// - Estela según la velocidad (más grande y opaca durante ataques y dash).
    /// - Chispas contra el suelo al moverse rápido con muchas RPM.
    /// - Humo y chispas sueltas con RPM bajas (acompañan al bamboleo, GDD 2.1).
    /// - Carga del ataque cargado: partículas que convergen, anillo en cada nivel y llamas al máximo.
    /// - Aura propia de cada poder activo (GDD 5).
    /// - Estado alterado (GDD 5): partículas del estado sobre la peonza e icono flotante encima.
    ///
    /// Clase C# pura que posee FakeBladeController. Emite con el pool compartido de VfxSystem
    /// (cero instancias y cero GC), hace el culling una vez por frame y escala las tasas
    /// con Opciones → Partículas.
    /// </summary>
    public sealed class BladeParticles
    {
        private const int MaxPerFrame = 12;
        private const float TrailSpeedForFullAlpha = 12f;
        private const float ChargeGatherLife = 0.3f;
        private const float ShockWaveRippleInterval = 0.4f;
        private static readonly Color FireHotColor = new Color(1f, 0.9f, 0.35f);

        private readonly FakeBladeController _blade;
        private readonly Collider _collider;

        // Medidas de la peonza (se toman del collider la primera vez que se dibuja)
        private bool _measured;
        private float _tipOffset = -0.2f;   // punta (apoyo en el suelo) respecto al pivote
        private float _centerOffset = 0.3f; // centro del cuerpo respecto al pivote
        private float _topOffset = 0.6f;    // parte de arriba del cuerpo respecto al pivote
        private float _radius = 0.35f;

        private Vector3 _lastTrailPos;
        private bool _hasTrailPos;
        private float _sparkAcc, _smokeAcc, _gatherAcc, _flameAcc, _auraAcc, _auraAcc2;
        private float _auraAngle, _auraTimer;
        private int _auraIndex;
        private int _lastChargeLevel;
        private float _statusAcc;

        // Icono del estado alterado (se crea la primera vez que hace falta)
        private const float IconGap = 0.35f;
        private SpriteRenderer _statusIcon;
        private StatusEffectType _iconStatus;
        private static Camera s_camera;
        private static int s_cameraFrame = -1;

        public BladeParticles(FakeBladeController blade)
        {
            _blade = blade;
            _collider = blade.GetComponent<Collider>();
        }

        private float AuraRadius => Mathf.Max(0.5f, _radius * 1.5f);

        /// <summary>Limpia el estado (al empezar, reaparecer o cambiar piezas).</summary>
        public void Reset()
        {
            _measured = false;
            _hasTrailPos = false;
            _sparkAcc = _smokeAcc = _gatherAcc = _flameAcc = _auraAcc = _auraAcc2 = _statusAcc = 0f;
            _auraTimer = 0f;
            _lastChargeLevel = 0;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            Vector3 pos = _blade.Position;
            VfxLibrary lib = VfxSystem.Library;
            if (lib != null) UpdateStatusIcon(lib);
            if (lib == null || !VfxSystem.IsOnScreen(pos))
            {
                _hasTrailPos = false;
                return;
            }
            if (!_measured) Measure(pos);

            float mul = SettingsService.ParticleMultiplier;
            Vector3 tip = new Vector3(pos.x, pos.y + _tipOffset, pos.z);
            Vector3 center = new Vector3(pos.x, pos.y + _centerOffset, pos.z);
            Vector3 velocity = _blade.Velocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontal.magnitude;
            float spin = _blade.SpinSpeedPercentage;
            Color owner = _blade.OwnerColor;

            UpdateTrail(lib, tip, speed, mul, owner);
            UpdateGroundSparks(lib, tip, horizontal, speed, spin, mul, dt);
            UpdateLowSpin(lib, center, tip, spin, mul, dt);
            UpdateCharge(lib, center, tip, mul, owner, dt);
            UpdateAura(lib, center, tip, mul, dt);
            UpdateStatusParticles(lib, center, tip, mul, dt);
        }

        private void Measure(Vector3 pos)
        {
            _measured = true;
            if (_collider == null || !_collider.enabled) return;

            Bounds bounds = _collider.bounds;
            if (bounds.size.sqrMagnitude < 0.0001f) return;
            _tipOffset = Mathf.Clamp(bounds.min.y - pos.y, -2f, 0.5f);
            _centerOffset = Mathf.Clamp(bounds.center.y - pos.y, -1f, 2f);
            _topOffset = Mathf.Clamp(bounds.max.y - pos.y, 0f, 3f);
            _radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.15f, 2f);
        }

        #region Movement
        /// <summary>Estela: una partícula cada trailSpacing metros (densidad independiente de los FPS).</summary>
        private void UpdateTrail(VfxLibrary lib, Vector3 tip, float speed, float mul, Color owner)
        {
            if (!_hasTrailPos || speed < lib.trailMinSpeed)
            {
                _lastTrailPos = tip;
                _hasTrailPos = true;
                return;
            }

            float spacing = Mathf.Max(0.02f, lib.trailSpacing / Mathf.Max(0.1f, mul));
            Vector3 delta = tip - _lastTrailPos;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist < spacing) return;

            bool attacking = _blade.IsAttacking;
            Color color = owner;
            color.a = attacking ? 1f : Mathf.Lerp(0.35f, 0.8f, speed / TrailSpeedForFullAlpha);
            float size = attacking ? 0.16f : 0.1f;
            Vector3 step = delta / dist * spacing;

            int count = Mathf.Min(MaxPerFrame, (int)(dist / spacing));
            for (int i = 0; i < count; i++)
            {
                _lastTrailPos += step;
                Vector3 p = _lastTrailPos + Jitter(0.05f);
                p.y = tip.y + 0.04f;
                VfxSystem.EmitParticle(VfxType.Trail, p, Vector3.zero, color, size);
            }
            if (count == MaxPerFrame) _lastTrailPos = tip; // salto enorme (teletransporte o tirón)
        }

        /// <summary>Chispas de la punta contra el suelo: salen hacia atrás al ir rápido con muchas RPM.</summary>
        private void UpdateGroundSparks(VfxLibrary lib, Vector3 tip, Vector3 horizontal, float speed, float spin, float mul, float dt)
        {
            float amount = Mathf.InverseLerp(lib.groundSparkMinSpeed, lib.groundSparkMinSpeed * 2.5f, speed)
                           * Mathf.InverseLerp(lib.groundSparkMinSpin, 1f, spin);
            int count = Accumulate(ref _sparkAcc, lib.groundSparkRate * amount * mul, dt);
            if (count == 0) return;

            Vector3 back = -horizontal / Mathf.Max(0.01f, speed);
            Vector3 side = new Vector3(-back.z, 0f, back.x);
            for (int i = 0; i < count; i++)
            {
                Vector3 v = back * Random.Range(1.5f, 4f) + side * Random.Range(-2f, 2f) + Vector3.up * Random.Range(1.5f, 3.5f);
                VfxSystem.EmitParticle(VfxType.GroundSpark, tip + Jitter(0.08f), v, lib.groundSparkColor);
            }
        }

        /// <summary>RPM bajas: humo que sube y chispas sueltas e irregulares desde el borde.</summary>
        private void UpdateLowSpin(VfxLibrary lib, Vector3 center, Vector3 tip, float spin, float mul, float dt)
        {
            float low = CombatConfig.Active.LowSpinFactor(spin);
            if (low <= 0f)
            {
                _smokeAcc = 0f;
                return;
            }

            int smoke = Accumulate(ref _smokeAcc, lib.lowSpinSmokeRate * (0.25f + 0.75f * low) * mul, dt);
            for (int i = 0; i < smoke; i++)
            {
                Vector3 p = center + Jitter(_radius * 0.7f) + Vector3.up * Random.Range(0f, 0.2f);
                Vector3 v = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.5f, 1.1f), Random.Range(-0.25f, 0.25f));
                VfxSystem.EmitParticle(VfxType.LowSpinSmoke, p, v, lib.smokeColor, Random.Range(0.16f, 0.26f) * (0.8f + 0.4f * low));
            }

            // Chispas a ráfagas aleatorias (media de 2 por ráfaga)
            if (Random.value < lib.lowSpinSparkRate * low * mul * dt * 0.5f)
            {
                int sparks = Random.Range(1, 4);
                for (int i = 0; i < sparks; i++)
                {
                    Vector3 dir = RandomFlatDirection();
                    Vector3 p = new Vector3(center.x, tip.y + 0.08f, center.z) + dir * _radius;
                    Vector3 v = dir * Random.Range(1.5f, 3.5f) + Vector3.up * Random.Range(1f, 2.5f);
                    VfxSystem.EmitParticle(VfxType.LowSpinSpark, p, v, lib.lowSpinSparkColor);
                }
            }
        }
        #endregion

        #region Charged attack
        /// <summary>
        /// Carga: partículas que convergen (más, más grandes y más calientes con cada nivel),
        /// un anillo en el suelo al subir de nivel y, al máximo, destello en estrella y llamas.
        /// </summary>
        private void UpdateCharge(VfxLibrary lib, Vector3 center, Vector3 tip, float mul, Color owner, float dt)
        {
            AttackSystem attack = _blade.Attack;
            if (!attack.IsCharging || _blade.IsStaggered)
            {
                _lastChargeLevel = 0;
                _gatherAcc = _flameAcc = 0f;
                return;
            }

            int level = attack.ChargeLevel;
            bool atMax = attack.ChargeLevelProgress >= 1f;
            float heat = attack.MaxCharges <= 1 ? 1f : Mathf.Clamp01((level - 1f) / (attack.MaxCharges - 1f));
            Color color = Color.Lerp(owner, lib.chargeHotColor, heat);
            Vector3 ground = new Vector3(center.x, tip.y + 0.05f, center.z);

            if (level > _lastChargeLevel)
            {
                float ringSize = AuraRadius * ((atMax ? 4.2f : 2.6f) + 0.3f * level);
                VfxSystem.EmitParticle(VfxType.ChargeRing, ground, Vector3.zero, color, ringSize);
                if (atMax) VfxSystem.Play(VfxType.Sparkle, center, Vector3.up, lib.chargeHotColor);
                _lastChargeLevel = level;
            }

            int gather = Accumulate(ref _gatherAcc, lib.chargeGatherRate * (1f + 0.5f * (level - 1)) * mul, dt);
            float size = 0.06f + 0.025f * level;
            for (int i = 0; i < gather; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.7f + 0.05f;
                dir.Normalize();
                float dist = Random.Range(1.8f, 2.6f) * AuraRadius;
                VfxSystem.EmitParticle(VfxType.ChargeGather, center + dir * dist, -dir * (dist / ChargeGatherLife),
                    color, size, ChargeGatherLife);
            }

            if (!atMax) return;
            int flames = Accumulate(ref _flameAcc, lib.chargeFlameRate * mul, dt);
            for (int i = 0; i < flames; i++)
            {
                // Por el borde: dentro del radio las taparía el propio cuerpo de la peonza
                Vector3 p = ground + RandomFlatDirection() * (_radius * Random.Range(0.9f, 1.3f));
                Vector3 v = new Vector3(0f, Random.Range(1.2f, 2.2f), 0f);
                VfxSystem.EmitParticle(VfxType.ChargeFlame, p, v, (i & 1) == 0 ? lib.chargeHotColor : color);
            }
        }
        #endregion

        #region Special auras
        private void UpdateAura(VfxLibrary lib, Vector3 center, Vector3 tip, float mul, float dt)
        {
            SpecialAbilitySystem special = _blade.Special;
            if (!special.IsActive)
            {
                _auraAcc = _auraAcc2 = 0f;
                _auraTimer = 0f;
                return;
            }

            Color color = special.Color;
            switch (special.Type)
            {
                case SpecialAbilityType.SpinBoost: AuraSpinBoost(center, tip, color, mul, dt); break;
                case SpecialAbilityType.ShockWave: AuraShockWave(center, tip, color, mul, dt); break;
                case SpecialAbilityType.Defense: AuraDefense(center, color, mul, dt); break;
                case SpecialAbilityType.Lightning: AuraLightning(center, color, mul, dt); break;
                case SpecialAbilityType.Fire: AuraFire(center, tip, color, mul, dt); break;
                case SpecialAbilityType.Ice: AuraIce(center, tip, color, mul, dt); break;
                default:
                    VfxSystem.EmitCount(VfxType.SpecialAura, center, color, Accumulate(ref _auraAcc, 40f * mul, dt));
                    break;
            }
        }

        /// <summary>Spin Boost: doble espiral verde que sube alrededor de la peonza (recupera RPM).</summary>
        private void AuraSpinBoost(Vector3 center, Vector3 tip, Color color, float mul, float dt)
        {
            _auraAngle = Mathf.Repeat(_auraAngle + dt * 720f * Mathf.Deg2Rad, Mathf.PI * 2f);
            float r = AuraRadius;
            int count = Accumulate(ref _auraAcc, 48f * mul, dt);
            for (int i = 0; i < count; i++)
            {
                // Alterna entre los dos brazos de la espiral
                _auraIndex++;
                float a = _auraAngle + ((_auraIndex & 1) == 0 ? 0f : Mathf.PI);
                Vector3 offset = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Vector3 p = new Vector3(center.x, tip.y + 0.05f, center.z) + offset;
                VfxSystem.EmitParticle(VfxType.AuraSpinBoost, p, Vector3.up * 1.8f - offset * 0.4f, color);
            }
        }

        /// <summary>Onda de choque: ondas naranjas que se expanden por el suelo y chispas a ras de suelo.</summary>
        private void AuraShockWave(Vector3 center, Vector3 tip, Color color, float mul, float dt)
        {
            Vector3 ground = new Vector3(center.x, tip.y + 0.04f, center.z);
            _auraTimer -= dt;
            if (_auraTimer <= 0f)
            {
                _auraTimer = ShockWaveRippleInterval;
                VfxSystem.EmitParticle(VfxType.AuraShockWave, ground, Vector3.zero, color, AuraRadius * 3.2f);
            }

            int sparks = Accumulate(ref _auraAcc, 14f * mul, dt);
            for (int i = 0; i < sparks; i++)
            {
                Vector3 dir = RandomFlatDirection();
                Vector3 v = dir * Random.Range(2.5f, 4f) + Vector3.up * Random.Range(0.5f, 1.5f);
                VfxSystem.EmitParticle(VfxType.GroundSpark, ground + dir * (AuraRadius * 0.6f), v, color);
            }
        }

        /// <summary>Defensa: anillo protector azul que gira alrededor y destellos en una cúpula.</summary>
        private void AuraDefense(Vector3 center, Color color, float mul, float dt)
        {
            const float angularSpeed = 900f * Mathf.Deg2Rad;
            _auraAngle = Mathf.Repeat(_auraAngle + dt * angularSpeed, Mathf.PI * 2f);
            float r = AuraRadius * 1.1f;

            // Tres cabezas a 120º; cada partícula vive ~0,2 s y deja un arco que sigue a la peonza
            int count = Accumulate(ref _auraAcc, 150f * mul, dt);
            for (int i = 0; i < count; i++)
            {
                _auraIndex++;
                float a = _auraAngle + (_auraIndex % 3) * (Mathf.PI * 2f / 3f) - Random.value * dt * angularSpeed;
                Vector3 p = new Vector3(center.x + Mathf.Cos(a) * r, center.y, center.z + Mathf.Sin(a) * r);
                VfxSystem.EmitParticle(VfxType.AuraDefense, p, Vector3.zero, color);
            }

            int shimmer = Accumulate(ref _auraAcc2, 10f * mul, dt);
            for (int i = 0; i < shimmer; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y);
                VfxSystem.EmitParticle(VfxType.Sparkle, center + dir * r, Vector3.zero, color, 0.14f, 0.25f);
            }
        }

        /// <summary>Rayos: rayos amarillos que chisporrotean alrededor y chispas que saltan.</summary>
        private void AuraLightning(Vector3 center, Color color, float mul, float dt)
        {
            float r = AuraRadius;
            if (Random.value < 22f * mul * dt)
            {
                int bolts = Random.Range(1, 3);
                for (int i = 0; i < bolts; i++)
                {
                    Vector3 p = center + RandomFlatDirection() * (r * Random.Range(0.5f, 1.1f)) + Vector3.up * Random.Range(-0.15f, 0.35f);
                    // Giros de 90º para que el rayo pixel no se deforme
                    VfxSystem.EmitParticle(VfxType.AuraLightning, p, Vector3.zero, color,
                        Random.Range(0.35f, 0.5f), 0f, 90f * Random.Range(0, 4));
                }
            }

            int sparks = Accumulate(ref _auraAcc, 16f * mul, dt);
            for (int i = 0; i < sparks; i++)
            {
                Vector3 dir = RandomFlatDirection();
                Vector3 v = dir * Random.Range(2f, 4f) + Vector3.up * Random.Range(1f, 3f);
                VfxSystem.EmitParticle(VfxType.GroundSpark, center + dir * (r * 0.5f), v, color);
            }
        }

        /// <summary>Fuego: corona de llamas alrededor de la peonza (rojas y amarillas) y brasas que saltan.</summary>
        private void AuraFire(Vector3 center, Vector3 tip, Color color, float mul, float dt)
        {
            Vector3 ground = new Vector3(center.x, tip.y + 0.05f, center.z);
            Color hot = Color.Lerp(color, FireHotColor, 0.6f);
            float r = AuraRadius;

            int flames = Accumulate(ref _auraAcc, 120f * mul, dt);
            for (int i = 0; i < flames; i++)
            {
                // Por fuera del cuerpo y algo hacia dentro al subir, como una hoguera alrededor
                Vector3 dir = RandomFlatDirection();
                Vector3 p = ground + dir * (r * Random.Range(0.9f, 1.25f));
                Vector3 v = Vector3.up * Random.Range(1.8f, 3.2f) - dir * 0.5f;
                VfxSystem.EmitParticle(VfxType.ChargeFlame, p, v, i % 3 == 0 ? hot : color, Random.Range(0.3f, 0.45f));
            }

            int embers = Accumulate(ref _auraAcc2, 16f * mul, dt);
            for (int i = 0; i < embers; i++)
            {
                Vector3 dir = RandomFlatDirection();
                Vector3 v = dir * Random.Range(1f, 2.5f) + Vector3.up * Random.Range(2.5f, 4f);
                VfxSystem.EmitParticle(VfxType.LowSpinSpark, center + dir * r, v, hot);
            }
        }

        /// <summary>Hielo: cristales que flotan alrededor de la peonza y humo blanco de frío que se arrastra por el suelo.</summary>
        private void AuraIce(Vector3 center, Vector3 tip, Color color, float mul, float dt)
        {
            float r = AuraRadius;

            int crystals = Accumulate(ref _auraAcc, 40f * mul, dt);
            for (int i = 0; i < crystals; i++)
            {
                Vector3 dir = RandomFlatDirection();
                Vector3 p = center + dir * (r * Random.Range(0.8f, 1.2f)) + Vector3.up * Random.Range(-0.1f, 0.5f);
                Color c = (i & 1) == 0 ? color : Color.white;
                VfxSystem.EmitParticle(VfxType.Sparkle, p, Vector3.up * 0.25f, c, Random.Range(0.16f, 0.26f), 0.45f);
            }

            int mist = Accumulate(ref _auraAcc2, 14f * mul, dt);
            for (int i = 0; i < mist; i++)
            {
                Vector3 dir = RandomFlatDirection();
                Color c = Color.Lerp(Color.white, color, 0.2f);
                c.a = 0.6f;
                VfxSystem.EmitParticle(VfxType.LowSpinSmoke, tip + Vector3.up * 0.08f + dir * (r * 0.7f),
                    dir * Random.Range(0.4f, 0.9f) + Vector3.down * 0.05f, c, Random.Range(0.26f, 0.38f));
            }
        }
        #endregion

        #region Status effects
        /// <summary>
        /// Partículas del estado alterado sobre la peonza: llamas que suben (quemada), escarcha y
        /// vaho frío (congelada) o rayos y chispas que saltan (lanzada).
        /// </summary>
        private void UpdateStatusParticles(VfxLibrary lib, Vector3 center, Vector3 tip, float mul, float dt)
        {
            StatusEffectType status = _blade.Status.Current;
            if (status == StatusEffectType.None)
            {
                _statusAcc = 0f;
                return;
            }

            Color color = lib.GetStatusColor(status);
            int count = Accumulate(ref _statusAcc, lib.statusParticleRate * mul, dt);
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = RandomFlatDirection();
                switch (status)
                {
                    case StatusEffectType.Burning:
                        VfxSystem.EmitParticle(VfxType.ChargeFlame,
                            center + dir * (_radius * Random.Range(0.4f, 1f)) + Vector3.up * Random.Range(-0.1f, 0.2f),
                            Vector3.up * Random.Range(1f, 1.8f), color);
                        break;

                    case StatusEffectType.Frozen:
                        if ((i & 1) == 0)
                        {
                            VfxSystem.EmitParticle(VfxType.Sparkle, center + Random.onUnitSphere * _radius,
                                Vector3.zero, color, 0.12f, 0.3f);
                        }
                        else
                        {
                            Color mist = Color.Lerp(Color.white, color, 0.3f);
                            mist.a = 0.5f;
                            VfxSystem.EmitParticle(VfxType.LowSpinSmoke, tip + Vector3.up * 0.1f + dir * (_radius * 0.8f),
                                dir * 0.3f + Vector3.down * 0.1f, mist, 0.18f);
                        }
                        break;

                    case StatusEffectType.Launched:
                        if (i % 3 == 0)
                        {
                            Vector3 p = center + dir * (_radius * Random.Range(0.6f, 1.1f)) + Vector3.up * Random.Range(-0.1f, 0.3f);
                            VfxSystem.EmitParticle(VfxType.AuraLightning, p, Vector3.zero, color, 0.3f, 0f, 90f * Random.Range(0, 4));
                        }
                        else
                        {
                            VfxSystem.EmitParticle(VfxType.GroundSpark, center + dir * (_radius * 0.6f),
                                dir * Random.Range(2f, 4f) + Vector3.up * Random.Range(1f, 2.5f), color);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Icono del estado encima de la peonza, siempre de cara a la cámara, teñido con el color
        /// del estado y parpadeando por pasos en el último 30% de su duración.
        /// </summary>
        private void UpdateStatusIcon(VfxLibrary lib)
        {
            StatusEffectSystem status = _blade.Status;
            if (!status.HasStatus)
            {
                if (_statusIcon != null && _statusIcon.enabled) _statusIcon.enabled = false;
                return;
            }

            if (_statusIcon == null) CreateStatusIcon();

            if (_iconStatus != status.Current)
            {
                _iconStatus = status.Current;
                _statusIcon.sprite = StatusIcons.Get(_iconStatus);
                _statusIcon.color = lib.GetStatusColor(_iconStatus);
            }

            _statusIcon.enabled = status.RemainingFraction > 0.3f || Mathf.FloorToInt(Time.time * 12f) % 2 == 0;

            // Encima de la peonza en pantalla (hacia el "arriba" de la cámara), mirando a la cámara
            Transform icon = _statusIcon.transform;
            Vector3 top = _blade.Position + Vector3.up * _topOffset;
            Camera cam = MainCamera;
            if (cam != null)
            {
                Transform camTransform = cam.transform;
                icon.SetPositionAndRotation(top + camTransform.up * (_radius + IconGap), camTransform.rotation);
            }
            else
            {
                icon.position = top + Vector3.up * IconGap;
            }
        }

        /// <summary>Oculta el icono al momento (al quitar el estado, también si la peonza ya no se actualiza).</summary>
        public void HideStatusIcon()
        {
            if (_statusIcon != null) _statusIcon.enabled = false;
        }

        private void CreateStatusIcon()
        {
            var go = new GameObject("StatusIcon");
            go.transform.SetParent(_blade.transform, false); // el root nunca rota
            _statusIcon = go.AddComponent<SpriteRenderer>();
            _statusIcon.sortingOrder = 50;
            _iconStatus = StatusEffectType.None;
        }

        private static Camera MainCamera
        {
            get
            {
                if (s_cameraFrame != Time.frameCount)
                {
                    s_cameraFrame = Time.frameCount;
                    if (s_camera == null || !s_camera.isActiveAndEnabled) s_camera = Camera.main;
                }
                return s_camera;
            }
        }
        #endregion

        #region Helpers
        /// <summary>Convierte una tasa (partículas/s) en partículas de este frame sin perder los decimales.</summary>
        private static int Accumulate(ref float accumulator, float rate, float dt)
        {
            if (rate <= 0f)
            {
                accumulator = 0f;
                return 0;
            }
            accumulator += rate * dt;
            int count = (int)accumulator;
            accumulator -= count;
            return Mathf.Min(count, MaxPerFrame);
        }

        private static Vector3 Jitter(float radius)
        {
            Vector2 c = Random.insideUnitCircle * radius;
            return new Vector3(c.x, 0f, c.y);
        }

        private static Vector3 RandomFlatDirection()
        {
            float a = Random.value * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }
        #endregion
    }
}
