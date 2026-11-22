#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SDGAdventure.FrontEnd.Editor
{
    /// <summary>
    /// Builds the currently linked front-end and four level scenes into a standalone Windows player.
    /// Read-only regarding scene assets and ProjectSettings: uses an explicit BuildPlayerOptions scene list.
    /// </summary>
    public static class SDGWindowsExeBuilder
    {
        private const string FrontEndScene =
            "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scenes/SDG_StartAndMainMenu.unity";
        private const string ExeName = "SDG_Adventure_Recycle.exe";

        [MenuItem("SDG Adventure/Windows EXE/01 Check build scenes", false, 1000)]
        private static void CheckScenes()
        {
            try
            {
                string[] scenes = CollectScenes();
                var sb = new StringBuilder();
                sb.AppendLine("The following scenes will be compiled into the Windows player:");
                for (int i = 0; i < scenes.Length; i++)
                    sb.AppendLine(i + ": " + scenes[i]);
                sb.AppendLine();
                sb.AppendLine("IMPORTANT: This check does not fix in-level QA or pause menus.");
                Debug.Log("[SDG Windows EXE] Scene check OK:\n" + sb);
                EditorUtility.DisplayDialog("SDG Windows EXE - Scene check", sb.ToString(), "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG Windows EXE] Scene check failed: " + ex);
                EditorUtility.DisplayDialog("SDG Windows EXE - Scene error", ex.Message, "OK");
            }
        }

        [MenuItem("SDG Adventure/Windows EXE/02 Build Windows EXE", false, 1001)]
        private static void BuildWindowsExe()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("SDG Windows EXE", "Stop Play mode and wait for compilation to finish.", "OK");
                return;
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                EditorUtility.DisplayDialog("SDG Windows EXE",
                    "Windows Build Support is missing. Install it via Unity Hub > Installs > Add modules.", "OK");
                return;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                EditorUtility.DisplayDialog("SDG Windows EXE",
                    "Switch the active platform to Windows x86_64 first in File > Build Settings, then run this command again.", "OK");
                return;
            }

            try
            {
                string[] scenes = CollectScenes();
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string outputDir = Path.Combine(projectRoot, "Builds", "Windows");
                string exePath = Path.Combine(outputDir, ExeName);
                var info = new StringBuilder();
                info.AppendLine("Build a Windows x64 game player with these scenes:");
                for (int i = 0; i < scenes.Length; ++i)
                    info.AppendLine(i + ": " + scenes[i]);
                info.AppendLine();
                info.AppendLine("Output: " + exePath);
                info.AppendLine();
                info.AppendLine("NOTE: Any existing development/QA menus inside the level scenes remain present in this build.");
                info.AppendLine("No level scene, controller, UI or ProjectSettings file will be changed.");
                if (!EditorUtility.DisplayDialog("Confirm Windows EXE build", info.ToString(), "Build", "Cancel"))
                    return;

                Directory.CreateDirectory(outputDir);
                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = exePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report == null || report.summary.result != BuildResult.Succeeded || !File.Exists(exePath))
                {
                    string detail = report == null ? "No build report was returned." :
                        "Result: " + report.summary.result + " | errors: " + report.summary.totalErrors;
                    Debug.LogError("[SDG Windows EXE] Build failed. " + detail);
                    EditorUtility.DisplayDialog("Windows EXE build failed",
                        detail + "\nCheck the Unity Console for the first red error.", "OK");
                    return;
                }

                var manifest = new StringBuilder();
                manifest.AppendLine("SDG Adventure: Recycle - Windows x64 build");
                manifest.AppendLine("Build UTC: " + DateTime.UtcNow.ToString("u"));
                manifest.AppendLine("Unity: " + Application.unityVersion);
                manifest.AppendLine("Scenes, in build order:");
                for (int i = 0; i < scenes.Length; i++) manifest.AppendLine(i + ": " + scenes[i]);
                manifest.AppendLine();
                manifest.AppendLine("To share the game, copy this entire Windows directory, not just the .exe.");
                manifest.AppendLine("This build does not remove development/QA menus from the level scenes.");
                File.WriteAllText(Path.Combine(outputDir, "BUILD_INFO.txt"), manifest.ToString(), Encoding.UTF8);
                Debug.Log("[SDG Windows EXE] SUCCESS: " + exePath +
                          " | size: " + report.summary.totalSize + " bytes" +
                          " | warnings: " + report.summary.totalWarnings);
                EditorUtility.DisplayDialog("Windows EXE build succeeded",
                    "Built: " + exePath +
                    "\n\nRun the .exe from the output directory. Keep its _Data folder and other player files together.", "OK");
                EditorUtility.RevealInFinder(exePath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG Windows EXE] Build exception: " + ex);
                EditorUtility.DisplayDialog("Windows EXE - error", ex.Message, "OK");
            }
        }

        private static string[] CollectScenes()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FrontEndScene) == null)
                throw new InvalidOperationException("Front-end scene is missing: " + FrontEndScene);

            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(x => x.enabled)
                .Select(x => (x.path ?? "").Replace('\\', '/'))
                .Where(x => !string.IsNullOrEmpty(x))
                .ToArray();

            var selected = new List<string> { FrontEndScene };
            for (int level = 1; level <= 4; ++level)
            {
                string token = "/Level0" + level + "/";
                string[] matches = enabledScenes
                    .Where(x => x.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (matches.Length == 0)
                    throw new InvalidOperationException("Level0" + level +
                        " has no enabled scene in Build Settings. Select its REAL main scene first.");
                if (matches.Length != 1)
                    throw new InvalidOperationException("Level0" + level +
                        " has multiple enabled scenes in Build Settings. Disable non-gameplay/test scenes first:\n" +
                        string.Join("\n", matches));
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(matches[0]) == null)
                    throw new InvalidOperationException("Missing Level0" + level + " scene asset: " + matches[0]);
                selected.Add(matches[0]);
            }
            return selected.ToArray();
        }
    }
}
#endif
