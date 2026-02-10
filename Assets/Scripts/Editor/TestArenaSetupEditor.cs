using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Script de prueba que genera una arena y peonzas en runtime.
    /// 
    /// ACTUALIZADO:
    /// - Soporte robusto para prefabs con/sin componentes pre-existentes
    /// - Configuraciones de test con componentes equipados (Light/Medium/Heavy)
    /// - Física mejorada para inercia real
    /// 
    /// Añádelo a un GameObject vacío en la escena para testear.
    /// </summary>
    public class TestArenaSetup : MonoBehaviour
    {
        #region Serialized Fields

        [Header("=== ARENA ===")]
        [SerializeField] private float arenaRadius = 10f;
        [SerializeField] private float wallHeight = 2f;
        [SerializeField] private int wallSegments = 32;
        [SerializeField] private float wallThickness = 0.3f;

        [Header("=== PLAYERS ===")]
        [SerializeField] private int numberOfPlayers = 2;

        [Header("=== PREFABS (Opcional) ===")]
        [Tooltip("Arrastra aquí prefabs 3D. Si están vacíos, se genera geometría básica. " +
                 "Si el prefab ya tiene componentes (Rigidbody, FakeBladeStats, etc) se respetan. " +
                 "Si no los tiene, se añaden automáticamente.")]
        [SerializeField] private GameObject[] fakeBladePrefabs;

        [Header("=== TEST COMPONENTS (Opcional) ===")]
        [Tooltip("Si se asignan, se equipan en los jugadores durante el test. " +
                 "Índice 0 = Player 0, etc. Si un slot está vacío, usa stats base.")]
        [SerializeField] private TestLoadout[] playerLoadouts;

        [Header("=== PHYSICS ===")]
        [SerializeField] private float groundFriction = 0.4f;
        [SerializeField] private float wallBounce = 0.6f;
        [SerializeField] private float wallFriction = 0.1f;

        [Header("=== VISUALS ===")]
        [SerializeField]
        private Color[] playerColors = new Color[]
        {
            new Color(0.2f, 0.5f, 1f),    // Blue
            new Color(1f, 0.3f, 0.3f),    // Red
            new Color(0.3f, 1f, 0.3f),    // Green
            new Color(1f, 1f, 0.3f)       // Yellow
        };

        [Header("=== DEBUG ===")]
        [SerializeField] private bool showControlsOnScreen = true;
        [SerializeField] private bool showStatsOnScreen = true;

        #endregion

        #region Private Fields
        private GameObject _arenaRoot;
        private GameObject[] generatedPlayers;
        #endregion

        #region Test Loadout
        [System.Serializable]
        public class TestLoadout
        {
            [Tooltip("Punta equipada para este jugador")]
            public FakeBladeComponentData tip;
            [Tooltip("Cuerpo equipado")]
            public FakeBladeComponentData body;
            [Tooltip("Disco/Cuchilla equipada")]
            public FakeBladeComponentData blade;
            [Tooltip("Núcleo equipado")]
            public FakeBladeComponentData core;
        }
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            CreateArena();
            CreatePlayers();
            SetupCamera();
            CreateGameManager();

            Debug.Log("=== TEST ARENA READY ===");
            Debug.Log("Player 1: WASD + Space (dash) + Shift (special)");
            Debug.Log("Player 2+: Gamepad - Left Stick + A/RT (dash) + X/LT (special)");
            Debug.Log("R: Reset | ESC: Quit");
            Debug.Log("========================");
        }

        private void Update()
        {
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
        #endregion

        #region Arena Creation
        private void CreateArena()
        {
            _arenaRoot = new GameObject("Arena");

            // Suelo - usar Box en vez de Cylinder para física plana perfecta
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            floor.transform.parent = _arenaRoot.transform;
            floor.transform.localScale = new Vector3(arenaRadius * 2f, 0.1f, arenaRadius * 2f);
            floor.transform.position = Vector3.zero;
            floor.tag = "Ground";

            Renderer floorRenderer = floor.GetComponent<Renderer>();
            floorRenderer.sharedMaterial = CreateMaterial("Floor_Mat", new Color(0.85f, 0.85f, 0.9f));

            var floorPhys = CreatePhysicMaterial("Floor_Physics", groundFriction, 0f);
            floor.GetComponent<Collider>().material = floorPhys;

            CreateCircularWalls();
        }

        private void CreateCircularWalls()
        {
            GameObject wallsParent = new GameObject("Walls");
            wallsParent.transform.parent = _arenaRoot.transform;

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

        #region Player Creation
        private void CreatePlayers()
        {
            generatedPlayers = new GameObject[numberOfPlayers];

            float spawnRadius = arenaRadius * 0.5f;
            float angleStep = 360f / numberOfPlayers;

            for (int i = 0; i < numberOfPlayers; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 spawnPos = new Vector3(
                    Mathf.Cos(angle) * spawnRadius,
                    0.5f,
                    Mathf.Sin(angle) * spawnRadius
                );

                generatedPlayers[i] = CreateFakeBlade(i, spawnPos);
            }
        }

        /// <summary>
        /// Crea un FakeBlade. Si hay prefab disponible, lo usa (respetando componentes existentes).
        /// Si no, genera geometría básica.
        /// </summary>
        private GameObject CreateFakeBlade(int playerIndex, Vector3 position)
        {
            GameObject fakeBlade;
            bool usingPrefab = fakeBladePrefabs != null &&
                               fakeBladePrefabs.Length > playerIndex &&
                               fakeBladePrefabs[playerIndex] != null;

            if (usingPrefab)
            {
                // === INSTANCIAR PREFAB ===
#if UNITY_EDITOR
                fakeBlade = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(fakeBladePrefabs[playerIndex]);
                fakeBlade.transform.position = position;
#else
                fakeBlade = Instantiate(fakeBladePrefabs[playerIndex], position, Quaternion.identity);
#endif
                fakeBlade.name = $"FakeBlade_Player{playerIndex}";
                fakeBlade.transform.parent = transform;

                // Asegurar que tiene collider
                EnsureCollider(fakeBlade);
            }
            else
            {
                // === GENERAR PEONZA BÁSICA ===
                fakeBlade = CreateBasicFakeBlade(playerIndex, position);
                fakeBlade.transform.parent = transform;
            }

            // Configurar componentes de juego (respetando los existentes)
            ConfigureGameComponents(fakeBlade, playerIndex);

            // Aplicar loadout de test si está definido
            ApplyTestLoadout(fakeBlade, playerIndex);

            return fakeBlade;
        }

        /// <summary>
        /// Asegura que el prefab tiene al menos un collider.
        /// Si ya tiene uno (incluso en hijos), lo respeta.
        /// </summary>
        private void EnsureCollider(GameObject fakeBlade)
        {
            Collider col = fakeBlade.GetComponent<Collider>();
            if (col == null)
            {
                col = fakeBlade.GetComponentInChildren<Collider>();
            }
            if (col == null)
            {
                // No tiene ningún collider - añadir esfera
                SphereCollider sphere = fakeBlade.AddComponent<SphereCollider>();
                sphere.radius = 0.45f;
                sphere.center = new Vector3(0, 0.05f, 0);
                sphere.material = CreatePhysicMaterial($"FB_P{fakeBlade.name}", 0.1f, 0.3f);
                Debug.Log($"[TestArena] Added SphereCollider to {fakeBlade.name} (no collider found)");
            }
        }

        /// <summary>
        /// Configura los componentes de juego necesarios.
        /// USA GetComponent PRIMERO - si ya existe, NO lo duplica.
        /// Esto permite que prefabs con componentes pre-configurados funcionen correctamente.
        /// </summary>
        private void ConfigureGameComponents(GameObject fakeBlade, int playerIndex)
        {
            // === RIGIDBODY ===
            Rigidbody rb = fakeBlade.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = fakeBlade.AddComponent<Rigidbody>();
                Debug.Log($"[TestArena] Added Rigidbody to {fakeBlade.name}");
            }

            // Configurar Rigidbody (siempre, para asegurar consistencia)
            rb.mass = 1.5f; // Se sobreescribirá por FakeBladeStats
            rb.linearDamping = 0.3f;
            rb.angularDamping = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezeRotationX |
                            RigidbodyConstraints.FreezeRotationZ;
            rb.centerOfMass = new Vector3(0, -0.1f, 0);

            // === FAKEBLADE STATS ===
            FakeBladeStats stats = fakeBlade.GetComponent<FakeBladeStats>();
            if (stats == null)
            {
                stats = fakeBlade.AddComponent<FakeBladeStats>();
                Debug.Log($"[TestArena] Added FakeBladeStats to {fakeBlade.name}");
            }

            // === FAKEBLADE CONTROLLER ===
            FakeBladeController controller = fakeBlade.GetComponent<FakeBladeController>();
            if (controller == null)
            {
                controller = fakeBlade.AddComponent<FakeBladeController>();
                Debug.Log($"[TestArena] Added FakeBladeController to {fakeBlade.name}");
            }

            // === INPUT HANDLER ===
            InputHandler inputHandler = fakeBlade.GetComponent<InputHandler>();
            if (inputHandler == null)
            {
                inputHandler = fakeBlade.AddComponent<InputHandler>();
                Debug.Log($"[TestArena] Added InputHandler to {fakeBlade.name}");
            }

            // === PLAYER CONTROLLER ===
            PlayerController playerController = fakeBlade.GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = fakeBlade.AddComponent<PlayerController>();
                Debug.Log($"[TestArena] Added PlayerController to {fakeBlade.name}");
            }

            // Configurar PlayerController
            playerController.SetPlayerID(playerIndex);
            playerController.SetPlayerName($"Player {playerIndex + 1}");
            playerController.SetPlayerColor(playerColors[playerIndex % playerColors.Length]);

            // Configurar Input
            inputHandler.SetGamepadIndex(playerIndex == 0 ? -1 : playerIndex - 1);
        }

        /// <summary>
        /// Aplica el loadout de test (componentes equipados) si está definido para este jugador.
        /// </summary>
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

            Debug.Log($"[TestArena] Player {playerIndex} loadout: {stats.GetStatsSummary()}");
        }

        /// <summary>
        /// Genera una peonza básica con primitivos cuando no hay prefab.
        /// </summary>
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

            // Collider esférico
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.45f;
            collider.center = new Vector3(0, 0.05f, 0);
            collider.material = CreatePhysicMaterial($"FakeBlade_P{playerIndex}", 0.1f, 0.3f);

            return root;
        }
        #endregion

        #region Camera
        private void SetupCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                mainCam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            mainCam.transform.position = new Vector3(0, arenaRadius * 1.5f, -arenaRadius * 0.8f);
            mainCam.transform.LookAt(Vector3.zero);
            mainCam.fieldOfView = 60f;

            SimpleCameraFollow camFollow = mainCam.gameObject.GetComponent<SimpleCameraFollow>();
            if (camFollow == null)
                camFollow = mainCam.gameObject.AddComponent<SimpleCameraFollow>();
            camFollow.SetTargets(generatedPlayers);
        }
        #endregion

        #region Game Manager
        private void CreateGameManager()
        {
            if (GameManager.Instance == null)
            {
                GameObject gmObj = new GameObject("GameManager");
                gmObj.AddComponent<GameManager>();
            }

            Invoke(nameof(StartMatch), 1f);
        }

        private void StartMatch()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ChangeState(GameManager.GameState.InMatch);
            }
        }
        #endregion

        #region Reset
        private void ResetMatch()
        {
            float spawnRadius = arenaRadius * 0.5f;
            float angleStep = 360f / numberOfPlayers;

            for (int i = 0; i < generatedPlayers.Length; i++)
            {
                if (generatedPlayers[i] == null) continue;

                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 spawnPos = new Vector3(
                    Mathf.Cos(angle) * spawnRadius,
                    0.5f,
                    Mathf.Sin(angle) * spawnRadius
                );

                generatedPlayers[i].transform.position = spawnPos;
                generatedPlayers[i].SetActive(true);

                var controller = generatedPlayers[i].GetComponent<FakeBladeController>();
                controller?.ResetFakeBlade();

                var pc = generatedPlayers[i].GetComponent<PlayerController>();
                pc?.ResetPlayer();
            }

            Debug.Log("=== Match Reset! ===");
        }
        #endregion

        #region GUI
        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            if (generatedPlayers == null) return;

            // Panel de controles
            if (showControlsOnScreen)
            {
                GUI.Box(new Rect(10, 10, 200, 100),
                    "=== CONTROLS ===\n" +
                    "P1: WASD+Space+Shift\n" +
                    "P2+: Gamepad\n" +
                    "R: Reset | ESC: Quit");
            }

            // Stats de jugadores
            if (showStatsOnScreen)
            {
                float y = showControlsOnScreen ? 120f : 10f;

                for (int i = 0; i < generatedPlayers.Length; i++)
                {
                    if (generatedPlayers[i] == null) continue;

                    var controller = generatedPlayers[i].GetComponent<FakeBladeController>();
                    var stats = generatedPlayers[i].GetComponent<FakeBladeStats>();
                    if (controller == null) continue;

                    float spin = controller.SpinSpeedPercentage;
                    float speed = controller.Velocity.magnitude;

                    // Barra de spin
                    GUI.color = playerColors[i % playerColors.Length];
                    GUI.Box(new Rect(10, y, 250, 22),
                        $"P{i + 1}: Spin {spin * 100:F0}% | Spd {speed:F1} | W:{stats?.Weight:F1}");

                    // Barra visual
                    GUI.color = Color.Lerp(Color.red, Color.green, spin);
                    GUI.Box(new Rect(10, y + 22, 250 * spin, 4), "");

                    y += 30f;
                }
                GUI.color = Color.white;
            }
        }
        #endregion

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
    public class SimpleCameraFollow : MonoBehaviour
    {
        private GameObject[] _targets;
        private Vector3 _offset;
        private float _smoothSpeed = 4f;
        private float _minZoom = 14f;
        private float _maxZoom = 28f;
        private float _zoomLimiter = 12f;

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
            float zoom = Mathf.Lerp(_minZoom, _maxZoom, distance / _zoomLimiter);

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