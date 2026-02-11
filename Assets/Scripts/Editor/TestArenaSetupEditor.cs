using UnityEngine;
using UnityEditor;

namespace FakeBlade.Core
{
    [CustomEditor(typeof(TestArenaSetup))]
    public class TestArenaSetupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            TestArenaSetup setup = (TestArenaSetup)target;

            EditorGUILayout.Space(15);

            // ============================================================
            // GENERAR TODO
            // ============================================================
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("GENERACIÓN DE ESCENA", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
            if (GUILayout.Button("▶ GENERAR TODO", GUILayout.Height(35)))
            {
                Undo.RegisterFullObjectHierarchyUndo(setup.gameObject, "Generate All");
                setup.GenerateAll();
                EditorUtility.SetDirty(setup);
            }

            EditorGUILayout.Space(5);

            // ============================================================
            // BOTONES INDIVIDUALES
            // ============================================================
            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🏟️ Generar Arena", GUILayout.Height(28)))
            {
                Undo.RegisterFullObjectHierarchyUndo(setup.gameObject, "Generate Arena");
                setup.GenerateArena();
                EditorUtility.SetDirty(setup);
            }
            if (GUILayout.Button("📍 Generar Spawns", GUILayout.Height(28)))
            {
                Undo.RegisterFullObjectHierarchyUndo(setup.gameObject, "Generate Spawn Points");
                setup.GenerateSpawnPoints();
                EditorUtility.SetDirty(setup);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎮 Generar Jugadores", GUILayout.Height(28)))
            {
                Undo.RegisterFullObjectHierarchyUndo(setup.gameObject, "Generate Players");
                setup.GeneratePlayers();
                EditorUtility.SetDirty(setup);
            }
            if (GUILayout.Button("⚙️ Generar GameManager", GUILayout.Height(28)))
            {
                Undo.RegisterFullObjectHierarchyUndo(setup.gameObject, "Generate GameManager");
                setup.GenerateGameManager();
                EditorUtility.SetDirty(setup);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // ============================================================
            // LIMPIAR
            // ============================================================
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("🗑️ LIMPIAR TODO", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Confirmar",
                    "¿Eliminar toda la escena generada?", "Sí", "No"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(setup.gameObject, "Clear All");
                    setup.ClearAll();
                    EditorUtility.SetDirty(setup);
                }
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            // ============================================================
            // INFO
            // ============================================================
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("INFORMACIÓN", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                $"Arena: Radio {setup.ArenaRadius}m\n" +
                $"Jugadores: {setup.NumberOfPlayers}\n" +
                $"Spawn Radio: {setup.ArenaRadius * setup.SpawnRadiusPercent:F1}m ({setup.SpawnRadiusPercent * 100:F0}%)\n" +
                $"Prefabs asignados: {CountPrefabs(setup)}/{setup.NumberOfPlayers}\n" +
                $"Spawn Points: {(setup.GeneratedSpawnPoints != null ? setup.GeneratedSpawnPoints.Length : 0)}",
                MessageType.Info);

            // Estado de generados
            bool hasArena = setup.transform.Find("Arena") != null;
            bool hasSpawns = setup.transform.Find("SpawnPoints") != null;
            bool hasPlayers = setup.GeneratedPlayers != null && setup.GeneratedPlayers.Length > 0;

            EditorGUILayout.BeginHorizontal();
            DrawStatusLabel("Arena", hasArena);
            DrawStatusLabel("Spawns", hasSpawns);
            DrawStatusLabel("Players", hasPlayers);
            EditorGUILayout.EndHorizontal();

            // Spawn points info
            if (setup.GeneratedSpawnPoints != null && setup.GeneratedSpawnPoints.Length > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Spawn Points:", EditorStyles.miniLabel);
                for (int i = 0; i < setup.GeneratedSpawnPoints.Length; i++)
                {
                    if (setup.GeneratedSpawnPoints[i] == null) continue;
                    Vector3 pos = setup.GeneratedSpawnPoints[i].position;
                    Color c = setup.PlayerColors[i % setup.PlayerColors.Length];

                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.DrawRect(EditorGUILayout.GetControlRect(GUILayout.Width(14), GUILayout.Height(14)), c);
                    EditorGUILayout.LabelField($"P{i + 1}: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private int CountPrefabs(TestArenaSetup setup)
        {
            if (setup.FakeBladePrefabs == null) return 0;
            int count = 0;
            foreach (var p in setup.FakeBladePrefabs)
            {
                if (p != null) count++;
            }
            return count;
        }

        private void DrawStatusLabel(string name, bool active)
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
            style.normal.textColor = active ? Color.green : Color.gray;
            style.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            EditorGUILayout.LabelField(active ? $"✓ {name}" : $"✗ {name}", style);
        }
    }
}