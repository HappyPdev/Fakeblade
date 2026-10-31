using FakeBlade.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// Ventana de indicaciones de Aprende a jugar (GDD 6.7): en el margen izquierdo, a la altura del centro.
    /// Paso (1 de 9), título, explicación y una fila por jugador con el botón que toca en su dispositivo y lo que
    /// lleva del objetivo. Al completarlo todos, «¡BIEN!» hasta el paso siguiente.
    /// Solo es vista: la lógica la lleva TutorialController. Los contadores usan SetText (sin GC).
    /// </summary>
    public class TutorialHintPanel : MonoBehaviour
    {
        private const int WidthPx = 96;
        private const int MarginPx = 6;
        private const int RowHeightPx = 8;
        private const int PaddingPx = 5;
        private const int MaxRows = 3;
        /// <summary>El nombre del botón va entre la insignia (izquierda) y el contador (derecha).</summary>
        private const int ButtonLeftPx = 15;
        private const int ButtonRightPx = 18;

        private HUDTheme _theme;
        private int _px;

        private TextMeshProUGUI _stepText;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private TextMeshProUGUI _wellDone;
        private readonly GameObject[] _rows = new GameObject[MaxRows];
        private readonly Image[] _badges = new Image[MaxRows];
        private readonly TextMeshProUGUI[] _badgeTexts = new TextMeshProUGUI[MaxRows];
        private readonly TextMeshProUGUI[] _buttons = new TextMeshProUGUI[MaxRows];
        private readonly TextMeshProUGUI[] _progress = new TextMeshProUGUI[MaxRows];
        private readonly int[] _shownProgress = new int[MaxRows];
        private readonly int[] _shownGoal = new int[MaxRows];

        public static TutorialHintPanel Create(Transform parent, HUDTheme theme, int sortingOrder)
        {
            Canvas canvas = PixelUI.CreateOverlayCanvas("[TutorialUI]", parent, theme, sortingOrder);
            RectTransform root = PixelUI.CreateRect("HintPanel", canvas.transform);
            var panel = root.gameObject.AddComponent<TutorialHintPanel>();
            panel.Build(theme, root);
            return panel;
        }

        private void Build(HUDTheme theme, RectTransform root)
        {
            _theme = theme;
            _px = Mathf.Max(1, theme.pixelSize);

            root.anchorMin = root.anchorMax = new Vector2(0f, 0.5f);
            root.pivot = new Vector2(0f, 0.5f);
            root.anchoredPosition = new Vector2(MarginPx * _px, 0f);
            root.sizeDelta = new Vector2(WidthPx * _px, 0f);

            Frame(root, "Outline", theme.sphereOutline, 0);
            Frame(root, "Border", theme.textDimColor, 1);
            Frame(root, "Background", theme.panelBackground, 2);

            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(PaddingPx * _px, PaddingPx * _px, PaddingPx * _px, PaddingPx * _px);
            layout.spacing = 2 * _px;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _stepText = Text("Step", root, 3.5f, theme.textDimColor);
            _title = Text("Title", root, 6f, theme.chargeReady);
            _body = Text("Body", root, 4f, theme.textColor);
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.richText = true;

            for (int i = 0; i < MaxRows; i++) BuildRow(root, i);

            _wellDone = Text("WellDone", root, 7f, theme.healthHigh);
            _wellDone.alignment = TextAlignmentOptions.Center;
            _wellDone.gameObject.SetActive(false);
        }

        private void Frame(Transform parent, string name, Color color, int inset)
        {
            Image image = PixelUI.CreateImage(name, parent, color);
            PixelUI.Stretch(image.rectTransform, inset, _px);
            image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        private TextMeshProUGUI Text(string name, Transform parent, float fontPx, Color color)
        {
            return PixelUI.CreateText(name, parent, _theme, fontPx * _px, TextAlignmentOptions.TopLeft, color);
        }

        private void BuildRow(Transform parent, int index)
        {
            RectTransform row = PixelUI.CreateRect($"Player_{index}", parent);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = RowHeightPx * _px;
            _rows[index] = row.gameObject;

            _badges[index] = PixelUI.CreateImage("Badge", row, Color.white);
            // Centrada en la fila, que puede crecer a dos líneas
            RectTransform badgeRt = _badges[index].rectTransform;
            badgeRt.anchorMin = badgeRt.anchorMax = badgeRt.pivot = new Vector2(0f, 0.5f);
            badgeRt.sizeDelta = new Vector2(12 * _px, RowHeightPx * _px);
            _badgeTexts[index] = PixelUI.CreateText("BadgeText", _badges[index].transform, _theme, 5f * _px,
                TextAlignmentOptions.Center, _theme.panelBackground);
            PixelUI.Stretch(_badgeTexts[index].rectTransform, 0, _px);

            // El botón ocupa lo que queda entre la insignia y el contador
            _buttons[index] = PixelUI.CreateText("Button", row, _theme, 4.5f * _px, TextAlignmentOptions.MidlineLeft, _theme.textColor);
            _buttons[index].textWrappingMode = TextWrappingModes.Normal;
            RectTransform brt = _buttons[index].rectTransform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(ButtonLeftPx * _px, 0f);
            brt.offsetMax = new Vector2(-ButtonRightPx * _px, 0f);

            _progress[index] = PixelUI.CreateText("Progress", row, _theme, 4.5f * _px, TextAlignmentOptions.MidlineRight, _theme.textDimColor);
            RectTransform prt = _progress[index].rectTransform;
            prt.anchorMin = new Vector2(1f, 0f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(1f, 0.5f);
            prt.sizeDelta = new Vector2(17 * _px, 0f);

            _shownProgress[index] = _shownGoal[index] = -1;
            row.gameObject.SetActive(false);
        }

        /// <summary>Paso nuevo: «PASO 1 DE 9», título y explicación (puede llevar color con rich text).</summary>
        public void SetStep(string stepLabel, string title, string body)
        {
            _stepText.text = stepLabel;
            _title.text = title;
            _body.text = body;
            _wellDone.gameObject.SetActive(false);
        }

        public void SetPlayerCount(int count)
        {
            for (int i = 0; i < MaxRows; i++) _rows[i].SetActive(i < count);
        }

        /// <summary>Fila de un jugador: insignia de su color y botón que le toca. Solo al cambiar de paso.</summary>
        public void SetPlayer(int index, string badge, Color color, string button)
        {
            if (index < 0 || index >= MaxRows) return;
            _badges[index].color = color;
            _badgeTexts[index].text = badge;
            _buttons[index].text = button;
            _shownProgress[index] = _shownGoal[index] = -1;

            // Un nombre largo («MANTÉN BARRA ESPACIADORA») pasa a dos líneas y la fila crece (GDD 9.2, regla 4)
            float width = (WidthPx - 2 * PaddingPx - ButtonLeftPx - ButtonRightPx) * _px;
            float textHeight = _buttons[index].GetPreferredValues(button, width, 0f).y;
            var le = _rows[index].GetComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = Mathf.Max(RowHeightPx * _px, Mathf.Ceil(textHeight + _px));
        }

        /// <summary>Lo que lleva del objetivo. Sin GC: solo reescribe si cambia.</summary>
        public void SetProgress(int index, int progress, int goal)
        {
            if (index < 0 || index >= MaxRows) return;
            progress = Mathf.Min(progress, goal);
            if (_shownProgress[index] == progress && _shownGoal[index] == goal) return;
            _shownProgress[index] = progress;
            _shownGoal[index] = goal;

            TextMeshProUGUI text = _progress[index];
            text.SetText("{0}/{1}", progress, goal);
            text.color = progress >= goal ? _theme.healthHigh : _theme.textDimColor;
        }

        /// <summary>Todos han cumplido el objetivo: «¡BIEN!» hasta el paso siguiente.</summary>
        public void ShowWellDone(string text)
        {
            _wellDone.text = text;
            _wellDone.gameObject.SetActive(true);
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
