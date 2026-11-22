#if UNITY_EDITOR
using System;
using System.IO;
using C3TreasurePuzzle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.Level03StoryV2.Editor
{
    public static class SDGLevel03StoryV23Validator
    {
        [MenuItem("SDG Adventure/Level03/Validate Story V2.3 (read-only)")]
        public static void Validate()
        {
            string project=Directory.GetParent(Application.dataPath).FullName;
            string report=Path.Combine(project,"SDG_Level03_StoryV2_3_Validation.txt");
            if(File.Exists(report))File.Delete(report);
            if(SDGLevel03StoryV2Controller.PatchVersion!="V2.3")throw new InvalidOperationException("Controller version is not V2.3");
            if(typeof(SDGLevel03AssemblyInteractor)==null || typeof(SDGLevel03AssemblyPartMarker)==null ||
               typeof(SDGLevel03AssemblyPreview)==null || typeof(SDGLevel03TraceGraphic)==null ||
               typeof(SDGLevel03PracticeTarget)==null)throw new InvalidOperationException("V2.3 runtime helper missing");
            string controller=Path.Combine(Application.dataPath,"SDG_Level03_StoryV2/SDGLevel03StoryV2Controller.cs");
            string code=File.ReadAllText(controller);
            if(!code.Contains("DeployAllFloatingTargets()") || !code.Contains("OnReal3DPartSnapped") ||
               code.Contains("Input.GetKeyDown(KeyCode.F8)"))
                throw new InvalidOperationException("V2.3 new gameplay hooks are missing or old F8 activation remains");
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(SDGLevel03StoryV2Bridge.ScenePath))
                throw new FileNotFoundException("C_Main scene missing");
            Scene scene=EditorSceneManager.OpenScene(SDGLevel03StoryV2Bridge.ScenePath,OpenSceneMode.Additive);
            try
            {
                int devices=0,consoles=0;
                foreach(GameObject root in scene.GetRootGameObjects())
                {
                    devices+=root.GetComponentsInChildren<C3DeviceState>(true).Length;
                    consoles+=root.GetComponentsInChildren<C3PuzzleConsole>(true).Length;
                }
                if(devices<1 || consoles<1)
                    throw new InvalidOperationException("Missing Level03 device or puzzle console");
                string content="SDG LEVEL03 STORY V2.3 STATIC SUCCESS\n"+
                    "Version=V2.3\nScene="+SDGLevel03StoryV2Bridge.ScenePath+"\n"+
                    "C3DeviceState="+devices+" C3PuzzleConsole="+consoles+"\n"+
                    "CurvedPath=joined spline ribbon, animated conducting pulse and hazard borders\n"+
                    "Assembly=drag ACTUAL 3D mesh parts by mouse onto glowing 3D socket\n"+
                    "Range=auto deploy all THREE real raycast airborne targets; no required F8\n"+
                    "LIMIT=Compilation and scene inspection only. User must verify 3D dragging, visibility, shots, exit in PlayMode.\n";
                File.WriteAllText(report,content);
                Debug.Log(content);
            }
            finally{if(scene.IsValid() && scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
#endif
