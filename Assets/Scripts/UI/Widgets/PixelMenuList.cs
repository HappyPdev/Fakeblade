using System;
using System.Collections.Generic;
using FakeBlade.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// Ventana de menú pixel-art con título y filas (botones y selectores).
    ///
    /// - Con EventSystem: teclado, mando y ratón de un solo usuario (menú principal, opciones...).
    /// - Manual: un dispositivo concreto la maneja con MoveFocus/StepFocused/SubmitFocused
    ///   (parámetros de partida, que solo controla J1).
    ///
    /// La altura se ajusta sola al contenido. Las etiquetas se re-traducen al cambiar de idioma.
    /// </summary>
    public class PixelMenuList : MonoBehaviour
    {
        public const int DefaultRowHeight = 10;

        private HUDTheme _theme;
        private int _px;
        private bool _useEventSystem;
        private TextMeshProUGUI _title;
        private string _titleKey;
        private Image _border;
        private readonly List<PixelOptionRow> _rows = new List<PixelOptionRow>(16);
        private readonly List<KeyValuePair<TextMeshProUGUI, string>> _texts = new List<KeyValuePair<TextMeshProUGUI, string>>(4);
        private int _focus = -1;
        private GameObject _lastSelected;

        /// <summary>Se invoca tras re-traducir las etiquetas: el dueño refresca los valores.</summary>
        public event Action OnLanguageRefreshed;

        public IReadOnlyList<PixelOptionRow> Rows => _rows;
        public PixelOptionRow FocusedRow => _focus >= 0 && _focus < _rows.Count ? _rows[_focus] : null;
        public RectTransform RectTransform => (RectTransform)transform;

        #region Creation
        public static PixelMenuList Create(Transform parent, HUDTheme theme, string titleKey, int widthPx, bool useEventSystem)
        {
            int px = Mathf.Max(1, theme.pixelSize);
            RectTransform root = PixelUI.CreateRect("Menu_" + (titleKey ?? "List"), parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(widthPx * px, 100f);

            var list = root.gameObject.AddComponent<PixelMenuList>();
            list.Build(theme, titleKey, useEventSystem);
            return list;
        }

        private void Build(HUDTheme theme, string titleKey, bool useEventSystem)
        {
            _theme = theme;
            _px = Mathf.Max(1, theme.pixelSize);
            _useEventSystem = useEventSystem;
            _titleKey = titleKey;

            // Marco (fuera del layout): sombra desplazada, contorno, borde de color y fondo
            AddFrame("Shadow", theme.panelShadow, 0, new Vector2(2, -2));
            AddFrame("Outline", theme.sphereOutline, 0, Vector2.zero);
            _border = AddFrame("Border", theme.chargeReady, 1, Vector2.zero);
            AddFrame("Background", theme.panelBackground, 2, Vector2.zero);

            var layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6 * _px, 6 * _px, 5 * _px, 6 * _px);
            layout.spacing = 2 * _px;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            if (!string.IsNullOrEmpty(titleKey))
            {
                _title = AddText(titleKey, 9, theme.textColor, TextAlignmentOptions.Center, 14);
            }

            Loc.OnLanguageChanged += RefreshLanguage;
        }

        /// <summary>Imagen estirada sobre la ventana con un margen interior y un desplazamiento (en píxeles de UI).</summary>
        private Image AddFrame(string name, Color color, int inset, Vector2 offsetPx)
        {
            var image = PixelUI.CreateImage(name, transform, color);
            PixelUI.Stretch(image.rectTransform, inset, _px);
            image.rectTransform.anchoredPosition = offsetPx * _px;
            image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return image;
        }

        private void OnDestroy()
        {
            Loc.OnLanguageChanged -= RefreshLanguage;
        }
        #endregion

        #region Content
        public TextMeshProUGUI AddText(string key, float sizePx, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center, int heightPx = 8)
        {
            var text = PixelUI.CreateText("Text", transform, _theme, sizePx * _px, alignment, color);
            text.text = Loc.Get(key);
            text.textWrappingMode = TextWrappingModes.Normal;
            var le = text.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = heightPx * _px;
            le.minHeight = heightPx * _px;
            if (!string.IsNullOrEmpty(key)) _texts.Add(new KeyValuePair<TextMeshProUGUI, string>(text, key));
            return text;
        }

        public void AddSpacer(int heightPx)
        {
            var rt = PixelUI.CreateRect("Spacer", transform);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = heightPx * _px;
            le.minHeight = heightPx * _px;
        }

        public PixelOptionRow AddButton(string labelKey, Action onSubmit, int heightPx = DefaultRowHeight)
        {
            var row = CreateRow(labelKey, true, heightPx);
            row.onSubmit = onSubmit;
            return row;
        }

        public PixelOptionRow AddSelector(string labelKey, string[] options, int index, Action<int> onChanged, int heightPx = DefaultRowHeight)
        {
            var row = CreateRow(labelKey, false, heightPx);
            row.SetOptions(options, index);
            row.onValueChanged = onChanged;
            return row;
        }

        /// <summary>Fila con valor libre: el dueño gestiona los pasos (◄ ►) y la confirmación.</summary>
        public PixelOptionRow AddCustom(string labelKey, string value, Action<int> onStep, Action onSubmit, int heightPx = DefaultRowHeight)
        {
            var row = CreateRow(labelKey, false, heightPx);
            row.SetValueText(value);
            row.onStep = onStep;
            row.onSubmit = onSubmit;
            return row;
        }

        private PixelOptionRow CreateRow(string labelKey, bool isButton, int heightPx)
        {
            var row = PixelOptionRow.Create(transform, _theme, heightPx, isButton, 6f);
            row.LabelKey = labelKey;
            row.SetLabel(Loc.Get(labelKey));
            if (_useEventSystem) row.gameObject.AddComponent<PixelRowSelectable>();
            _rows.Add(row);
            return row;
        }

        public void SetTitle(string text)
        {
            if (_title != null) _title.text = text;
        }

        public void SetBorderColor(Color color)
        {
            if (_border != null) _border.color = color;
        }

        private void RefreshLanguage()
        {
            if (_title != null && !string.IsNullOrEmpty(_titleKey)) _title.text = Loc.Get(_titleKey);
            for (int i = 0; i < _texts.Count; i++) _texts[i].Key.text = Loc.Get(_texts[i].Value);
            for (int i = 0; i < _rows.Count; i++)
                if (!string.IsNullOrEmpty(_rows[i].LabelKey)) _rows[i].SetLabel(Loc.Get(_rows[i].LabelKey));
            OnLanguageRefreshed?.Invoke();
        }
        #endregion

        #region Show / Hide
        public void Show()
        {
            gameObject.SetActive(true);
            if (_useEventSystem) SelectInitial();
            else if (FocusedRow == null || !IsNavigable(_focus)) Focus(FirstNavigable());
        }

        public void Hide()
        {
            if (_useEventSystem && EventSystem.current != null)
            {
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null && selected.transform.IsChildOf(transform)) _lastSelected = selected;
            }
            gameObject.SetActive(false);
        }

        private void SelectInitial()
        {
            if (EventSystem.current == null) return;
            GameObject target = _lastSelected != null && _lastSelected.activeInHierarchy ? _lastSelected : null;
            if (target == null)
            {
                int first = FirstNavigable();
                if (first >= 0) target = _rows[first].gameObject;
            }
            EventSystem.current.SetSelectedGameObject(null);
            if (target != null) EventSystem.current.SetSelectedGameObject(target);
        }

        public void SelectRow(PixelOptionRow row)
        {
            if (row == null) return;
            if (_useEventSystem)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(row.gameObject);
            }
            else
            {
                Focus(_rows.IndexOf(row));
            }
        }
        #endregion

        #region Manual navigation
        private bool IsNavigable(int index) =>
            index >= 0 && index < _rows.Count && _rows[index].gameObject.activeSelf && _rows[index].Interactable;

        private int FirstNavigable()
        {
            for (int i = 0; i < _rows.Count; i++)
                if (IsNavigable(i)) return i;
            return -1;
        }

        public void Focus(int index)
        {
            if (_focus >= 0 && _focus < _rows.Count) _rows[_focus].SetFocused(false);
            _focus = index;
            if (_focus >= 0 && _focus < _rows.Count) _rows[_focus].SetFocused(true);
        }

        public void MoveFocus(int dir)
        {
            if (_rows.Count == 0) return;
            int index = _focus;
            for (int i = 0; i < _rows.Count; i++)
            {
                index = (index + dir + _rows.Count) % _rows.Count;
                if (IsNavigable(index))
                {
                    Focus(index);
                    return;
                }
            }
        }

        public void StepFocused(int dir) => FocusedRow?.Step(dir);

        public void SubmitFocused() => FocusedRow?.Submit();

        /// <summary>Si la fila enfocada se ha ocultado, busca la siguiente navegable.</summary>
        public void EnsureValidFocus()
        {
            if (!IsNavigable(_focus)) MoveFocus(1);
        }
        #endregion
    }
}
