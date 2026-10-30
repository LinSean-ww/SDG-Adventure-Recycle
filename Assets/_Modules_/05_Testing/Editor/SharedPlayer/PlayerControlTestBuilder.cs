using System;
using SDGAdventure.SharedPlayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.PlayerTest.Editor
{
    public static class PlayerControlTestBuilder
    {
        private const string Root = "Assets/_Modules_/01_Shared/Player";
        private const string PrefabPath = Root + "/Prefabs/SharedPlayer.prefab";
        private const string ScenePath = "Assets/_Modules_/05_Testing/SharedPlayer/PlayerControlTest.unity";
        private const string Materials = "Assets/_Modules_/05_Testing/SharedPlayer/Materials";

        [MenuItem("SDG Adventure/Build Player Control Test Scene")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("PlayerControlTest.unity already exists; refusing to overwrite it.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreatePlayerPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.56f, 0.6f, 0.68f);
            RenderSettings.fog = false;

            GameObject lightObject = new GameObject("Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            CreateCube("Ground", new Vector3(0, -0.2f, 1), new Vector3(30, 0.4f, 30),
                ColorFor("Ground", new Color(0.24f, 0.3f, 0.34f)));
            CreateCube("JumpPlatform", new Vector3(4, 0.38f, -1), new Vector3(2.2f, 0.76f, 2.2f),
                ColorFor("JumpPlatform", new Color(0.15f, 0.56f, 0.95f)));
            CreateCube("Obstacle", new Vector3(-4, 0.75f, -1), new Vector3(1.3f, 1.5f, 1.3f),
                ColorFor("Obstacle", new Color(0.92f, 0.44f, 0.22f)));
            Material tunnel = ColorFor("Tunnel", new Color(0.9f, 0.72f, 0.25f));
            CreateCube("LowTunnelLeftWall", new Vector3(-1.35f, 0.7f, 5), new Vector3(0.25f, 1.4f, 6), tunnel);
            CreateCube("LowTunnelRightWall", new Vector3(1.35f, 0.7f, 5), new Vector3(0.25f, 1.4f, 6), tunnel);
            CreateCube("LowTunnelRoof", new Vector3(0, 1.35f, 5), new Vector3(2.95f, 0.3f, 6), tunnel);

            GameObject target = CreateCube("F_InteractableTest", new Vector3(0, 0.8f, 10),
                new Vector3(1f, 1.6f, 1f), ColorFor("Interactable", new Color(0.1f, 0.8f, 0.9f)));
            var interactable = target.AddComponent<PlayerControlTestInteractable>();

            GameObject spawn = new GameObject("PlayerSpawn");
            spawn.transform.position = new Vector3(0, 0, -7);
            spawn.transform.rotation = Quaternion.identity;
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
            player.GetComponent<PlayerPauseMenu>().SetTestReturnPoint(spawn.transform);

            GameObject hudObject = new GameObject("PlayerControlTestHUD");
            hudObject.AddComponent<PlayerControlTestHud>()
                .Configure(player.GetComponent<SharedFirstPersonController>(), interactable);
            hudObject.AddComponent<PlayerControlTestTaskDemo>()
                .Configure(player.GetComponent<SharedFirstPersonController>(), interactable);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save PlayerControlTest.unity.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Player control test scene created: " + ScenePath);
        }

        private static GameObject CreatePlayerPrefab()
        {
            GameObject root = new GameObject("SharedPlayer");
            CharacterController motor = root.AddComponent<CharacterController>();
            motor.height = 1.8f;
            motor.radius = 0.3f;
            motor.center = new Vector3(0f, 0.9f, 0f);
            motor.stepOffset = 0.3f;
            motor.slopeLimit = 45f;
            GameObject view = new GameObject("Main Camera");
            view.tag = "MainCamera";
            view.transform.SetParent(root.transform, false);
            view.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            Camera camera = view.AddComponent<Camera>();
            camera.fieldOfView = 65f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            view.AddComponent<AudioListener>();

            SharedFirstPersonController controller = root.AddComponent<SharedFirstPersonController>();
            controller.SetViewTransform(view.transform);
            PlayerInteractor interactor = root.AddComponent<PlayerInteractor>();
            interactor.SetCamera(camera);
            PlayerPauseMenu menu = root.AddComponent<PlayerPauseMenu>();
            menu.SetPlayer(controller);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null) throw new InvalidOperationException("Could not create SharedPlayer.prefab.");
            return prefab;
        }

        private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static Material ColorFor(string name, Color color)
        {
            string path = Materials + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Built-in Standard shader is unavailable.");
            Material material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
