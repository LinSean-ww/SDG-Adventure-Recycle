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
        private const string Model = Root + "/Models/fk.fbx";
        private const string Prefab = Root + "/Prefabs/CompletePlayer.prefab";
        private const string Demo = Root + "/Demo/CompletePlayer_Demo.unity";
        private const string Shared = "Assets/_Modules_/01_Shared/Player";
        private const string SourceModel = "Assets/_Modules_/02_Levels/Level03/Models/fk.fbx";
        private const string License = "Assets/_Modules_/06_ExternalAssets/ThirdPartyNotices/SharedPlayer";

        [MenuItem("SDG Adventure/Complete Player/Build and Export")]
        public static void BuildAndExport()
        {
            Build();
            string output = Path.GetFullPath("../Exports/SDGAdventure_CompletePlayer.unitypackage");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            // These three complete roots are the audited asset boundary. IncludeDependencies
            // would also pull Package Manager source files into the unitypackage.
            AssetDatabase.ExportPackage(new[] { Root, Shared, License }, output,
                ExportPackageOptions.Recurse);
            if (!File.Exists(output) || new FileInfo(output).Length == 0)
                throw new InvalidOperationException("Unity did not create a nonempty unitypackage.");
            Debug.Log("COMPLETE_PLAYER_PACKAGE=" + output + " bytes=" + new FileInfo(output).Length);
        }

        [MenuItem("SDG Adventure/Complete Player/Build")]
        public static void Build()
        {
            EnsureFolder(Root + "/Models");
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Animations");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Demo");
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(Model))
            {
                if (!AssetDatabase.CopyAsset(SourceModel, Model))
                    throw new InvalidOperationException("Could not copy the FK model.");
                AssetDatabase.ImportAsset(Model, ImportAssetOptions.ForceUpdate);
            }

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
            CompletePlayerAnimation animation = prefab.GetComponentInChildren<CompletePlayerAnimation>(true);
            if (!animation || !animation.TryResolveBones())
                throw new InvalidOperationException("FK arm/leg bones are missing.");
            if (prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
                throw new InvalidOperationException("The character mesh is missing.");
            if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(x => !x))
                throw new InvalidOperationException("Prefab has a missing script.");
            string[] dependencies = AssetDatabase.GetDependencies(new[] { Prefab, Demo }, true);
            if (!dependencies.Contains(Model)) throw new InvalidOperationException("Prefab does not depend on copied FK model.");
            if (dependencies.Any(x => x.StartsWith("Assets/_Modules_/02_Levels/Level03/", StringComparison.Ordinal)))
                throw new InvalidOperationException("CompletePlayer still depends on Level03 assets.");
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(Demo))
                throw new InvalidOperationException("Demo scene is missing.");
            Scene scene = EditorSceneManager.OpenScene(Demo, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            int players = roots.SelectMany(x => x.GetComponentsInChildren<SharedFirstPersonController>(true)).Count();
            int cameras = roots.SelectMany(x => x.GetComponentsInChildren<Camera>(true)).Count();
            int listeners = roots.SelectMany(x => x.GetComponentsInChildren<AudioListener>(true)).Count();
            int interactables = roots.SelectMany(x => x.GetComponentsInChildren<CompletePlayerDemoInteractable>(true)).Count();
            int tasks = roots.SelectMany(x => x.GetComponentsInChildren<CompletePlayerDemoTasks>(true)).Count();
            int missing = roots.SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true)).Count(x => !x);
            if (players != 1 || cameras != 1 || listeners != 1 || interactables != 1 || tasks != 1 || missing != 0)
                throw new InvalidOperationException($"Demo components: players={players}, cameras={cameras}, listeners={listeners}, interactables={interactables}, tasks={tasks}, missing={missing}.");
            Debug.Log("COMPLETE_PLAYER_VALIDATION_OK dependencies=" + dependencies.Length);
        }

        private static void CreatePrefab()
        {
            GameObject shared = AssetDatabase.LoadAssetAtPath<GameObject>(Shared + "/Prefabs/SharedPlayer.prefab");
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (!shared || !model) throw new InvalidOperationException("SharedPlayer or FK model missing.");
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(shared);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                player.name = "CompletePlayer";
                player.GetComponent<PlayerPauseMenu>().SetStandaloneLevelActions(true);
                visual.name = "CharacterVisual";
                visual.transform.SetParent(player.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                visual.transform.localScale = Vector3.one * 0.14f;
                foreach (Transform item in visual.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 2;
                if (!visual.AddComponent<CompletePlayerAnimation>().TryResolveBones())
                    throw new InvalidOperationException("Copied FBX does not contain the expected FK skeleton.");
                Camera camera = player.GetComponentInChildren<Camera>();
                camera.cullingMask &= ~(1 << 2); // Built-in Ignore Raycast layer: visible in Scene and to other cameras.
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
                var model = player ? player.GetComponentInChildren<CompletePlayerAnimation>() : null;
                var target = UnityEngine.Object.FindFirstObjectByType<CompletePlayerDemoInteractable>();
                var tasks = UnityEngine.Object.FindFirstObjectByType<CompletePlayerDemoTasks>();
                if (!player || !service || !menu || !model || !model.HasRequiredBones || !target || !tasks ||
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
