using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>Estados alterados (GDD 5). Los valores numéricos se serializan: no reordenar.</summary>
    public enum StatusEffectType
    {
        None = 0,
        /// <summary>Fuego: pierde RPM por tic.</summary>
        Burning = 1,
        /// <summary>Hielo: se mueve más lento y recarga los ataques más despacio.</summary>
        Frozen = 2,
        /// <summary>Rayos: sin prioridad por velocidad; recibe el golpe al chocar.</summary>
        Launched = 3,
        /// <summary>
        /// Rastro de agua: resbala (casi sin agarre). Va aparte del estado principal: se puede estar mojada a la vez
        /// que congelada o lanzada; mojarse apaga la quemadura y mojada no se quema (2026-10-14).
        /// </summary>
        Wet = 4
    }

    /// <summary>
    /// Estados alterados de una peonza (GDD 5). Clase C# pura que posee FakeBladeController.
    ///
    /// - Solo un estado a la vez: uno nuevo sustituye al anterior si no está bloqueado.
    /// - Triángulo de bloqueos, Fuego &gt; Hielo &gt; Rayos &gt; Fuego: en llamas no se congela,
    ///   congelada no se lanza y lanzada no se quema.
    /// - El mismo estado otra vez reinicia la duración (no se acumula).
    /// - Los valores los pone quien aplica el estado (el poder que golpea).
    /// - Mojada (Rastro de agua) va aparte: convive con el estado principal, pero apaga la quemadura y la impide.
    /// </summary>
    public sealed class StatusEffectSystem
    {
        // Mojada: independiente del estado principal
        private float _wetDuration;
        private float _wetTime;
        private float _wetGrip = 1f;
        private StatusEffectType _current;
        private FakeBladeController _source;
        private float _duration;
        private float _elapsed;

        // Quemadura: daño por tic contado por tiempo transcurrido (siempre duration/interval tics)
        private float _burnDamagePct;
        private float _burnInterval;
        private int _burnTicksDone;
        private int _burnTicksTotal;

        // Congelación
        private float _freezeMoveMultiplier = 1f;
        private float _freezeRechargeMultiplier = 1f;

        public StatusEffectType Current => _current;
        public bool HasStatus => _current != StatusEffectType.None;
        /// <summary>Quien provocó el estado (para el punto de K.O.).</summary>
        public FakeBladeController Source => _source;
        /// <summary>0-1: lo que queda del estado (el icono parpadea al acabar).</summary>
        public float RemainingFraction => _duration > 0f ? Mathf.Clamp01(1f - _elapsed / _duration) : 0f;
        /// <summary>Segundos que le quedan al estado (0 si no hay).</summary>
        public float RemainingTime => _current != StatusEffectType.None ? Mathf.Max(0f, _duration - _elapsed) : 0f;

        public bool IsBurning => _current == StatusEffectType.Burning;
        public bool IsFrozen => _current == StatusEffectType.Frozen;
        public bool IsLaunched => _current == StatusEffectType.Launched;

        public bool IsWet => _wetTime > 0f;
        /// <summary>0-1: lo que le queda de estar mojada (el icono parpadea al acabar).</summary>
        public float WetRemainingFraction => _wetDuration > 0f ? Mathf.Clamp01(_wetTime / _wetDuration) : 0f;
        /// <summary>Agarre (giro y frenado) mientras está mojada.</summary>
        public float WetGripMultiplier => IsWet ? _wetGrip : 1f;

        /// <summary>Velocidad de movimiento (congelación).</summary>
        public float MoveSpeedMultiplier => IsFrozen ? _freezeMoveMultiplier : 1f;
        /// <summary>Velocidad de recarga de los ataques (congelación).</summary>
        public float AttackRechargeMultiplier => IsFrozen ? _freezeRechargeMultiplier : 1f;

        /// <summary>¿El estado actual bloquea al que llega? (triángulo Fuego &gt; Hielo &gt; Rayos &gt; Fuego)</summary>
        public static bool Blocks(StatusEffectType current, StatusEffectType incoming)
        {
            return (current == StatusEffectType.Burning && incoming == StatusEffectType.Frozen) ||
                   (current == StatusEffectType.Frozen && incoming == StatusEffectType.Launched) ||
                   (current == StatusEffectType.Launched && incoming == StatusEffectType.Burning);
        }

        public bool CanApply(StatusEffectType type) =>
            type != StatusEffectType.None && !Blocks(_current, type) && !(type == StatusEffectType.Burning && IsWet);

        /// <summary>
        /// Mojada: el agarre se multiplica por gripMultiplier durante duration (volver a mojarse lo renueva). Apaga la
        /// quemadura. Devuelve true si ha apagado una quemadura (cambia el estado principal).
        /// </summary>
        public bool Wet(float gripMultiplier, float duration)
        {
            if (duration <= 0f) return false;
            _wetGrip = Mathf.Clamp01(gripMultiplier);
            _wetDuration = duration;
            _wetTime = duration;
            return IsBurning && Clear();
        }

        public void ClearWet() => _wetTime = _wetDuration = 0f;

        /// <summary>Quemadura: pierde damagePctPerTick de sus RPM máximas cada tickInterval segundos.</summary>
        public bool TryBurn(FakeBladeController source, float damagePctPerTick, float tickInterval, float duration)
        {
            if (!Begin(StatusEffectType.Burning, source, duration)) return false;
            _burnDamagePct = Mathf.Max(0f, damagePctPerTick);
            _burnInterval = Mathf.Max(0.05f, tickInterval);
            _burnTicksDone = 0;
            _burnTicksTotal = Mathf.FloorToInt(duration / _burnInterval + 0.001f);
            return true;
        }

        /// <summary>Congelación: multiplica la velocidad de movimiento y la de recarga de ataques.</summary>
        public bool TryFreeze(FakeBladeController source, float moveMultiplier, float rechargeMultiplier, float duration)
        {
            if (!Begin(StatusEffectType.Frozen, source, duration)) return false;
            _freezeMoveMultiplier = Mathf.Clamp01(moveMultiplier);
            _freezeRechargeMultiplier = Mathf.Clamp01(rechargeMultiplier);
            return true;
        }

        /// <summary>Lanzada: pierde la prioridad por velocidad en los choques durante duration.</summary>
        public bool TryLaunch(FakeBladeController source, float duration) =>
            Begin(StatusEffectType.Launched, source, duration);

        private bool Begin(StatusEffectType type, FakeBladeController source, float duration)
        {
            if (!CanApply(type) || duration <= 0f) return false;
            _current = type;
            _source = source;
            _duration = duration;
            _elapsed = 0f;
            return true;
        }

        /// <summary>
        /// Avanza el estado y aplica los tics de quemadura. Devuelve true el frame en que termina.
        /// </summary>
        public bool Tick(float dt, FakeBladeController owner)
        {
            if (_wetTime > 0f) _wetTime = Mathf.Max(0f, _wetTime - dt);
            if (_current == StatusEffectType.None) return false;
            _elapsed += dt;

            if (_current == StatusEffectType.Burning)
            {
                while (_burnTicksDone < _burnTicksTotal && _elapsed >= (_burnTicksDone + 1) * _burnInterval - 0.0001f)
                {
                    _burnTicksDone++;
                    owner.ApplyStatusDamage(owner.MaxSpinSpeed * _burnDamagePct, _source);
                    if (_current == StatusEffectType.None) return true; // la quemadura lo ha dejado K.O.
                }
            }

            if (_elapsed < _duration - 0.0001f) return false;
            Clear();
            return true;
        }

        /// <summary>Quita el estado actual. Devuelve true si había alguno.</summary>
        public bool Clear()
        {
            if (_current == StatusEffectType.None) return false;
            _current = StatusEffectType.None;
            _source = null;
            _duration = _elapsed = 0f;
            return true;
        }
    }
}
