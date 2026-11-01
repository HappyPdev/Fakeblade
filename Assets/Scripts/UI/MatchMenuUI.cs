using System;
using System.Collections.Generic;
using FakeBlade.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// Menús de la escena de batalla (GDD 9.2), en estilo pixel-art y navegables con
    /// flechas/stick: Pausa (reanudar, reiniciar, idioma, salir con confirmación)
    /// y Resultados (ganadores, revancha, menú principal).
    /// En Aprende a jugar (GDD 6.7, P8) la pausa es otra: reanudar, repetir paso, saltar paso, cambiar peonza
    /// (ChangeBladeScreen), opciones, controles y salir del tutorial. En el final, «Repetir paso» repite todo el
    /// tutorial (con confirmación) y «Saltar paso» queda en gris.
    /// </summary>
    public class MatchMenuUI : MonoBehaviour
    {
        #region Layout (píxeles de UI)
        private const int WINDOW_W = 120;
        private const int BUTTON_W = 80;
        private const int BUTTON_H = 11;
        private const int BUTTON_GAP = 3;
        private const int TITLE_H = 14;
        private const int ROW_H = 8;
        private const int PADDING = 6;
        /// <summary>Pausa del tutorial: más ancha para «SALIR DEL TUTORIAL» (texto que no se pisa, GDD 9.2).</summary>
        private const int TUTORIAL_W = 150;
        private const int TUTORIAL_BUTTON_W = 120;
        /// <summary>Confirmaciones del tutorial: «¿REPETIR TODO EL TUTORIAL?» en una línea.</summary>
        private const int TUTORIAL_CONFIRM_W = 200;
        #endregion

        private HUDTheme _theme;
        private int _px;

        private GameObject _dim;
        private Window _pause;
        private Window _confirm;
        private Window _results;
        private readonly List<Window> _windows = new List<Window>(5);
        private readonly List<ResultRow> _rows = new List<ResultRow>(4);

        // Aprende a jugar (se crean la primera vez que se pausa)
        private Window _tutorialPause;
        private Window _tutorialConfirm;
        private Button _repeatButton;
        private Button _skipButton;
        private TextMeshProUGUI _repeatLabel;
        private TextMeshProUGUI _skipLabel;
        private string _tutorialConfirmKey;
        private Action _tutorialConfirmAction;
        private ChangeBladeScreen _changeBlade;

        private readonly List<KeyValuePair<TextMeshProUGUI, string>> _localized =
            new List<KeyValuePair<TextMeshProUGUI, string>>(16);

        private MatchResult _shownResult;
        private MatchRules _shownRules;
        private IReadOnlyList<PlayerController> _shownPlayers;

        private sealed class Window
        {
            public RectTransform Root;
            public TextMeshProUGUI Title;
            public int ButtonCount;
            public int Width;
            public int ButtonWidth;
            public readonly List<Button> Buttons = new List<Button>(4);
        }

        private sealed class ResultRow
        {
            public GameObject Root;
            public Image Winner;
            public Image Color;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Stat;
        }

        #region Creation
        public static MatchMenuUI Create(Transform parent, HUDTheme theme)
        {
            RectTransform root = PixelUI.CreateRect("Menus", parent);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;
            root.gameObject.AddComponent<GraphicRaycaster>();

            var menus = root.gameObject.AddComponent<MatchMenuUI>();
            menus.Build(theme);
            return menus;
        }

        /// <summary>
        /// Crea un EventSystem con el módulo del Input System si no existe ninguno, y lo prepara para que el mando y el
        /// teclado no pierdan nunca el foco (N1, GDD 9.2): un clic en el fondo no quita la selección.
        /// </summary>
        public static void EnsureEventSystem()
        {
            EventSystem events = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
            if (events == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                events = go.GetComponent<EventSystem>();
            }

            if (events.TryGetComponent(out InputSystemUIInputModule module)) module.deselectOnBackgroundClick = false;
            if (!events.TryGetComponent(out MenuFocusGuard _)) events.gameObject.AddComponent<MenuFocusGuard>();
        }

        private void Build(HUDTheme theme)
        {
            _theme = theme;
            _px = Mathf.Max(1, theme.pixelSize);

            var dim = PixelUI.CreateImage("Dim", transform, theme.overlayColor);
            dim.raycastTarget = true;
            PixelUI.Stretch(dim.rectTransform, 0, _px);
            _dim = dim.gameObject;

            _pause = CreateWindow("PauseWindow", "PAUSED", 5);
            AddButton(_pause, "RESUME", () => GameManager.Instance?.ResumeGame());
            AddButton(_pause, "RESTART", () => GameManager.Instance?.RestartMatch());
            AddButton(_pause, "OPTIONS", () => OpenSubmenu(ref _options, () => OptionsScreen.Create(transform, _theme, ShowPause)));
            AddButton(_pause, "CONTROLS", () => OpenSubmenu(ref _controls, () => ControlsScreen.Create(transform, _theme, ShowPause)));
            AddButton(_pause, "MAIN_MENU", ShowConfirm);
            LinkNavigation(_pause);

            _confirm = CreateWindow("ConfirmWindow", "CONFIRM_EXIT", 2);
            AddButton(_confirm, "YES", () => GameManager.Instance?.ReturnToMenu());
            AddButton(_confirm, "NO", ShowPause);
            LinkNavigation(_confirm);

            _results = CreateWindow("ResultsWindow", "DRAW", 3, 4);
            AddButton(_results, "REMATCH", () => GameManager.Instance?.RestartMatch());
            AddButton(_results, "CHANGE_BLADES", () => GameManager.Instance?.ReturnToLobby());
            AddButton(_results, "MAIN_MENU", () => GameManager.Instance?.ReturnToMenu());
            LinkNavigation(_results);

            Loc.OnLanguageChanged += RefreshLanguage;
            HideAll();
        }

        private void OnDestroy()
        {
            Loc.OnLanguageChanged -= RefreshLanguage;
        }

        private MenuScreen _options;
        private MenuScreen _controls;

        /// <summary>Abre Opciones/Controles sobre la pausa (se crean la primera vez).</summary>
        private void OpenSubmenu(ref MenuScreen screen, Func<MenuScreen> factory)
        {
            if (screen == null) screen = factory();
            HideWindows();
            _dim.SetActive(true);
            screen.Open();
        }

        private Window CreateWindow(string name, string titleKey, int buttonCount, int resultRows = 0,
            int width = WINDOW_W, int buttonWidth = BUTTON_W)
        {
            int height = PADDING + TITLE_H + resultRows * ROW_H + (resultRows > 0 ? PADDING : 0)
                         + buttonCount * (BUTTON_H + BUTTON_GAP) - BUTTON_GAP + PADDING;

            RectTransform root = PixelUI.CreateRect(name, transform);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(width * _px, height * _px);

            var shadow = PixelUI.CreateImage("Shadow", root, _theme.panelShadow);
            PixelUI.Place(shadow.rectTransform, 2, 2, width, height, _px);
            var outline = PixelUI.CreateImage("Outline", root, _theme.sphereOutline);
            PixelUI.Place(outline.rectTransform, 0, 0, width, height, _px);
            var border = PixelUI.CreateImage("Border", root, _theme.chargeReady);
            PixelUI.Place(border.rectTransform, 1, 1, width - 2, height - 2, _px);
            var bg = PixelUI.CreateImage("Background", root, _theme.panelBackground);
            PixelUI.Place(bg.rectTransform, 2, 2, width - 4, height - 4, _px);

            var window = new Window { Root = root, ButtonCount = buttonCount, Width = width, ButtonWidth = buttonWidth };
            window.Title = PixelUI.CreateText("Title", root, _theme, 8 * _px, TextAlignmentOptions.Center, _theme.textColor);
            PixelUI.Place(window.Title.rectTransform, PADDING, PADDING, width - PADDING * 2, TITLE_H - 3, _px);
            Localize(window.Title, titleKey);

            for (int i = 0; i < resultRows; i++)
                _rows.Add(CreateResultRow(root, PADDING + TITLE_H + i * ROW_H));

            _windows.Add(window);
            return window;
        }

        private ResultRow CreateResultRow(Transform parent, int y)
        {
            RectTransform rowRoot = PixelUI.CreateRect("Row", parent);
            PixelUI.Place(rowRoot, PADDING, y, WINDOW_W - PADDING * 2, ROW_H - 1, _px);

            var row = new ResultRow { Root = rowRoot.gameObject };
            row.Winner = PixelUI.CreateImage("Winner", rowRoot, _theme.dashReady, PixelUI.TopIcon);
            PixelUI.Place(row.Winner.rectTransform, 2, 1, 5, 5, _px);
            row.Color = PixelUI.CreateImage("Color", rowRoot, Color.white);
            PixelUI.Place(row.Color.rectTransform, 9, 1, 5, 5, _px);
            row.Name = PixelUI.CreateText("Name", rowRoot, _theme, 5 * _px, TextAlignmentOptions.MidlineLeft, _theme.textColor);
            PixelUI.Place(row.Name.rectTransform, 17, 0, 60, ROW_H - 1, _px);
            row.Stat = PixelUI.CreateText("Stat", rowRoot, _theme, 5 * _px, TextAlignmentOptions.MidlineRight, _theme.textColor);
            PixelUI.Place(row.Stat.rectTransform, 77, 0, 29, ROW_H - 1, _px);
            return row;
        }

        private Button AddButton(Window window, string labelKey, Action onClick)
        {
            int index = window.Buttons.Count;
            int height = Mathf.RoundToInt(window.Root.sizeDelta.y / _px);
            int y = height - PADDING - (window.ButtonCount - index) * (BUTTON_H + BUTTON_GAP) + BUTTON_GAP;
            int x = (window.Width - window.ButtonWidth) / 2;

            var outline = PixelUI.CreateImage($"Button_{labelKey}", window.Root, _theme.sphereOutline);
            PixelUI.Place(outline.rectTransform, x, y, window.ButtonWidth, BUTTON_H, _px);

            var face = PixelUI.CreateImage("Face", outline.transform, Color.white);
            face.raycastTarget = true;
            PixelUI.Stretch(face.rectTransform, 1, _px);

            var label = PixelUI.CreateText("Label", face.transform, _theme, 5 * _px, TextAlignmentOptions.Center, _theme.textColor);
            PixelUI.Stretch(label.rectTransform, 0, _px);
            Localize(label, labelKey);

            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = _theme.barBackground;
            colors.highlightedColor = Color.Lerp(_theme.barBackground, _theme.chargeReady, 0.6f);
            colors.selectedColor = _theme.chargeReady * 0.8f;
            colors.pressedColor = Color.white;
            colors.disabledColor = _theme.chargeEmpty;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0f; // cambio instantáneo, estética pixel
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            window.Buttons.Add(button);
            return button;
        }

        /// <summary>Arriba/abajo en bucle, saltando los botones en gris (GDD 9.2, regla 2). Repetirla si cambian.</summary>
        private static void LinkNavigation(Window window)
        {
            int count = window.Buttons.Count;
            for (int i = 0; i < count; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = NextInteractable(window, i, -1),
                    selectOnDown = NextInteractable(window, i, 1)
                };
                window.Buttons[i].navigation = nav;
            }
        }

        private static Button NextInteractable(Window window, int from, int dir)
        {
            int count = window.Buttons.Count;
            for (int step = 1; step < count; step++)
            {
                Button candidate = window.Buttons[((from + dir * step) % count + count) % count];
                if (candidate.interactable) return candidate;
            }
            return window.Buttons[from];
        }
        #endregion

        #region Aprende a jugar
        private void BuildTutorialWindows()
        {
            if (_tutorialPause != null) return;

            _tutorialPause = CreateWindow("TutorialPauseWindow", "PAUSED", 7, 0, TUTORIAL_W, TUTORIAL_BUTTON_W);
            AddButton(_tutorialPause, "RESUME", () => GameManager.Instance?.ResumeGame());
            _repeatButton = AddButton(_tutorialPause, "TUT_REPEAT_STEP", RepeatStep);
            _repeatLabel = _repeatButton.GetComponentInChildren<TextMeshProUGUI>();
            _skipButton = AddButton(_tutorialPause, "TUT_SKIP_STEP", SkipStep);
            _skipLabel = _skipButton.GetComponentInChildren<TextMeshProUGUI>();
            AddButton(_tutorialPause, "CHANGE_BLADE", OpenChangeBlade);
            AddButton(_tutorialPause, "OPTIONS", () => OpenSubmenu(ref _options, () => OptionsScreen.Create(transform, _theme, ShowPause)));
            AddButton(_tutorialPause, "CONTROLS", () => OpenSubmenu(ref _controls, () => ControlsScreen.Create(transform, _theme, ShowPause)));
            AddButton(_tutorialPause, "TUT_QUIT", () => ShowTutorialConfirm("TUT_CONFIRM_QUIT", 6, () => GameManager.Instance?.ReturnToMenu()));

            _tutorialConfirm = CreateWindow("TutorialConfirmWindow", "TUT_CONFIRM_QUIT", 2, 0, TUTORIAL_CONFIRM_W);
            AddButton(_tutorialConfirm, "YES", () => _tutorialConfirmAction?.Invoke());
            AddButton(_tutorialConfirm, "NO", () =>
            {
                if (TutorialController.Current != null) ShowTutorialPause(TutorialController.Current, _confirmReturnIndex);
                else ShowPause();
            });
            LinkNavigation(_tutorialConfirm);
        }

        /// <summary>En el final, «Repetir paso» pasa a «Repetir tutorial» y «Saltar paso» queda en gris.</summary>
        private void RefreshTutorialPause(TutorialController tutorial)
        {
            bool finished = tutorial.IsFinished;
            _repeatLabel.text = Loc.Get(finished ? "TUT_REPEAT_ALL" : "TUT_REPEAT_STEP");
            _skipButton.interactable = !finished;
            _skipLabel.color = finished ? _theme.textDimColor : _theme.textColor;
            LinkNavigation(_tutorialPause);
        }

        private void ShowTutorialPause(TutorialController tutorial, int selectedIndex)
        {
            BuildTutorialWindows();
            RefreshTutorialPause(tutorial);
            Show(_tutorialPause, selectedIndex);
        }

        private void RepeatStep()
        {
            TutorialController tutorial = TutorialController.Current;
            if (tutorial == null) return;
            if (tutorial.IsFinished)
            {
                ShowTutorialConfirm("TUT_CONFIRM_RESTART", 1, () =>
                {
                    TutorialController.Current?.RestartTutorial();
                    GameManager.Instance?.ResumeGame();
                });
                return;
            }
            tutorial.RepeatStep();
            GameManager.Instance?.ResumeGame();
        }

        private void SkipStep()
        {
            TutorialController tutorial = TutorialController.Current;
            if (tutorial == null || tutorial.IsFinished) return;
            tutorial.SkipStep();
            GameManager.Instance?.ResumeGame();
        }

        private int _confirmReturnIndex;

        /// <summary>Confirmación del tutorial; «No» vuelve a la pausa con el foco en el botón que la abrió.</summary>
        private void ShowTutorialConfirm(string titleKey, int returnIndex, Action onYes)
        {
            _tutorialConfirmKey = titleKey;
            _tutorialConfirmAction = onYes;
            _confirmReturnIndex = returnIndex;
            _tutorialConfirm.Title.text = Loc.Get(titleKey);
            // «No» primero: confirmar sin querer no saca a nadie del tutorial
            Show(_tutorialConfirm, 1);
        }

        private void OpenChangeBlade()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            if (_changeBlade == null)
            {
                var bootstrap = FindFirstObjectByType<BattleBootstrap>();
                if (bootstrap == null || bootstrap.Catalog == null) return;
                _changeBlade = ChangeBladeScreen.Create(transform, _theme, bootstrap.Catalog,
                    () => { if (gm.IsPaused) ShowTutorialPause(TutorialController.Current, 3); },
                    () => TutorialController.Current?.HandleBladesChanged());
            }
            HideWindows();
            _dim.SetActive(true);
            _changeBlade.Open(gm.Players);
            if (!_changeBlade.IsOpen) ShowPause();
        }
        #endregion

        #region Show / Hide
        public void HideAll()
        {
            if (_options != null) _options.Dismiss();
            if (_controls != null) _controls.Dismiss();
            if (_changeBlade != null) _changeBlade.Dismiss();
            _dim.SetActive(false);
            HideWindows();
            _shownResult = null;
        }

        private void HideWindows()
        {
            for (int i = 0; i < _windows.Count; i++) _windows[i].Root.gameObject.SetActive(false);
        }

        /// <summary>La pausa normal o, en Aprende a jugar, la suya.</summary>
        public void ShowPause()
        {
            TutorialController tutorial = TutorialController.Current;
            if (tutorial != null) ShowTutorialPause(tutorial, 0);
            else Show(_pause, 0);
        }

        public void ShowConfirm() => Show(_confirm, 1);

        public void ShowResults(MatchResult result, MatchRules rules, IReadOnlyList<PlayerController> players)
        {
            _shownResult = result;
            _shownRules = rules;
            _shownPlayers = players;
            FillResults();
            Show(_results, 0);
        }

        private void Show(Window window, int selectedIndex)
        {
            _dim.SetActive(true);
            for (int i = 0; i < _windows.Count; i++) _windows[i].Root.gameObject.SetActive(_windows[i] == window);

            if (EventSystem.current != null && window.Buttons.Count > 0)
            {
                // Nunca en un botón en gris: el siguiente que se pueda pulsar
                int index = Mathf.Clamp(selectedIndex, 0, window.Buttons.Count - 1);
                Button selected = window.Buttons[index].interactable ? window.Buttons[index] : NextInteractable(window, index, 1);
                EventSystem.current.SetSelectedGameObject(selected.gameObject);
            }
        }

        private void FillResults()
        {
            MatchResult result = _shownResult;
            if (result == null) return;

            // Título
            if (result.IsDraw)
            {
                _results.Title.text = Loc.Get("DRAW");
                _results.Title.color = _theme.textColor;
            }
            else if (result.WinningTeam >= 0)
            {
                _results.Title.text = Loc.Format("WINS", Loc.Format("TEAM_NAME", result.WinningTeam + 1));
                _results.Title.color = result.Winners.Count > 0 ? result.Winners[0].PlayerColor : _theme.textColor;
            }
            else if (result.Winners.Count > 0)
            {
                PlayerController winner = result.Winners[0];
                _results.Title.text = Loc.Format("WINS", DisplayName(winner));
                _results.Title.color = winner.PlayerColor;
            }

            // Clasificación
            for (int i = 0; i < _rows.Count; i++)
            {
                ResultRow row = _rows[i];
                PlayerController player = _shownPlayers != null && i < _shownPlayers.Count ? _shownPlayers[i] : null;
                row.Root.SetActive(player != null);
                if (player == null) continue;

                row.Winner.enabled = result.Winners.Contains(player);
                row.Color.color = player.PlayerColor;
                row.Name.text = DisplayName(player);
                row.Stat.text = StatText(player);
            }
        }

        private string StatText(PlayerController player)
        {
            if (_shownRules == null) return string.Empty;
            if (_shownRules.ShowsScore) return PixelUI.Number(player.Score) + " " + Loc.Get("POINTS_SHORT");
            if (player.IsEliminated) return Loc.Get("KO");
            if (_shownRules.ShowsLives) return PixelUI.Times(player.Lives);
            return PixelUI.Percent(Mathf.CeilToInt(player.SpinPercentage * 100f));
        }

        private static string DisplayName(PlayerController player)
        {
            string name = player.PlayerName;
            bool isDefault = string.IsNullOrEmpty(name) || name.StartsWith("Player ");
            return isDefault ? Loc.Format("PLAYER_NAME", player.PlayerID + 1) : name;
        }
        #endregion

        #region Localization
        private void Localize(TextMeshProUGUI text, string key)
        {
            text.text = Loc.Get(key);
            _localized.Add(new KeyValuePair<TextMeshProUGUI, string>(text, key));
        }

        private void RefreshLanguage()
        {
            for (int i = 0; i < _localized.Count; i++)
                _localized[i].Key.text = Loc.Get(_localized[i].Value);

            if (_shownResult != null) FillResults();
            // Textos que cambian con el paso del tutorial
            if (_tutorialConfirm != null && _tutorialConfirmKey != null) _tutorialConfirm.Title.text = Loc.Get(_tutorialConfirmKey);
            if (_tutorialPause != null && TutorialController.Current != null) RefreshTutorialPause(TutorialController.Current);
        }
        #endregion
    }
}
