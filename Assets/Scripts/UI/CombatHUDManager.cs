using UnityEngine;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// Gestor principal del HUD de combate.
    /// 
    /// === JERARQUÍA QUE GENERA EN EL CANVAS ===
    /// 
    /// [CombatHUD_Canvas]                          ← Canvas (ScreenSpace-Overlay)
    ///   ├── PlayerPanel_0  (esquina sup-izq)      ← PlayerHUDPanel
    ///   ├── PlayerPanel_1  (esquina sup-der)       ← PlayerHUDPanel
    ///   ├── PlayerPanel_2  (esquina inf-izq)       ← PlayerHUDPanel
    ///   ├── PlayerPanel_3  (esquina inf-der)       ← PlayerHUDPanel
    ///   └── CenterInfo                             ← Countdown, mensajes centrales
    /// 
    /// === CÓMO USAR ===
    /// 1. Añadir este script a un GameObject vacío en la escena
    /// 2. Llamar a InitializeHUD(playerControllers[]) después de crear los jugadores
    /// 3. El HUD se actualiza automáticamente via eventos del PlayerController
    /// 
    /// === TAGS UTILIZADOS ===
    /// - "CombatHUD"       → Canvas principal
    /// - "PlayerPanel"     → Cada panel individual
    /// - "HealthBar"       → Barra de vida (spin)
    /// - "AbilityCharges"  → Contenedor de cargas
    /// - "PlayerName"      → Texto del nombre
    /// - "EliminatedOverlay" → Overlay de eliminación
    /// </summary>
    public class CombatHUDManager : MonoBehaviour
    {
        #region Singleton
        public static CombatHUDManager Instance { get; private set; }
        #endregion

        #region Serialized Fields
        [Header("=== HUD CONFIG ===")]
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private float panelWidth = 280f;
        [SerializeField] private float panelHeight = 120f;
        [SerializeField] private float panelMargin = 20f;

        [Header("=== HEALTH BAR ===")]
        [Tooltip("Número de segmentos en la barra de vida (ej: 10 = cada segmento es 10%)")]
        [SerializeField] private int healthSegments = 10;
        [SerializeField] private float segmentSpacing = 2f;
        [SerializeField] private Color healthHighColor = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color healthMidColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color healthLowColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private float healthLowThreshold = 0.3f;
        [SerializeField] private float healthMidThreshold = 0.6f;

        [Header("=== ABILITY CHARGES ===")]
        [Tooltip("Número máximo de cargas de habilidad especial")]
        [SerializeField] private int maxAbilityCharges = 3;
        [SerializeField] private float chargeBarWidth = 12f;
        [SerializeField] private float chargeBarHeight = 40f;
        [SerializeField] private float chargeBarSpacing = 6f;
        [SerializeField] private Color chargeReadyColor = new Color(0.3f, 0.7f, 1f);
        [SerializeField] private Color chargeChargingColor = new Color(0.15f, 0.35f, 0.5f);
        [SerializeField] private Color chargeEmptyColor = new Color(0.1f, 0.1f, 0.15f);

        [Header("=== DASH COOLDOWN ===")]
        [SerializeField] private Color dashReadyColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color dashCooldownColor = new Color(0.3f, 0.25f, 0.1f);

        [Header("=== STYLING ===")]
        [SerializeField] private Color panelBackgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.85f);
        [SerializeField] private Color panelBorderColor = new Color(0.3f, 0.3f, 0.4f, 0.6f);
        [SerializeField] private int fontSize = 14;
        [SerializeField] private int playerNameFontSize = 16;

        [Header("=== DEBUG ===")]
        [SerializeField] private bool debugMode = false;
        #endregion

        #region Private Fields
        private Canvas _canvas;
        private CanvasScaler _canvasScaler;
        private PlayerHUDPanel[] _panels;
        private RectTransform _centerInfoRect;
        private Text _centerText;
        private bool _isInitialized;
        #endregion

        #region Properties
        public Canvas HUDCanvas => _canvas;
        public bool IsInitialized => _isInitialized;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CleanupPanels();
        }
        #endregion

        // =====================================================================
        // PUBLIC API
        // =====================================================================

        #region Public Methods

        /// <summary>
        /// Inicializa el HUD para N jugadores.
        /// Llamar DESPUÉS de crear los PlayerControllers.
        /// </summary>
        public void InitializeHUD(FakeBlade.Core.PlayerController[] players)
        {
            if (players == null || players.Length == 0)
            {
                Debug.LogError("[CombatHUDManager] No players provided!");
                return;
            }

            CleanupPanels();
            CreateCanvas();
            CreatePlayerPanels(players);
            CreateCenterInfo();
            BindPlayerEvents(players);

            _isInitialized = true;

            if (debugMode)
                Debug.Log($"[CombatHUDManager] HUD initialized for {players.Length} players");
        }

        /// <summary>
        /// Actualiza el número de cargas de habilidad de un jugador.
        /// Llamar desde SpecialAbilityHandler.
        /// </summary>
        public void UpdateAbilityCharges(int playerID, int currentCharges, int maxCharges, float currentChargeProgress)
        {
            if (!ValidatePlayerID(playerID)) return;
            _panels[playerID].UpdateAbilityCharges(currentCharges, maxCharges, currentChargeProgress);
        }

        /// <summary>
        /// Actualiza el cooldown del dash.
        /// </summary>
        public void UpdateDashCooldown(int playerID, float progress)
        {
            if (!ValidatePlayerID(playerID)) return;
            _panels[playerID].UpdateDashCooldown(progress);
        }

        /// <summary>
        /// Muestra texto central (countdown, mensajes de victoria, etc.)
        /// </summary>
        public void ShowCenterMessage(string message, float duration = 2f)
        {
            if (_centerText == null) return;
            _centerText.text = message;
            _centerText.gameObject.SetActive(true);
            if (duration > 0f)
                Invoke(nameof(HideCenterMessage), duration);
        }

        public void HideCenterMessage()
        {
            if (_centerText != null)
                _centerText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Marca un jugador como eliminado en el HUD.
        /// </summary>
        public void SetPlayerEliminated(int playerID, bool eliminated)
        {
            if (!ValidatePlayerID(playerID)) return;
            _panels[playerID].SetEliminated(eliminated);
        }

        /// <summary>
        /// Resetea todos los paneles al estado inicial.
        /// </summary>
        public void ResetAllPanels()
        {
            if (_panels == null) return;
            foreach (var panel in _panels)
            {
                if (panel != null) panel.ResetPanel();
            }
            HideCenterMessage();
        }

        /// <summary>
        /// Obtiene el panel de un jugador específico para acceso directo.
        /// </summary>
        public PlayerHUDPanel GetPlayerPanel(int playerID)
        {
            if (!ValidatePlayerID(playerID)) return null;
            return _panels[playerID];
        }

        #endregion

        // =====================================================================
        // CANVAS CREATION
        // =====================================================================

        #region Canvas Setup

        private void CreateCanvas()
        {
            // Crear Canvas
            GameObject canvasObj = new GameObject("[CombatHUD_Canvas]");
            canvasObj.tag = "Untagged"; // Se puede cambiar a tag custom "CombatHUD"
            canvasObj.transform.SetParent(transform);

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            _canvasScaler = canvasObj.AddComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = new Vector2(1920, 1080);
            _canvasScaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            if (debugMode)
                Debug.Log("[CombatHUDManager] Canvas created");
        }

        #endregion

        // =====================================================================
        // PLAYER PANELS
        // =====================================================================

        #region Panel Creation

        private void CreatePlayerPanels(FakeBlade.Core.PlayerController[] players)
        {
            int playerCount = Mathf.Min(players.Length, maxPlayers);
            _panels = new PlayerHUDPanel[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                _panels[i] = CreateSinglePanel(i, players[i]);
            }
        }

        private PlayerHUDPanel CreateSinglePanel(int index, FakeBlade.Core.PlayerController player)
        {
            // === RAÍZ DEL PANEL ===
            GameObject panelObj = new GameObject($"PlayerPanel_{index}");
            panelObj.transform.SetParent(_canvas.transform, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            SetPanelAnchor(panelRect, index);
            panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

            // Componente principal
            PlayerHUDPanel panel = panelObj.AddComponent<PlayerHUDPanel>();

            // === FONDO DEL PANEL ===
            Image panelBg = CreateChildImage(panelObj, "Background", panelBackgroundColor);
            RectTransform bgRect = panelBg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // === BORDE DEL PANEL ===
            Image borderImage = CreateChildImage(panelObj, "Border", Color.clear);
            RectTransform borderRect = borderImage.rectTransform;
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            Outline borderOutline = borderImage.gameObject.AddComponent<Outline>();
            borderOutline.effectColor = panelBorderColor;
            borderOutline.effectDistance = new Vector2(2, 2);

            // === BARRA DE COLOR DEL JUGADOR (acento superior) ===
            Image accentBar = CreateChildImage(panelObj, "PlayerAccent", player.PlayerColor);
            RectTransform accentRect = accentBar.rectTransform;
            bool isTop = (index < 2);
            if (isTop)
            {
                accentRect.anchorMin = new Vector2(0, 0);
                accentRect.anchorMax = new Vector2(1, 0);
                accentRect.pivot = new Vector2(0.5f, 0);
                accentRect.offsetMin = new Vector2(4, 4);
                accentRect.offsetMax = new Vector2(-4, 8);
            }
            else
            {
                accentRect.anchorMin = new Vector2(0, 1);
                accentRect.anchorMax = new Vector2(1, 1);
                accentRect.pivot = new Vector2(0.5f, 1);
                accentRect.offsetMin = new Vector2(4, -8);
                accentRect.offsetMax = new Vector2(-4, -4);
            }

            // === NOMBRE DEL JUGADOR ===
            Text nameText = CreateChildText(panelObj, "PlayerName", player.PlayerName,
                playerNameFontSize, FontStyle.Bold, player.PlayerColor);
            RectTransform nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0, 1);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.pivot = new Vector2(0, 1);
            nameRect.offsetMin = new Vector2(10, -32);
            nameRect.offsetMax = new Vector2(-10, -10);

            // === PLAYER ID BADGE ===
            Text idText = CreateChildText(panelObj, "PlayerID", $"P{index + 1}",
                12, FontStyle.Bold, Color.white);
            RectTransform idRect = idText.rectTransform;
            Image idBg = idText.gameObject.AddComponent<Image>();
            idBg.color = new Color(player.PlayerColor.r, player.PlayerColor.g, player.PlayerColor.b, 0.3f);
            // Posición: justo a la izquierda del nombre
            idRect.anchorMin = new Vector2(0, 1);
            idRect.anchorMax = new Vector2(0, 1);
            idRect.pivot = new Vector2(0, 1);
            idRect.anchoredPosition = new Vector2(10, -10);
            idRect.sizeDelta = new Vector2(30, 20);
            idText.alignment = TextAnchor.MiddleCenter;
            // Ajustar nombre para no solapar
            nameRect.offsetMin = new Vector2(45, -32);

            // === BARRA DE VIDA (SPIN) POR SEGMENTOS ===
            GameObject healthContainer = CreateHealthBar(panelObj, index);

            // === CARGAS DE HABILIDAD ESPECIAL ===
            GameObject chargesContainer = CreateAbilityCharges(panelObj, index);

            // === INDICADOR DE DASH ===
            GameObject dashIndicator = CreateDashIndicator(panelObj, index);

            // === OVERLAY DE ELIMINACIÓN ===
            GameObject eliminatedOverlay = CreateEliminatedOverlay(panelObj);

            // === TEXTO DE STATS (velocidad, peso - debug/opcional) ===
            Text statsText = CreateChildText(panelObj, "StatsText", "",
                10, FontStyle.Normal, new Color(0.7f, 0.7f, 0.7f));
            RectTransform statsRect = statsText.rectTransform;
            statsRect.anchorMin = new Vector2(0, 0);
            statsRect.anchorMax = new Vector2(1, 0);
            statsRect.pivot = new Vector2(0, 0);
            statsRect.offsetMin = new Vector2(10, 12);
            statsRect.offsetMax = new Vector2(-10, 28);

            // === INICIALIZAR EL PANEL ===
            panel.Initialize(
                playerID: index,
                playerColor: player.PlayerColor,
                background: panelBg,
                accentBar: accentBar,
                nameText: nameText,
                idText: idText,
                healthContainer: healthContainer,
                chargesContainer: chargesContainer,
                dashIndicator: dashIndicator,
                eliminatedOverlay: eliminatedOverlay,
                statsText: statsText,
                config: new PlayerHUDPanel.PanelConfig
                {
                    healthSegments = this.healthSegments,
                    maxAbilityCharges = this.maxAbilityCharges,
                    healthHighColor = this.healthHighColor,
                    healthMidColor = this.healthMidColor,
                    healthLowColor = this.healthLowColor,
                    healthLowThreshold = this.healthLowThreshold,
                    healthMidThreshold = this.healthMidThreshold,
                    chargeReadyColor = this.chargeReadyColor,
                    chargeChargingColor = this.chargeChargingColor,
                    chargeEmptyColor = this.chargeEmptyColor,
                    dashReadyColor = this.dashReadyColor,
                    dashCooldownColor = this.dashCooldownColor
                }
            );

            if (debugMode)
                Debug.Log($"[CombatHUDManager] Panel {index} created for {player.PlayerName}");

            return panel;
        }

        /// <summary>
        /// Posiciona cada panel en una esquina de la pantalla.
        /// P1=sup-izq, P2=sup-der, P3=inf-izq, P4=inf-der
        /// </summary>
        private void SetPanelAnchor(RectTransform rect, int index)
        {
            float m = panelMargin;

            switch (index)
            {
                case 0: // Superior Izquierda
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.pivot = new Vector2(0, 1);
                    rect.anchoredPosition = new Vector2(m, -m);
                    break;

                case 1: // Superior Derecha
                    rect.anchorMin = new Vector2(1, 1);
                    rect.anchorMax = new Vector2(1, 1);
                    rect.pivot = new Vector2(1, 1);
                    rect.anchoredPosition = new Vector2(-m, -m);
                    break;

                case 2: // Inferior Izquierda
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(0, 0);
                    rect.pivot = new Vector2(0, 0);
                    rect.anchoredPosition = new Vector2(m, m);
                    break;

                case 3: // Inferior Derecha
                    rect.anchorMin = new Vector2(1, 0);
                    rect.anchorMax = new Vector2(1, 0);
                    rect.pivot = new Vector2(1, 0);
                    rect.anchoredPosition = new Vector2(-m, m);
                    break;
            }
        }

        #endregion

        // =====================================================================
        // HEALTH BAR (SEGMENTED)
        // =====================================================================

        #region Health Bar

        private GameObject CreateHealthBar(GameObject parent, int playerIndex)
        {
            // Contenedor de la barra de vida
            GameObject container = new GameObject("HealthBar_Container");
            container.transform.SetParent(parent.transform, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0.35f);
            containerRect.anchorMax = new Vector2(0.75f, 0.55f);
            containerRect.offsetMin = new Vector2(10, 0);
            containerRect.offsetMax = new Vector2(-5, 0);

            // Label "SPIN"
            Text spinLabel = CreateChildText(container, "SpinLabel", "SPIN",
                9, FontStyle.Bold, new Color(0.5f, 0.5f, 0.6f));
            RectTransform labelRect = spinLabel.rectTransform;
            labelRect.anchorMin = new Vector2(0, 1);
            labelRect.anchorMax = new Vector2(0.3f, 1.8f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            // Fondo de la barra completa
            Image barBg = CreateChildImage(container, "HealthBar_BG",
                new Color(0.08f, 0.08f, 0.12f));
            RectTransform barBgRect = barBg.rectTransform;
            barBgRect.anchorMin = Vector2.zero;
            barBgRect.anchorMax = Vector2.one;
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;

            // Crear segmentos individuales
            HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = segmentSpacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(2, 2, 2, 2);

            for (int s = 0; s < healthSegments; s++)
            {
                GameObject segment = new GameObject($"Segment_{s}");
                segment.transform.SetParent(container.transform, false);

                RectTransform segRect = segment.AddComponent<RectTransform>();
                Image segImage = segment.AddComponent<Image>();
                segImage.color = healthHighColor;

                // LayoutElement para control fino si se necesita
                LayoutElement segLayout = segment.AddComponent<LayoutElement>();
                segLayout.flexibleWidth = 1f;
            }

            // Texto de porcentaje
            Text percentText = CreateChildText(container, "PercentText", "100%",
                11, FontStyle.Bold, Color.white);
            RectTransform percentRect = percentText.rectTransform;
            LayoutElement percentLayout = percentText.gameObject.AddComponent<LayoutElement>();
            percentLayout.ignoreLayout = true;
            percentRect.anchorMin = new Vector2(0, 0);
            percentRect.anchorMax = new Vector2(1, 1);
            percentRect.offsetMin = Vector2.zero;
            percentRect.offsetMax = Vector2.zero;
            percentText.alignment = TextAnchor.MiddleCenter;

            return container;
        }

        #endregion

        // =====================================================================
        // ABILITY CHARGES
        // =====================================================================

        #region Ability Charges

        private GameObject CreateAbilityCharges(GameObject parent, int playerIndex)
        {
            // Contenedor de cargas
            GameObject container = new GameObject("AbilityCharges_Container");
            container.transform.SetParent(parent.transform, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.78f, 0.15f);
            containerRect.anchorMax = new Vector2(1f, 0.85f);
            containerRect.offsetMin = new Vector2(0, 0);
            containerRect.offsetMax = new Vector2(-10, 0);

            // Label "SPEC"
            Text specLabel = CreateChildText(container, "SpecLabel", "SPEC",
                8, FontStyle.Bold, new Color(0.4f, 0.55f, 0.7f));
            RectTransform specLabelRect = specLabel.rectTransform;
            specLabelRect.anchorMin = new Vector2(0, -0.1f);
            specLabelRect.anchorMax = new Vector2(1, 0.1f);
            specLabelRect.offsetMin = Vector2.zero;
            specLabelRect.offsetMax = Vector2.zero;
            specLabel.alignment = TextAnchor.MiddleCenter;

            // Layout horizontal para las barras verticales
            HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = chargeBarSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(4, 4, 4, 14);

            for (int c = 0; c < maxAbilityCharges; c++)
            {
                CreateSingleChargeBar(container, c);
            }

            return container;
        }

        /// <summary>
        /// Cada carga es una barra vertical con fondo + fill que se llena de abajo a arriba.
        /// </summary>
        private void CreateSingleChargeBar(GameObject parent, int chargeIndex)
        {
            // Contenedor de una carga individual
            GameObject chargeObj = new GameObject($"Charge_{chargeIndex}");
            chargeObj.transform.SetParent(parent.transform, false);

            RectTransform chargeRect = chargeObj.AddComponent<RectTransform>();

            // Fondo de la carga
            Image chargeBg = chargeObj.AddComponent<Image>();
            chargeBg.color = chargeEmptyColor;

            // Fill (se llena de abajo a arriba)
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(chargeObj.transform, false);

            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.1f, 0);
            fillRect.anchorMax = new Vector2(0.9f, 1);
            fillRect.offsetMin = new Vector2(0, 1);
            fillRect.offsetMax = new Vector2(0, -1);

            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = chargeReadyColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Vertical;
            fillImage.fillOrigin = 0; // Bottom
            fillImage.fillAmount = 1f;

            // Brillo/glow cuando está lista
            GameObject glowObj = new GameObject("Glow");
            glowObj.transform.SetParent(chargeObj.transform, false);

            RectTransform glowRect = glowObj.AddComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-2, -2);
            glowRect.offsetMax = new Vector2(2, 2);

            Image glowImage = glowObj.AddComponent<Image>();
            glowImage.color = new Color(chargeReadyColor.r, chargeReadyColor.g, chargeReadyColor.b, 0.3f);
            glowImage.raycastTarget = false;
            glowObj.SetActive(false); // Se activa cuando la carga está llena
        }

        #endregion

        // =====================================================================
        // DASH INDICATOR
        // =====================================================================

        #region Dash Indicator

        private GameObject CreateDashIndicator(GameObject parent, int playerIndex)
        {
            GameObject container = new GameObject("DashIndicator");
            container.transform.SetParent(parent.transform, false);

            RectTransform containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0.1f);
            containerRect.anchorMax = new Vector2(0.75f, 0.3f);
            containerRect.offsetMin = new Vector2(10, 0);
            containerRect.offsetMax = new Vector2(-5, 0);

            // Label "DASH"
            Text dashLabel = CreateChildText(container, "DashLabel", "DASH",
                9, FontStyle.Bold, new Color(0.6f, 0.55f, 0.3f));
            RectTransform dashLabelRect = dashLabel.rectTransform;
            LayoutElement dashLabelLayout = dashLabel.gameObject.AddComponent<LayoutElement>();
            dashLabelLayout.ignoreLayout = true;
            dashLabelRect.anchorMin = new Vector2(0, 0);
            dashLabelRect.anchorMax = new Vector2(0.2f, 1);
            dashLabelRect.offsetMin = Vector2.zero;
            dashLabelRect.offsetMax = Vector2.zero;

            // Fondo de la barra de dash
            Image dashBg = CreateChildImage(container, "DashBar_BG",
                new Color(0.08f, 0.08f, 0.12f));
            RectTransform dashBgRect = dashBg.rectTransform;
            dashBgRect.anchorMin = new Vector2(0.22f, 0.15f);
            dashBgRect.anchorMax = new Vector2(1, 0.85f);
            dashBgRect.offsetMin = Vector2.zero;
            dashBgRect.offsetMax = Vector2.zero;

            // Fill del dash
            GameObject dashFillObj = new GameObject("DashFill");
            dashFillObj.transform.SetParent(container.transform, false);

            RectTransform dashFillRect = dashFillObj.AddComponent<RectTransform>();
            dashFillRect.anchorMin = new Vector2(0.22f, 0.15f);
            dashFillRect.anchorMax = new Vector2(1, 0.85f);
            dashFillRect.offsetMin = new Vector2(1, 1);
            dashFillRect.offsetMax = new Vector2(-1, -1);

            Image dashFillImage = dashFillObj.AddComponent<Image>();
            dashFillImage.color = dashReadyColor;
            dashFillImage.type = Image.Type.Filled;
            dashFillImage.fillMethod = Image.FillMethod.Horizontal;
            dashFillImage.fillAmount = 1f;

            // Texto "READY" / cooldown
            Text dashStatusText = CreateChildText(container, "DashStatus", "READY",
                9, FontStyle.Bold, Color.white);
            RectTransform dashStatusRect = dashStatusText.rectTransform;
            LayoutElement dashStatusLayout = dashStatusText.gameObject.AddComponent<LayoutElement>();
            dashStatusLayout.ignoreLayout = true;
            dashStatusRect.anchorMin = new Vector2(0.22f, 0);
            dashStatusRect.anchorMax = new Vector2(1, 1);
            dashStatusRect.offsetMin = Vector2.zero;
            dashStatusRect.offsetMax = Vector2.zero;
            dashStatusText.alignment = TextAnchor.MiddleCenter;

            return container;
        }

        #endregion

        // =====================================================================
        // ELIMINATED OVERLAY
        // =====================================================================

        #region Eliminated Overlay

        private GameObject CreateEliminatedOverlay(GameObject parent)
        {
            GameObject overlay = new GameObject("EliminatedOverlay");
            overlay.transform.SetParent(parent.transform, false);

            RectTransform overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = new Color(0, 0, 0, 0.7f);
            overlayImage.raycastTarget = false;

            // Texto "ELIMINATED"
            Text elimText = CreateChildText(overlay, "EliminatedText", "ELIMINATED",
                18, FontStyle.Bold, new Color(1f, 0.3f, 0.3f));
            RectTransform elimRect = elimText.rectTransform;
            elimRect.anchorMin = Vector2.zero;
            elimRect.anchorMax = Vector2.one;
            elimRect.offsetMin = Vector2.zero;
            elimRect.offsetMax = Vector2.zero;
            elimText.alignment = TextAnchor.MiddleCenter;

            overlay.SetActive(false);
            return overlay;
        }

        #endregion

        // =====================================================================
        // CENTER INFO
        // =====================================================================

        #region Center Info

        private void CreateCenterInfo()
        {
            GameObject centerObj = new GameObject("CenterInfo");
            centerObj.transform.SetParent(_canvas.transform, false);

            _centerInfoRect = centerObj.AddComponent<RectTransform>();
            _centerInfoRect.anchorMin = new Vector2(0.3f, 0.4f);
            _centerInfoRect.anchorMax = new Vector2(0.7f, 0.6f);
            _centerInfoRect.offsetMin = Vector2.zero;
            _centerInfoRect.offsetMax = Vector2.zero;

            Image centerBg = centerObj.AddComponent<Image>();
            centerBg.color = new Color(0, 0, 0, 0.6f);

            _centerText = CreateChildText(centerObj, "CenterText", "",
                32, FontStyle.Bold, Color.white);
            RectTransform textRect = _centerText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            _centerText.alignment = TextAnchor.MiddleCenter;

            centerObj.SetActive(false);
        }

        #endregion

        // =====================================================================
        // EVENT BINDING
        // =====================================================================

        #region Events

        private void BindPlayerEvents(FakeBlade.Core.PlayerController[] players)
        {
            for (int i = 0; i < _panels.Length; i++)
            {
                int capturedIndex = i; // Captura para closure
                FakeBlade.Core.PlayerController player = players[i];

                // Spin changed → actualizar barra de vida
                player.OnSpinChanged += (percentage) =>
                {
                    if (_panels != null && capturedIndex < _panels.Length && _panels[capturedIndex] != null)
                        _panels[capturedIndex].UpdateHealth(percentage);
                };

                // Player defeated → overlay de eliminación
                player.OnPlayerDefeated += (id) =>
                {
                    SetPlayerEliminated(capturedIndex, true);
                };
            }
        }

        private void CleanupPanels()
        {
            if (_panels != null)
            {
                foreach (var panel in _panels)
                {
                    if (panel != null && panel.gameObject != null)
                        Destroy(panel.gameObject);
                }
                _panels = null;
            }

            if (_canvas != null)
                Destroy(_canvas.gameObject);

            _isInitialized = false;
        }

        #endregion

        // =====================================================================
        // UI UTILITY HELPERS
        // =====================================================================

        #region Helpers

        private bool ValidatePlayerID(int playerID)
        {
            if (_panels == null || playerID < 0 || playerID >= _panels.Length)
            {
                if (debugMode)
                    Debug.LogWarning($"[CombatHUDManager] Invalid playerID: {playerID}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Crea un hijo con Image. Perfecto para fondos, barras, etc.
        /// </summary>
        private Image CreateChildImage(GameObject parent, string name, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            Image image = obj.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return image;
        }

        /// <summary>
        /// Crea un hijo con Text. Usa fuente por defecto (sustituible por TMP después).
        /// Para migrar a TextMeshPro, buscar todos los CreateChildText y cambiar.
        /// </summary>
        private Text CreateChildText(GameObject parent, string name, string content,
            int size, FontStyle style, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);

            RectTransform rect = obj.AddComponent<RectTransform>();

            Text text = obj.AddComponent<Text>();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            return text;
        }

        #endregion
    }
}
