using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Explicit, read-only render preview of the authoritative runtime factory.
/// Captures survive Play/domain reload on disk in ignored UserSettings. Never
/// invokes builders in Edit Mode, bakes gameplay or saves the loaded scene.
/// </summary>
public sealed class C3V12FactoryPreview : EditorWindow
{
    private const string DirectoryPath="UserSettings/C3V12FactoryPreview";
    private const string ScenePath="Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    [Serializable] private sealed class Pose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Fov,Near,Far,Aspect;
        public int Mask,Width,Height;
        public Color Background;
        public CameraClearFlags Clear;
        public bool Hdr;
    }
    private Texture2D edit,play;
    private Vector2 scroll;
    [MenuItem("SDG Adventure/Level03 V12/Factory Preview")]
    public static void Open()=>GetWindow<C3V12FactoryPreview>("C3 Factory Preview");
    private void OnEnable()=>LoadImages();
    private void OnDisable()=>ReleaseImages();
    private void ReleaseImages()
    {
        if(edit)DestroyImmediate(edit);if(play)DestroyImmediate(play);
        edit=play=null;
    }
    private void LoadImages()
    {
        ReleaseImages();edit=Load("Edit.png");play=Load("Play.png");Repaint();
    }
    private static Texture2D Load(string file)
    {
        string path=Path.Combine(DirectoryPath,file);
        if(!File.Exists(path))return null;
        Texture2D image=new Texture2D(2,2){hideFlags=HideFlags.HideAndDontSave};
        image.LoadImage(File.ReadAllBytes(path));return image;
    }
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("正式配置由 Play 的 FactoryPresentation 建構。先在 Edit 選定相機，擷取基準；進入 Play 後擷取正式配置。兩次使用完全相同的 pose / FOV / mask / resolution。停止 Play 後仍可在此查看正式工廠。這是唯讀影像預覽，不是場景烘焙，也不會讓舊版 Game 視窗自動變成正式配置。",MessageType.Info);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            if(GUILayout.Button("1. Capture Edit camera baseline"))Capture(false);
        using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            if(GUILayout.Button("2. Capture official Play factory at the SAME camera pose"))Capture(true);
        if(GUILayout.Button("Reload preview images"))LoadImages();
        if(GUILayout.Button("Clear preview"))
        {
            ReleaseImages();
            foreach(string name in new[]{"Edit.png","Play.png","Camera.json"})
            {string path=Path.Combine(DirectoryPath,name);if(File.Exists(path))File.Delete(path);}
        }
        scroll=EditorGUILayout.BeginScrollView(scroll);
        Show("Edit: serialized legacy presentation",edit);
        Show("Official factory: captured runtime presentation (available outside Play)",play);
        EditorGUILayout.EndScrollView();
    }
    private static void Show(string title,Texture2D image)
    {
        EditorGUILayout.LabelField(title,EditorStyles.boldLabel);
        if(!image)return;
        Rect rect=GUILayoutUtility.GetAspectRect(image.width/(float)image.height);
        GUI.DrawTexture(rect,image,ScaleMode.ScaleToFit);
    }
    private void Capture(bool runtime)
    {
        Camera source=Selection.activeGameObject?Selection.activeGameObject.GetComponent<Camera>():null;
        if(!source)source=Camera.main;
        if(!source || source.gameObject.scene.path!=ScenePath)
        {Debug.LogError("Select the C_Main camera; no other scene is supported.");return;}
        string posePath=Path.Combine(DirectoryPath,"Camera.json");
        Directory.CreateDirectory(DirectoryPath);
        Pose pose;
        if(runtime)
        {
            if(!GameObject.Find("C3_P2_RecyclingLine") || !GameObject.Find("C3_V3_VerifiedCirculation"))
            {Debug.LogError("Wait for the official factory builders to finish.");return;}
            if(!File.Exists(posePath)){Debug.LogError("Capture the Edit baseline first.");return;}
            pose=JsonUtility.FromJson<Pose>(File.ReadAllText(posePath));
        }
        else
        {
            pose=new Pose {Position=source.transform.position,Rotation=source.transform.rotation,
                Fov=source.fieldOfView,Near=source.nearClipPlane,Far=source.farClipPlane,
                Aspect=source.aspect,Mask=source.cullingMask,Width=1280,
                Height=Mathf.Clamp(Mathf.RoundToInt(1280f/source.aspect),360,2160),
                Background=source.backgroundColor,Clear=source.clearFlags,Hdr=source.allowHDR};
            File.WriteAllText(posePath,JsonUtility.ToJson(pose,true));
        }
        // Camera lives in a disposable preview scene and renders the source scene.
        // No scripts, colliders, legacy toggles, transforms or lights are changed.
        Scene preview=EditorSceneManager.NewPreviewScene();
        GameObject cameraObject=new GameObject("V12 read-only capture camera"){hideFlags=HideFlags.HideAndDontSave};
        SceneManager.MoveGameObjectToScene(cameraObject,preview);
        RenderTexture target=RenderTexture.GetTemporary(pose.Width,pose.Height,24);
        RenderTexture previous=RenderTexture.active;
        Texture2D image=null;
        try
        {
            Camera camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.CopyFrom(source);camera.scene=source.gameObject.scene;
            camera.transform.SetPositionAndRotation(pose.Position,pose.Rotation);
            camera.fieldOfView=pose.Fov;camera.nearClipPlane=pose.Near;camera.farClipPlane=pose.Far;
            camera.aspect=pose.Aspect;camera.cullingMask=pose.Mask;camera.clearFlags=pose.Clear;
            camera.backgroundColor=pose.Background;camera.allowHDR=pose.Hdr;
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image=new Texture2D(pose.Width,pose.Height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,pose.Width,pose.Height),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(DirectoryPath,runtime?"Play.png":"Edit.png"),image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;
            if(image)DestroyImmediate(image);
            DestroyImmediate(cameraObject);RenderTexture.ReleaseTemporary(target);
            EditorSceneManager.ClosePreviewScene(preview);
        }
        LoadImages();
    }
}
