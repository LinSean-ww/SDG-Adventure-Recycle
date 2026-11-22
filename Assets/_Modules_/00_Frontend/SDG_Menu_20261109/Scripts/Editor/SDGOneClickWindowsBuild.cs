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
    /// Dedicated batch build entry point. Keeps scenes and ProjectSettings unchanged.
    /// Run only through an offline staging build; never writes directly over a released EXE.
    /// </summary>
    public static class SDGOneClickWindowsBuild
    {
        private const string MenuScene =
            "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scenes/SDG_StartAndMainMenu.unity";
        private const string ExeName = "SDG_Adventure_Recycle.exe";
        private const string SuccessMarker = "SDG_EXE_BUILD_SUCCESS";

        public static void Run()
        {
            try
            {
                string stage = Environment.GetEnvironmentVariable("SDG_EXE_STAGE_DIR");
                if (string.IsNullOrEmpty(stage) || !Path.IsPathRooted(stage))
                    throw new InvalidOperationException("SDG_EXE_STAGE_DIR must be an absolute path.");
                stage = Path.GetFullPath(stage);
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string allowedRoot = Path.Combine(projectRoot, "Builds") + Path.DirectorySeparatorChar;
                if (!stage.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Staging output must be inside PROJECT/Builds.");
                if (Directory.Exists(stage) && Directory.EnumerateFileSystemEntries(stage).Any())
                    throw new InvalidOperationException("Staging output is not empty: " + stage);
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                    throw new InvalidOperationException("Windows x86_64 Build Support is not installed.");
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                    throw new InvalidOperationException("Active target is not Windows x86_64; supply -buildTarget win64.");

                var sceneList = new List<string> { MenuScene };
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene) == null)
                    throw new FileNotFoundException("Menu scene missing: " + MenuScene);
                string[] enabled = EditorBuildSettings.scenes
                    .Where(x => x.enabled)
                    .Select(x => (x.path ?? "").Replace('\\','/'))
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToArray();
                for (int i = 1; i <= 4; i++)
                {
                    string token = "/Level0" + i + "/";
                    string[] matches = enabled
                        .Where(p => p.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    if (matches.Length != 1)
                        throw new InvalidOperationException("Expected exactly one enabled main scene for Level0" + i +
                            "; actual: " + matches.Length + ". Check File > Build Settings.");
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(matches[0]) == null)
                        throw new FileNotFoundException("Level scene missing: " + matches[0]);
                    sceneList.Add(matches[0]);
                }
                Directory.CreateDirectory(stage);
                string outputExe = Path.Combine(stage, ExeName);
                var options = new BuildPlayerOptions
                {
                    scenes = sceneList.ToArray(),
                    locationPathName = outputExe,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };
                Debug.Log("[SDG EXE UPDATE] Starting Windows x64 build: " + outputExe);
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report == null || report.summary.result != BuildResult.Succeeded ||
                    report.summary.totalErrors > 0 || !File.Exists(outputExe))
                    throw new InvalidOperationException("Build failed: " +
                        (report == null ? "no BuildReport" :
                         report.summary.result + ", errors " + report.summary.totalErrors));
                string dataFolder = Path.Combine(stage, "SDG_Adventure_Recycle_Data");
                if (!Directory.Exists(dataFolder))
                    throw new DirectoryNotFoundException("Build missing _Data directory: " + dataFolder);

                string version = PlayerSettings.bundleVersion;
                File.WriteAllText(Path.Combine(stage, "SDG_BUILD_VERSION.txt"), version, Encoding.UTF8);
                var info = new StringBuilder();
                info.AppendLine("SDG Adventure Recycle - Windows x64 compiled game");
                info.AppendLine("UTC: " + DateTime.UtcNow.ToString("u"));
                info.AppendLine("PlayerSettings.bundleVersion: " + version);
                info.AppendLine("Unity: " + Application.unityVersion);
                info.AppendLine("Build size bytes: " + report.summary.totalSize);
                info.AppendLine("Scenes:");
                for (int i = 0; i < sceneList.Count; i++)
                    info.AppendLine(i + ": " + sceneList[i]);
                info.AppendLine("Build contains the current local project's compiled state.");
                info.AppendLine("Remote automatic update requires a configured HTTPS release manifest.");
                File.WriteAllText(Path.Combine(stage, "BUILD_INFO.txt"), info.ToString(), Encoding.UTF8);
                Debug.Log(SuccessMarker + " version=" + version + " size=" + report.summary.totalSize);
            }
            catch (Exception ex)
            {
                Debug.LogError("SDG_EXE_BUILD_FATAL " + ex);
                EditorApplication.Exit(2);
            }
        }
    }
}
#endif
