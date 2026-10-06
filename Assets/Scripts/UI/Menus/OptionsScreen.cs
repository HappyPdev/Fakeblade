using System;
using System.Collections.Generic;
using FakeBlade.Core;
using UnityEngine;

namespace FakeBlade.UI
{
    /// <summary>
    /// Opciones (GDD 9.2.3): gráficos, audio y juego. Cada cambio se aplica y se guarda al momento.
    /// </summary>
    public class OptionsScreen : MenuScreen
    {
        private const int RowHeight = 9;

        private List<Vector2Int> _resolutions;
        private PixelOptionRow _resolution, _fullscreen, _quality, _shadows, _antialiasing, _bloom,
            _particles, _vsync, _fps, _master, _music, _sfx, _language, _vibration, _shake;

        private FakeBladeCatalog _catalog;
        private BladeColorsScreen _bladeColors;

        /// <param name="catalog">Con catálogo aparece "Colores de peonza" (necesita la paleta y el prefab).</param>
        public static OptionsScreen Create(Transform parent, HUDTheme theme, Action onClose, FakeBladeCatalog catalog = null)
        {
            var screen = CreateScreen<OptionsScreen>("OptionsScreen", parent);
            screen._catalog = catalog;
            screen.Initialize(theme, "OPTIONS", 150, onClose);
            return screen;
        }

        protected override void BuildContent()
        {
            GameSettings s = SettingsService.Current;
            _resolutions = SettingsService.GetResolutions();

            _resolution = List.AddSelector("OPT_RESOLUTION", ResolutionLabels(), ResolutionIndex(), i =>
            {
                s.resolutionWidth = _resolutions[i].x;
                s.resolutionHeight = _resolutions[i].y;
                SettingsService.Apply();
            }, RowHeight);

            _fullscreen = List.AddSelector("OPT_FULLSCREEN", YesNo(), s.fullscreen ? 1 : 0, i => Set(() => s.fullscreen = i == 1), RowHeight);

#if UNITY_WEBGL && !UNITY_EDITOR
            // En navegador la resolución y la pantalla completa las controla la página
            _resolution.gameObject.SetActive(false);
            _fullscreen.gameObject.SetActive(false);
#endif

            _quality = List.AddSelector("OPT_QUALITY", QualityLabels(), s.qualityLevel, i => Set(() => s.qualityLevel = i), RowHeight);
            _shadows = List.AddSelector("OPT_SHADOWS", ShadowLabels(), s.shadows, i => Set(() => s.shadows = i), RowHeight);
            _antialiasing = List.AddSelector("OPT_AA", AALabels(), s.antialiasing, i => Set(() => s.antialiasing = i), RowHeight);
            _bloom = List.AddSelector("OPT_BLOOM", YesNo(), s.bloom ? 1 : 0, i => Set(() => s.bloom = i == 1), RowHeight);
            _particles = List.AddSelector("OPT_PARTICLES", LevelLabels(), s.particles, i => Set(() => s.particles = i), RowHeight);
            _vsync = List.AddSelector("OPT_VSYNC", YesNo(), s.vsync ? 1 : 0, i => Set(() => s.vsync = i == 1), RowHeight);
            _fps = List.AddSelector("OPT_FPS", YesNo(), s.showFps ? 1 : 0, i => Set(() => s.showFps = i == 1), RowHeight);

            _master = List.AddSelector("OPT_MASTER", VolumeLabels(), VolumeIndex(s.masterVolume), i => Set(() => s.masterVolume = i / 10f), RowHeight);
            _music = List.AddSelector("OPT_MUSIC", VolumeLabels(), VolumeIndex(s.musicVolume), i => Set(() => s.musicVolume = i / 10f), RowHeight);
            _sfx = List.AddSelector("OPT_SFX", VolumeLabels(), VolumeIndex(s.sfxVolume), i => Set(() => s.sfxVolume = i / 10f), RowHeight);

            _language = List.AddSelector("OPT_LANGUAGE", LanguageLabels(), (int)Loc.Current, i => Loc.Current = (Language)i, RowHeight);
            _vibration = List.AddSelector("OPT_VIBRATION", YesNo(), s.vibration ? 1 : 0, i => Set(() => s.vibration = i == 1), RowHeight);
            _shake = List.AddSelector("OPT_SHAKE", YesNo(), s.cameraShake ? 1 : 0, i => Set(() => s.cameraShake = i == 1), RowHeight);

            if (_catalog != null) List.AddButton("OPT_BLADE_COLORS", OpenBladeColors, RowHeight);

            List.AddSpacer(2);
            List.AddButton("BACK", Close, RowHeight);
        }

        /// <summary>Submenú Colores de peonza: Opciones se oculta y vuelve al cerrarlo.</summary>
        private void OpenBladeColors()
        {
            if (_bladeColors == null)
                _bladeColors = BladeColorsScreen.Create(transform.parent, Theme, _catalog, Open);
            Dismiss();
            _bladeColors.Open();
        }

        private static void Set(Action change)
        {
            change();
            SettingsService.Apply();
        }

        protected override void OnOpened() => RefreshValues();

        /// <summary>Regenera los textos de las opciones (idioma) manteniendo la selección.</summary>
        protected override void OnLanguageRefreshed() => RefreshValues();

        private void RefreshValues()
        {
            GameSettings s = SettingsService.Current;
            _resolution.SetOptions(ResolutionLabels(), ResolutionIndex());
            _fullscreen.SetOptions(YesNo(), s.fullscreen ? 1 : 0);
            _quality.SetOptions(QualityLabels(), s.qualityLevel);
            _shadows.SetOptions(ShadowLabels(), s.shadows);
            _antialiasing.SetOptions(AALabels(), s.antialiasing);
            _bloom.SetOptions(YesNo(), s.bloom ? 1 : 0);
            _particles.SetOptions(LevelLabels(), s.particles);
            _vsync.SetOptions(YesNo(), s.vsync ? 1 : 0);
            _fps.SetOptions(YesNo(), s.showFps ? 1 : 0);
            _master.SetOptions(VolumeLabels(), VolumeIndex(s.masterVolume));
            _music.SetOptions(VolumeLabels(), VolumeIndex(s.musicVolume));
            _sfx.SetOptions(VolumeLabels(), VolumeIndex(s.sfxVolume));
            _language.SetOptions(LanguageLabels(), (int)Loc.Current);
            _vibration.SetOptions(YesNo(), s.vibration ? 1 : 0);
            _shake.SetOptions(YesNo(), s.cameraShake ? 1 : 0);
        }

        #region Labels
        private string[] ResolutionLabels()
        {
            var labels = new string[_resolutions.Count];
            for (int i = 0; i < labels.Length; i++) labels[i] = $"{_resolutions[i].x}x{_resolutions[i].y}";
            return labels;
        }

        private int ResolutionIndex()
        {
            var s = SettingsService.Current;
            int index = _resolutions.IndexOf(new Vector2Int(s.resolutionWidth, s.resolutionHeight));
            return index < 0 ? _resolutions.Count - 1 : index;
        }

        private static string[] QualityLabels()
        {
            string[] names = QualitySettings.names;
            var labels = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string key = "QUALITY_" + names[i].ToUpperInvariant();
                string localized = Loc.Get(key);
                labels[i] = localized == key ? names[i].ToUpperInvariant() : localized;
            }
            return labels;
        }

        private static string[] ShadowLabels() => new[] { Loc.Get("OFF"), Loc.Get("SHADOWS_HARD"), Loc.Get("SHADOWS_SOFT") };
        private static string[] AALabels() => new[] { Loc.Get("OFF"), "FXAA", "SMAA" };
        private static string[] LevelLabels() => new[] { Loc.Get("LEVEL_LOW"), Loc.Get("LEVEL_MEDIUM"), Loc.Get("LEVEL_HIGH") };
        private static string[] LanguageLabels() => new[] { "ESPAÑOL", "ENGLISH" };

        private static string[] VolumeLabels()
        {
            var labels = new string[11];
            for (int i = 0; i <= 10; i++) labels[i] = PixelUI.Percent(i * 10);
            return labels;
        }

        private static int VolumeIndex(float volume) => Mathf.Clamp(Mathf.RoundToInt(volume * 10f), 0, 10);
        #endregion
    }
}
