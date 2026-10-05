using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace FakeBlade.Core
{
    /// <summary>Acciones reasignables (GDD 9.2.4).</summary>
    public enum BindingAction
    {
        Up, Down, Left, Right,
        Attack, AttackAlt,
        Dash, DashAlt,
        Special, SpecialAlt,
        /// <summary>Abre el panel del sandbox (GDD 6.3). Añadida al final: no cambia los valores guardados.</summary>
        SandboxPanel
    }

    /// <summary>Teclas de un esquema de teclado. Cada acción de combate admite una tecla alternativa.</summary>
    [Serializable]
    public class KeyboardScheme
    {
        public Key up = Key.W;
        public Key down = Key.S;
        public Key left = Key.A;
        public Key right = Key.D;
        public Key attack = Key.Space;
        public Key attackAlt = Key.None;
        public Key dash = Key.LeftShift;
        public Key dashAlt = Key.None;
        public Key special = Key.E;
        public Key specialAlt = Key.None;
        public Key sandboxPanel = Key.Tab;

        public static KeyboardScheme CreateLeft() => new KeyboardScheme();

        public static KeyboardScheme CreateRight() => new KeyboardScheme
        {
            up = Key.UpArrow,
            down = Key.DownArrow,
            left = Key.LeftArrow,
            right = Key.RightArrow,
            attack = Key.RightCtrl,
            attackAlt = Key.Numpad0,
            dash = Key.RightShift,
            dashAlt = Key.Numpad1,
            special = Key.Enter,
            specialAlt = Key.Numpad2,
            sandboxPanel = Key.None // el mismo teclado: Tab ya lo tiene J1
        };

        public Key Get(BindingAction action)
        {
            switch (action)
            {
                case BindingAction.Up: return up;
                case BindingAction.Down: return down;
                case BindingAction.Left: return left;
                case BindingAction.Right: return right;
                case BindingAction.Attack: return attack;
                case BindingAction.AttackAlt: return attackAlt;
                case BindingAction.Dash: return dash;
                case BindingAction.DashAlt: return dashAlt;
                case BindingAction.Special: return special;
                case BindingAction.SpecialAlt: return specialAlt;
                case BindingAction.SandboxPanel: return sandboxPanel;
                default: return Key.None;
            }
        }

        public void Set(BindingAction action, Key key)
        {
            switch (action)
            {
                case BindingAction.Up: up = key; break;
                case BindingAction.Down: down = key; break;
                case BindingAction.Left: left = key; break;
                case BindingAction.Right: right = key; break;
                case BindingAction.Attack: attack = key; break;
                case BindingAction.AttackAlt: attackAlt = key; break;
                case BindingAction.Dash: dash = key; break;
                case BindingAction.DashAlt: dashAlt = key; break;
                case BindingAction.Special: special = key; break;
                case BindingAction.SpecialAlt: specialAlt = key; break;
                case BindingAction.SandboxPanel: sandboxPanel = key; break;
            }
        }
    }

    /// <summary>Botones del mando (común a todos los mandos). El movimiento es siempre stick/cruceta.</summary>
    [Serializable]
    public class GamepadScheme
    {
        public GamepadButton attack = GamepadButton.South;
        public GamepadButton attackAlt = GamepadButton.RightShoulder;
        public GamepadButton dash = GamepadButton.East;
        public GamepadButton dashAlt = GamepadButton.RightTrigger;
        public GamepadButton special = GamepadButton.North;
        public GamepadButton specialAlt = GamepadButton.LeftTrigger;
        public GamepadButton sandboxPanel = GamepadButton.Select;

        public GamepadButton Get(BindingAction action)
        {
            switch (action)
            {
                case BindingAction.Attack: return attack;
                case BindingAction.AttackAlt: return attackAlt;
                case BindingAction.Dash: return dash;
                case BindingAction.DashAlt: return dashAlt;
                case BindingAction.Special: return special;
                case BindingAction.SandboxPanel: return sandboxPanel;
                default: return specialAlt;
            }
        }

        public void Set(BindingAction action, GamepadButton button)
        {
            switch (action)
            {
                case BindingAction.Attack: attack = button; break;
                case BindingAction.AttackAlt: attackAlt = button; break;
                case BindingAction.Dash: dash = button; break;
                case BindingAction.DashAlt: dashAlt = button; break;
                case BindingAction.Special: special = button; break;
                case BindingAction.SpecialAlt: specialAlt = button; break;
                case BindingAction.SandboxPanel: sandboxPanel = button; break;
            }
        }
    }

    /// <summary>
    /// Controles en uso (teclado J1, teclado J2 y mando), guardados en PlayerPrefs.
    /// Al asignar una tecla ya usada, se intercambia con la acción que la tenía (GDD 9.2.4).
    /// </summary>
    public static class InputBindings
    {
        private const string PrefsKey = "fakeblade.bindings";

        public static event Action OnChanged;

        /// <summary>Acciones reasignables en mando (el movimiento no se reasigna).</summary>
        public static readonly BindingAction[] GamepadActions =
        {
            BindingAction.Attack, BindingAction.AttackAlt,
            BindingAction.Dash, BindingAction.DashAlt,
            BindingAction.Special, BindingAction.SpecialAlt,
            BindingAction.SandboxPanel
        };

        /// <summary>Botones que se aceptan al reasignar un mando (sin Start, reservado para pausa).</summary>
        public static readonly GamepadButton[] AssignableButtons =
        {
            GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North,
            GamepadButton.LeftShoulder, GamepadButton.RightShoulder,
            GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
            GamepadButton.LeftStick, GamepadButton.RightStick, GamepadButton.Select
        };

        [Serializable]
        private class Data
        {
            public KeyboardScheme left = KeyboardScheme.CreateLeft();
            public KeyboardScheme right = KeyboardScheme.CreateRight();
            public GamepadScheme pad = new GamepadScheme();
        }

        private static Data _data;

        private static Data Current
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        public static KeyboardScheme Left => Current.left;
        public static KeyboardScheme Right => Current.right;
        public static GamepadScheme Pad => Current.pad;

        public static KeyboardScheme GetKeyboard(InputDeviceKind side) =>
            side == InputDeviceKind.KeyboardRight ? Current.right : Current.left;

        /// <summary>Asigna una tecla. Si ya la usaba otra acción (de cualquiera de los dos teclados), se intercambian.</summary>
        public static void SetKey(InputDeviceKind side, BindingAction action, Key key)
        {
            KeyboardScheme target = GetKeyboard(side);
            Key previous = target.Get(action);
            if (previous == key) return;

            if (key != Key.None)
            {
                SwapIfUsed(Current.left, key, previous, target, action);
                SwapIfUsed(Current.right, key, previous, target, action);
            }

            target.Set(action, key);
            Save();
        }

        public static void SetButton(BindingAction action, GamepadButton button)
        {
            GamepadScheme pad = Current.pad;
            GamepadButton previous = pad.Get(action);
            if (previous == button) return;

            for (int i = 0; i < GamepadActions.Length; i++)
            {
                BindingAction other = GamepadActions[i];
                if (other != action && pad.Get(other) == button)
                    pad.Set(other, previous);
            }

            pad.Set(action, button);
            Save();
        }

        public static void ResetDefaults()
        {
            _data = new Data();
            Save();
        }

        public static void Load()
        {
            _data = null;
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { _data = JsonUtility.FromJson<Data>(json); }
                catch (Exception e) { Debug.LogWarning($"[InputBindings] No se pudieron leer los controles: {e.Message}"); }
            }
            if (_data == null) _data = new Data();

            // Controles guardados antes de existir el panel del sandbox: los dos teclados heredan Tab
            if (_data.left.sandboxPanel != Key.None && _data.left.sandboxPanel == _data.right.sandboxPanel)
                _data.right.sandboxPanel = Key.None;
        }

        public static void Save()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Current));
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        private static void SwapIfUsed(KeyboardScheme scheme, Key key, Key replacement, KeyboardScheme target, BindingAction targetAction)
        {
            foreach (BindingAction action in (BindingAction[])Enum.GetValues(typeof(BindingAction)))
            {
                if (scheme == target && action == targetAction) continue;
                if (scheme.Get(action) == key) scheme.Set(action, replacement);
            }
        }

        #region Display names
        public static string KeyName(Key key)
        {
            if (key == Key.None) return "—";
            var kb = Keyboard.current;
            if (kb != null)
            {
                string display = kb[key].displayName;
                if (!string.IsNullOrEmpty(display)) return display.ToUpperInvariant();
            }
            return key.ToString().ToUpperInvariant();
        }

        public static string ButtonName(GamepadButton button)
        {
            switch (button)
            {
                case GamepadButton.South: return "A";
                case GamepadButton.East: return "B";
                case GamepadButton.West: return "X";
                case GamepadButton.North: return "Y";
                case GamepadButton.LeftShoulder: return "LB";
                case GamepadButton.RightShoulder: return "RB";
                case GamepadButton.LeftTrigger: return "LT";
                case GamepadButton.RightTrigger: return "RT";
                case GamepadButton.LeftStick: return "LS";
                case GamepadButton.RightStick: return "RS";
                case GamepadButton.Select: return "SELECT";
                case GamepadButton.Start: return "START";
                default: return button.ToString().ToUpperInvariant();
            }
        }
        #endregion
    }
}
