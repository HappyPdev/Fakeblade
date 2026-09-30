using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// Contador de FPS (Opciones → Mostrar FPS). Persiste entre escenas y solo
    /// actualiza el texto dos veces por segundo con strings cacheadas (sin GC).
    /// </summary>
    public class FpsCounter : MonoBehaviour
    {
        private const float Interval = 0.5f;

        private static FpsCounter _instance;

        private TextMeshProUGUI _text;
        private float _timer;
        private int _frames;

        public static void SetVisible(bool visible)
        {
            if (!Application.isPlaying) return;

            if (visible && _instance == null)
                Create();

            if (_instance != null)
                _instance.gameObject.SetActive(visible);
        }

        private static void Create()
        {
            var go = new GameObject("[FPS]", typeof(RectTransform));
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _instance = go.AddComponent<FpsCounter>();

            var theme = HUDTheme.CreateDefault();
            var bg = PixelUI.CreateImage("Background", go.transform, new Color(0f, 0f, 0f, 0.6f));
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-8f, -8f);
            rt.sizeDelta = new Vector2(120f, 36f);

            _instance._text = PixelUI.CreateText("Text", rt, theme, 24f, TextAlignmentOptions.Center, Color.white);
            PixelUI.Stretch(_instance._text.rectTransform, 0, 1);
        }

        private void Update()
        {
            _frames++;
            _timer += Time.unscaledDeltaTime;
            if (_timer < Interval) return;

            int fps = Mathf.RoundToInt(_frames / _timer);
            _frames = 0;
            _timer = 0f;

            _text.text = PixelUI.Number(fps);
            _text.color = fps >= 55 ? new Color(0.4f, 1f, 0.5f) : fps >= 30 ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.35f, 0.35f);
        }
    }
}
