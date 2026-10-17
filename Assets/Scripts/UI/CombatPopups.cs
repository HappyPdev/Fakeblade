using System;
using System.Collections.Generic;
using FakeBlade.Core;
using TMPro;
using UnityEngine;

namespace FakeBlade.UI
{
    /// <summary>
    /// Números de daño en la arena (GDD 9.1, 2026-10-16/17), dentro del HUD de combate: lo que pierde cada peonza
    /// (1 RPM o más), en blanco, amarillento desde 10 y rojizo desde 30, con un **contorno fino del color de esa
    /// peonza** (sin recuadro). Al salir laten con un rebote (más fuerte y con destello en los de 30 o más), suben un
    /// poco y se desvanecen. Se apagan en Opciones → Números de daño.
    ///
    /// El parry y el bloqueo ya no tienen cartel: se ven con su impacto (VfxSystem.PlayImpact).
    /// Todo sale de una reserva creada al principio: sin instanciar ni GC en combate (los números con SetText).
    /// </summary>
    public sealed class CombatPopups : MonoBehaviour
    {
        private const int NumberPool = 24;
        private const float NumberLife = 0.95f;
        private const float MinDamage = 1f; // el roce continuo no llena la pantalla de números
        private const float BigDamage = 30f;
        private const float MidDamage = 10f;
        private const float RisePx = 14f;
        private const float OutlineWidth = 0.22f;

        private sealed class Popup
        {
            public RectTransform Root;
            public TextMeshProUGUI Text;
            public Vector3 World;
            public float Age;
            public Color TextColor;
            public bool Big;
            public bool Active;
        }

        private sealed class Tracked
        {
            public FakeBladeController Blade;
            public Action<float, FakeBladeController> OnDamaged;
        }

        private HUDTheme _theme;
        private int _px;
        private RectTransform _root;
        private readonly Popup[] _numbers = new Popup[NumberPool];
        private int _nextNumber;
        private readonly List<Tracked> _tracked = new List<Tracked>(4);
        private Camera _camera;

        public static CombatPopups Create(Transform parent, HUDTheme theme)
        {
            RectTransform root = PixelUI.CreateRect("Popups", parent);
            PixelUI.Stretch(root, 0, 1);
            var popups = root.gameObject.AddComponent<CombatPopups>();
            popups.Build(theme, root);
            return popups;
        }

        private void Build(HUDTheme theme, RectTransform root)
        {
            _theme = theme;
            _px = Mathf.Max(1, theme.pixelSize);
            _root = root;
            for (int i = 0; i < NumberPool; i++)
            {
                RectTransform rt = PixelUI.CreateRect("Damage", _root);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(30 * _px, 12 * _px);
                TextMeshProUGUI text = PixelUI.CreateText("Text", rt, theme, 7f * _px, TextAlignmentOptions.Center, Color.white);
                PixelUI.Stretch(text.rectTransform, 0, _px);
                // Contorno fino (crea el material propio de este texto una sola vez, aquí)
                text.outlineWidth = OutlineWidth;
                rt.gameObject.SetActive(false);
                _numbers[i] = new Popup { Root = rt, Text = text };
            }
        }

        private void OnDestroy() => Untrack();

        #region Peonzas
        /// <summary>Sigue el daño de las peonzas de la partida (al prepararla).</summary>
        public void Track(IReadOnlyList<PlayerController> players)
        {
            Untrack();
            for (int i = 0; i < players.Count; i++) Add(players[i]);
            for (int i = 0; i < NumberPool; i++) Hide(_numbers[i]);
        }

        /// <summary>Peonza que entra con la partida empezada (dummies del Sandbox).</summary>
        public void Add(PlayerController player)
        {
            if (player == null || player.Blade == null) return;
            FakeBladeController blade = player.Blade;
            for (int i = 0; i < _tracked.Count; i++)
                if (_tracked[i].Blade == blade) return;
            var t = new Tracked { Blade = blade };
            t.OnDamaged = (amount, source) => ShowDamage(blade, player.PlayerColor, amount);
            blade.OnDamaged += t.OnDamaged;
            _tracked.Add(t);
        }

        public void Remove(PlayerController player)
        {
            if (player == null) return;
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                if (_tracked[i].Blade != player.Blade) continue;
                if (_tracked[i].Blade != null) _tracked[i].Blade.OnDamaged -= _tracked[i].OnDamaged;
                _tracked.RemoveAt(i);
            }
        }

        private void Untrack()
        {
            for (int i = 0; i < _tracked.Count; i++)
                if (_tracked[i].Blade != null) _tracked[i].Blade.OnDamaged -= _tracked[i].OnDamaged;
            _tracked.Clear();
        }
        #endregion

        #region Números
        private void ShowDamage(FakeBladeController blade, Color playerColor, float amount)
        {
            if (amount < MinDamage || !SettingsService.Current.damageNumbers) return;

            Popup p = _numbers[_nextNumber];
            _nextNumber = (_nextNumber + 1) % NumberPool;
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * 0.3f;
            p.World = blade.Position + new Vector3(jitter.x, 1.1f, jitter.y);
            p.Age = 0f;
            p.Big = amount >= BigDamage;
            p.TextColor = p.Big ? _theme.healthLow : amount >= MidDamage ? _theme.healthMid : Color.white;
            p.Text.SetText("-{0:0}", amount);
            p.Text.outlineColor = playerColor;
            p.Active = true;
            p.Root.gameObject.SetActive(true);
            Animate(p);
        }

        private static void Hide(Popup p)
        {
            p.Active = false;
            p.Root.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < NumberPool; i++)
            {
                Popup p = _numbers[i];
                if (!p.Active) continue;
                p.Age += dt;
                if (p.Age >= NumberLife || _camera == null) Hide(p);
                else Animate(p);
            }
        }

        /// <summary>
        /// Late y rebota: sale pequeño, se pasa de tamaño y oscila hasta quedarse quieto (muelle amortiguado). Los de 30
        /// o más, más grandes, con un rebote más fuerte y un destello blanco al salir. Después suben y se desvanecen.
        /// </summary>
        private void Animate(Popup p)
        {
            float t = p.Age / NumberLife;
            float amplitude = p.Big ? 0.85f : 0.5f;
            float frequency = p.Big ? 30f : 26f;
            float baseScale = p.Big ? 1.3f : 1f;
            float scale = baseScale * (1f - amplitude * Mathf.Exp(-7f * p.Age) * Mathf.Cos(frequency * p.Age));
            p.Root.localScale = new Vector3(scale, scale, 1f);

            if (_camera != null)
            {
                Vector3 screen = _camera.WorldToScreenPoint(p.World);
                if (screen.z > 0f)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
                    // Subida en píxeles de UI enteros, para no romper la rejilla pixel; los grandes tiemblan al salir
                    float shake = p.Big && p.Age < 0.18f ? Mathf.Sin(p.Age * 90f) * 1.5f : 0f;
                    p.Root.anchoredPosition = local + new Vector2(Mathf.Round(shake) * _px, Mathf.Round(t * RisePx) * _px);
                }
            }

            Color color = p.Big && p.Age < 0.07f ? Color.white : p.TextColor;
            color.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            p.Text.color = color;
        }
        #endregion
    }
}
