using System;
using System.IO;
using System.Linq;
using SDGAdventure.SharedPlayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SDGAdventure.CompletePlayer.Editor
{
    public static class CompletePlayerBuilder
    {
        private const string Root = "Assets/_Modules_/01_Shared/CompletePlayer";
        private const string Prefab = Root + "/Prefabs/CompletePlayer.prefab";
        private const string Demo = Root + "/Demo/CompletePlayer_Demo.unity";
        private const string Shared = "Assets/_Modules_/01_Shared/Player";
        private const string License = "Assets/_Modules_/06_ExternalAssets/ThirdPartyNotices/SharedPlayer";
        private const string ExportPath = "Exports/SDGAdventure_CompletePlayer_v1.3.unitypackage";

        [MenuItem("SDG Adventure/Complete Player/Export Runtime v1.3 (No Demo)")]
        public static void BuildAndExport()
        {
            string output = Path.GetFullPath(ExportPath);
            if (File.Exists(output) || Directory.Exists(output))
                throw new InvalidOperationException("Export already exists; it will not be overwritten: " + output);
            string[] assets = RuntimeExportAssets();
            ValidateRuntimeExport(assets);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            // Export to a unique temporary name and use a non-overwriting move for the final name.
            // The current Prefab is validated in place: no rebuild or scene replacement occurs.
            string temporary = Path.Combine(Path.GetDirectoryName(output),
                ".CompletePlayer-" + Guid.NewGuid().ToString("N") + ".unitypackage");
            try
            {
                AssetDatabase.ExportPackage(assets, temporary, ExportPackageOptions.Default);
                if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                    throw new InvalidOperationException("Unity did not create a nonempty unitypackage.");
                File.Move(temporary, output);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            Debug.Log("COMPLETE_PLAYER_PACKAGE=" + output + " bytes=" + new FileInfo(output).Length);
        }

        private static string[] RuntimeExportAssets()
        {
            // Enumerate individual files, never folders: this keeps Demo, Editor, legacy FK,
            // generated demo materials and demo-only scripts out of the runtime package.
            string[] fixedAssets = {
                Prefab,
                Root + "/Scripts/SDGAdventure.CompletePlayer.asmdef",
                Root + "/Scripts/CompletePlayerWorkerAppearance.cs",
                Root + "/Scripts/SharedWorkerVisualBuilder.cs",
                Root + "/Resources/SharedWorker/Rust.png",
                Root + "/Resources/SharedWorker/Steel.png",
                Root + "/README_同學匯入教學.md",
                Root + "/操作鍵位對照表.md"
            };
            var files = fixedAssets.Concat(new[] { Shared, License }.SelectMany(directory =>
                Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                    .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    .Select(path => path.Replace('\\', '/')))).Distinct().OrderBy(path => path).ToArray();
            foreach (string path in files)
                if (!File.Exists(path) || !File.Exists(path + ".meta") || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                    throw new InvalidOperationException("Missing export asset, meta or imported GUID: " + path);
            return files;
        }

        private static void ValidateRuntimeExport(string[] assets)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (!prefab) throw new InvalidOperationException("CompletePlayer prefab is missing.");
            if (prefab.GetComponentsInChildren<CharacterController>(true).Length != 1 ||
                prefab.GetComponentsInChildren<SharedFirstPersonController>(true).Length != 1 ||
                prefab.GetComponentsInChildren<Camera>(true).Length != 1 ||
                prefab.GetComponentsInChildren<AudioListener>(true).Length != 1 ||
                prefab.GetComponentsInChildren<PlayerInteractor>(true).Length != 1 ||
                prefab.GetComponentsInChildren<PlayerPauseMenu>(true).Length != 1 ||
                prefab.GetComponentsInChildren<CompletePlayerWorkerAppearance>(true).Length != 1)
                throw new InvalidOperationException("Player core components are missing or duplicated.");
            if (prefab.GetComponentsInChildren<CompletePlayerAnimation>(true).Length != 0 ||
                prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0)
                throw new InvalidOperationException("Legacy FK visual is still attached to the player.");
            if ((prefab.GetComponentInChildren<Camera>(true).cullingMask & 1) == 0)
                throw new InvalidOperationException("Camera excludes the worker layer.");
            CheckReferences(prefab.GetComponentsInChildren<Component>(true), "Prefab");
            var controls = Resources.Load<UnityEngine.InputSystem.InputActionAsset>("SDGPlayerControls");
            if (!controls) throw new InvalidOperationException("Shared input actions are missing.");
            foreach (string action in new[] { "Move", "Look", "Jump", "Sprint", "Crouch", "Prone", "LeanLeft", "LeanRight", "Interact", "Inventory", "Slot1", "Slot2", "Slot3", "Slot4", "Slot5", "TaskList", "Reload", "Drop" })
                if (controls.FindAction("Gameplay/" + action) == null)
                    throw new InvalidOperationException("Missing gameplay action: " + action);
            foreach (string action in new[] { "UI/Inventory", "UI/TaskList", "UI/Cancel", "Global/Pause" })
                if (controls.FindAction(action) == null) throw new InvalidOperationException("Missing action: " + action);
            foreach (string texture in new[] { "Rust", "Steel" })
                if (!Resources.Load<Texture2D>("SharedWorker/" + texture))
                    throw new InvalidOperationException("Worker texture missing: " + texture);
            if (!Resources.Load<Font>("SharedPlayer/Fonts/NotoSansCJKtc-Regular") ||
                !AssetDatabase.LoadAssetAtPath<TextAsset>(License + "/NotoSansCJK-OFL.txt"))
                throw new InvalidOperationException("Shared UI font or its license is missing.");
            var included = new System.Collections.Generic.HashSet<string>(assets, StringComparer.Ordinal);
            string[] missing = AssetDatabase.GetDependencies(assets, true)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) &&
                    !AssetDatabase.IsValidFolder(path) && !included.Contains(path)).ToArray();
            if (missing.Length != 0)
                throw new InvalidOperationException("Export has dependencies outside the runtime list: " + string.Join(", ", missing));
            Debug.Log("COMPLETE_PLAYER_RUNTIME_VALIDATION_OK assets=" + assets.Length);
        }

        [MenuItem("SDG Adventure/Complete Player/Build")]
        public static void Build()
        {
            EnsureFolder(Root + "/Models");
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Animations");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Demo");
            CreatePrefab();
            CreateDemo();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("COMPLETE_PLAYER_BUILD_OK");
        }

        [MenuItem("SDG Adventure/Complete Player/Validate")]
        public static void Validate()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (!prefab) throw new InvalidOperationException("CompletePlayer prefab is missing.");
            if (prefab.GetComponentsInChildren<CharacterController>(true).Length != 1 ||
                prefab.GetComponentsInChildren<SharedFirstPersonController>(true).Length != 1 ||
                prefab.GetComponentsInChildren<Camera>(true).Length != 1 ||
                prefab.GetComponentsInChildren<AudioListener>(true).Length != 1 ||
                prefab.GetComponentsInChildren<PlayerInteractor>(true).Length != 1 ||
                prefab.GetComponentsInChildren<PlayerPauseMenu>(true).Length != 1)
                throw new InvalidOperationException("Player has duplicate or missing core components.");
            if (prefab.GetComponentsInChildren<CompletePlayerWorkerAppearance>(true).Length != 1)
                throw new InvalidOperationException("Worker appearance missing or duplicated.");
            if (prefab.GetComponentsInChildren<CompletePlayerAnimation>(true).Length != 0 ||
                prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0)
                throw new InvalidOperationException("Legacy FK visual must not be the default player.");
            var camera = prefab.GetComponentInChildren<Camera>(true);
            if ((camera.cullingMask & 1) == 0)
                throw new InvalidOperationException("Camera excludes worker default layer.");
            foreach (string texture in new[] { "Rust", "Steel" })
                if (!Resources.Load<Texture2D>("SharedWorker/" + texture))
                    throw new InvalidOperationException("Worker texture missing: " + texture);
            if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(x => !x))
                throw new InvalidOperationException("Prefab has a missing script.");
            CheckReferences(prefab.GetComponentsInChildren<Component>(true), "Prefab");
            var controls = Resources.Load<UnityEngine.InputSystem.InputActionAsset>("SDGPlayerControls");
            if (!controls) throw new InvalidOperationException("Shared input actions are missing.");
            foreach (string action in new[] { "Prone", "LeanLeft", "LeanRight", "Inventory", "Slot1", "Slot2", "Slot3", "Slot4", "Slot5" })
                if (controls.FindAction("Gameplay/" + action) == null)
                    throw new InvalidOperationException("Missing gameplay action: " + action);
            if (controls.FindAction("UI/Inventory") == null)
                throw new InvalidOperationException("Missing UI inventory action.");
            if (!Resources.Load<Font>("SharedPlayer/Fonts/NotoSansCJKtc-Regular"))
                throw new InvalidOperationException("Shared UI font is missing.");
            if (!AssetDatabase.LoadAssetAtPath<TextAsset>(License + "/NotoSansCJK-OFL.txt"))
                throw new InvalidOperationException("Shared UI font license is missing.");
            string[] dependencies = AssetDatabase.GetDependencies(new[] { Prefab, Demo }, true);
            if (dependencies.Any(x => x.StartsWith("Assets/_Modules_/02_Levels/Level03/", StringComparison.Ordinal)))
                throw new InvalidOperationException("CompletePlayer still depends on Level03 assets.");
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(Demo))
                throw new InvalidOperationException("Demo scene is missing.");
            Scene scene = EditorSceneManager.OpenScene(Demo, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            CheckReferences(roots.SelectMany(x => x.GetComponentsInChildren<Component>(true)), "Demo");
            int players = roots.SelectMany(x => x.GetComponentsInChildren<SharedFirstPersonController>(true)).Count();
            int cameras = roots.SelectMany(x => x.GetComponentsInChildren<Camera>(true)).Count();
            int listeners = roots.SelectMany(x => x.GetComponentsInChildren<AudioListener>(true)).Count();
            int interactables = roots.SelectMany(x => x.GetComponentsInChildren<CompletePlayerDemoInteractable>(true)).Count();
            int tasks = roots.SelectMany(x => x.GetComponentsInChildren<CompletePlayerDemoTasks>(true)).Count();
            int inventoryDemos = roots.SelectMany(x => x.GetComponentsInChildren<CompletePlayerDemoInventory>(true)).Count();
            if (inventoryDemos != 1) throw new InvalidOperationException("Demo inventory UI missing or duplicated.");
            int missing = roots.SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true)).Count(x => !x);
            if (players != 1 || cameras != 1 || listeners != 1 || interactables != 1 || tasks != 1 || missing != 0)
                throw new InvalidOperationException($"Demo components: players={players}, cameras={cameras}, listeners={listeners}, interactables={interactables}, tasks={tasks}, missing={missing}.");
            Debug.Log("COMPLETE_PLAYER_VALIDATION_OK dependencies=" + dependencies.Length);
        }

        private static void CheckReferences(System.Collections.Generic.IEnumerable<Component> components, string owner)
        {
            foreach (Component component in components)
            {
                if (!component) throw new InvalidOperationException(owner + " has a missing component.");
                var properties = new SerializedObject(component).GetIterator();
                while (properties.Next(true))
                    if (properties.propertyType == SerializedPropertyType.ObjectReference &&
                        properties.objectReferenceValue == null && properties.objectReferenceInstanceIDValue != 0)
                        throw new InvalidOperationException(owner + " has a missing reference: " + component.name + "/" + properties.propertyPath);
            }
        }

        private static void CreatePrefab()
        {
            GameObject shared = AssetDatabase.LoadAssetAtPath<GameObject>(Shared + "/Prefabs/SharedPlayer.prefab");
            if (!shared) throw new InvalidOperationException("SharedPlayer missing.");
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(shared);
            try
            {
                player.name = "CompletePlayer";
                player.GetComponent<PlayerPauseMenu>().SetStandaloneLevelActions(true);
                player.AddComponent<CompletePlayerWorkerAppearance>();
                Camera camera = player.GetComponentInChildren<Camera>();
                camera.cullingMask |= 1 << 2;
                if (!PrefabUtility.SaveAsPrefabAsset(player, Prefab))
                    throw new InvalidOperationException("Could not save CompletePlayer prefab.");
            }
            finally { UnityEngine.Object.DestroyImmediate(player); }
        }

        private static void CreateDemo()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.62f, 0.7f);
            GameObject sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Cube("Ground", new Vector3(0, -0.2f, 1), new Vector3(30, 0.4f, 30), Material("Ground", new Color(0.25f, 0.31f, 0.36f)));
            Cube("JumpPlatform", new Vector3(4, 0.38f, -1), new Vector3(2.2f, 0.76f, 2.2f), Material("JumpPlatform", new Color(0.15f, 0.56f, 0.95f)));
            Cube("Obstacle", new Vector3(-4, 0.75f, -1), new Vector3(1.3f, 1.5f, 1.3f), Material("Obstacle", new Color(0.92f, 0.44f, 0.22f)));
            Material tunnel = Material("Tunnel", new Color(0.9f, 0.72f, 0.25f));
            Cube("LowTunnelLeftWall", new Vector3(-1.35f, 0.7f, 5), new Vector3(0.25f, 1.4f, 6), tunnel);
            Cube("LowTunnelRightWall", new Vector3(1.35f, 0.7f, 5), new Vector3(0.25f, 1.4f, 6), tunnel);
            Cube("LowTunnelRoof", new Vector3(0, 1.35f, 5), new Vector3(2.95f, 0.3f, 6), tunnel);
            GameObject target = Cube("E_InteractableTest", new Vector3(0, 0.8f, 10),
                new Vector3(1, 1.6f, 1), Material("Interactable", new Color(0.1f, 0.8f, 0.9f)));
            var interactable = target.AddComponent<CompletePlayerDemoInteractable>();

            GameObject spawn = new GameObject("PlayerSpawn");
            spawn.transform.position = new Vector3(0, 0, -7);
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), scene);
            player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
            player.GetComponent<PlayerPauseMenu>().SetTestReturnPoint(spawn.transform);
            new GameObject("CompletePlayerDemoInventory").AddComponent<CompletePlayerDemoInventory>()
                .Configure(player.GetComponent<PlayerPauseMenu>());
            new GameObject("CompletePlayerDemoTasks").AddComponent<CompletePlayerDemoTasks>()
                .Configure(player.GetComponent<SharedFirstPersonController>(), interactable);
            if (!EditorSceneManager.SaveScene(scene, Demo))
                throw new InvalidOperationException("Could not save CompletePlayer demo scene.");
        }

        private static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static Material Material(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(Shader.Find("Standard")) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    // SessionState survives the domain reload when the batch Editor enters Play Mode.
    [InitializeOnLoad]
    public static class CompletePlayerRuntimeSmoke
    {
        private const string Pending = "SDGAdventure.CompletePlayer.RuntimeSmokePending";

        static CompletePlayerRuntimeSmoke()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Modules_/01_Shared/CompletePlayer/Demo/CompletePlayer_Demo.unity",
                OpenSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || Time.frameCount < 4)
                return;
            SessionState.SetBool(Pending, false);
            try
            {
                var player = UnityEngine.Object.FindFirstObjectByType<SharedFirstPersonController>();
                var service = PlayerInputService.Instance;
                var menu = player ? player.GetComponent<PlayerPauseMenu>() : null;
                var model = player ? player.GetComponentInChildren<CompletePlayerWorkerAppearance>() : null;
                var target = UnityEngine.Object.FindFirstObjectByType<CompletePlayerDemoInteractable>();
                var tasks = UnityEngine.Object.FindFirstObjectByType<CompletePlayerDemoTasks>();
                if (!player || !service || !menu || !model || !model.HasWorker || !target || !tasks ||
                    !menu.View || !menu.Journal || menu.Journal.TotalCount != 3 ||
                    service.Context != PlayerInputContext.Gameplay ||
                    UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length != 1 ||
                    UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length != 1 ||
                    UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).Length != 1)
                    throw new InvalidOperationException("Runtime player, camera, input, rig, UI, or tasks are incomplete.");

                Vector3 before = player.transform.position;
                player.TickMotor(Vector2.up, false, false, false, 0.1f);
                if (player.transform.position.z <= before.z)
                    throw new InvalidOperationException("SharedPlayer movement did not advance.");
                menu.Pause();
                if (menu.State != PlayerMenuState.Paused || service.Context != PlayerInputContext.UI || Time.timeScale != 0)
                    throw new InvalidOperationException("Pause did not switch to UI context.");
                menu.OpenSettings();
                if (menu.State != PlayerMenuState.Settings) throw new InvalidOperationException("Settings did not open.");
                menu.SetMouseSensitivity(1.7f);
                if (Mathf.Abs(service.Keybindings.Settings.mouseSensitivity - 1.7f) > 0.01f)
                    throw new InvalidOperationException("Sensitivity setting failed.");
                menu.RestoreDefaults();
                menu.BackToPause();
                menu.Resume();
                if (menu.State != PlayerMenuState.Playing || service.Context != PlayerInputContext.Gameplay || Time.timeScale == 0)
                    throw new InvalidOperationException("Pause menu did not resume gameplay.");
                menu.OpenTasks();
                if (menu.State != PlayerMenuState.Tasks || service.Context != PlayerInputContext.UI)
                    throw new InvalidOperationException("Tasks did not open.");
                menu.CloseTasks();
                target.Interact(player.gameObject);
                if (target.InteractionCount != 1) throw new InvalidOperationException("Demo interaction failed.");
                Debug.Log("COMPLETE_PLAYER_RUNTIME_SMOKE_OK");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
    }
}
