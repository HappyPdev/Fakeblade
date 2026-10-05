using FakeBlade.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// Panel de HUD de un jugador (GDD 9.1), construido por código en estilo pixel-art.
    ///
    ///  ┌──────────────────────────────────────────────┐
    ///  │ (●)  [J1] Jugador 1              ♦♦♦         │   ← badge, nombre, vidas/puntos
    ///  │ (●)  ▓▓▓▓▓▓▓▓▓▓▓▓░░░░░░░░░░░░       64%      │   ← barra RPM (2 capas) + %
    ///  │ (●)  ■ ■ ■ □                                  │   ← cargas de ataque
    ///  │      ────────────                             │   ← dash
    ///  └──────────────────────────────────────────────┘
    ///  (●) = esfera del especial en la esquina exterior. Los paneles de la derecha se reflejan.
    ///
    /// Rendimiento: canvas propio (aísla los rebuilds), valores cuantizados a la rejilla
    /// de píxeles (solo se redibuja cuando cambia un "píxel") y strings cacheadas (sin GC).
    /// </summary>
    public class PlayerHUDPanel : MonoBehaviour
    {
        #region Layout (píxeles de UI)
        private const int W = 100;
        private const int H = 29;

        private const int SPHERE_X = 3;
        private const int SPHERE_Y = 3;
        private const int SPHERE_D = 22;

        private const int CONTENT_X = 28;
        private const int HEADER_Y = 3;
        private const int HEADER_H = 6;
        private const int BADGE_W = 10;
        private const int NAME_X = CONTENT_X + 12;
        private const int NAME_W = 34;
        private const int STATUS_X = CONTENT_X + 46;
        private const int STATUS_W = 23;
        private const int LIFE_ICON = 5;
        private const int MAX_LIFE_ICONS = 4;

        private const int BAR_Y = 11;
        private const int BAR_H = 6;
        private const int BAR_W = 52;
        private const int BAR_STEPS = BAR_W - 2;
        private const int PERCENT_X = CONTENT_X + 54;
        private const int PERCENT_W = 15;

        private const int PIP_Y = 19;
        private const int PIP_SIZE = 5;
        private const int PIP_GAP = 1;
        private const int PIP_INNER_STEPS = PIP_SIZE - 2;

        private const int DASH_Y = 26;
        private const int DASH_H = 1;

        private const int ORBIT_COUNT = 6;
        private const int ORBIT_RADIUS = 13;
        private const int ORBIT_SIZE = 2;
        private const int SPHERE_STEPS = SPHERE_D - 2;

        private const float POP_DURATION = 0.15f;
        #endregion

        #region References
        private HUDTheme _theme;
        private int _px;
        private bool _mirror;
        private PlayerController _player;

        private Image _border;
        private CanvasGroup _contentGroup;

        private Image _badge;
        private TextMeshProUGUI _badgeText;
        private TextMeshProUGUI _nameText;
        private readonly Image[] _lifeIcons = new Image[MAX_LIFE_ICONS];
        private TextMeshProUGUI _statusText;

        private Image _barFill;
        private Image _barTrail;
        private TextMeshProUGUI _percentText;

        private readonly Image[] _pipBorders = new Image[AttackSystem.MaxSupportedCharges];
        private readonly Image[] _pipFills = new Image[AttackSystem.MaxSupportedCharges];
        private readonly float[] _pipPop = new float[AttackSystem.MaxSupportedCharges];
        private readonly float[] _pipScale = new float[AttackSystem.MaxSupportedCharges];

        private Image _dashFill;

        private Image _sphereGlow;
        private Image _sphereFill;
        private readonly Image[] _orbit = new Image[ORBIT_COUNT];

        private GameObject _koOverlay;
        private TextMeshProUGUI _koText;
        #endregion

        #region State
        private float _time;
        private float _trail = 1f;
        private float _trailHold;
        private float _lastHealth = 1f;
        private int _lastFillStep = -1;
        private int _lastTrailStep = -1;
        private int _lastPercent = -1;
        private int _lastTier = -1;
        private int _pipCount = -1;
        private int _lastCharges = -1;
        private int _lastDashStep = -1;
        private int _lastSphereStep = -1;
        private int _lastOrbitVisible = -1;
        private float _orbitAngle;
        private Color _abilityColor;
        private bool _lastInvulnerableBlink;
        #endregion

        public PlayerController Player => _player;

        #region Creation
        /// <summary>Crea un panel en la esquina del slot (0 sup-izq, 1 sup-der, 2 inf-izq, 3 inf-der).</summary>
        public static PlayerHUDPanel Create(Transform parent, HUDTheme theme, int slot)
        {
            int px = Mathf.Max(1, theme.pixelSize);
            bool right = slot == 1 || slot == 3;
            bool bottom = slot >= 2;

            RectTransform root = PixelUI.CreateRect($"PlayerPanel_{slot}", parent);
            var anchor = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);
            root.anchorMin = anchor;
            root.anchorMax = anchor;
            root.pivot = anchor;
            float margin = theme.screenMarginPixels * px;
            root.anchoredPosition = new Vector2(right ? -margin : margin, bottom ? margin : -margin);
            root.sizeDelta = new Vector2(W * px, H * px);

            // Canvas anidado: los cambios de este panel no reconstruyen los demás
            root.gameObject.AddComponent<Canvas>();

            var panel = root.gameObject.AddComponent<PlayerHUDPanel>();
            panel.Build(theme, right);
            return panel;
        }

        private void Build(HUDTheme theme, bool mirror)
        {
            _theme = theme;
            _px = Mathf.Max(1, theme.pixelSize);
            _mirror = mirror;
            Transform root = transform;

            // Marco: sombra, contorno oscuro, borde de color del jugador y fondo
            var shadow = PixelUI.CreateImage("Shadow", root, theme.panelShadow);
            PixelUI.Place(shadow.rectTransform, 1, 1, W, H, _px);
            var outline = PixelUI.CreateImage("Outline", root, theme.sphereOutline);
            PixelUI.Place(outline.rectTransform, 0, 0, W, H, _px);
            _border = PixelUI.CreateImage("Border", root, Color.white);
            PixelUI.Place(_border.rectTransform, 1, 1, W - 2, H - 2, _px);
            var background = PixelUI.CreateImage("Background", root, theme.panelBackground);
            PixelUI.Place(background.rectTransform, 2, 2, W - 4, H - 4, _px);

            RectTransform content = PixelUI.CreateRect("Content", root);
            PixelUI.Stretch(content, 0, _px);
            _contentGroup = content.gameObject.AddComponent<CanvasGroup>();
            _contentGroup.interactable = false;
            _contentGroup.blocksRaycasts = false;

            BuildSphere(content);
            BuildHeader(content);
            BuildBar(content);
            BuildPips(content);
            BuildDash(content);
            BuildOverlay(root);
        }

        private void BuildSphere(Transform parent)
        {
            RectTransform sphere = PixelUI.CreateRect("SpecialSphere", parent);
            PixelUI.Place(sphere, SPHERE_X, SPHERE_Y, SPHERE_D, SPHERE_D, _px, _mirror, W);

            _sphereGlow = PixelUI.CreateImage("Glow", sphere, Color.clear, PixelUI.Disc(SPHERE_D + 4));
            CenterIn(_sphereGlow.rectTransform, 0, 0, SPHERE_D + 4);
            _sphereGlow.enabled = false;

            var outline = PixelUI.CreateImage("Outline", sphere, _theme.sphereOutline, PixelUI.Disc(SPHERE_D));
            PixelUI.Stretch(outline.rectTransform, 0, _px);

            var empty = PixelUI.CreateImage("Empty", sphere, _theme.sphereEmpty, PixelUI.Disc(SPHERE_D - 2));
            PixelUI.Stretch(empty.rectTransform, 1, _px);

            _sphereFill = PixelUI.CreateFilledImage("Fill", sphere, Color.white, PixelUI.Disc(SPHERE_D - 2),
                Image.FillMethod.Vertical, (int)Image.OriginVertical.Bottom);
            PixelUI.Stretch(_sphereFill.rectTransform, 1, _px);
            _sphereFill.fillAmount = 0f;

            var shine = PixelUI.CreateImage("Shine", sphere, new Color(1f, 1f, 1f, 0.45f));
            PixelUI.Place(shine.rectTransform, 5, 4, 2, 2, _px);

            for (int i = 0; i < ORBIT_COUNT; i++)
            {
                _orbit[i] = PixelUI.CreateImage($"Orbit_{i}", sphere, Color.white);
                CenterIn(_orbit[i].rectTransform, 0, 0, ORBIT_SIZE);
                _orbit[i].enabled = false;
            }
        }

        private void BuildHeader(Transform parent)
        {
            _badge = PixelUI.CreateImage("Badge", parent, Color.white);
            PixelUI.Place(_badge.rectTransform, CONTENT_X, HEADER_Y, BADGE_W, HEADER_H, _px, _mirror, W);
            _badgeText = PixelUI.CreateText("BadgeText", _badge.transform, _theme, 5 * _px,
                TextAlignmentOptions.Center, _theme.panelBackground);
            PixelUI.Stretch(_badgeText.rectTransform, 0, _px);

            _nameText = PixelUI.CreateText("Name", parent, _theme, 5 * _px,
                _mirror ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft, _theme.textColor);
            PixelUI.Place(_nameText.rectTransform, NAME_X, HEADER_Y, NAME_W, HEADER_H, _px, _mirror, W);

            // Vidas: iconos desde el borde exterior hacia dentro
            for (int i = 0; i < MAX_LIFE_ICONS; i++)
            {
                _lifeIcons[i] = PixelUI.CreateImage($"Life_{i}", parent, Color.white, PixelUI.TopIcon);
                int x = STATUS_X + STATUS_W - LIFE_ICON - i * (LIFE_ICON + 1);
                PixelUI.Place(_lifeIcons[i].rectTransform, x, HEADER_Y + 1, LIFE_ICON, LIFE_ICON, _px, _mirror, W);
                _lifeIcons[i].enabled = false;
            }

            _statusText = PixelUI.CreateText("Status", parent, _theme, 5 * _px,
                _mirror ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight, _theme.textColor);
            PixelUI.Place(_statusText.rectTransform, STATUS_X, HEADER_Y, STATUS_W, HEADER_H, _px, _mirror, W);
        }

        private void BuildBar(Transform parent)
        {
            var barBg = PixelUI.CreateImage("RPMBar", parent, _theme.barBackground);
            PixelUI.Place(barBg.rectTransform, CONTENT_X, BAR_Y, BAR_W, BAR_H, _px, _mirror, W);

            int origin = _mirror ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;

            _barTrail = PixelUI.CreateFilledImage("Trail", barBg.transform, _theme.healthTrail, null,
                Image.FillMethod.Horizontal, origin);
            PixelUI.Stretch(_barTrail.rectTransform, 1, _px);

            _barFill = PixelUI.CreateFilledImage("Fill", barBg.transform, _theme.healthHigh, null,
                Image.FillMethod.Horizontal, origin);
            PixelUI.Stretch(_barFill.rectTransform, 1, _px);

            // Brillo superior (1 píxel) y marcas cada 25%
            var shine = PixelUI.CreateImage("Shine", barBg.transform, new Color(1f, 1f, 1f, 0.18f));
            PixelUI.Place(shine.rectTransform, 1, 1, BAR_W - 2, 1, _px);
            for (int i = 1; i <= 3; i++)
            {
                var tick = PixelUI.CreateImage($"Tick_{i}", barBg.transform, new Color(0f, 0f, 0f, 0.45f));
                PixelUI.Place(tick.rectTransform, 1 + Mathf.RoundToInt(BAR_STEPS * i * 0.25f), 1, 1, BAR_H - 2, _px);
            }

            _percentText = PixelUI.CreateText("Percent", parent, _theme, 5 * _px,
                _mirror ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight, _theme.healthHigh);
            PixelUI.Place(_percentText.rectTransform, PERCENT_X, BAR_Y, PERCENT_W, BAR_H, _px, _mirror, W);
        }

        private void BuildPips(Transform parent)
        {
            for (int i = 0; i < _pipBorders.Length; i++)
            {
                _pipBorders[i] = PixelUI.CreateImage($"Charge_{i}", parent, _theme.chargeBorder);
                RectTransform rt = _pipBorders[i].rectTransform;
                PixelUI.Place(rt, CONTENT_X + i * (PIP_SIZE + PIP_GAP), PIP_Y, PIP_SIZE, PIP_SIZE, _px, _mirror, W);
                SetCenterPivot(rt);

                _pipFills[i] = PixelUI.CreateFilledImage("Fill", rt, _theme.chargeReady, null,
                    Image.FillMethod.Vertical, (int)Image.OriginVertical.Bottom);
                PixelUI.Stretch(_pipFills[i].rectTransform, 1, _px);
                _pipScale[i] = 1f;
            }
        }

        private void BuildDash(Transform parent)
        {
            var dashBg = PixelUI.CreateImage("Dash", parent, _theme.barBackground);
            PixelUI.Place(dashBg.rectTransform, CONTENT_X, DASH_Y, BAR_W, DASH_H, _px, _mirror, W);

            int origin = _mirror ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            _dashFill = PixelUI.CreateFilledImage("Fill", dashBg.transform, _theme.dashReady, null,
                Image.FillMethod.Horizontal, origin);
            PixelUI.Stretch(_dashFill.rectTransform, 0, _px);
        }

        private void BuildOverlay(Transform root)
        {
            var overlay = PixelUI.CreateImage("KOOverlay", root, _theme.overlayColor);
            PixelUI.Place(overlay.rectTransform, 2, 2, W - 4, H - 4, _px);
            _koOverlay = overlay.gameObject;

            _koText = PixelUI.CreateText("KOText", overlay.transform, _theme, 9 * _px,
                TextAlignmentOptions.Center, _theme.healthLow);
            PixelUI.Stretch(_koText.rectTransform, 0, _px);
            _koOverlay.SetActive(false);
        }

        /// <summary>Rect centrado en el padre, desplazado (dx, dy) píxeles de UI.</summary>
        private void CenterIn(RectTransform rt, int dx, int dy, int size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(dx * _px, dy * _px);
            rt.sizeDelta = new Vector2(size * _px, size * _px);
        }

        /// <summary>Cambia el pivot al centro sin mover el rect (para escalar desde el centro).</summary>
        private static void SetCenterPivot(RectTransform rt)
        {
            Vector2 size = rt.sizeDelta;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition += new Vector2(size.x * 0.5f, -size.y * 0.5f);
        }
        #endregion

        #region Binding
        public void Bind(PlayerController player)
        {
            Unbind();
            _player = player;
            if (player == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            player.OnStatusChanged += HandleStatusChanged;
            Loc.OnLanguageChanged += RefreshTexts;

            Color color = player.PlayerColor;
            _border.color = color;
            _badge.color = color;
            for (int i = 0; i < _lifeIcons.Length; i++) _lifeIcons[i].color = color;

            ResetVisualState();
            RefreshTexts();
            HandleStatusChanged(player);
        }

        private void Unbind()
        {
            if (_player != null) _player.OnStatusChanged -= HandleStatusChanged;
            Loc.OnLanguageChanged -= RefreshTexts;
            _player = null;
        }

        private void OnDestroy() => Unbind();

        private void ResetVisualState()
        {
            _trail = 1f;
            _trailHold = 0f;
            _lastHealth = 1f;
            _lastFillStep = _lastTrailStep = _lastPercent = _lastTier = -1;
            _pipCount = _lastCharges = -1;
            _lastDashStep = _lastSphereStep = _lastOrbitVisible = -1;
            _lastInvulnerableBlink = false;
            for (int i = 0; i < _pipPop.Length; i++) _pipPop[i] = 0f;

            _abilityColor = SpecialAbilities.Get(_player.Stats != null ? _player.Stats.SpecialAbility : SpecialAbilityType.SpinBoost).color;
            _sphereGlow.color = new Color(_abilityColor.r, _abilityColor.g, _abilityColor.b, 0.35f);
            for (int i = 0; i < _orbit.Length; i++) _orbit[i].color = Color.Lerp(_abilityColor, Color.white, 0.4f);

            HideKO();
            _contentGroup.alpha = 1f;
        }

        private void RefreshTexts()
        {
            if (_player == null) return;

            int number = _player.PlayerID + 1;
            _badgeText.text = Loc.Format("PLAYER_BADGE", number);

            string playerName = _player.PlayerName;
            bool isDefaultName = string.IsNullOrEmpty(playerName) || playerName.StartsWith("Player ");
            _nameText.text = isDefaultName ? Loc.Format("PLAYER_NAME", number) : playerName;

            HandleStatusChanged(_player);
        }

        /// <summary>Vidas / puntos (por evento, no por frame).</summary>
        private void HandleStatusChanged(PlayerController player)
        {
            var gm = GameManager.Instance;
            MatchRules rules = gm != null ? gm.Rules : null;

            for (int i = 0; i < _lifeIcons.Length; i++) _lifeIcons[i].enabled = false;
            _statusText.text = string.Empty;

            if (rules == null || player == null) return;

            if (rules.ShowsLives)
            {
                int lives = player.Lives;
                if (lives <= MAX_LIFE_ICONS)
                {
                    for (int i = 0; i < lives; i++) _lifeIcons[i].enabled = true;
                }
                else
                {
                    _lifeIcons[0].enabled = true;
                    _statusText.text = PixelUI.Times(lives);
                    // Deja hueco para el icono (que está en el lado exterior)
                    float gap = (LIFE_ICON + 1) * _px;
                    _statusText.margin = _mirror ? new Vector4(gap, 0f, 0f, 0f) : new Vector4(0f, 0f, gap, 0f);
                }
            }
            else if (rules.ShowsScore)
            {
                _statusText.margin = Vector4.zero;
                _statusText.text = PixelUI.Number(player.Score) + " " + Loc.Get("POINTS_SHORT");
            }

            if (player.IsEliminated) ShowKO(true);
        }
        #endregion

        #region K.O.
        public void ShowKO(bool eliminated)
        {
            _koOverlay.SetActive(true);
            _koText.text = Loc.Get(eliminated ? "OUT" : "KO");
            _koText.fontSize = (eliminated ? 6 : 9) * _px;
            _contentGroup.alpha = eliminated ? 0.45f : 0.8f;
        }

        public void ShowRespawning()
        {
            _koOverlay.SetActive(true);
            _koText.text = Loc.Get("RESPAWN");
            _koText.fontSize = 6 * _px;
        }

        public void HideKO()
        {
            _koOverlay.SetActive(false);
            _contentGroup.alpha = 1f;
        }
        #endregion

        #region Update
        private void Update()
        {
            // _barFill null = recompilación en caliente (las referencias construidas por código no se serializan)
            if (_player == null || _barFill == null) return;
            FakeBladeController blade = _player.Blade;
            if (blade == null) return;

            float dt = Time.deltaTime;
            _time += dt;

            UpdateHealth(blade.SpinSpeedPercentage, dt);
            UpdatePips(blade.Attack, dt);
            UpdateDash(blade.DashCooldownProgress);
            UpdateSphere(blade.Special, dt);
            UpdateInvulnerability(blade.IsInvulnerable);
        }

        /// <summary>
        /// Barra de dos capas: la superior baja de golpe; la inferior espera un momento
        /// y luego la sigue poco a poco (GDD 9.1).
        /// </summary>
        private void UpdateHealth(float health, float dt)
        {
            health = Mathf.Clamp01(health);

            if (health < _lastHealth - 0.005f) _trailHold = _theme.trailDelay; // golpe
            _lastHealth = health;

            if (_trail <= health) _trail = health;
            else if (_trailHold > 0f) _trailHold -= dt;
            else _trail = Mathf.MoveTowards(_trail, health, _theme.trailSpeed * dt);

            int fillStep = Quantize(health, BAR_STEPS);
            if (fillStep != _lastFillStep)
            {
                _lastFillStep = fillStep;
                _barFill.fillAmount = fillStep / (float)BAR_STEPS;
            }

            int trailStep = Quantize(_trail, BAR_STEPS);
            if (trailStep != _lastTrailStep)
            {
                _lastTrailStep = trailStep;
                _barTrail.fillAmount = trailStep / (float)BAR_STEPS;
            }

            int tier = health <= _theme.lowThreshold ? 0 : health <= _theme.midThreshold ? 1 : 2;
            if (tier != _lastTier)
            {
                _lastTier = tier;
                _barFill.color = _theme.GetHealthColor(health);
            }

            int percent = health <= 0f ? 0 : Mathf.Max(1, Mathf.CeilToInt(health * 100f - 0.001f));
            if (percent != _lastPercent)
            {
                _lastPercent = percent;
                _percentText.text = PixelUI.Percent(percent);
            }

            // Vida baja: el % parpadea por pasos
            Color percentColor = tier == 0 && percent > 0 && PixelUI.Blink(_time, 2f)
                ? _theme.textColor
                : _theme.GetHealthColor(health);
            _percentText.color = percentColor;
        }

        /// <summary>
        /// Cargas de ataque: llenas, recargando (se llenan por pasos y parpadea el contorno
        /// poco antes de completarse) y "pop" de tamaño al recuperarse.
        /// Mientras se carga un ataque, las cargas que se van a gastar parpadean.
        /// </summary>
        private void UpdatePips(AttackSystem attack, float dt)
        {
            int max = attack.MaxCharges;
            if (max != _pipCount)
            {
                _pipCount = max;
                for (int i = 0; i < _pipBorders.Length; i++)
                    _pipBorders[i].gameObject.SetActive(i < max);
            }

            int current = attack.CurrentCharges;
            if (_lastCharges >= 0 && current > _lastCharges)
            {
                for (int i = _lastCharges; i < current && i < max; i++)
                    _pipPop[i] = POP_DURATION;
            }
            _lastCharges = current;

            bool charging = attack.IsCharging;
            int chargeLevel = attack.ChargeLevel;
            float recharge = attack.RechargeProgress;
            bool chargeBlink = PixelUI.Blink(_time, 6f);
            bool readyBlink = recharge >= _theme.blinkBeforeReady && PixelUI.Blink(_time, 8f);

            for (int i = 0; i < max; i++)
            {
                Image border = _pipBorders[i];
                Image fill = _pipFills[i];

                if (i < current)
                {
                    bool willSpend = charging && i >= current - chargeLevel;
                    fill.fillAmount = 1f;
                    fill.color = willSpend && chargeBlink ? _theme.chargeCharging : _theme.chargeReady;
                    border.color = willSpend ? _theme.chargeCharging : _theme.chargeBorder;
                }
                else if (i == current)
                {
                    int step = Mathf.FloorToInt(recharge * PIP_INNER_STEPS);
                    fill.fillAmount = step / (float)PIP_INNER_STEPS;
                    fill.color = _theme.chargeEmpty;
                    border.color = readyBlink ? _theme.chargeBlink : _theme.chargeBorder;
                }
                else
                {
                    fill.fillAmount = 0f;
                    border.color = _theme.chargeBorder;
                }

                // Pop por pasos: 1.4 → 1.2 → 1.0
                float scale = 1f;
                if (_pipPop[i] > 0f)
                {
                    _pipPop[i] -= dt;
                    scale = _pipPop[i] > POP_DURATION * 0.66f ? 1.4f : _pipPop[i] > POP_DURATION * 0.33f ? 1.2f : 1f;
                }
                if (!Mathf.Approximately(scale, _pipScale[i]))
                {
                    _pipScale[i] = scale;
                    border.rectTransform.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        private void UpdateDash(float progress)
        {
            int step = Quantize(progress, BAR_W);
            if (step == _lastDashStep) return;

            _lastDashStep = step;
            _dashFill.fillAmount = step / (float)BAR_W;
            _dashFill.color = step >= BAR_W ? _theme.dashReady : _theme.dashCooldown;
        }

        /// <summary>
        /// Esfera del especial: se rellena al cargar; llena → brilla y aparecen partículas;
        /// activa → color y partículas exagerados mientras el relleno baja (GDD 9.1).
        /// </summary>
        private void UpdateSphere(SpecialAbilitySystem special, float dt)
        {
            bool ready = special.IsReady;
            bool active = special.IsActive;

            int step = Quantize(special.Energy, SPHERE_STEPS);
            if (step != _lastSphereStep)
            {
                _lastSphereStep = step;
                _sphereFill.fillAmount = step / (float)SPHERE_STEPS;
            }

            Color fillColor;
            if (active)
                fillColor = PixelUI.Blink(_time, 4f) ? Color.Lerp(_abilityColor, Color.white, 0.45f) : _abilityColor;
            else if (ready)
                fillColor = PixelUI.Blink(_time, 1.5f) ? Color.Lerp(_abilityColor, Color.white, 0.25f) : _abilityColor;
            else
                fillColor = new Color(_abilityColor.r * 0.7f, _abilityColor.g * 0.7f, _abilityColor.b * 0.7f, 1f);
            _sphereFill.color = fillColor;

            bool glow = ready || active;
            _sphereGlow.enabled = glow;
            if (glow)
            {
                bool strong = active ? PixelUI.Blink(_time, 5f) : PixelUI.Blink(_time, 1.5f);
                float alpha = active ? (strong ? 0.75f : 0.4f) : (strong ? 0.45f : 0.2f);
                _sphereGlow.color = new Color(_abilityColor.r, _abilityColor.g, _abilityColor.b, alpha);
            }

            int visible = active ? ORBIT_COUNT : ready ? ORBIT_COUNT / 2 : 0;
            if (visible != _lastOrbitVisible)
            {
                _lastOrbitVisible = visible;
                for (int i = 0; i < ORBIT_COUNT; i++) _orbit[i].enabled = i < visible;
            }

            if (visible > 0)
            {
                _orbitAngle += (active ? 300f : 110f) * dt;
                float stepAngle = 360f / visible;
                for (int i = 0; i < visible; i++)
                {
                    float a = (_orbitAngle + i * stepAngle) * Mathf.Deg2Rad;
                    // Posición ajustada a la rejilla de píxeles
                    float x = Mathf.Round(Mathf.Cos(a) * ORBIT_RADIUS) * _px;
                    float y = Mathf.Round(Mathf.Sin(a) * ORBIT_RADIUS) * _px;
                    RectTransform rt = _orbit[i].rectTransform;
                    Vector2 pos = new Vector2(x, y);
                    if (rt.anchoredPosition != pos) rt.anchoredPosition = pos;
                }
            }
        }

        private void UpdateInvulnerability(bool invulnerable)
        {
            bool blink = invulnerable && PixelUI.Blink(_time, 6f);
            if (blink == _lastInvulnerableBlink) return;

            _lastInvulnerableBlink = blink;
            _border.color = blink ? Color.white : _player.PlayerColor;
        }

        private static int Quantize(float value, int steps)
        {
            if (value <= 0f) return 0;
            return Mathf.Clamp(Mathf.CeilToInt(value * steps - 0.001f), 1, steps);
        }
        #endregion
    }
}
