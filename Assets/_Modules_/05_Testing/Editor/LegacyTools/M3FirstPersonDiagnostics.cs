using System;
using System.IO;
using C3TreasurePuzzle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class M3FirstPersonDiagnostics
{
    private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    private static readonly string Output = SDGAdventure.Testing.ReportOutputPaths.Resolve("M3_FirstPerson_Diagnostics");
    private const string TestScenePath = "Assets/_Modules_/05_Testing/Scenes/M2_CharacterIsolation.unity";
    private const string TestPrefabPath = "Assets/_Modules_/05_Testing/Prefabs/M2_Player_CharacterTest.prefab";
    private const string FormalPrefabPath = "Assets/_Modules_/02_Levels/Level03/Prefabs/M2_Player.prefab";
    private const int HiddenLayer = 2; // Built-in Ignore Raycast; C3 scene has no existing objects on this layer.

    public static void InspectAndCapture() => Inspect(ScenePath, Output);

    public static void InspectIsolation() => Inspect(TestScenePath, SDGAdventure.Testing.ReportOutputPaths.Resolve("M3_Isolation_Diagnostics"));

    private static void Inspect(string scenePath, string output)
    {
        Directory.CreateDirectory(output);
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        C3FirstPersonController control = UnityEngine.Object.FindObjectOfType<C3FirstPersonController>();
        if (!control) throw new Exception("C3 player is missing");
        Transform player = control.transform;
        Transform visual = player.Find("fk");
        Transform head = visual ? visual.Find("BODYBONE/downbone/topbone/head") : null;
        Camera camera = control.GetComponentInChildren<Camera>();
        C3BoneController bones = control.GetComponentInChildren<C3BoneController>();
        if (!visual || !head || !camera || !bones) throw new Exception("M3 model, head, camera or bones missing");
        Renderer[] modelRenderers = visual.GetComponentsInChildren<Renderer>(true);
        Debug.Log($"M3_HIERARCHY player={player.name} cameraParent={camera.transform.parent.name} visualParent={visual.parent.name} headPath=BODYBONE/downbone/topbone/head rendererCount={modelRenderers.Length}");
        Debug.Log($"M3_CAMERA localPosition={camera.transform.localPosition} localRotation={camera.transform.localEulerAngles} near={camera.nearClipPlane} far={camera.farClipPlane} mask={camera.cullingMask} fov={camera.fieldOfView}");
        Debug.Log($"M3_MODEL localPosition={visual.localPosition} localRotation={visual.localEulerAngles} localScale={visual.localScale} headWorld={head.position} cameraWorld={camera.transform.position} headDistance={Vector3.Distance(head.position, camera.transform.position)} bonesEnabled={bones.enabled}");
        foreach (Renderer renderer in modelRenderers)
            Debug.Log($"M3_RENDERER name={renderer.name} type={renderer.GetType().Name} layer={renderer.gameObject.layer} bounds={renderer.bounds} shadows={renderer.shadowCastingMode} enabled={renderer.enabled}");

        Quaternion headRest = head.localRotation;
        Vector3 cameraRestPosition = camera.transform.localPosition;
        Quaternion cameraRestRotation = camera.transform.localRotation;
        foreach (float eyeHeight in new[] { 1.5f, 0.85f })
        {
            foreach (float pitch in new[] { -60f, -30f, 0f, 30f, 48f })
            {
                camera.transform.localPosition = new Vector3(cameraRestPosition.x, eyeHeight, cameraRestPosition.z);
                camera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                head.rotation = Quaternion.AngleAxis(pitch, player.right) *
                    (head.parent.rotation * headRest);
                string label = $"eye{eyeHeight:0.00}_pitch{pitch:+0;-0;0}".Replace('.', '_');
                Color32[] visible = Capture(camera, Path.Combine(output, label + "_visible.png"));
                foreach (Renderer renderer in modelRenderers)
                    if (renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly) renderer.enabled = false;
                Color32[] hidden = Capture(camera, Path.Combine(output, label + "_hidden.png"));
                foreach (Renderer renderer in modelRenderers)
                    if (renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly) renderer.enabled = true;
                int changed = 0;
                for (int i = 0; i < visible.Length; i++)
                    if (Math.Abs(visible[i].r - hidden[i].r) > 3 ||
                        Math.Abs(visible[i].g - hidden[i].g) > 3 ||
                        Math.Abs(visible[i].b - hidden[i].b) > 3)
                        changed++;
                Debug.Log($"M3_SELF_OCCLUSION {label} changedPixels={changed} totalPixels={visible.Length}");
            }
        }
        camera.transform.localPosition = cameraRestPosition;
        camera.transform.localRotation = cameraRestRotation;
        head.localRotation = headRest;
        Debug.Log("M3_DIAGNOSTIC_COMPLETE");
    }

    public static void ApplyIsolationCulling()
    {
        GameObject player = PrefabUtility.LoadPrefabContents(TestPrefabPath);
        try
        {
            Transform visual = player.transform.Find("fk");
            Camera camera = player.GetComponentInChildren<Camera>();
            if (!visual || !camera) throw new Exception("Isolation player is incomplete");
            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = HiddenLayer;
            camera.cullingMask &= ~(1 << HiddenLayer);
            if (!PrefabUtility.SaveAsPrefabAsset(player, TestPrefabPath))
                throw new Exception("Could not save isolation culling test prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
        Debug.Log("M3_ISOLATION_CULLING_APPLIED layer=2");
    }

    public static void ApplyIsolationShadowProxy()
    {
        GameObject player = PrefabUtility.LoadPrefabContents(TestPrefabPath);
        try
        {
            Transform visual = player.transform.Find("fk");
            SkinnedMeshRenderer skin = visual ? visual.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (!skin) throw new Exception("Isolation skin missing");
            Transform existing = skin.transform.parent.Find("M3_FirstPersonShadow");
            if (existing) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            GameObject proxy = new GameObject("M3_FirstPersonShadow");
            proxy.transform.SetParent(skin.transform.parent, false);
            proxy.transform.localPosition = skin.transform.localPosition;
            proxy.transform.localRotation = skin.transform.localRotation;
            proxy.transform.localScale = skin.transform.localScale;
            proxy.layer = 0;
            SkinnedMeshRenderer shadow = proxy.AddComponent<SkinnedMeshRenderer>();
            EditorUtility.CopySerialized(skin, shadow);
            shadow.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            shadow.receiveShadows = false;
            skin.shadowCastingMode = ShadowCastingMode.Off;
            if (!PrefabUtility.SaveAsPrefabAsset(player, TestPrefabPath))
                throw new Exception("Could not save isolation shadow proxy");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
        Debug.Log("M3_ISOLATION_SHADOW_PROXY_APPLIED");
    }

    public static void ApplyFormalFix()
    {
        GameObject player = PrefabUtility.LoadPrefabContents(FormalPrefabPath);
        try
        {
            Transform visual = player.transform.Find("fk");
            Camera camera = player.GetComponentInChildren<Camera>(true);
            SkinnedMeshRenderer skin = visual ? visual.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (!visual || !camera || !skin || !player.GetComponent<CharacterController>() ||
                !player.GetComponent<C3FirstPersonController>() ||
                !player.GetComponentInChildren<C3BoneController>(true))
                throw new Exception("Formal prefab dependencies are incomplete");
            if (visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 1 ||
                skin.transform.parent != visual ||
                visual.Find("M3_FirstPersonShadow"))
                throw new Exception("Formal model hierarchy differs from the tested isolation prefab");
            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = HiddenLayer;
            camera.cullingMask &= ~(1 << HiddenLayer);
            GameObject proxy = new GameObject("M3_FirstPersonShadow");
            proxy.transform.SetParent(visual, false);
            proxy.transform.localPosition = skin.transform.localPosition;
            proxy.transform.localRotation = skin.transform.localRotation;
            proxy.transform.localScale = skin.transform.localScale;
            proxy.layer = 0;
            SkinnedMeshRenderer shadow = proxy.AddComponent<SkinnedMeshRenderer>();
            EditorUtility.CopySerialized(skin, shadow);
            shadow.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            shadow.receiveShadows = false;
            skin.shadowCastingMode = ShadowCastingMode.Off;
            if (!PrefabUtility.SaveAsPrefabAsset(player, FormalPrefabPath))
                throw new Exception("Could not save formal M3 player prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
        Debug.Log("M3_FORMAL_FIX_APPLIED");
    }

    public static void ValidateIsolationExterior() => ValidateExterior(TestScenePath, SDGAdventure.Testing.ReportOutputPaths.Resolve("M3_Isolation_Exterior"));
    public static void ValidateFormalExterior() => ValidateExterior(ScenePath, SDGAdventure.Testing.ReportOutputPaths.Resolve("M3_Formal_Exterior"));

    public static void ValidateFormalRig()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        C3FirstPersonController control = UnityEngine.Object.FindObjectOfType<C3FirstPersonController>();
        if (!control) throw new Exception("C3 controller missing");
        GameObject player = control.gameObject;
        Transform visual = player.transform.Find("fk");
        SkinnedMeshRenderer skin = visual ? visual.GetComponentInChildren<SkinnedMeshRenderer>() : null;
        SkinnedMeshRenderer proxy = visual ? visual.Find("M3_FirstPersonShadow")?.GetComponent<SkinnedMeshRenderer>() : null;
        Camera camera = player.GetComponentInChildren<Camera>();
        if (!skin || !proxy || !camera || !player.GetComponent<CharacterController>() ||
            !player.GetComponent<C3PlayerInteractor>() ||
            !player.GetComponentInChildren<C3BoneController>() ||
            player.GetComponentsInChildren<Camera>(true).Length != 1 ||
            player.GetComponentsInChildren<AudioListener>(true).Length != 1 ||
            skin.gameObject.layer != HiddenLayer || proxy.gameObject.layer != 0 ||
            (camera.cullingMask & (1 << HiddenLayer)) != 0 ||
            skin.shadowCastingMode != ShadowCastingMode.Off ||
            proxy.shadowCastingMode != ShadowCastingMode.ShadowsOnly ||
            skin.sharedMesh != proxy.sharedMesh ||
            skin.rootBone != proxy.rootBone || skin.bones.Length != proxy.bones.Length)
            throw new Exception("M3 formal player rig validation failed");
        for (int i = 0; i < skin.bones.Length; i++)
            if (skin.bones[i] != proxy.bones[i]) throw new Exception($"Shadow bone mismatch {i}");
        Debug.Log($"M3_FORMAL_RIG_PASS bones={skin.bones.Length} cameraCount=1 listenerCount=1 interactor=True controller=True");
    }

    private static void ValidateExterior(string scenePath, string output)
    {
        Directory.CreateDirectory(output);
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        C3FirstPersonController control = UnityEngine.Object.FindObjectOfType<C3FirstPersonController>();
        if (!control) throw new Exception("Isolation player missing");
        Transform player = control.transform;
        Transform visual = player.Find("fk");
        Camera firstPerson = player.GetComponentInChildren<Camera>();
        SkinnedMeshRenderer skin = visual ? visual.GetComponentInChildren<SkinnedMeshRenderer>() : null;
        SkinnedMeshRenderer proxy = visual ? visual.Find("M3_FirstPersonShadow")?.GetComponent<SkinnedMeshRenderer>() : null;
        if (!skin || !firstPerson) throw new Exception("Isolation mesh or camera missing");
        Debug.Log($"M3_EXTERIOR_CONFIGURATION quality={QualitySettings.GetQualityLevel()} shadows={QualitySettings.shadows} lightCount={UnityEngine.Object.FindObjectsOfType<Light>().Length} proxy={!!proxy} skinLayer={skin.gameObject.layer} skinCasting={skin.shadowCastingMode} proxyLayer={(proxy ? proxy.gameObject.layer : -1)} proxyCasting={(proxy ? proxy.shadowCastingMode.ToString() : "none")}");
        Light light = UnityEngine.Object.FindObjectOfType<Light>();
        if (!light) throw new Exception("Isolation light missing");
        light.shadows = LightShadows.Soft;
        GameObject host = new GameObject("M3 Temporary Observer");
        try
        {
            Camera observer = host.AddComponent<Camera>();
            observer.clearFlags = CameraClearFlags.SolidColor;
            observer.backgroundColor = new Color(0.2f, 0.25f, 0.3f);
            observer.fieldOfView = 50f;
            observer.cullingMask = ~0;
            host.transform.position = player.position + new Vector3(0f, 1.2f, -3f);
            host.transform.LookAt(player.position + new Vector3(0f, 0.9f, 0f));
            Color32[] thirdPersonVisible = Capture(observer, Path.Combine(output, "third_person_visible.png"));
            skin.enabled = false;
            if (proxy) proxy.enabled = false;
            Color32[] thirdPersonHidden = Capture(observer, Path.Combine(output, "third_person_hidden.png"));
            skin.enabled = true;
            if (proxy) proxy.enabled = true;
            Debug.Log($"M3_THIRD_PERSON_MODEL_PIXELS {CountChanged(thirdPersonVisible, thirdPersonHidden)}");

            observer.cullingMask = firstPerson.cullingMask;
            Color32[] shadowCandidate = Capture(observer, Path.Combine(output, "camera_mask_shadow_candidate.png"));
            skin.enabled = false;
            if (proxy) proxy.enabled = false;
            Color32[] noModel = Capture(observer, Path.Combine(output, "camera_mask_no_model.png"));
            skin.enabled = true;
            if (proxy) proxy.enabled = true;
            Debug.Log($"M3_CAMERA_MASK_SHADOW_PIXELS {CountChanged(shadowCandidate, noModel)}");

            skin.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            Color32[] shadowOnly = Capture(observer, Path.Combine(output, "shadow_only_candidate.png"));
            skin.enabled = false;
            if (proxy) proxy.enabled = false;
            Color32[] noShadow = Capture(observer, Path.Combine(output, "shadow_only_no_model.png"));
            skin.enabled = true;
            if (proxy) proxy.enabled = true;
            skin.shadowCastingMode = ShadowCastingMode.On;
            Debug.Log($"M3_SHADOW_ONLY_PIXELS {CountChanged(shadowOnly, noShadow)}");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
        Debug.Log("M3_EXTERIOR_DIAGNOSTIC_COMPLETE");
    }

    private static int CountChanged(Color32[] left, Color32[] right)
    {
        int changed = 0;
        for (int i = 0; i < left.Length; i++)
            if (Math.Abs(left[i].r - right[i].r) > 3 ||
                Math.Abs(left[i].g - right[i].g) > 3 ||
                Math.Abs(left[i].b - right[i].b) > 3)
                changed++;
        return changed;
    }

    private static Color32[] Capture(Camera camera, string path)
    {
        RenderTexture texture = new RenderTexture(640, 360, 24);
        Texture2D image = new Texture2D(640, 360, TextureFormat.RGB24, false);
        RenderTexture oldActive = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
            image.Apply();
            Color32[] pixels = image.GetPixels32();
            File.WriteAllBytes(path, image.EncodeToPNG());
            return pixels;
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
