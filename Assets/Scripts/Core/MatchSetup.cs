using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>Quién controla una peonza.</summary>
    public enum ControllerType
    {
        Human = 0,
        /// <summary>Peonza de práctica que no se mueve.</summary>
        Dummy = 1,
        /// <summary>Reservado para rivales con IA (GDD 10).</summary>
        AI = 2
    }

    /// <summary>Configuración de un jugador elegida en la selección de peonzas.</summary>
    public sealed class PlayerSetup
    {
        public int PlayerIndex;
        public ControllerType Controller = ControllerType.Human;
        public InputDeviceKind Device = InputDeviceKind.KeyboardLeft;
        /// <summary>InputDevice.deviceId del mando (solo si Device = Gamepad).</summary>
        public int GamepadDeviceId;

        /// <summary>Índice del preset en el catálogo, o -1 si está personalizada.</summary>
        public int PresetIndex;
        public FakeBladeComponentData Tip;
        public FakeBladeComponentData Body;
        public FakeBladeComponentData Blade;
        public FakeBladeComponentData Core;

        public int ColorIndex;
        public Color Color = Color.white;
        public int Team;

        public FakeBladeComponentData GetPart(ComponentSlot slot)
        {
            switch (slot)
            {
                case ComponentSlot.Tip: return Tip;
                case ComponentSlot.Body: return Body;
                case ComponentSlot.Blade: return Blade;
                default: return Core;
            }
        }

        public void SetPart(ComponentSlot slot, FakeBladeComponentData part)
        {
            switch (slot)
            {
                case ComponentSlot.Tip: Tip = part; break;
                case ComponentSlot.Body: Body = part; break;
                case ComponentSlot.Blade: Blade = part; break;
                default: Core = part; break;
            }
        }

        public void ApplyPreset(BladePreset preset)
        {
            if (preset == null) return;
            Tip = preset.tip;
            Body = preset.body;
            Blade = preset.blade;
            Core = preset.core;
        }

        public void EquipOn(FakeBladeStats stats)
        {
            if (stats == null) return;
            if (Tip != null) stats.EquipComponent(Tip);
            if (Body != null) stats.EquipComponent(Body);
            if (Blade != null) stats.EquipComponent(Blade);
            if (Core != null) stats.EquipComponent(Core);
        }
    }

    /// <summary>
    /// Datos de la partida que viajan entre escenas (selección → batalla → selección).
    /// Estático a propósito: vive mientras la aplicación esté abierta.
    /// </summary>
    public static class MatchSetup
    {
        public static readonly List<PlayerSetup> Players = new List<PlayerSetup>(4);
        public static MatchRules Rules;
        public static ArenaData Arena;

        public static bool HasPlayers => Players.Count > 0;
        public static bool IsSandbox => Rules != null && Rules.IsSandbox;

        public static void Clear()
        {
            Players.Clear();
            Rules = null;
            Arena = null;
        }

        /// <summary>
        /// Partida por defecto para abrir BattleArena directamente desde el editor:
        /// J1 teclado WASD y J2 primer mando (o teclado flechas), con los dos primeros presets.
        /// </summary>
        public static void CreateDefault(FakeBladeCatalog catalog)
        {
            Clear();
            for (int i = 0; i < 2; i++)
            {
                InputAssignment.GetDefault(i, out InputDeviceKind kind, out int padIndex);
                var setup = new PlayerSetup
                {
                    PlayerIndex = i,
                    Device = kind,
                    GamepadDeviceId = InputDeviceUtil.GamepadIdAt(padIndex),
                    PresetIndex = i,
                    ColorIndex = i,
                    Color = catalog.GetColor(i),
                    Team = i % 2
                };
                setup.ApplyPreset(catalog.GetPreset(i));
                Players.Add(setup);
            }

            Rules = catalog.lastStanding != null ? catalog.lastStanding.CloneRuntime() : MatchRules.CreateDefault();
            Arena = catalog.arenas.Count > 0 ? catalog.arenas[0] : null;
        }

        /// <summary>Sandbox por defecto para abrir la escena Sandbox directamente: J1 con el primer preset.</summary>
        public static void CreateDefaultSandbox(FakeBladeCatalog catalog)
        {
            CreateDefault(catalog);
            Players.RemoveRange(1, Players.Count - 1);

            Rules = catalog.sandbox != null ? catalog.sandbox.CloneRuntime() : MatchRules.CreateDefault();
            Rules.winCondition = WinCondition.Sandbox;
            Rules.displayNameKey = "MODE_SANDBOX";
            Rules.teams = false;
            Rules.timeLimit = 0f;
        }
    }
}
