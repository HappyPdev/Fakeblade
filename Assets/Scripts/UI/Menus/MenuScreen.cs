using System;
using FakeBlade.Core;
using UnityEngine;

namespace FakeBlade.UI
{
    /// <summary>
    /// Base de las pantallas de menú (opciones, controles, información...).
    /// Envuelve una PixelMenuList, cierra con Atrás (Esc/Retroceso/B) y, mientras está abierta,
    /// bloquea la pausa del GameManager para que Esc no reanude la partida por debajo.
    /// </summary>
    public abstract class MenuScreen : MonoBehaviour
    {
        protected HUDTheme Theme;
        protected PixelMenuList List;
        private Action _onClose;
        private bool _isOpen;
        private int _openedFrame = -1;

        public bool IsOpen => _isOpen;

        protected void Initialize(HUDTheme theme, string titleKey, int widthPx, Action onClose)
        {
            Theme = theme;
            _onClose = onClose;

            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            List = PixelMenuList.Create(transform, theme, titleKey, widthPx, true);
            List.OnLanguageRefreshed += OnLanguageRefreshed;
            BuildContent();
            gameObject.SetActive(false);
        }

        protected abstract void BuildContent();
        protected virtual void OnLanguageRefreshed() { }
        protected virtual void OnOpened() { }

        /// <summary>Devuelve false para impedir cerrar con Atrás (p. ej. esperando una tecla).</summary>
        protected virtual bool CanCloseWithBack => true;

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            _openedFrame = Time.frameCount;
            MenuStack.Push();
            gameObject.SetActive(true);
            OnOpened();
            List.Show();
        }

        public void Close()
        {
            if (!_isOpen) return;
            Dismiss();
            _onClose?.Invoke();
        }

        /// <summary>Cierra sin avisar al dueño (p. ej. al reiniciar la partida desde fuera).</summary>
        public void Dismiss()
        {
            if (!_isOpen) return;
            _isOpen = false;
            MenuStack.Pop();
            List.Hide();
            gameObject.SetActive(false);
        }

        protected virtual void Update()
        {
            // El Atrás que cerró un submenú y reabrió esta pantalla en el mismo frame no la cierra también
            if (_isOpen && CanCloseWithBack && Time.frameCount != _openedFrame && MenuInput.AnyBackPressed())
                Close();
        }

        protected virtual void OnDestroy()
        {
            if (_isOpen) MenuStack.Pop();
        }

        protected static T CreateScreen<T>(string name, Transform parent) where T : MenuScreen
        {
            RectTransform rt = PixelUI.CreateRect(name, parent);
            return rt.gameObject.AddComponent<T>();
        }

        protected static string[] YesNo() => new[] { Loc.Get("NO"), Loc.Get("YES") };
    }
}
