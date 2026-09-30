using System.Collections.Generic;
using FakeBlade.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// HUD de combate (GDD 9.1). Se construye por código a partir de un HUDTheme y se
    /// alimenta de los eventos del GameManager (sin búsquedas en escena ni esperas).
    ///
    /// [CombatHUD]              ← este componente
    ///   └── HUD_Canvas         ← Screen Space Overlay, escalado a 1920x1080
    ///       ├── PlayerPanel_0..3  (esquinas; cada uno con su propio canvas)
    ///       ├── Center           (cuenta atrás / avisos)
    ///       ├── Timer            (si hay límite de tiempo)
    ///       └── Menus            (pausa, confirmación, resultados)
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class CombatHUDManager : MonoBehaviour
    {
        public static CombatHUDManager Instance { get; private set; }

        #region Serialized Fields
        [Tooltip("Tema visual. Vacío = tema por defecto")]
        [SerializeField] private HUDTheme theme;
        [Tooltip("Crea un EventSystem con Input System si la escena no tiene (menús con mando/teclado)")]
        [SerializeField] private bool ensureEventSystem = true;
        [SerializeField] private int sortingOrder = 100;
        [SerializeField] private bool debugMode = false;
        #endregion

        #region Private Fields
        private const int MaxPanels = 4;

        private Canvas _canvas;
        private readonly PlayerHUDPanel[] _panels = new PlayerHUDPanel[MaxPanels];
        private readonly Dictionary<PlayerController, PlayerHUDPanel> _panelByPlayer =
            new Dictionary<PlayerController, PlayerHUDPanel>(MaxPanels);

        private RectTransform _centerRoot;
        private TextMeshProUGUI _centerText;
        private TextMeshProUGUI _centerShadow;
        private float _centerTimer;
        private float _centerPopTimer;

        private RectTransform _timerRoot;
        private TextMeshProUGUI _timerText;
        private int _lastTimerSeconds = -1;

        private MatchMenuUI _menus;
        private GameManager _gm;
        private int _px;
        #endregion

        public Canvas HUDCanvas => _canvas;
        public HUDTheme Theme => theme;

        /// <summary>Crea el HUD por código con un tema concreto (antes de su Awake).</summary>
        public static CombatHUDManager Create(HUDTheme hudTheme)
        {
            var go = new GameObject("[CombatHUD]");
            go.SetActive(false);
            var hud = go.AddComponent<CombatHUDManager>();
            hud.theme = hudTheme;
            go.SetActive(true);
            return hud;
        }

        #region Lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (theme == null) theme = HUDTheme.CreateDefault();
            _px = Mathf.Max(1, theme.pixelSize);

            BuildCanvas();
            BuildCenter();
            BuildTimer();

            _menus = MatchMenuUI.Create(_canvas.transform, theme);
            if (ensureEventSystem) MatchMenuUI.EnsureEventSystem();
        }

        private void OnEnable() => CollisionResolver.OnParry += HandleParry;
        private void OnDisable() => CollisionResolver.OnParry -= HandleParry;

        private void HandleParry(FakeBladeController parrier, FakeBladeController attacker, bool doubleParry)
        {
            if (_centerText == null) return;
            ShowCenter(Loc.Get("PARRY"), 0.6f, 14, VfxSystem.ParryColor);
        }

        private void Start()
        {
            Subscribe(GameManager.Instance);

            // Si la partida ya estaba preparada antes de que existiera el HUD
            if (_gm != null && _gm.PlayerCount > 0 && _gm.CurrentState != GameManager.GameState.MainMenu)
                HandleMatchPrepared(_gm.Players);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_centerRoot == null) return; // recompilación en caliente

            float dt = Time.unscaledDeltaTime;
            UpdateCenter(dt);
            UpdateTimer();
        }
        #endregion

        #region Events
        private void Subscribe(GameManager gm)
        {
            if (gm == null || gm == _gm) return;
            Unsubscribe();
            _gm = gm;

            _gm.OnMatchPrepared += HandleMatchPrepared;
            _gm.OnCountdownTick += HandleCountdown;
            _gm.OnPlayerKO += HandlePlayerKO;
            _gm.OnPlayerRespawned += HandlePlayerRespawned;
            _gm.OnPlayerEliminated += HandlePlayerEliminated;
            _gm.OnPauseChanged += HandlePauseChanged;
            _gm.OnMatchEnded += HandleMatchEnded;
        }

        private void Unsubscribe()
        {
            if (_gm == null) return;

            _gm.OnMatchPrepared -= HandleMatchPrepared;
            _gm.OnCountdownTick -= HandleCountdown;
            _gm.OnPlayerKO -= HandlePlayerKO;
            _gm.OnPlayerRespawned -= HandlePlayerRespawned;
            _gm.OnPlayerEliminated -= HandlePlayerEliminated;
            _gm.OnPauseChanged -= HandlePauseChanged;
            _gm.OnMatchEnded -= HandleMatchEnded;
            _gm = null;
        }

        private void HandleMatchPrepared(IReadOnlyList<PlayerController> players)
        {
            _panelByPlayer.Clear();

            for (int slot = 0; slot < MaxPanels; slot++)
            {
                PlayerController player = slot < players.Count ? players[slot] : null;

                if (player == null)
                {
                    if (_panels[slot] != null) _panels[slot].Bind(null);
                    continue;
                }

                if (_panels[slot] == null)
                    _panels[slot] = PlayerHUDPanel.Create(_canvas.transform, theme, slot);

                _panels[slot].Bind(player);
                _panelByPlayer[player] = _panels[slot];
            }

            _menus.HideAll();
            _lastTimerSeconds = -1;
            _timerRoot.gameObject.SetActive(_gm != null && _gm.Rules.HasTimeLimit);

            if (debugMode) Debug.Log($"[CombatHUD] Paneles para {players.Count} jugadores", this);
        }

        private void HandleCountdown(int secondsLeft)
        {
            if (secondsLeft > 0)
                ShowCenter(PixelUI.Number(secondsLeft), 1.1f, 20);
            else
                ShowCenter(Loc.Get("FIGHT"), 0.9f, 12);
        }

        private void HandlePlayerKO(PlayerController victim, PlayerController killer)
        {
            if (_panelByPlayer.TryGetValue(victim, out var panel))
                panel.ShowKO(false);

            ShowCenter(Loc.Get("KO"), 0.8f, 14);
        }

        private void HandlePlayerRespawned(PlayerController player)
        {
            if (_panelByPlayer.TryGetValue(player, out var panel))
                panel.HideKO();
        }

        private void HandlePlayerEliminated(PlayerController player)
        {
            if (_panelByPlayer.TryGetValue(player, out var panel))
                panel.ShowKO(true);
        }

        private void HandlePauseChanged(bool paused)
        {
            if (paused) _menus.ShowPause();
            else _menus.HideAll();
        }

        private void HandleMatchEnded(MatchResult result)
        {
            if (result.EndedByTime) ShowCenter(Loc.Get("TIME_UP"), 1.2f, 14);
            _menus.ShowResults(result, _gm != null ? _gm.Rules : null, _gm != null ? _gm.Players : null);
        }
        #endregion

        #region Canvas
        private void BuildCanvas()
        {
            var canvasObj = new GameObject("HUD_Canvas", typeof(RectTransform));
            canvasObj.layer = PixelUI.UILayer;
            canvasObj.transform.SetParent(transform, false);

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = sortingOrder;
            _canvas.pixelPerfect = true;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = theme.referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        private void BuildCenter()
        {
            _centerRoot = PixelUI.CreateRect("Center", _canvas.transform);
            _centerRoot.anchorMin = _centerRoot.anchorMax = _centerRoot.pivot = new Vector2(0.5f, 0.55f);
            _centerRoot.sizeDelta = new Vector2(1400f, 60f * _px);
            _centerRoot.gameObject.AddComponent<Canvas>();

            // Sombra desplazada 1 píxel de UI (efecto pixel-art sin shaders)
            _centerShadow = PixelUI.CreateText("Shadow", _centerRoot, theme, 20 * _px,
                TextAlignmentOptions.Center, theme.panelShadow);
            PixelUI.Stretch(_centerShadow.rectTransform, 0, _px);
            _centerShadow.rectTransform.anchoredPosition = new Vector2(_px, -_px);

            _centerText = PixelUI.CreateText("Text", _centerRoot, theme, 20 * _px,
                TextAlignmentOptions.Center, theme.textColor);
            PixelUI.Stretch(_centerText.rectTransform, 0, _px);

            _centerRoot.gameObject.SetActive(false);
        }

        private void BuildTimer()
        {
            _timerRoot = PixelUI.CreateRect("Timer", _canvas.transform);
            _timerRoot.anchorMin = _timerRoot.anchorMax = _timerRoot.pivot = new Vector2(0.5f, 1f);
            _timerRoot.anchoredPosition = new Vector2(0f, -theme.screenMarginPixels * _px);
            _timerRoot.sizeDelta = new Vector2(30 * _px, 11 * _px);
            _timerRoot.gameObject.AddComponent<Canvas>();

            var outline = PixelUI.CreateImage("Outline", _timerRoot, theme.sphereOutline);
            PixelUI.Stretch(outline.rectTransform, 0, _px);
            var bg = PixelUI.CreateImage("Background", _timerRoot, theme.panelBackground);
            PixelUI.Stretch(bg.rectTransform, 1, _px);

            _timerText = PixelUI.CreateText("Text", _timerRoot, theme, 7 * _px,
                TextAlignmentOptions.Center, theme.textColor);
            PixelUI.Stretch(_timerText.rectTransform, 1, _px);

            _timerRoot.gameObject.SetActive(false);
        }
        #endregion

        #region Center Message
        /// <summary>Mensaje central grande (cuenta atrás, K.O., tiempo).</summary>
        public void ShowCenter(string message, float duration, int sizeInPixels) =>
            ShowCenter(message, duration, sizeInPixels, theme.textColor);

        public void ShowCenter(string message, float duration, int sizeInPixels, Color color)
        {
            _centerText.color = color;
            _centerText.text = message;
            _centerShadow.text = message;
            _centerText.fontSize = sizeInPixels * _px;
            _centerShadow.fontSize = sizeInPixels * _px;
            _centerRoot.gameObject.SetActive(true);
            _centerTimer = duration;
            _centerPopTimer = 0.12f;
            _centerRoot.localScale = new Vector3(1.3f, 1.3f, 1f);
        }

        public void HideCenter()
        {
            _centerTimer = 0f;
            _centerRoot.gameObject.SetActive(false);
        }

        private void UpdateCenter(float dt)
        {
            if (_centerTimer <= 0f) return;

            if (_centerPopTimer > 0f)
            {
                _centerPopTimer -= dt;
                if (_centerPopTimer <= 0f) _centerRoot.localScale = Vector3.one;
            }

            _centerTimer -= dt;
            if (_centerTimer <= 0f) _centerRoot.gameObject.SetActive(false);
        }
        #endregion

        #region Timer
        private void UpdateTimer()
        {
            if (_gm == null || !_timerRoot.gameObject.activeSelf) return;

            float remaining = _gm.RemainingTime;
            if (remaining < 0f) return;

            int seconds = Mathf.CeilToInt(remaining);
            if (seconds == _lastTimerSeconds) return;

            _lastTimerSeconds = seconds;
            // Solo cambia una vez por segundo: la asignación es despreciable
            _timerText.text = $"{seconds / 60}:{seconds % 60:00}";
            _timerText.color = seconds <= 10 ? theme.healthLow : theme.textColor;
        }
        #endregion

        public PlayerHUDPanel GetPanel(PlayerController player) =>
            player != null && _panelByPlayer.TryGetValue(player, out var panel) ? panel : null;
    }
}
