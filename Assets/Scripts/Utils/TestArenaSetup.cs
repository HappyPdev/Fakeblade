using UnityEngine;
using System.Collections.Generic;

namespace FakeBlade.Core
{
    /// <summary>
    /// Generador de arena de prueba con botones de Editor.
    /// Usa los botones en el Inspector para generar la escena sin ejecutar.
    /// 
    /// FEATURES:
    /// - Botones de Editor: Generar Todo, Arena, Jugadores, Spawn Points, Limpiar
    /// - Soporte de prefabs con/sin componentes pre-existentes
    /// - Sistema de Spawn Points dinámico basado en número de jugadores
    /// - Loadouts de test con componentes equipados
    /// - HUD en runtime: controles, spin%, velocidad, peso
    /// - Gizmos para visualizar arena y spawn points
    /// </summary>
    public class TestArenaSetup : MonoBehaviour
    {
        #region Serialized Fields

        [Header("=== PREFABS DE PEONZAS ===")]
        [Tooltip("Arrastra tus prefabs de peonzas aquí. Si están vacíos se genera geometría básica.")]
        [SerializeField] private GameObject[] fakeBladePrefabs;

        [Header("=== ARENA ===")]
        [SerializeField] private float arenaRadius = 10f;
        [SerializeField] private float wallHeight = 1.5f;
        [SerializeField] private int wallSegments = 24;
        [SerializeField] private float wallThickness = 0.3f;

        [Header("=== JUGADORES ===")]
        [SerializeField] private int numberOfPlayers = 2;
        [SerializeField] private float spawnHeight = 0.3f;

        [Header("=== SPAWN POINTS ===")]
        [Tooltip("Radio como porcentaje del radio de la arena (0.3 = 30% del radio)")]
        [SerializeField][Range(0.2f, 0.8f)] private float spawnRadiusPercent = 0.5f;
        [Tooltip("Offset angular en grados para rotar todos los spawn points")]
        [SerializeField] private float spawnAngleOffset = 90f;

        [Header("=== LOADOUTS DE TEST ===")]
        [Tooltip("Componentes equipados para cada jugador durante el test.")]
        [SerializeField] private TestLoadout[] playerLoadouts;

        [Header("=== COLORES ===")]
        [SerializeField]
        private Color[] playerColors = new Color[]
        {
            new Color(0.2f, 0.6f, 1f),
            new Color(1f, 0.3f, 0.3f),
            new Color(0.3f, 1f, 0.4f),
            new Color(1f, 0.9f, 0.2f)
        };

        [Header("=== FÍSICA ===")]
        [SerializeField] private float groundFriction = 0.5f;
        [SerializeField] private float wallFriction = 0.05f;
        [SerializeField] private float wallBounce = 0.4f;

        [Header("=== HUD EN RUNTIME ===")]
        [SerializeField] private bool showControlsOnScreen = true;
        [SerializeField] private bool showStatsOnScreen = true;

        [Header("=== REFERENCIAS GENERADAS ===")]
        [SerializeField] private GameObject generatedArena;
        [SerializeField] private GameObject[] generatedPlayers;
        [SerializeField] private GameObject generatedSpawnPointsParent;
        [SerializeField] private Transform[] generatedSpawnPoints;

        #endregion

        #region Test Loadout
        [System.Serializable]
        public class TestLoadout
        {
            public FakeBladeComponentData tip;
            public FakeBladeComponentData body;
            public FakeBladeComponentData blade;
            public FakeBladeComponentData core;
        }
        #endregion

        #region Public Accessors (para el Editor)
        public float ArenaRadius => arenaRadius;
        public int NumberOfPlayers => numberOfPlayers;
        public GameObject[] FakeBladePrefabs => fakeBladePrefabs;
        public Color[] PlayerColors => playerColors;
        public float SpawnRadiusPercent => spawnRadiusPercent;
        public float SpawnAngleOffset => spawnAngleOffset;
        public float SpawnHeight => spawnHeight;
        public Transform[] GeneratedSpawnPoints => generatedSpawnPoints;
        public GameObject[] GeneratedPlayers => generatedPlayers;
        #endregion

        // =====================================================================
        // BOTONES DEL EDITOR (llamados desde TestArenaSetupEditor)
        // =====================================================================

        #region Editor Buttons

        public void GenerateAll()
        {
            ClearAll();
            GenerateArena();
            GenerateSpawnPoints();
            GeneratePlayers();
            GenerateGameManager();
            Debug.Log("✓ Escena generada completamente");
        }

        public void GenerateArena()
        {
            ClearArena();

            generatedArena = new GameObject("Arena");
            generatedArena.transform.parent = transform;

            CreateFloor();
            CreateWalls();

            Debug.Log("✓ Arena generada");
        }

        public void GenerateSpawnPoints()
        {
            ClearSpawnPoints();

            generatedSpawnPointsParent = new GameObject("SpawnPoints");
            generatedSpawnPointsParent.transform.parent = transform;

            float spawnRadius = arenaRadius * spawnRadiusPercent;
            float angleStep = 360f / numberOfPlayers;

            generatedSpawnPoints = new Transform[numberOfPlayers];

            for (int i = 0; i < numberOfPlayers; i++)
            {
                float angle = (i * angleStep + spawnAngleOffset) * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * spawnRadius,
                    spawnHeight,
                    Mathf.Sin(angle) * spawnRadius
                );

                GameObject spawnPoint = new GameObject($"SpawnPoint_P{i}");
                spawnPoint.transform.parent = generatedSpawnPointsParent.transform;
                spawnPoint.transform.position = pos;

                // Mirar hacia el centro de la arena
                spawnPoint.transform.LookAt(new Vector3(0, spawnHeight, 0));

                generatedSpawnPoints[i] = spawnPoint.transform;
            }

            Debug.Log($"✓ {numberOfPlayers} spawn points generados (radio: {spawnRadius:F1}m)");
        }

        public void GeneratePlayers()
        {
            ClearPlayers();

            // Si no hay spawn points, generarlos primero
            if (generatedSpawnPoints == null || generatedSpawnPoints.Length < numberOfPlayers)
            {
                GenerateSpawnPoints();
            }

            generatedPlayers = new GameObject[numberOfPlayers];

            for (int i = 0; i < numberOfPlayers; i++)
            {
                Vector3 spawnPos = generatedSpawnPoints[i].position;
                generatedPlayers[i] = CreateFakeBlade(i, spawnPos);
            }

            Debug.Log($"✓ {numberOfPlayers} jugadores generados");
        }

        public void GenerateGameManager()
        {
            ClearGameManager();
            GameObject gmObj = new GameObject("[GameManager]");
            gmObj.transform.parent = transform;
            gmObj.AddComponent<GameManager>();
            Debug.Log("✓ GameManager generado");
        }

        public void ClearAll()
        {
            ClearPlayers();
            ClearSpawnPoints();
            ClearArena();
            ClearGameManager();
            Debug.Log("✓ Escena limpiada");
        }

        public void ClearArena()
        {
            if (generatedArena != null)
            {
                DestroyImmediate(generatedArena);
                generatedArena = null;
            }
        }

        public void ClearPlayers()
        {
            if (generatedPlayers != null)
            {
                foreach (var player in generatedPlayers)
                {
                    if (player != null) DestroyImmediate(player);
                }
                generatedPlayers = null;
            }
        }

        public void ClearSpawnPoints()
        {
            if (generatedSpawnPointsParent != null)
            {
                DestroyImmediate(generatedSpawnPointsParent);
                generatedSpawnPointsParent = null;
            }
            generatedSpawnPoints = null;
        }

        private void ClearGameManager()
        {
            // Buscar como hijo primero
            var gmTransform = transform.Find("[GameManager]");
            if (gmTransform != null)
            {
                DestroyImmediate(gmTransform.gameObject);
            }

            // Buscar global por si acaso
            var existingGM = FindFirstObjectByType<GameManager>();
            if (existingGM != null)
            {
                DestroyImmediate(existingGM.gameObject);
            }
        }

        #endregion

        // =====================================================================
        // RUNTIME (se ejecuta al dar Play)
        // =====================================================================

        #region Runtime

        private void Start()
        {
            // Si no se generó en el editor, generar en runtime
            if (generatedArena == null)
            {
                GenerateArena();
            }

            if (generatedSpawnPoints == null || generatedSpawnPoints.Length == 0)
            {
                GenerateSpawnPoints();
            }

            if (generatedPlayers == null || generatedPlayers.Length == 0)
            {
                GeneratePlayers();
            }

            SetupRuntimeCamera();

            if (GameManager.Instance == null)
            {
                GenerateGameManager();
            }

            Invoke(nameof(DelayedStartMatch), 0.5f);

            Debug.Log("=== TEST ARENA READY ===");
            Debug.Log("Player 1: WASD + Space (dash) + Shift (special)");
            Debug.Log("Player 2+: Gamepad - Left Stick + A/RT (dash) + X/LT (special)");
            Debug.Log("R: Reset | ESC: Quit");
        }

        private void DelayedStartMatch()
        {
            GameManager.Instance?.ChangeState(GameManager.GameState.InMatch);
        }

        private void SetupRuntimeCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                mainCam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            float height = arenaRadius * 1.2f;
            float distance = arenaRadius * 0.5f;
            mainCam.transform.position = new Vector3(0, height, -distance);
            mainCam.transform.LookAt(Vector3.zero);
            mainCam.fieldOfView = 60f;

            SimpleCameraFollow camFollow = mainCam.GetComponent<SimpleCameraFollow>();
            if (camFollow == null)
                camFollow = mainCam.gameObject.AddComponent<SimpleCameraFollow>();

            if (generatedPlayers != null)
                camFollow.SetTargets(generatedPlayers);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetMatch();
            }
        }

        private void ResetMatch()
        {
            if (generatedPlayers == null) return;

            for (int i = 0; i < generatedPlayers.Length; i++)
            {
                if (generatedPlayers[i] == null) continue;

                // Usar spawn points si existen
                Vector3 spawnPos;
                if (generatedSpawnPoints != null && i < generatedSpawnPoints.Length && generatedSpawnPoints[i] != null)
                {
                    spawnPos = generatedSpawnPoints[i].position;
                }
                else
                {
                    float angle = (i * (360f / numberOfPlayers) + spawnAngleOffset) * Mathf.Deg2Rad;
                    float spawnRadius = arenaRadius * spawnRadiusPercent;
                    spawnPos = new Vector3(Mathf.Cos(angle) * spawnRadius, spawnHeight, Mathf.Sin(angle) * spawnRadius);
                }

                generatedPlayers[i].transform.position = spawnPos;
                generatedPlayers[i].transform.rotation = Quaternion.identity;
                generatedPlayers[i].SetActive(true);

                var rb = generatedPlayers[i].GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                generatedPlayers[i].GetComponent<FakeBladeController>()?.ResetFakeBlade();
                generatedPlayers[i].GetComponent<PlayerController>()?.ResetPlayer();
            }

            Debug.Log("=== Match Reset! ===");
        }

        #endregion

        // =====================================================================
        // HUD RUNTIME
        // =====================================================================

        #region Runtime GUI

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            if (generatedPlayers == null) return;

            float y = 10f;

            // Panel de controles
            if (showControlsOnScreen)
            {
                GUIStyle controlStyle = new GUIStyle(GUI.skin.box);
                controlStyle.fontSize = 12;
                controlStyle.alignment = TextAnchor.UpperLeft;

                string controls =
                    "=== CONTROLS ===\n" +
                    "P1: WASD + Space + Shift\n" +
                    "P2+: Gamepad LStick + A/RT + X/LT\n" +
                    "R: Reset | ESC: Quit";

                GUI.Box(new Rect(10, y, 230, 80), controls, controlStyle);
                y += 90;
            }

            // Stats de cada jugador
            if (showStatsOnScreen)
            {
                for (int i = 0; i < generatedPlayers.Length; i++)
                {
                    if (generatedPlayers[i] == null) continue;

                    var controller = generatedPlayers[i].GetComponent<FakeBladeController>();
                    var stats = generatedPlayers[i].GetComponent<FakeBladeStats>();
                    if (controller == null) continue;

                    float spin = controller.SpinSpeedPercentage;
                    float speed = controller.Velocity.magnitude;
                    float weight = stats != null ? stats.Weight : 1f;
                    bool destroyed = controller.IsDestroyed;

                    // Color del jugador
                    Color pColor = playerColors[i % playerColors.Length];
                    if (destroyed) pColor *= 0.4f;

                    // Info text
                    string status = destroyed ? " [ELIMINATED]" : "";
                    string info = $"P{i + 1}: Spin {spin * 100:F0}% | Spd {speed:F1} | W:{weight:F1}{status}";

                    // Fondo
                    GUI.color = new Color(0, 0, 0, 0.6f);
                    GUI.Box(new Rect(10, y, 280, 28), "");

                    // Barra de spin (fondo)
                    GUI.color = new Color(pColor.r * 0.3f, pColor.g * 0.3f, pColor.b * 0.3f, 0.8f);
                    GUI.Box(new Rect(10, y, 280, 28), "");

                    // Barra de spin (progreso)
                    GUI.color = new Color(pColor.r, pColor.g, pColor.b, 0.9f);
                    GUI.Box(new Rect(10, y, 280 * spin, 28), "");

                    // Texto
                    GUIStyle textStyle = new GUIStyle(GUI.skin.label);
                    textStyle.fontStyle = FontStyle.Bold;
                    textStyle.fontSize = 13;
                    textStyle.normal.textColor = Color.white;
                    textStyle.alignment = TextAnchor.MiddleLeft;
                    GUI.Label(new Rect(15, y, 270, 28), info, textStyle);

                    y += 32f;
                }

                GUI.color = Color.white;
            }
        }

        #endregion

        // =====================================================================
        // ARENA CREATION
        // =====================================================================

        #region Arena Creation

        private void CreateFloor()
        {
            // IMPORTANTE: Usar Cube, NO Cylinder.
            // Un Cylinder tiene bordes curvos que actúan como rampa
            // y lanzan las peonzas lateralmente al hacer contacto con el borde.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.parent = generatedArena.transform;
            // Cube de 1x1x1, escalamos: ancho x grosor x profundidad
            floor.transform.localScale = new Vector3(arenaRadius * 2.2f, 0.1f, arenaRadius * 2.2f);
            floor.transform.position = new Vector3(0, -0.05f, 0); // Centrar en Y=0 (superficie arriba)
            floor.transform.position = Vector3.zero;
            floor.tag = "Ground";

            floor.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Floor_Mat", new Color(0.85f, 0.85f, 0.9f));
            floor.GetComponent<Collider>().material = CreatePhysicMaterial("Floor_Physics", groundFriction, 0f);
        }

        private void CreateWalls()
        {
            GameObject wallsParent = new GameObject("Walls");
            wallsParent.transform.parent = generatedArena.transform;

            float angleStep = 360f / wallSegments;
            float wallWidth = (2f * Mathf.PI * arenaRadius) / wallSegments * 1.15f;

            Material wallMat = CreateMaterial("Wall_Mat", new Color(0.3f, 0.6f, 1f, 0.3f), true);
            PhysicsMaterial wallPhysMat = CreatePhysicMaterial("Wall_Physics", wallFriction, wallBounce);

            for (int i = 0; i < wallSegments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * (arenaRadius + wallThickness * 0.5f);
                float z = Mathf.Sin(angle) * (arenaRadius + wallThickness * 0.5f);

                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_{i:D2}";
                wall.transform.parent = wallsParent.transform;
                wall.transform.position = new Vector3(x, wallHeight * 0.5f, z);
                wall.transform.localScale = new Vector3(wallWidth, wallHeight, wallThickness);
                wall.transform.LookAt(new Vector3(0, wallHeight * 0.5f, 0));

                wall.GetComponent<Renderer>().sharedMaterial = wallMat;
                wall.GetComponent<BoxCollider>().material = wallPhysMat;
            }
        }

        #endregion

        // =====================================================================
        // PLAYER CREATION
        // =====================================================================

        #region Player Creation

        private GameObject CreateFakeBlade(int playerIndex, Vector3 position)
        {
            GameObject fakeBlade;
            bool usingPrefab = fakeBladePrefabs != null &&
                               fakeBladePrefabs.Length > playerIndex &&
                               fakeBladePrefabs[playerIndex] != null;

            if (usingPrefab)
            {
#if UNITY_EDITOR
                fakeBlade = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(fakeBladePrefabs[playerIndex]);
                fakeBlade.transform.position = position;
#else
                fakeBlade = Instantiate(fakeBladePrefabs[playerIndex], position, Quaternion.identity);
#endif
                fakeBlade.name = $"FakeBlade_Player{playerIndex}";
                fakeBlade.transform.parent = transform;
                EnsureCollider(fakeBlade);
            }
            else
            {
                fakeBlade = CreateBasicFakeBlade(playerIndex, position);
                fakeBlade.transform.parent = transform;
            }

            ConfigureGameComponents(fakeBlade, playerIndex);
            ApplyTestLoadout(fakeBlade, playerIndex);

            return fakeBlade;
        }

        private void EnsureCollider(GameObject fakeBlade)
        {
            Collider col = fakeBlade.GetComponent<Collider>();
            if (col == null) col = fakeBlade.GetComponentInChildren<Collider>();
            if (col == null)
            {
                SphereCollider sphere = fakeBlade.AddComponent<SphereCollider>();
                sphere.radius = 0.45f;
                sphere.center = new Vector3(0, 0.05f, 0);
                sphere.material = CreatePhysicMaterial($"FB_{fakeBlade.name}", 0.1f, 0.3f);
                Debug.Log($"[TestArena] Added SphereCollider to {fakeBlade.name}");
            }
        }

        private void ConfigureGameComponents(GameObject fakeBlade, int playerIndex)
        {
            // Rigidbody
            Rigidbody rb = fakeBlade.GetComponent<Rigidbody>();
            if (rb == null) rb = fakeBlade.AddComponent<Rigidbody>();

            rb.mass = 1.5f;
            rb.linearDamping = 1.5f;   // DEBE coincidir con FakeBladeController.baseDrag
            rb.angularDamping = 0.5f;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.centerOfMass = new Vector3(0, -0.15f, 0);

            // Scripts (solo añadir si no existen)
            if (fakeBlade.GetComponent<FakeBladeStats>() == null)
                fakeBlade.AddComponent<FakeBladeStats>();
            if (fakeBlade.GetComponent<FakeBladeController>() == null)
                fakeBlade.AddComponent<FakeBladeController>();

            InputHandler inputHandler = fakeBlade.GetComponent<InputHandler>();
            if (inputHandler == null) inputHandler = fakeBlade.AddComponent<InputHandler>();

            PlayerController playerController = fakeBlade.GetComponent<PlayerController>();
            if (playerController == null) playerController = fakeBlade.AddComponent<PlayerController>();

            // Configurar
            playerController.SetPlayerID(playerIndex);
            playerController.SetPlayerName($"Player {playerIndex + 1}");
            playerController.SetPlayerColor(playerColors[playerIndex % playerColors.Length]);
            inputHandler.SetGamepadIndex(playerIndex == 0 ? -1 : playerIndex - 1);
        }

        private void ApplyTestLoadout(GameObject fakeBlade, int playerIndex)
        {
            if (playerLoadouts == null || playerIndex >= playerLoadouts.Length) return;
            var loadout = playerLoadouts[playerIndex];
            if (loadout == null) return;

            var stats = fakeBlade.GetComponent<FakeBladeStats>();
            if (stats == null) return;

            if (loadout.tip != null) stats.EquipComponent(loadout.tip);
            if (loadout.body != null) stats.EquipComponent(loadout.body);
            if (loadout.blade != null) stats.EquipComponent(loadout.blade);
            if (loadout.core != null) stats.EquipComponent(loadout.core);

            Debug.Log($"[TestArena] P{playerIndex} loadout: {stats.GetStatsSummary()}");
        }

        private GameObject CreateBasicFakeBlade(int playerIndex, Vector3 position)
        {
            GameObject root = new GameObject($"FakeBlade_Player{playerIndex}");
            root.transform.position = position;

            Color color = playerColors[playerIndex % playerColors.Length];

            // SpinPivot (para que el visualRoot no sea el root)
            GameObject spinPivot = new GameObject("SpinPivot");
            spinPivot.transform.parent = root.transform;
            spinPivot.transform.localPosition = Vector3.zero;

            // Cuerpo
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.parent = spinPivot.transform;
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.8f, 0.2f, 0.8f);
            DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = CreateMaterial($"Body_P{playerIndex}", color);

            // Anillo
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            ring.transform.parent = spinPivot.transform;
            ring.transform.localPosition = new Vector3(0, 0.12f, 0);
            ring.transform.localScale = new Vector3(1f, 0.06f, 1f);
            DestroyImmediate(ring.GetComponent<Collider>());
            ring.GetComponent<Renderer>().sharedMaterial = CreateMaterial($"Ring_P{playerIndex}", Color.Lerp(color, Color.white, 0.6f));

            // Punta
            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "Tip";
            tip.transform.parent = spinPivot.transform;
            tip.transform.localPosition = new Vector3(0, -0.15f, 0);
            tip.transform.localScale = new Vector3(0.2f, 0.25f, 0.2f);
            DestroyImmediate(tip.GetComponent<Collider>());
            tip.GetComponent<Renderer>().sharedMaterial = CreateMaterial($"Tip_P{playerIndex}", Color.gray);

            // Collider en el ROOT, no en el pivot
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.45f;
            collider.center = new Vector3(0, 0.05f, 0);
            collider.material = CreatePhysicMaterial($"FakeBlade_P{playerIndex}", 0.1f, 0.3f);

            return root;
        }

        #endregion

        // =====================================================================
        // GIZMOS
        // =====================================================================

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Arena circle
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.3f);
            DrawGizmoCircle(transform.position, arenaRadius, 48);

            // Spawn radius circle
            float spawnRadius = arenaRadius * spawnRadiusPercent;
            Gizmos.color = new Color(1f, 1f, 0.3f, 0.3f);
            DrawGizmoCircle(transform.position, spawnRadius, 32);
        }

        private void OnDrawGizmosSelected()
        {
            // Arena circle (más visible)
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.8f);
            DrawGizmoCircle(transform.position, arenaRadius, 64);

            // Walls (altura)
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.2f);
            DrawGizmoCircle(transform.position + Vector3.up * wallHeight, arenaRadius, 64);

            // Spawn points
            float spawnRadius = arenaRadius * spawnRadiusPercent;
            float angleStep = 360f / numberOfPlayers;

            Gizmos.color = new Color(1f, 1f, 0.3f, 0.6f);
            DrawGizmoCircle(transform.position, spawnRadius, 32);

            for (int i = 0; i < numberOfPlayers; i++)
            {
                float angle = (i * angleStep + spawnAngleOffset) * Mathf.Deg2Rad;
                Vector3 pos = transform.position + new Vector3(
                    Mathf.Cos(angle) * spawnRadius,
                    spawnHeight,
                    Mathf.Sin(angle) * spawnRadius
                );

                // Usar spawn points generados si existen
                if (generatedSpawnPoints != null && i < generatedSpawnPoints.Length && generatedSpawnPoints[i] != null)
                {
                    pos = generatedSpawnPoints[i].position;
                }

                Color pColor = playerColors[i % playerColors.Length];
                Gizmos.color = pColor;
                Gizmos.DrawSphere(pos, 0.3f);
                Gizmos.DrawLine(pos, pos + Vector3.up * 1f);

                // Línea al centro
                Gizmos.color = new Color(pColor.r, pColor.g, pColor.b, 0.3f);
                Gizmos.DrawLine(pos, transform.position + Vector3.up * spawnHeight);

#if UNITY_EDITOR
                UnityEditor.Handles.color = pColor;
                UnityEditor.Handles.Label(pos + Vector3.up * 1.2f, $"P{i + 1}");
#endif
            }
        }

        private void DrawGizmoCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }

        #endregion

        // =====================================================================
        // UTILITIES
        // =====================================================================

        #region Utility

        private Material CreateMaterial(string name, Color color, bool transparent = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.name = name;
            mat.color = color;

            if (transparent)
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            return mat;
        }

        private PhysicsMaterial CreatePhysicMaterial(string name, float friction, float bounce)
        {
            PhysicsMaterial mat = new PhysicsMaterial(name);
            mat.dynamicFriction = friction;
            mat.staticFriction = friction;
            mat.bounciness = bounce;
            mat.frictionCombine = PhysicsMaterialCombine.Average;
            mat.bounceCombine = PhysicsMaterialCombine.Maximum;
            return mat;
        }

        #endregion
    }

    /// <summary>
    /// Cámara que sigue múltiples objetivos con zoom dinámico.
    /// </summary>
    /// <summary>
    /// Cámara que sigue a todos los jugadores con zoom dinámico.
    /// Valores ajustables desde el Inspector del objeto cámara.
    /// </summary>
    public class SimpleCameraFollow : MonoBehaviour
    {
        [Header("=== CAMERA SETTINGS ===")]
        [Tooltip("Distancia mínima de la cámara (cuando están juntos)")]
        public float minZoom = 10f;
        [Tooltip("Distancia máxima de la cámara (cuando están lejos)")]
        public float maxZoom = 22f;
        [Tooltip("Divisor de distancia para calcular zoom (menor = zoom más lejano)")]
        public float zoomLimiter = 12f;
        [Tooltip("Velocidad de suavizado del movimiento")]
        public float smoothSpeed = 5f;
        [Tooltip("Offset vertical extra")]
        public float heightOffset = 2f;

        private GameObject[] _targets;
        private Vector3 _offset;

        public void SetTargets(GameObject[] targets)
        {
            _targets = targets;
            if (targets != null && targets.Length > 0)
                _offset = transform.position - GetCenterPoint();
        }

        private void LateUpdate()
        {
            if (_targets == null || _targets.Length == 0) return;

            Vector3 center = GetCenterPoint();
            float distance = GetMaxDistance();
            float zoom = Mathf.Lerp(minZoom, maxZoom, distance / zoomLimiter);

            Vector3 targetPos = center + _offset.normalized * zoom + Vector3.up * heightOffset;
            transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
            transform.LookAt(center + Vector3.up * 0.5f);
        }

        private Vector3 GetCenterPoint()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var t in _targets)
            {
                if (t != null && t.activeInHierarchy)
                {
                    sum += t.transform.position;
                    count++;
                }
            }
            return count > 0 ? sum / count : Vector3.zero;
        }

        private float GetMaxDistance()
        {
            Vector3 center = GetCenterPoint();
            float max = 0f;
            foreach (var t in _targets)
            {
                if (t != null && t.activeInHierarchy)
                {
                    float d = Vector3.Distance(center, t.transform.position);
                    if (d > max) max = d;
                }
            }
            return max * 2f;
        }
    }

    /// <summary>
    /// Sistema de camera shake estático. Se engancha automáticamente a Camera.main.
    /// Llamar CameraShake.Shake(intensidad, duración) desde cualquier script.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        private static CameraShake _instance;
        private float _shakeTimer;
        private float _shakeIntensity;
        private Vector3 _originalLocalPos;

        /// <summary>
        /// Sacude la cámara principal.
        /// </summary>
        /// <param name="intensity">Magnitud del shake (0.05 = sutil, 0.2 = fuerte)</param>
        /// <param name="duration">Duración en segundos</param>
        public static void Shake(float intensity, float duration)
        {
            if (_instance == null)
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                _instance = cam.GetComponent<CameraShake>();
                if (_instance == null)
                    _instance = cam.gameObject.AddComponent<CameraShake>();
            }

            // Solo sobrescribir si el nuevo shake es más intenso
            if (intensity > _instance._shakeIntensity || _instance._shakeTimer <= 0f)
            {
                _instance._shakeIntensity = intensity;
                _instance._shakeTimer = duration;
            }
        }

        private void LateUpdate()
        {
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;

                float decay = _shakeTimer > 0f ? _shakeTimer / 0.2f : 0f; // decay rápido
                decay = Mathf.Clamp01(decay);

                Vector3 offset = Random.insideUnitSphere * _shakeIntensity * decay;
                offset.z = 0f; // Shake solo en X/Y para que no cambie la profundidad
                transform.localPosition += offset;

                if (_shakeTimer <= 0f)
                {
                    _shakeIntensity = 0f;
                }
            }
        }
    }
}