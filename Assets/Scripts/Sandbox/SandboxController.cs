using System.Collections;
using System.Collections.Generic;
using FakeBlade.UI;
using TMPro;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Escena Sandbox (GDD 6.3, Quehaceres B): campo de pruebas con dummies y un panel que se abre
    /// con su propio botón (Tab / Select, reasignable) y pausa el juego.
    ///
    /// - Rivales: de 0 a 3 dummies, hasta completar el máximo de peonzas (4). Cambiar el número
    ///   reinicia la partida al cerrar el panel (la cámara y el HUD se rehacen).
    /// - Comportamiento e intervalo de los dummies: se aplican al momento (SandboxSettings).
    /// - El panel lo navega cualquiera (EventSystem, como la pausa). Al cerrarlo no hay cuenta atrás.
    ///
    /// Va en el mismo objeto que BattleBootstrap, que crea la arena, los jugadores y la cámara.
    /// </summary>
    [RequireComponent(typeof(BattleBootstrap))]
    public class SandboxController : MonoBehaviour
    {
        private const int PanelWidthPx = 160;
        private const int CanvasOrder = 520;

        private BattleBootstrap _bootstrap;
        private readonly List<PlayerController> _dummies = new List<PlayerController>(3);

        private HUDTheme _theme;
        private int _px;
        private GameObject _dim;
        private PixelMenuList _panel;
        private PixelOptionRow _rivalsRow;
        private PixelOptionRow _behaviourRow;
        private PixelOptionRow _intervalRow;
        private TextMeshProUGUI _maxHint;
        private TextMeshProUGUI _hint;

        private bool _open;
        private int _pendingDummies;

        public bool IsPanelOpen => _open;

        private int MaxDummies
        {
            get
            {
                var gm = GameManager.Instance;
                int max = gm != null ? gm.MaxPlayers : 4;
                return Mathf.Max(0, max - MatchSetup.Players.Count);
            }
        }

        #region Lifecycle
        private void Awake()
        {
            _bootstrap = GetComponent<BattleBootstrap>();
        }

        private void Start()
        {
            // BattleBootstrap (orden -200) ya ha creado la arena, los jugadores y la cámara
            SandboxSettings.DummyCount = Mathf.Clamp(SandboxSettings.DummyCount, 0, MaxDummies);
            SetDummyCount(SandboxSettings.DummyCount);
            BuildUI();

            InputBindings.OnChanged += RefreshHint;
            Loc.OnLanguageChanged += RefreshHint;
        }

        private void OnDestroy()
        {
            InputBindings.OnChanged -= RefreshHint;
            Loc.OnLanguageChanged -= RefreshHint;
            if (_open) MenuStack.Pop();
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || _panel == null) return;

            // GameManager (orden -100) ya ha mirado la pausa este frame: Esc/Start no se pisan
            if (_open)
            {
                if (MenuInput.AnySandboxPanelPressed() || MenuInput.AnyBackPressed())
                    ClosePanel();
            }
            else if (gm.CurrentState == GameManager.GameState.InMatch && !MenuStack.IsSubmenuOpen && MenuInput.AnySandboxPanelPressed())
            {
                OpenPanel();
            }

            bool showHint = !_open && (gm.CurrentState == GameManager.GameState.InMatch || gm.CurrentState == GameManager.GameState.Countdown);
            if (_hint.enabled != showHint) _hint.enabled = showHint;
        }
        #endregion

        #region Panel
        private void OpenPanel()
        {
            _open = true;
            MenuStack.Push();
            GameManager.Instance.SetPanelPause(true);

            _pendingDummies = _dummies.Count;
            RefreshOptions();
            _dim.SetActive(true);
            _panel.Show();
        }

        private void ClosePanel()
        {
            if (!_open) return;
            _open = false;
            _panel.Hide();
            _dim.SetActive(false);
            MenuStack.Pop();

            var gm = GameManager.Instance;
            if (_pendingDummies != _dummies.Count)
            {
                SetDummyCount(_pendingDummies);
                StartCoroutine(RestartWhenRegistered(gm));
            }
            else
            {
                gm.SetPanelPause(false);
            }
        }

        /// <summary>Los dummies nuevos se registran en su Start: se espera a que estén para reiniciar.</summary>
        private static IEnumerator RestartWhenRegistered(GameManager gm)
        {
            yield return null;
            yield return null;
            if (gm != null) gm.RestartMatch();
        }

        private void RefreshOptions()
        {
            int max = MaxDummies;
            var rivals = new string[max + 1];
            rivals[0] = Loc.Get("SANDBOX_NONE");
            for (int i = 1; i <= max; i++) rivals[i] = PixelUI.Number(i);
            _rivalsRow.SetOptions(rivals, Mathf.Clamp(_pendingDummies, 0, max));
            _rivalsRow.SetInteractable(max > 0);
            _maxHint.gameObject.SetActive(max == 0);

            _behaviourRow.SetOptions(new[]
            {
                Loc.Get("DUMMY_IDLE"), Loc.Get("DUMMY_MOVE"), Loc.Get("DUMMY_ATTACK"),
                Loc.Get("DUMMY_DASH"), Loc.Get("DUMMY_SPECIAL")
            }, (int)SandboxSettings.Behaviour);

            // Decimales con coma en español y con punto en inglés
            char separator = Loc.Current == Language.English ? '.' : ',';
            var intervals = new string[SandboxSettings.Intervals.Length];
            for (int i = 0; i < intervals.Length; i++)
            {
                string number = SandboxSettings.Intervals[i].ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                intervals[i] = number.Replace('.', separator) + " S";
            }
            _intervalRow.SetOptions(intervals, SandboxSettings.IntervalIndex);
        }
        #endregion

        #region Dummies
        private void SetDummyCount(int count)
        {
            count = Mathf.Clamp(count, 0, MaxDummies);
            SandboxSettings.DummyCount = count;

            while (_dummies.Count > count)
            {
                int last = _dummies.Count - 1;
                _bootstrap.Despawn(_dummies[last]);
                _dummies.RemoveAt(last);
            }

            while (_dummies.Count < count)
            {
                int playerIndex = MatchSetup.Players.Count + _dummies.Count;
                _dummies.Add(_bootstrap.SpawnDummy(playerIndex, _dummies.Count + 1));
            }

            _bootstrap.RefreshCameraTargets();
        }
        #endregion

        #region UI
        private void BuildUI()
        {
            _theme = _bootstrap.Catalog.uiTheme;
            _px = Mathf.Max(1, _theme.pixelSize);
            MatchMenuUI.EnsureEventSystem();

            Canvas canvas = PixelUI.CreateOverlayCanvas("[SandboxUI]", transform, _theme, CanvasOrder);

            var dim = PixelUI.CreateImage("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.45f));
            PixelUI.Stretch(dim.rectTransform, 0, _px);
            _dim = dim.gameObject;
            _dim.SetActive(false);

            _panel = PixelMenuList.Create(canvas.transform, _theme, "SANDBOX_TITLE", PanelWidthPx, true);
            _rivalsRow = _panel.AddSelector("SANDBOX_RIVALS", new[] { "-" }, 0, i => _pendingDummies = i);
            _maxHint = _panel.AddText("SANDBOX_MAX_HINT", 5, _theme.textColor, TextAlignmentOptions.Center, 7);
            _behaviourRow = _panel.AddSelector("SANDBOX_BEHAVIOUR", new[] { "-" }, 0,
                i => SandboxSettings.Behaviour = (DummyBehaviour)i);
            _intervalRow = _panel.AddSelector("SANDBOX_INTERVAL", new[] { "-" }, 0,
                i => SandboxSettings.IntervalIndex = i);
            _panel.AddSpacer(2);
            _panel.AddButton("BACK", ClosePanel);
            _panel.OnLanguageRefreshed += RefreshOptions;
            _panel.Hide();

            // Recordatorio del botón del panel, abajo en el centro
            _hint = PixelUI.CreateText("SandboxHint", canvas.transform, _theme, 5 * _px, TextAlignmentOptions.Center, _theme.textColor);
            RectTransform rt = _hint.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 4 * _px);
            rt.sizeDelta = new Vector2(160 * _px, 8 * _px);
            RefreshHint();
        }

        private void RefreshHint()
        {
            if (_hint == null) return;
            // Nombre corto ("TAB"): el de la distribución del teclado puede ser largo y con tildes
            string key = InputBindings.Left.sandboxPanel.ToString().ToUpperInvariant();
            string button = InputBindings.ButtonName(InputBindings.Pad.sandboxPanel);
            _hint.text = Loc.Format("SANDBOX_HINT", key + " / " + button);
        }
        #endregion
    }
}
