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
    public static class SDGLevel03StoryV2Validator
    {
        [MenuItem("SDG Adventure/Level03/Validate Story V2 (read only)")]
        public static void Validate()
        {
            string project=Directory.GetParent(Application.dataPath).FullName;
            string report=Path.Combine(project,"SDG_Level03_StoryV2_Validation.txt");
            if(File.Exists(report))File.Delete(report);
            const string scenePath=SDGLevel03StoryV2Bridge.ScenePath;
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath))
                throw new FileNotFoundException("Missing Level03 C_Main scene",scenePath);
            if(typeof(SDGLevel03StoryV2Controller)==null || typeof(SDGLevel03PracticeTarget)==null)
                throw new InvalidOperationException("Story V2 runtime not compiled");
            Scene scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
            try
            {
                int devices=0, consoles=0, parts=0, gates=0, benches=0;
                foreach(GameObject root in scene.GetRootGameObjects())
                {
                    devices+=root.GetComponentsInChildren<C3DeviceState>(true).Length;
                    consoles+=root.GetComponentsInChildren<C3PuzzleConsole>(true).Length;
                    parts+=root.GetComponentsInChildren<C3CollectiblePart>(true).Length;
                    foreach(MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                        if(script!=null && script.GetType().Name=="C3MissionLockedVerticalShutter") gates++;
                    foreach(EcoWorkshopInteractable b in root.GetComponentsInChildren<EcoWorkshopInteractable>(true))
                        if(b && !b.IsMaterialCache) benches++;
                }
                // The original EcoFactoryWorkshop creates its workbenches dynamically in Awake.
                // Do not reject a valid scene because the static .unity contains no workbench.
                if(devices<1 || consoles<1)
                    throw new InvalidOperationException(string.Format(
                        "Level03 scene missing device or console: device={0},console={1}",devices,consoles));
                string result="SDG LEVEL03 STORY V2 STATIC SUCCESS\n"+
                    "Scene="+scenePath+"\n"+
                    "Device="+devices+" Console="+consoles+" Parts="+parts+" MissionGates="+gates+" Workbenches="+benches+"\n"+
                    "Flow=Dark intro > original repair and puzzle > auto voltage calibration > mouse cable trace > 100-unit load allocation > existing recycling line > intercepted real weapon craft > real weapon 3-target test > exit gate\n"+
                    "Warning=Static inspection only, not proof of PlayMode or FPS.\n";
                File.WriteAllText(report,result);
                Debug.Log("[SDG Level03 Story V2] "+result);
            }
            finally
            { if(scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true); }
        }
    }
}
#endif
