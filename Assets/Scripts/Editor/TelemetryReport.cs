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
    /// Informe de telemetría (C19, GDD 6.6): junta los CSV de las partidas (partida_*.csv) que haya en
    /// Logs/Telemetria/ y sus subcarpetas (también los copiados de otros ordenadores) y escribe en
    /// Logs/Telemetria/Informes/ un informe en Markdown y una tabla con una fila por jugador y partida.
    ///
    /// Las victorias se comparan con las esperadas (1 / jugadores, o 1 / equipos): un ratio de 1 es lo justo.
    /// Las partidas abandonadas y las que no tienen ni un choque solo se cuentan en el resumen.
    /// </summary>
    public static class TelemetryReport
    {
        private const int SmallSample = 5;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private sealed class Match
        {
            public string File;
            public Dictionary<string, string> Info = new Dictionary<string, string>();
            public float Duration;
            public string End = "abandoned";
            public bool Draw;
            public int Dropped;
            public readonly List<PlayerRow> Players = new List<PlayerRow>(4);

            public bool Complete => End != "abandoned";
            /// <summary>Nadie ha chocado con nadie (peonzas quietas, pruebas): no dice nada del equilibrio.</summary>
            public bool NoCombat => Players.All(p => p.S("clashes") <= 0f);
            public bool Teams => Get(Info, "teams") == "1";
            public string Mode => Get(Info, "mode");

            /// <summary>Victorias esperadas de cada jugador si todos fueran iguales.</summary>
            public float ExpectedWin
            {
                get
                {
                    int sides = Teams ? Players.Select(p => Get(p.Info, "team")).Distinct().Count() : Players.Count;
                    return sides > 0 ? 1f / sides : 0f;
                }
            }
        }

        private sealed class PlayerRow
        {
            public Match Match;
            public string Name;
            public float MaxSpin, Weight, Attack, Defense;
            public Dictionary<string, string> Info = new Dictionary<string, string>();
            public Dictionary<string, string> Summary = new Dictionary<string, string>();

            public bool Winner => Get(Summary, "winner") == "1";
            public bool IsAI => Get(Info, "control") == "ai";
            public string Control => IsAI ? "IA " + Get(Info, "level") : "Humano";
            public float S(string key) => ParseF(Get(Summary, key));
        }

        public static string LastReportPath { get; private set; }

        [MenuItem("FakeBlade/Informe de telemetría")]
        public static void Run()
        {
            if (Generate() == null)
            {
                EditorUtility.DisplayDialog("Informe de telemetría",
                    $"No hay partidas en {MatchTelemetry.EditorFolder}.\n\nJuega partidas (no el sandbox) o copia aquí la " +
                    "carpeta Telemetria que deja la build junto al ejecutable.", "Vale");
                return;
            }
            EditorUtility.RevealInFinder(LastReportPath);
        }

        /// <summary>Escribe el informe y la tabla. Devuelve la ruta del informe, o null si no hay partidas.</summary>
        public static string Generate()
        {
            string folder = MatchTelemetry.EditorFolder;
            List<Match> matches = Load(folder, out int files);
            if (matches.Count == 0) return null;

            string outFolder = Path.Combine(folder, "Informes");
            Directory.CreateDirectory(outFolder);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string tablePath = Path.Combine(outFolder, $"jugadores_{stamp}.csv");
            LastReportPath = Path.Combine(outFolder, $"informe_{stamp}.md");
            // Decimales siempre con punto, como en los CSV
            CultureInfo culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = Inv;
            try
            {
                File.WriteAllText(tablePath, PlayersTable(matches), new UTF8Encoding(false));
                File.WriteAllText(LastReportPath, Report(matches, files, Path.GetFileName(tablePath)), new UTF8Encoding(false));
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
            Debug.Log($"[Telemetría] Informe de {matches.Count} partidas en {LastReportPath}");
            return LastReportPath;
        }

        [MenuItem("FakeBlade/Abrir último informe de telemetría")]
        public static void OpenLastReport() => EditorUtility.OpenWithDefaultApp(LastReportPath);

        [MenuItem("FakeBlade/Abrir último informe de telemetría", true)]
        private static bool CanOpenLastReport() => !string.IsNullOrEmpty(LastReportPath);

        #region Lectura
        private static List<Match> Load(string folder, out int files)
        {
            var matches = new List<Match>();
            files = 0;
            if (!Directory.Exists(folder)) return matches;

            // El mismo archivo copiado dos veces (de otra carpeta) cuenta una sola vez
            var seen = new HashSet<string>();
            foreach (string path in Directory.GetFiles(folder, MatchTelemetry.FilePrefix + "*.csv", SearchOption.AllDirectories)
                         .OrderBy(p => Path.GetFileName(p)))
            {
                if (!seen.Add(Path.GetFileName(path))) continue;
                files++;
                try
                {
                    Match m = Parse(path);
                    if (m != null && m.Players.Count > 0) matches.Add(m);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Telemetría] No se puede leer {path}: {e.Message}");
                }
            }
            return matches;
        }

        private static Match Parse(string path)
        {
            var m = new Match { File = Path.GetFileName(path) };
            var byName = new Dictionary<string, PlayerRow>();
            foreach (string line in File.ReadLines(path))
            {
                string[] c = line.Split(';');
                if (c.Length < 9) continue;
                switch (c[1])
                {
                    case "MATCH":
                        m.Info = KeyValues(c[8]);
                        break;
                    case "PLAYER":
                        var p = new PlayerRow
                        {
                            Match = m, Name = c[2], MaxSpin = ParseF(c[4]), Weight = ParseF(c[5]), Attack = ParseF(c[6]),
                            Defense = ParseF(c[7]), Info = KeyValues(c[8])
                        };
                        m.Players.Add(p);
                        byName[p.Name] = p;
                        break;
                    case "END":
                        m.Duration = ParseF(c[4]);
                        m.Draw = c[5] == "1";
                        m.Dropped = (int)ParseF(c[7]);
                        m.End = Get(KeyValues(c[8]), "end");
                        break;
                    case "SUMMARY":
                        if (byName.TryGetValue(c[2], out PlayerRow row)) row.Summary = KeyValues(c[8]);
                        break;
                }
            }
            return m;
        }

        private static Dictionary<string, string> KeyValues(string info)
        {
            var d = new Dictionary<string, string>();
            foreach (string pair in info.Split('|'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0) d[pair.Substring(0, eq)] = pair.Substring(eq + 1);
            }
            return d;
        }

        private static string Get(Dictionary<string, string> d, string key) =>
            d != null && d.TryGetValue(key, out string v) ? v : "";

        private static float ParseF(string s) =>
            float.TryParse(s, NumberStyles.Float, Inv, out float v) ? v : 0f;
        #endregion

        #region Tabla por jugador
        private static readonly string[] SummaryKeys =
        {
            "winner", "finalSpin", "lives", "score", "eliminatedAt", "kos", "deaths", "selfKOs", "damageDealt",
            "damageTakenRival", "damageTakenWall", "clashDamageDealt", "clashDamageTaken", "clashes", "hitsLanded",
            "hitsReceived", "evenClashes", "attackHits", "dashHits", "attacks0", "attacks1", "attacks2", "attacks3", "dashes",
            "parries", "parried", "doubleParries", "blocks", "blocked", "specials", "specialTime", "healed", "healCuts",
            "statusInflicted", "statusReceived", "wallHits", "avgSpeed", "avgCenterDistance", "aliveTime"
        };

        private static readonly string[] InfoKeys =
        {
            "control", "level", "profile", "device", "team", "preset", "archetype", "tip", "body", "blade", "core", "special",
            "energyRequired"
        };

        /// <summary>Una fila por jugador y partida, para analizarla con una hoja de cálculo o con Claude.</summary>
        private static string PlayersTable(List<Match> matches)
        {
            var sb = new StringBuilder();
            sb.Append("file;date;version;platform;machine;mode;arena;players;duration;end;expectedWin;player;");
            sb.Append(string.Join(";", InfoKeys)).Append(";maxSpin;weight;attack;defense;");
            sb.Append(string.Join(";", SummaryKeys)).Append('\n');
            foreach (Match m in matches)
            {
                foreach (PlayerRow p in m.Players)
                {
                    sb.Append(m.File).Append(';').Append(Get(m.Info, "date")).Append(';').Append(Get(m.Info, "version"))
                        .Append(';').Append(Get(m.Info, "platform")).Append(';').Append(Get(m.Info, "machine"))
                        .Append(';').Append(m.Mode).Append(';').Append(Get(m.Info, "arena")).Append(';')
                        .Append(m.Players.Count).Append(';').Append(m.Duration.ToString("0.#", Inv)).Append(';')
                        .Append(m.End).Append(';').Append(m.ExpectedWin.ToString("0.###", Inv)).Append(';').Append(p.Name);
                    foreach (string k in InfoKeys) sb.Append(';').Append(Get(p.Info, k));
                    sb.Append(';').Append(p.MaxSpin.ToString(Inv)).Append(';').Append(p.Weight.ToString(Inv))
                        .Append(';').Append(p.Attack.ToString(Inv)).Append(';').Append(p.Defense.ToString(Inv));
                    foreach (string k in SummaryKeys) sb.Append(';').Append(Get(p.Summary, k));
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }
        #endregion

        #region Informe
        private static string Report(List<Match> all, int files, string tableName)
        {
            List<Match> done = all.Where(m => m.Complete && !m.NoCombat).ToList();
            int idle = all.Count(m => m.Complete && m.NoCombat);
            List<PlayerRow> rows = done.SelectMany(m => m.Players).ToList();
            var sb = new StringBuilder();

            sb.Append($"# Informe de telemetría ({DateTime.Now:yyyy-MM-dd HH:mm})\n\n");
            sb.Append($"Tabla con una fila por jugador y partida: `{tableName}` (misma carpeta).\n\n");

            // Resumen
            sb.Append("## Resumen\n\n");
            float minutes = all.Sum(m => m.Duration) / 60f;
            sb.Append($"- Archivos: {files}. Partidas completas: **{done.Count}**; abandonadas: {all.Count(m => !m.Complete)}; " +
                      $"sin ningún choque (fuera del informe): {idle}.\n");
            sb.Append($"- Tiempo jugado: {minutes:0.#} min. Duración media de las completas: " +
                      $"{(done.Count > 0 ? done.Average(m => m.Duration) : 0f):0} s (mediana {Median(done.Select(m => m.Duration)):0} s).\n");
            sb.Append($"- Ordenadores: {all.Select(m => Get(m.Info, "machine")).Distinct().Count()}. " +
                      $"Versiones: {string.Join(", ", all.Select(m => Get(m.Info, "version")).Distinct())}. " +
                      $"En el editor: {all.Count(m => Get(m.Info, "platform") == "editor")}.\n");
            sb.Append($"- Modos: {Counts(done, m => m.Mode)}.\n");
            sb.Append($"- Arenas: {Counts(done, m => Get(m.Info, "arena"))}.\n");
            sb.Append($"- Partidas por número de jugadores: {Counts(done, m => m.Players.Count + " jugadores:")}. " +
                      $"Con IA: {done.Count(m => m.Players.Any(p => p.IsAI))}; solo humanos: {done.Count(m => m.Players.All(p => !p.IsAI))}.\n");
            int dropped = all.Sum(m => m.Dropped);
            if (dropped > 0) sb.Append($"- ⚠ {dropped} sucesos no cupieron en el búfer (partidas muy largas).\n");
            sb.Append($"- Las filas con menos de {SmallSample} apariciones llevan ⚠: muestra pequeña.\n\n");
            if (done.Count == 0) return sb.ToString();

            sb.Append("**Cómo leerlo:** *V/E* = victorias / victorias esperadas (1 = justo; >1,2 o <0,8 con muestra suficiente " +
                      "merece mirarlo). *Hecho* y *recibido* = RPM por minuto de partida (de rivales). *Golpes* = % de choques " +
                      "ganados (hace más daño del que recibe).\n\n");

            Section(sb, "Por arquetipo", rows, p => Get(p.Info, "archetype"));
            Section(sb, "Por preset (piezas sin cambiar)", rows, p => Get(p.Info, "preset"));
            Section(sb, "Por poder", rows, p => Get(p.Info, "special"));
            PowerUse(sb, rows);
            Section(sb, "Por control (humano o nivel de IA), solo partidas mixtas",
                done.Where(m => m.Players.Any(p => p.IsAI) && m.Players.Any(p => !p.IsAI)).SelectMany(m => m.Players).ToList(),
                p => p.Control);
            Section(sb, "IA contra IA, por nivel (partidas sin humanos)",
                done.Where(m => m.Players.All(p => p.IsAI)).SelectMany(m => m.Players).ToList(), p => p.Control);
            Section(sb, "Puntas", rows, p => Get(p.Info, "tip"));
            Section(sb, "Cuerpos", rows, p => Get(p.Info, "body"));
            Section(sb, "Discos", rows, p => Get(p.Info, "blade"));
            Section(sb, "Núcleos", rows, p => Get(p.Info, "core"));
            Duels(sb, done);
            DamageSources(sb, rows);
            return sb.ToString();
        }

        private static void Section(StringBuilder sb, string title, List<PlayerRow> rows, Func<PlayerRow, string> key)
        {
            sb.Append($"## {title}\n\n");
            if (rows.Count == 0)
            {
                sb.Append("Sin datos.\n\n");
                return;
            }
            sb.Append("| | n | Victorias | V/E | Hecho/min | Recibido/min | Hecho/recibido | K.O./partida | Autoelim./partida | Golpes | Parrys/partida | Pared % |\n");
            sb.Append("|---|---|---|---|---|---|---|---|---|---|---|---|\n");
            foreach (IGrouping<string, PlayerRow> g in rows.GroupBy(key).OrderByDescending(g => g.Count()))
            {
                int n = g.Count();
                float wins = g.Count(p => p.Winner);
                float expected = g.Sum(p => p.Match.ExpectedWin);
                float minutes = Mathf.Max(g.Sum(p => p.Match.Duration) / 60f, 0.0001f);
                float dealt = g.Sum(p => p.S("damageDealt"));
                float taken = g.Sum(p => p.S("damageTakenRival"));
                float wall = g.Sum(p => p.S("damageTakenWall"));
                float landed = g.Sum(p => p.S("hitsLanded"));
                float received = g.Sum(p => p.S("hitsReceived"));
                sb.Append($"| {(string.IsNullOrEmpty(g.Key) ? "-" : g.Key)}{(n < SmallSample ? " ⚠" : "")} | {n} | {wins:0} | " +
                          $"{Ratio(wins, expected)} | {dealt / minutes:0} | {taken / minutes:0} | {Ratio(dealt, taken)} | " +
                          $"{g.Average(p => p.S("kos")):0.##} | {g.Average(p => p.S("selfKOs")):0.##} | " +
                          $"{Pct(landed, landed + received)} | {g.Average(p => p.S("parries")):0.#} | {Pct(wall, wall + taken)} |\n");
            }
            sb.Append('\n');
        }

        /// <summary>Uso de cada poder: cuántas veces, cuánto tiempo, cuánto cura y si se lo cortan.</summary>
        private static void PowerUse(StringBuilder sb, List<PlayerRow> rows)
        {
            sb.Append("### Uso de los poderes\n\n");
            sb.Append("| Poder | n | Usos/min | Tiempo activo % | Curado/uso | Curas cortadas/uso | Estados causados/min | Energía necesaria |\n");
            sb.Append("|---|---|---|---|---|---|---|---|\n");
            foreach (IGrouping<string, PlayerRow> g in rows.GroupBy(p => Get(p.Info, "special")).OrderByDescending(g => g.Count()))
            {
                float alive = Mathf.Max(g.Sum(p => p.S("aliveTime")), 0.0001f);
                float uses = g.Sum(p => p.S("specials"));
                sb.Append($"| {g.Key}{(g.Count() < SmallSample ? " ⚠" : "")} | {g.Count()} | {uses / (alive / 60f):0.##} | " +
                          $"{Pct(g.Sum(p => p.S("specialTime")), alive)} | {(uses > 0 ? g.Sum(p => p.S("healed")) / uses : 0f):0} | " +
                          $"{(uses > 0 ? g.Sum(p => p.S("healCuts")) / uses : 0f):0.##} | " +
                          $"{g.Sum(p => p.S("statusInflicted")) / (alive / 60f):0.##} | " +
                          $"{string.Join(", ", g.Select(p => Get(p.Info, "energyRequired")).Distinct())} |\n");
            }
            sb.Append('\n');
        }

        /// <summary>Duelos de 1 contra 1: victorias del arquetipo de la fila contra el de la columna.</summary>
        private static void Duels(StringBuilder sb, List<Match> done)
        {
            List<Match> duels = done.Where(m => m.Players.Count == 2 && !m.Draw).ToList();
            sb.Append("## Duelos 1 contra 1 por arquetipo\n\n");
            if (duels.Count == 0)
            {
                sb.Append("Sin duelos.\n\n");
                return;
            }
            List<string> types = duels.SelectMany(m => m.Players).Select(p => Get(p.Info, "archetype")).Distinct().OrderBy(t => t).ToList();
            sb.Append("Victorias de la fila contra la columna (victorias / duelos). Espejos fuera.\n\n");
            sb.Append("| | ").Append(string.Join(" | ", types)).Append(" |\n|---|").Append(string.Concat(types.Select(_ => "---|"))).Append('\n');
            foreach (string row in types)
            {
                sb.Append($"| {row} |");
                foreach (string col in types)
                {
                    if (row == col)
                    {
                        sb.Append(" - |");
                        continue;
                    }
                    int total = 0, wins = 0;
                    foreach (Match m in duels)
                    {
                        PlayerRow a = m.Players.FirstOrDefault(p => Get(p.Info, "archetype") == row);
                        PlayerRow b = m.Players.FirstOrDefault(p => p != a && Get(p.Info, "archetype") == col);
                        if (a == null || b == null) continue;
                        total++;
                        if (a.Winner) wins++;
                    }
                    sb.Append(total > 0 ? $" {wins}/{total} |" : " |");
                }
                sb.Append('\n');
            }
            sb.Append('\n');
        }

        /// <summary>De dónde sale el daño: choques, poderes y estados (y paredes de una lanzada), y paredes.</summary>
        private static void DamageSources(StringBuilder sb, List<PlayerRow> rows)
        {
            float clash = rows.Sum(p => p.S("clashDamageTaken"));
            float rival = rows.Sum(p => p.S("damageTakenRival"));
            float wall = rows.Sum(p => p.S("damageTakenWall"));
            float total = Mathf.Max(rival + wall, 0.0001f);
            sb.Append("## Origen del daño recibido\n\n");
            sb.Append($"- Choques entre peonzas: {Pct(clash, total)}\n");
            sb.Append($"- Poderes y estados de un rival (quemadura, onda, pared tras una lanzada...): {Pct(Mathf.Max(0f, rival - clash), total)}\n");
            sb.Append($"- Paredes sin rival: {Pct(wall, total)}\n\n");
        }

        private static string Ratio(float a, float b) => b > 0.0001f ? (a / b).ToString("0.00", Inv) : "-";

        private static string Pct(float part, float total) => total > 0.0001f ? (100f * part / total).ToString("0", Inv) + "%" : "-";

        private static string Counts(IEnumerable<Match> matches, Func<Match, string> key) =>
            string.Join(", ", matches.GroupBy(key).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}"));

        private static float Median(IEnumerable<float> values)
        {
            List<float> list = values.OrderBy(v => v).ToList();
            if (list.Count == 0) return 0f;
            int mid = list.Count / 2;
            return list.Count % 2 == 1 ? list[mid] : (list[mid - 1] + list[mid]) * 0.5f;
        }
        #endregion
    }
}
