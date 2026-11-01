using System;
using System.Collections.Generic;
using FakeBlade.Core;
using UnityEngine;

namespace FakeBlade.UI
{
    /// <summary>
    /// Montar una peonza en una columna de la selección (GDD 9.2.1): cambiar preset, piezas y color de un PlayerSetup
    /// y enseñarlo en su LobbyColumn (valores, barras de stats y vista previa 3D). Lo comparten la selección de
    /// peonzas (LobbyController) y la ventana «Cambiar peonza» de la pausa (ChangeBladeScreen).
    /// </summary>
    public sealed class BladeSetupEditor
    {
        /// <summary>Nombres de los niveles de IA, en el orden de AIDifficulty.</summary>
        public static readonly string[] AILevelKeys = { "AI_EASY", "AI_NORMAL", "AI_HARD", "AI_EXPERT", "AI_IMPOSSIBLE" };

        private readonly FakeBladeCatalog _catalog;
        private readonly HUDTheme _theme;
        private readonly float[] _statParts = new float[LobbyColumn.PartCount];

        public BladeSetupEditor(FakeBladeCatalog catalog, HUDTheme theme)
        {
            _catalog = catalog;
            _theme = theme;
        }

        #region Cambios
        /// <summary>Presets + "Aleatorio" (índice = número de presets). -1 = personalizada.</summary>
        public void StepPreset(PlayerSetup s, int dir)
        {
            int count = _catalog.presets.Count;
            int options = count + 1;
            int index = s.PresetIndex < 0 ? (dir > 0 ? 0 : count) : (s.PresetIndex + dir + options) % options;
            s.PresetIndex = index;

            if (index < count)
            {
                s.ApplyPreset(_catalog.presets[index]);
            }
            else
            {
                s.Tip = RandomPart(ComponentSlot.Tip);
                s.Body = RandomPart(ComponentSlot.Body);
                s.Blade = RandomPart(ComponentSlot.Blade);
                s.Core = RandomPart(ComponentSlot.Core);
            }
        }

        public void StepPart(PlayerSetup s, ComponentSlot slot, int dir)
        {
            List<FakeBladeComponentData> parts = _catalog.GetParts(slot);
            if (parts.Count == 0) return;

            int index = parts.IndexOf(s.GetPart(slot));
            index = index < 0 ? 0 : (index + dir + parts.Count) % parts.Count;
            s.SetPart(slot, parts[index]);
            s.PresetIndex = -1; // personalizada
        }

        /// <summary>Siguiente color de la paleta que no use nadie más (isUsed: índice de color → ya elegido por otro).</summary>
        public void StepColor(PlayerSetup s, int dir, Func<int, bool> isUsed)
        {
            int count = _catalog.palette.Length;
            int index = s.ColorIndex;
            for (int i = 0; i < count; i++)
            {
                index = (index + dir + count) % count;
                if (!isUsed(index)) break;
            }
            s.ColorIndex = index;
            s.Color = _catalog.GetColor(index);
        }

        private FakeBladeComponentData RandomPart(ComponentSlot slot)
        {
            List<FakeBladeComponentData> parts = _catalog.GetParts(slot);
            return parts.Count == 0 ? null : parts[UnityEngine.Random.Range(0, parts.Count)];
        }
        #endregion

        #region Vista
        /// <summary>
        /// Valores de la columna, stats y vista previa. tutorial: Aprende a jugar (arquetipo elegido, fila Poder y
        /// descripciones).
        /// </summary>
        public void Refresh(LobbyColumn column, PlayerSetup s, BladePreviewStage stage, bool tutorial)
        {
            column.PresetRow.SetValueText(PresetName(s.PresetIndex));
            column.TipRow.SetValueText(PartName(s.Tip));
            column.BodyRow.SetValueText(PartName(s.Body));
            column.BladeRow.SetValueText(PartName(s.Blade));
            column.CoreRow.SetValueText(PartName(s.Core));
            column.CoreRow.SetValueColor(CoreColor(s.Core));
            SetPartArchetype(column.TipRow, s.Tip);
            SetPartArchetype(column.BodyRow, s.Body);
            SetPartArchetype(column.BladeRow, s.Blade);
            SetPartArchetype(column.CoreRow, s.Core);

            column.ColorRow.SetValueText(Loc.Format("COLOR_N", s.ColorIndex + 1));
            column.ColorRow.SetValueColor(s.Color);
            column.TeamRow.SetValueText(s.Team == 0 ? "A" : "B");
            column.TeamRow.SetValueColor(_catalog.GetTeamColor(s.Team));
            if (s.IsCpu) column.LevelRow.SetValueText(Loc.Get(AILevelKeys[(int)s.Difficulty]));
            column.SetBorderColor(s.Color);

            BladeStatBlock stats = FakeBladeStats.Calculate(_catalog.GetBaseStats(), s.Tip, s.Body, s.Blade, s.Core);
            // En Aprende a jugar manda el arquetipo elegido: otro núcleo podría mover el calculado
            BladeArchetype archetype = tutorial ? PresetArchetype(s.PresetIndex) : stats.Archetype;
            SpecialAbilityData special = SpecialAbilities.Get(stats.Special);
            column.SetArchetype(ArchetypeName(archetype),
                Loc.Format("LOBBY_SPECIAL_LINE", special.DisplayName, stats.AttackCharges),
                _theme.GetArchetypeColor(archetype));

            if (tutorial)
            {
                column.PresetRow.SetValueColor(_theme.GetArchetypeColor(archetype));
                column.PowerRow.SetValueText(special.DisplayName.ToUpperInvariant());
                column.PowerRow.SetValueColor(special.color);
                column.SetDescriptions(Loc.Get("ARCHETYPE_DESC_" + archetype.ToString().ToUpperInvariant()),
                    special.DisplayName.ToUpperInvariant(), Loc.Get("POWER_DESC_" + special.Type.ToString().ToUpperInvariant()),
                    special.color);
            }

            // Barras por tramos: base + lo que aporta cada pieza
            BladeBaseStats b = _catalog.GetBaseStats();
            SetStatBar(column, 0, "STAT_ATTACK", s, b.attackPower, p => p.AttackPowerModifier, stats.AttackPower, 40f);
            SetStatBar(column, 1, "STAT_DEFENSE", s, b.defense, p => p.DefenseModifier, stats.Defense, 60f);
            SetStatBar(column, 2, "STAT_SPEED", s, b.moveSpeed, p => p.MoveSpeedModifier, stats.MoveSpeed, 16f);
            SetStatBar(column, 3, "STAT_RPM", s, b.maxSpin, p => p.MaxSpinModifier, stats.MaxSpin, 900f);
            SetStatBar(column, 4, "STAT_WEIGHT", s, b.weight, p => p.WeightModifier, stats.Weight, 4f);

            if (stage == null) return;
            stage.SetParts(s.Tip, s.Body, s.Blade, s.Core);
            stage.SetScheme(BladeColors.Get(_catalog, s.ColorIndex));
            // El núcleo brilla con el color de su poder, como con la esfera llena
            stage.SetCoreGlow(special.color, 1f);
            stage.SetSpinSpeed(300f + stats.MaxSpin);
        }

        public string PresetName(int index)
        {
            if (index < 0) return Loc.Get("PRESET_CUSTOM");
            if (index >= _catalog.presets.Count) return Loc.Get("PRESET_RANDOM");
            return Loc.Get(_catalog.presets[index].nameKey);
        }

        /// <summary>Arquetipo de un preset con sus piezas de serie.</summary>
        public BladeArchetype PresetArchetype(int index)
        {
            BladePreset p = _catalog.GetPreset(index);
            if (p == null) return BladeArchetype.Balanced;
            return FakeBladeStats.Calculate(_catalog.GetBaseStats(), p.tip, p.body, p.blade, p.core).Archetype;
        }

        private static string PartName(FakeBladeComponentData part) =>
            part != null ? part.ComponentName.ToUpperInvariant() : "—";

        private static string ArchetypeName(BladeArchetype archetype) =>
            Loc.Get("ARCHETYPE_" + archetype.ToString().ToUpperInvariant());

        /// <summary>Arquetipo de la pieza, pequeño y de su color, debajo del nombre (para elegir sin confusiones).</summary>
        private void SetPartArchetype(PixelOptionRow row, FakeBladeComponentData part)
        {
            if (part == null) row.SetSubValue("", _theme.textDimColor);
            else row.SetSubValue(ArchetypeName(part.Archetype), _theme.GetArchetypeColor(part.Archetype));
        }

        private void SetStatBar(LobbyColumn column, int index, string labelKey, PlayerSetup s, float baseValue,
            Func<FakeBladeComponentData, float> modifier, float finalValue, float scale)
        {
            for (int i = 0; i < _statParts.Length; i++)
            {
                FakeBladeComponentData part = s.GetPart((ComponentSlot)i);
                _statParts[i] = part != null ? modifier(part) : 0f;
            }
            column.SetStat(index, Loc.Get(labelKey), baseValue, _statParts, finalValue, scale);
        }

        /// <summary>El núcleo se ve con el color de su poder (el de su aura y su esfera), para reconocerlo de un vistazo.</summary>
        private Color CoreColor(FakeBladeComponentData core) =>
            core != null ? SpecialAbilities.Get(core.SpecialAbility).color : _theme.chargeReady;
        #endregion
    }
}
