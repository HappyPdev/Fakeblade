using FakeBlade.Core;
using TMPro;
using UnityEngine;

namespace FakeBlade.UI
{
    /// <summary>
    /// Menú principal (GDD 9.2): Jugar, Opciones, Controles, Información y Salir.
    /// Todo se construye por código con el tema del catálogo. Navegable con teclado, mando y ratón.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private FakeBladeCatalog catalog;

        private HUDTheme _theme;
        private int _px;
        private TextMeshProUGUI _title;
        private GameObject _titleRoot;
        private PixelMenuList _main;
        private PixelMenuList _quitConfirm;
        private OptionsScreen _options;
        private ControlsScreen _controls;
        private CreditsScreen _credits;

        private void Awake()
        {
            MatchSetup.Clear();
            MenuStack.Reset();
            Time.timeScale = 1f;
            if (catalog != null) CombatConfig.SetActive(catalog.combatConfig);
        }

        private void Start()
        {
            _theme = catalog != null && catalog.uiTheme != null ? catalog.uiTheme : HUDTheme.CreateDefault();
            _px = Mathf.Max(1, _theme.pixelSize);

            MatchMenuUI.EnsureEventSystem();
            Canvas canvas = PixelUI.CreateOverlayCanvas("MainMenu_Canvas", transform, _theme, 10);
            Transform root = canvas.transform;

            BuildTitle(root);
            BuildMainList(root);
            BuildQuitConfirm(root);
            BuildFooter(root);

            _options = OptionsScreen.Create(root, _theme, ShowMain);
            _controls = ControlsScreen.Create(root, _theme, ShowMain);
            _credits = CreditsScreen.Create(root, _theme, catalog != null ? catalog.credits : null, ShowMain);

            RefreshTexts();
            Loc.OnLanguageChanged += RefreshTexts;
            ShowMain();
        }

        private void BuildTitle(Transform root)
        {
            _title = PixelUI.CreateShadowedTitle("Title", root, _theme, 40 * _px, _theme.textColor);
            RectTransform titleRt = (RectTransform)_title.transform.parent;
            _titleRoot = titleRt.gameObject;
            titleRt.anchorMin = titleRt.anchorMax = titleRt.pivot = new Vector2(0.5f, 0.8f);
            titleRt.sizeDelta = new Vector2(1600f, 60 * _px);
            _title.text = catalog != null && catalog.credits != null ? catalog.credits.gameTitle : "FAKEBLADE";
            _title.color = _theme.chargeReady;

            var subtitle = PixelUI.CreateText("Subtitle", titleRt, _theme, 6 * _px, TextAlignmentOptions.Center, _theme.textColor);
            var rt = subtitle.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, 2 * _px);
            rt.sizeDelta = new Vector2(0f, 10 * _px);
            _subtitle = subtitle;
        }

        private TextMeshProUGUI _subtitle;
        private TextMeshProUGUI _hint;

        private void RefreshTexts()
        {
            if (_subtitle != null) _subtitle.text = Loc.Get("SUBTITLE");
            if (_hint != null) _hint.text = Loc.Get("MENU_HINT");
        }

        private void OnDestroy()
        {
            Loc.OnLanguageChanged -= RefreshTexts;
        }

        private void BuildMainList(Transform root)
        {
            _main = PixelMenuList.Create(root, _theme, null, 90, true);
            RectTransform rt = _main.RectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.36f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            _main.AddButton("PLAY", () => SceneFlow.Load(SceneFlow.Lobby));
            _main.AddButton("OPTIONS", () => OpenScreen(_options));
            _main.AddButton("CONTROLS", () => OpenScreen(_controls));
            _main.AddButton("INFO", () => OpenScreen(_credits));
#if !UNITY_WEBGL || UNITY_EDITOR
            _main.AddButton("QUIT", ShowQuitConfirm);
#endif
        }

        private void BuildQuitConfirm(Transform root)
        {
            _quitConfirm = PixelMenuList.Create(root, _theme, "CONFIRM_QUIT", 110, true);
            _quitConfirm.AddButton("YES", Quit);
            _quitConfirm.AddButton("NO", ShowMain);
            _quitConfirm.SetBorderColor(_theme.healthLow);
            _quitConfirm.Hide();
        }

        private void BuildFooter(Transform root)
        {
            var hint = PixelUI.CreateText("Hint", root, _theme, 4 * _px, TextAlignmentOptions.Center, _theme.textDimColor);
            var rt = hint.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 4 * _px);
            rt.sizeDelta = new Vector2(0f, 8 * _px);
            _hint = hint;

            var version = PixelUI.CreateText("Version", root, _theme, 4 * _px, TextAlignmentOptions.BottomRight, _theme.textDimColor);
            var vrt = version.rectTransform;
            vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(1f, 0f);
            vrt.anchoredPosition = new Vector2(-4 * _px, 4 * _px);
            vrt.sizeDelta = new Vector2(60 * _px, 8 * _px);
            version.text = "v" + Application.version;
        }

        private void OpenScreen(MenuScreen screen)
        {
            _main.Hide();
            _quitConfirm.Hide();
            _titleRoot.SetActive(false);
            screen.Open();
        }

        private void ShowMain()
        {
            _quitConfirm.Hide();
            _titleRoot.SetActive(true);
            _main.Show();
        }

        private void ShowQuitConfirm()
        {
            _main.Hide();
            _quitConfirm.Show();
            // Por defecto "No" para evitar salir sin querer
            if (_quitConfirm.Rows.Count > 1) _quitConfirm.SelectRow(_quitConfirm.Rows[1]);
        }

        private void Update()
        {
            if (!MenuInput.AnyBackPressed()) return;

            if (_quitConfirm.gameObject.activeSelf) ShowMain();
#if !UNITY_WEBGL || UNITY_EDITOR
            else if (_main.gameObject.activeSelf) ShowQuitConfirm();
#endif
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
