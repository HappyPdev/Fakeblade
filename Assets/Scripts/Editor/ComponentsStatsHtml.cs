using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using UnityEngine;
using X = FakeBlade.Core.Editor.ComponentsStatsExporter;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Versión HTML de las estadísticas de las piezas (la escribe ComponentsStatsExporter): un solo archivo
    /// sin dependencias, con pestañas, colores por arquetipo, valores en verde/rojo según ayuden o
    /// perjudiquen, lo mejor y lo peor de cada columna en "En el juego" y una tabla de todas las piezas
    /// que se puede ordenar y filtrar. Se genera entera cada vez, así que vale aunque haya piezas nuevas.
    /// </summary>
    internal static class ComponentsStatsHtml
    {
        /// <summary>Columna de estadística de pieza: +1 = subir ayuda, −1 = subir perjudica, 0 = ni bueno ni malo.</summary>
        private struct Column
        {
            public string Header;
            public Func<FakeBladeComponentData, float> Part;
            public Func<BladeStatBlock, float> Total;
            public int Good;
            public float Min; // mínimo del juego (para marcar los totales que lo tocan)
        }

        private static readonly Column[] Columns =
        {
            new Column { Header = "RPM", Part = p => p.MaxSpinModifier, Total = s => s.MaxSpin, Good = 1, Min = 100f },
            new Column { Header = "Desgaste", Part = p => p.SpinDecayModifier, Total = s => s.SpinDecay, Good = -1, Min = 0.5f },
            new Column { Header = "Vel.", Part = p => p.MoveSpeedModifier, Total = s => s.MoveSpeed, Good = 1, Min = 2f },
            new Column { Header = "Peso", Part = p => p.WeightModifier, Total = s => s.Weight, Good = 0, Min = 0.3f },
            new Column { Header = "Ataque", Part = p => p.AttackPowerModifier, Total = s => s.AttackPower, Good = 1, Min = 1f },
            new Column { Header = "Defensa", Part = p => p.DefenseModifier, Total = s => s.Defense, Good = 1, Min = 0f },
            new Column { Header = "Dash", Part = p => p.DashForceModifier, Total = s => s.DashForce, Good = 1, Min = 5f },
            new Column { Header = "Cargas", Part = p => p.AttackChargesModifier, Total = s => s.AttackCharges, Good = 1, Min = 1f },
            new Column { Header = "Parry (s)", Part = p => p.ParryWindowModifier, Total = s => s.ParryWindowBonus, Good = 1, Min = float.MinValue },
            new Column { Header = "Agarre", Part = p => p.GripModifier, Total = s => s.Grip, Good = 1, Min = float.MinValue },
        };

        /// <summary>Rasgos en los que bajar es lo bueno (menos espera, menos coste, menos daño recibido...).</summary>
        private static readonly HashSet<PartTraitType> LowerIsBetter = new HashSet<PartTraitType>
        {
            PartTraitType.DashCooldown, PartTraitType.DashCost, PartTraitType.AttackRechargeTime, PartTraitType.AttackCost,
            PartTraitType.ChargeTime, PartTraitType.DamageTaken, PartTraitType.KnockbackTaken, PartTraitType.WallDamageTaken,
            PartTraitType.StatusDurationTaken
        };

        public static string Build(FakeBladeCatalog catalog, CombatConfig cfg, BladeBaseStats b, List<FakeBladeComponentData> parts)
        {
            var h = new StringBuilder();
            h.AppendLine("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">");
            h.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            h.AppendLine("<title>Estadísticas de las piezas</title>");
            h.AppendLine("<style>" + Style + "</style></head><body>");

            h.AppendLine("<header><h1>Estadísticas de las piezas</h1>");
            h.AppendLine($"<p class=\"meta\">Generado el {DateTime.Now:yyyy-MM-dd HH:mm} con <b>FakeBlade → Export ComponentsData Stats</b>. " +
                         "Las tablas son una foto de ese momento; las gráficas y <b>Editar y exportar</b> se recalculan con tus cambios. " +
                         "Para pasarlos a Unity: descarga los cambios y usa <b>FakeBlade → Import ComponentsData Stats</b>. Fórmulas en el GDD (2.8).</p>");
            h.AppendLine($"<p class=\"base\"><b>Base de toda peonza:</b> {X.N(b.maxSpin)} RPM · desgaste {X.N(b.spinDecay)} · velocidad {X.N(b.moveSpeed)} · " +
                         $"peso {X.N(b.weight)} · ataque {X.N(b.attackPower)} · defensa {X.N(b.defense)} · dash {X.N(b.dashForce)} · " +
                         $"{b.attackCharges} cargas · agarre {X.N(b.grip)} · parry {X.N(cfg.parryWindow)} s. Las piezas suman o restan sobre esto.</p>");
            h.AppendLine("<p class=\"legend\"><span class=\"good\">verde</span> ayuda · <span class=\"bad\">rojo</span> perjudica · " +
                         "<span class=\"neutral\">azul</span> ni bueno ni malo (peso) · <span class=\"min\">✱</span> llega al mínimo del juego</p>");
            h.AppendLine("<nav>" +
                         "<a href=\"#graficas\">Gráficas</a><a href=\"#editar\">Editar y exportar</a>" +
                         "<a href=\"#arquetipo\">Por arquetipo</a><a href=\"#tipo\">Por tipo de pieza</a><a href=\"#todas\">Todas (ordenar y filtrar)</a>" +
                         "<a href=\"#presets\">Presets</a><a href=\"#juego\">En el juego</a><a href=\"#estadisticas\">Qué hace cada estadística</a></nav></header>");
            h.AppendLine("<main>");

            // 0) Gráficas y editor: los rellena el script con los datos de abajo y se recalculan al editar
            h.AppendLine("<section id=\"graficas\"><h2>Gráficas <span class=\"dirty-flag\" hidden>· con tus cambios sin aplicar</span></h2>");
            h.AppendLine("<h3>Presets respecto a la Balanceada</h3>");
            h.AppendLine("<p class=\"hint\">Cada barra sale de la línea de la Balanceada (100%): a la derecha tiene más, a la izquierda menos. " +
                         "En todas las filas, más es mejor. Vida efectiva = RPM ÷ daño que recibe (cuánto daño aguanta). Pasa el ratón por una barra para ver el valor.</p>");
            h.AppendLine("<div class=\"chart-legend\" id=\"legend-profile\"></div><div class=\"chart\" id=\"chart-profile\"></div>");
            h.AppendLine("<h3>Lo que aporta cada pieza</h3>");
            h.AppendLine("<p class=\"hint\">Modificador de cada pieza en la estadística elegida (a la derecha suma, a la izquierda resta). " +
                         "<label>Estadística: <select id=\"part-stat\"></select></label></p>");
            h.AppendLine("<div class=\"chart\" id=\"chart-parts\"></div></section>");

            h.AppendLine("<section id=\"editar\"><h2>Editar y exportar</h2>");
            h.AppendLine("<p class=\"hint\">Cambia los valores de las piezas: los totales, lo que sale en el juego y las gráficas se recalculan al momento. " +
                         "Los rasgos se editan en % (−20 = −20%); añadir o quitar rasgos se hace en Unity. Luego <b>Descargar cambios</b> y en Unity " +
                         "<b>FakeBlade → Import ComponentsData Stats</b> con ese archivo (enseña los cambios antes de aplicarlos y se pueden deshacer con Ctrl+Z).</p>");
            h.AppendLine("<div class=\"edit-actions\"><button id=\"btn-download\" class=\"primary\" disabled>Descargar cambios</button>" +
                         "<button id=\"btn-reset\" disabled>Deshacer todos</button><span id=\"change-count\" class=\"hint\">Sin cambios</span></div>");
            h.AppendLine("<h3>Presets con tus cambios</h3><div class=\"scroll\" id=\"live-presets\"></div>");
            h.AppendLine("<h3>Piezas</h3><div class=\"scroll\" id=\"editor\"></div></section>");

            // 1) Por arquetipo
            h.AppendLine("<section id=\"arquetipo\"><h2>Por arquetipo</h2><div class=\"scroll\"><table>");
            h.AppendLine(HeaderRow("Pieza", "Tipo"));
            foreach (BladeArchetype archetype in X.ArchetypeOrder)
            {
                h.AppendLine(GroupRow(X.ArchetypeName(archetype), Css(archetype)));
                foreach (ComponentSlot slot in X.SlotOrder)
                    foreach (var part in X.Sorted(parts.Where(p => p.Archetype == archetype && p.ComponentType == slot)))
                        h.AppendLine(PartRow(part, X.SlotName(slot)));
                foreach (BladePreset preset in catalog.presets)
                {
                    var s = FakeBladeStats.Calculate(b, preset.tip, preset.body, preset.blade, preset.core);
                    if (s.Archetype == archetype) h.AppendLine(TotalRow("Preset " + X.PresetName(preset), "Total", s, cfg, archetype));
                }
            }
            h.AppendLine("</table></div></section>");

            // 2) Por tipo de pieza
            h.AppendLine("<section id=\"tipo\"><h2>Por tipo de pieza</h2><div class=\"scroll\"><table>");
            h.AppendLine(HeaderRow("Pieza", "Arquetipo"));
            foreach (ComponentSlot slot in X.SlotOrder)
            {
                h.AppendLine(GroupRow(X.SlotPlural(slot), "slot"));
                foreach (BladeArchetype archetype in X.ArchetypeOrder)
                    foreach (var part in X.Sorted(parts.Where(p => p.Archetype == archetype && p.ComponentType == slot)))
                        h.AppendLine(PartRow(part, X.ArchetypeName(archetype)));
            }
            h.AppendLine("</table></div></section>");

            // 3) Todas las piezas: ordenar y filtrar
            h.AppendLine("<section id=\"todas\"><h2>Todas las piezas</h2>");
            h.AppendLine("<p class=\"hint\">Pulsa una cabecera para ordenar (otra vez, al revés). Los botones filtran por arquetipo y por tipo.</p>");
            h.AppendLine("<div class=\"filters\">");
            foreach (BladeArchetype a in X.ArchetypeOrder)
                h.AppendLine($"<button class=\"chip on {Css(a)}\" data-filter=\"arch\" data-value=\"{Css(a)}\">{X.ArchetypeName(a)}</button>");
            h.AppendLine("<span class=\"sep\"></span>");
            foreach (ComponentSlot slot in X.SlotOrder)
                h.AppendLine($"<button class=\"chip on\" data-filter=\"slot\" data-value=\"{slot}\">{X.SlotPlural(slot)}</button>");
            h.AppendLine("</div><div class=\"scroll\"><table id=\"all\" class=\"sortable\">");
            h.AppendLine("<thead><tr><th data-type=\"text\">Pieza</th><th data-type=\"text\">Tipo</th><th data-type=\"text\">Arquetipo</th>" +
                         string.Concat(Columns.Select(c => $"<th>{c.Header}</th>")) + "<th data-type=\"text\">Rasgos</th></tr></thead><tbody>");
            foreach (BladeArchetype archetype in X.ArchetypeOrder)
                foreach (ComponentSlot slot in X.SlotOrder)
                    foreach (var part in X.Sorted(parts.Where(p => p.Archetype == archetype && p.ComponentType == slot)))
                        h.AppendLine(PartRow(part, X.SlotName(slot), X.ArchetypeName(archetype), slot.ToString()));
            h.AppendLine("</tbody></table></div></section>");

            // 4) Presets
            h.AppendLine("<section id=\"presets\"><h2>Presets</h2><div class=\"scroll\"><table>");
            h.AppendLine("<thead><tr><th>Preset</th><th>Piezas</th>" + string.Concat(Columns.Select(c => $"<th>{c.Header}</th>")) +
                         "<th>Rasgos</th></tr></thead>");
            foreach (BladePreset preset in catalog.presets)
            {
                var s = FakeBladeStats.Calculate(b, preset.tip, preset.body, preset.blade, preset.core);
                string names = string.Join(" + ", new[] { preset.tip, preset.body, preset.blade, preset.core }
                    .Where(p => p != null).Select(p => E(p.ComponentName)));
                h.AppendLine(TotalRow(X.PresetName(preset), names, s, cfg, s.Archetype, false));
            }
            h.AppendLine("</table></div><p class=\"hint\">Parry: la ventana total, con el tope de 0,4 s.</p></section>");

            // 5) En el juego
            h.AppendLine(GameTable(catalog, cfg, b));

            // 6) Qué hace cada estadística
            h.AppendLine("<section id=\"estadisticas\"><h2>Qué hace cada estadística</h2><table class=\"legend-table\">");
            foreach (var row in StatHelp)
                h.AppendLine($"<tr><th>{row.Key}</th><td>{row.Value}</td></tr>");
            h.AppendLine("</table></section>");

            h.AppendLine("</main><div id=\"tooltip\" hidden></div>");
            h.AppendLine("<script id=\"fb-data\" type=\"application/json\">" + DataJson(catalog, cfg, b, parts) + "</script>");
            h.AppendLine("<script>" + Script + EditorScript + "</script></body></html>");
            return h.ToString();
        }

        #region Tablas
        private static string HeaderRow(string first, string second) =>
            $"<thead><tr><th>{first}</th><th>{second}</th>" + string.Concat(Columns.Select(c => $"<th>{c.Header}</th>")) +
            "<th>Rasgos</th></tr></thead>";

        private static string GroupRow(string label, string css) =>
            $"<tr class=\"group {css}\"><td colspan=\"{Columns.Length + 3}\">{E(label)}</td></tr>";

        private static string PartRow(FakeBladeComponentData p, string second, string archetypeName = null, string slot = null)
        {
            string name = p.ComponentType == ComponentSlot.Core && p.SpecialAbility != SpecialAbilityType.None
                ? $"{E(p.ComponentName)} <span class=\"power\">{E(X.PowerName(p.SpecialAbility))}</span>"
                : E(p.ComponentName);
            var row = new StringBuilder();
            row.Append(archetypeName != null
                ? $"<tr class=\"part {Css(p.Archetype)}\" data-arch=\"{Css(p.Archetype)}\" data-slot=\"{slot}\"><td data-v=\"{E(p.ComponentName)}\">{name}</td><td>{second}</td><td>{archetypeName}</td>"
                : $"<tr class=\"part {Css(p.Archetype)}\"><td>{name}</td><td>{second}</td>");
            foreach (var c in Columns)
            {
                float v = c.Part(p);
                row.Append($"<td class=\"num {Tone(v, c.Good)}\" data-v=\"{v.ToString(System.Globalization.CultureInfo.InvariantCulture)}\">{X.S(v)}</td>");
            }
            row.Append($"<td class=\"traits\">{TraitBadges(p.Traits.Select(t => (t.type, t.percent)))}</td></tr>");
            return row.ToString();
        }

        private static string TotalRow(string label, string second, BladeStatBlock s, CombatConfig cfg, BladeArchetype archetype, bool total = true)
        {
            var row = new StringBuilder($"<tr class=\"{(total ? "total " : "")}{Css(archetype)}\"><td>{E(label)}</td><td>{second}</td>");
            foreach (var c in Columns)
            {
                float v = c.Header.StartsWith("Parry") ? Mathf.Clamp(cfg.parryWindow + s.ParryWindowBonus, 0.02f, 0.4f) : c.Total(s);
                bool atMin = !c.Header.StartsWith("Parry") && v <= c.Min + 0.0001f;
                row.Append($"<td class=\"num\">{X.N(v)}{(atMin ? "<span class=\"min\" title=\"Llega al mínimo del juego: lo que resten sus piezas por debajo no cuenta\">✱</span>" : "")}</td>");
            }
            var traits = new List<(PartTraitType, float)>();
            if (s.Traits != null)
                foreach (PartTraitType type in Enum.GetValues(typeof(PartTraitType)))
                    if (s.Traits.Sum(type) != 0f) traits.Add((type, s.Traits.Sum(type)));
            row.Append($"<td class=\"traits\">{TraitBadges(traits)}</td></tr>");
            return row.ToString();
        }

        private static string GameTable(FakeBladeCatalog catalog, CombatConfig cfg, BladeBaseStats b)
        {
            // Columnas: cabecera, valor, formato, +1 = más es mejor, −1 = menos es mejor, 0 = sin marcar
            var cols = new List<(string head, Func<BladeStatBlock, float> value, Func<float, string> fmt, int better)>
            {
                ("Daño que hace", s => BladeFormulas.AttackMultiplier(cfg, s.AttackPower), v => "×" + X.N(v), 1),
                ("Daño que recibe", s => BladeFormulas.DamageTakenFactor(s.Defense), v => "×" + X.N(v), -1),
                ("Empuje que recibe", s => BladeFormulas.KnockbackTakenFactor(s.Defense), v => "×" + X.N(v), -1),
                ("Vel. máx.", s => BladeFormulas.MaxSpeed(cfg, s.MoveSpeed), v => X.N(v, 1) + " m/s", 1),
                ("Aceleración", s => BladeFormulas.Acceleration(cfg, s.MoveSpeed, s.Weight), v => X.N(v, 0) + " m/s²", 1),
                ("Llega a tope en", s => BladeFormulas.MaxSpeed(cfg, s.MoveSpeed) /
                                         Mathf.Max(0.01f, BladeFormulas.Acceleration(cfg, s.MoveSpeed, s.Weight)), v => X.N(v) + " s", -1),
                ("Giro", s => BladeFormulas.TurnRate(cfg, s.Grip), v => X.N(v, 1) + "/s", 1),
                ("Frenado al soltar", s => BladeFormulas.StoppingRate(cfg, s.Grip), v => X.N(v) + "/s", 1),
                ("Dash", s => s.DashForce * BladeFormulas.ImpulseFactor(cfg, s.Weight), v => X.N(v, 1) + " m/s", 1),
                ("Acelerón del ataque", s => cfg.quickAttackImpulse * BladeFormulas.ImpulseFactor(cfg, s.Weight), v => X.N(v, 1) + " m/s", 1),
                ("Masa", s => BladeFormulas.PhysicalMass(cfg, s.Weight), v => X.N(v), 0),
            };
            var stats = catalog.presets.Select(p => (p, s: FakeBladeStats.Calculate(b, p.tip, p.body, p.blade, p.core))).ToList();

            var h = new StringBuilder("<section id=\"juego\"><h2>Lo que sale en el juego</h2><div class=\"scroll\"><table>");
            h.Append("<thead><tr><th>Preset</th>" + string.Concat(cols.Select(c => $"<th>{c.head}</th>")) + "</tr></thead>");
            foreach (var (preset, s) in stats)
            {
                h.Append($"<tr class=\"{Css(s.Archetype)}\"><td>{E(X.PresetName(preset))}</td>");
                foreach (var c in cols)
                {
                    float v = c.value(s);
                    string mark = "";
                    if (c.better != 0 && stats.Count > 1)
                    {
                        float max = stats.Max(x => c.value(x.s)), min = stats.Min(x => c.value(x.s));
                        if (max - min > 0.0001f)
                        {
                            bool best = c.better > 0 ? v >= max - 0.0001f : v <= min + 0.0001f;
                            bool worst = c.better > 0 ? v <= min + 0.0001f : v >= max - 0.0001f;
                            mark = best ? " best" : worst ? " worst" : "";
                        }
                    }
                    h.Append($"<td class=\"num{mark}\">{c.fmt(v)}</td>");
                }
                h.AppendLine("</tr>");
            }
            h.Append("</table></div><p class=\"hint\">Sin rasgos ni poderes. <span class=\"good\">Verde</span>: el mejor de la columna; " +
                     "<span class=\"bad\">rojo</span>: el peor. Daño que hace: multiplicador por el ataque (×1 = ataque " +
                     $"{X.N(cfg.referenceAttackPower)}). Daño y empuje que recibe: lo que deja pasar la defensa. Llega a tope en: velocidad máxima ÷ aceleración.</p></section>");
            return h.ToString();
        }
        #endregion

        #region Datos para el script (editor y gráficas)
        /// <summary>
        /// JSON con las piezas (GUID, valores y rasgos), los presets, la base y los valores de CombatConfig que
        /// usan las fórmulas. El script del HTML recalcula con esto; las fórmulas de JS copian las de
        /// BladeFormulas y FakeBladeStats.Calculate (si cambian allí, cambiar también EditorScript).
        /// </summary>
        private static string DataJson(FakeBladeCatalog catalog, CombatConfig cfg, BladeBaseStats b, List<FakeBladeComponentData> parts)
        {
            var j = new StringBuilder("{");
            j.Append($"\"exported\":{J(DateTime.Now.ToString("yyyy-MM-dd HH:mm"))},\"maxCharges\":{AttackSystem.MaxSupportedCharges},");
            j.Append($"\"base\":{{\"maxSpin\":{F(b.maxSpin)},\"spinDecay\":{F(b.spinDecay)},\"moveSpeed\":{F(b.moveSpeed)},\"weight\":{F(b.weight)}," +
                     $"\"attack\":{F(b.attackPower)},\"defense\":{F(b.defense)},\"dash\":{F(b.dashForce)},\"charges\":{b.attackCharges},\"grip\":{F(b.grip)}}},");
            j.Append($"\"cfg\":{{\"parryWindow\":{F(cfg.parryWindow)},\"minPhysicalMass\":{F(cfg.minPhysicalMass)},\"maxVelocity\":{F(cfg.maxVelocity)}," +
                     $"\"speedPerPoint\":{F(cfg.speedPerPoint)},\"referenceMaxSpeed\":{F(cfg.referenceMaxSpeed)},\"speedSpread\":{F(cfg.speedSpread)}," +
                     $"\"referenceWeight\":{F(cfg.referenceWeight)},\"referenceAcceleration\":{F(cfg.referenceAcceleration)}," +
                     $"\"accelerationWeightExponent\":{F(cfg.accelerationWeightExponent)},\"referenceImpulse\":{F(cfg.referenceImpulse)}," +
                     $"\"impulseWeightExponent\":{F(cfg.impulseWeightExponent)},\"turnRate\":{F(cfg.turnRate)},\"stoppingRate\":{F(cfg.stoppingRate)}," +
                     $"\"gripPerPoint\":{F(cfg.gripPerPoint)}," +
                     $"\"referenceAttackPower\":{F(cfg.referenceAttackPower)},\"attackSpread\":{F(cfg.attackSpread)},\"quickAttackImpulse\":{F(cfg.quickAttackImpulse)}}},");

            j.Append("\"parts\":[");
            bool first = true;
            foreach (BladeArchetype archetype in X.ArchetypeOrder)
                foreach (ComponentSlot slot in X.SlotOrder)
                    foreach (var p in X.Sorted(parts.Where(x => x.Archetype == archetype && x.ComponentType == slot)))
                    {
                        if (!first) j.Append(',');
                        first = false;
                        string guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(p));
                        j.Append($"{{\"guid\":{J(guid)},\"name\":{J(p.ComponentName)},\"file\":{J(p.name)},\"slot\":{J(slot.ToString())}," +
                                 $"\"slotName\":{J(X.SlotName(slot))},\"slotPlural\":{J(X.SlotPlural(slot))},\"arch\":{J(Css(archetype))}," +
                                 $"\"archName\":{J(X.ArchetypeName(archetype))}," +
                                 $"\"power\":{J(slot == ComponentSlot.Core && p.SpecialAbility != SpecialAbilityType.None ? X.PowerName(p.SpecialAbility) : "")}," +
                                 $"\"stats\":{{\"maxSpin\":{F(p.MaxSpinModifier)},\"spinDecay\":{F(p.SpinDecayModifier)},\"moveSpeed\":{F(p.MoveSpeedModifier)}," +
                                 $"\"weight\":{F(p.WeightModifier)},\"attack\":{F(p.AttackPowerModifier)},\"defense\":{F(p.DefenseModifier)}," +
                                 $"\"dash\":{F(p.DashForceModifier)},\"charges\":{p.AttackChargesModifier},\"parry\":{F(p.ParryWindowModifier)},\"grip\":{F(p.GripModifier)}}},\"traits\":[");
                        j.Append(string.Join(",", p.Traits.Select(t =>
                            $"{{\"type\":{(int)t.type},\"name\":{J(X.TraitName(t.type))},\"lower\":{(LowerIsBetter.Contains(t.type) ? "true" : "false")},\"percent\":{F(t.percent)}}}")));
                        j.Append("]}");
                    }
            j.Append("],\"presets\":[");
            j.Append(string.Join(",", catalog.presets.Select(pr =>
            {
                var s = FakeBladeStats.Calculate(b, pr.tip, pr.body, pr.blade, pr.core);
                var guids = new[] { pr.tip, pr.body, pr.blade, pr.core }.Where(x => x != null)
                    .Select(x => J(UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(x))));
                return $"{{\"name\":{J(X.PresetName(pr))},\"arch\":{J(Css(s.Archetype))},\"parts\":[{string.Join(",", guids)}]}}";
            })));
            j.Append("]}");
            return j.ToString();
        }

        private static string F(float v) => v.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);

        private static string J(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '<') sb.Append("\\u003c"); // nunca cerrar el <script> por accidente
                else if (c < ' ') sb.Append($"\\u{(int)c:x4}");
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
        #endregion

        #region Ayudas
        private static string TraitBadges(IEnumerable<(PartTraitType type, float percent)> traits)
        {
            var list = traits.ToList();
            if (list.Count == 0) return "<span class=\"zero\">—</span>";
            return string.Join(" ", list.Select(t =>
            {
                bool good = LowerIsBetter.Contains(t.type) ? t.percent < 0f : t.percent > 0f;
                return $"<span class=\"badge {(good ? "good" : "bad")}\">{E(X.TraitName(t.type))} {X.Percent(t.percent)}</span>";
            }));
        }

        private static string Tone(float v, int good)
        {
            if (Mathf.Abs(v) < 0.0001f) return "zero";
            if (good == 0) return "neutral";
            return (v > 0f) == (good > 0) ? "good" : "bad";
        }

        private static string Css(BladeArchetype a) =>
            a == BladeArchetype.Attack ? "attack" : a == BladeArchetype.Defense ? "defense" : a == BladeArchetype.Agility ? "agility" : "balanced";

        private static string E(string s) => WebUtility.HtmlEncode(s ?? "");

        private static readonly KeyValuePair<string, string>[] StatHelp =
        {
            new KeyValuePair<string, string>("RPM", "La vida. Los costes son un % de ella (ataque 2% por carga, dash 8%, curación común 25%)."),
            new KeyValuePair<string, string>("Desgaste", "RPM que se pierden solas por segundo."),
            new KeyValuePair<string, string>("Velocidad", "Solo la velocidad máxima: la misma por punto pese lo que pese (tras igualar). A la aceleración solo le afecta por encima de 10."),
            new KeyValuePair<string, string>("Peso", "Aceleración y acelerones del ataque y del dash (una sola vez y sin topes: un jefe de peso 10 sigue moviéndose) y empuje en los choques. No toca el daño, la velocidad máxima ni el giro."),
            new KeyValuePair<string, string>("Ataque", "Daño que hace: cada punto, +4% (ataque 15 = ×1)."),
            new KeyValuePair<string, string>("Defensa", "Daño recibido −1% por punto y empuje recibido −0,5% por punto."),
            new KeyValuePair<string, string>("Dash", "Velocidad del acelerón del dash (× el factor de peso)."),
            new KeyValuePair<string, string>("Cargas", "Ataques guardados y nivel máximo del cargado."),
            new KeyValuePair<string, string>("Parry", "Segundos que se suman a la ventana de parry (tope 0,4 s)."),
            new KeyValuePair<string, string>("Agarre", "Giro (cuánto derrapa al cambiar de dirección) y frenado al soltar el stick: cada punto, ±10% (gripPerPoint). Lo dan las puntas: goma agarra, bola desliza."),
        };
        #endregion

        private const string Style = @"
:root{--bg:#f6f7f9;--card:#fff;--text:#1d2128;--dim:#6b7280;--line:#e3e6eb;--head:#eef0f3;
--good:#15803d;--good-bg:#dcfce7;--bad:#b91c1c;--bad-bg:#fee2e2;--neutral:#1d4ed8;
--attack:#eb6834;--balanced:#6b7280;--defense:#2a78d6;--agility:#1baf7a;--grid:#e5e7eb;--input:#fff;--changed:#fff4c2;}
@media (prefers-color-scheme:dark){:root{--bg:#121417;--card:#1b1e23;--text:#e6e8eb;--dim:#9aa1ab;--line:#2c3138;--head:#23272d;
--good:#4ade80;--good-bg:#14361f;--bad:#f87171;--bad-bg:#3b1717;--neutral:#93b4ff;
--attack:#d95926;--balanced:#8b95a5;--defense:#3987e5;--agility:#199e70;--grid:#2c3138;--input:#15181c;--changed:#3d3514;}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:14px/1.45 system-ui,-apple-system,'Segoe UI',sans-serif}
header{background:var(--card);border-bottom:1px solid var(--line);padding:16px 24px;position:sticky;top:0;z-index:5}
h1{margin:0 0 4px;font-size:22px}h2{font-size:18px;margin:0 0 10px}.meta,.hint{color:var(--dim);margin:2px 0}.base,.legend{margin:6px 0}
nav{display:flex;flex-wrap:wrap;gap:6px;margin-top:10px}nav a{color:var(--text);text-decoration:none;padding:4px 10px;border:1px solid var(--line);border-radius:999px;background:var(--head)}
nav a:hover{border-color:var(--dim)}main{padding:16px 24px;max-width:1500px}section{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:16px;margin:0 0 18px;scroll-margin-top:150px}
.scroll{overflow-x:auto}table{border-collapse:collapse;width:100%;white-space:nowrap}th,td{padding:5px 9px;border-bottom:1px solid var(--line);text-align:left}
thead th{background:var(--head);position:sticky;top:0;font-weight:600;font-size:13px}td.num{text-align:right;font-variant-numeric:tabular-nums}
.good{color:var(--good)}.bad{color:var(--bad)}.neutral{color:var(--neutral)}.zero{color:var(--dim)}
td.best{background:var(--good-bg);color:var(--good);font-weight:600}td.worst{background:var(--bad-bg);color:var(--bad);font-weight:600}
tr.part td:first-child,tr.total td:first-child,section#juego tr td:first-child,section#presets tr td:first-child{border-left:4px solid var(--c,transparent)}
.attack{--c:var(--attack)}.balanced{--c:var(--balanced)}.defense{--c:var(--defense)}.agility{--c:var(--agility)}
tr.group td{font-weight:700;letter-spacing:.04em;text-transform:uppercase;background:color-mix(in srgb,var(--c,var(--dim)) 14%,var(--card));color:var(--c,var(--text));border-left:4px solid var(--c,var(--dim))}
tr.group.slot td{--c:var(--dim)}tr.total td{font-weight:700;background:color-mix(in srgb,var(--c) 7%,var(--card))}
.min{color:var(--bad);margin-left:2px;font-size:11px;vertical-align:super}.power{color:var(--dim);font-size:12px}
.badge{display:inline-block;padding:1px 7px;border-radius:999px;font-size:12px;margin:1px 2px 1px 0}.badge.good{background:var(--good-bg)}.badge.bad{background:var(--bad-bg)}
td.traits{white-space:normal;min-width:180px}.filters{display:flex;flex-wrap:wrap;gap:6px;margin:8px 0}.sep{width:12px}
.chip{border:1px solid var(--line);background:var(--head);color:var(--dim);border-radius:999px;padding:3px 11px;cursor:pointer;font:inherit}
.chip.on{color:var(--text);border-color:var(--c,var(--dim));box-shadow:inset 0 -2px 0 var(--c,var(--dim))}
table.sortable th{cursor:pointer;user-select:none}table.sortable th.asc::after{content:' ▲'}table.sortable th.desc::after{content:' ▼'}
.legend-table th{width:120px;white-space:nowrap}.legend-table td{white-space:normal}
h3{font-size:15px;margin:18px 0 6px}.dirty-flag{color:var(--bad);font-size:14px;font-weight:600}
.chart{width:100%;overflow-x:auto}.chart svg{display:block;max-width:100%;height:auto}
.chart text{fill:var(--text);font:12px system-ui,-apple-system,'Segoe UI',sans-serif}.chart .muted{fill:var(--dim)}
.chart .gridline{stroke:var(--grid);stroke-width:1}.chart .baseline{stroke:var(--dim);stroke-width:1.5}
.chart rect.bar{fill:var(--c)}.chart rect.bar:hover{opacity:.8}.chart .group-label{font-weight:700;letter-spacing:.04em}
.chart-legend{display:flex;gap:14px;flex-wrap:wrap;margin:6px 0;color:var(--text)}.chart-legend span::before{content:'';display:inline-block;width:12px;height:12px;border-radius:3px;background:var(--c);margin-right:6px;vertical-align:-1px}
#tooltip{position:fixed;z-index:20;pointer-events:none;background:var(--card);color:var(--text);border:1px solid var(--line);border-radius:8px;padding:6px 9px;box-shadow:0 4px 14px rgba(0,0,0,.18);font-size:13px;max-width:320px}
select,button,input{font:inherit;color:var(--text)}select{background:var(--input);border:1px solid var(--line);border-radius:6px;padding:2px 6px}
.edit-actions{display:flex;gap:8px;align-items:center;margin:8px 0}.edit-actions button{border:1px solid var(--line);background:var(--head);border-radius:8px;padding:6px 14px;cursor:pointer}
.edit-actions button.primary:not([disabled]){background:var(--defense);border-color:var(--defense);color:#fff}.edit-actions button[disabled]{opacity:.5;cursor:default}
#editor input{width:64px;text-align:right;background:var(--input);border:1px solid var(--line);border-radius:5px;padding:2px 5px;font-variant-numeric:tabular-nums}
#editor input.changed{background:var(--changed);border-color:var(--dim);font-weight:600}#editor .trait{display:inline-flex;align-items:center;gap:4px;margin-right:8px;font-size:12px}
#editor .trait input{width:54px}#live-presets .delta{font-size:11px;margin-left:4px}";

        /// <summary>
        /// Editor, recálculo y gráficas. Las fórmulas copian FakeBladeStats.Calculate (sumas y mínimos) y
        /// BladeFormulas (movimiento, ataque y defensa); los valores de CombatConfig llegan en el JSON.
        /// El archivo que descarga lo lee ComponentsStatsImporter (mismos nombres de campo).
        /// </summary>
        private const string EditorScript = @"
(function(){
const D=JSON.parse(document.getElementById('fb-data').textContent), C=D.cfg, B=D.base;
const parts=D.parts, orig=JSON.parse(JSON.stringify(parts)), byGuid={};parts.forEach(p=>byGuid[p.guid]=p);
const STATS=[['maxSpin','RPM',1,5],['spinDecay','Desgaste',-1,0.1],['moveSpeed','Vel.',1,0.1],['weight','Peso',0,0.1],
 ['attack','Ataque',1,0.5],['defense','Defensa',1,1],['dash','Dash',1,0.5],['charges','Cargas',1,1],['parry','Parry (s)',1,0.01],['grip','Agarre',1,0.5]];
const lerp=(a,b,t)=>a+(b-a)*t, clamp=(v,a,b)=>Math.min(b,Math.max(a,v)), inv=(a,b,v)=>clamp((v-a)/(b-a),0,1);
const fmt=(v,d=2)=>Number(Number(v).toFixed(d)).toLocaleString('es-ES',{maximumFractionDigits:d});
const sgn=v=>v>0?'+'+fmt(v):v<0?'−'+fmt(-v):'0';
const tip=document.getElementById('tooltip');
function showTip(e,html){tip.innerHTML=html;tip.hidden=false;moveTip(e);}
function moveTip(e){tip.style.left=Math.min(e.clientX+14,innerWidth-tip.offsetWidth-8)+'px';tip.style.top=(e.clientY+14)+'px';}
function hideTip(){tip.hidden=true;}

// FakeBladeStats.Calculate: base + piezas, con los mínimos del juego
function totals(guids,useOrig){
 const s={maxSpin:B.maxSpin,spinDecay:B.spinDecay,moveSpeed:B.moveSpeed,weight:B.weight,attack:B.attack,defense:B.defense,dash:B.dash,charges:B.charges,parry:0,grip:B.grip||0};
 guids.forEach(g=>{const p=useOrig?orig.find(o=>o.guid===g):byGuid[g];if(!p)return;for(const k in s)s[k]+=p.stats[k]||0;});
 s.maxSpin=Math.max(100,s.maxSpin);s.spinDecay=Math.max(0.5,s.spinDecay);s.moveSpeed=Math.max(2,s.moveSpeed);s.weight=Math.max(0.3,s.weight);
 s.attack=Math.max(1,s.attack);s.defense=clamp(s.defense,0,80);s.dash=Math.max(5,s.dash);s.charges=clamp(Math.round(s.charges),1,D.maxCharges);
 s.parryWin=clamp(C.parryWindow+s.parry,0.02,0.4);return s;}
// BladeFormulas: lo que sale en el juego (sin rasgos ni poderes)
function game(s){
 const mass=Math.max(C.minPhysicalMass,s.weight),ratio=C.referenceWeight/mass,grip=Math.max(0.25,1+s.grip*C.gripPerPoint);
 const maxSpeed=clamp(lerp(C.referenceMaxSpeed,C.maxVelocity+s.moveSpeed*C.speedPerPoint,C.speedSpread),3,25);
 const accel=clamp(C.referenceAcceleration*Math.max(s.moveSpeed*0.1,1)*Math.pow(ratio,C.accelerationWeightExponent),1,120);
 const turn=clamp(C.turnRate*grip,2,50);
 const imp=C.referenceImpulse*Math.pow(ratio,C.impulseWeightExponent),taken=1-s.defense*0.01;
 return {atk:lerp(1,s.attack/Math.max(1,C.referenceAttackPower),C.attackSpread),taken:taken,knock:1-s.defense*0.005,maxSpeed:maxSpeed,accel:accel,
  top:maxSpeed/Math.max(0.01,accel),turn:turn,stop:C.stoppingRate*grip,dash:s.dash*imp,
  impulse:C.quickAttackImpulse*imp,mass:mass,ehp:s.maxSpin/Math.max(0.01,taken)};}

// ---- Editor ----
const editor=document.getElementById('editor');
function buildEditor(){
 let h='<table><thead><tr><th>Pieza</th><th>Tipo</th>'+STATS.map(c=>'<th>'+c[1]+'</th>').join('')+'<th>Rasgos (%)</th></tr></thead><tbody>';
 let arch='';
 parts.forEach(p=>{
  if(p.arch!==arch){arch=p.arch;h+='<tr class=\'group '+p.arch+'\'><td colspan=\''+(STATS.length+3)+'\'>'+p.archName+'</td></tr>';}
  h+='<tr class=\'part '+p.arch+'\'><td>'+p.name+(p.power?' <span class=\'power\'>'+p.power+'</span>':'')+'</td><td>'+p.slotName+'</td>';
  STATS.forEach(c=>{h+='<td><input type=\'number\' step=\''+c[3]+'\' data-g=\''+p.guid+'\' data-k=\''+c[0]+'\' value=\''+p.stats[c[0]]+'\'></td>';});
  h+='<td>'+(p.traits.length?p.traits.map((t,i)=>'<label class=\'trait\'>'+t.name+' <input type=\'number\' step=\'5\' data-g=\''+p.guid+'\' data-t=\''+i+'\' value=\''+Math.round(t.percent*1000)/10+'\'></label>').join(''):'<span class=\'zero\'>—</span>')+'</td></tr>';});
 editor.innerHTML=h+'</tbody></table>';
 editor.querySelectorAll('input').forEach(inp=>inp.addEventListener('input',()=>{
  const p=byGuid[inp.dataset.g],o=orig.find(x=>x.guid===p.guid),v=parseFloat(inp.value);if(isNaN(v))return;
  if(inp.dataset.k){const k=inp.dataset.k;p.stats[k]=k==='charges'?Math.round(v):v;inp.classList.toggle('changed',Math.abs(p.stats[k]-o.stats[k])>1e-6);}
  else{const i=+inp.dataset.t;p.traits[i].percent=v/100;inp.classList.toggle('changed',Math.abs(p.traits[i].percent-o.traits[i].percent)>1e-6);}
  update();}));}
function changes(){let n=0;parts.forEach(p=>{const o=orig.find(x=>x.guid===p.guid);
 STATS.forEach(c=>{if(Math.abs(p.stats[c[0]]-o.stats[c[0]])>1e-6)n++;});p.traits.forEach((t,i)=>{if(Math.abs(t.percent-o.traits[i].percent)>1e-6)n++;});});return n;}

// ---- Presets con los cambios ----
function livePresets(){
 const cols=[['maxSpin','RPM',v=>fmt(v,0),1,'s'],['ehp','Vida efectiva',v=>fmt(v,0),1,'g'],['atk','Daño que hace',v=>'×'+fmt(v),1,'g'],
  ['taken','Daño que recibe',v=>'×'+fmt(v),-1,'g'],['maxSpeed','Vel. máx.',v=>fmt(v,1)+' m/s',1,'g'],['accel','Aceleración',v=>fmt(v,0)+' m/s²',1,'g'],
  ['top','Llega a tope en',v=>fmt(v)+' s',-1,'g'],['dash','Dash',v=>fmt(v,1)+' m/s',1,'g'],['charges','Cargas',v=>fmt(v,0),1,'s'],['parryWin','Parry',v=>fmt(v)+' s',1,'s']];
 let h='<table><thead><tr><th>Preset</th>'+cols.map(c=>'<th>'+c[1]+'</th>').join('')+'</tr></thead><tbody>';
 D.presets.forEach(pr=>{const s=totals(pr.parts),g=game(s),s0=totals(pr.parts,true),g0=game(s0);
  h+='<tr class=\''+pr.arch+'\'><td>'+pr.name+'</td>';
  cols.forEach(c=>{const v=(c[4]==='s'?s:g)[c[0]],v0=(c[4]==='s'?s0:g0)[c[0]],d=v-v0;let delta='';
   if(Math.abs(d)>1e-6)delta='<span class=\'delta '+((d>0)===(c[3]>0)?'good':'bad')+'\'>'+(d>0?'▲':'▼')+' '+c[2](Math.abs(d)).replace('×','')+'</span>';
   h+='<td class=\'num\'>'+c[2](v)+delta+'</td>';});h+='</tr>';});
 document.getElementById('live-presets').innerHTML=h+'</tbody></table>';}

// ---- Gráfica 1: perfil respecto a la Balanceada (barras divergentes desde el 100%) ----
const METRICS=[['atk','Daño que hace',v=>'×'+fmt(v)],['ehp','Vida efectiva',v=>fmt(v,0)],['maxSpeed','Velocidad máxima',v=>fmt(v,1)+' m/s'],
 ['accel','Aceleración',v=>fmt(v,0)+' m/s²'],['turn','Giro',v=>fmt(v,1)+'/s'],['stop','Frenado al soltar',v=>fmt(v)+'/s'],['dash','Dash',v=>fmt(v,1)+' m/s'],['charges','Cargas',v=>fmt(v,0)]];
function chartProfile(){
 const el=document.getElementById('chart-profile'),lg=document.getElementById('legend-profile');
 const ref=D.presets.find(p=>p.arch==='balanced');if(!ref){el.innerHTML='<p class=\'hint\'>No hay preset Balanceada.</p>';return;}
 const val=(pr,k)=>{const s=totals(pr.parts),g=game(s);return k==='charges'?s.charges:g[k];};
 const others=D.presets.filter(p=>p!==ref);
 lg.innerHTML=others.map(p=>'<span class=\''+p.arch+'\'>'+p.name+'</span>').join('')+'<span style=\'--c:var(--dim)\'>Balanceada = 100%</span>';
 const rows=METRICS.map(m=>({m:m,ref:val(ref,m[0]),items:others.map(p=>{const v=val(p,m[0]);return {p:p,v:v};})}));
 rows.forEach(r=>r.items.forEach(i=>i.r=r.ref>0?i.v/r.ref*100:100));
 const all=rows.flatMap(r=>r.items.map(i=>i.r));let lo=Math.min(50,...all),hi=Math.max(150,...all);lo=Math.floor(lo/50)*50;hi=Math.ceil(hi/50)*50;
 const L=150,W=Math.max(620,el.clientWidth||760),R=60,bh=11,gap=3,rowH=others.length*(bh+gap)+16,top=22,H=top+rows.length*rowH+8;
 const x=v=>L+(v-lo)/(hi-lo)*(W-L-R);
 let s='<svg viewBox=\'0 0 '+W+' '+H+'\' role=\'img\' aria-label=\'Presets respecto a la Balanceada\'>';
 for(let t=lo;t<=hi;t+=50){s+='<line class=\''+(t===100?'baseline':'gridline')+'\' x1=\''+x(t)+'\' x2=\''+x(t)+'\' y1=\''+(top-6)+'\' y2=\''+(H-6)+'\'/>'+
  '<text class=\'muted\' x=\''+x(t)+'\' y=\'12\' text-anchor=\'middle\'>'+t+'%</text>';}
 rows.forEach((r,ri)=>{const y0=top+ri*rowH;
  s+='<text x=\''+(L-10)+'\' y=\''+(y0+(others.length*(bh+gap))/2+4)+'\' text-anchor=\'end\'>'+r.m[1]+'</text>';
  r.items.forEach((i,k)=>{const y=y0+k*(bh+gap),a=x(Math.min(100,i.r)),b=x(Math.max(100,i.r)),w=Math.max(1.5,b-a);
   const t='<b>'+i.p.name+'</b> · '+r.m[1]+'<br>'+r.m[2](i.v)+' ('+fmt(i.r,0)+'% de la Balanceada, que tiene '+r.m[2](r.ref)+')';
   s+='<rect class=\'bar '+i.p.arch+'\' x=\''+a+'\' y=\''+y+'\' width=\''+w+'\' height=\''+bh+'\' rx=\'2\' data-tip=\''+encodeURIComponent(t)+'\'/>';});});
 el.innerHTML=s+'</svg>';bindTips(el);}

// ---- Gráfica 2: lo que aporta cada pieza en una estadística ----
const sel=document.getElementById('part-stat');
STATS.forEach(c=>{const o=document.createElement('option');o.value=c[0];o.textContent=c[1];sel.appendChild(o);});
sel.value='attack';sel.addEventListener('change',chartParts);
function chartParts(){
 const el=document.getElementById('chart-parts'),k=sel.value,label=STATS.find(c=>c[0]===k)[1];
 const slots=[...new Set(parts.map(p=>p.slot))];const order=['Tip','Body','Blade','Core'];slots.sort((a,b)=>order.indexOf(a)-order.indexOf(b));
 const vals=parts.map(p=>p.stats[k]);let lo=Math.min(0,...vals),hi=Math.max(0,...vals);if(hi-lo<1e-6){hi=1;}
 const pad=(hi-lo)*0.12;lo-=lo<0?pad:0;hi+=hi>0?pad:0;
 const L=210,W=Math.max(620,el.clientWidth||760),R=40,bh=14,gap=4,top=10;let rowsCount=0;slots.forEach(sl=>{rowsCount+=1+parts.filter(p=>p.slot===sl).length;});
 const H=top+rowsCount*(bh+gap)+10,x=v=>L+(v-lo)/(hi-lo)*(W-L-R);
 let s='<svg viewBox=\'0 0 '+W+' '+H+'\' role=\'img\' aria-label=\'Aporte de cada pieza: '+label+'\'>';
 s+='<line class=\'baseline\' x1=\''+x(0)+'\' x2=\''+x(0)+'\' y1=\''+top+'\' y2=\''+(H-6)+'\'/>';
 let y=top;
 slots.forEach(sl=>{const list=parts.filter(p=>p.slot===sl);
  s+='<text class=\'group-label muted\' x=\'0\' y=\''+(y+bh-2)+'\'>'+list[0].slotPlural.toUpperCase()+'</text>';y+=bh+gap;
  list.forEach(p=>{const v=p.stats[k],a=x(Math.min(0,v)),b=x(Math.max(0,v));
   s+='<text x=\''+(L-10)+'\' y=\''+(y+bh-3)+'\' text-anchor=\'end\'>'+p.name+'</text>';
   if(Math.abs(v)>1e-6)s+='<rect class=\'bar '+p.arch+'\' x=\''+a+'\' y=\''+y+'\' width=\''+Math.max(1.5,b-a)+'\' height=\''+bh+'\' rx=\'2\' data-tip=\''+
     encodeURIComponent('<b>'+p.name+'</b> ('+p.archName+', '+p.slotName.toLowerCase()+')<br>'+label+': '+sgn(v))+'\'/>';
   s+='<text class=\'muted\' x=\''+(v<0?a-4:b+4)+'\' y=\''+(y+bh-3)+'\' text-anchor=\''+(v<0?'end':'start')+'\'>'+sgn(v)+'</text>';
   y+=bh+gap;});});
 el.innerHTML=s+'</svg>';bindTips(el);}
function bindTips(el){el.querySelectorAll('[data-tip]').forEach(r=>{r.addEventListener('mouseenter',e=>showTip(e,decodeURIComponent(r.dataset.tip)));
 r.addEventListener('mousemove',moveTip);r.addEventListener('mouseleave',hideTip);});}

// ---- Descargar / deshacer ----
const btnDl=document.getElementById('btn-download'),btnReset=document.getElementById('btn-reset');
function update(){const n=changes();btnDl.disabled=n===0;btnReset.disabled=n===0;btnDl.textContent=n?'Descargar cambios ('+n+')':'Descargar cambios';
 document.getElementById('change-count').textContent=n?n+' valores cambiados':'Sin cambios';
 document.querySelectorAll('.dirty-flag').forEach(f=>f.hidden=n===0);livePresets();chartProfile();chartParts();}
const r4=v=>Math.round(v*10000)/10000;
btnDl.addEventListener('click',()=>{
 const out={version:1,exported:D.exported,parts:parts.map(p=>{const o=orig.find(x=>x.guid===p.guid),st=s=>({maxSpin:r4(s.maxSpin),spinDecay:r4(s.spinDecay),
  moveSpeed:r4(s.moveSpeed),weight:r4(s.weight),attack:r4(s.attack),defense:r4(s.defense),dash:r4(s.dash),charges:Math.round(s.charges),parry:r4(s.parry),grip:r4(s.grip||0)});
  return {guid:p.guid,name:p.name,original:st(o.stats),values:st(p.stats),originalTraits:o.traits.map(t=>({type:t.type,percent:r4(t.percent)})),
   traits:p.traits.map(t=>({type:t.type,percent:r4(t.percent)}))};})};
 const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(out,null,1)],{type:'application/json'}));
 a.download='fakeblade-piezas.json';document.body.appendChild(a);a.click();a.remove();});
btnReset.addEventListener('click',()=>{if(!confirm('¿Deshacer todos los cambios?'))return;
 parts.forEach(p=>{const o=orig.find(x=>x.guid===p.guid);p.stats=JSON.parse(JSON.stringify(o.stats));p.traits=JSON.parse(JSON.stringify(o.traits));});
 buildEditor();update();});
buildEditor();update();
let rz;addEventListener('resize',()=>{clearTimeout(rz);rz=setTimeout(()=>{chartProfile();chartParts();},150);});
})();";

        private const string Script = @"
document.querySelectorAll('table.sortable').forEach(t=>{
  t.querySelectorAll('thead th').forEach((th,i)=>th.addEventListener('click',()=>{
    const asc=!th.classList.contains('asc');t.querySelectorAll('thead th').forEach(x=>x.classList.remove('asc','desc'));
    th.classList.add(asc?'asc':'desc');const text=th.dataset.type==='text';const body=t.tBodies[0];
    const rows=[...body.rows];rows.sort((a,b)=>{const va=a.cells[i].dataset.v??a.cells[i].textContent,vb=b.cells[i].dataset.v??b.cells[i].textContent;
      const r=text?va.localeCompare(vb,'es'):parseFloat(va)-parseFloat(vb);return asc?r:-r;});rows.forEach(r=>body.appendChild(r));}));});
const active={arch:new Set(),slot:new Set()};
document.querySelectorAll('.chip').forEach(c=>{active[c.dataset.filter].add(c.dataset.value);
  c.addEventListener('click',()=>{const set=active[c.dataset.filter];c.classList.toggle('on');
    c.classList.contains('on')?set.add(c.dataset.value):set.delete(c.dataset.value);
    document.querySelectorAll('#all tbody tr').forEach(r=>{r.style.display=active.arch.has(r.dataset.arch)&&active.slot.has(r.dataset.slot)?'':'none';});});});";
    }
}
