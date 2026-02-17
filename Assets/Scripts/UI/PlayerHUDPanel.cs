using UnityEngine;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// Panel individual de HUD para un jugador.
    /// 
    /// === ESTRUCTURA INTERNA ===
    /// 
    /// PlayerPanel_N
    ///   ├── Background           (Image - fondo semi-transparente)
    ///   ├── Border               (Image + Outline - borde decorativo)
    ///   ├── PlayerAccent         (Image - franja de color del jugador)
    ///   ├── PlayerID             (Text + Image - badge "P1", "P2"...)
    ///   ├── PlayerName           (Text - nombre del jugador)
    ///   ├── HealthBar_Container  (HorizontalLayoutGroup)
    ///   │   ├── SpinLabel        (Text "SPIN")
    ///   │   ├── HealthBar_BG     (Image - fondo oscuro)
    ///   │   ├── Segment_0..N     (Image - segmentos individuales)
    ///   │   └── PercentText      (Text "100%")
    ///   ├── AbilityCharges_Container (HorizontalLayoutGroup)
    ///   │   ├── SpecLabel        (Text "SPEC")
    ///   │   └── Charge_0..N      (cada uno con BG + Fill + Glow)
    ///   ├── DashIndicator
    ///   │   ├── DashLabel        (Text "DASH")
    ///   │   ├── DashBar_BG       (Image)
    ///   │   ├── DashFill         (Image filled horizontal)
    ///   │   └── DashStatus       (Text "READY")
    ///   ├── StatsText            (Text - info adicional/debug)
    ///   └── EliminatedOverlay    (Image + Text - se activa al morir)
    /// 
    /// === PARA SUSTITUIR PLACEHOLDERS POR SPRITES ===
    /// Cada Image tiene nombre descriptivo. Para cambiar:
    /// 1. Buscar el GameObject por nombre en el panel
    /// 2. Obtener su componente Image
    /// 3. Asignar sprite: image.sprite = tuSprite; image.type = Image.Type.Sliced;
    /// 
    /// Ejemplo desde código externo:
    ///   var panel = CombatHUDManager.Instance.GetPlayerPanel(0);
    ///   panel.SetHealthBarSprite(myCustomSprite);
    ///   panel.SetChargeBarSprite(myChargeSprite);
    ///   panel.SetBackgroundSprite(myPanelBgSprite);
    /// </summary>
    public class PlayerHUDPanel : MonoBehaviour
    {
        #region Config Struct
        [System.Serializable]
        public struct PanelConfig
        {
            public int healthSegments;
            public int maxAbilityCharges;
            public Color healthHighColor;
            public Color healthMidColor;
            public Color healthLowColor;
            public float healthLowThreshold;
            public float healthMidThreshold;
            public Color chargeReadyColor;
            public Color chargeChargingColor;
            public Color chargeEmptyColor;
            public Color dashReadyColor;
            public Color dashCooldownColor;
        }
        #endregion

        #region Private Fields
        private int _playerID;
        private Color _playerColor;
        private PanelConfig _config;
        private bool _isEliminated;
        private float _currentHealth = 1f;

        // === CACHED REFERENCES ===
        // Estas referencias permiten acceso rápido a cada elemento.
        // Todas se nombran con prefijo descriptivo para encontrarlas fácilmente.
        private Image _background;
        private Image _accentBar;
        private Text _nameText;
        private Text _idText;
        private Text _statsText;
        private GameObject _eliminatedOverlay;

        // Health bar
        private GameObject _healthContainer;
        private Image[] _healthSegments;
        private Text _healthPercentText;

        // Ability charges
        private GameObject _chargesContainer;
        private Image[] _chargeFills;
        private Image[] _chargeBackgrounds;
        private GameObject[] _chargeGlows;

        // Dash
        private GameObject _dashIndicator;
        private Image _dashFillImage;
        private Text _dashStatusText;

        // Animation
        private float _displayedHealth = 1f;
        private float _healthLerpSpeed = 5f;
        private float _pulseTimer;
        private bool _isPulsing;
        #endregion

        #region Properties
        public int PlayerID => _playerID;
        public Color PlayerColor => _playerColor;
        public float CurrentHealth => _currentHealth;
        public bool IsEliminated => _isEliminated;
        #endregion

        // =====================================================================
        // INITIALIZATION
        // =====================================================================

        #region Init

        /// <summary>
        /// Inicializa el panel con todas sus referencias.
        /// Llamado por CombatHUDManager al crear el panel.
        /// </summary>
        public void Initialize(
            int playerID,
            Color playerColor,
            Image background,
            Image accentBar,
            Text nameText,
            Text idText,
            GameObject healthContainer,
            GameObject chargesContainer,
            GameObject dashIndicator,
            GameObject eliminatedOverlay,
            Text statsText,
            PanelConfig config)
        {
            _playerID = playerID;
            _playerColor = playerColor;
            _config = config;

            _background = background;
            _accentBar = accentBar;
            _nameText = nameText;
            _idText = idText;
            _statsText = statsText;
            _eliminatedOverlay = eliminatedOverlay;

            _healthContainer = healthContainer;
            _chargesContainer = chargesContainer;
            _dashIndicator = dashIndicator;

            CacheHealthSegments();
            CacheChargeElements();
            CacheDashElements();

            ResetPanel();
        }

        private void CacheHealthSegments()
        {
            if (_healthContainer == null) return;

            // Los segmentos son hijos directos con Image que NO son el BG ni el PercentText
            var images = _healthContainer.GetComponentsInChildren<Image>();
            var segments = new System.Collections.Generic.List<Image>();

            foreach (var img in images)
            {
                if (img.gameObject.name.StartsWith("Segment_"))
                    segments.Add(img);
            }

            _healthSegments = segments.ToArray();

            // Buscar texto de porcentaje
            Transform percentTf = _healthContainer.transform.Find("PercentText");
            if (percentTf != null)
                _healthPercentText = percentTf.GetComponent<Text>();
        }

        private void CacheChargeElements()
        {
            if (_chargesContainer == null) return;

            var fills = new System.Collections.Generic.List<Image>();
            var bgs = new System.Collections.Generic.List<Image>();
            var glows = new System.Collections.Generic.List<GameObject>();

            for (int c = 0; c < _config.maxAbilityCharges; c++)
            {
                Transform chargeTf = _chargesContainer.transform.Find($"Charge_{c}");
                if (chargeTf == null) continue;

                // Background es la Image del propio charge
                Image bg = chargeTf.GetComponent<Image>();
                bgs.Add(bg);

                // Fill es el hijo "Fill"
                Transform fillTf = chargeTf.Find("Fill");
                if (fillTf != null)
                    fills.Add(fillTf.GetComponent<Image>());

                // Glow es el hijo "Glow"
                Transform glowTf = chargeTf.Find("Glow");
                if (glowTf != null)
                    glows.Add(glowTf.gameObject);
            }

            _chargeFills = fills.ToArray();
            _chargeBackgrounds = bgs.ToArray();
            _chargeGlows = glows.ToArray();
        }

        private void CacheDashElements()
        {
            if (_dashIndicator == null) return;

            Transform dashFillTf = _dashIndicator.transform.Find("DashFill");
            if (dashFillTf != null)
                _dashFillImage = dashFillTf.GetComponent<Image>();

            Transform dashStatusTf = _dashIndicator.transform.Find("DashStatus");
            if (dashStatusTf != null)
                _dashStatusText = dashStatusTf.GetComponent<Text>();
        }

        #endregion

        // =====================================================================
        // UPDATE LOOP
        // =====================================================================

        #region Unity Update

        private void Update()
        {
            if (_isEliminated) return;

            // Lerp suave de la barra de vida
            if (Mathf.Abs(_displayedHealth - _currentHealth) > 0.001f)
            {
                _displayedHealth = Mathf.Lerp(_displayedHealth, _currentHealth, _healthLerpSpeed * Time.deltaTime);
                ApplyHealthVisuals(_displayedHealth);
            }

            // Pulso cuando la vida está baja
            if (_isPulsing)
            {
                _pulseTimer += Time.deltaTime * 3f;
                float pulse = 0.7f + Mathf.Sin(_pulseTimer) * 0.3f;
                if (_accentBar != null)
                {
                    Color c = _config.healthLowColor;
                    c.a = pulse;
                    _accentBar.color = c;
                }
            }
        }

        #endregion

        // =====================================================================
        // PUBLIC UPDATE METHODS
        // =====================================================================

        #region Public Updates

        /// <summary>
        /// Actualiza la barra de vida (spin percentage 0-1).
        /// Llamado automáticamente via evento OnSpinChanged.
        /// </summary>
        public void UpdateHealth(float percentage)
        {
            _currentHealth = Mathf.Clamp01(percentage);

            // Activar pulso si vida baja
            _isPulsing = _currentHealth <= _config.healthLowThreshold && _currentHealth > 0;
            if (!_isPulsing && _accentBar != null)
                _accentBar.color = _playerColor;
        }

        /// <summary>
        /// Actualiza las cargas de habilidad especial.
        /// currentCharges: cargas completas disponibles
        /// maxCharges: máximo de cargas
        /// currentChargeProgress: progreso de la carga actual (0-1), 
        ///   se aplica a la primera carga no completada
        /// </summary>
        public void UpdateAbilityCharges(int currentCharges, int maxCharges, float currentChargeProgress)
        {
            if (_chargeFills == null) return;

            for (int c = 0; c < _chargeFills.Length; c++)
            {
                if (c < currentCharges)
                {
                    // Carga completa
                    _chargeFills[c].fillAmount = 1f;
                    _chargeFills[c].color = _config.chargeReadyColor;
                    if (c < _chargeGlows.Length)
                        _chargeGlows[c].SetActive(true);
                }
                else if (c == currentCharges && currentChargeProgress > 0)
                {
                    // Carga en progreso (se llena de abajo a arriba)
                    _chargeFills[c].fillAmount = currentChargeProgress;
                    _chargeFills[c].color = _config.chargeChargingColor;
                    if (c < _chargeGlows.Length)
                        _chargeGlows[c].SetActive(false);
                }
                else
                {
                    // Carga vacía
                    _chargeFills[c].fillAmount = 0f;
                    _chargeFills[c].color = _config.chargeEmptyColor;
                    if (c < _chargeGlows.Length)
                        _chargeGlows[c].SetActive(false);
                }
            }
        }

        /// <summary>
        /// Actualiza el indicador de dash cooldown (0 = en cooldown, 1 = listo).
        /// </summary>
        public void UpdateDashCooldown(float progress)
        {
            if (_dashFillImage != null)
            {
                _dashFillImage.fillAmount = progress;
                _dashFillImage.color = progress >= 1f ? _config.dashReadyColor : _config.dashCooldownColor;
            }

            if (_dashStatusText != null)
            {
                _dashStatusText.text = progress >= 1f ? "READY" : $"{progress * 100:F0}%";
            }
        }

        /// <summary>
        /// Muestra/oculta el overlay de eliminación.
        /// </summary>
        public void SetEliminated(bool eliminated)
        {
            _isEliminated = eliminated;

            if (_eliminatedOverlay != null)
                _eliminatedOverlay.SetActive(eliminated);

            // Desaturar el panel
            if (eliminated && _background != null)
            {
                Color bg = _background.color;
                _background.color = new Color(bg.r * 0.5f, bg.g * 0.5f, bg.b * 0.5f, bg.a);
            }
        }

        /// <summary>
        /// Actualiza texto de stats adicionales (velocidad, peso, etc.)
        /// </summary>
        public void UpdateStats(float speed, float weight)
        {
            if (_statsText != null)
                _statsText.text = $"SPD:{speed:F1}  W:{weight:F1}";
        }

        /// <summary>
        /// Resetea el panel a su estado inicial (100% vida, cargas llenas, no eliminado).
        /// </summary>
        public void ResetPanel()
        {
            _currentHealth = 1f;
            _displayedHealth = 1f;
            _isEliminated = false;
            _isPulsing = false;

            ApplyHealthVisuals(1f);
            UpdateDashCooldown(1f);
            UpdateAbilityCharges(_config.maxAbilityCharges, _config.maxAbilityCharges, 0f);

            if (_eliminatedOverlay != null)
                _eliminatedOverlay.SetActive(false);

            if (_accentBar != null)
                _accentBar.color = _playerColor;

            if (_background != null)
            {
                // Restaurar color original
                Color bgColor = new Color(0.05f, 0.05f, 0.1f, 0.85f);
                _background.color = bgColor;
            }
        }

        #endregion

        // =====================================================================
        // SPRITE REPLACEMENT API
        // =====================================================================

        #region Sprite Replacement

        /// <summary>
        /// Sustituye el fondo del panel por un sprite personalizado.
        /// Útil para temas/skins del HUD.
        /// </summary>
        public void SetBackgroundSprite(Sprite sprite)
        {
            if (_background != null && sprite != null)
            {
                _background.sprite = sprite;
                _background.type = Image.Type.Sliced;
            }
        }

        /// <summary>
        /// Sustituye los sprites de los segmentos de la barra de vida.
        /// </summary>
        public void SetHealthBarSprite(Sprite sprite)
        {
            if (_healthSegments == null || sprite == null) return;
            foreach (var seg in _healthSegments)
            {
                if (seg != null)
                {
                    seg.sprite = sprite;
                    seg.type = Image.Type.Sliced;
                }
            }
        }

        /// <summary>
        /// Sustituye los sprites de las barras de carga de habilidad.
        /// </summary>
        public void SetChargeBarSprite(Sprite fillSprite, Sprite bgSprite = null)
        {
            if (_chargeFills != null && fillSprite != null)
            {
                foreach (var fill in _chargeFills)
                {
                    if (fill != null)
                    {
                        fill.sprite = fillSprite;
                        fill.type = Image.Type.Filled;
                    }
                }
            }

            if (_chargeBackgrounds != null && bgSprite != null)
            {
                foreach (var bg in _chargeBackgrounds)
                {
                    if (bg != null)
                    {
                        bg.sprite = bgSprite;
                        bg.type = Image.Type.Sliced;
                    }
                }
            }
        }

        /// <summary>
        /// Sustituye el sprite del indicador de dash.
        /// </summary>
        public void SetDashBarSprite(Sprite sprite)
        {
            if (_dashFillImage != null && sprite != null)
            {
                _dashFillImage.sprite = sprite;
                _dashFillImage.type = Image.Type.Filled;
            }
        }

        /// <summary>
        /// Cambia la fuente de todos los textos del panel.
        /// Usar para migrar a una fuente personalizada sin cambiar código.
        /// </summary>
        public void SetFont(Font font)
        {
            if (font == null) return;

            Text[] allTexts = GetComponentsInChildren<Text>(true);
            foreach (var text in allTexts)
            {
                text.font = font;
            }
        }

        #endregion

        // =====================================================================
        // PRIVATE VISUALS
        // =====================================================================

        #region Health Visuals

        private void ApplyHealthVisuals(float health)
        {
            if (_healthSegments == null || _healthSegments.Length == 0) return;

            float healthPercent = Mathf.Clamp01(health);
            int totalSegments = _healthSegments.Length;
            int filledSegments = Mathf.CeilToInt(healthPercent * totalSegments);

            // Determinar color según nivel de vida
            Color barColor;
            if (healthPercent <= _config.healthLowThreshold)
                barColor = _config.healthLowColor;
            else if (healthPercent <= _config.healthMidThreshold)
                barColor = Color.Lerp(_config.healthLowColor, _config.healthMidColor,
                    (healthPercent - _config.healthLowThreshold) /
                    (_config.healthMidThreshold - _config.healthLowThreshold));
            else
                barColor = Color.Lerp(_config.healthMidColor, _config.healthHighColor,
                    (healthPercent - _config.healthMidThreshold) /
                    (1f - _config.healthMidThreshold));

            for (int i = 0; i < totalSegments; i++)
            {
                if (_healthSegments[i] == null) continue;

                if (i < filledSegments)
                {
                    _healthSegments[i].color = barColor;
                    _healthSegments[i].gameObject.SetActive(true);
                }
                else
                {
                    // Segmento vacío - color oscuro
                    _healthSegments[i].color = new Color(0.12f, 0.12f, 0.18f);
                    _healthSegments[i].gameObject.SetActive(true);
                }
            }

            // Actualizar texto de porcentaje
            if (_healthPercentText != null)
            {
                _healthPercentText.text = $"{healthPercent * 100:F0}%";
                _healthPercentText.color = barColor;
            }
        }

        #endregion
    }
}
