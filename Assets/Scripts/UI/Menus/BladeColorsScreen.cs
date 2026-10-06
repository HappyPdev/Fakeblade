using System;
using FakeBlade.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// Opciones → Colores de peonza (GDD 9.2.3): para cada color de la paleta se elige el color de
    /// disco, cuerpo, punta y núcleo de la lista retro del catálogo, con vista previa 3D.
    /// Cada cambio se guarda al momento (BladeColors → GameSettings).
    /// </summary>
    public class BladeColorsScreen : MenuScreen
    {
        private const int RowHeight = 9;
        private const int PreviewPx = 56;
        private const int PreviewStageSlot = 7; // lejos de las 4 de la selección de peonzas

        private static readonly ComponentSlot[] Slots =
            { ComponentSlot.Blade, ComponentSlot.Body, ComponentSlot.Tip, ComponentSlot.Core };
        private static readonly string[] SlotKeys = { "SLOT_BLADE", "SLOT_BODY", "SLOT_TIP", "SLOT_CORE" };

        private FakeBladeCatalog _catalog;
        private int _paletteIndex;
        private PixelOptionRow _paletteRow;
        private PixelOptionRow _resetRow;
        private readonly PixelOptionRow[] _slotRows = new PixelOptionRow[4];
        private readonly Image[] _swatches = new Image[4];
        private RawImage _preview;
        private BladePreviewStage _stage;

        public static BladeColorsScreen Create(Transform parent, HUDTheme theme, FakeBladeCatalog catalog, Action onClose)
        {
            var screen = CreateScreen<BladeColorsScreen>("BladeColorsScreen", parent);
            screen._catalog = catalog;
            screen.Initialize(theme, "BLADE_COLORS", 150, onClose);
            return screen;
        }

        protected override void BuildContent()
        {
            AddPreview();

            _paletteRow = List.AddCustom("BLADE_COLORS_PALETTE", "", StepPalette, null, RowHeight);
            for (int i = 0; i < Slots.Length; i++)
            {
                int slot = i;
                _slotRows[i] = List.AddCustom(SlotKeys[i], "", dir => StepSlot(slot, dir), null, RowHeight);
                _swatches[i] = AddSwatch(_slotRows[i]);
            }

            List.AddSpacer(2);
            _resetRow = List.AddButton("BLADE_COLORS_RESET", ResetCurrent, RowHeight);
            List.AddButton("BACK", Close, RowHeight);
        }

        protected override void OnOpened()
        {
            if (_stage == null && _catalog != null)
            {
                _stage = BladePreviewStage.Create(PreviewStageSlot, _catalog.playerPrefab, Theme.barBackground);
                _preview.texture = _stage.Texture;
                // Se previsualiza con las piezas de la peonza balanceada
                BladePreset preset = _catalog.presets.Find(p => p.nameKey == "PRESET_BALANCED") ?? _catalog.GetPreset(0);
                if (preset != null) _stage.SetParts(preset.tip, preset.body, preset.blade, preset.core);
            }
            if (_stage != null) _stage.gameObject.SetActive(true);
            Refresh();
        }

        protected override void OnLanguageRefreshed() => Refresh();

        private void OnDisable()
        {
            // Cerrada: su cámara deja de renderizar
            if (_stage != null) _stage.gameObject.SetActive(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_stage != null) Destroy(_stage.gameObject);
        }

        #region Actions
        private void StepPalette(int dir)
        {
            int count = PaletteCount;
            if (count == 0) return;
            _paletteIndex = (_paletteIndex + dir + count) % count;
            Refresh();
        }

        /// <summary>Siguiente color de la lista para una pieza. Un color fuera de la lista pasa al más parecido.</summary>
        private void StepSlot(int slotIndex, int dir)
        {
            Color[] colors = _catalog != null ? _catalog.paintColors : null;
            if (colors == null || colors.Length == 0) return;

            BladeColorScheme scheme = BladeColors.Get(_catalog, _paletteIndex);
            ComponentSlot slot = Slots[slotIndex];
            int index = Nearest(colors, scheme.Get(slot), out bool exact);
            if (exact) index = (index + dir + colors.Length) % colors.Length;

            scheme.Set(slot, colors[index]);
            BladeColors.Set(_catalog, _paletteIndex, scheme);
            Refresh();
        }

        private void ResetCurrent()
        {
            BladeColors.Reset(_catalog, _paletteIndex);
            Refresh();
        }
        #endregion

        #region View
        private void Refresh()
        {
            if (_catalog == null) return;

            _paletteRow.SetValueText(Loc.Format("COLOR_N", _paletteIndex + 1));
            _paletteRow.SetValueColor(_catalog.GetColor(_paletteIndex));

            BladeColorScheme scheme = BladeColors.Get(_catalog, _paletteIndex);
            for (int i = 0; i < Slots.Length; i++)
                _swatches[i].color = scheme.Get(Slots[i]);

            _resetRow.SetInteractable(BladeColors.IsCustom(_catalog, _paletteIndex));
            if (_stage != null) _stage.SetScheme(scheme);
        }

        private int PaletteCount => _catalog != null && _catalog.palette != null ? _catalog.palette.Length : 0;

        private void AddPreview()
        {
            int px = Mathf.Max(1, Theme.pixelSize);
            RectTransform box = PixelUI.CreateRect("PreviewBox", List.transform);
            var layout = box.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = layout.minHeight = (PreviewPx + 2) * px;

            var frame = PixelUI.CreateImage("Frame", box, Theme.sphereOutline);
            var frameRt = frame.rectTransform;
            frameRt.anchorMin = frameRt.anchorMax = frameRt.pivot = new Vector2(0.5f, 0.5f);
            frameRt.sizeDelta = new Vector2(PreviewPx * px, PreviewPx * px);

            RectTransform image = PixelUI.CreateRect("Preview", frame.transform);
            PixelUI.Stretch(image, 1, px);
            _preview = image.gameObject.AddComponent<RawImage>();
            _preview.raycastTarget = false;
        }

        /// <summary>Muestra del color en la zona del valor (entre las flechas), con contorno.</summary>
        private Image AddSwatch(PixelOptionRow row)
        {
            int px = Mathf.Max(1, Theme.pixelSize);
            var outline = PixelUI.CreateImage("SwatchOutline", row.transform, Theme.textColor);
            outline.raycastTarget = false;
            var rt = outline.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.75f, 0.5f);
            rt.sizeDelta = new Vector2(24 * px, 5 * px);

            var fill = PixelUI.CreateImage("Swatch", outline.transform, Color.white);
            fill.raycastTarget = false;
            PixelUI.Stretch(fill.rectTransform, 1, px);
            return fill;
        }

        private static int Nearest(Color[] colors, Color target, out bool exact)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < colors.Length; i++)
            {
                Color c = colors[i];
                float d = (c.r - target.r) * (c.r - target.r) + (c.g - target.g) * (c.g - target.g) + (c.b - target.b) * (c.b - target.b);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            exact = bestDistance < 0.0001f;
            return best;
        }
        #endregion
    }
}
