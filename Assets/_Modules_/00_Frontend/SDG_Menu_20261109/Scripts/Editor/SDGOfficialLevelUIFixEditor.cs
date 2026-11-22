#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SDGAdventure.FrontEnd;

namespace SDGAdventure.FrontEnd.Editor
{
    /// <summary>Non-destructive preview plus opt-in fix for four verified Build Settings level scenes.</summary>
    public static class SDGOfficialLevelUIFixEditor
    {
        private const string FolderToken = "/_Modules_/02_Levels/Level0";
        private const string GuardName = "SDG Official Level UI Startup Guard";

        [MenuItem("SDG Adventure/前端 UI/16 檢查四關開發及暫停 UI（唯讀）")]
        public static void Audit()
        {
            string[] paths;
            string error;
            if (!TryGetLevelScenes(out paths, out error)) { EditorUtility.DisplayDialog("SDG Official UI - Audit", error, "OK"); return; }
            if (!CanOperate()) return;
            try
            {
                string report = Scan(paths);
                Debug.Log("[SDG Official UI] Audit\n" + report);
                EditorUtility.DisplayDialog("SDG Official UI - Audit", report, "OK");
            }
            catch (Exception ex) { Debug.LogError(ex); EditorUtility.DisplayDialog("SDG Official UI", ex.Message, "OK"); }
        }

        [MenuItem("SDG Adventure/前端 UI/17 修正四關初始選單（備份後套用）")]
        public static void Repair()
        {
            string[] paths;
            string error;
            if (!TryGetLevelScenes(out paths, out error)) { EditorUtility.DisplayDialog("SDG Official UI", error, "OK"); return; }
            if (!CanOperate()) return;
            string report;
            try { report = Scan(paths); }
            catch (Exception ex) { Debug.LogError(ex); EditorUtility.DisplayDialog("SDG Official UI", ex.Message, "OK"); return; }
            string prompt = report + "\n\nThis will: (1) back up all four selected .unity scenes; " +
                "(2) disable confidently identified development and pause panels; " +
                "(3) add a startup guard to those four scenes only. " +
                "It does not remove controls, prefab assets, game logic, or change Build Settings.\n\nApply?";
            if (!EditorUtility.DisplayDialog("SDG Official UI - Confirm", prompt, "Back up and apply", "Cancel")) return;
            string backupFolder = Backup(paths);
            Scene previous = SceneManager.GetActiveScene();
            int changed = 0;
            int installed = 0;
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    bool openedHere;
                    Scene scene = Open(paths[i], out openedHere);
                    try
                    {
                        GameObject dev = SDGOfficialLevelUIStartupGuard.FindDeveloperOverlay(scene);
                        GameObject pause = SDGOfficialLevelUIStartupGuard.FindPauseOverlay(scene);
                        if (dev != null && dev.activeSelf) { dev.SetActive(false); changed++; }
                        if (pause != null && pause.activeSelf) { pause.SetActive(false); changed++; }
                        if (FindGuard(scene) == null)
                        {
                            SceneManager.SetActiveScene(scene);
                            GameObject go = new GameObject(GuardName);
                            go.AddComponent<SDGOfficialLevelUIStartupGuard>();
                            installed++;
                        }
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Failed to save " + paths[i]);
                    }
                    finally { if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
                }
                string result = "Completed. Backups: " + backupFolder + "\n" +
                    "Panels set inactive: " + changed + "\nStartup guards installed: " + installed +
                    "\n\nTest: Play from main menu > each Level. ESC should still open the existing pause UI. " +
                    "If a development screen still appears, it may be a separate scene or runtime-generated UI; inspect the log.";
                Debug.Log("[SDG Official UI] " + result);
                EditorUtility.DisplayDialog("SDG Official UI", result, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG Official UI] Repair failed. Original scene copies are in " + backupFolder + "\n" + ex);
                EditorUtility.DisplayDialog("SDG Official UI - Error", ex.Message + "\nBackup: " + backupFolder, "OK");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static bool CanOperate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog("SDG Official UI", "Stop Play Mode and wait for compilation.", "OK");
                return false;
            }
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        private static Scene Open(string path, out bool openedHere)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            openedHere = !(scene.IsValid() && scene.isLoaded);
            return openedHere ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : scene;
        }

        private static SDGOfficialLevelUIStartupGuard FindGuard(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                SDGOfficialLevelUIStartupGuard guard = root.GetComponent<SDGOfficialLevelUIStartupGuard>();
                if (guard != null) return guard;
            }
            return null;
        }

        private static string Scan(string[] paths)
        {
            StringBuilder sb = new StringBuilder();
            Scene previous = SceneManager.GetActiveScene();
            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    bool openedHere;
                    Scene scene = Open(paths[i], out openedHere);
                    try
                    {
                        GameObject dev = SDGOfficialLevelUIStartupGuard.FindDeveloperOverlay(scene);
                        GameObject pause = SDGOfficialLevelUIStartupGuard.FindPauseOverlay(scene);
                        sb.Append("Level0").Append(i + 1).Append(": ").Append(Path.GetFileName(paths[i])).AppendLine();
                        sb.Append("  Developer: ").AppendLine(dev != null ? SDGOfficialLevelUIStartupGuard.PathOf(dev.transform) : "not detected in saved scene");
                        sb.Append("  Pause: ").AppendLine(pause != null ? SDGOfficialLevelUIStartupGuard.PathOf(pause.transform) : "not detected in saved scene");
                        sb.Append("  Guard: ").AppendLine(FindGuard(scene) != null ? "installed" : "not installed");
                    }
                    finally { if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
                }
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
            return sb.ToString();
        }

        private static bool TryGetLevelScenes(out string[] paths, out string error)
        {
            paths = new string[4];
            error = null;
            List<string>[] candidates = new List<string>[4];
            for (int i = 0; i < 4; i++) candidates[i] = new List<string>();
            foreach (EditorBuildSettingsScene item in EditorBuildSettings.scenes)
            {
                if (!item.enabled || string.IsNullOrEmpty(item.path)) continue;
                string normalized = item.path.Replace('\\', '/');
                for (int i = 0; i < 4; i++)
                {
                    if (normalized.IndexOf(FolderToken + (i + 1).ToString() + "/", StringComparison.OrdinalIgnoreCase) >= 0)
                        candidates[i].Add(normalized);
                }
            }
            for (int i = 0; i < 4; i++)
            {
                if (candidates[i].Count != 1)
                {
                    error = "Level0" + (i + 1) + " has " + candidates[i].Count +
                        " enabled scenes in Build Settings; expected exactly one. " +
                        "Choose the official gameplay scene first (File > Build Settings). No files were modified.";
                    return false;
                }
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(candidates[i][0]) == null)
                {
                    error = "Scene asset missing: " + candidates[i][0];
                    return false;
                }
                paths[i] = candidates[i][0];
            }
            return true;
        }

        private static string Backup(string[] paths)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string folder = Path.Combine(root, "SDG_FrontEnd_Backups", "Official_UI_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(folder);
            for (int i = 0; i < paths.Length; i++)
                File.Copy(Path.Combine(root, paths[i]), Path.Combine(folder, "Level0" + (i + 1) + "_" + Path.GetFileName(paths[i])), false);
            return folder;
        }
    }
}
#endif
