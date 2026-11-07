using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class C3V13FactoryBakeTests
{
    const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    const string CopyPath = "Assets/V13_TestSceneCopy.unity";
    const string AssetsPath = "Assets/_Modules_/02_Levels/Level03/ArtFull/V13Baked";
    static Type Runtime(string name) => Type.GetType("C3TreasurePuzzle." + name + ", Assembly-CSharp", true);
    static object Tool(string name) => Type.GetType("C3V13FactorySceneTools, Assembly-CSharp-Editor", true)
        .GetMethod(name).Invoke(null, new object[] { SceneManager.GetActiveScene() });
    static Transform[] Objects() => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
    static Component Manifest() => Object.FindObjectOfType(Runtime("C3BakedFactory")) as Component;
    static object Member(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(owner)
        ?? owner.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance).GetValue(owner);
    [Serializable] sealed class SavedSetup { public string[] paths; public bool[] loaded, active; public string[] assets; }
    [Serializable] sealed class LayoutPose { public Vector3[] positions, scales; public Quaternion[] rotations; }
    const string SetupKey = "C3V13FactoryBakeTests.Setup";

    [UnitySetUp] public IEnumerator Open()
    {
        // Unity Test Runner calls setup again after Enter/ExitPlayMode domain reload.
        if (!string.IsNullOrEmpty(SessionState.GetString(SetupKey, ""))) yield break;
        Assert.IsFalse(SceneManager.GetActiveScene().isDirty, "Save user changes before running scene tests.");
        Assert.IsFalse(File.Exists(CopyPath), "A pre-existing test copy must not be overwritten.");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        SessionState.SetString(SetupKey, JsonUtility.ToJson(new SavedSetup {
            paths = previous.Select(s => s.path).ToArray(), loaded = previous.Select(s => s.isLoaded).ToArray(),
            active = previous.Select(s => s.isActive).ToArray(),
            assets = AssetDatabase.FindAssets("", new[] { AssetsPath }).Select(AssetDatabase.GUIDToAssetPath).ToArray() }));
        EditorSceneManager.OpenScene(ScenePath);
    }
    [UnityTearDown] public IEnumerator Close()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        Undo.ClearAll();
        var saved = JsonUtility.FromJson<SavedSetup>(SessionState.GetString(SetupKey, "{}"));
        SessionState.EraseString(SetupKey);
        if (saved.paths == null) yield break;
        var previous = saved.paths.Select((path, i) => new SceneSetup { path = path, isLoaded = saved.loaded[i], isActive = saved.active[i] }).ToArray();
        if (previous.Any(s => s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AssetDatabase.DeleteAsset(CopyPath);
        foreach (string path in AssetDatabase.FindAssets("", new[] { AssetsPath }).Select(AssetDatabase.GUIDToAssetPath).Distinct())
            if (!saved.assets.Contains(path) && path.EndsWith(".asset", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
    }
    [Test] public void SavedFactoryHasPersistentMeshesMaterialsAndConnectedBindings()
    {
        StringAssert.Contains("validated", (string)Tool("ValidateBaked"));
        Assert.IsNotNull(Manifest());
        Assert.IsFalse(SceneManager.GetActiveScene().isDirty);
        Assert.IsNull(Object.FindObjectOfType(Runtime("C3RecyclingLine")), "A gameplay session was serialized.");
    }
    [Test] public void RepeatedBakePreservesManualEditsAndReferencesAfterSaveReload()
    {
        byte[] disk = File.ReadAllBytes(ScenePath);
        var scene = SceneManager.GetActiveScene();
        Transform target = Objects().First(t => t.name == "Industrial wall-longitudinal supply");
        target.position += Vector3.up * .125f;
        Vector3 edited = target.position;
        var addition = new GameObject("V13_ArtistOwnedChild"); addition.transform.SetParent(target, false);
        int count = Objects().Length;
        int colliderCount = Objects().Sum(t => t.GetComponents<Collider>().Length);
        for (int i = 0; i < 3; i++)
        {
            StringAssert.Contains("no changes", (string)Tool("Bake"));
            Assert.AreEqual(count, Objects().Length);
            Assert.AreEqual(edited, target.position);
            Assert.IsTrue(addition);
        }
        Assert.IsTrue(EditorSceneManager.SaveScene(scene, CopyPath, true));
        EditorSceneManager.OpenScene(CopyPath);
        Transform reloaded = Objects().Single(t => t.Find("V13_ArtistOwnedChild"));
        Assert.AreEqual(edited, reloaded.position);
        Assert.AreEqual(count, Objects().Length);
        Assert.AreEqual(colliderCount, Objects().Sum(t => t.GetComponents<Collider>().Length));
        var geometry = (Component)Member(Manifest(), "geometry");
        Assert.AreEqual(3, ((IList)Member(geometry, "Machines")).Count);
        foreach (object section in (IEnumerable)Member(geometry, "Sections"))
        {
            Assert.IsTrue((Transform)Member(section, "Start"));
            Assert.IsTrue((Transform)Member(section, "End"));
            Assert.IsTrue(((BoxCollider)Member(section, "Surface")).enabled);
        }
        CollectionAssert.AreEqual(disk, File.ReadAllBytes(ScenePath), "Test wrote the production scene.");
    }
    [Test] public void DamagedBakeRefusesToRebuildOrOverwriteUserObjects()
    {
        var marker = Manifest();
        var generated = (GameObject[])Member(marker, "generatedObjects");
        Object.DestroyImmediate(generated.First(g => g.name == "Industrial wall-longitudinal supply"));
        var user = new GameObject("ArtistWorkMustSurvive");
        int count = Objects().Length;
        var error = Assert.Throws<TargetInvocationException>(() => Tool("Bake"));
        StringAssert.Contains("Missing generated objects", error.InnerException.Message);
        Assert.IsTrue(user);
        Assert.AreEqual(count, Objects().Length);
    }
    [Test] public void FreshBakeUndoRedoRestoresHierarchyCollidersAndBindings()
    {
        // Make an unsaved, isolated pre-generation fixture from manifest-owned objects.
        // No disk asset or user object is deleted by production tooling.
        var marker = Manifest();
        var owned = new HashSet<GameObject>((GameObject[])Member(marker, "generatedObjects"));
        var owner = marker.gameObject;
        foreach (GameObject root in owned.Where(g => !g.transform.parent || !owned.Contains(g.transform.parent.gameObject)).ToArray())
            Object.DestroyImmediate(root);
        foreach (string name in new[] { "C3BakedFactory", "C3ReferenceFactoryEnvironment", "C3RecyclingLineGeometry", "C3RecyclingVisuals", "C3V3Circulation", "EcoFactoryWorkshop", "C3WorkstationVisualUpgrade" })
            Object.DestroyImmediate(owner.GetComponent(Runtime(name)));
        Undo.ClearAll();
        int original = Objects().Length;
        var colliders = Objects().SelectMany(t => t.GetComponents<Collider>()).ToDictionary(c => c, c => c.enabled);
        Tool("Preview"); Assert.AreEqual(original, Objects().Length);
        Tool("Bake"); int baked = Objects().Length;
        Assert.Greater(baked, original + 1000);
        Undo.PerformUndo();
        Assert.AreEqual(original, Objects().Length);
        Assert.IsNull(Manifest());
        foreach (var entry in colliders) Assert.AreEqual(entry.Value, entry.Key.enabled);
        Undo.PerformRedo();
        Assert.AreEqual(baked, Objects().Length);
        StringAssert.Contains("validated", (string)Tool("ValidateBaked"));
    }
    [UnityTest] public IEnumerator PlayStopPreservesAuthoredFactoryAndDiskScene()
    {
        SessionState.SetString("V13.DiskHash", DiskHash());
        SessionState.SetInt("V13.AuthoredCount", ((GameObject[])Member(Manifest(), "generatedObjects")).Length);
        SessionState.SetString("V13.LayoutPose", JsonUtility.ToJson(ReadLayoutPose()));
        Capture("Edit");
        yield return new EnterPlayMode();
        yield return null;
        yield return null;
        Assert.IsNotNull(Object.FindObjectOfType(Runtime("C3RecyclingLine")));
        Assert.AreEqual(SessionState.GetInt("V13.AuthoredCount", -1), ((GameObject[])Member(Manifest(), "generatedObjects")).Length);
        var expected = JsonUtility.FromJson<LayoutPose>(SessionState.GetString("V13.LayoutPose", ""));
        var actual = ReadLayoutPose();
        for (int i = 0; i < expected.positions.Length; i++)
        {
            Assert.Less(Vector3.Distance(expected.positions[i], actual.positions[i]), .0001f, "Authored position changed on entering Play: " + i);
            Assert.Less(Quaternion.Angle(expected.rotations[i], actual.rotations[i]), .05f, "Authored rotation changed: " + i);
            Assert.Less(Vector3.Distance(expected.scales[i], actual.scales[i]), .0001f, "Authored scale changed: " + i);
        }
        Capture("Play");
        foreach (string name in new[] { "C3_ReferenceRecyclingFactory_Visuals", "C3_P2_RecyclingLine", "C3_V3_VerifiedCirculation" })
            Assert.AreEqual(1, Objects().Count(t => t.name == name));
        yield return new ExitPlayMode();
        StringAssert.Contains("validated", (string)Tool("ValidateBaked"));
        Assert.AreEqual(SessionState.GetString("V13.DiskHash", ""), DiskHash());
        Assert.IsFalse(SceneManager.GetActiveScene().isDirty);
        SessionState.EraseString("V13.DiskHash"); SessionState.EraseInt("V13.AuthoredCount");
        SessionState.EraseString("V13.LayoutPose");
    }
    static string DiskHash()
    {
        using (var hash = System.Security.Cryptography.SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(ScenePath)));
    }
    static LayoutPose ReadLayoutPose()
    {
        var objects = ((GameObject[])Member(Manifest(), "generatedObjects")).Select(g => g.transform).ToArray();
        return new LayoutPose { positions = objects.Select(t => t.position).ToArray(), rotations = objects.Select(t => t.rotation).ToArray(), scales = objects.Select(t => t.lossyScale).ToArray() };
    }
    static void Capture(string label)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var scene = SceneManager.GetActiveScene();
        var preview = EditorSceneManager.NewPreviewScene();
        var go = new GameObject("V13 matched visual inspection") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(go, preview);
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var previousTarget = RenderTexture.active;
        Texture2D capture = null;
        try
        {
            var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.transform.position = new Vector3(-12.5f, 6.9f, 2.4f);
            camera.transform.LookAt(new Vector3(-12.5f, 2.7f, 15.5f));
            camera.fieldOfView = 65f; camera.aspect = 1280f / 720f; camera.farClipPlane = 100f;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            capture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); capture.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/V13-" + label + ".png", capture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (capture) Object.DestroyImmediate(capture);
            Object.DestroyImmediate(go); RenderTexture.ReleaseTemporary(target);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
