#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace FakeBlade.Core
{
    /// <summary>
    /// Editor personalizado para TestArenaSetup.
    /// Añade botones para generar/limpiar la escena sin ejecutar.
    /// </summary>
    [CustomEditor(typeof(TestArenaSetup))]
    public class TestArenaSetupEditor : Editor
    {
        private TestArenaSetup _target;
        private bool _showGenerateSection = true;
        private bool _showClearSection = true;

        private GUIStyle _headerStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _bigButtonStyle;

        private void OnEnable()
        {
            _target = (TestArenaSetup)target;
        }

        public override void OnInspectorGUI()
        {
            InitStyles();

            // Título principal
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🎮 FAKEBLADE TEST ARENA", _headerStyle);
            EditorGUILayout.Space(5);

            // ═══════════════════════════════════════════════
            // SECCIÓN: GENERAR
            // ═══════════════════════════════════════════════
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _showGenerateSection = EditorGUILayout.Foldout(_showGenerateSection, "⚡ GENERAR ESCENA", true);

            if (_showGenerateSection)
            {
                EditorGUILayout.Space(5);

                // Botón grande: Generar Todo
                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                if (GUILayout.Button("✓ GENERAR TODO", _bigButtonStyle, GUILayout.Height(40)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(_target.gameObject, "Generate All");
                    _target.GenerateAll();
                    MarkSceneDirty();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(5);

                // Botones individuales
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
                if (GUILayout.Button("Arena", _buttonStyle, GUILayout.Height(30)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(_target.gameObject, "Generate Arena");
                    _target.GenerateArena();
                    MarkSceneDirty();
                }

                GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
                if (GUILayout.Button("Jugadores", _buttonStyle, GUILayout.Height(30)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(_target.gameObject, "Generate Players");
                    _target.GeneratePlayers();
                    MarkSceneDirty();
                }

                GUI.backgroundColor = new Color(0.8f, 0.8f, 0.2f);
                if (GUILayout.Button("GameManager", _buttonStyle, GUILayout.Height(30)))
                {
                    _target.GenerateGameManager();
                    MarkSceneDirty();
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // ═══════════════════════════════════════════════
            // SECCIÓN: LIMPIAR
            // ═══════════════════════════════════════════════
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _showClearSection = EditorGUILayout.Foldout(_showClearSection, "🗑 LIMPIAR ESCENA", true);

            if (_showClearSection)
            {
                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("🗑 Limpiar Todo", _buttonStyle, GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Confirmar",
                        "¿Eliminar toda la escena generada?", "Sí", "No"))
                    {
                        Undo.RegisterFullObjectHierarchyUndo(_target.gameObject, "Clear All");
                        _target.ClearAll();
                        MarkSceneDirty();
                    }
                }

                GUI.backgroundColor = new Color(0.9f, 0.6f, 0.6f);
                if (GUILayout.Button("Arena", _buttonStyle, GUILayout.Height(30)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(_target.gameObject, "Clear Arena");
                    _target.ClearArena();
                    MarkSceneDirty();
                }

                if (GUILayout.Button("Jugadores", _buttonStyle, GUILayout.Height(30)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(_target.gameObject, "Clear Players");
                    _target.ClearPlayers();
                    MarkSceneDirty();
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // ═══════════════════════════════════════════════
            // INFO
            // ═══════════════════════════════════════════════
            EditorGUILayout.HelpBox(
                "INSTRUCCIONES:\n" +
                "1. Configura los parámetros abajo\n" +
                "2. Arrastra tus prefabs de peonzas (opcional)\n" +
                "3. Pulsa 'GENERAR TODO'\n" +
                "4. Dale a Play para probar\n\n" +
                "Controles: WASD + Space + Shift | R = Reset",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Dibujar el inspector por defecto
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("⚙ CONFIGURACIÓN", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawDefaultInspector();
        }

        private void InitStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.2f, 0.6f, 1f) }
                };
            }

            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                };
            }

            if (_bigButtonStyle == null)
            {
                _bigButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        private void MarkSceneDirty()
        {
            if (!Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }
        }

        // Dibujar gizmos en la escena
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void DrawArenaGizmo(TestArenaSetup setup, GizmoType gizmoType)
        {
            if (setup == null) return;

            // Dibujar círculo de la arena
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.5f);
            DrawWireCircle(setup.transform.position, setup.ArenaRadius, 32);

            // Dibujar posiciones de spawn
            float spawnRadius = setup.ArenaRadius * 0.5f;
            float angleStep = 360f / setup.NumberOfPlayers;

            for (int i = 0; i < setup.NumberOfPlayers; i++)
            {
                float angle = (i * angleStep + 90f) * Mathf.Deg2Rad;
                Vector3 pos = setup.transform.position + new Vector3(
                    Mathf.Cos(angle) * spawnRadius,
                    0.5f,
                    Mathf.Sin(angle) * spawnRadius
                );

                Color color = setup.PlayerColors != null && i < setup.PlayerColors.Length
                    ? setup.PlayerColors[i]
                    : Color.white;

                Gizmos.color = color;
                Gizmos.DrawWireSphere(pos, 0.5f);
                Gizmos.DrawLine(pos, pos + Vector3.up * 0.5f);

                // Etiqueta
#if UNITY_EDITOR
                Handles.Label(pos + Vector3.up * 1.2f, $"P{i + 1}");
#endif
            }
        }

        private static void DrawWireCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 nextPoint = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0,
                    Mathf.Sin(angle) * radius
                );
                Gizmos.DrawLine(prevPoint, nextPoint);
                prevPoint = nextPoint;
            }
        }
    }
}
#endif