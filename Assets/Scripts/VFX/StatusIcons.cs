using FakeBlade.UI;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Iconos pixel de los estados alterados (GDD 5), generados por código: blanco (se tiñe con el
    /// color del estado) con contorno negro para que se lean sobre cualquier fondo.
    /// </summary>
    public static class StatusIcons
    {
        /// <summary>Píxeles del icono por metro: 12 píxeles ≈ 0,85 m (se lee con la cámara de juego).</summary>
        private const float PixelsPerUnit = 14f;

        private static Sprite s_burn;
        private static Sprite s_freeze;
        private static Sprite s_launch;

        public static Sprite Get(StatusEffectType status)
        {
            switch (status)
            {
                case StatusEffectType.Burning:
                    return s_burn != null ? s_burn : s_burn = PixelUI.FromPattern("Status_Burn", Flame, PixelsPerUnit);
                case StatusEffectType.Frozen:
                    return s_freeze != null ? s_freeze : s_freeze = PixelUI.FromPattern("Status_Freeze", Snowflake, PixelsPerUnit);
                case StatusEffectType.Launched:
                    return s_launch != null ? s_launch : s_launch = PixelUI.FromPattern("Status_Launch", Bolt, PixelsPerUnit);
                default:
                    return null;
            }
        }

        private static readonly string[] Flame =
        {
            "     o      ",
            "    o#o     ",
            "    o##o    ",
            "   o###o o  ",
            "   o###oo#o ",
            "  o#######o ",
            " o########o ",
            " o###oo###o ",
            " o##o  o##o ",
            " o##o  o##o ",
            "  o##oo##o  ",
            "   oooooo   ",
        };

        private static readonly string[] Snowflake =
        {
            "     o     ",
            "  o o#o o  ",
            " o#oo#oo#o ",
            "  o#o#o#o  ",
            "oooo###oooo",
            "o#########o",
            "oooo###oooo",
            "  o#o#o#o  ",
            " o#oo#oo#o ",
            "  o o#o o  ",
            "     o     ",
        };

        private static readonly string[] Bolt =
        {
            "   oooooo  ",
            "   o####o  ",
            "  o####o   ",
            "  o###o    ",
            " o###ooooo ",
            " o#######o ",
            " ooooo###o ",
            "    o###o  ",
            "   o###o   ",
            "   o##o    ",
            "  o#o      ",
            "  oo       ",
        };
    }
}
