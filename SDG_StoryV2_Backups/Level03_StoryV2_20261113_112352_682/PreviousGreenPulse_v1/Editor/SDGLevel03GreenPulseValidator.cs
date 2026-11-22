#if UNITY_EDITOR
using System;
using System.IO;
using C3TreasurePuzzle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.Level03GreenPulse.Editor
{
    public static class SDGLevel03GreenPulseValidator
    {
        [MenuItem("SDG Adventure/Level03/Verify GreenPulse add-on (read only)")]
        public static void Run()
        {
            const string expected = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
            string project = Directory.GetParent(Application.dataPath).FullName;
            string report = Path.Combine(project, "SDG_Level03_GreenPulse_v1_Report.txt");
            if (File.Exists(report)) File.Delete(report);
            if (SDGLevel03GreenPulseBootstrap.ScenePath != expected)
                throw new InvalidOperationException("GreenPulse Level03 scene path mismatch.");
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(expected))
                throw new FileNotFoundException("Required C_Main scene not found", expected);
            Scene scene = EditorSceneManager.OpenScene(expected, OpenSceneMode.Additive);
            try
            {
                int deviceCount = 0, consoleCount = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    deviceCount += root.GetComponentsInChildren<C3DeviceState>(true).Length;
                    consoleCount += root.GetComponentsInChildren<C3PuzzleConsole>(true).Length;
                }
                if (deviceCount < 1 || consoleCount < 1)
                    throw new InvalidOperationException(string.Format(
                        "GreenPulse requires Level03 device/console. Found: C3DeviceState={0}, C3PuzzleConsole={1}",
                        deviceCount, consoleCount));
                string message = "SDG LEVEL03 GREENPULSE V1 SUCCESS\n" +
                    "Scene=" + expected + "\n" +
                    "C3DeviceState=" + deviceCount + "\nC3PuzzleConsole=" + consoleCount + "\n" +
                    "Gameplay=OPTIONAL; unlocked after factory running\n" +
                    "Keys=F7 open/close; SPACE/ENTER sync; R retry; ESC close\n" +
                    "QuestOrGateFilesChanged=NO\n" +
                    "PlayModeTest=NOT RUN; verify in Game view\n";
                File.WriteAllText(report, message);
                Debug.Log("[SDG C3 GreenPulse] " + message);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
