using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Velocidad "normal" del juego: 1, salvo en el sandbox (cámara lenta o pausa de depuración,
    /// GDD 6.3). Al reanudar tras una pausa, la cuenta atrás o un hit-stop se vuelve a ella, no a 1.
    /// </summary>
    public static class GameTime
    {
        public static float NormalScale { get; private set; } = 1f;

        /// <summary>Cambia la velocidad normal. Si el juego corre ahora mismo, se aplica ya.</summary>
        public static void SetNormalScale(float scale, bool applyNow)
        {
            NormalScale = Mathf.Max(0f, scale);
            if (applyNow) Time.timeScale = NormalScale;
        }

        /// <summary>Vuelve a la velocidad normal (tras una pausa, la cuenta atrás o un hit-stop).</summary>
        public static void Resume() => Time.timeScale = NormalScale;
    }
}
