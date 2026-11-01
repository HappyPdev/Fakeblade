using System;
using System.Collections.Generic;
using FakeBlade.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// «Cambiar peonza» en la pausa (GDD 6.7, P8; después también en el Sandbox): una columna de la selección de peonzas
    /// por jugador, en fila, para cambiar el preset, cada pieza y el color. Cada jugador maneja la suya con su mando o
    /// su teclado (como en la selección) y confirma con Ataque; con todos listos se aplica a sus peonzas y se vuelve a
    /// la pausa. Atrás de cualquiera que no esté listo cancela sin cambiar nada. Con el ratón: flechas de cada fila,
    /// Confirmar de cada columna, y Aplicar o Volver arriba.
    ///
    /// Se edita una copia del PlayerSetup de cada jugador; al aplicar se copia al de MatchSetup (para que se mantenga al
    /// reiniciar) y se monta en la peonza. Mientras está abierta bloquea la pausa del GameManager (MenuStack).
    /// </summary>
    public class ChangeBladeScreen : MonoBehaviour
    {
        private const int MaxColumns = 4;
        /// <summary>Ancho de pantalla de cada columna: como en la selección, como mucho la mitad, y entre todas el 96%.</summary>
        private const float MaxColumnWidth = 0.48f;
        private const float ColumnsWidth = 0.96f;
        /// <summary>Con todos listos, espera esto antes de aplicar (para ver el «¡LISTO!»).</summary>
        private const float ApplyDelay = 0.3f;
        /// <summary>Las vistas previas van después de las de la selección (plazas 0-3), por si conviven.</summary>
        private const int StageOffset = 4;

        private sealed class Entry
        {
            public PlayerController Player;
            /// <summary>El de MatchSetup: se escribe al aplicar.</summary>
            public PlayerSetup Target;
            /// <summary>La copia que se edita.</summary>
            public PlayerSetup Edit;
            public DeviceMenuInput Input;
            public LobbyColumn Column;
            public BladePreviewStage Stage;
            public bool Ready;
        }

        private HUDTheme _theme;
        private FakeBladeCatalog _catalog;
        private BladeSetupEditor _editor;
        private int _px;
        private Action _onClose;
        private Action _onApplied;

        private TextMeshProUGUI _title;
        private PixelOptionRow _backButton;
        private PixelOptionRow _applyButton;
        private RectTransform _area;
        private readonly List<LobbyColumn> _columns = new List<LobbyColumn>(MaxColumns);
        private readonly BladePreviewStage[] _stages = new BladePreviewStage[MaxColumns];
        private readonly List<Entry> _entries = new List<Entry>(MaxColumns);

        private bool _isOpen;
        private int _openedFrame = -1;
        private float _allReadyTimer;

        public bool IsOpen => _isOpen;

        #region Creación
        public static ChangeBladeScreen Create(Transform parent, HUDTheme theme, FakeBladeCatalog catalog, Action onClose, Action onApplied)
        {
            RectTransform root = PixelUI.CreateRect("ChangeBladeScreen", parent);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            // Por encima de las ventanas de la pausa
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 510;
            root.gameObject.AddComponent<GraphicRaycaster>();

            var screen = root.gameObject.AddComponent<ChangeBladeScreen>();
            screen.Build(theme, catalog, onClose, onApplied);
            return screen;
        }

        private void Build(HUDTheme theme, FakeBladeCatalog catalog, Action onClose, Action onApplied)
        {
            _theme = theme;
            _catalog = catalog;
            _editor = new BladeSetupEditor(catalog, theme);
            _px = Mathf.Max(1, theme.pixelSize);
            _onClose = onClose;
            _onApplied = onApplied;

            // Fondo opaco, como el de la selección: tapa el HUD, que se pisaría con Volver y Aplicar
            Color background = theme.barBackground;
            background.a = 1f;
            var bg = PixelUI.CreateImage("Background", transform, background);
            bg.raycastTarget = true;
            PixelUI.Stretch(bg.rectTransform, 0, _px);

            _title = PixelUI.CreateShadowedTitle("Title", transform, theme, 12 * _px, theme.textColor);
            var titleRt = (RectTransform)_title.transform.parent;
            titleRt.anchorMin = new Vector2(0.2f, 1f);
            titleRt.anchorMax = new Vector2(0.8f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -3 * _px);
            titleRt.sizeDelta = new Vector2(0f, 16 * _px);

            _backButton = CreateTopButton("BackHolder", new Vector2(0f, 1f), new Vector2(6 * _px, -5 * _px), Cancel);
            // A la izquierda del contador de FPS (Opciones), que va en la esquina
            _applyButton = CreateTopButton("ApplyHolder", new Vector2(1f, 1f), new Vector2(-34 * _px, -5 * _px), Apply);

            _area = PixelUI.CreateRect("Columns", transform);
            _area.offsetMin = new Vector2(0f, 5 * _px);
            _area.offsetMax = new Vector2(0f, -22 * _px);
            var layout = _area.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3 * _px;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            RefreshTexts();
            Loc.OnLanguageChanged += HandleLanguageChanged;
            gameObject.SetActive(false);
        }

        /// <summary>Botón de ratón de la barra de arriba (Volver o Aplicar), fuera del EventSystem (PixelRowClick).</summary>
        private PixelOptionRow CreateTopButton(string name, Vector2 corner, Vector2 offset, Action onSubmit)
        {
            RectTransform holder = PixelUI.CreateRect(name, transform);
            holder.anchorMin = holder.anchorMax = holder.pivot = corner;
            holder.anchoredPosition = offset;
            holder.sizeDelta = new Vector2(44 * _px, 10 * _px);
            PixelOptionRow button = PixelOptionRow.Create(holder, _theme, 10, true, 5f);
            PixelUI.Stretch((RectTransform)button.transform, 0, _px);
            button.gameObject.AddComponent<PixelRowClick>();
            button.onSubmit = onSubmit;
            return button;
        }

        private LobbyColumn GetColumn(int index)
        {
            while (_columns.Count <= index)
            {
                LobbyColumn column = LobbyColumn.Create(_area, _theme);
                // Sin equipo: aquí solo se cambia la peonza
                column.TeamRow.gameObject.SetActive(false);
                // El ratón confirma con un clic en Confirmar (el foco lo lleva cada jugador)
                column.ConfirmRow.gameObject.AddComponent<PixelRowClick>().Highlight = false;
                _columns.Add(column);
            }
            return _columns[index];
        }

        private BladePreviewStage GetStage(int index)
        {
            if (_stages[index] == null)
                _stages[index] = BladePreviewStage.Create(StageOffset + index, _catalog.playerPrefab, _theme.barBackground);
            return _stages[index];
        }

        private void OnDestroy()
        {
            Loc.OnLanguageChanged -= HandleLanguageChanged;
            if (_isOpen) MenuStack.Pop();
            for (int i = 0; i < _stages.Length; i++)
                if (_stages[i] != null) Destroy(_stages[i].gameObject);
        }
        #endregion

        #region Abrir y cerrar
        /// <summary>Abre con una columna por cada jugador humano de la partida (los que tienen PlayerSetup).</summary>
        public void Open(IReadOnlyList<PlayerController> players)
        {
            if (_isOpen) return;
            _entries.Clear();
            for (int i = 0; i < players.Count && _entries.Count < MaxColumns; i++)
            {
                PlayerController player = players[i];
                PlayerSetup target = player != null ? FindHumanSetup(player.PlayerID) : null;
                if (target == null) continue;
                _entries.Add(new Entry
                {
                    Player = player,
                    Target = target,
                    Edit = Copy(target),
                    Input = new DeviceMenuInput(target.Device, target.GamepadDeviceId)
                });
            }
            _entries.Sort((a, b) => a.Player.PlayerID.CompareTo(b.Player.PlayerID));
            if (_entries.Count == 0) return;

            _isOpen = true;
            _openedFrame = Time.frameCount;
            _allReadyTimer = 0f;
            MenuStack.Push();
            gameObject.SetActive(true);
            // Que el Confirmar de los mandos no pulse también el botón de la pausa que tenía el foco
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            // Las columnas, centradas (los nombres largos, como PERSONALIZADA, necesitan el ancho de la selección)
            float half = _entries.Count * Mathf.Min(MaxColumnWidth, ColumnsWidth / _entries.Count) * 0.5f;
            _area.anchorMin = new Vector2(0.5f - half, 0f);
            _area.anchorMax = new Vector2(0.5f + half, 1f);

            for (int i = 0; i < MaxColumns; i++)
            {
                bool used = i < _entries.Count;
                if (used || i < _columns.Count) GetColumn(i).gameObject.SetActive(used);
                if (!used && _stages[i] != null) _stages[i].SetVisible(false);
            }
            for (int i = 0; i < _entries.Count; i++) Bind(_entries[i], i);
        }

        private void Bind(Entry entry, int index)
        {
            entry.Column = GetColumn(index);
            entry.Stage = GetStage(index);
            entry.Stage.SetVisible(true);
            entry.Ready = false;

            LobbyColumn column = entry.Column;
            PlayerSetup s = entry.Edit;
            column.ShowPlayer(entry.Player.PlayerID + 1, s.Color, InputDeviceUtil.DeviceLabel(s.Device, s.GamepadDeviceId));
            column.SetPreview(entry.Stage.Texture);
            column.SetFocusVisible(true);
            column.ResetFocus();
            column.SetReady(false);
            column.SetLeaveProgress(0f);
            column.SetHint(Loc.Get("CHANGE_BLADE_HINT"));

            // Las flechas (ratón) y las direcciones del mando pasan por el mismo sitio: PixelOptionRow.Step → onStep
            WireRow(entry, column.PresetRow);
            WireRow(entry, column.TipRow);
            WireRow(entry, column.BodyRow);
            WireRow(entry, column.BladeRow);
            WireRow(entry, column.CoreRow);
            WireRow(entry, column.ColorRow);
            column.ConfirmRow.onSubmit = () => SetReady(entry, !entry.Ready);

            Refresh(entry);
        }

        private void WireRow(Entry entry, PixelOptionRow row) => row.onStep = dir => StepRow(entry, row, dir);

        /// <summary>Cierra sin aplicar nada.</summary>
        public void Cancel()
        {
            if (!_isOpen) return;
            Dismiss();
            _onClose?.Invoke();
        }

        /// <summary>Cierra sin avisar al dueño (al reiniciar o salir de la partida desde fuera).</summary>
        public void Dismiss()
        {
            if (!_isOpen) return;
            _isOpen = false;
            MenuStack.Pop();
            for (int i = 0; i < _stages.Length; i++)
                if (_stages[i] != null) _stages[i].SetVisible(false);
            gameObject.SetActive(false);
        }
        #endregion

        #region Entrada
        private void Update()
        {
            // El Confirmar que abrió la ventana no cuenta como «listo»
            if (!_isOpen || Time.frameCount == _openedFrame) return;
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                DeviceMenuInput input = entry.Input;
                input.Update(dt);

                if (entry.Ready)
                {
                    if (input.Back) SetReady(entry, false);
                    continue;
                }

                if (input.Back)
                {
                    Cancel();
                    return;
                }

                LobbyColumn column = entry.Column;
                if (input.Up) column.MoveFocus(-1);
                if (input.Down) column.MoveFocus(1);
                if (input.Left) column.FocusedRow.Step(-1);
                if (input.Right) column.FocusedRow.Step(1);
                if (input.Confirm) SetReady(entry, true);
            }

            bool allReady = _entries.Count > 0;
            for (int i = 0; i < _entries.Count; i++) allReady &= _entries[i].Ready;
            if (!allReady)
            {
                _allReadyTimer = 0f;
                return;
            }
            _allReadyTimer += dt;
            if (_allReadyTimer >= ApplyDelay) Apply();
        }

        private void SetReady(Entry entry, bool ready)
        {
            entry.Ready = ready;
            entry.Column.SetReady(ready);
        }

        private void StepRow(Entry entry, PixelOptionRow row, int dir)
        {
            if (entry.Ready) return;
            LobbyColumn column = entry.Column;
            PlayerSetup s = entry.Edit;

            if (row == column.PresetRow) _editor.StepPreset(s, dir);
            else if (row == column.TipRow) _editor.StepPart(s, ComponentSlot.Tip, dir);
            else if (row == column.BodyRow) _editor.StepPart(s, ComponentSlot.Body, dir);
            else if (row == column.BladeRow) _editor.StepPart(s, ComponentSlot.Blade, dir);
            else if (row == column.CoreRow) _editor.StepPart(s, ComponentSlot.Core, dir);
            else if (row == column.ColorRow) _editor.StepColor(s, dir, c => IsColorUsed(c, entry));
            else return;

            Refresh(entry);
        }

        private bool IsColorUsed(int colorIndex, Entry except)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i] != except && _entries[i].Edit.ColorIndex == colorIndex) return true;
            return false;
        }

        private void Refresh(Entry entry) => _editor.Refresh(entry.Column, entry.Edit, entry.Stage, false);
        #endregion

        #region Aplicar
        /// <summary>Monta en cada peonza lo elegido (si ha cambiado) y lo guarda en MatchSetup.</summary>
        public void Apply()
        {
            if (!_isOpen) return;
            CombatHUDManager hud = CombatHUDManager.Instance;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                PlayerSetup edit = entry.Edit, target = entry.Target;
                PlayerController player = entry.Player;
                bool partsChanged = edit.Tip != target.Tip || edit.Body != target.Body || edit.Blade != target.Blade ||
                                    edit.Core != target.Core;
                bool colorChanged = edit.ColorIndex != target.ColorIndex;

                target.Tip = edit.Tip;
                target.Body = edit.Body;
                target.Blade = edit.Blade;
                target.Core = edit.Core;
                target.PresetIndex = edit.PresetIndex;
                target.ColorIndex = edit.ColorIndex;
                target.Color = edit.Color;
                if (player == null) continue;

                if (partsChanged)
                {
                    target.EquipOn(player.Stats);
                    // Empieza con las RPM llenas de su peonza nueva
                    FakeBladeController blade = player.Blade;
                    blade.AddSpin(blade.MaxSpinSpeed - blade.CurrentSpinSpeed);
                }
                if (colorChanged)
                {
                    player.SetPlayerColor(target.Color);
                    player.SetColorScheme(BladeColors.Get(_catalog, target.ColorIndex));
                    if (hud != null) hud.RefreshPanel(player);
                }
            }

            Dismiss();
            _onApplied?.Invoke();
            _onClose?.Invoke();
        }

        private static PlayerSetup FindHumanSetup(int playerId)
        {
            for (int i = 0; i < MatchSetup.Players.Count; i++)
            {
                PlayerSetup setup = MatchSetup.Players[i];
                if (setup.PlayerIndex == playerId && !setup.IsCpu) return setup;
            }
            return null;
        }

        private static PlayerSetup Copy(PlayerSetup s) => new PlayerSetup
        {
            PlayerIndex = s.PlayerIndex,
            Controller = s.Controller,
            Device = s.Device,
            GamepadDeviceId = s.GamepadDeviceId,
            PresetIndex = s.PresetIndex,
            Tip = s.Tip,
            Body = s.Body,
            Blade = s.Blade,
            Core = s.Core,
            ColorIndex = s.ColorIndex,
            Color = s.Color,
            Team = s.Team,
            Difficulty = s.Difficulty,
            CpuNumber = s.CpuNumber
        };
        #endregion

        #region Textos
        private void HandleLanguageChanged()
        {
            RefreshTexts();
            if (!_isOpen) return;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                bool ready = entry.Ready;
                Bind(entry, i);
                SetReady(entry, ready);
            }
        }

        private void RefreshTexts()
        {
            _title.text = Loc.Get("CHANGE_BLADE");
            _backButton.SetLabel("< " + Loc.Get("BACK"));
            _applyButton.SetLabel(Loc.Get("APPLY") + " >");
        }
        #endregion
    }
}
