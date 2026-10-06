using System.Collections.Generic;
using FakeBlade.UI;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Icono pixel de cada poder (imagen del núcleo, GDD 3): blanco (se tiñe con el color del poder)
    /// con contorno negro. Si el poder tiene su propio icono en el asset (SpecialAbilityData.icon), se usa ese.
    /// Fuego, Hielo y Rayos comparten el icono de su estado alterado.
    /// </summary>
    public static class PowerIcons
    {
        private const float PixelsPerUnit = 14f;
        private static readonly Dictionary<SpecialAbilityType, Sprite> s_cache = new Dictionary<SpecialAbilityType, Sprite>();

        public static Sprite Get(SpecialAbilityType type)
        {
            SpecialAbilityData data = SpecialAbilities.Get(type);
            if (data != null && data.icon != null) return data.icon;

            switch (type)
            {
                case SpecialAbilityType.Fire: return StatusIcons.Get(StatusEffectType.Burning);
                case SpecialAbilityType.Ice: return StatusIcons.Get(StatusEffectType.Frozen);
                case SpecialAbilityType.Lightning: return StatusIcons.Get(StatusEffectType.Launched);
            }

            if (s_cache.TryGetValue(type, out Sprite sprite) && sprite != null) return sprite;
            string[] pattern;
            switch (type)
            {
                case SpecialAbilityType.ShockWave: pattern = Wave; break;
                case SpecialAbilityType.Defense: pattern = Shield; break;
                case SpecialAbilityType.Ghost: pattern = Ghost; break;
                default: pattern = Boost; break;
            }
            sprite = PixelUI.FromPattern("Power_" + type, pattern, PixelsPerUnit);
            s_cache[type] = sprite;
            return sprite;
        }

        // Spin Boost: flecha hacia arriba (recupera RPM)
        private static readonly string[] Boost =
        {
            "     oo     ",
            "    o##o    ",
            "   o####o   ",
            "  o######o  ",
            " o########o ",
            " oooo##oooo ",
            "    o##o    ",
            "    o##o    ",
            "    o##o    ",
            "    o##o    ",
            "    o##o    ",
            "    oooo    ",
        };

        // Onda de choque: anillos concéntricos
        private static readonly string[] Wave =
        {
            "  oooooooo  ",
            " o########o ",
            "o##oooooo##o",
            "o#o      o#o",
            "o#o oooo o#o",
            "o#o o##o o#o",
            "o#o o##o o#o",
            "o#o oooo o#o",
            "o#o      o#o",
            "o##oooooo##o",
            " o########o ",
            "  oooooooo  ",
        };

        // Defensa: escudo
        private static readonly string[] Shield =
        {
            " oooooooooo ",
            " o########o ",
            " o###oo###o ",
            " o###oo###o ",
            " o#oooooo#o ",
            " o###oo###o ",
            "  o##oo##o  ",
            "  o######o  ",
            "   o####o   ",
            "    o##o    ",
            "     oo     ",
        };

        // Fantasma
        private static readonly string[] Ghost =
        {
            "   oooooo   ",
            "  o######o  ",
            " o########o ",
            " o#oo##oo#o ",
            " o#oo##oo#o ",
            " o########o ",
            " o########o ",
            " o########o ",
            " o########o ",
            " o#o##o##o#o",
            " oo oo oo oo",
        };
    }
}
