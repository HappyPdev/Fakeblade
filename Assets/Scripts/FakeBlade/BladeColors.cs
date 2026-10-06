using System;
using System.Collections.Generic;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Color de cada pieza de la peonza (GDD 3). El color elegido por el jugador es el protagonista:
    /// va en el disco (anillas) y, en otro tono, en el núcleo.
    /// </summary>
    [Serializable]
    public struct BladeColorScheme
    {
        /// <summary>Disco / anillas: el color del jugador.</summary>
        public Color blade;
        public Color body;
        public Color tip;
        public Color core;

        public Color Get(ComponentSlot slot)
        {
            switch (slot)
            {
                case ComponentSlot.Tip: return tip;
                case ComponentSlot.Body: return body;
                case ComponentSlot.Core: return core;
                default: return blade;
            }
        }

        public void Set(ComponentSlot slot, Color color)
        {
            switch (slot)
            {
                case ComponentSlot.Tip: tip = color; break;
                case ComponentSlot.Body: body = color; break;
                case ComponentSlot.Core: core = color; break;
                default: blade = color; break;
            }
        }
    }

    /// <summary>Colores personalizados de un color de la paleta (se guardan en Opciones).</summary>
    [Serializable]
    public class SavedBladeColors
    {
        public int paletteIndex;
        public BladeColorScheme scheme;
    }

    /// <summary>
    /// Colores de las peonzas por color de la paleta (GDD 9.2.3). Por defecto salen del color del
    /// jugador (<see cref="Derive"/>); desde Opciones se pueden cambiar y se guardan en GameSettings.
    /// </summary>
    public static class BladeColors
    {
        /// <summary>Cuerpo blanco y punta negra por defecto (también están en la lista de colores del catálogo).</summary>
        public static readonly Color DefaultBody = new Color(0.95f, 0.94f, 0.9f);
        public static readonly Color DefaultTip = new Color(0.1f, 0.09f, 0.11f);

        /// <summary>Se ha cambiado o restaurado la paleta de un color (índice).</summary>
        public static event Action<int> OnChanged;

        /// <summary>
        /// Paleta por defecto de un color: anillas de ese color, cuerpo blanco, punta negra y núcleo
        /// en un tono más profundo del mismo color.
        /// </summary>
        public static BladeColorScheme Derive(Color main)
        {
            Color.RGBToHSV(main, out float h, out float s, out float v);
            return new BladeColorScheme
            {
                blade = main,
                body = DefaultBody,
                tip = DefaultTip,
                core = Color.HSVToRGB(h, Mathf.Min(1f, s * 1.1f), v * 0.6f)
            };
        }

        /// <summary>Paleta del color paletteIndex del catálogo: la personalizada si la hay, si no la de por defecto.</summary>
        public static BladeColorScheme Get(FakeBladeCatalog catalog, int paletteIndex)
        {
            int index = Normalize(catalog, paletteIndex);
            SavedBladeColors saved = Find(index);
            if (saved != null) return saved.scheme;
            return Derive(catalog != null ? catalog.GetColor(index) : Color.white);
        }

        public static bool IsCustom(FakeBladeCatalog catalog, int paletteIndex) => Find(Normalize(catalog, paletteIndex)) != null;

        /// <summary>Guarda la paleta personalizada de un color.</summary>
        public static void Set(FakeBladeCatalog catalog, int paletteIndex, BladeColorScheme scheme)
        {
            int index = Normalize(catalog, paletteIndex);
            SavedBladeColors saved = Find(index);
            if (saved == null)
            {
                saved = new SavedBladeColors { paletteIndex = index };
                SettingsService.Current.bladeColors.Add(saved);
            }
            saved.scheme = scheme;
            SettingsService.Save();
            OnChanged?.Invoke(index);
        }

        /// <summary>Vuelve a los colores por defecto de un color de la paleta.</summary>
        public static void Reset(FakeBladeCatalog catalog, int paletteIndex)
        {
            int index = Normalize(catalog, paletteIndex);
            List<SavedBladeColors> list = SettingsService.Current.bladeColors;
            if (list.RemoveAll(s => s != null && s.paletteIndex == index) == 0) return;
            SettingsService.Save();
            OnChanged?.Invoke(index);
        }

        private static SavedBladeColors Find(int index)
        {
            List<SavedBladeColors> list = SettingsService.Current.bladeColors;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].paletteIndex == index) return list[i];
            return null;
        }

        private static int Normalize(FakeBladeCatalog catalog, int index)
        {
            int count = catalog != null && catalog.palette != null ? catalog.palette.Length : 0;
            return count > 0 ? Mathf.Abs(index) % count : Mathf.Abs(index);
        }
    }
}
