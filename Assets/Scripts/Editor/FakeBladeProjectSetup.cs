using System.Collections.Generic;
using System.Text;
using FakeBlade.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FakeBlade.Core.Editor
{
    /// <summary>
    /// Monta el contenido de menús y escenas (idempotente, se puede repetir):
    /// - Prefab de arena a partir de "Arena 00.fbx" (medido y escalado a tamaño de juego) y arena de pruebas.
    /// - ArenaData, reglas de práctica, créditos y catálogo (FakeBladeCatalog).
    /// - Escenas MainMenu / Assembly / BattleArena con su controlador y Build Settings.
    /// </summary>
    public static class FakeBladeProjectSetup
    {
        private const string ArenaModelPath = "Assets/3D Models/Arena 00.fbx";
        private const string ArenaPrefabFolder = "Assets/Prefabs/Arena";
        private const string ArenaDataFolder = "Assets/Settings/Arenas";
        private const string PhysicsFolder = "Assets/Settings/Physics";
        private const string CatalogPath = "Assets/Settings/FakeBladeCatalog.asset";
        private const string PlayerPrefabPath = "Assets/Prefabs/FakeBlades/Player_0_Player 1 Variant.prefab";
        private const string TestScenePath = "Assets/Scenes/TestScene.unity";
        private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";

        /// <summary>Radio interior jugable de las arenas (metros).</summary>
        private const float TargetArenaRadius = 7f;

        [MenuItem("FakeBlade/Setup Menus & Scenes")]
        public static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var log = new StringBuilder();
            EditorSceneManager.SaveOpenScenes();

            FakeBladeAssetsMenu.EnsureFolder(ArenaPrefabFolder);
            FakeBladeAssetsMenu.EnsureFolder(ArenaDataFolder);
            FakeBladeAssetsMenu.EnsureFolder(PhysicsFolder);
            FakeBladeAssetsMenu.CreateDefaultAssets();

            log.AppendLine(FakeBladeVfxSetup.Run());
            log.AppendLine(FakeBladeSpecialsSetup.Run());

            var floorMat = PhysicsMaterialAsset("Arena_Floor", 0.1f, 0f);
            var wallMat = PhysicsMaterialAsset("Arena_Wall", 0.05f, 0.5f);

            // Escena temporal vacía para medir con raycasts sin ensuciar otras escenas
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject arena00 = BuildArena00(floorMat, wallMat, log);
            GameObject arenaTest = BuildTestArena(floorMat, wallMat, log);

            var arena00Data = ArenaDataAsset("Arena_00", "ARENA_00", arena00);
            var arenaTestData = ArenaDataAsset("Arena_Test", "ARENA_TEST", arenaTest);

            MatchRules sandbox = RulesAsset("Sandbox", "MODE_SANDBOX", WinCondition.Sandbox);
            CreditsData credits = LoadOrCreate<CreditsData>("Assets/Settings/Credits.asset");

            FakeBladeCatalog catalog = BuildCatalog(new List<ArenaData> { arena00Data, arenaTestData }, sandbox, credits, log);
            // Guardar antes de abrir escenas (abrir una escena puede descargar assets sin usar)
            AssetDatabase.SaveAssets();

            SetupScene("Assets/Scenes/MainMenu.unity", "[MainMenu]", catalog, typeof(MainMenuController), typeof(MenuArenaBackground));
            SetupScene("Assets/Scenes/Assembly.unity", "[Lobby]", catalog, typeof(LobbyController));
            SetupScene("Assets/Scenes/BattleArena.unity", "[Battle]", catalog, typeof(BattleBootstrap));
            EnsureSceneFrom(SandboxScenePath, "Assets/Scenes/BattleArena.unity");
            SetupScene(SandboxScenePath, "[Battle]", catalog, typeof(BattleBootstrap), typeof(SandboxController));

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Assembly.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/BattleArena.unity", true),
                new EditorBuildSettingsScene(SandboxScenePath, true),
                new EditorBuildSettingsScene(TestScenePath, true)
            };
            log.AppendLine("Build Settings: MainMenu, Assembly, BattleArena, Sandbox, TestScene");

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(TestScenePath);
            return log.ToString();
        }

        #region Arenas
        private static GameObject BuildArena00(PhysicsMaterial floorMat, PhysicsMaterial wallMat, StringBuilder log)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaModelPath);
            if (model == null)
            {
                log.AppendLine("No existe " + ArenaModelPath);
                return null;
            }

            var root = new GameObject("Arena_00");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.name = "Model";

            var floorColliders = new List<Collider>();
            var wallColliders = new List<Collider>();
            foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                var collider = mf.gameObject.GetComponent<MeshCollider>();
                if (collider == null) collider = mf.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mf.sharedMesh;
                bool isWall = mf.name.ToLowerInvariant().Contains("pared") || mf.name.ToLowerInvariant().Contains("wall");
                collider.sharedMaterial = isWall ? wallMat : floorMat;
                (isWall ? wallColliders : floorColliders).Add(collider);
                mf.gameObject.isStatic = true;
            }

            // Medir a escala 1. Suelo: solo colliders de suelo. Radio: solo colliders de muro,
            // para que la subida del cuenco no se confunda con el muro.
            SetEnabled(wallColliders, false);
            Physics.SyncTransforms();
            float floorY = RaycastFloor(Vector3.zero, 100f, out bool hitCenter);

            SetEnabled(wallColliders, true);
            SetEnabled(floorColliders, false);
            Physics.SyncTransforms();
            // Las caras del muro pueden mirar hacia fuera: aceptar caras traseras al medir
            bool previousBackfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            // El muro puede empezar por encima del suelo (cuenco): se prueba a varias alturas
            float measuredRadius = 0f;
            for (float h = 0.5f; h <= 12f; h += 0.5f)
            {
                float r = MeasureInnerRadius(floorY + h);
                if (r > 0f && (measuredRadius <= 0f || r < measuredRadius)) measuredRadius = r;
            }
            Physics.queriesHitBackfaces = previousBackfaces;
            SetEnabled(floorColliders, true);

            if (measuredRadius <= 0f)
            {
                Bounds b = CombinedBounds(instance);
                measuredRadius = Mathf.Min(b.extents.x, b.extents.z) * 0.9f;
                log.AppendLine("Arena 00: radio estimado por bounds");
            }

            float scale = TargetArenaRadius / measuredRadius;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localPosition = new Vector3(0f, -floorY * scale, 0f);
            Physics.SyncTransforms();

            // Perfil del suelo tras escalar (para saber si es cuenco)
            log.Append($"Arena 00: suelo centro y={floorY:F2} (hit {hitCenter}), radio medido {measuredRadius:F2} → escala {scale:F3}. Perfil: ");
            for (int i = 0; i <= 4; i++)
            {
                float r = TargetArenaRadius * i / 4f * 0.95f;
                float y = RaycastFloor(new Vector3(r, 0f, 0f), 50f, out bool hit);
                log.Append($"r{r:F1}:{(hit ? y.ToString("F2") : "-")} ");
            }
            log.AppendLine();

            var definition = root.AddComponent<ArenaDefinition>();
            definition.EditorConfigure(TargetArenaRadius, 0.6f);

            string path = $"{ArenaPrefabFolder}/Arena_00.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            log.AppendLine("Prefab: " + path);
            return prefab;
        }

        private static GameObject BuildTestArena(PhysicsMaterial floorMat, PhysicsMaterial wallMat, StringBuilder log)
        {
            var root = new GameObject("Arena_Test");
            var floorRender = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ArenaMaterial/SueloMaterial 00.mat");
            var wallRender = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ArenaMaterial/ParedMaterial Cristal 00.mat");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localScale = new Vector3(TargetArenaRadius * 2.4f, 0.2f, TargetArenaRadius * 2.4f);
            floor.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            floor.GetComponent<Collider>().sharedMaterial = floorMat;
            if (floorRender != null) floor.GetComponent<Renderer>().sharedMaterial = floorRender;
            floor.isStatic = true;

            const int segments = 32;
            float wallRadius = TargetArenaRadius + 0.15f;
            float width = 2f * Mathf.PI * wallRadius / segments * 1.15f;
            var walls = new GameObject("Walls").transform;
            walls.SetParent(root.transform, false);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_{i:D2}";
                wall.transform.SetParent(walls, false);
                wall.transform.localPosition = new Vector3(Mathf.Cos(angle) * wallRadius, 0.75f, Mathf.Sin(angle) * wallRadius);
                wall.transform.localScale = new Vector3(width, 1.5f, 0.3f);
                wall.transform.LookAt(root.transform.position + Vector3.up * 0.75f);
                wall.GetComponent<Collider>().sharedMaterial = wallMat;
                if (wallRender != null) wall.GetComponent<Renderer>().sharedMaterial = wallRender;
                wall.isStatic = true;
            }

            var definition = root.AddComponent<ArenaDefinition>();
            definition.EditorConfigure(TargetArenaRadius, 0.6f);

            string path = $"{ArenaPrefabFolder}/Arena_Test.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            log.AppendLine("Prefab: " + path);
            return prefab;
        }

        private static void SetEnabled(List<Collider> colliders, bool enabled)
        {
            foreach (var c in colliders) c.enabled = enabled;
        }

        private static float RaycastFloor(Vector3 xz, float height, out bool hit)
        {
            hit = Physics.Raycast(new Vector3(xz.x, height, xz.z), Vector3.down, out RaycastHit info, height * 3f);
            return hit ? info.point.y : 0f;
        }

        /// <summary>Distancia media desde el centro hasta el muro en 8 direcciones (0 si alguna no toca).</summary>
        private static float MeasureInnerRadius(float y)
        {
            float total = 0f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (!Physics.Raycast(new Vector3(0f, y, 0f), dir, out RaycastHit info, 500f)) return 0f;
                total += info.distance;
            }
            return total / 8f;
        }

        private static Bounds CombinedBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(go.transform.position, Vector3.one);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
        #endregion

        #region Assets
        private static PhysicsMaterial PhysicsMaterialAsset(string name, float friction, float bounce)
        {
            string path = $"{PhysicsFolder}/{name}.physicMaterial";
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (mat == null)
            {
                mat = new PhysicsMaterial(name);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.dynamicFriction = friction;
            mat.staticFriction = friction;
            mat.bounciness = bounce;
            mat.frictionCombine = PhysicsMaterialCombine.Average;
            mat.bounceCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static ArenaData ArenaDataAsset(string fileName, string nameKey, GameObject prefab)
        {
            var data = LoadOrCreate<ArenaData>($"{ArenaDataFolder}/{fileName}.asset");
            data.nameKey = nameKey;
            data.prefab = prefab;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static MatchRules RulesAsset(string fileName, string nameKey, WinCondition condition)
        {
            var rules = LoadOrCreate<MatchRules>($"Assets/Settings/GameModes/{fileName}.asset");
            rules.displayNameKey = nameKey;
            rules.winCondition = condition;
            rules.respawnDelay = 1.5f;
            EditorUtility.SetDirty(rules);
            return rules;
        }

        private static FakeBladeCatalog BuildCatalog(List<ArenaData> arenas, MatchRules sandbox, CreditsData credits, StringBuilder log)
        {
            if (AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>(FakeBladeComponentPresets.SAVE_PATH + "Tip_Medium_FlatBase.asset") == null)
                FakeBladeComponentPresets.CreateAllPresets();

            var catalog = LoadOrCreate<FakeBladeCatalog>(CatalogPath);
            catalog.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            catalog.combatConfig = AssetDatabase.LoadAssetAtPath<CombatConfig>("Assets/Settings/CombatConfig.asset");
            catalog.uiTheme = AssetDatabase.LoadAssetAtPath<HUDTheme>("Assets/Settings/HUDTheme.asset");
            catalog.credits = credits;

            catalog.tips.Clear();
            catalog.bodies.Clear();
            catalog.blades.Clear();
            catalog.cores.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:FakeBladeComponentData", new[] { FakeBladeComponentPresets.SAVE_PATH.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                // Solo las piezas preset con nombre de pieza (Tip_/Body_/Blade_/Core_)
                if (!(file.StartsWith("Tip_") || file.StartsWith("Body_") || file.StartsWith("Blade_") || file.StartsWith("Core_"))) continue;

                var part = AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>(path);
                catalog.GetParts(part.ComponentType).Add(part);
            }
            SortByWeight(catalog.tips);
            SortByWeight(catalog.bodies);
            SortByWeight(catalog.blades);
            SortByWeight(catalog.cores);

            catalog.presets.Clear();
            // Cada preset con las piezas de su arquetipo (C10)
            catalog.presets.Add(Preset("PRESET_ATTACK", "Tip_Medium_StrikerPoint", "Body_Medium_AssaultFrame", "Blade_Light_RazorEdge", "Core_Medium_Fire"));
            catalog.presets.Add(Preset("PRESET_DEFENSE", "Tip_Heavy_WideBall", "Body_Heavy_IronFortress", "Blade_Heavy_CrushWheel", "Core_Heavy_Defense"));
            catalog.presets.Add(Preset("PRESET_AGILITY", "Tip_Light_NeedlePoint", "Body_Light_AeroShell", "Blade_Light_GaleRing", "Core_Light_Lightning"));
            catalog.presets.Add(Preset("PRESET_BALANCED", "Tip_Medium_FlatBase", "Body_Medium_StandardFrame", "Blade_Medium_BalancedRing", "Core_Medium_SpinBoost"));

            catalog.arenas.Clear();
            foreach (var arena in arenas)
                if (arena != null && arena.prefab != null) catalog.arenas.Add(arena);

            catalog.lastStanding = AssetDatabase.LoadAssetAtPath<MatchRules>("Assets/Settings/GameModes/LastStanding.asset");
            catalog.stocks = AssetDatabase.LoadAssetAtPath<MatchRules>("Assets/Settings/GameModes/Stocks_3.asset");
            catalog.points = AssetDatabase.LoadAssetAtPath<MatchRules>("Assets/Settings/GameModes/FreeForAll_Points.asset");
            catalog.teams = AssetDatabase.LoadAssetAtPath<MatchRules>("Assets/Settings/GameModes/Teams_2v2.asset");
            catalog.sandbox = sandbox;

            EditorUtility.SetDirty(catalog);
            log.AppendLine($"Catálogo: {catalog.tips.Count} puntas, {catalog.bodies.Count} cuerpos, {catalog.blades.Count} discos, " +
                           $"{catalog.cores.Count} núcleos, {catalog.presets.Count} presets, {catalog.arenas.Count} arenas, " +
                           $"prefab jugador {(catalog.playerPrefab != null ? "OK" : "FALTA")}");
            return catalog;
        }

        private static void SortByWeight(List<FakeBladeComponentData> list) =>
            list.Sort((a, b) => a.WeightClass != b.WeightClass
                ? a.WeightClass.CompareTo(b.WeightClass)
                : string.CompareOrdinal(a.ComponentName, b.ComponentName));

        private static BladePreset Preset(string key, string tip, string body, string blade, string core) => new BladePreset
        {
            nameKey = key,
            tip = Part(tip),
            body = Part(body),
            blade = Part(blade),
            core = Part(core)
        };

        private static FakeBladeComponentData Part(string file) =>
            AssetDatabase.LoadAssetAtPath<FakeBladeComponentData>($"{FakeBladeComponentPresets.SAVE_PATH}{file}.asset");

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        #endregion

        #region Scenes
        /// <summary>Crea una escena copiando otra si todavía no existe (la Sandbox parte de BattleArena).</summary>
        private static void EnsureSceneFrom(string scenePath, string templatePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null) return;
            AssetDatabase.CopyAsset(templatePath, scenePath);
        }

        private static void SetupScene(string scenePath, string rootName, FakeBladeCatalog catalog, params System.Type[] components)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            // Abrir una escena descarga los assets sin usar: recargar el catálogo desde disco
            catalog = AssetDatabase.LoadAssetAtPath<FakeBladeCatalog>(CatalogPath);

            foreach (var go in scene.GetRootGameObjects())
                if (go.name == rootName) Object.DestroyImmediate(go);

            var root = new GameObject(rootName);
            foreach (var type in components)
            {
                var component = root.AddComponent(type);
                var so = new SerializedObject(component);
                var prop = so.FindProperty("catalog");
                if (prop != null)
                {
                    prop.objectReferenceValue = catalog;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        #endregion
    }
}
