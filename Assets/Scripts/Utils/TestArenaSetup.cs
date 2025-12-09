using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Generador de arena de prueba con botones de Editor.
    /// Usa los botones en el Inspector para generar la escena sin ejecutar.
    /// </summary>
    public class TestArenaSetup : MonoBehaviour
    {
        [Header("=== PREFABS DE PEONZAS ===")]
        [Tooltip("Arrastra tus prefabs de peonzas aquí")]
        [SerializeField] private GameObject[] fakeBladePrefabs;

        [Header("=== ARENA ===")]
        [SerializeField] private float arenaRadius = 10f;
        [SerializeField] private float wallHeight = 1.5f;
        [SerializeField] private int wallSegments = 24;
        [SerializeField] private float wallThickness = 0.3f;

        [Header("=== JUGADORES ===")]
        [SerializeField] private int numberOfPlayers = 2;
        [SerializeField] private float spawnHeight = 0.3f;

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

        [Header("=== REFERENCIAS GENERADAS ===")]
        [SerializeField] private GameObject generatedArena;
        [SerializeField] private GameObject[] generatedPlayers;

        // Para acceso desde el Editor
        public float ArenaRadius => arenaRadius;
        public int NumberOfPlayers => numberOfPlayers;
        public GameObject[] FakeBladePrefabs => fakeBladePrefabs;
        public Color[] PlayerColors => playerColors;

        #region Botones del Editor (llamados desde TestArenaSetupEditor)

        /// <summary>
        /// Genera toda la escena: arena + jugadores
        /// </summary>
        public void GenerateAll()
        {
            ClearAll();
            GenerateArena();
            GeneratePlayers();
            GenerateGameManager();
            Debug.Log("✓ Escena generada completamente");
        }

        /// <summary>
        /// Genera solo la arena
        /// </summary>
        public void GenerateArena()
        {
            if (generatedArena != null)
            {
                DestroyImmediate(generatedArena);
            }

            generatedArena = new GameObject("Arena");
            generatedArena.transform.parent = transform;

            CreateFloor();
            CreateWalls();

            Debug.Log("✓ Arena generada");
        }

        /// <summary>
        /// Genera solo los jugadores
        /// </summary>
        public void GeneratePlayers()
        {
            ClearPlayers();

            generatedPlayers = new GameObject[numberOfPlayers];
            float spawnRadius = arenaRadius * 0.5f;
            float angleStep = 360f / numberOfPlayers;

            for (int i = 0; i < numberOfPlayers; i++)
            {
                float angle = (i * angleStep + 90f) * Mathf.Deg2Rad;
                Vector3 spawnPos = new Vector3(
                    Mathf.Cos(angle) * spawnRadius,
                    spawnHeight,
                    Mathf.Sin(angle) * spawnRadius
                );

                generatedPlayers[i] = CreateFakeBlade(i, spawnPos);
            }

            Debug.Log($"✓ {numberOfPlayers} jugadores generados");
        }

        /// <summary>
        /// Limpia toda la escena generada
        /// </summary>
        public void ClearAll()
        {
            ClearArena();
            ClearPlayers();
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
                    if (player != null)
                    {
                        DestroyImmediate(player);
                    }
                }
                generatedPlayers = null;
            }
        }

        private void ClearGameManager()
        {
            var existingGM = FindFirstObjectByType<GameManager>();
            if (existingGM != null)
            {
                DestroyImmediate(existingGM.gameObject);
            }
        }

        public void GenerateGameManager()
        {
            ClearGameManager();
            GameObject gmObj = new GameObject("[GameManager]");
            gmObj.AddComponent<GameManager>();
            Debug.Log("✓ GameManager creado");
        }

        #endregion

        #region Creación de Arena

        private void CreateFloor()
        {
            // Crear suelo plano (NO cilindro que tiene CapsuleCollider)
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.parent = generatedArena.transform;
            floor.transform.position = new Vector3(0, -0.05f, 0);
            floor.transform.localScale = new Vector3(arenaRadius * 2.2f, 0.1f, arenaRadius * 2.2f);

            // Material
            var renderer = floor.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial("Floor_Mat", new Color(0.9f, 0.9f, 0.9f));

            // Physics Material
            var collider = floor.GetComponent<BoxCollider>();
            collider.material = CreatePhysicMaterial("Floor_Physics", groundFriction, 0f);

            // Para arena circular, añadir un borde visual (opcional)
            CreateFloorCircleVisual();
        }

        private void CreateFloorCircleVisual()
        {
            // Crear un disco visual encima del cubo
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "FloorDisc_Visual";
            disc.transform.parent = generatedArena.transform;
            disc.transform.position = new Vector3(0, 0.001f, 0);
            disc.transform.localScale = new Vector3(arenaRadius * 2f, 0.001f, arenaRadius * 2f);

            // Quitar collider - es solo visual
            DestroyImmediate(disc.GetComponent<Collider>());

            var renderer = disc.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial("FloorDisc_Mat", new Color(0.95f, 0.95f, 0.95f));
        }

        private void CreateWalls()
        {
            GameObject wallsParent = new GameObject("Walls");
            wallsParent.transform.parent = generatedArena.transform;

            float angleStep = 360f / wallSegments;
            float wallWidth = (2f * Mathf.PI * arenaRadius) / wallSegments * 1.1f;

            // Material compartido para todas las paredes
            Material wallMat = CreateTransparentMaterial("Wall_Mat", new Color(0.3f, 0.6f, 1f, 0.2f));
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

                var renderer = wall.GetComponent<Renderer>();
                renderer.sharedMaterial = wallMat;

                var boxCollider = wall.GetComponent<BoxCollider>();
                boxCollider.material = wallPhysMat;
            }
        }

        #endregion

        #region Creación de Jugadores

        private GameObject CreateFakeBlade(int playerIndex, Vector3 position)
        {
            GameObject fakeBlade;
            bool usingPrefab = fakeBladePrefabs != null &&
                               fakeBladePrefabs.Length > 0 &&
                               playerIndex < fakeBladePrefabs.Length &&
                               fakeBladePrefabs[playerIndex] != null;

            if (usingPrefab)
            {
                // Usar prefab del usuario
#if UNITY_EDITOR
                fakeBlade = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(fakeBladePrefabs[playerIndex]);
                fakeBlade.transform.position = position;
#else
                fakeBlade = Instantiate(fakeBladePrefabs[playerIndex], position, Quaternion.identity);
#endif
                fakeBlade.name = $"FakeBlade_Player{playerIndex}";

                EnsureCollider(fakeBlade);
            }
            else
            {
                // Crear peonza básica
                fakeBlade = CreateBasicFakeBlade(playerIndex, position);
            }

            fakeBlade.transform.parent = transform;
            ConfigureGameComponents(fakeBlade, playerIndex);

            return fakeBlade;
        }

        private GameObject CreateBasicFakeBlade(int playerIndex, Vector3 position)
        {
            GameObject root = new GameObject($"FakeBlade_Player{playerIndex}");
            root.transform.position = position;

            Color color = playerColors[playerIndex % playerColors.Length];

            // Cuerpo principal
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.parent = root.transform;
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.8f, 0.2f, 0.8f);
            DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = CreateMaterial($"Body_P{playerIndex}", color);

            // Anillo
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            ring.transform.parent = root.transform;
            ring.transform.localPosition = new Vector3(0, 0.12f, 0);
            ring.transform.localScale = new Vector3(1f, 0.06f, 1f);
            DestroyImmediate(ring.GetComponent<Collider>());
            ring.GetComponent<Renderer>().sharedMaterial = CreateMaterial($"Ring_P{playerIndex}", Color.Lerp(color, Color.white, 0.6f));

            // Punta
            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "Tip";
            tip.transform.parent = root.transform;
            tip.transform.localPosition = new Vector3(0, -0.15f, 0);
            tip.transform.localScale = new Vector3(0.2f, 0.25f, 0.2f);
            DestroyImmediate(tip.GetComponent<Collider>());
            tip.GetComponent<Renderer>().sharedMaterial = CreateMaterial($"Tip_P{playerIndex}", Color.gray);

            // Collider - ESFERA para física estable
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.45f;
            collider.center = new Vector3(0, 0.05f, 0);
            collider.material = CreatePhysicMaterial($"FakeBlade_P{playerIndex}", 0.1f, 0.3f);

            return root;
        }

        private void EnsureCollider(GameObject fakeBlade)
        {
            Collider col = fakeBlade.GetComponent<Collider>();
            if (col == null)
            {
                col = fakeBlade.GetComponentInChildren<Collider>();
            }
            if (col == null)
            {
                SphereCollider sphere = fakeBlade.AddComponent<SphereCollider>();
                sphere.radius = 0.45f;
                sphere.material = CreatePhysicMaterial("FakeBlade_Custom", 0.1f, 0.3f);
            }
        }

        private void ConfigureGameComponents(GameObject fakeBlade, int playerIndex)
        {
            // Rigidbody
            Rigidbody rb = fakeBlade.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = fakeBlade.AddComponent<Rigidbody>();
            }

            rb.mass = 1.5f;
            rb.linearDamping = 4f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationX |
                            RigidbodyConstraints.FreezeRotationZ;
            rb.centerOfMass = new Vector3(0, -0.15f, 0);

            // Scripts
            if (fakeBlade.GetComponent<FakeBladeStats>() == null)
                fakeBlade.AddComponent<FakeBladeStats>();

            if (fakeBlade.GetComponent<FakeBladeController>() == null)
                fakeBlade.AddComponent<FakeBladeController>();

            InputHandler inputHandler = fakeBlade.GetComponent<InputHandler>();
            if (inputHandler == null)
                inputHandler = fakeBlade.AddComponent<InputHandler>();

            PlayerController playerController = fakeBlade.GetComponent<PlayerController>();
            if (playerController == null)
                playerController = fakeBlade.AddComponent<PlayerController>();

            // Configurar
            playerController.SetPlayerID(playerIndex);
            playerController.SetPlayerName($"Player {playerIndex + 1}");
            playerController.SetPlayerColor(playerColors[playerIndex % playerColors.Length]);
            inputHandler.SetGamepadIndex(playerIndex == 0 ? -1 : playerIndex - 1);
        }

        #endregion

        #region Materiales

        private Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.name = name;
            mat.color = color;
            return mat;
        }

        private Material CreateTransparentMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.name = name;

            // Configurar transparencia
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            mat.color = color;

            return mat;
        }

        private PhysicsMaterial CreatePhysicMaterial(string name, float friction, float bounce)
        {
            PhysicsMaterial mat = new PhysicsMaterial(name);
            mat.dynamicFriction = friction;
            mat.staticFriction = friction;
            mat.bounciness = bounce;
            mat.frictionCombine = PhysicsMaterialCombine.Minimum;
            mat.bounceCombine = PhysicsMaterialCombine.Average;
            return mat;
        }

        #endregion

        #region Runtime (cuando se ejecuta la escena)

        private void Start()
        {
            // Auto-iniciar partida si hay jugadores generados
            if (generatedPlayers != null && generatedPlayers.Length > 0)
            {
                SetupRuntimeCamera();
                StartMatch();
            }
        }

        private void SetupRuntimeCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            float height = arenaRadius * 1.2f;
            float distance = arenaRadius * 0.5f;
            mainCam.transform.position = new Vector3(0, height, -distance);
            mainCam.transform.LookAt(Vector3.zero);

            SimpleCameraFollow camFollow = mainCam.GetComponent<SimpleCameraFollow>();
            if (camFollow == null)
                camFollow = mainCam.gameObject.AddComponent<SimpleCameraFollow>();

            if (generatedPlayers != null)
                camFollow.SetTargets(generatedPlayers);
        }

        private void StartMatch()
        {
            Invoke(nameof(DelayedStartMatch), 0.5f);
        }

        private void DelayedStartMatch()
        {
            GameManager.Instance?.ChangeState(GameManager.GameState.InMatch);
            Debug.Log("¡Partida iniciada!");
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

            float spawnRadius = arenaRadius * 0.5f;
            float angleStep = 360f / numberOfPlayers;

            for (int i = 0; i < generatedPlayers.Length; i++)
            {
                if (generatedPlayers[i] == null) continue;

                float angle = (i * angleStep + 90f) * Mathf.Deg2Rad;
                Vector3 spawnPos = new Vector3(
                    Mathf.Cos(angle) * spawnRadius,
                    spawnHeight,
                    Mathf.Sin(angle) * spawnRadius
                );

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

            Debug.Log("Match Reset!");
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            if (generatedPlayers == null) return;

            // Panel de controles
            GUI.Box(new Rect(10, 10, 160, 80),
                "CONTROLES\n" +
                "P1: WASD+Space+Shift\n" +
                "R: Reset | ESC: Salir");

            // Barras de spin
            for (int i = 0; i < generatedPlayers.Length; i++)
            {
                if (generatedPlayers[i] == null) continue;
                var controller = generatedPlayers[i].GetComponent<FakeBladeController>();
                if (controller == null) continue;

                float spin = controller.SpinSpeedPercentage;
                float y = 100f + i * 25f;

                GUI.color = playerColors[i % playerColors.Length];
                GUI.Box(new Rect(10, y, 150 * spin, 20), $"P{i + 1}: {spin * 100:F0}%");
            }
            GUI.color = Color.white;
        }

        #endregion
    }

    /// <summary>
    /// Cámara que sigue múltiples objetivos
    /// </summary>
    public class SimpleCameraFollow : MonoBehaviour
    {
        private GameObject[] _targets;
        private Vector3 _offset;
        private float _smoothSpeed = 3f;

        public void SetTargets(GameObject[] targets)
        {
            _targets = targets;
            if (targets != null && targets.Length > 0)
            {
                _offset = transform.position - GetCenterPoint();
            }
        }

        private void LateUpdate()
        {
            if (_targets == null || _targets.Length == 0) return;

            Vector3 center = GetCenterPoint();
            float distance = GetMaxDistance();
            float zoom = Mathf.Lerp(12f, 25f, distance / 15f);

            Vector3 targetPos = center + _offset.normalized * zoom;
            transform.position = Vector3.Lerp(transform.position, targetPos, _smoothSpeed * Time.deltaTime);
            transform.LookAt(center);
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
}