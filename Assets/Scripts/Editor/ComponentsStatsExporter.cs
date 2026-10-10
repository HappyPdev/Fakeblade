using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Exporta las estadísticas de las piezas (Assets/Settings/ComponentsData, sin LEGACY) a
    /// "Estadísticas de las piezas.md" y "Estadísticas de las piezas.html" (para leer en el navegador, ver
    /// ComponentsStatsHtml) en la raíz del proyecto: tablas por arquetipo y por tipo de pieza, totales de
    /// los presets y lo que sale en el juego (velocidad, aceleración, daño...). Los valores derivados salen
    /// de BladeFormulas, las mismas fórmulas que usa la peonza (GDD 2.8).
    /// </summary>
    public static class ComponentsStatsExporter
    {
        private const string PartsFolder = "Assets/Settings/ComponentsData";
        private const string CatalogPath = "Assets/Settings/FakeBladeCatalog.asset";
        public const string OutputFile = "Estadísticas de las piezas.md";
        public const string HtmlFile = "Estadísticas de las piezas.html";

        /// <summary>Ruta del último HTML exportado.</summary>
        public static string LastHtmlPath { get; private set; }

        internal static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");
        internal static readonly BladeArchetype[] ArchetypeOrder =
            { BladeArchetype.Attack, BladeArchetype.Balanced, BladeArchetype.Defense, BladeArchetype.Agility };
        internal static readonly ComponentSlot[] SlotOrder =
            { ComponentSlot.Tip, ComponentSlot.Body, ComponentSlot.Blade, ComponentSlot.Core };

        [MenuItem("FakeBlade/Export ComponentsData Stats")]
        public static void ExportMenu()
        {
            string path = Export();
            if (path == null) return;
            Debug.Log($"[FakeBlade] Estadísticas exportadas a {LastHtmlPath} y {path}");
            if (EditorUtility.DisplayDialog("Estadísticas de las piezas",
                    $"Exportadas a:\n{LastHtmlPath}\n{path}", "Abrir en el navegador", "Cerrar"))
                EditorUtility.OpenWithDefaultApp(LastHtmlPath);
        }

        /// <summary>Escribe el documento y devuelve su ruta (null si falta algo).</summary>
        public static string Export()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FakeBladeCatalog>(CatalogPath);
            var cfg = catalog != null && catalog.combatConfig != null ? catalog.combatConfig : CombatConfig.Active;
            if (catalog == null || cfg == null)
            {
                Debug.LogError("[FakeBlade] No se encuentra el catálogo o CombatConfig.");
                return null;
            }

            // Base real: la del prefab de la peonza que usa el juego
            var prefabStats = catalog.playerPrefab != null ? catalog.playerPrefab.GetComponent<FakeBladeStats>() : null;
            BladeBaseStats b = prefabStats != null ? prefabStats.BaseStats : BladeBaseStats.Default;

            List<FakeBladeComponentData> parts = AssetDatabase.FindAssets("t:FakeBladeComponentData", new[] { PartsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.Replace('\\', '/').Contains("/LEGACY/"))
                .Select(AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>)
                .Where(p => p != null)
                .ToList();

            var md = new StringBuilder();
            md.AppendLine("# Estadísticas de las piezas");
            md.AppendLine();
            md.AppendLine($"> Generado el {DateTime.Now:yyyy-MM-dd HH:mm} desde `{PartsFolder}` con *FakeBlade → Export ComponentsData Stats*. " +
                          "No se edita a mano: se cambian las piezas en Unity y se vuelve a exportar. Fórmulas en el GDD (2.8).");
            md.AppendLine();
            md.AppendLine($"**Base de toda peonza** (prefab de la peonza): {N(b.maxSpin)} RPM, desgaste {N(b.spinDecay)}, velocidad {N(b.moveSpeed)}, " +
                          $"peso {N(b.weight)}, ataque {N(b.attackPower)}, defensa {N(b.defense)}, dash {N(b.dashForce)}, {b.attackCharges} cargas, " +
                          $"agarre {N(b.grip)}, parry {N(cfg.parryWindow)} s. Las piezas suman o restan sobre esto.");
            md.AppendLine();

            // 1) Por arquetipo, con el total del preset de cada uno
            md.AppendLine("## Por arquetipo");
            md.AppendLine();
            md.AppendLine("| Pieza | Tipo | RPM | Desgaste | Vel. | Peso | Ataque | Defensa | Dash | Cargas | Parry (s) | Agarre | Rasgos |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (BladeArchetype archetype in ArchetypeOrder)
            {
                md.AppendLine($"| **{ArchetypeName(archetype).ToUpperInvariant()}** | | | | | | | | | | | | |");
                foreach (ComponentSlot slot in SlotOrder)
                    foreach (var part in Sorted(parts.Where(p => p.Archetype == archetype && p.ComponentType == slot)))
                        md.AppendLine(PartRow(part, SlotName(slot)));
                foreach (BladePreset preset in catalog.presets)
                {
                    var s = FakeBladeStats.Calculate(b, preset.tip, preset.body, preset.blade, preset.core);
                    if (s.Archetype == archetype) md.AppendLine(TotalRow($"*Preset {PresetName(preset)}*", "*Total*", s, cfg, true));
                }
            }
            md.AppendLine();

            // 2) Por tipo de pieza
            md.AppendLine("## Por tipo de pieza");
            md.AppendLine();
            md.AppendLine("| Pieza | Arquetipo | RPM | Desgaste | Vel. | Peso | Ataque | Defensa | Dash | Cargas | Parry (s) | Agarre | Rasgos |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (ComponentSlot slot in SlotOrder)
            {
                md.AppendLine($"| **{SlotPlural(slot).ToUpperInvariant()}** | | | | | | | | | | | | |");
                foreach (BladeArchetype archetype in ArchetypeOrder)
                    foreach (var part in Sorted(parts.Where(p => p.Archetype == archetype && p.ComponentType == slot)))
                        md.AppendLine(PartRow(part, ArchetypeName(archetype)));
            }
            md.AppendLine();

            // 3) Totales de los presets y lo que sale en el juego
            md.AppendLine("## Presets");
            md.AppendLine();
            md.AppendLine("| Preset | Piezas | RPM | Desgaste | Vel. | Peso | Ataque | Defensa | Dash | Cargas | Parry (s) | Agarre | Rasgos |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (BladePreset preset in catalog.presets)
            {
                var s = FakeBladeStats.Calculate(b, preset.tip, preset.body, preset.blade, preset.core);
                string names = string.Join(" + ", new[] { preset.tip, preset.body, preset.blade, preset.core }
                    .Where(p => p != null).Select(p => p.ComponentName));
                md.AppendLine(TotalRow(PresetName(preset), names, s, cfg, false));
            }
            md.AppendLine();
            md.AppendLine("\\* Llega al mínimo del juego (desgaste 0,5, velocidad 2 o peso 0,3): lo que resten sus piezas por debajo no cuenta. " +
                          "Parry: la ventana total, con el tope de 0,4 s.");
            md.AppendLine();

            md.AppendLine("### Lo que sale en el juego");
            md.AppendLine();
            md.AppendLine("| Preset | Daño que hace | Daño que recibe | Empuje que recibe | Vel. máx. | Aceleración | Llega a tope en | Giro | Frenado al soltar | Dash | Acelerón del ataque | Masa |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (BladePreset preset in catalog.presets)
            {
                var s = FakeBladeStats.Calculate(b, preset.tip, preset.body, preset.blade, preset.core);
                float maxSpeed = BladeFormulas.MaxSpeed(cfg, s.MoveSpeed);
                float accel = BladeFormulas.Acceleration(cfg, s.MoveSpeed, s.Weight);
                float impulse = BladeFormulas.ImpulseFactor(cfg, s.Weight);
                md.AppendLine($"| {PresetName(preset)} " +
                              $"| ×{N(BladeFormulas.AttackMultiplier(cfg, s.AttackPower))} " +
                              $"| ×{N(BladeFormulas.DamageTakenFactor(s.Defense))} " +
                              $"| ×{N(BladeFormulas.KnockbackTakenFactor(s.Defense))} " +
                              $"| {N(maxSpeed, 1)} m/s | {N(accel, 0)} m/s² | {N(maxSpeed / Mathf.Max(0.01f, accel), 2)} s " +
                              $"| {N(BladeFormulas.TurnRate(cfg, s.Grip), 1)}/s " +
                              $"| {N(BladeFormulas.StoppingRate(cfg, s.Grip), 2)}/s " +
                              $"| {N(s.DashForce * impulse, 1)} m/s | {N(cfg.quickAttackImpulse * impulse, 1)} m/s " +
                              $"| {N(BladeFormulas.PhysicalMass(cfg, s.Weight))} |");
            }
            md.AppendLine();
            md.AppendLine("Sin rasgos ni poderes. Daño que hace: multiplicador por el ataque (×1 = ataque " +
                          $"{N(cfg.referenceAttackPower)}). Daño y empuje que recibe: lo que deja pasar la defensa. " +
                          "Llega a tope en: tiempo aproximado para coger casi toda su velocidad (velocidad máxima ÷ aceleración).");
            md.AppendLine();

            // 4) Qué hace cada estadística
            md.AppendLine("## Qué hace cada estadística");
            md.AppendLine();
            md.AppendLine("| Estadística | Qué cambia |");
            md.AppendLine("|---|---|");
            md.AppendLine("| RPM | La vida. Los costes son un % de ella (ataque 2% por carga, dash 8%, curación común 25%). |");
            md.AppendLine("| Desgaste | RPM que se pierden solas por segundo. |");
            md.AppendLine($"| Velocidad | Solo la velocidad máxima: {N(cfg.speedPerPoint * cfg.speedSpread)} m/s por punto (tras igualar), pese lo que pese. A la aceleración solo le afecta por encima de 10. |");
            md.AppendLine($"| Peso | Aceleración y acelerones del ataque y del dash, una sola vez y sin topes ((peso {N(cfg.referenceWeight)} ÷ peso) elevado a {N(cfg.accelerationWeightExponent)} y a {N(cfg.impulseWeightExponent)}), y empuje en los choques. No toca el daño, la velocidad máxima ni el giro. |");
            md.AppendLine("| Ataque | Daño que hace: cada punto, +4% (ataque 15 = ×1). |");
            md.AppendLine("| Defensa | Daño recibido −1% por punto y empuje recibido −0,5% por punto. |");
            md.AppendLine("| Dash | Velocidad del acelerón del dash (× el factor de peso). |");
            md.AppendLine("| Cargas | Ataques guardados y nivel máximo del cargado. |");
            md.AppendLine("| Parry | Segundos que se suman a la ventana de parry (tope 0,4 s). |");
            md.AppendLine($"| Agarre | Giro (cuánto derrapa) y frenado al soltar el stick: cada punto, ±{N(cfg.gripPerPoint * 100f, 0)}%. Lo dan las puntas. |");

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputFile));
            File.WriteAllText(path, md.ToString(), new UTF8Encoding(false));

            // La misma información en HTML, para leerla en el navegador (tablas con colores, filtros y orden)
            string htmlPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", HtmlFile));
            File.WriteAllText(htmlPath, ComponentsStatsHtml.Build(catalog, cfg, b, parts), new UTF8Encoding(false));
            LastHtmlPath = htmlPath;
            return path;
        }

        #region Filas
        internal static IEnumerable<FakeBladeComponentData> Sorted(IEnumerable<FakeBladeComponentData> parts) =>
            parts.OrderBy(p => p.ComponentName, StringComparer.Ordinal);

        private static string PartRow(FakeBladeComponentData p, string second)
        {
            string name = p.ComponentType == ComponentSlot.Core && p.SpecialAbility != SpecialAbilityType.None
                ? $"{p.ComponentName} ({PowerName(p.SpecialAbility)})"
                : p.ComponentName;
            return $"| {name} | {second} | {S(p.MaxSpinModifier)} | {S(p.SpinDecayModifier)} | {S(p.MoveSpeedModifier)} | {S(p.WeightModifier)} " +
                   $"| {S(p.AttackPowerModifier)} | {S(p.DefenseModifier)} | {S(p.DashForceModifier)} | {S(p.AttackChargesModifier)} " +
                   $"| {S(p.ParryWindowModifier)} | {S(p.GripModifier)} | {Traits(p.Traits)} |";
        }

        private static string TotalRow(string label, string second, BladeStatBlock s, CombatConfig cfg, bool italic)
        {
            string i = italic ? "*" : "";
            float parry = Mathf.Clamp(cfg.parryWindow + s.ParryWindowBonus, 0.02f, 0.4f);
            string Min(float value, float min) => value <= min + 0.0001f ? $"{N(value)}\\*" : N(value);
            return $"| {label} | {second} | {i}{N(s.MaxSpin)}{i} | {i}{Min(s.SpinDecay, 0.5f)}{i} | {i}{Min(s.MoveSpeed, 2f)}{i} " +
                   $"| {i}{Min(s.Weight, 0.3f)}{i} | {i}{N(s.AttackPower)}{i} | {i}{N(s.Defense)}{i} | {i}{N(s.DashForce)}{i} " +
                   $"| {i}{s.AttackCharges}{i} | {i}{N(parry)}{i} | {i}{N(s.Grip)}{i} | {i}{(s.Traits == null || s.Traits.IsEmpty ? "—" : TraitTotals(s.Traits))}{i} |";
        }

        internal static string Traits(IReadOnlyList<PartTrait> traits)
        {
            if (traits == null || traits.Count == 0) return "—";
            return string.Join(", ", traits.Select(t => $"{TraitName(t.type)} {Percent(t.percent)}"));
        }

        internal static string TraitTotals(PartTraits traits)
        {
            var list = new List<string>();
            foreach (PartTraitType type in Enum.GetValues(typeof(PartTraitType)))
            {
                float sum = traits.Sum(type);
                if (sum != 0f) list.Add($"{TraitName(type)} {Percent(sum)}");
            }
            return string.Join(", ", list);
        }
        #endregion

        #region Textos
        internal static string TraitName(PartTraitType type)
        {
            string name = Loc.Get("TRAIT_" + type.ToString().ToUpperInvariant(), Language.Spanish);
            return name.Length > 0 ? char.ToUpper(name[0]) + name.Substring(1).ToLower(Es) : name;
        }

        internal static string Percent(float value) => (value > 0f ? "+" : value < 0f ? "−" : "") + N(Mathf.Abs(value) * 100f, 0) + "%";

        /// <summary>Nombre del preset en español con mayúscula inicial ("Ataque").</summary>
        internal static string PresetName(BladePreset preset)
        {
            string name = Loc.Get(preset.nameKey, Language.Spanish);
            return name.Length > 0 ? char.ToUpper(name[0]) + name.Substring(1).ToLower(Es) : name;
        }

        internal static string ArchetypeName(BladeArchetype a) =>
            a == BladeArchetype.Attack ? "Ataque" : a == BladeArchetype.Defense ? "Defensa" : a == BladeArchetype.Agility ? "Agilidad" : "Balanceada";

        internal static string SlotName(ComponentSlot s) =>
            s == ComponentSlot.Tip ? "Punta" : s == ComponentSlot.Body ? "Cuerpo" : s == ComponentSlot.Blade ? "Anilla" : "Núcleo";

        internal static string SlotPlural(ComponentSlot s) =>
            s == ComponentSlot.Tip ? "Puntas" : s == ComponentSlot.Body ? "Cuerpos" : s == ComponentSlot.Blade ? "Anillas" : "Núcleos";

        internal static string PowerName(SpecialAbilityType type)
        {
            var field = typeof(SpecialAbilityType).GetField(type.ToString());
            var attr = field != null ? (InspectorNameAttribute)Attribute.GetCustomAttribute(field, typeof(InspectorNameAttribute)) : null;
            return attr != null ? attr.displayName : type.ToString();
        }

        /// <summary>Número con coma decimal.</summary>
        internal static string N(float v, int decimals = 2) =>
            Math.Round(v, decimals).ToString(decimals == 0 ? "0" : "0." + new string('#', decimals), Es);

        /// <summary>Modificador con signo: +2, −0,5 o 0.</summary>
        internal static string S(float v) => v > 0f ? "+" + N(v) : v < 0f ? "−" + N(-v) : "0";
        #endregion
    }
}
