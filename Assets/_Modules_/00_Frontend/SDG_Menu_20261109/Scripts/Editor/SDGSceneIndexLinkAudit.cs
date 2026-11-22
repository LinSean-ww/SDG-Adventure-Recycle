#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SDGAdventure.FrontEnd;

namespace SDGAdventure.FrontEnd.Editor
{
    public static class SDGSceneIndexLinkAudit
    {
        private const string MenuScene = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scenes/SDG_StartAndMainMenu.unity";

        [MenuItem("SDG Adventure/前端 UI/15 按 Build Index 導通四關（核對後套用）")]
        public static void Repair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Scene Link", "Stop Play mode and wait for compilation.", "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene) == null)
            {
                EditorUtility.DisplayDialog("Scene Link", "Front-end scene was not found:\n" + MenuScene, "OK");
                return;
            }

            Scene front = SceneManager.GetSceneByPath(MenuScene);
            bool opened = !front.IsValid() || !front.isLoaded;
            Scene prior = SceneManager.GetActiveScene();
            try
            {
                if (opened) front = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Additive);
                SDGFrontEndController controller = null;
                foreach (GameObject root in front.GetRootGameObjects())
                {
                    controller = root.GetComponent<SDGFrontEndController>();
                    if (controller != null) break;
                }
                if (controller == null) throw new InvalidOperationException("SDGFrontEndController missing from front-end scene.");
                if (controller.levelScenePaths == null || controller.levelScenePaths.Length != 4)
                    throw new InvalidOperationException("Four saved level paths are missing. Use scene selection menu 14 first.");

                var targets = new string[4];
                var confirmation = new System.Text.StringBuilder();
                confirmation.AppendLine("These four saved level scenes will be enabled in Build Settings.");
                confirmation.AppendLine("Verify they are REAL gameplay scenes. No level contents will be edited.\n");
                for (int i = 0; i < 4; i++)
                {
                    string raw = (controller.levelScenePaths[i] ?? "").Trim().Replace('\\', '/');
                    if (string.IsNullOrEmpty(raw)) throw new InvalidOperationException("Level0" + (i+1) + " has no assigned scene. Use menu 14.");
                    string sceneAsset = raw.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ? raw : raw + ".unity";
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneAsset) == null)
                        throw new InvalidOperationException("Level0" + (i+1) + " scene file missing: " + sceneAsset);
                    targets[i] = sceneAsset;
                    confirmation.AppendLine("Level0" + (i + 1) + ": " + sceneAsset);
                }
                confirmation.AppendLine("\nFront-end will be build index 0. Other Build Settings entries will be preserved.");
                if (!EditorUtility.DisplayDialog("Verify scene targets", confirmation.ToString(), "Apply", "Cancel")) return;

                string projectDir = Directory.GetParent(Application.dataPath).FullName;
                string backups = Path.Combine(projectDir, "SDG_FrontEnd_Backups");
                Directory.CreateDirectory(backups);
                string buildSettingsFile = Path.Combine(projectDir, "ProjectSettings", "EditorBuildSettings.asset");
                if (File.Exists(buildSettingsFile))
                    File.Copy(buildSettingsFile, Path.Combine(backups, "SceneIndex_EditorBuildSettings_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak"), false);

                List<EditorBuildSettingsScene> all = EditorBuildSettings.scenes.ToList();
                string[] desired = (new[] { MenuScene }).Concat(targets).ToArray();
                foreach (string path in desired)
                {
                    int at = all.FindIndex(s => string.Equals(s.path, path, StringComparison.OrdinalIgnoreCase));
                    if (at < 0) all.Add(new EditorBuildSettingsScene(path, true));
                    else if (!all[at].enabled) all[at] = new EditorBuildSettingsScene(path, true);
                }
                int first = all.FindIndex(s => string.Equals(s.path, MenuScene, StringComparison.OrdinalIgnoreCase));
                EditorBuildSettingsScene entry = all[first];
                all.RemoveAt(first);
                all.Insert(0, entry);
                EditorBuildSettings.scenes = all.ToArray();
                AssetDatabase.SaveAssets();

                var report = new System.Text.StringBuilder();
                for (int i = 0; i < 4; i++)
                {
                    int index = SceneUtility.GetBuildIndexByScenePath(targets[i]);
                    bool enabled = EditorBuildSettings.scenes.Any(s => s.enabled && string.Equals(s.path, targets[i], StringComparison.OrdinalIgnoreCase));
                    report.AppendLine("Level0" + (i + 1) + ": " + (index >= 0 && enabled ? "BUILD READY" : "NOT READY") +
                        " | index=" + index + " | " + targets[i]);
                }
                Debug.Log("[SDG Scene Index] Build Settings audit:\n" + report);
                EditorUtility.DisplayDialog("Scene Index Audit", report + "\nRestart Play mode and test Level01.\nIf level loads but gameplay breaks, inspect the level scene Console errors.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError("[SDG Scene Index] Failed: " + e);
                EditorUtility.DisplayDialog("Scene Index Link Error", e.Message, "OK");
            }
            finally
            {
                if (opened && front.IsValid() && front.isLoaded) EditorSceneManager.CloseScene(front, true);
                if (prior.IsValid() && prior.isLoaded) SceneManager.SetActiveScene(prior);
            }
        }
    }
}
#endif
