using System;
using System.Collections.Generic;
using FakeBlade.UI;
using TMPro;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Debug visual del sandbox (GDD 6.3, Quehaceres E4 y E5). Cada cosa se activa en el panel
    /// (SandboxSettings) y todo empieza apagado:
    /// - Ventana de parry: anillo cian alrededor de la peonza mientras su ataque puede hacer parry.
    /// - Velocidades: flecha con la velocidad de cada peonza (lo que recorrerá en 0,25 s).
    /// - Números de daño: lo que pierde cada peonza, flotando sobre ella.
    /// - Estados: el estado alterado (y la invulnerabilidad) con el tiempo que le queda.
    /// - FPS: el contador de Opciones, mientras se está en el sandbox.
    /// - Registro: los últimos eventos abajo a la izquierda (choques, parrys, especiales, estados, K.O.).
    /// Sigue a las peonzas que entran y salen (dummies, reinicios) y usa tiempo real, así que
    /// se lee igual a cámara lenta.
    /// </summary>
    public sealed class SandboxDebugView : MonoBehaviour
    {
        private const int LogLines = 6;
        private const float LogLife = 6f;
        private const int NumberPool = 24;
        private const float NumberLife = 0.9f;
        private const float MinDamageNumber = 1f; // el roce continuo no llena la pantalla de números
        private const float VelocityLookAhead = 0.25f;
        private const int RingSegments = 32;

        private sealed class Tracked
        {
            public PlayerController Player;
            public FakeBladeController Blade;
            public LineRenderer Parry;
            public LineRenderer Velocity;
            public TextMeshProUGUI Status;
            public Action<float, FakeBladeController> OnDamaged;
            public Action<FakeBladeController, float> OnClash;
            public Action<SpecialAbilityType> OnSpecial;
            public Action<StatusEffectType> OnStatus;
            public Action OnHealCut;
            public Action OnKO;
        }

        private sealed class Number
        {
            public TextMeshProUGUI Text;
            public Vector3 World;
            public float Age = NumberLife;
        }

        private struct LogEntry
        {
            public string Text;
            public float Time;
        }

        private HUDTheme _theme;
        private int _px;
        private RectTransform _root;
        private Material _lineMaterial;
        private Transform _worldRoot; // líneas del mundo: fuera del canvas, que las escalaría
        private UnityEngine.UI.Image _logBackground;

        private readonly Dictionary<PlayerController, Tracked> _tracked = new Dictionary<PlayerController, Tracked>();
        private readonly List<PlayerController> _gone = new List<PlayerController>();
        private readonly Number[] _numbers = new Number[NumberPool];
        private int _nextNumber;

        private readonly List<LogEntry> _log = new List<LogEntry>();
        private readonly TextMeshProUGUI[] _logLines = new TextMeshProUGUI[LogLines];
        private int _clashFrame = -1;
        private PlayerController _clashA, _clashB;
        private float _clashDamageA, _clashDamageB;

        private bool _fpsShown;

        public static SandboxDebugView Create(Transform parent, HUDTheme theme, int sortingOrder)
        {
            Canvas canvas = PixelUI.CreateOverlayCanvas("[SandboxDebug]", parent, theme, sortingOrder);
            var view = canvas.gameObject.AddComponent<SandboxDebugView>();
            view.Build(theme, (RectTransform)canvas.transform);
            return view;
        }

        private void Build(HUDTheme theme, RectTransform root)
        {
            _theme = theme;
            _px = Mathf.Max(1, theme.pixelSize);
            _root = root;
            _lineMaterial = new Material(Shader.Find("Sprites/Default"));
            _worldRoot = new GameObject("[SandboxDebugWorld]").transform;

            for (int i = 0; i < NumberPool; i++)
            {
                var text = PixelUI.CreateText("DamageNumber", root, theme, 6 * _px, TextAlignmentOptions.Center, Color.white);
                text.rectTransform.sizeDelta = new Vector2(40 * _px, 8 * _px);
                text.enabled = false;
                _numbers[i] = new Number { Text = text };
            }

            // Registro abajo a la izquierda (con fondo para leerse sobre el suelo claro): la línea más nueva, abajo
            _logBackground = PixelUI.CreateImage("LogBackground", root, new Color(0f, 0f, 0f, 0.55f));
            RectTransform bg = _logBackground.rectTransform;
            bg.anchorMin = bg.anchorMax = bg.pivot = Vector2.zero;
            bg.anchoredPosition = new Vector2(2 * _px, 12 * _px);
            bg.sizeDelta = new Vector2(130 * _px, (LogLines * 6 + 4) * _px);
            _logBackground.raycastTarget = false;
            _logBackground.enabled = false;
            for (int i = 0; i < LogLines; i++)
            {
                var text = PixelUI.CreateText("Log", root, theme, 4.5f * _px, TextAlignmentOptions.BottomLeft, theme.textColor);
                text.richText = true; // nombres con el color de cada jugador
                RectTransform rt = text.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(4 * _px, (14 + (LogLines - 1 - i) * 6) * _px);
                rt.sizeDelta = new Vector2(200 * _px, 6 * _px);
                text.enabled = false;
                _logLines[i] = text;
            }
        }

        private void OnEnable() => CollisionResolver.OnParry += HandleParry;

        private void OnDisable() => CollisionResolver.OnParry -= HandleParry;

        private void OnDestroy()
        {
            foreach (Tracked t in _tracked.Values) Untrack(t);
            _tracked.Clear();
            if (_lineMaterial != null) Destroy(_lineMaterial);
            if (_worldRoot != null) Destroy(_worldRoot.gameObject);
            // Al salir del sandbox, el contador vuelve a lo que diga Opciones
            if (_fpsShown) FpsCounter.SetVisible(SettingsService.Current.showFps);
        }

        private void LateUpdate()
        {
            Sync();
            Camera cam = Camera.main;
            foreach (Tracked t in _tracked.Values) UpdateBlade(t, cam);
            UpdateNumbers(cam);
            UpdateLog();

            bool fps = SandboxSettings.ShowFps || SettingsService.Current.showFps;
            if (fps != _fpsShown)
            {
                _fpsShown = fps;
                FpsCounter.SetVisible(fps);
            }
        }

        #region Peonzas
        private void Sync()
        {
            GameManager gm = GameManager.Instance;
            _gone.Clear();
            foreach (PlayerController p in _tracked.Keys)
                if (p == null || gm == null || !Contains(gm.Players, p)) _gone.Add(p);
            foreach (PlayerController p in _gone)
            {
                Untrack(_tracked[p]);
                _tracked.Remove(p);
            }

            if (gm == null) return;
            foreach (PlayerController p in gm.Players)
                if (p != null && p.Blade != null && !_tracked.ContainsKey(p)) _tracked.Add(p, Track(p));
        }

        private static bool Contains(IReadOnlyList<PlayerController> list, PlayerController p)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == p) return true;
            return false;
        }

        private Tracked Track(PlayerController player)
        {
            FakeBladeController blade = player.Blade;
            var t = new Tracked
            {
                Player = player,
                Blade = blade,
                Parry = CreateLine("ParryWindow", VfxSystem.ParryColor, RingSegments, false),
                Velocity = CreateLine("Velocity", player.PlayerColor, 5, true),
                Status = PixelUI.CreateText("Status", _root, _theme, 4.5f * _px, TextAlignmentOptions.Center, _theme.textColor)
            };
            t.Status.rectTransform.sizeDelta = new Vector2(60 * _px, 6 * _px);
            t.Status.enabled = false;

            // Anillo del tamaño de la peonza (collider), un poco por fuera
            float radius = 0.6f;
            if (blade.TryGetComponent(out SphereCollider sphere)) radius = sphere.radius * blade.transform.lossyScale.x;
            radius *= 1.15f;
            for (int i = 0; i < RingSegments; i++)
            {
                float a = i / (float)RingSegments * Mathf.PI * 2f;
                t.Parry.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.05f, Mathf.Sin(a) * radius));
            }

            t.OnDamaged = (amount, source) => { if (amount >= MinDamageNumber) SpawnNumber(blade.Position, amount); };
            t.OnClash = (other, taken) => LogClash(player, other, taken);
            t.OnSpecial = type => Log(Loc.Format("LOG_SPECIAL", Name(player), SpecialAbilities.Get(type).DisplayName));
            t.OnStatus = status => { if (status != StatusEffectType.None) Log(Loc.Format("LOG_STATUS", Name(player), StatusName(status))); };
            t.OnKO = () => Log(Loc.Format("LOG_KO", Name(player)));
            t.OnHealCut = () => Log(Loc.Format("LOG_HEAL_CUT", Name(player)));
            blade.OnDamaged += t.OnDamaged;
            blade.OnClash += t.OnClash;
            blade.OnSpecialActivated += t.OnSpecial;
            blade.OnStatusEffectChanged += t.OnStatus;
            blade.OnHealCut += t.OnHealCut;
            blade.OnSpinOut += t.OnKO;
            return t;
        }

        private static void Untrack(Tracked t)
        {
            if (t.Blade != null)
            {
                t.Blade.OnDamaged -= t.OnDamaged;
                t.Blade.OnClash -= t.OnClash;
                t.Blade.OnSpecialActivated -= t.OnSpecial;
                t.Blade.OnStatusEffectChanged -= t.OnStatus;
                t.Blade.OnSpinOut -= t.OnKO;
                t.Blade.OnHealCut -= t.OnHealCut;
            }
            if (t.Parry != null) Destroy(t.Parry.gameObject);
            if (t.Velocity != null) Destroy(t.Velocity.gameObject);
            if (t.Status != null) Destroy(t.Status.gameObject);
        }

        private LineRenderer CreateLine(string name, Color color, int points, bool worldSpace)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_worldRoot, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = worldSpace;
            line.loop = !worldSpace;
            line.positionCount = points;
            line.widthMultiplier = 0.08f;
            line.sharedMaterial = _lineMaterial;
            line.startColor = line.endColor = color;
            line.numCapVertices = 0;
            line.enabled = false;
            return line;
        }

        private void UpdateBlade(Tracked t, Camera cam)
        {
            FakeBladeController blade = t.Blade;
            bool alive = blade != null && !blade.IsDestroyed && blade.gameObject.activeInHierarchy;

            bool parry = alive && SandboxSettings.ShowParryWindow && blade.IsInParryWindow;
            t.Parry.enabled = parry;
            if (parry) t.Parry.transform.position = blade.Position;

            Vector3 velocity = alive ? blade.Velocity : Vector3.zero;
            velocity.y = 0f;
            bool showVelocity = alive && SandboxSettings.ShowVelocity && velocity.sqrMagnitude > 0.04f;
            t.Velocity.enabled = showVelocity;
            if (showVelocity)
            {
                Vector3 start = blade.Position + Vector3.up * 0.6f;
                Vector3 end = start + velocity * VelocityLookAhead;
                Vector3 back = -velocity.normalized * 0.25f;
                Vector3 side = Vector3.Cross(Vector3.up, velocity.normalized) * 0.15f;
                t.Velocity.SetPosition(0, start);
                t.Velocity.SetPosition(1, end);
                t.Velocity.SetPosition(2, end + back + side);
                t.Velocity.SetPosition(3, end);
                t.Velocity.SetPosition(4, end + back - side);
            }

            string status = alive && SandboxSettings.ShowStatus ? StatusText(blade) : null;
            t.Status.enabled = status != null && cam != null;
            if (t.Status.enabled)
            {
                t.Status.text = status;
                PlaceOnScreen(t.Status.rectTransform, cam, blade.Position + Vector3.down * 0.3f, Vector2.zero);
            }
        }

        private static string StatusText(FakeBladeController blade)
        {
            StatusEffectSystem s = blade.Status;
            if (s.HasStatus) return $"{StatusName(s.Current)} {Seconds(s.RemainingTime)}";
            if (blade.IsInvulnerable) return Loc.Get("STATUS_INVULNERABLE");
            return null;
        }
        #endregion

        #region Números de daño
        private void SpawnNumber(Vector3 position, float amount)
        {
            if (!SandboxSettings.ShowDamageNumbers) return;

            Number n = _numbers[_nextNumber];
            _nextNumber = (_nextNumber + 1) % NumberPool;
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * 0.3f;
            n.World = position + new Vector3(jitter.x, 1.1f, jitter.y);
            n.Age = 0f;
            n.Text.text = "-" + Mathf.RoundToInt(amount);
            n.Text.color = amount >= 30f ? _theme.healthLow : amount >= 10f ? _theme.healthMid : Color.white;
            n.Text.enabled = true;
        }

        private void UpdateNumbers(Camera cam)
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < NumberPool; i++)
            {
                Number n = _numbers[i];
                if (!n.Text.enabled) continue;
                n.Age += dt;
                if (n.Age >= NumberLife || cam == null)
                {
                    n.Text.enabled = false;
                    continue;
                }
                float t = n.Age / NumberLife;
                PlaceOnScreen(n.Text.rectTransform, cam, n.World, new Vector2(0f, t * 16f * _px));
                Color c = n.Text.color;
                c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                n.Text.color = c;
            }
        }
        #endregion

        #region Registro
        private void HandleParry(FakeBladeController parrier, FakeBladeController attacker, bool both)
        {
            string a = Name(parrier.GetComponent<PlayerController>());
            string b = Name(attacker.GetComponent<PlayerController>());
            Log(both ? Loc.Format("LOG_DOUBLE_PARRY", a, b) : Loc.Format("LOG_PARRY", a, b));
        }

        /// <summary>Cada choque llega dos veces (una por peonza) en el mismo frame: se juntan en una línea.</summary>
        private void LogClash(PlayerController player, FakeBladeController other, float taken)
        {
            PlayerController otherPlayer = other != null ? other.GetComponent<PlayerController>() : null;
            if (_clashFrame == Time.frameCount && _log.Count > 0 &&
                ((_clashA == otherPlayer && _clashB == player) || (_clashA == player && _clashB == otherPlayer)))
            {
                if (_clashA == player) _clashDamageA = taken; else _clashDamageB = taken;
                _log[_log.Count - 1] = new LogEntry { Text = ClashText(), Time = Time.unscaledTime };
                return;
            }

            _clashFrame = Time.frameCount;
            _clashA = player;
            _clashB = otherPlayer;
            _clashDamageA = taken;
            _clashDamageB = 0f;
            Log(ClashText());
        }

        private string ClashText() => string.Format(Loc.Get("LOG_CLASH"), Name(_clashA), Mathf.RoundToInt(_clashDamageA),
            Name(_clashB), Mathf.RoundToInt(_clashDamageB));

        private void Log(string text)
        {
            if (!SandboxSettings.ShowEventLog) return;
            _log.Add(new LogEntry { Text = text, Time = Time.unscaledTime });
            if (_log.Count > LogLines) _log.RemoveAt(0);
        }

        private void UpdateLog()
        {
            bool show = SandboxSettings.ShowEventLog;
            float now = Time.unscaledTime;
            while (_log.Count > 0 && now - _log[0].Time > LogLife) _log.RemoveAt(0);

            _logBackground.enabled = show && _log.Count > 0;

            // La más nueva en la última línea (abajo)
            int offset = LogLines - _log.Count;
            for (int i = 0; i < LogLines; i++)
            {
                TextMeshProUGUI line = _logLines[i];
                int index = i - offset;
                bool visible = show && index >= 0 && index < _log.Count;
                line.enabled = visible;
                if (!visible) continue;
                LogEntry entry = _log[index];
                line.text = entry.Text;
                float left = LogLife - (now - entry.Time);
                line.alpha = Mathf.Clamp01(left); // se desvanece en el último segundo
            }
        }
        #endregion

        #region Helpers
        private void PlaceOnScreen(RectTransform rt, Camera cam, Vector3 world, Vector2 offset)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z < 0f) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = local + offset;
        }

        /// <summary>"J1" con el color del jugador.</summary>
        private static string Name(PlayerController player)
        {
            if (player == null) return "?";
            return $"<color=#{ColorUtility.ToHtmlStringRGB(player.PlayerColor)}>{Loc.Format("PLAYER_BADGE", player.PlayerID + 1)}</color>";
        }

        private static string StatusName(StatusEffectType status)
        {
            switch (status)
            {
                case StatusEffectType.Burning: return Loc.Get("STATUS_BURNING");
                case StatusEffectType.Frozen: return Loc.Get("STATUS_FROZEN");
                case StatusEffectType.Launched: return Loc.Get("STATUS_LAUNCHED");
                default: return "";
            }
        }

        private static string Seconds(float seconds)
        {
            string number = seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            if (Loc.Current != Language.English) number = number.Replace('.', ',');
            return number + " S";
        }
        #endregion
    }
}
