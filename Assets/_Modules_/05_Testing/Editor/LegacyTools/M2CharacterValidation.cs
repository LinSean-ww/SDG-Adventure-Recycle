using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class M2CharacterValidation
{
    private const string ModelPath = "Assets/_Modules_/02_Levels/Level03/Models/fk.fbx";
    private const string PlayerPath = "Assets/_Modules_/02_Levels/Level03/Prefabs/M2_Player.prefab";
    private const string C3ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    private const string TestFolder = "Assets/_Modules_/05_Testing";
    private const string TestPrefabPath = TestFolder + "/Prefabs/M2_Player_CharacterTest.prefab";
    private const string TestScenePath = TestFolder + "/Scenes/M2_CharacterIsolation.unity";

    public static void Inspect()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new Exception("FAIL: fk.fbx has no ModelImporter");
        if (!importer.bakeAxisConversion)
        {
            importer.bakeAxisConversion = true;
            importer.SaveAndReimport();
        }
        Debug.Log($"M2_MODEL_IMPORT bakeAxisConversion={importer.bakeAxisConversion} globalScale={importer.globalScale} useFileScale={importer.useFileScale} fileScale={importer.fileScale} animationType={importer.animationType} importAnimation={importer.importAnimation}");

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new Exception("FAIL: fk.fbx did not import as GameObject");
        Debug.Log($"M2_MODEL_ROOT name={model.name} scale={model.transform.localScale} position={model.transform.localPosition} rotation={model.transform.localEulerAngles}");
        foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(bone, model.transform);
            Debug.Log($"M2_BONE path={path} localPosition={bone.localPosition} localRotation={bone.localEulerAngles} localScale={bone.localScale}");
        }
        foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            Debug.Log($"M2_SKIN name={skin.name} bones={skin.bones.Length} bounds={skin.localBounds} rootBone={(skin.rootBone ? skin.rootBone.name : "NULL")}");
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            Debug.Log($"M2_RENDERER type={renderer.GetType().Name} name={renderer.name} bounds={renderer.bounds}");
        foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            Debug.Log($"M2_ANIMATOR name={animator.name} avatar={(animator.avatar ? animator.avatar.name : "NULL")} controller={(animator.runtimeAnimatorController ? animator.runtimeAnimatorController.name : "NULL")}");
        foreach (UnityEngine.Object subAsset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            if (subAsset is AnimationClip clip)
                Debug.Log($"M2_CLIP name={clip.name} length={clip.length}");

        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            Debug.Log($"M2_PLAYER_PRE counts motor={player.GetComponentsInChildren<CharacterController>(true).Length} C3Controller={player.GetComponentsInChildren<C3TreasurePuzzle.C3FirstPersonController>(true).Length} camera={player.GetComponentsInChildren<Camera>(true).Length} listener={player.GetComponentsInChildren<AudioListener>(true).Length} interactor={player.GetComponentsInChildren<C3TreasurePuzzle.C3PlayerInteractor>(true).Length}");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
        Debug.Log("M2_INSPECT_COMPLETE");
    }

    public static void BuildIsolation()
    {
        if (!AssetDatabase.IsValidFolder(TestFolder)) throw new Exception("Testing root is missing");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabPath) != null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) != null)
            throw new Exception("Isolation assets already exist; refusing overwrite");

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (!source) throw new Exception("fk.fbx did not import");
        if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer) || !importer.bakeAxisConversion)
            throw new Exception("Bake Axis Conversion is not enabled");
        string[] required = {
            "BODYBONE/downbone/topbone/head",
            "BODYBONE/downbone/topbone/LtopHand",
            "BODYBONE/downbone/topbone/RtopHand",
            "BODYBONE/Ltopleg", "BODYBONE/Rtopleg"
        };
        foreach (string path in required)
            if (!source.transform.Find(path)) throw new Exception("Missing fk bone: " + path);

        Renderer skin = source.GetComponentInChildren<SkinnedMeshRenderer>();
        if (!skin || skin.bounds.size.y <= 0f) throw new Exception("fk has no measurable skinned mesh");
        float scale = 1.8f / skin.bounds.size.y;
        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            AddVisual(player, source, scale);
            VerifyPlayer(player);
            if (!PrefabUtility.SaveAsPrefabAsset(player, TestPrefabPath))
                throw new Exception("Could not save isolation player prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabPath);
        if (!prefab) throw new Exception("Isolation prefab did not import");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.position = Vector3.zero;
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Isolation Floor";
        floor.transform.position = new Vector3(0f, -0.25f, 0f);
        floor.transform.localScale = new Vector3(15f, 0.5f, 15f);
        GameObject light = new GameObject("Isolation Light");
        Light sun = light.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        if (!EditorSceneManager.SaveScene(scene, TestScenePath))
            throw new Exception("Could not save isolation scene");
        VerifyPlayer(instance);
        Debug.Log($"M2_ISOLATION_PASS scale={scale} prefab={TestPrefabPath} scene={TestScenePath}");
        AssetDatabase.SaveAssets();
    }

    private static void AddVisual(GameObject player, GameObject source, float scale)
    {
        if (player.transform.Find("fk")) throw new Exception("Player already has an fk visual");
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source, player.scene);
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        visual.transform.localScale = Vector3.one * scale;
        var bones = visual.AddComponent<C3TreasurePuzzle.C3BoneController>();
        if (!bones.TryResolveBones()) throw new Exception("C3BoneController could not resolve fk bones");
    }

    private static void VerifyPlayer(GameObject player)
    {
        if (player.GetComponentsInChildren<CharacterController>(true).Length != 1 ||
            player.GetComponentsInChildren<C3TreasurePuzzle.C3FirstPersonController>(true).Length != 1 ||
            player.GetComponentsInChildren<C3TreasurePuzzle.C3PlayerInteractor>(true).Length != 1 ||
            player.GetComponentsInChildren<Camera>(true).Length != 1 ||
            player.GetComponentsInChildren<AudioListener>(true).Length != 1 ||
            player.GetComponentsInChildren<C3TreasurePuzzle.C3BoneController>(true).Length != 1)
            throw new Exception("Isolation player component count mismatch");
        Transform visual = player.transform.Find("fk");
        if (!visual || !visual.Find("BODYBONE/downbone/topbone/head"))
            throw new Exception("Isolation player head reference is missing");
        var control = player.GetComponent<C3TreasurePuzzle.C3FirstPersonController>();
        var interaction = player.GetComponent<C3TreasurePuzzle.C3PlayerInteractor>();
        if (!new SerializedObject(control).FindProperty("viewTransform").objectReferenceValue ||
            !new SerializedObject(interaction).FindProperty("playerCamera").objectReferenceValue)
            throw new Exception("Existing C3 camera references are missing");
        foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
            if (!behaviour) throw new Exception("Isolation player has a Missing Script");
    }

    public static void CaptureIsolation()
    {
        EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
        var controller = UnityEngine.Object.FindObjectOfType<C3TreasurePuzzle.C3FirstPersonController>();
        GameObject player = controller ? controller.gameObject : null;
        if (!player) throw new Exception("Isolation player not found in test scene");
        Camera firstPerson = player.GetComponentInChildren<Camera>();
        if (!firstPerson) throw new Exception("First person camera missing");
        Capture(firstPerson, SDGAdventure.Testing.ReportOutputPaths.Resolve("M2_Character_FirstPerson_Aligned.png"));

        GameObject host = new GameObject("Temporary M2 observer");
        try
        {
            Camera observer = host.AddComponent<Camera>();
            observer.clearFlags = CameraClearFlags.SolidColor;
            observer.backgroundColor = new Color(0.25f, 0.3f, 0.35f);
            observer.fieldOfView = 50f;
            foreach (float z in new[] { -3f, 3f })
            {
                host.transform.position = new Vector3(0f, 1.1f, z);
                host.transform.LookAt(new Vector3(0f, 0.9f, 0f));
                Capture(observer, z < 0f ? SDGAdventure.Testing.ReportOutputPaths.Resolve("M2_Character_ViewNegativeZ_Aligned.png") :
                    SDGAdventure.Testing.ReportOutputPaths.Resolve("M2_Character_ViewPositiveZ_Aligned.png"));
            }
            foreach (float x in new[] { -3f, 3f })
            {
                host.transform.position = new Vector3(x, 1.1f, 0f);
                host.transform.LookAt(new Vector3(0f, 0.9f, 0f));
                Capture(observer, x < 0f ? SDGAdventure.Testing.ReportOutputPaths.Resolve("M2_Character_ViewNegativeX_Aligned.png") :
                    SDGAdventure.Testing.ReportOutputPaths.Resolve("M2_Character_ViewPositiveX_Aligned.png"));
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
        Debug.Log("M2_CAPTURE_COMPLETE");
    }

    public static void AlignIsolation()
    {
        GameObject player = PrefabUtility.LoadPrefabContents(TestPrefabPath);
        try
        {
            Transform visual = player.transform.Find("fk");
            if (!visual) throw new Exception("Isolation visual is missing");
            visual.localRotation = Quaternion.Euler(0f, -90f, 0f);
            VerifyPlayer(player);
            if (!PrefabUtility.SaveAsPrefabAsset(player, TestPrefabPath))
                throw new Exception("Could not save aligned isolation prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
        Debug.Log("M2_ISOLATION_ALIGNED Y=-90");
    }

    public static void IntegrateFormal()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (!source) throw new Exception("fk.fbx is missing");
        if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer) || !importer.bakeAxisConversion)
            throw new Exception("Bake Axis Conversion is not enabled");
        GameObject test = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabPath);
        if (!test || !test.transform.Find("fk")) throw new Exception("Isolation prefab is not validated");
        VerifyPlayer(test);

        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            if (player.transform.Find("fk")) throw new Exception("Formal player already has fk; refusing duplicate");
            AddVisual(player, source, test.transform.Find("fk").localScale.x);
            VerifyPlayer(player);
            if (!PrefabUtility.SaveAsPrefabAsset(player, PlayerPath))
                throw new Exception("Could not save formal player prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
        VerifyFormal();
        Debug.Log("M2_FORMAL_INTEGRATION_PASS");
    }

    public static void VerifyFormal()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        if (!prefab) throw new Exception("Formal player prefab did not import");
        VerifyPlayer(prefab);
        Scene scene = EditorSceneManager.OpenScene(C3ScenePath, OpenSceneMode.Single);
        int playerCount = 0, cameraCount = 0, listenerCount = 0, missingScripts = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            playerCount += root.GetComponentsInChildren<C3TreasurePuzzle.C3FirstPersonController>(true).Length;
            cameraCount += root.GetComponentsInChildren<Camera>(true).Length;
            listenerCount += root.GetComponentsInChildren<AudioListener>(true).Length;
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (!behaviour) missingScripts++;
        }
        if (playerCount != 1 || cameraCount != 1 || listenerCount != 1 || missingScripts != 0)
            throw new Exception($"C3 scene validation failed: player={playerCount} camera={cameraCount} listener={listenerCount} missing={missingScripts}");
        Debug.Log($"M2_FORMAL_VERIFY_PASS player={playerCount} camera={cameraCount} listener={listenerCount} missing={missingScripts}");
    }

    public static void WireCameraLook()
    {
        WireCameraPrefab(TestPrefabPath);
        WireCameraPrefab(PlayerPath);
        VerifyCameraWiring();
        AssetDatabase.SaveAssets();
        Debug.Log("M2H_CAMERA_WIRING_PASS");
    }

    private static void WireCameraPrefab(string path)
    {
        GameObject player = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var controller = player.GetComponent<C3TreasurePuzzle.C3FirstPersonController>();
            Transform head = player.transform.Find("fk/BODYBONE/downbone/topbone/head");
            if (!controller || !head) throw new Exception("Camera target missing in " + path);
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("headBone").objectReferenceValue = head;
            serialized.FindProperty("mouseSensitivity").floatValue = 2f;
            serialized.FindProperty("minLookAngle").floatValue = -60f;
            serialized.FindProperty("maxLookAngle").floatValue = 48f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!PrefabUtility.SaveAsPrefabAsset(player, path))
                throw new Exception("Could not save camera wiring in " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
    }

    public static void VerifyCameraWiring()
    {
        foreach (string path in new[] { TestPrefabPath, PlayerPath })
        {
            GameObject player = PrefabUtility.LoadPrefabContents(path);
            try
            {
                VerifyPlayer(player);
                var controller = player.GetComponent<C3TreasurePuzzle.C3FirstPersonController>();
                var fields = new SerializedObject(controller);
                Transform head = player.transform.Find("fk/BODYBONE/downbone/topbone/head");
                if (fields.FindProperty("headBone").objectReferenceValue != head ||
                    fields.FindProperty("viewTransform").objectReferenceValue !=
                        player.GetComponentInChildren<Camera>().transform ||
                    !Mathf.Approximately(fields.FindProperty("mouseSensitivity").floatValue, 2f) ||
                    !Mathf.Approximately(fields.FindProperty("minLookAngle").floatValue, -60f) ||
                    !Mathf.Approximately(fields.FindProperty("maxLookAngle").floatValue, 48f))
                    throw new Exception("Camera wiring invalid in " + path);
                foreach (MonoBehaviour component in player.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component && component.GetType().Name == "CameraLook")
                        throw new Exception("Duplicate CameraLook found in " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }
        VerifyFormal();
        Debug.Log("M2H_CAMERA_VERIFY_PASS");
    }

    public static void CaptureCameraExtremes()
    {
        EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
        var controller = UnityEngine.Object.FindObjectOfType<C3TreasurePuzzle.C3FirstPersonController>();
        if (!controller) throw new Exception("Isolation controller is missing");
        Transform player = controller.transform;
        Camera camera = controller.GetComponentInChildren<Camera>();
        Transform head = player.Find("fk/BODYBONE/downbone/topbone/head");
        if (!camera || !head) throw new Exception("Isolation camera or head is missing");
        Quaternion headRest = head.localRotation;
        foreach (float pitch in new[] { -60f, 48f })
        {
            camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            head.rotation = Quaternion.AngleAxis(pitch, player.right) *
                (head.parent.rotation * headRest);
            Capture(camera, pitch < 0f ? SDGAdventure.Testing.ReportOutputPaths.Resolve("M2H_FirstPerson_Up60.png") :
                SDGAdventure.Testing.ReportOutputPaths.Resolve("M2H_FirstPerson_Down48.png"));
            if (pitch > 0f)
            {
                Renderer[] renderers = player.Find("fk").GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers) renderer.enabled = false;
                Capture(camera, SDGAdventure.Testing.ReportOutputPaths.Resolve("M2H_FirstPerson_Down48_ModelHidden.png"));
                foreach (Renderer renderer in renderers) renderer.enabled = true;
            }
        }
        Debug.Log("M2H_EXTREME_CAPTURE_COMPLETE");
    }

    private static void Capture(Camera camera, string path)
    {
        RenderTexture render = new RenderTexture(960, 540, 24);
        Texture2D image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        RenderTexture prior = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = render;
            camera.Render();
            RenderTexture.active = render;
            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log("M2_CAPTURE path=" + path);
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = prior;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(render);
        }
    }
}

