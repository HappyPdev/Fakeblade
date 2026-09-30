using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// Fila de menú pixel-art. Dos modos:
    /// - Botón:    [      JUGAR      ]
    /// - Selector: [ SOMBRAS    < ALTAS > ]
    ///
    /// No depende del EventSystem: se puede manejar a mano (columnas de la selección de peonzas,
    /// una por mando) o añadirle PixelRowSelectable para menús de un usuario con ratón.
    /// </summary>
    public class PixelOptionRow : MonoBehaviour
    {
        private HUDTheme _theme;
        private int _px;
        private Image _background;
        private Image _accent;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _value;
        private TextMeshProUGUI _leftArrow;
        private TextMeshProUGUI _rightArrow;

        private string[] _options;
        private int _index;
        private bool _focused;
        private bool _interactable = true;
        private Color _accentColor;

        /// <summary>Clave de localización de la etiqueta (la lista la refresca al cambiar idioma).</summary>
        public string LabelKey { get; set; }
        public bool IsButton { get; private set; }
        public bool IsFocused => _focused;
        public bool Interactable => _interactable;
        public int Index => _index;

        /// <summary>Cambio de valor (índice nuevo) al moverse izquierda/derecha por las opciones.</summary>
        public Action<int> onValueChanged;
        /// <summary>Paso a izquierda (-1) o derecha (+1) cuando la fila no tiene lista de opciones.</summary>
        public Action<int> onStep;
        public Action onSubmit;

        #region Creation
        public static PixelOptionRow Create(Transform parent, HUDTheme theme, int heightPx, bool isButton, float fontPx = 5f)
        {
            int px = Mathf.Max(1, theme.pixelSize);
            RectTransform root = PixelUI.CreateRect(isButton ? "Button" : "Row", parent);
            root.sizeDelta = new Vector2(0f, heightPx * px);

            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = heightPx * px;
            layout.minHeight = heightPx * px;
            layout.flexibleWidth = 1f;

            var row = root.gameObject.AddComponent<PixelOptionRow>();
            row.Build(theme, isButton, fontPx);
            return row;
        }

        private void Build(HUDTheme theme, bool isButton, float fontPx)
        {
            _theme = theme;
            _px = Mathf.Max(1, theme.pixelSize);
            IsButton = isButton;
            _accentColor = theme.chargeReady;

            _background = PixelUI.CreateImage("Background", transform, theme.barBackground);
            _background.raycastTarget = true; // recibe el ratón
            PixelUI.Stretch(_background.rectTransform, 0, _px);

            _accent = PixelUI.CreateImage("Accent", transform, _accentColor);
            var accentRt = _accent.rectTransform;
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(0f, 1f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.sizeDelta = new Vector2(_px, 0f);
            accentRt.anchoredPosition = Vector2.zero;

            float size = fontPx * _px;
            if (isButton)
            {
                _label = PixelUI.CreateText("Label", transform, theme, size, TextAlignmentOptions.Center, theme.textColor);
                PixelUI.Stretch(_label.rectTransform, 1, _px);
            }
            else
            {
                _label = PixelUI.CreateText("Label", transform, theme, size, TextAlignmentOptions.MidlineLeft, theme.textColor);
                var labelRt = _label.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(0.5f, 1f);
                labelRt.offsetMin = new Vector2(4 * _px, 0f);
                labelRt.offsetMax = Vector2.zero;

                _value = PixelUI.CreateText("Value", transform, theme, size, TextAlignmentOptions.Center, theme.chargeReady);
                var valueRt = _value.rectTransform;
                valueRt.anchorMin = new Vector2(0.5f, 0f);
                valueRt.anchorMax = new Vector2(1f, 1f);
                valueRt.offsetMin = new Vector2(8 * _px, 0f);
                valueRt.offsetMax = new Vector2(-8 * _px, 0f);

                _leftArrow = CreateArrow("<", -1, new Vector2(0.5f, 0.5f), new Vector2(4 * _px, 0f), size);
                _rightArrow = CreateArrow(">", 1, new Vector2(1f, 0.5f), new Vector2(-4 * _px, 0f), size);
            }

            ApplyVisuals();
        }

        private TextMeshProUGUI CreateArrow(string glyph, int dir, Vector2 anchor, Vector2 offset, float size)
        {
            var arrow = PixelUI.CreateText(dir < 0 ? "Left" : "Right", transform, _theme, size, TextAlignmentOptions.Center, _theme.textDimColor);
            var rt = arrow.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(8 * _px, 0f);
            rt.anchorMin = new Vector2(anchor.x, 0f);
            rt.anchorMax = new Vector2(anchor.x, 1f);
            arrow.text = glyph;
            arrow.raycastTarget = true;

            var click = arrow.gameObject.AddComponent<PixelArrowClick>();
            click.Row = this;
            click.Direction = dir;
            return arrow;
        }
        #endregion

        #region Content
        public void SetLabel(string text) => _label.text = text;

        public void SetOptions(string[] options, int index)
        {
            _options = options;
            _index = options == null || options.Length == 0 ? 0 : Mathf.Clamp(index, 0, options.Length - 1);
            RefreshValue();
        }

        /// <summary>Cambia el índice sin avisar (p. ej. al cargar valores guardados).</summary>
        public void SetIndex(int index)
        {
            if (_options == null || _options.Length == 0) return;
            _index = Mathf.Clamp(index, 0, _options.Length - 1);
            RefreshValue();
        }

        public void SetValueText(string text)
        {
            if (_value != null) _value.text = text;
        }

        public void SetValueColor(Color color)
        {
            if (_value != null) _value.color = color;
        }

        public void SetAccentColor(Color color)
        {
            _accentColor = color;
            ApplyVisuals();
        }

        private void RefreshValue()
        {
            if (_value != null && _options != null && _options.Length > 0)
                _value.text = _options[_index];
        }
        #endregion

        #region Interaction
        /// <summary>Izquierda (-1) / derecha (+1). Recorre las opciones en bucle.</summary>
        public void Step(int dir)
        {
            if (!_interactable || IsButton) return;

            if (_options != null && _options.Length > 0)
            {
                _index = (_index + dir + _options.Length) % _options.Length;
                RefreshValue();
                onValueChanged?.Invoke(_index);
            }

            onStep?.Invoke(dir);
        }

        /// <summary>Confirmar: en botones ejecuta la acción; en selectores avanza una opción.</summary>
        public void Submit()
        {
            if (!_interactable) return;

            if (onSubmit != null) onSubmit.Invoke();
            else if (!IsButton) Step(1);
        }

        public void SetFocused(bool focused)
        {
            if (_focused == focused) return;
            _focused = focused;
            ApplyVisuals();
        }

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            if (TryGetComponent(out Selectable selectable)) selectable.interactable = interactable;
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            if (_background == null) return;

            Color bg = _focused ? Color.Lerp(_theme.barBackground, _accentColor, 0.45f) : _theme.barBackground;
            if (!_interactable) bg = Color.Lerp(bg, _theme.panelBackground, 0.6f);
            _background.color = bg;
            _accent.enabled = _focused;
            _accent.color = _accentColor;

            Color text = _interactable ? (_focused ? Color.white : _theme.textColor) : _theme.textDimColor;
            _label.color = text;
            if (_leftArrow != null)
            {
                Color arrow = _focused && _interactable ? Color.white : _theme.textDimColor;
                _leftArrow.color = arrow;
                _rightArrow.color = arrow;
            }
        }
        #endregion
    }

}
