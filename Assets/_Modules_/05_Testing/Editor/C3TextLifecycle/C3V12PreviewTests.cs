using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class C3V12PreviewTests
{
    [Test] public void RepeatedEditCaptureDoesNotBakeObjectsDirtyTheSceneOrLeaveACamera()
    {
        const string scenePath="Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        const string directory="UserSettings/C3V12FactoryPreview";
        // Never discard an unsaved user scene; Test Runner should run in a clean test session.
        var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Assert.IsFalse(active.isDirty,"Save your current scene before running preview tests.");
        var previous=EditorSceneManager.GetSceneManagerSetup();
        string[] files={"Edit.png","Play.png","Camera.json"};
        byte[][] backups=files.Select(f=>File.Exists(Path.Combine(directory,f))?File.ReadAllBytes(Path.Combine(directory,f)):null).ToArray();
        EditorWindow window=null;UnityEngine.Object selected=Selection.activeObject;
        try
        {
            var scene=EditorSceneManager.OpenScene(scenePath);
            Camera camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).Single();
            Selection.activeGameObject=camera.gameObject;
            int objects=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Length);
            Type type=Type.GetType("C3V12FactoryPreview, Assembly-CSharp-Editor",true);
            window=ScriptableObject.CreateInstance(type) as EditorWindow;
            MethodInfo capture=type.GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Instance);
            Assert.IsNotNull(capture);
            for(int i=0;i<3;i++)
            {
                capture.Invoke(window,new object[]{false});
                Assert.IsFalse(scene.isDirty,"Preview modified the authored scene");
                Assert.AreEqual(objects,scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Length));
                Assert.IsFalse(Resources.FindObjectsOfTypeAll<Camera>().Any(c=>c.name=="V12 read-only capture camera"));
            }
            Assert.Greater(new FileInfo(Path.Combine(directory,"Edit.png")).Length,100);
            Assert.IsTrue(File.Exists(Path.Combine(directory,"Camera.json")));
        }
        finally
        {
            if(window)UnityEngine.Object.DestroyImmediate(window);
            for(int i=0;i<files.Length;i++)
            {
                string path=Path.Combine(directory,files[i]);
                if(backups[i]!=null)File.WriteAllBytes(path,backups[i]);else if(File.Exists(path))File.Delete(path);
            }
            EditorSceneManager.RestoreSceneManagerSetup(previous);Selection.activeObject=selected;
        }
    }
}
