using System;
using System.IO;
using System.Linq;
using C3TreasurePuzzle;
using SDGAdventure.C3;
using SDGAdventure.ModuleSystem;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class M6IntegrationSetup
{
    private const string Selection = ModuleCoordinator.SelectionScenePath;
    private const string C3 = C3ModuleAdapter.ScenePath;

    public static void Configure()
    {
        if (File.Exists(Selection)) throw new Exception("Selection scene already exists; refusing to overwrite it.");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject cameraObject = new GameObject("Development selection camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.065f, 0.09f);
        cameraObject.AddComponent<AudioListener>();
        EditorSceneManager.SaveScene(scene, Selection);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(Selection, true),
            new EditorBuildSettingsScene(C3, true)
        };
        AssetDatabase.SaveAssets();
        Debug.Log("M6_CONFIGURE_PASS selection=" + Selection + " c3=" + C3);
    }

    public static void Validate()
    {
        int failures = 0;
        void Check(bool ok, string name)
        {
            Debug.Log((ok ? "PASS " : "FAIL ") + name);
            if (!ok) failures++;
        }

        Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(Selection) != null, "selection scene imported");
        Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(C3) != null, "C3 scene imported");
        Check(EditorBuildSettings.scenes.Length == 2 && EditorBuildSettings.scenes[0].enabled &&
              EditorBuildSettings.scenes[0].path == Selection && EditorBuildSettings.scenes[1].enabled &&
              EditorBuildSettings.scenes[1].path == C3, "Build Settings scenes");
        Check(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Modules_/02_Levels/Level03/Models/ONLYfactory.fbx") != null,
            "original FBX imported");
        foreach (string name in new[] { "M2_Player", "M3_Fuse", "M3_ControlChip", "M3_FilterModule" })
            Check(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Modules_/02_Levels/Level03/Prefabs/" + name + ".prefab") != null,
                name + " prefab imported");

        string[] dependencies = AssetDatabase.GetDependencies(C3, true);
        Check(dependencies.All(path => path.StartsWith("Assets/") || path.StartsWith("Packages/")), "C3 dependency paths");
        Check(dependencies.All(path => AssetDatabase.LoadMainAssetAtPath(path) != null), "C3 dependencies resolve");
        Scene old = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenScene(C3, OpenSceneMode.Additive);
        int missing = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
        Check(missing == 0, "no Missing Script in C3 scene");
        Check(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<C3DeviceState>(true)).Count() == 1,
            "one C3 device");
        Check(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<C3FirstPersonController>(true)).Count() == 1,
            "one C3 player");
        Check(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Count() == 1,
            "one C3 camera");
        Check(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AudioListener>(true)).Count() == 1,
            "one C3 AudioListener");
        EditorSceneManager.CloseScene(scene, true);
        EditorSceneManager.SetActiveScene(old);

        ModuleDefinition valid = new ModuleDefinition("m6_static_probe", "Probe", C3, typeof(C3ModuleAdapter));
        Check(valid.IsValid(out _), "valid definition");
        Check(!new ModuleDefinition("Bad ID", "Probe", C3, typeof(C3ModuleAdapter)).IsValid(out _), "invalid ID rejected");
        Check(!new ModuleDefinition("probe", "Probe", C3, null).IsValid(out _), "missing adapter rejected");
        Check(ModuleRegistry.TryRegister(valid, out _), "registration accepted");
        Check(!ModuleRegistry.TryRegister(valid, out _), "duplicate ID rejected");
        Check(!ModuleRegistry.TryGet("not_registered", out _), "unknown ID rejected");
        Check(!Application.CanStreamedLevelBeLoaded("Assets/_Modules_/05_Testing/Scenes/DefinitelyMissing.unity"), "missing scene rejected");

        Debug.Log("M6_VALIDATE_COMPLETE failures=" + failures);
        if (failures != 0) throw new Exception("M6 validation failed: " + failures);
    }

    public static void BuildWindows()
    {
        string output = SDGAdventure.Testing.ReportOutputPaths.Resolve("M6/WindowsBuild/SDGAdventure_M6_TEST.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(item => item.enabled).Select(item => item.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        Debug.Log("M6_BUILD_RESULT " + report.summary.result + " errors=" + report.summary.totalErrors +
                  " warnings=" + report.summary.totalWarnings + " output=" + output);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("M6 Windows build failed.");
    }
}

