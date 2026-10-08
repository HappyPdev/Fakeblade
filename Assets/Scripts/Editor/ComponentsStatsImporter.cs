using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Importa los cambios hechos en "Estadísticas de las piezas.html" (botón Descargar cambios →
    /// fakeblade-piezas.json) a las piezas de ComponentsData. Busca cada pieza por su GUID, enseña los
    /// cambios antes de aplicarlos, avisa si la pieza cambió en Unity después de exportar, se puede deshacer
    /// (Ctrl+Z) y al terminar vuelve a exportar las estadísticas.
    /// </summary>
    public static class ComponentsStatsImporter
    {
        private const int MaxLinesInDialog = 18;

        #region Formato del JSON (mismos nombres que escribe el HTML)
        [Serializable] private class ImportFile { public int version; public string exported; public ImportPart[] parts; }

        [Serializable]
        private class ImportPart
        {
            public string guid;
            public string name;
            public Stats original;
            public Stats values;
            public Trait[] originalTraits;
            public Trait[] traits;
        }

        [Serializable]
        private class Stats
        {
            public float maxSpin, spinDecay, moveSpeed, weight, attack, defense, dash, parry;
            public int charges;
        }

        [Serializable] private class Trait { public int type; public float percent; }
        #endregion

        /// <summary>Campo de estadística: nombre en el JSON, campo serializado de la pieza y nombre para mostrar.</summary>
        private static readonly (string json, string field, string label)[] Fields =
        {
            ("maxSpin", "maxSpinModifier", "RPM"), ("spinDecay", "spinDecayModifier", "Desgaste"),
            ("moveSpeed", "moveSpeedModifier", "Velocidad"), ("weight", "weightModifier", "Peso"),
            ("attack", "attackPowerModifier", "Ataque"), ("defense", "defenseModifier", "Defensa"),
            ("dash", "dashForceModifier", "Dash"), ("charges", "attackChargesModifier", "Cargas"),
            ("parry", "parryWindowModifier", "Parry"),
        };

        private struct Change
        {
            public FakeBladeComponentData Part;
            public string Field;     // campo serializado, o "traits.Array.data[i].percent"
            public string Label;
            public float From, To;
            public bool IsInt;
            public bool Conflict;    // la pieza tenía otro valor en Unity que el que se exportó
        }

        [MenuItem("FakeBlade/Import ComponentsData Stats")]
        public static void ImportMenu()
        {
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string path = EditorUtility.OpenFilePanel("Cambios de las piezas (Descargar cambios en el HTML)",
                Directory.Exists(downloads) ? downloads : "", "json");
            if (string.IsNullOrEmpty(path)) return;
            Import(path, true);
        }

        /// <summary>Aplica un archivo de cambios. Con ask = false aplica sin preguntar (pruebas).</summary>
        public static string Import(string path, bool ask)
        {
            ImportFile file;
            try { file = JsonUtility.FromJson<ImportFile>(File.ReadAllText(path)); }
            catch (Exception e) { return Fail($"No se puede leer el archivo: {e.Message}", ask); }
            if (file == null || file.parts == null) return Fail("El archivo no tiene piezas.", ask);

            var changes = new List<Change>();
            var warnings = new List<string>();
            foreach (ImportPart ip in file.parts)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(ip.guid);
                var part = string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>(assetPath);
                if (part == null)
                {
                    warnings.Add($"No existe la pieza {ip.name} ({ip.guid}): se ignora.");
                    continue;
                }
                CollectStats(part, ip, changes);
                CollectTraits(part, ip, changes, warnings);
            }

            if (changes.Count == 0)
            {
                string msg = "No hay cambios: las piezas ya tienen esos valores." + (warnings.Count > 0 ? "\n\n" + string.Join("\n", warnings) : "");
                if (ask) EditorUtility.DisplayDialog("Importar estadísticas", msg, "Vale");
                return msg;
            }

            string summary = Summary(changes, warnings, file.exported);
            Debug.Log("[FakeBlade] Importar estadísticas:\n" + summary);
            if (ask && !EditorUtility.DisplayDialog("Importar estadísticas", summary, "Aplicar", "Cancelar"))
                return "Cancelado.";

            Apply(changes);
            ComponentsStatsExporter.Export(); // documentos al día con los valores nuevos
            string done = $"Aplicados {changes.Count} cambios en {CountParts(changes)} piezas. Ctrl+Z para deshacer.";
            Debug.Log("[FakeBlade] " + done);
            if (ask) EditorUtility.DisplayDialog("Importar estadísticas", done + "\nLas estadísticas se han vuelto a exportar.", "Vale");
            return done + "\n" + summary;
        }

        #region Cambios
        private static void CollectStats(FakeBladeComponentData part, ImportPart ip, List<Change> changes)
        {
            if (ip.values == null) return;
            var so = new SerializedObject(part);
            foreach (var f in Fields)
            {
                SerializedProperty prop = so.FindProperty(f.field);
                if (prop == null) continue;
                bool isInt = prop.propertyType == SerializedPropertyType.Integer;
                float current = isInt ? prop.intValue : prop.floatValue;
                float target = Get(ip.values, f.json), exported = ip.original != null ? Get(ip.original, f.json) : current;
                if (isInt) target = Mathf.Round(target);
                if (Mathf.Abs(target - current) < 1e-5f) continue;
                changes.Add(new Change
                {
                    Part = part, Field = f.field, Label = f.label, From = current, To = target, IsInt = isInt,
                    Conflict = Mathf.Abs(current - exported) > 1e-5f
                });
            }
        }

        private static void CollectTraits(FakeBladeComponentData part, ImportPart ip, List<Change> changes, List<string> warnings)
        {
            if (ip.traits == null) return;
            var current = part.Traits;
            if (current.Count != ip.traits.Length)
            {
                if (ip.traits.Length > 0 || current.Count > 0)
                    warnings.Add($"{part.ComponentName}: sus rasgos cambiaron en Unity (añadidos o quitados) desde que exportaste; sus rasgos no se tocan.");
                return;
            }
            for (int i = 0; i < current.Count; i++)
            {
                if ((int)current[i].type != ip.traits[i].type)
                {
                    warnings.Add($"{part.ComponentName}: el rasgo {i + 1} ya no es del mismo tipo en Unity; sus rasgos no se tocan.");
                    return;
                }
            }
            for (int i = 0; i < current.Count; i++)
            {
                float from = current[i].percent, to = ip.traits[i].percent;
                if (Mathf.Abs(from - to) < 1e-5f) continue;
                float exported = ip.originalTraits != null && i < ip.originalTraits.Length ? ip.originalTraits[i].percent : from;
                changes.Add(new Change
                {
                    Part = part, Field = $"traits.Array.data[{i}].percent",
                    Label = "Rasgo " + ComponentsStatsExporter.TraitName(current[i].type), From = from, To = to,
                    Conflict = Mathf.Abs(from - exported) > 1e-5f
                });
            }
        }

        private static void Apply(List<Change> changes)
        {
            var byPart = new Dictionary<FakeBladeComponentData, List<Change>>();
            foreach (var c in changes)
            {
                if (!byPart.TryGetValue(c.Part, out var list)) byPart[c.Part] = list = new List<Change>();
                list.Add(c);
            }
            Undo.SetCurrentGroupName("Importar estadísticas de piezas");
            int group = Undo.GetCurrentGroup();
            foreach (var pair in byPart)
            {
                Undo.RecordObject(pair.Key, "Importar estadísticas de piezas");
                var so = new SerializedObject(pair.Key);
                foreach (var c in pair.Value)
                {
                    SerializedProperty prop = so.FindProperty(c.Field);
                    if (prop == null) continue;
                    if (c.IsInt) prop.intValue = Mathf.RoundToInt(c.To);
                    else prop.floatValue = c.To;
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(pair.Key);
            }
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
        }
        #endregion

        #region Textos
        private static string Summary(List<Change> changes, List<string> warnings, string exported)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{changes.Count} cambios en {CountParts(changes)} piezas (HTML exportado el {exported}):");
            sb.AppendLine();
            int shown = 0;
            foreach (var c in changes)
            {
                if (shown++ >= MaxLinesInDialog) { sb.AppendLine($"… y {changes.Count - MaxLinesInDialog} más (lista completa en la consola)."); break; }
                bool trait = c.Field.StartsWith("traits");
                string from = trait ? ComponentsStatsExporter.Percent(c.From) : Num(c.From);
                string to = trait ? ComponentsStatsExporter.Percent(c.To) : Num(c.To);
                sb.AppendLine($"• {c.Part.ComponentName} · {c.Label}: {from} → {to}{(c.Conflict ? "  ⚠" : "")}");
            }
            int conflicts = changes.FindAll(c => c.Conflict).Count;
            if (conflicts > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"⚠ {conflicts} valores habían cambiado en Unity después de exportar el HTML: se sobrescriben con los del HTML.");
            }
            if (warnings.Count > 0)
            {
                sb.AppendLine();
                foreach (string w in warnings) sb.AppendLine("• " + w);
            }
            return sb.ToString();
        }

        private static int CountParts(List<Change> changes)
        {
            var set = new HashSet<FakeBladeComponentData>();
            foreach (var c in changes) set.Add(c.Part);
            return set.Count;
        }

        private static string Num(float v) => ComponentsStatsExporter.S(v);

        private static float Get(Stats s, string key)
        {
            switch (key)
            {
                case "maxSpin": return s.maxSpin;
                case "spinDecay": return s.spinDecay;
                case "moveSpeed": return s.moveSpeed;
                case "weight": return s.weight;
                case "attack": return s.attack;
                case "defense": return s.defense;
                case "dash": return s.dash;
                case "charges": return s.charges;
                default: return s.parry;
            }
        }

        private static string Fail(string message, bool ask)
        {
            Debug.LogWarning("[FakeBlade] Importar estadísticas: " + message);
            if (ask) EditorUtility.DisplayDialog("Importar estadísticas", message, "Vale");
            return message;
        }
        #endregion
    }
}
