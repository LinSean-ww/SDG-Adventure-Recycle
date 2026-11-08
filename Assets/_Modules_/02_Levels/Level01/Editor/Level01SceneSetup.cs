using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class Level01SceneSetup
{
    public static void Verify()
    {
        var scene = EditorSceneManager.OpenScene(Level01ModuleAdapter.ScenePath, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects();
        var oldPlayer = roots.Single(x => x.name == "fk");
        var player = roots.Single(x => x.name == "Level01 CompletePlayer");
        if (oldPlayer.activeSelf || !player.activeSelf) throw new Exception("Player ownership invalid");
        var activeCameras = roots.SelectMany(x => x.GetComponentsInChildren<Camera>(true))
            .Count(x => x.enabled && x.gameObject.activeInHierarchy);
        var activeListeners = roots.SelectMany(x => x.GetComponentsInChildren<AudioListener>(true))
            .Count(x => x.enabled && x.gameObject.activeInHierarchy);
        if (activeCameras != 1 || activeListeners != 1) throw new Exception("Camera or listener ownership invalid");
        var detector = player.GetComponentInChildren<TreasureDetector>(true);
        var scan = player.GetComponentInChildren<scanball>(true);
        if (detector == null || scan == null || detector.scanBallScript != scan ||
            scan.treasureDetector != detector || detector.distanceText == null || scan.cooldownText == null ||
            detector.targetBottles.Count != 10 || detector.targetBottles.Any(x => x == null))
            throw new Exception("Scanner or target references invalid");
        var door = roots.SelectMany(x => x.GetComponentsInChildren<DoorInteraction>(true)).Single();
        if (door.treasureDetector != detector || door.doorObject == null || door.finalItem == null ||
            door.powerLowPlane == null || door.powerHighPlane == null)
            throw new Exception("Power door references invalid");
        int missingMaterials = 0, missingShaders = 0;
        foreach (var renderer in roots.SelectMany(x => x.GetComponentsInChildren<Renderer>(true)))
            foreach (var material in renderer.sharedMaterials)
                if (material == null) missingMaterials++;
                else if (material.shader == null) missingShaders++;
        Debug.Log($"LEVEL01_RENDERER_REFS missingMaterials={missingMaterials} missingShaders={missingShaders}");
        var importer = AssetImporter.GetAtPath("Assets/_Modules_/02_Levels/Level01/Imported/mod/ONLYfactory.fbx");
        foreach (var entry in importer.GetExternalObjectMap())
            if (entry.Key.name == "siteC_floor")
                Debug.Log("LEVEL01_FACTORY_SITE_C_FLOOR=" + (entry.Value == null ? "UNRESOLVED" : AssetDatabase.GetAssetPath(entry.Value)));
        foreach (GameObject root in roots)
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    throw new Exception("Missing script on " + transform.name);
        Debug.Log("LEVEL01_SCENE_VERIFY_OK cameras=1 listeners=1 targets=10 missingScripts=0");
    }

    public static void Setup()
    {
        const string scenePath = Level01ModuleAdapter.ScenePath;
        const string playerPath = "Assets/_Modules_/01_Shared/CompletePlayer/Prefabs/CompletePlayer.prefab";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var oldPlayer = scene.GetRootGameObjects().Single(x => x.name == "fk");
        var scan = oldPlayer.GetComponentInChildren<scanball>(true);
        if (scan == null || scan.GetComponent<TreasureDetector>() == null) throw new Exception("Original scan references missing");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
        if (prefab == null) throw new Exception("CompletePlayer prefab missing");
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        player.name = "Level01 CompletePlayer";
        player.transform.SetPositionAndRotation(oldPlayer.transform.position, oldPlayer.transform.rotation);
        var holder = new GameObject("Level01 Scanner Anchor");
        holder.transform.SetParent(player.transform, false);
        holder.transform.localPosition = new Vector3(0, 0.79f, 0);
        holder.transform.localScale = Vector3.one * 0.09f;
        scan.transform.SetParent(holder.transform, false);
        scan.transform.localPosition = Vector3.zero;
        oldPlayer.SetActive(false);
        var events = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<EventSystem>(true)).ToArray();
        foreach (var eventSystem in events)
        {
            var legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null) legacy.enabled = false;
            var modern = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (modern == null) modern = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            modern.AssignDefaultActions();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save Level01 scene");
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(x => x.path == scenePath)) { scenes.Add(new EditorBuildSettingsScene(scenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
        Debug.Log("LEVEL01_SCENE_SETUP_OK");
    }
}
