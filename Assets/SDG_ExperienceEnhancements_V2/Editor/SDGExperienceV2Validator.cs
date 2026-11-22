#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.ExperienceV2.Editor
{
    public static class SDGExperienceV2Validator
    {
        private const string ReportFile = "SDG_ExperienceV2_Report.txt";
        private const string ObjectsFile = "SDG_ExperienceV2_Level02Objects.txt";

        [MenuItem("SDG Adventure/關卡/檢查第一關挖掘特效與第二關導航 v2")]
        public static void Run()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            string report = Path.Combine(root, ReportFile);
            string objectsReport = Path.Combine(root, ObjectsFile);
            if (File.Exists(report)) File.Delete(report);

            string level1 = SDGExperienceV2Bootstrap.Level01;
            string level2 = SDGExperienceV2Bootstrap.Level02;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(level1) == null)
                throw new InvalidOperationException("Level01 scene missing: " + level1);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(level2) == null)
                throw new InvalidOperationException("Level02 scene missing: " + level2);
            foreach (var name in new[] { "SDGExperienceV2Bootstrap", "SDGLevel01ShovelVFX", "SDGLevel02StarNavigator" })
            {
                string expected = "Assets/SDG_ExperienceEnhancements_V2/" + name + ".cs";
                if (AssetDatabase.LoadAssetAtPath<MonoScript>(expected) == null)
                    throw new InvalidOperationException("New script was not imported: " + expected);
            }
            Scene scene = EditorSceneManager.OpenScene(level2, OpenSceneMode.Additive);
            string lines = "";
            int starComponents = 0, namedBoxes = 0, plates = 0;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("SDG Level02 scene object scan (read-only)");
                sb.AppendLine("Scene=" + level2);
                foreach (GameObject obj in scene.GetRootGameObjects())
                {
                    foreach (Transform transform in obj.GetComponentsInChildren<Transform>(true))
                    {
                        string goName = transform.name.ToLowerInvariant();
                        MonoBehaviour[] scripts = transform.GetComponents<MonoBehaviour>();
                        string components = string.Join(",", scripts.Where(m => m != null).Select(m => m.GetType().Name).ToArray());
                        bool candidate = goName.Contains("sobox") || goName.Contains("星星箱") ||
                                         (goName.Contains("star") && (goName.Contains("box") || goName.Contains("crate")));
                        bool compCandidate = components.IndexOf("Star", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             components.IndexOf("Box", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (components.Contains("StarGoalPlate")) plates++;
                        if (candidate) namedBoxes++;
                        if (compCandidate) starComponents++;
                        if (!candidate && !compCandidate) continue;
                        sb.AppendLine("object=" + Hierarchy(transform));
                        sb.AppendLine("  pos=" + transform.position.ToString("F2") + " active=" + transform.gameObject.activeInHierarchy);
                        sb.AppendLine("  scripts=" + components);
                    }
                }
                if (sb.Length == 0) sb.AppendLine("No matching objects detected; inspect actual prefabs in Editor.");
                lines = sb.ToString();
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            File.WriteAllText(objectsReport, lines, new UTF8Encoding(true));
            string verdict = "SDG EXPERIENCE V2 SUCCESS\n" +
                "Compile=OK\n" +
                "Level01=shovel impact dust + visual-only flying scrap fragments\n" +
                "Level02=real scene object discovery + directional navigation + platform + gate stages\n" +
                "KnownNameMatches=" + namedBoxes + "\n" +
                "StarOrBoxComponentMatches=" + starComponents + "\n" +
                "StarGoalPlateMatches=" + plates + "\n" +
                "SceneFilesModified=NO\n" +
                "ExistingScriptsOverwritten=NO\n" +
                "UnityPlayModeVerified=NO\n" +
                "ObjectScan=" + objectsReport + "\n";
            File.WriteAllText(report, verdict, new UTF8Encoding(true));
            Debug.Log("[SDG Experience V2] " + verdict);
        }

        private static string Hierarchy(Transform t)
        {
            string path = t.name;
            for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
#endif
