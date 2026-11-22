#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace SDGAdventure.Experience.Editor
{
    public static class SDGExperienceValidator
    {
        private const string ReportName = "SDG_ExperienceEnhancements_V1_Report.txt";
        public static void Run()
        {
            string report = Path.Combine(Path.GetDirectoryName(Application.dataPath), ReportName);
            if (File.Exists(report)) File.Delete(report);
            string[] scenes = {
                SDGExperienceBootstrap.Level01Path,
                SDGExperienceBootstrap.Level02Path,
                SDGExperienceBootstrap.Level04Path
            };
            foreach (string path in scenes)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    throw new InvalidOperationException("Missing expected level scene: " + path);
            if (typeof(BossHealth).GetField("maxHealth") == null || typeof(BossHealth).GetEvent("HealthChanged") == null)
                throw new InvalidOperationException("BossHealth API differs from the inspected project snapshot.");
            if (typeof(ToiletMechBossAI).GetField("attackCooldown") == null)
                throw new InvalidOperationException("Boss AI API differs from the inspected project snapshot.");
            // Read the actual Level04 scene additively without saving or modifying it.
            Scene bossScene = EditorSceneManager.OpenScene(SDGExperienceBootstrap.Level04Path, OpenSceneMode.Additive);
            try
            {
                int bossCount = bossScene.GetRootGameObjects()
                    .Sum(root => root.GetComponentsInChildren<BossHealth>(true).Length);
                if (bossCount != 1)
                    throw new InvalidOperationException("Expected one BossHealth in Level04 scene, found " + bossCount);
            }
            finally
            {
                if (bossScene.IsValid() && bossScene.isLoaded) EditorSceneManager.CloseScene(bossScene, true);
            }
            string[] paths = {
                "Assets/SDG_ExperienceEnhancements/SDGExperienceBootstrap.cs",
                "Assets/SDG_ExperienceEnhancements/SDGExperienceLevelRuntime.cs",
                "Assets/SDG_ExperienceEnhancements/SDGExperienceUI.cs",
                "Assets/SDG_ExperienceEnhancements/SDGLevel01HeldTools.cs",
                "Assets/SDG_ExperienceEnhancements/SDGLevel04BossOverlay.cs"
            };
            foreach (string path in paths)
                if (AssetDatabase.LoadAssetAtPath<MonoScript>(path) == null)
                    throw new InvalidOperationException("Script not imported: " + path);

            var result = "SDG EXPERIENCE ENHANCEMENTS V1 SUCCESS\n" +
                         "Compile=OK\n" +
                         "ScenePaths=" + string.Join(",",scenes) + "\n" +
                         "Level01=hand-held clamp/shovel visual models and tool action animations (original interactions unchanged)\n" +
                         "Level02=objective tutorial shown at runtime, F1 help\n" +
                         "Level04=Boss HP top overlay, minimum 30 HP at sceneLoaded before Start, 3 AI speed phases, recycling tutorial\n" +
                         "ScenesModified=NO\n" +
                         "UnityPlayModeVerified=NO\n";
            File.WriteAllText(report, result);
            Debug.Log("[SDG Experience] " + result);
        }
    }
}
#endif
