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
        // Una lista de auras por estado: cambiar de lista rehace las copias, y eso no se hace en combate
        private readonly AuraSlot[] _specialAuras = new AuraSlot[MaxAuras];
        private readonly AuraSlot[] _burnAuras = new AuraSlot[MaxAuras];
        private readonly AuraSlot[] _frozenAuras = new AuraSlot[MaxAuras];
        private bool _effectsWereOn;
        private float _scorchDistance;
        private float _scorchIdle;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;

        public BladeSpecialFx(FakeBladeController blade)
        {
            _blade = blade;
            for (int i = 0; i < MaxAuras; i++)
            {
                _specialAuras[i] = new AuraSlot();
                _burnAuras[i] = new AuraSlot();
                _frozenAuras[i] = new AuraSlot();
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

            bool active = specialActive && special != null;
            bool effectsOn = active && !windingUp;
            // El remolino de la carga sigue, más flojo, mientras dura el poder (2026-10-17)
            float swirl = !active || !visible ? 0f : windingUp ? 1f : special.windupSustain;
            EmitSpirals(swirl > 0f ? special.windup : null, _windupAcc, swirl, tip, dt);
            if (effectsOn && !_effectsWereOn && visible) Burst(special.activationBurst, tip);
            _effectsWereOn = effectsOn;
            EmitSpirals(effectsOn && visible ? special.swirl : null, _specialSwirlAcc, 1f, tip, dt);
            Emit(effectsOn && visible ? special.particles : null, _specialAcc, _specialBurst, tip, dt);
            UpdateScorch(effectsOn ? special.scorch : null, tip, dt);
            UpdateAuras(special != null ? special.auras : null, _specialAuras, effectsOn, tipOffset, dt);

            StatusVfxStyle status = StatusStyle(_blade);
            Emit(status != null && visible ? status.particles : null, _statusAcc, _statusBurst, tip, dt);
            EmitSpirals(status != null && visible ? status.swirl : null, _statusSwirlAcc, 1f, tip, dt);
            UpdateAuras(lib != null ? lib.burn?.auras : null, _burnAuras, status != null && status == lib.burn, tipOffset, dt);
            UpdateAuras(lib != null ? lib.frozen?.auras : null, _frozenAuras, status != null && status == lib.frozen, tipOffset, dt);
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
                    _specialSwirlAcc[i] = _statusSwirlAcc[i] = 0f;
            _hasLastPosition = false;
            _effectsWereOn = false;
            _scorchDistance = _scorchIdle = 0f;
            for (int i = 0; i < MaxAuras; i++)
            {
                StopAura(_specialAuras[i]);
                StopAura(_burnAuras[i]);
                StopAura(_frozenAuras[i]);
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
        private static void EmitSpirals(List<SpiralParticleStyle> styles, float[] acc, float rateScale, Vector3 tip, float dt)
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
                acc[i] += dt * style.rate * mul;
                int n = Mathf.Min((int)acc[i], MaxParticlesPerFrame);
                acc[i] -= (int)acc[i];
                for (int k = 0; k < n; k++) EmitSpiral(style, tip, Random.value * Mathf.PI * 2f);
            }
        }

        /// <summary>
        /// Choque con daño (K8, 2026-10-17): si la peonza tiene encendido un especial con efectos de choque, su círculo de
        /// partículas (Fuego: llamas, chispas y ceniza; Hielo: esquirlas, destellos y vaho); si no, y está quemada o
        /// congelada, el de su estado (más pequeño). Se suma al impacto genérico. groundPoint: el punto del choque a ras
        /// de suelo.
        /// </summary>
        public static void PlayClashImpact(FakeBladeController blade, Vector3 groundPoint)
        {
            VfxLibrary lib = VfxSystem.Library;
            if (lib == null || blade == null || blade.IsDestroyed || !VfxSystem.IsOnScreen(groundPoint)) return;
            SpecialAbilitySystem special = blade.Special;
            if (special != null && special.IsActive && !special.IsWindingUp)
            {
                SpecialVfxStyle style = lib.GetSpecialEffects(special.Type);
                if (style != null && style.impact != null && style.impact.Count > 0)
                {
                    Burst(style.impact, groundPoint);
                    return;
                }
            }
            StatusVfxStyle status = StatusStyle(blade);
            if (status != null) Burst(status.impact, groundPoint);
        }

        /// <summary>Explosión en espiral (al encenderse o al chocar): cada estilo con su cantidad, repartida alrededor.</summary>
        private static void Burst(List<SpiralParticleStyle> styles, Vector3 tip)
        {
            if (styles == null) return;
            float mul = SettingsService.ParticleMultiplier;
            for (int i = 0; i < styles.Count && i < MaxStyles; i++)
            {
                SpiralParticleStyle style = styles[i];
                if (style == null) continue;
                int n = Mathf.Min(MaxBurstParticles, Mathf.RoundToInt(style.count * mul));
                float step = Mathf.PI * 2f / Mathf.Max(1, n);
                for (int k = 0; k < n; k++) EmitSpiral(style, tip, k * step + Random.Range(0f, step));
            }
        }

        /// <summary>Una partícula del anillo en el ángulo dado: gira alrededor de la peonza, sale (o entra) y sube.</summary>
        private static void EmitSpiral(SpiralParticleStyle style, Vector3 tip, float angle)
        {
            var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            // Tangente vista desde arriba: en el sentido de las agujas del reloj, (z, -x)
            Vector3 tangent = style.clockwise ? new Vector3(outward.z, 0f, -outward.x) : new Vector3(-outward.z, 0f, outward.x);
            Vector3 position = tip + outward * Random.Range(style.radius.x, style.radius.y) +
                               Vector3.up * Random.Range(style.height.x, style.height.y);
            Vector3 velocity = tangent * Random.Range(style.tangentialSpeed.x, style.tangentialSpeed.y) +
                               outward * Random.Range(style.outwardSpeed.x, style.outwardSpeed.y) +
                               Vector3.up * Random.Range(style.upSpeed.x, style.upSpeed.y);
            Color color = Color.Lerp(style.color, style.color2, Random.value);
            color.a = style.alpha;
            float size = Random.Range(style.size.x, style.size.y);
            float lifetime = Random.Range(style.lifetime.x, style.lifetime.y);
            if (!string.IsNullOrEmpty(style.sprite))
                VfxSystem.EmitSprite(style.sprite, position, velocity, color, size, Random.value * 360f, lifetime);
            else
                VfxSystem.EmitParticle(style.emitter, position, velocity, color, size, lifetime);
        }

        /// <summary>Cada partícula de la lista: su ritmo seguido más los chasquidos (varias de golpe, a intervalos al azar).</summary>
        private void Emit(List<SpecialParticleStyle> styles, float[] acc, float[] burst, Vector3 tip, float dt)
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
                for (int k = 0; k < n; k++) EmitOne(style, tip);
            }
        }

        private void EmitOne(SpecialParticleStyle style, Vector3 tip)
        {
            // Dirección al azar dentro del cono (desde la vertical); salen repartidas alrededor de la punta
            float tilt = Random.Range(0f, style.coneAngle) * Mathf.Deg2Rad;
            float turn = Random.value * Mathf.PI * 2f;
            float sin = Mathf.Sin(tilt);
            var direction = new Vector3(sin * Mathf.Cos(turn), Mathf.Cos(tilt), sin * Mathf.Sin(turn));
            var around = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
            Vector3 position = tip + around * Random.Range(style.spawnRadius.x, style.spawnRadius.y) +
                               Vector3.up * Random.Range(style.height.x, style.height.y);
            Vector3 velocity = _blade.Velocity;
            Vector3 inherited = new Vector3(velocity.x, 0f, velocity.z) * style.inheritVelocity;
            Color color = Color.Lerp(style.color, style.color2, Random.value);
            color.a = style.alpha;
            Vector3 v = direction * Random.Range(style.speed.x, style.speed.y) + inherited;
            float size = Random.Range(style.size.x, style.size.y);
            float lifetime = Random.Range(style.lifetime.x, style.lifetime.y);
            if (!string.IsNullOrEmpty(style.sprite))
                VfxSystem.EmitSprite(style.sprite, position, v, color, size, Random.value * 360f, lifetime);
            else
                VfxSystem.EmitParticle(style.emitter, position, v, color, size, lifetime);
        }

        /// <summary>Una mancha cada Spacing metros recorridos (y cada Idle Interval segundos aunque no se mueva).</summary>
        private void UpdateScorch(ScorchStyle style, Vector3 tip, float dt)
        {
            if (style == null || string.IsNullOrEmpty(style.sprite) || _blade.IsDestroyed || _blade.IsLeaping)
            {
                _hasLastPosition = false;
                _scorchDistance = _scorchIdle = 0f;
                return;
            }
            if (!_hasLastPosition)
            {
                _hasLastPosition = true;
                _lastPosition = tip;
                _scorchDistance = style.spacing; // la primera, nada más empezar
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
            VfxSystem.EmitSprite(style.sprite, new Vector3(tip.x, tip.y + style.height, tip.z), Vector3.zero, color,
                Random.Range(style.size.x, style.size.y), Random.Range(0f, 360f), style.lifetime);
        }
    }
}
