using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Partículas, manchas y auras de los especiales y de los estados (K8, GDD 7.6), con los emisores compartidos de
    /// VfxSystem:
    /// - Especial (VfxLibrary.specialEffects[...]): mientras dura la carga del principio, partículas en remolino; al
    ///   encenderse, una explosión en espiral; después, el remolino más flojo, su remolino propio (Hielo: vaho a ras de
    ///   suelo), su lista de partículas (en el Fuego, chispas que crepitan y ceniza que sube), las manchas en el suelo por
    ///   donde pasa (quemado, escarcha), sus auras de paquetes (Aura Smoke) y un círculo de partículas en cada choque
    ///   (PlayClashImpact, desde VfxSystem).
    /// - Peonza quemada o congelada (VfxLibrary.burn / frozen): sus partículas, su remolino, sus auras (Aura Burn y Aura
    ///   Smoke en la quemada) y un círculo más pequeño al chocar.
    /// Lo demás lo pintan BladeBurstTip (punta), BladeOverlay (sobre la peonza) y BladePartSpin (giro del disco).
    ///
    /// Clase C# pura que posee FakeBladeController. Emite partículas de pools que ya existen y la copia de cada aura se crea
    /// una sola vez, en el primer frame de la peonza: cero GC en combate. Los valores se leen en vivo de la biblioteca.
    /// </summary>
    public sealed class BladeSpecialFx
    {
        private const int MaxStyles = 8;
        private const int MaxAuras = 4;
        private const int MaxParticlesPerFrame = 12;
        private const int MaxBurstParticles = 80;

        /// <summary>Copia de un prefab de aura pegada a la peonza.</summary>
        private sealed class AuraSlot
        {
            public ParticleSystem Instance;
            public ParticleSystem Source;
            public ParticleSystem[] Systems;
            public Quaternion BaseRotation;
            public Vector3 BaseScale;
            public bool Playing;
            public float Angle;
            public float SimulationSpeed = -1f;
            /// <summary>Color de salida de cada sistema en el prefab (para quitar el tinte).</summary>
            public ParticleSystem.MinMaxGradient[] Original;
            public bool Tinted;
            public Color TintColor;
            /// <summary>Sistemas apagados por nombre (el texto de hideSystems aplicado; null = ninguno todavía).</summary>
            public string Hidden;
        }

        private readonly FakeBladeController _blade;
        // Por partícula de la lista: acumulado del ritmo y espera hasta el próximo chasquido (especial y estado; los estados
        // van de uno en uno, así que comparten los suyos)
        private readonly float[] _specialAcc = new float[MaxStyles];
        private readonly float[] _specialBurst = new float[MaxStyles];
        private readonly float[] _statusAcc = new float[MaxStyles];
        private readonly float[] _statusBurst = new float[MaxStyles];
        private readonly float[] _windupAcc = new float[MaxStyles];
        private readonly float[] _specialSwirlAcc = new float[MaxStyles];
        private readonly float[] _statusSwirlAcc = new float[MaxStyles];
        // Estela de la peonza lanzada por un poder con su propia estela (Onda de choque)
        private readonly float[] _launchAcc = new float[MaxStyles];
        private readonly float[] _launchBurst = new float[MaxStyles];
        // Una lista de auras por estado: cambiar de lista rehace las copias, y eso no se hace en combate
        private readonly AuraSlot[] _specialAuras = new AuraSlot[MaxAuras];
        private readonly AuraSlot[] _burnAuras = new AuraSlot[MaxAuras];
        private readonly AuraSlot[] _frozenAuras = new AuraSlot[MaxAuras];
        private readonly AuraSlot[] _launchedAuras = new AuraSlot[MaxAuras];
        private bool _effectsWereOn;
        private bool _wasWindingUp;
        private bool _wasSpecialActive;
        private float _scorchDistance;
        private float _scorchIdle;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        // Estela en la golpeada: el estilo y el color del poder de quien la golpeó, y lo que le queda
        private SpecialVfxStyle _hitTrailStyle;
        private Color _hitTrailPower;
        private float _hitTrailLeft;
        private readonly float[] _hitAcc = new float[MaxStyles];
        private readonly float[] _hitBurst = new float[MaxStyles];
        // Arrancada (Embestida mientras embiste)
        private readonly float[] _rushAcc = new float[MaxStyles];
        private readonly float[] _rushBurst = new float[MaxStyles];

        /// <summary>Color del poder de lo que se emite ahora (Power Tint de cada partícula); se fija antes de cada tanda.</summary>
        private static Color s_power = Color.white;

        public BladeSpecialFx(FakeBladeController blade)
        {
            _blade = blade;
            for (int i = 0; i < MaxAuras; i++)
            {
                _specialAuras[i] = new AuraSlot();
                _burnAuras[i] = new AuraSlot();
                _frozenAuras[i] = new AuraSlot();
                _launchedAuras[i] = new AuraSlot();
            }
        }

        /// <summary>
        /// tipOffset: altura de la punta respecto al pivote (BladeParticles). special: efectos del especial (o null).
        /// specialActive: el poder está activo; windingUp: todavía en la carga del principio (sus efectos esperan).
        /// </summary>
        public void Tick(float dt, float tipOffset, SpecialVfxStyle special, bool specialActive, bool windingUp)
        {
            VfxLibrary lib = VfxSystem.Library;
            Vector3 pos = _blade.Position;
            var tip = new Vector3(pos.x, pos.y + tipOffset, pos.z);
            bool visible = lib != null && VfxSystem.IsOnScreen(tip);

            // Color del poder para las partículas con Power Tint (la plantilla común se tiñe con él)
            s_power = _blade.Special != null ? _blade.Special.Color : Color.white;
            bool active = specialActive && special != null;
            bool effectsOn = active && !windingUp;
            Vector3 motion = MotionDirection();
            // El remolino de la carga sigue, más flojo, mientras dura el poder (2026-10-17)
            float swirl = !active || !visible ? 0f : windingUp ? 1f : special.windupSustain;
            // Las de solo la carga (windupOnly): la primera nada más empezar cada carga y ninguna después
            EmitSpirals(swirl > 0f ? special.windup : null, _windupAcc, swirl, tip, dt, motion, !windingUp, windingUp && !_wasWindingUp);
            // Explosión al encenderse; también al acabar una carga que gasta el poder (la última onda de Onda de choque, que
            // lo termina en ese momento: los efectos no llegan a encenderse)
            bool released = effectsOn && !_effectsWereOn;
            if (!released && _wasWindingUp && !windingUp && !specialActive && !_blade.IsDestroyed) released = true;
            if (released && visible && special != null) Burst(special.activationBurst, tip, motion);
            // Al terminar el poder (Rebote: la burbuja se rompe), salvo si la peonza ha caído
            if (_wasSpecialActive && !specialActive && !_blade.IsDestroyed && visible && special != null)
                Burst(special.endBurst, tip, motion);
            _wasSpecialActive = specialActive;
            // Marca donde va a caer (Salto, 2026-10-21): dura hasta que llega (un círculo que se cierra acaba al caer)
            SpecialAbility ability = _blade.Special != null ? _blade.Special.Ability : null;
            if (released && special != null && special.targetMark.Count > 0 && ability != null &&
                ability.TryGetTargetPoint(out Vector3 target, out float timeLeft))
            {
                target.y += tipOffset;
                // Junto a la pared, más pequeña: que no salga de la arena
                if (VfxSystem.IsOnScreen(target)) Burst(special.targetMark, target, motion, timeLeft, ArenaDefinition.MaxWidthAt(target));
            }
            _effectsWereOn = effectsOn;
            _wasWindingUp = windingUp;
            EmitSpirals(effectsOn && visible ? special.swirl : null, _specialSwirlAcc, 1f, tip, dt, motion);
            Emit(effectsOn && visible ? special.particles : null, _specialAcc, _specialBurst, tip, dt, motion);
            // Arrancada (Embestida mientras embiste, 2026-10-20; Salto mientras vuela): sus partículas y, si lo pide, el
            // rastro de manchas
            bool rushing = effectsOn && ability != null && ability.IsRushing;
            Emit(rushing && visible ? special.rush : null, _rushAcc, _rushBurst, tip, dt, motion);
            bool scorch = effectsOn && special.scorch != null && (!special.scorch.onlyWhileRushing || rushing);
            UpdateScorch(scorch ? special.scorch : null, tip, dt, motion);
            UpdateAuras(special != null ? special.auras : null, _specialAuras, effectsOn, tipOffset, dt);

            s_power = Color.white;
            StatusVfxStyle status = StatusStyle(_blade);
            Emit(status != null && visible ? status.particles : null, _statusAcc, _statusBurst, tip, dt, motion);
            EmitSpirals(status != null && visible ? status.swirl : null, _statusSwirlAcc, 1f, tip, dt, motion);
            UpdateAuras(lib != null ? lib.burn?.auras : null, _burnAuras, status != null && status == lib.burn, tipOffset, dt);
            UpdateAuras(lib != null ? lib.frozen?.auras : null, _frozenAuras, status != null && status == lib.frozen, tipOffset, dt);
            UpdateAuras(lib != null ? lib.launched?.auras : null, _launchedAuras, status != null && status == lib.launched, tipOffset, dt);
            // Lanzada por un poder con su propia estela (Onda de choque: polvo mientras vuela)
            SpecialVfxStyle thrower = status != null && status == lib.launched ? ThrowerEffects(_blade) : null;
            if (thrower != null) s_power = _blade.Status.Source.Special.Color;
            Emit(thrower != null && visible ? thrower.launchedTrail : null, _launchAcc, _launchBurst, tip, dt, motion);
            // Estela en la golpeada (2026-10-19): el rastro del poder de quien la golpeó, un momento tras cada golpe
            bool hitTrail = _hitTrailLeft > 0f && _hitTrailStyle != null && !_blade.IsDestroyed;
            if (hitTrail)
            {
                _hitTrailLeft -= dt;
                s_power = _hitTrailPower;
            }
            Emit(hitTrail && visible ? _hitTrailStyle.hitTrail : null, _hitAcc, _hitBurst, tip, dt, motion);
        }

        /// <summary>Hacia dónde va la peonza, en el suelo (si está casi parada, hacia donde mira).</summary>
        private Vector3 MotionDirection()
        {
            Vector3 v = _blade.Velocity;
            v.y = 0f;
            float speed = v.magnitude;
            if (speed > 0.5f) return v / speed;
            Vector3 facing = _blade.Facing;
            facing.y = 0f;
            return facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.forward;
        }

        /// <summary>Ángulo de una dirección en el suelo, en grados (0 = +Z, en el sentido de las agujas del reloj visto desde arriba).</summary>
        private static float Heading(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        /// <summary>Efectos del especial de quien dejó lanzada a esta peonza (o null).</summary>
        private static SpecialVfxStyle ThrowerEffects(FakeBladeController blade)
        {
            FakeBladeController source = blade.Status.Source;
            VfxLibrary lib = VfxSystem.Library;
            return source == null || source.Special == null || lib == null ? null : lib.GetSpecialEffectsOrGeneric(source.Special.Type);
        }

        /// <summary>
        /// Efectos del estado de la peonza que hay que pintar (quemada o congelada, con los de la biblioteca encendidos), o
        /// null si no tiene.
        /// </summary>
        public static StatusVfxStyle StatusStyle(FakeBladeController blade)
        {
            VfxLibrary lib = VfxSystem.Library;
            return lib == null || blade.IsDestroyed ? null : lib.GetStatusEffects(blade.Status.Current);
        }

        public void Clear()
        {
            for (int i = 0; i < MaxStyles; i++)
                _specialAcc[i] = _specialBurst[i] = _statusAcc[i] = _statusBurst[i] = _windupAcc[i] =
                    _specialSwirlAcc[i] = _statusSwirlAcc[i] = _launchAcc[i] = _launchBurst[i] = _rushAcc[i] = _rushBurst[i] = 0f;
            _hasLastPosition = false;
            _effectsWereOn = false;
            _wasWindingUp = false;
            _wasSpecialActive = false;
            _scorchDistance = _scorchIdle = 0f;
            for (int i = 0; i < MaxAuras; i++)
            {
                StopAura(_specialAuras[i]);
                StopAura(_burnAuras[i]);
                StopAura(_frozenAuras[i]);
                StopAura(_launchedAuras[i]);
            }
        }

        private static void StopAura(AuraSlot slot)
        {
            if (slot.Instance != null) slot.Instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            slot.Playing = false;
        }

        /// <summary>
        /// Auras de paquetes: la copia de cada prefab se crea la primera vez que se ve (en el primer frame de la peonza, no
        /// en un golpe), cuelga de la peonza en la punta, gira y va a su velocidad, y se enciende y apaga con on.
        /// </summary>
        private void UpdateAuras(List<AuraStyle> styles, AuraSlot[] slots, bool on, float tipOffset, float dt)
        {
            int count = styles != null ? Mathf.Min(styles.Count, MaxAuras) : 0;
            float rootScale = Mathf.Max(0.001f, _blade.transform.lossyScale.x);
            for (int i = 0; i < MaxAuras; i++)
            {
                AuraStyle style = i < count ? styles[i] : null;
                AuraSlot slot = slots[i];
                ParticleSystem prefab = style != null ? style.prefab : null;
                if (prefab != slot.Source) Rebuild(slot, prefab);
                if (slot.Instance == null) continue;

                // En metros del mundo (la raíz de la peonza puede estar escalada); gira alrededor de la vertical
                Transform t = slot.Instance.transform;
                t.localPosition = new Vector3(0f, (tipOffset + style.height) / rootScale, 0f);
                t.localScale = slot.BaseScale * (style.scale / rootScale);
                slot.Angle = Mathf.Repeat(slot.Angle + style.spin * dt, 360f);
                t.localRotation = Quaternion.Euler(0f, slot.Angle, 0f) * slot.BaseRotation;
                if (slot.SimulationSpeed != style.simulationSpeed)
                {
                    slot.SimulationSpeed = style.simulationSpeed;
                    for (int k = 0; k < slot.Systems.Length; k++)
                    {
                        ParticleSystem.MainModule main = slot.Systems[k].main;
                        main.simulationSpeed = style.simulationSpeed;
                    }
                }

                if (style.tint != slot.Tinted || (style.tint && style.tintColor != slot.TintColor))
                    ApplyTint(slot, style.tint, style.tintColor);
                if (!string.Equals(style.hideSystems ?? "", slot.Hidden)) ApplyHidden(slot, style.hideSystems ?? "");

                if (on == slot.Playing) continue;
                slot.Playing = on;
                if (on) slot.Instance.Play(true);
                else slot.Instance.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>
        /// Tinte de un aura (Aura Lightning en amarillo): cambia el color de salida de cada sistema, el de los que salen
        /// de un color y el segundo de los que salen al azar entre dos (la transparencia, la del prefab). Sin tinte, el
        /// del prefab. Solo cuando cambia: no se hace cada frame.
        /// </summary>
        private static void ApplyTint(AuraSlot slot, bool tint, Color color)
        {
            slot.Tinted = tint;
            slot.TintColor = color;
            for (int k = 0; k < slot.Systems.Length; k++)
            {
                ParticleSystem.MinMaxGradient original = slot.Original[k];
                ParticleSystem.MinMaxGradient result = original;
                if (tint && original.mode == ParticleSystemGradientMode.Color)
                    result = new ParticleSystem.MinMaxGradient(new Color(color.r, color.g, color.b, original.color.a));
                else if (tint && original.mode == ParticleSystemGradientMode.TwoColors)
                    result = new ParticleSystem.MinMaxGradient(original.colorMin,
                        new Color(color.r, color.g, color.b, original.colorMax.a));
                ParticleSystem.MainModule main = slot.Systems[k].main;
                main.startColor = result;
            }
        }

        /// <summary>
        /// Apaga los sistemas del aura cuyo nombre está en la lista (separados por comas; el humo amarillo del Aura
        /// Lightning parecía que la peonza apestaba) y enciende los demás. Solo cuando cambia la lista.
        /// </summary>
        private static void ApplyHidden(AuraSlot slot, string names)
        {
            slot.Hidden = names;
            string[] hidden = names.Split(',');
            for (int k = 0; k < slot.Systems.Length; k++)
            {
                ParticleSystem system = slot.Systems[k];
                if (system == slot.Instance) continue; // la raíz siempre sale
                bool hide = false;
                for (int h = 0; h < hidden.Length && !hide; h++)
                    hide = string.Equals(hidden[h].Trim(), system.name, System.StringComparison.OrdinalIgnoreCase);
                if (system.gameObject.activeSelf == hide) system.gameObject.SetActive(!hide);
            }
            if (slot.Playing) slot.Instance.Play(true);
        }

        private void Rebuild(AuraSlot slot, ParticleSystem prefab)
        {
            if (slot.Instance != null) Object.Destroy(slot.Instance.gameObject);
            slot.Instance = null;
            slot.Systems = null;
            slot.Playing = false;
            slot.SimulationSpeed = -1f;
            slot.Source = prefab;
            if (prefab == null) return;
            slot.Instance = Object.Instantiate(prefab, _blade.transform, false);
            slot.Systems = slot.Instance.GetComponentsInChildren<ParticleSystem>(true);
            slot.Original = new ParticleSystem.MinMaxGradient[slot.Systems.Length];
            for (int k = 0; k < slot.Systems.Length; k++) slot.Original[k] = slot.Systems[k].main.startColor;
            slot.Tinted = false;
            slot.Hidden = null;
            slot.BaseRotation = slot.Instance.transform.localRotation;
            slot.BaseScale = slot.Instance.transform.localScale;
            slot.Instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>
        /// Remolino: cada estilo a su ritmo, por rateScale (1 en la carga; menos mientras dura el poder). acc: acumulado
        /// del ritmo de cada estilo (uno por remolino: especial y quemadura).
        /// </summary>
        private static void EmitSpirals(List<SpiralParticleStyle> styles, float[] acc, float rateScale, Vector3 tip, float dt,
            Vector3 direction, bool sustained = false, bool windupStarted = false)
        {
            int count = styles != null ? Mathf.Min(styles.Count, MaxStyles) : 0;
            float mul = SettingsService.ParticleMultiplier * rateScale;
            for (int i = 0; i < MaxStyles; i++)
            {
                SpiralParticleStyle style = i < count ? styles[i] : null;
                if (style == null)
                {
                    acc[i] = 0f;
                    continue;
                }
                // Solo en la carga: fuera de ella no sale; al empezarla, la primera sale ya
                if (style.windupOnly && sustained)
                {
                    acc[i] = 0f;
                    continue;
                }
                if (style.windupOnly && windupStarted) acc[i] = Mathf.Max(acc[i], 1f);
                acc[i] += dt * style.rate * mul;
                int n = Mathf.Min((int)acc[i], MaxParticlesPerFrame);
                acc[i] -= (int)acc[i];
                for (int k = 0; k < n; k++) EmitSpiral(style, tip, Random.value * Mathf.PI * 2f, direction);
            }
        }

        /// <summary>
        /// Choque con daño (K8, 2026-10-17): si la peonza tiene encendido un especial con efectos de choque, su círculo de
        /// partículas (Fuego: llamas, chispas y ceniza; Hielo: esquirlas, destellos y vaho); si no, y está quemada o
        /// congelada, el de su estado (más pequeño). Se suma al impacto genérico. groundPoint: el punto del choque a ras
        /// de suelo.
        /// </summary>
        /// <summary>
        /// Golpe fuerte de un especial (Rayos, que gasta el poder en ese golpe; 2026-10-18): su círculo grande
        /// (strongImpact), aunque el poder ya no esté activo.
        /// </summary>
        public static void PlayStrongImpact(FakeBladeController blade, Vector3 groundPoint)
        {
            VfxLibrary lib = VfxSystem.Library;
            if (lib == null || blade == null || blade.Special == null || !VfxSystem.IsOnScreen(groundPoint)) return;
            SpecialVfxStyle style = lib.GetSpecialEffectsOrGeneric(blade.Special.Type);
            if (style == null) return;
            s_power = blade.Special.Color;
            Burst(style.strongImpact, groundPoint);
        }

        /// <summary>
        /// Empujón de un poder a un rival (cada onda de Onda de choque, el Tornado y cada golpe de la Embestida; 2026-10-18):
        /// su círculo pushHit en el lado del rival por el que le llega el golpe, a ras de suelo, girado hacia donde sale
        /// despedido (los que tienen Align To Direction). direction: hacia donde sale despedido. Devuelve false si el poder
        /// no tiene (el llamador pone entonces el efecto común).
        /// </summary>
        public static bool PlayPushHit(FakeBladeController owner, FakeBladeController target, Vector3 direction)
        {
            VfxLibrary lib = VfxSystem.Library;
            if (lib == null || owner == null || target == null || owner.Special == null) return false;
            SpecialVfxStyle style = lib.GetSpecialEffectsOrGeneric(owner.Special.Type);
            if (style == null || style.pushHit == null || style.pushHit.Count == 0) return false;
            Vector3 point = target.Position - direction * 0.45f;
            s_power = owner.Special.Color;
            if (VfxSystem.IsOnScreen(point)) Burst(style.pushHit, point, direction);
            return true;
        }

        /// <summary>
        /// Rebote (2026-10-21, boceto del usuario): la peonza que la embiste sale despedida. Zbam en la burbuja desde el punto
        /// del choque, el círculo pushHit en el lado del rival (girado hacia donde sale despedido) y su estela (hitTrail)
        /// mientras vuela. Devuelve false si el poder no tiene efectos propios (el llamador pone entonces los comunes).
        /// </summary>
        public static bool PlayBounce(FakeBladeController owner, FakeBladeController attacker, Vector3 contactPoint)
        {
            VfxLibrary lib = VfxSystem.Library;
            if (lib == null || owner == null || attacker == null || owner.Special == null) return false;
            SpecialVfxStyle style = lib.GetSpecialEffects(owner.Special.Type);
            if (style == null) return false;
            owner.PulseChargeDome(contactPoint);
            Vector3 away = attacker.Position - owner.Position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : owner.Facing;
            PlayPushHit(owner, attacker, away);
            if (style.hitTrail != null && style.hitTrail.Count > 0 && style.hitTrailTime > 0f && !attacker.IsDestroyed)
                attacker.StartSpecialHitTrail(style, owner.Special.Color);
            return true;
        }

        /// <summary>
        /// Suelta el empujón de un poder (Tornado, 2026-10-18): su explosión release alrededor de la peonza, a la altura de la
        /// punta. Devuelve false si el poder no tiene (el llamador pone entonces el efecto común).
        /// </summary>
        public static bool PlayRelease(FakeBladeController owner) => owner != null && PlayRelease(owner, owner.TipPosition);

        /// <summary>
        /// Como el de arriba, en un punto dado (el Salto al caer: la punta a la altura del suelo, porque al aterrizar con tanta
        /// gravedad se hunde un momento y los sprites tumbados quedarían bajo el suelo). maxSize: tamaño máximo de cada
        /// partícula (junto a la pared, para no salir de la arena).
        /// </summary>
        public static bool PlayRelease(FakeBladeController owner, Vector3 tip, float maxSize = float.MaxValue)
        {
            VfxLibrary lib = VfxSystem.Library;
            if (lib == null || owner == null || owner.Special == null) return false;
            SpecialVfxStyle style = lib.GetSpecialEffectsOrGeneric(owner.Special.Type);
            if (style == null || style.release == null || style.release.Count == 0) return false;
            s_power = owner.Special.Color;
            if (VfxSystem.IsOnScreen(tip)) Burst(style.release, tip, default, 0f, maxSize);
            return true;
        }

        /// <summary>
        /// Peonza lanzada que choca contra la pared (2026-10-18): el círculo launchedWallHit del poder de quien la lanzó (Onda
        /// de choque: polvo), en el punto del choque.
        /// </summary>
        public static void PlayLaunchedWallHit(FakeBladeController blade, Vector3 point)
        {
            if (blade == null || !VfxSystem.IsOnScreen(point)) return;
            SpecialVfxStyle style = ThrowerEffects(blade);
            if (style == null) return;
            s_power = blade.Status.Source != null && blade.Status.Source.Special != null ? blade.Status.Source.Special.Color : Color.white;
            Burst(style.launchedWallHit, point);
        }

        public static void PlayClashImpact(FakeBladeController blade, Vector3 groundPoint)
        {
            VfxLibrary lib = VfxSystem.Library;
            if (lib == null || blade == null || blade.IsDestroyed || !VfxSystem.IsOnScreen(groundPoint)) return;
            SpecialAbilitySystem special = blade.Special;
            if (special != null && special.IsActive && !special.IsWindingUp)
            {
                SpecialVfxStyle style = lib.GetSpecialEffectsOrGeneric(special.Type);
                if (style != null && style.impact != null && style.impact.Count > 0)
                {
                    s_power = special.Color;
                    Burst(style.impact, groundPoint);
                    return;
                }
            }
            StatusVfxStyle status = StatusStyle(blade);
            s_power = Color.white;
            if (status != null) Burst(status.impact, groundPoint);
        }

        /// <summary>
        /// Choque con daño (2026-10-19): si quien golpea tiene encendido un especial con estela (hitTrail), la golpeada deja
        /// ese rastro un momento mientras sale despedida. damage: lo que le ha quitado a la golpeada.
        /// </summary>
        public static void PlayHitTrail(FakeBladeController attacker, FakeBladeController target, float damage)
        {
            VfxLibrary lib = VfxSystem.Library;
            if (lib == null || attacker == null || target == null || damage <= 0f || target.IsDestroyed) return;
            SpecialAbilitySystem special = attacker.Special;
            if (special == null || !special.IsActive || special.IsWindingUp) return;
            SpecialVfxStyle style = lib.GetSpecialEffectsOrGeneric(special.Type);
            if (style == null || style.hitTrail == null || style.hitTrail.Count == 0 || style.hitTrailTime <= 0f) return;
            target.StartSpecialHitTrail(style, special.Color);
        }

        /// <summary>Empieza (o renueva) la estela de un golpe en esta peonza, con los efectos del poder de quien la golpeó.</summary>
        public void StartHitTrail(SpecialVfxStyle style, Color power)
        {
            _hitTrailStyle = style;
            _hitTrailPower = power;
            _hitTrailLeft = style.hitTrailTime;
        }

        /// <summary>Color de una partícula con su Power Tint: hacia el color del poder que se está emitiendo (s_power).</summary>
        private static Color Tinted(Color color, float tint)
        {
            if (tint <= 0f) return color;
            Color power = Color.Lerp(Color.white, s_power, tint);
            return new Color(color.r * power.r, color.g * power.g, color.b * power.b, color.a);
        }

        /// <summary>
        /// Explosión en espiral (al encenderse o al chocar): cada estilo con su cantidad, repartida alrededor. direction: la
        /// marcha o el golpe, para los que se adelantan o se giran con ella (cero = ninguna). untilTime: la duración de las
        /// que tienen Lifetime 0 (la marca del Salto, hasta que cae; 0 = la del sprite). maxSize: tamaño máximo de cada
        /// partícula (la marca y la caída del Salto junto a la pared, para no salir de la arena).
        /// </summary>
        private static void Burst(List<SpiralParticleStyle> styles, Vector3 tip, Vector3 direction = default, float untilTime = 0f,
            float maxSize = float.MaxValue)
        {
            if (styles == null) return;
            float mul = SettingsService.ParticleMultiplier;
            for (int i = 0; i < styles.Count && i < MaxStyles; i++)
            {
                SpiralParticleStyle style = styles[i];
                if (style == null) continue;
                int n = Mathf.Min(MaxBurstParticles, Mathf.RoundToInt(style.count * mul));
                float step = Mathf.PI * 2f / Mathf.Max(1, n);
                for (int k = 0; k < n; k++) EmitSpiral(style, tip, k * step + Random.Range(0f, step), direction, untilTime, maxSize);
            }
        }

        /// <summary>
        /// Una partícula del anillo en el ángulo dado: gira alrededor de la peonza, sale (o entra) y sube. direction: la marcha
        /// o el golpe (Forward Offset y Align To Direction; cero = ninguna). untilTime: ver Burst.
        /// </summary>
        private static void EmitSpiral(SpiralParticleStyle style, Vector3 tip, float angle, Vector3 direction, float untilTime = 0f,
            float maxSize = float.MaxValue)
        {
            var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            // Tangente vista desde arriba: en el sentido de las agujas del reloj, (z, -x)
            Vector3 tangent = style.clockwise ? new Vector3(outward.z, 0f, -outward.x) : new Vector3(-outward.z, 0f, outward.x);
            Vector3 position = tip + outward * Random.Range(style.radius.x, style.radius.y) +
                               Vector3.up * Random.Range(style.height.x, style.height.y) + direction * style.forwardOffset;
            Vector3 velocity = tangent * Random.Range(style.tangentialSpeed.x, style.tangentialSpeed.y) +
                               outward * Random.Range(style.outwardSpeed.x, style.outwardSpeed.y) +
                               Vector3.up * Random.Range(style.upSpeed.x, style.upSpeed.y);
            Color color = Tinted(Color.Lerp(style.color, style.color2, Random.value), style.powerTint);
            color.a = style.alpha;
            float size = Mathf.Min(Random.Range(style.size.x, style.size.y), maxSize);
            float lifetime = Random.Range(style.lifetime.x, style.lifetime.y);
            if (lifetime <= 0f) lifetime = untilTime;
            // Varios sprites: uno al azar en cada partícula (grietas distintas en cada golpe)
            List<string> variants = style.spriteVariants;
            string sprite = variants != null && variants.Count > 0 ? variants[Random.Range(0, variants.Count)] : style.sprite;
            float rotation = Random.Range(style.rotation.x, style.rotation.y);
            if (style.alignToDirection && direction.sqrMagnitude > 0.0001f) rotation += Heading(direction);
            if (!string.IsNullOrEmpty(sprite))
                VfxSystem.EmitSprite(sprite, position, velocity, color, size, rotation, lifetime);
            else
                VfxSystem.EmitParticle(style.emitter, position, velocity, color, size, lifetime);
        }

        /// <summary>Cada partícula de la lista: su ritmo seguido más los chasquidos (varias de golpe, a intervalos al azar).</summary>
        private void Emit(List<SpecialParticleStyle> styles, float[] acc, float[] burst, Vector3 tip, float dt, Vector3 motion)
        {
            int count = styles != null ? Mathf.Min(styles.Count, MaxStyles) : 0;
            float mul = SettingsService.ParticleMultiplier;
            for (int i = 0; i < MaxStyles; i++)
            {
                SpecialParticleStyle style = i < count ? styles[i] : null;
                if (style == null)
                {
                    acc[i] = burst[i] = 0f;
                    continue;
                }

                acc[i] += dt * style.rate * mul;
                int n = (int)acc[i];
                acc[i] -= n;
                if (style.burstInterval.y > 0f)
                {
                    burst[i] -= dt;
                    if (burst[i] <= 0f)
                    {
                        burst[i] = Random.Range(style.burstInterval.x, style.burstInterval.y);
                        n += Mathf.RoundToInt(Random.Range(style.burstCount.x, style.burstCount.y) * mul);
                    }
                }
                n = Mathf.Min(n, MaxParticlesPerFrame);
                for (int k = 0; k < n; k++) EmitOne(style, tip, motion);
            }
        }

        /// <summary>motion: hacia dónde va la peonza (Forward Offset y Align To Motion).</summary>
        private void EmitOne(SpecialParticleStyle style, Vector3 tip, Vector3 motion)
        {
            // Dirección al azar dentro del cono (desde la vertical); salen repartidas alrededor de la punta
            float tilt = Random.Range(0f, style.coneAngle) * Mathf.Deg2Rad;
            float turn = Random.value * Mathf.PI * 2f;
            float sin = Mathf.Sin(tilt);
            var direction = new Vector3(sin * Mathf.Cos(turn), Mathf.Cos(tilt), sin * Mathf.Sin(turn));
            var around = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
            Vector3 position = tip + around * Random.Range(style.spawnRadius.x, style.spawnRadius.y) +
                               Vector3.up * Random.Range(style.height.x, style.height.y) +
                               motion * Random.Range(style.forwardOffset.x, style.forwardOffset.y);
            Vector3 velocity = _blade.Velocity;
            Vector3 inherited = new Vector3(velocity.x, 0f, velocity.z) * style.inheritVelocity;
            Color color = Tinted(Color.Lerp(style.color, style.color2, Random.value), style.powerTint);
            color.a = style.alpha;
            Vector3 v = direction * Random.Range(style.speed.x, style.speed.y) + inherited;
            float size = Random.Range(style.size.x, style.size.y);
            float lifetime = Random.Range(style.lifetime.x, style.lifetime.y);
            float rotation = Random.Range(style.rotation.x, style.rotation.y);
            if (style.alignToMotion) rotation += Heading(motion);
            if (!string.IsNullOrEmpty(style.sprite))
                VfxSystem.EmitSprite(style.sprite, position, v, color, size, rotation, lifetime);
            else
                VfxSystem.EmitParticle(style.emitter, position, v, color, size, lifetime);
        }

        /// <summary>
        /// Una mancha cada Spacing metros recorridos (y cada Idle Interval segundos aunque no se mueva). Con varios sprites, uno
        /// al azar; con Align To Motion, girada hacia la marcha (las grietas de la Embestida siguen el camino).
        /// </summary>
        private void UpdateScorch(ScorchStyle style, Vector3 tip, float dt, Vector3 motion)
        {
            bool hasSprite = style != null && (!string.IsNullOrEmpty(style.sprite) || (style.spriteVariants != null && style.spriteVariants.Count > 0));
            if (!hasSprite || _blade.IsDestroyed || _blade.IsLeaping)
            {
                _hasLastPosition = false;
                _scorchDistance = _scorchIdle = 0f;
                return;
            }
            if (!_hasLastPosition)
            {
                _hasLastPosition = true;
                _lastPosition = tip;
                // La primera, nada más empezar; las de la arrancada no (la Embestida ya deja su grieta de salida)
                _scorchDistance = style.onlyWhileRushing ? 0f : style.spacing;
            }
            float dx = tip.x - _lastPosition.x, dz = tip.z - _lastPosition.z;
            _scorchDistance += Mathf.Sqrt(dx * dx + dz * dz);
            _lastPosition = tip;
            _scorchIdle += dt;

            bool idle = style.idleInterval > 0f && _scorchIdle >= style.idleInterval;
            if (_scorchDistance < style.spacing && !idle) return;
            _scorchDistance = Mathf.Min(_scorchDistance - style.spacing, style.spacing); // sin ráfagas tras un salto
            if (_scorchDistance < 0f) _scorchDistance = 0f;
            _scorchIdle = 0f;
            if (!VfxSystem.IsOnScreen(tip)) return;

            Color color = style.color;
            color.a = style.alpha;
            List<string> variants = style.spriteVariants;
            string sprite = variants != null && variants.Count > 0 ? variants[Random.Range(0, variants.Count)] : style.sprite;
            float rotation = Random.Range(style.rotation.x, style.rotation.y);
            if (style.alignToMotion) rotation += Heading(motion);
            VfxSystem.EmitSprite(sprite, new Vector3(tip.x, tip.y + style.height, tip.z), Vector3.zero, color,
                Random.Range(style.size.x, style.size.y), rotation, style.lifetime);
        }
    }
}
