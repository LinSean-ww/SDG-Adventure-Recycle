using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using C3TreasurePuzzle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class C3V13FactorySceneTools : EditorWindow
{
    public const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    const string AssetFolder = "Assets/_Modules_/02_Levels/Level03/ArtFull/V13Baked";
    const string Menu = "SDG Adventure/Level03 V13/Factory Scene Tools";
    string report = "Validate or preview before baking. Existing baked layouts are never overwritten.";
    Vector2 scroll;

    [MenuItem(Menu)] public static void Open() => GetWindow<C3V13FactorySceneTools>("V13 Factory Scene");
    void OnGUI()
    {
        EditorGUILayout.HelpBox("Open C_Main alone in Edit Mode. Bake saves editable objects in the scene and meshes/materials in Assets. Save the scene with Ctrl+S. Repeated Bake preserves all edits. Undo restores the pre-bake scene; assets remain for Redo.", MessageType.Info);
        if (GUILayout.Button("Validate Scene")) Run(() => ValidateScene(SceneManager.GetActiveScene()));
        if (GUILayout.Button("Preview Bake Changes")) Run(() => Preview(SceneManager.GetActiveScene()));
        if (GUILayout.Button("Bake Factory")) Run(() => Bake(SceneManager.GetActiveScene()));
        if (GUILayout.Button("Validate Baked Factory")) Run(() => ValidateBaked(SceneManager.GetActiveScene()));
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.SelectableLabel(report, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(320));
        EditorGUILayout.EndScrollView();
    }
    void Run(Func<string> action)
    {
        try { report = action(); Debug.Log(report); }
        catch (Exception e) { report = e.Message; Debug.LogException(e); }
    }
    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static C3FactoryPresentation Owner(Scene scene) => All<C3FactoryPresentation>(scene).Single();

    public static string ValidateScene(Scene scene)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Bake is only allowed in Edit Mode.");
        Require(scene.IsValid() && scene.isLoaded && scene.path == ScenePath, "Open the official Level03 C_Main scene.");
        Require(SceneManager.sceneCount == 1, "Open C_Main alone; legacy builders use scene name lookup.");
        Require(All<C3FactoryPresentation>(scene).Length == 1, "Expected one factory presentation owner.");
        Require(Owner(scene).GetComponent<C3DeviceState>(), "Missing factory device.");
        foreach (string name in new[] { "WasteSortingLine", "C_Factory_Art", "C_TwoFloor_Blockout", "M4_PuzzleConsole", "UpperChipMaintenanceBench" })
            Require(All<Transform>(scene).Count(t => t.name == name) == 1, "Missing or ambiguous required owner: " + name);
        foreach (Transform t in All<Transform>(scene))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "Missing Script: " + t.name);
        Require(All<C3RecyclingLine>(scene).Length == 0, "Runtime recycling session must not be baked into the scene.");
        return "Scene validation passed. Runtime session, inventory and progress will initialize only in Play Mode.";
    }
    public static string Preview(Scene scene)
    {
        ValidateScene(scene);
        if (All<C3BakedFactory>(scene).Length != 0)
            return ValidateBaked(scene) + "\nBake will make no changes. Existing Transform edits and added children are preserved.";
        return "Will create: reference warehouse/utilities, 30 conveyor sections and 3 machines, verified stairs/platforms/rails, workshop/cache/salvage placements and 4 station art sets.\nWill preserve existing gameplay components and triggers; hide C_Factory_Art renderers except upper stations, retire only V12 collision allowlists, and reposition WasteSortingLine as V12 does.\nWill persist generated Material/Mesh sub-assets in " + AssetFolder + ".\nNo Awake, Start, simulation ticks, player setup or gameplay progress will be invoked. No existing baked objects will be deleted.";
    }

    public static string Bake(Scene scene)
    {
        ValidateScene(scene);
        if (All<C3BakedFactory>(scene).Length != 0)
            return ValidateBaked(scene) + "\nAlready baked: no changes made.";
        var owner = Owner(scene).gameObject;
        // Refuse partial/foreign generations instead of guessing ownership or deleting art.
        foreach (string name in new[] { "C3_ReferenceRecyclingFactory_Visuals", C3RecyclingLineGeometry.RootName,
            "C3_V3_VerifiedCirculation", "EASTER_EGG_EcoQuantumWorkshop", "C3_Distributed_Materials_And_Energy" })
            Require(!All<Transform>(scene).Any(t => t.name == name), "Unowned layout found: " + name + ". Restore the pre-bake scene or inspect it manually.");
        var before = new HashSet<GameObject>(All<Transform>(scene).Select(t => t.gameObject));
        var components = new HashSet<Component>(All<Component>(scene));
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Bake Level03 V13 Factory");
        foreach (GameObject root in scene.GetRootGameObjects()) Undo.RegisterFullObjectHierarchyUndo(root, "Bake factory");
        // RenderSettings are native scene data and need their own Undo record.
        var settings = typeof(RenderSettings).GetMethod("GetRenderSettings", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null) as Object;
        Require(settings, "Cannot record RenderSettings for Undo; bake cancelled before generation.");
        Undo.RegisterCompleteObjectUndo(settings, "Factory lighting");
        bool registered = false;
        try
        {
            var workshop = GetOrAdd<EcoFactoryWorkshop>(owner); workshop.EnsureLayout();
            var environment = GetOrAdd<C3ReferenceFactoryEnvironment>(owner); environment.EnsureLayout();
            var geometry = GetOrAdd<C3RecyclingLineGeometry>(owner); geometry.EnsureLayout();
            var circulation = GetOrAdd<C3V3Circulation>(owner); circulation.EnsureLayout();
            Physics.SyncTransforms();
            var stations = GetOrAdd<C3WorkstationVisualUpgrade>(owner); stations.EnsureLayout();
            foreach (var salvage in All<C3SalvageAppearance>(scene)) salvage.EnsureLayout();
            var marker = GetOrAdd<C3BakedFactory>(owner);
            marker.environment = environment; marker.geometry = geometry; marker.circulation = circulation;
            marker.workshop = workshop; marker.stations = stations;
            marker.generatedObjects = All<Transform>(scene).Select(t => t.gameObject).Where(g => !before.Contains(g)).ToArray();
            PersistAssets(scene, marker.generatedObjects);
            RegisterCreations(scene, before, components);
            registered = true;
            foreach (Component component in All<Component>(scene)) if (component) EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            string result = ValidateBaked(scene);
            Undo.CollapseUndoOperations(group);
            return result + "\nBake complete. Ctrl+S saves the scene; Ctrl+Z undoes the bake. Persistent sub-assets are retained for Redo.";
        }
        catch
        {
            if (!registered) RegisterCreations(scene, before, components);
            Undo.RevertAllDownToGroup(group);
            throw;
        }
    }
    static T GetOrAdd<T>(GameObject owner) where T : Component => owner.GetComponent<T>() ?? owner.AddComponent<T>();
    static void RegisterCreations(Scene scene, HashSet<GameObject> before, HashSet<Component> components)
    {
        foreach (Transform t in All<Transform>(scene))
            if (!before.Contains(t.gameObject) && (!t.parent || before.Contains(t.parent.gameObject)))
                Undo.RegisterCreatedObjectUndo(t.gameObject, "Factory layout");
        foreach (Component c in All<Component>(scene))
            if (c && before.Contains(c.gameObject) && !components.Contains(c))
                Undo.RegisterCreatedObjectUndo(c, "Factory layout bindings");
    }
    static void PersistAssets(Scene scene, GameObject[] generated)
    {
        var assets = new HashSet<Object>();
        foreach (GameObject g in generated)
        {
            foreach (Renderer r in g.GetComponents<Renderer>()) foreach (Material m in r.sharedMaterials) if (m) assets.Add(m);
            foreach (MeshFilter f in g.GetComponents<MeshFilter>()) if (f.sharedMesh) assets.Add(f.sharedMesh);
            foreach (MeshCollider c in g.GetComponents<MeshCollider>()) if (c.sharedMesh) assets.Add(c.sharedMesh);
        }
        // Include materials used by runtime products, even if no static renderer uses them yet.
        foreach (Component c in All<Component>(scene))
        {
            if (!c) continue;
            var properties = new SerializedObject(c).GetIterator();
            while (properties.Next(true))
                if (properties.propertyType == SerializedPropertyType.ObjectReference &&
                    (properties.objectReferenceValue is Material || properties.objectReferenceValue is Mesh))
                    assets.Add(properties.objectReferenceValue);
        }
        if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets/_Modules_/02_Levels/Level03/ArtFull", "V13Baked");
        var container = ScriptableObject.CreateInstance<C3FactoryLayoutAssets>();
        string path = AssetDatabase.GenerateUniqueAssetPath(AssetFolder + "/FactoryLayout.asset");
        AssetDatabase.CreateAsset(container, path);
        foreach (Object asset in assets)
            if (!EditorUtility.IsPersistent(asset)) AssetDatabase.AddObjectToAsset(asset, container);
        EditorUtility.SetDirty(container);
        AssetDatabase.SaveAssets();
    }
    public static string ValidateBaked(Scene scene)
    {
        ValidateScene(scene);
        var markers = All<C3BakedFactory>(scene);
        Require(markers.Length == 1, "Expected exactly one V13 bake manifest.");
        var marker = markers[0];
        Require(marker.version == 13 && marker.environment && marker.geometry && marker.circulation && marker.workshop && marker.stations, "Incomplete V13 builder bindings.");
        Require(marker.generatedObjects != null && marker.generatedObjects.Length > 1000 && marker.generatedObjects.All(g => g && g.scene == scene), "Missing generated objects. Bake will not overwrite or silently repair edits; undo the deletion or restore from Git.");
        foreach (string name in new[] { "C3_ReferenceRecyclingFactory_Visuals", C3RecyclingLineGeometry.RootName, "C3_V3_VerifiedCirculation", "EASTER_EGG_EcoQuantumWorkshop", "C3_Distributed_Materials_And_Energy" })
            Require(All<Transform>(scene).Count(t => t.name == name) == 1, "Duplicate or missing factory root: " + name);
        var geo = marker.geometry;
        Require(geo.BuildRoot && geo.MainEntry && geo.BMaterialInterface && geo.Visuals && geo.MainBelt, "Missing geometry bindings.");
        Require(geo.Sections.Count == 30 && geo.CommonSections.Count == 5 && geo.Machines.Count == 3, "Incomplete recycling routes.");
        Require(geo.Sections.Distinct().Count() == 30, "Duplicate conveyor references.");
        foreach (var s in geo.Sections)
            Require(s && s.Start && s.End && s.Surface && s.SurfaceRenderer && s.Width > 0 && s.Surface.enabled, "Missing conveyor surface/endpoints.");
        foreach (var m in geo.Machines)
            Require(m.Root && m.Intake && m.Tray && m.Output && m.Collection && m.Press && m.Pusher && m.Indicator && m.ProcessBed &&
                m.InputSections.Count == 4 && m.OutputSections.Count == 4 && m.InputSections.All(s => s) && m.OutputSections.All(s => s), "Missing machine references.");
        Physics.SyncTransforms();
        Require(geo.ConnectionsMatch(.01f), "Conveyor paths disconnected. Edit the surface and its endpoints together.");
        Require(marker.stations.StationCount == 4, "Expected four upgraded stations.");
        Require(All<EcoWorkshopInteractable>(scene).Length == 5 && All<EcoEnergyPickup>(scene).Length == 9, "Missing/duplicate workshop interactions or salvage.");
        foreach (GameObject g in marker.generatedObjects)
        {
            foreach (Renderer r in g.GetComponents<Renderer>())
                Require(r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m && m.shader && EditorUtility.IsPersistent(m)), "Missing or transient material: " + g.name);
            foreach (MeshFilter f in g.GetComponents<MeshFilter>()) Require(f.sharedMesh && EditorUtility.IsPersistent(f.sharedMesh), "Missing or transient mesh: " + g.name);
            foreach (MeshCollider c in g.GetComponents<MeshCollider>()) Require(c.sharedMesh && EditorUtility.IsPersistent(c.sharedMesh), "Missing collider mesh: " + g.name);
        }
        return "V13 baked factory validated: " + marker.generatedObjects.Length + " editable objects, 30 connected conveyor sections, 3 machines, 4 stations. No gameplay session saved.";
    }
    // Explicit batch entry point: only opens the on-disk scene in a fresh batch editor.
    public static void BakeBatch()
    {
        Require(Application.isBatchMode, "Batch bake requires a fresh -batchmode process.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        Debug.Log(Bake(scene));
        Require(EditorSceneManager.SaveScene(scene), "Scene save failed.");
        scene = EditorSceneManager.OpenScene(ScenePath);
        Debug.Log(ValidateBaked(scene));
    }
}
