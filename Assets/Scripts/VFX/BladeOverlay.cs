using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Efectos sobre la propia peonza (K9, GDD 7.6): añade el material de superposición (VfxLibrary.bladeOverlay.material,
    /// shader FakeBlade/BladeOverlay) a cada pieza del modelo y, por evento, le da un borde de corte duro con contorno
    /// grueso (tipo cómic), un destello que tiñe toda la peonza y anillos con textura (Zbam) proyectados desde arriba:
    /// - Cargar un ataque: borde que crece, se calienta y late; anillos que salen del núcleo hacia fuera una y otra vez
    ///   (más deprisa con el nivel); destello al subir de nivel.
    /// - Ataque rápido: un anillo desde el núcleo, una vez.
    /// - Golpe recibido de otra peonza: destello (más fuerte cuanto más daño) y un anillo del color del daño que se abre
    ///   desde el punto del choque y recorre la peonza hasta la parte de atrás.
    /// - Parry y bloqueo (en quien para): destello.
    /// - Especial activo con efectos propios (K8/K9, VfxLibrary.specialEffects[...].overlay): impulsos del borde a ritmo
    ///   irregular, del color del poder, y un sprite animado (hoja de fotogramas) proyectado desde arriba.
    /// - Quemada (K8, VfxLibrary.burn): impulsos naranjas del borde (el fuego y el humo, en BladeSpecialFx).
    ///
    /// Clase C# pura que posee FakeBladeController. El material se añade al montar el modelo (Rebind, también al cambiar
    /// de piezas); después, cada frame solo cambia valores con un MaterialPropertyBlock en el material añadido: cero GC.
    /// Los valores se leen en vivo de la biblioteca.
    /// </summary>
    public sealed class BladeOverlay
    {
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int RimIntensityId = Shader.PropertyToID("_RimIntensity");
        private static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");
        private static readonly int RimSoftnessId = Shader.PropertyToID("_RimSoftness");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int RingTexId = Shader.PropertyToID("_RingTex");
        private static readonly int RingAId = Shader.PropertyToID("_RingA");
        private static readonly int RingAColorId = Shader.PropertyToID("_RingAColor");
        private static readonly int RingBId = Shader.PropertyToID("_RingB");
        private static readonly int RingBColorId = Shader.PropertyToID("_RingBColor");
        private static readonly int RingShadowColorId = Shader.PropertyToID("_RingShadowColor");
        private static readonly int RingShadowScaleId = Shader.PropertyToID("_RingShadowScale");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int FxTexId = Shader.PropertyToID("_FxTex");
        private static readonly int FxId = Shader.PropertyToID("_Fx");
        private static readonly int FxSheetId = Shader.PropertyToID("_FxSheet");
        private static readonly int FxColorId = Shader.PropertyToID("_FxColor");
        /// <summary>Lo que tarda el efecto del especial en aparecer y en apagarse (s).</summary>
        private const float SpecialFade = 0.15f;

        private readonly FakeBladeController _blade;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<int> _indices = new List<int>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Material _boundMaterial;
        private bool _bound;
        private bool _shown;
        /// <summary>Radio de la peonza en el suelo (m), medido al montar el modelo.</summary>
        private float _radius = 0.6f;

        // Destello en curso (el último evento manda)
        private Color _eventColor;
        private float _eventFlash, _eventRim, _eventDuration, _eventTimer;
        private int _lastChargeLevel;
        private float _pulse;

        // Anillo A: carga (en bucle) o ataque rápido (una vez)
        private float _chargeRingPhase;
        private float _quickRingTimer;
        private float _ringAngle;
        // Anillo B: golpe recibido (desde el punto del choque, que se mueve con la peonza)
        private float _impactRingTimer;
        private Vector3 _impactOffset;
        private Color _impactColor;
        private float _impactAngle;

        /// <summary>Impulsos del borde a ritmo irregular y sprite animado (especial activo o quemadura).</summary>
        private sealed class PulseState
        {
            public float Fade, Wait, Age, Strength, Heat, FxTime, FxAngle;
        }

        // Especial activo y quemadura (K8/K9)
        private readonly PulseState _specialPulse = new PulseState();
        private readonly PulseState _burnPulse = new PulseState();

        public BladeOverlay(FakeBladeController blade)
        {
            _blade = blade;
            _blade.OnDamaged += HandleDamaged;
            _blade.OnAttackLaunched += HandleAttackLaunched;
            CollisionResolver.OnParry += HandleParry;
            CollisionResolver.OnBlock += HandleBlock;
        }

        public void Dispose()
        {
            _blade.OnDamaged -= HandleDamaged;
            _blade.OnAttackLaunched -= HandleAttackLaunched;
            CollisionResolver.OnParry -= HandleParry;
            CollisionResolver.OnBlock -= HandleBlock;
        }

        /// <summary>¿Es el material de superposición? (BladePaint no debe tocarlo)</summary>
        public static bool IsOverlay(Material material) =>
            material != null && material.shader != null && material.shader.name == "FakeBlade/BladeOverlay";

        /// <summary>El modelo es nuevo (piezas cambiadas): hay que volver a poner el material en sus renderers.</summary>
        public void Rebind() => _bound = false;

        private static BladeOverlayStyle Style => VfxSystem.Library != null ? VfxSystem.Library.bladeOverlay : null;

        /// <summary>Efecto sobre la peonza de su especial (aunque ya no esté activo: se apaga con un fundido), o null.</summary>
        private SpecialOverlayStyle SpecialStyle()
        {
            if (VfxSystem.Library == null || _blade.Special == null) return null;
            SpecialVfxStyle effects = VfxSystem.Library.GetSpecialEffects(_blade.Special.Type);
            return effects != null ? effects.overlay : null;
        }

        public void Tick(float dt)
        {
            BladeOverlayStyle style = Style;
            if (style == null || style.material == null) return;
            if (!_bound || _boundMaterial != style.material) Bind(style.material);

            // Destello del último evento: sube de golpe y se apaga (más rápido al principio)
            float flash = 0f, rim = 0f;
            Color rimColor = _eventColor;
            if (_eventTimer > 0f)
            {
                _eventTimer = Mathf.Max(0f, _eventTimer - dt);
                float k = _eventTimer / _eventDuration;
                k *= k;
                flash = _eventFlash * k;
                rim = _eventRim * k;
            }

            // Carga: borde que crece y se calienta con el nivel, y late
            AttackSystem attack = _blade.Attack;
            bool charging = style.chargeEnabled && attack.IsCharging && !_blade.IsStaggered && !_blade.IsDestroyed;
            float heat = 0f;
            Color chargeColor = style.chargeColor;
            if (charging)
            {
                int level = attack.ChargeLevel;
                heat = attack.MaxCharges <= 1 ? 1f : Mathf.Clamp01((level - 1f) / (attack.MaxCharges - 1f));
                if (level > _lastChargeLevel && _lastChargeLevel > 0) Trigger(style.chargeLevelUp, 1f);
                _lastChargeLevel = level;

                Color baseColor = style.chargePlayerColor ? _blade.OwnerColor : style.chargeColor;
                chargeColor = Color.Lerp(baseColor, style.chargeHotColor, heat);
                _pulse = Mathf.Repeat(_pulse + dt * style.chargePulseSpeed, 1f);
                float beat = 1f - style.chargePulseAmount * (0.5f + 0.5f * Mathf.Cos(_pulse * Mathf.PI * 2f));
                float chargeRim = Mathf.Lerp(style.chargeRim.x, style.chargeRim.y, heat) * beat;
                if (chargeRim > rim)
                {
                    rim = chargeRim;
                    rimColor = chargeColor;
                }
            }
            else
            {
                _lastChargeLevel = 0;
                _pulse = 0f;
            }

            Vector3 center = _blade.Position;

            // Especial activo con efectos propios: impulsos del borde (manda el más fuerte, como con la carga) y sprite
            Color flashColor = _eventColor;
            Vector4 fx = Vector4.zero;
            Vector4 fxSheet = Vector4.zero;
            Color fxColor = Color.clear;
            Texture2D fxTexture = null;
            SpecialOverlayStyle special = SpecialStyle();
            bool specialOn = special != null && special.enabled && _blade.Special.IsActive && !_blade.Special.IsWindingUp &&
                             !_blade.IsDestroyed;
            Pulses(_specialPulse, special, specialOn, dt, center, ref rim, ref rimColor, ref flash, ref flashColor,
                ref fx, ref fxSheet, ref fxColor, ref fxTexture);

            // Quemadura: impulsos naranjas del borde
            BurnEffectStyle burn = VfxSystem.Library.burn;
            bool burning = burn != null && burn.enabled && !_blade.IsDestroyed && _blade.Status.Current == StatusEffectType.Burning;
            Pulses(_burnPulse, burn != null ? burn.overlay : null, burning, dt, center, ref rim, ref rimColor, ref flash,
                ref flashColor, ref fx, ref fxSheet, ref fxColor, ref fxTexture);
            _ringAngle += dt * style.chargeRingSpin * Mathf.Deg2Rad;

            // Anillo A: crece desde el núcleo hacia fuera (en bucle mientras se carga; una vez en el ataque rápido)
            Vector4 ringA = Vector4.zero;
            Color ringAColor = chargeColor;
            float ringAAlpha = 0f;
            if (charging && style.chargeRingEnabled)
            {
                float cycle = Mathf.Lerp(style.chargeRingCycle, style.chargeRingCycleMax, heat);
                _chargeRingPhase = Mathf.Repeat(_chargeRingPhase + dt / Mathf.Max(0.05f, cycle), 1f);
                ringA = Ring(center, Mathf.Lerp(style.chargeRingScale.x, style.chargeRingScale.y, _chargeRingPhase), _ringAngle);
                ringAAlpha = style.chargeRingAlpha * Fade(_chargeRingPhase);
            }
            else
            {
                _chargeRingPhase = 0f;
                if (_quickRingTimer > 0f)
                {
                    _quickRingTimer = Mathf.Max(0f, _quickRingTimer - dt);
                    float phase = 1f - _quickRingTimer / Mathf.Max(0.05f, style.quickRingDuration);
                    ringA = Ring(center, Mathf.Lerp(style.chargeRingScale.x, style.chargeRingScale.y, phase), _ringAngle);
                    ringAAlpha = style.chargeRingAlpha * Fade(phase);
                }
            }
            ringAColor.a = ringAAlpha;

            // Anillo B: se abre desde el punto del choque y recorre la peonza
            Vector4 ringB = Vector4.zero;
            Color ringBColor = _impactColor;
            ringBColor.a = 0f;
            if (_impactRingTimer > 0f)
            {
                _impactRingTimer = Mathf.Max(0f, _impactRingTimer - dt);
                float phase = 1f - _impactRingTimer / Mathf.Max(0.05f, style.impactRingDuration);
                ringB = Ring(center + _impactOffset, Mathf.Lerp(style.impactRingScale.x, style.impactRingScale.y, phase), _impactAngle);
                ringBColor.a = style.impactRingAlpha * Fade(phase);
            }

            bool visible = flash > 0.001f || rim > 0.001f || ringAColor.a > 0.001f || ringBColor.a > 0.001f || fxColor.a > 0.001f;
            if (!visible && !_shown) return; // apagado y ya se pintó apagado: nada que cambiar
            _shown = visible;

            _block.SetColor(RimColorId, rimColor);
            _block.SetFloat(RimIntensityId, rim);
            _block.SetFloat(RimWidthId, style.rimWidth);
            _block.SetFloat(RimSoftnessId, style.rimSoftness);
            _block.SetColor(FlashColorId, flashColor);
            _block.SetFloat(FlashAmountId, flash);
            if (fxTexture != null) _block.SetTexture(FxTexId, fxTexture);
            _block.SetVector(FxId, fx);
            _block.SetVector(FxSheetId, fxSheet);
            _block.SetColor(FxColorId, fxColor);
            if (style.ringTexture != null) _block.SetTexture(RingTexId, style.ringTexture);
            _block.SetVector(RingAId, ringA);
            _block.SetColor(RingAColorId, ringAColor);
            _block.SetVector(RingBId, ringB);
            _block.SetColor(RingBColorId, ringBColor);
            _block.SetColor(RingShadowColorId, style.ringShadowColor);
            _block.SetFloat(RingShadowScaleId, style.ringShadowScale);
            Color outline = rimColor;
            outline.a = style.outlineAlpha * Mathf.Clamp01(rim);
            _block.SetColor(OutlineColorId, outline);
            _block.SetFloat(OutlineWidthId, style.outlineWidth);
            for (int i = 0; i < _renderers.Count; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_block, _indices[i]);
        }

        /// <summary>
        /// Impulsos del borde (suben de golpe y se apagan, a ritmo irregular; manda el borde y el destello más fuertes) y
        /// sprite animado proyectado desde arriba (solo uno a la vez: el primero que lo pida). Con un fundido al encender y
        /// al apagar (on).
        /// </summary>
        private void Pulses(PulseState state, SpecialOverlayStyle style, bool on, float dt, Vector3 center,
            ref float rim, ref Color rimColor, ref float flash, ref Color flashColor,
            ref Vector4 fx, ref Vector4 fxSheet, ref Color fxColor, ref Texture2D fxTexture)
        {
            state.Fade = style == null || !style.enabled ? 0f : Mathf.MoveTowards(state.Fade, on ? 1f : 0f, dt / SpecialFade);
            if (state.Fade <= 0f)
            {
                state.Wait = state.FxTime = 0f;
                return;
            }

            state.Wait -= dt;
            if (state.Wait <= 0f)
            {
                state.Wait = Random.Range(style.pulseInterval.x, style.pulseInterval.y);
                state.Age = 0f;
                state.Strength = Random.value;
                state.Heat = Random.value;
            }
            state.Age += dt;
            float k = 1f - Mathf.Clamp01(state.Age / style.pulseDecay);
            k *= k;
            Color baseColor = style.powerColor ? _blade.Special.Color : style.color;
            Color color = Color.Lerp(baseColor, style.hotColor, state.Heat * k);
            float pulseRim = (style.baseRim + Mathf.Lerp(style.pulseRim.x, style.pulseRim.y, state.Strength) * k) * state.Fade;
            if (pulseRim > rim)
            {
                rim = pulseRim;
                rimColor = color;
            }
            float pulseFlash = Mathf.Lerp(style.pulseFlash.x, style.pulseFlash.y, state.Strength) * k * state.Fade;
            if (pulseFlash > flash)
            {
                flash = pulseFlash;
                flashColor = color;
            }

            if (style.texture == null || fxTexture != null) return;
            state.FxTime += dt;
            state.FxAngle += dt * style.textureSpin * Mathf.Deg2Rad;
            int tiles = Mathf.Max(1, style.tilesX * style.tilesY);
            int first = Mathf.Clamp(style.firstFrame, 0, tiles - 1);
            int frames = Mathf.Clamp(style.frames, 1, tiles - first);
            float phase = Mathf.Repeat(state.FxTime / Mathf.Max(0.05f, style.cycle), 1f);
            fx = Ring(center, style.textureScale, state.FxAngle);
            fxSheet = new Vector4(style.tilesX, style.tilesY, first + Mathf.Min(frames - 1, (int)(phase * frames)), 0f);
            fxColor = style.textureColor;
            fxColor.a *= state.Fade;
            fxTexture = style.texture;
        }

        /// <summary>Anillo para el shader: centro (x, z), radio (escala × radio de la peonza) y giro.</summary>
        private Vector4 Ring(Vector3 center, float scale, float angle) =>
            new Vector4(center.x, center.z, Mathf.Max(0.0001f, scale * _radius), angle);

        /// <summary>Transparencia de un anillo a lo largo de su vida: entra rápido y se apaga al final.</summary>
        private static float Fade(float phase) => Mathf.Clamp01(phase * 6f) * (1f - phase * phase);

        /// <summary>Apaga el efecto ya (al reaparecer o quedar fuera, cuando la peonza deja de actualizarse).</summary>
        public void Clear()
        {
            _eventTimer = _quickRingTimer = _impactRingTimer = 0f;
            _lastChargeLevel = 0;
            _specialPulse.Fade = _burnPulse.Fade = 0f;
            if (!_shown) return;
            _shown = false;
            _block.SetFloat(RimIntensityId, 0f);
            _block.SetFloat(FlashAmountId, 0f);
            _block.SetColor(RingAColorId, Color.clear);
            _block.SetColor(RingBColorId, Color.clear);
            _block.SetColor(FxColorId, Color.clear);
            _block.SetColor(OutlineColorId, Color.clear);
            for (int i = 0; i < _renderers.Count; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_block, _indices[i]);
        }

        private void Trigger(OverlayFlash preset, float strength)
        {
            if (preset == null || !preset.enabled || strength <= 0f) return;
            _eventColor = preset.playerColor ? _blade.OwnerColor : preset.color;
            _eventFlash = preset.flash * strength;
            _eventRim = preset.rim * strength;
            _eventDuration = Mathf.Max(0.01f, preset.duration);
            _eventTimer = _eventDuration;
        }

        private void HandleDamaged(float damage, FakeBladeController source)
        {
            // Solo los golpes de otra peonza (la pared o la quemadura no)
            if (source == null) return;
            BladeOverlayStyle style = Style;
            if (style == null) return;
            Trigger(style.damaged, Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(damage / style.damageForFull)));

            if (!style.impactRingEnabled) return;
            // Desde el borde que mira a quien golpea, con el color del daño (el mismo que los impactos)
            Vector3 toSource = source.Position - _blade.Position;
            toSource.y = 0f;
            _impactOffset = toSource.sqrMagnitude > 0.0001f ? toSource.normalized * _radius : Vector3.zero;
            Color color = VfxSystem.Library.ImpactColor(damage);
            if (style.impactRingLighten > 0f) color = Color.Lerp(color, Color.white, style.impactRingLighten);
            _impactColor = color;
            _impactAngle = Random.value * Mathf.PI * 2f;
            _impactRingTimer = Mathf.Max(0.05f, style.impactRingDuration);
        }

        private void HandleAttackLaunched(int level)
        {
            BladeOverlayStyle style = Style;
            // Ataque rápido (nivel 0): un anillo desde el núcleo
            if (level == 0 && style != null && style.quickRingEnabled)
                _quickRingTimer = Mathf.Max(0.05f, style.quickRingDuration);
        }

        private void HandleParry(FakeBladeController parrier, FakeBladeController attacker, bool doubleParry)
        {
            if (parrier != _blade) return;
            BladeOverlayStyle style = Style;
            if (style != null) Trigger(style.parry, 1f);
        }

        private void HandleBlock(FakeBladeController blocker, FakeBladeController attacker)
        {
            if (blocker != _blade) return;
            BladeOverlayStyle style = Style;
            if (style != null) Trigger(style.block, 1f);
        }

        /// <summary>
        /// Añade el material de superposición a cada malla del modelo (una vez por modelo: reserva memoria) y mide el radio
        /// de la peonza para los anillos.
        /// </summary>
        private void Bind(Material overlay)
        {
            _bound = true;
            _boundMaterial = overlay;
            _renderers.Clear();
            _indices.Clear();
            Transform root = _blade.VisualRoot;
            if (root == null) return;

            Vector3 center = _blade.Position;
            float radius = 0f;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (BladePaint.IsImagePlane(r.name)) continue;

                Material[] materials = r.sharedMaterials;
                int index = -1;
                for (int i = 0; i < materials.Length; i++)
                    if (IsOverlay(materials[i])) { index = i; materials[i] = overlay; }
                if (index < 0)
                {
                    var extended = new Material[materials.Length + 1];
                    materials.CopyTo(extended, 0);
                    index = materials.Length;
                    extended[index] = overlay;
                    materials = extended;
                }
                r.sharedMaterials = materials;
                _renderers.Add(r);
                _indices.Add(index);

                Bounds b = r.bounds;
                radius = Mathf.Max(radius, Mathf.Abs(b.max.x - center.x), Mathf.Abs(b.min.x - center.x),
                    Mathf.Abs(b.max.z - center.z), Mathf.Abs(b.min.z - center.z));
            }
            if (radius > 0.05f) _radius = radius;
            _shown = true; // que el primer Tick deje los valores puestos (apagado)
        }
    }
}
