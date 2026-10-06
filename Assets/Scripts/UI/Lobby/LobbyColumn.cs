using System.Collections.Generic;
using FakeBlade.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// Columna de un jugador en la selección de peonzas (GDD 9.2.1).
    ///
    /// Vista vacía:  "Mantén A / Espacio / Ctrl der. para unirte" + barra de progreso.
    /// Vista jugador: cabecera (J1 + dispositivo), vista previa 3D, arquetipo, stats y filas
    ///                Preset / Punta / Cuerpo / Disco / Núcleo / Color / Equipo / Confirmar.
    /// Capa "¡LISTO!" cuando el jugador confirma.
    ///
    /// Solo es vista: la lógica (qué valor tiene cada fila) la lleva LobbyController.
    /// </summary>
    public class LobbyColumn : MonoBehaviour
    {
        private const int RowHeight = 8;
        /// <summary>Filas de pieza: más altas, con el arquetipo debajo del nombre.</summary>
        private const int PartRowHeight = 11;
        private const int StatCount = 5;
        /// <summary>Piezas que aportan a las barras: punta, cuerpo, disco y núcleo (orden de ComponentSlot).</summary>
        public const int PartCount = 4;
        /// <summary>Las barras se dibujan por pasos (estética pixel).</summary>
        private const float StatSteps = 40f;

        private HUDTheme _theme;
        private int _px;

        private Image _border;
        private GameObject _emptyView;
        private GameObject _playerView;
        private GameObject _readyOverlay;

        private TextMeshProUGUI _joinText;
        private Image _joinFill;

        private Image _badge;
        private TextMeshProUGUI _badgeText;
        private TextMeshProUGUI _deviceText;
        private RawImage _preview;
        private TextMeshProUGUI _archetypeText;
        private TextMeshProUGUI _specialText;
        private readonly TextMeshProUGUI[] _statLabels = new TextMeshProUGUI[StatCount];
        private readonly StatBar[] _statBars = new StatBar[StatCount];
        private int _blinkPart = -1;
        private bool _blinkOn;
        private TextMeshProUGUI _hintText;
        private Image _leaveFill;
        private TextMeshProUGUI _readyText;

        private readonly List<PixelOptionRow> _rows = new List<PixelOptionRow>(8);
        private int _focus;

        public PixelOptionRow PresetRow { get; private set; }
        public PixelOptionRow TipRow { get; private set; }
        public PixelOptionRow BodyRow { get; private set; }
        public PixelOptionRow BladeRow { get; private set; }
        public PixelOptionRow CoreRow { get; private set; }
        public PixelOptionRow ColorRow { get; private set; }
        public PixelOptionRow TeamRow { get; private set; }
        public PixelOptionRow ConfirmRow { get; private set; }

        public PixelOptionRow FocusedRow => _rows[_focus];

        #region Creation
        public static LobbyColumn Create(Transform parent, HUDTheme theme)
        {
            RectTransform root = PixelUI.CreateRect("Column", parent);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;

            var column = root.gameObject.AddComponent<LobbyColumn>();
            column.Build(theme);
            return column;
        }

        private void Build(HUDTheme theme)
        {
            _theme = theme;
            _px = Mathf.Max(1, theme.pixelSize);

            var outline = PixelUI.CreateImage("Outline", transform, theme.sphereOutline);
            PixelUI.Stretch(outline.rectTransform, 0, _px);
            _border = PixelUI.CreateImage("Border", transform, theme.textDimColor);
            PixelUI.Stretch(_border.rectTransform, 1, _px);
            var bg = PixelUI.CreateImage("Background", transform, theme.panelBackground);
            PixelUI.Stretch(bg.rectTransform, 2, _px);

            BuildEmptyView();
            BuildPlayerView();
            BuildReadyOverlay();
        }

        private void BuildEmptyView()
        {
            RectTransform view = PixelUI.CreateRect("EmptyView", transform);
            PixelUI.Stretch(view, 3, _px);
            _emptyView = view.gameObject;

            _joinText = PixelUI.CreateText("JoinText", view, _theme, 6 * _px, TextAlignmentOptions.Center, _theme.textColor);
            _joinText.textWrappingMode = TextWrappingModes.Normal;
            var rt = _joinText.rectTransform;
            rt.anchorMin = new Vector2(0.05f, 0.4f);
            rt.anchorMax = new Vector2(0.95f, 0.6f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var barBg = PixelUI.CreateImage("JoinBar", view, _theme.barBackground);
            var brt = barBg.rectTransform;
            brt.anchorMin = new Vector2(0.15f, 0.36f);
            brt.anchorMax = new Vector2(0.85f, 0.36f);
            brt.sizeDelta = new Vector2(0f, 3 * _px);
            _joinFill = PixelUI.CreateFilledImage("Fill", barBg.transform, _theme.chargeReady, null,
                Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left);
            PixelUI.Stretch(_joinFill.rectTransform, 0, _px);
            _joinFill.fillAmount = 0f;
        }

        private void BuildPlayerView()
        {
            RectTransform view = PixelUI.CreateRect("PlayerView", transform);
            PixelUI.Stretch(view, 3, _px);
            _playerView = view.gameObject;

            var layout = view.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(3 * _px, 3 * _px, 3 * _px, 3 * _px);
            layout.spacing = _px;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Cabecera: badge J1 + dispositivo
            RectTransform header = Fixed("Header", view, 8);
            _badge = PixelUI.CreateImage("Badge", header, Color.white);
            PixelUI.Place(_badge.rectTransform, 0, 0, 12, 8, _px);
            _badgeText = PixelUI.CreateText("BadgeText", _badge.transform, _theme, 6 * _px, TextAlignmentOptions.Center, _theme.panelBackground);
            PixelUI.Stretch(_badgeText.rectTransform, 0, _px);
            _deviceText = PixelUI.CreateText("Device", header, _theme, 4 * _px, TextAlignmentOptions.MidlineLeft, _theme.textDimColor);
            var drt = _deviceText.rectTransform;
            drt.anchorMin = new Vector2(0f, 0f);
            drt.anchorMax = new Vector2(1f, 1f);
            drt.offsetMin = new Vector2(15 * _px, 0f);
            drt.offsetMax = Vector2.zero;

            // Vista previa 3D
            RectTransform previewBox = Fixed("PreviewBox", view, 64);
            var previewFrame = PixelUI.CreateImage("Frame", previewBox, _theme.sphereOutline);
            var prt = previewFrame.rectTransform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(64 * _px, 64 * _px);
            var previewObj = PixelUI.CreateRect("Preview", previewFrame.transform);
            PixelUI.Stretch(previewObj, 1, _px);
            _preview = previewObj.gameObject.AddComponent<RawImage>();
            _preview.raycastTarget = false;

            _archetypeText = FixedText("Archetype", view, 7, 6, _theme.chargeReady);
            _specialText = FixedText("Special", view, 6, 4, _theme.textDimColor);

            for (int i = 0; i < StatCount; i++)
            {
                RectTransform stat = Fixed($"Stat_{i}", view, 4);
                _statLabels[i] = PixelUI.CreateText("Label", stat, _theme, 3.5f * _px, TextAlignmentOptions.MidlineLeft, _theme.textDimColor);
                var lrt = _statLabels[i].rectTransform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(0.38f, 1f);
                lrt.offsetMin = lrt.offsetMax = Vector2.zero;

                var barBg = PixelUI.CreateImage("Bar", stat, _theme.barBackground);
                var brt = barBg.rectTransform;
                brt.anchorMin = new Vector2(0.4f, 0.2f);
                brt.anchorMax = new Vector2(1f, 0.8f);
                brt.offsetMin = brt.offsetMax = Vector2.zero;
                _statBars[i] = BuildStatBar(barBg.transform);
            }

            Fixed("Spacer", view, 1);

            PresetRow = AddRow(view, false);
            TipRow = AddRow(view, false, PartRowHeight);
            BodyRow = AddRow(view, false, PartRowHeight);
            BladeRow = AddRow(view, false, PartRowHeight);
            CoreRow = AddRow(view, false, PartRowHeight);
            ColorRow = AddRow(view, false);
            TeamRow = AddRow(view, false);
            ConfirmRow = AddRow(view, true);

            _hintText = FixedText("Hint", view, 6, 3.5f, _theme.textDimColor);

            RectTransform leave = Fixed("LeaveBar", view, 1);
            _leaveFill = PixelUI.CreateFilledImage("Fill", leave, _theme.healthLow, null,
                Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left);
            PixelUI.Stretch(_leaveFill.rectTransform, 0, _px);
            _leaveFill.fillAmount = 0f;
        }

        private void BuildReadyOverlay()
        {
            var overlay = PixelUI.CreateImage("ReadyOverlay", transform, new Color(0f, 0f, 0f, 0.55f));
            PixelUI.Stretch(overlay.rectTransform, 2, _px);
            _readyOverlay = overlay.gameObject;

            _readyText = PixelUI.CreateText("Ready", overlay.transform, _theme, 14 * _px, TextAlignmentOptions.Center, Color.white);
            PixelUI.Stretch(_readyText.rectTransform, 0, _px);
            _readyOverlay.SetActive(false);
        }

        private RectTransform Fixed(string name, Transform parent, int heightPx)
        {
            RectTransform rt = PixelUI.CreateRect(name, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = heightPx * _px;
            le.minHeight = heightPx * _px;
            return rt;
        }

        private TextMeshProUGUI FixedText(string name, Transform parent, int heightPx, float fontPx, Color color)
        {
            var text = PixelUI.CreateText(name, parent, _theme, fontPx * _px, TextAlignmentOptions.Center, color);
            var le = text.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = heightPx * _px;
            le.minHeight = heightPx * _px;
            return text;
        }

        private PixelOptionRow AddRow(Transform parent, bool isButton, int heightPx = RowHeight)
        {
            var row = PixelOptionRow.Create(parent, _theme, heightPx, isButton, 4.5f);
            _rows.Add(row);
            return row;
        }
        #endregion

        #region Views
        public void ShowEmpty(float joinProgress)
        {
            _emptyView.SetActive(true);
            _playerView.SetActive(false);
            _readyOverlay.SetActive(false);
            _border.color = _theme.textDimColor;
            _joinText.text = Loc.Get("LOBBY_JOIN");
            _joinFill.fillAmount = Mathf.Clamp01(joinProgress);
        }

        public void SetJoinProgress(float progress) => _joinFill.fillAmount = Mathf.Clamp01(progress);

        public void ShowPlayer(int playerNumber, Color color, string deviceLabel)
        {
            _emptyView.SetActive(false);
            _playerView.SetActive(true);
            _border.color = color;
            _badge.color = color;
            _badgeText.text = Loc.Format("PLAYER_BADGE", playerNumber);
            _deviceText.text = deviceLabel;
            _readyText.color = color;

            PresetRow.SetLabel(Loc.Get("LOBBY_PRESET"));
            TipRow.SetLabel(Loc.Get("SLOT_TIP"));
            BodyRow.SetLabel(Loc.Get("SLOT_BODY"));
            BladeRow.SetLabel(Loc.Get("SLOT_BLADE"));
            CoreRow.SetLabel(Loc.Get("SLOT_CORE"));
            ColorRow.SetLabel(Loc.Get("LOBBY_COLOR"));
            TeamRow.SetLabel(Loc.Get("LOBBY_TEAM"));
            ConfirmRow.SetLabel(Loc.Get("LOBBY_CONFIRM"));
            _hintText.text = Loc.Get("LOBBY_HINT");

            for (int i = 0; i < _rows.Count; i++) _rows[i].SetAccentColor(color);
        }

        public void SetBorderColor(Color color)
        {
            _border.color = color;
            _badge.color = color;
            _readyText.color = color;
            for (int i = 0; i < _rows.Count; i++) _rows[i].SetAccentColor(color);
        }

        public void SetPreview(Texture texture) => _preview.texture = texture;

        public void SetArchetype(string archetype, string special, Color archetypeColor)
        {
            _archetypeText.text = archetype;
            _archetypeText.color = archetypeColor;
            _specialText.text = special;
        }

        /// <summary>
        /// Barra de stat por tramos: lo que da la peonza sin piezas y lo que suma cada pieza
        /// (parts, en el orden de ComponentSlot), uno tras otro. Lo que resta una pieza se ve en
        /// rojo al final, hasta el valor final (ya limitado). scale = valor que llena la barra.
        /// </summary>
        public void SetStat(int index, string label, float baseValue, float[] parts, float finalValue, float scale)
        {
            if (index < 0 || index >= StatCount) return;
            _statLabels[index].text = label;

            StatBar bar = _statBars[index];
            float inv = 1f / Mathf.Max(0.0001f, scale);
            float x = Snap(baseValue * inv);
            Place(bar.Base, 0f, x);

            for (int p = 0; p < PartCount; p++)
            {
                float v = parts[p] * inv;
                if (v <= 0f)
                {
                    Place(bar.Gain[p], 0f, 0f);
                    continue;
                }
                float end = Snap(x + v);
                Place(bar.Gain[p], x, end);
                x = end;
            }

            // Lo que restan las piezas se come el final de lo sumado, sin pasar del valor final
            float final = Snap(finalValue * inv);
            float lossEnd = x;
            for (int p = 0; p < PartCount; p++)
            {
                float v = parts[p] * inv;
                if (v >= 0f)
                {
                    Place(bar.Loss[p], 0f, 0f);
                    continue;
                }
                float start = Snap(lossEnd + v);
                Place(bar.Loss[p], Mathf.Max(start, final), lossEnd);
                lossEnd = start;
            }
        }

        public void SetReady(bool ready)
        {
            _readyOverlay.SetActive(ready);
            _readyText.text = Loc.Get("LOBBY_READY");
        }

        public void SetLeaveProgress(float progress) => _leaveFill.fillAmount = Mathf.Clamp01(progress);

        public void SetHint(string text) => _hintText.text = text;
        #endregion

        #region Focus
        public void ResetFocus()
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].SetFocused(false);
            _focus = 0;
            _rows[0].SetFocused(true);
        }

        public void MoveFocus(int dir)
        {
            _rows[_focus].SetFocused(false);
            _focus = (_focus + dir + _rows.Count) % _rows.Count;
            _rows[_focus].SetFocused(true);
        }

        /// <summary>Pieza de la fila con el foco (0-3, orden de ComponentSlot) o -1.</summary>
        private int FocusedPart
        {
            get
            {
                PixelOptionRow row = FocusedRow;
                if (row == TipRow) return (int)ComponentSlot.Tip;
                if (row == BodyRow) return (int)ComponentSlot.Body;
                if (row == BladeRow) return (int)ComponentSlot.Blade;
                if (row == CoreRow) return (int)ComponentSlot.Core;
                return -1;
            }
        }

        /// <summary>El tramo de la pieza con el foco parpadea en todas las barras.</summary>
        private void Update()
        {
            if (_playerView == null || !_playerView.activeSelf || _rows.Count == 0) return;

            int part = FocusedPart;
            bool on = part >= 0 && Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.5f;
            if (part == _blinkPart && on == _blinkOn) return;

            SetPartHighlight(_blinkPart, false);
            SetPartHighlight(part, on);
            _blinkPart = part;
            _blinkOn = on;
        }
        #endregion

        #region Stat bars
        /// <summary>Tramos de una barra: base, lo que suma cada pieza y lo que resta cada pieza (encima).</summary>
        private sealed class StatBar
        {
            public Image Base;
            public readonly Image[] Gain = new Image[PartCount];
            public readonly Image[] Loss = new Image[PartCount];
        }

        private StatBar BuildStatBar(Transform area)
        {
            var bar = new StatBar { Base = Segment(area, _theme.statBase) };
            for (int p = 0; p < PartCount; p++) bar.Gain[p] = Segment(area, GainColor(p));
            for (int p = 0; p < PartCount; p++) bar.Loss[p] = Segment(area, LossColor(p));
            return bar;
        }

        private static Image Segment(Transform area, Color color)
        {
            var image = PixelUI.CreateImage("Segment", area, color);
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        /// <summary>Tonos del verde de la barra, distintos entre piezas vecinas.</summary>
        private Color GainColor(int part)
        {
            Color green = _theme.healthHigh;
            switch (part)
            {
                case 0: return Color.Lerp(green, Color.white, 0.45f);            // punta
                case 1: return green;                                              // cuerpo
                case 2: return Color.Lerp(green, Color.black, 0.4f);             // disco
                default: return Color.Lerp(green, new Color(0.2f, 0.8f, 0.9f), 0.55f); // núcleo
            }
        }

        private Color LossColor(int part) =>
            (part & 1) == 0 ? _theme.statLoss : Color.Lerp(_theme.statLoss, Color.black, 0.3f);

        private void SetPartHighlight(int part, bool on)
        {
            if (part < 0 || part >= PartCount) return;
            Color gain = on ? Color.white : GainColor(part);
            Color loss = on ? Color.Lerp(_theme.statLoss, Color.white, 0.6f) : LossColor(part);
            for (int i = 0; i < StatCount; i++)
            {
                _statBars[i].Gain[part].color = gain;
                _statBars[i].Loss[part].color = loss;
            }
        }

        /// <summary>Coloca un tramo entre dos posiciones 0-1 de la barra (vacío = oculto).</summary>
        private static void Place(Image segment, float from, float to)
        {
            from = Mathf.Clamp01(from);
            to = Mathf.Clamp01(to);
            segment.enabled = to > from;
            if (!segment.enabled) return;

            RectTransform rt = segment.rectTransform;
            rt.anchorMin = new Vector2(from, 0f);
            rt.anchorMax = new Vector2(to, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static float Snap(float value) => Mathf.Round(value * StatSteps) / StatSteps;
        #endregion
    }
}
