using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Temblor de cámara global. Llamar CameraShake.Shake(intensidad, duración) desde cualquier script.
    ///
    /// Calcula un offset que SimpleCameraFollow suma a su posición. Si la cámara no tiene
    /// SimpleCameraFollow, este componente aplica y retira el offset él mismo, sin deriva.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class CameraShake : MonoBehaviour
    {
        private const float DecayWindow = 0.2f;

        private static CameraShake _instance;

        private float _timer;
        private float _intensity;
        private Vector3 _offset;
        private Vector3 _appliedOffset;
        private bool _hasFollow;

        /// <summary>Desactiva el temblor temporalmente (p. ej. el combate de fondo del menú).</summary>
        public static bool Suppressed { get; set; }

        /// <summary>Offset actual del temblor (cero si no hay).</summary>
        public static Vector3 CurrentOffset => _instance != null ? _instance._offset : Vector3.zero;

        /// <param name="intensity">Magnitud (0.05 sutil, 0.2 fuerte)</param>
        /// <param name="duration">Duración en segundos</param>
        public static void Shake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;
            if (Suppressed || !CombatConfig.Active.cameraShake || !SettingsService.Current.cameraShake) return;

            if (_instance == null)
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                if (!cam.TryGetComponent(out _instance))
                    _instance = cam.gameObject.AddComponent<CameraShake>();
            }

            // Solo se sustituye si el nuevo es más fuerte o el anterior terminó
            if (intensity > _instance._intensity || _instance._timer <= 0f)
            {
                _instance._intensity = intensity;
                _instance._timer = duration;
            }
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
            _hasFollow = TryGetComponent<SimpleCameraFollow>(out _);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void LateUpdate()
        {
            if (_timer > 0f)
            {
                _timer -= Time.deltaTime;
                float decay = Mathf.Clamp01(_timer / DecayWindow);
                Vector3 offset = Random.insideUnitSphere * (_intensity * decay);
                offset.z = 0f;
                _offset = transform.rotation * offset;

                if (_timer <= 0f)
                {
                    _intensity = 0f;
                    _offset = Vector3.zero;
                }
            }
            else
            {
                _offset = Vector3.zero;
            }

            if (!_hasFollow) _hasFollow = TryGetComponent<SimpleCameraFollow>(out _);
            if (_hasFollow) return;

            // Sin SimpleCameraFollow: aplicar/retirar el offset sin acumular
            transform.position += _offset - _appliedOffset;
            _appliedOffset = _offset;
        }
    }
}
