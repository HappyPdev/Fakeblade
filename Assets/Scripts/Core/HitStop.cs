using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Hit-stop: congela casi por completo el juego unos instantes para remarcar un golpe
    /// (p. ej. un parry). Usa tiempo real, así que dura lo mismo aunque el juego vaya a cámara lenta.
    /// Si durante el hit-stop se pausa la partida (timeScale = 0), no restaura la velocidad.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        private const float FrozenTimeScale = 0.02f;

        private static HitStop _instance;

        private float _remaining;
        private bool _active;

        public static void Trigger(float duration)
        {
            if (duration <= 0f || !Application.isPlaying || Time.timeScale <= 0f) return;

            if (_instance == null)
                _instance = new GameObject("[HitStop]").AddComponent<HitStop>();

            _instance._remaining = Mathf.Max(_instance._remaining, duration);
            if (_instance._active) return;

            _instance._active = true;
            Time.timeScale = FrozenTimeScale;
        }

        private void Update()
        {
            if (!_active) return;

            _remaining -= Time.unscaledDeltaTime;
            if (_remaining > 0f) return;

            _active = false;
            // Si mientras tanto se ha pausado la partida, la pausa manda
            if (Time.timeScale > 0f) Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            if (_active && Time.timeScale > 0f) Time.timeScale = 1f;
        }
    }
}
