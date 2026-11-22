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
    // This editor-only repair does not change any Level scene contents or UI button events.
    public sealed class SDGFrontEndSceneBuildRepair : EditorWindow
    {
        private const string FrontScene = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scenes/SDG_StartAndMainMenu.unity";
        private const string LevelsRoot = "Assets/_Modules_/02_Levels";
        private readonly List<string>[] candidates = new List<string>[4];
        private readonly int[] selected = new int[4];
        private readonly string[] existingPaths = new string[4];
        private Vector2 scroll;
        private string scanProblem = "";

        [MenuItem("SDG Adventure/前端 UI/14 修復四關 Build Settings（核對後套用）")]
        private static void ShowWindow()
        {
            SDGFrontEndSceneBuildRepair window = GetWindow<SDGFrontEndSceneBuildRepair>(false, "SDG Level Build Fix", true);
            window.minSize = new Vector2(820, 490);
            window.Scan();
            window.Show();
        }

        private void OnEnable() { Scan(); }

        private void Scan()
        {
            scanProblem = "";
            for (int i = 0; i < 4; i++)
            {
                candidates[i] = new List<string>();
                selected[i] = 0;
                existingPaths[i] = "";
            }
            if (!AssetDatabase.IsValidFolder(LevelsRoot))
            {
                scanProblem = "Level root not found: " + LevelsRoot;
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FrontScene) == null)
            {
                scanProblem = "Front-end scene missing: " + FrontScene;
                return;
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { LevelsRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                for (int i = 0; i < 4; i++)
                {
                    string segment = "/Level" + (i + 1).ToString("00") + "/";
                    if (path.IndexOf(segment, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) &&
                        AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                        candidates[i].Add(path);
                }
            }
            for (int i = 0; i < 4; i++)
                candidates[i].Sort(StringComparer.OrdinalIgnoreCase);
            TryReadExistingPaths();
            for (int i = 0; i < 4; i++)
            {
                string exact = existingPaths[i] + ".unity";
                int match = candidates[i].FindIndex(p => string.Equals(p, exact, StringComparison.OrdinalIgnoreCase));
                if (match >= 0) selected[i] = match + 1;
                else if (candidates[i].Count == 1) selected[i] = 1;
            }
            Repaint();
        }

        private void TryReadExistingPaths()
        {
            Scene scene = SceneManager.GetSceneByPath(FrontScene);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            Scene before = SceneManager.GetActiveScene();
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(FrontScene, OpenSceneMode.Additive);
                SDGFrontEndController ctrl = FindController(scene);
                if (ctrl == null) { scanProblem = "No SDGFrontEndController in front-end scene."; return; }
                for (int i = 0; i < 4; i++)
                    existingPaths[i] = ctrl.levelScenePaths != null && i < ctrl.levelScenePaths.Length
                        ? (ctrl.levelScenePaths[i] ?? "") : "";
            }
            catch (Exception ex) { scanProblem = ex.Message; }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (before.IsValid() && before.isLoaded) SceneManager.SetActiveScene(before);
            }
        }

        private static SDGFrontEndController FindController(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                SDGFrontEndController ctrl = root.GetComponent<SDGFrontEndController>();
                if (ctrl != null) return ctrl;
            }
            return null;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Front-end -> Level01 ~ Level04: Build Settings repair", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Stop Play Mode. Select the REAL main scene for each level (NOT a test scene). This updates front-end level paths and Build Settings only; no level scene is edited.", MessageType.Info);
            if (!string.IsNullOrEmpty(scanProblem)) EditorGUILayout.HelpBox(scanProblem, MessageType.Error);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Refresh scene list", GUILayout.Height(25))) Scan();
                scroll = EditorGUILayout.BeginScrollView(scroll);
                for (int i = 0; i < 4; i++)
                {
                    EditorGUILayout.Space(9);
                    EditorGUILayout.LabelField("Level " + (i + 1).ToString("00"), EditorStyles.boldLabel);
                    string old = existingPaths[i];
                    EditorGUILayout.LabelField("Currently linked:", string.IsNullOrEmpty(old) ? "(none)" : old, EditorStyles.wordWrappedMiniLabel);
                    List<string> list = candidates[i] ?? new List<string>();
                    string[] options = (new[] { "-- Choose the real gameplay scene --" }).Concat(list).ToArray();
                    selected[i] = Mathf.Clamp(selected[i], 0, options.Length - 1);
                    selected[i] = EditorGUILayout.Popup("Scene asset", selected[i], options);
                    if (list.Count == 0) EditorGUILayout.HelpBox("No Level" + (i + 1).ToString("00") + " scene found. Nothing can be applied.", MessageType.Error);
                    else if (selected[i] == 0) EditorGUILayout.HelpBox("Select the real scene to enable Apply.", MessageType.Warning);
                    else
                    {
                        string chosen = list[selected[i] - 1];
                        bool ready = EditorBuildSettings.scenes.Any(s => s.enabled && string.Equals(s.path, chosen, StringComparison.OrdinalIgnoreCase));
                        EditorGUILayout.LabelField("Build Settings:", ready ? "ENABLED" : "MISSING / DISABLED");
                        if (string.Equals(Path.GetFileNameWithoutExtension(chosen), "WTF", StringComparison.OrdinalIgnoreCase))
                            EditorGUILayout.HelpBox("This scene is named WTF. Verify it is the actual Level gameplay scene, not a development/test scene, before applying.", MessageType.Warning);
                    }
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(8);
                bool valid = string.IsNullOrEmpty(scanProblem) &&
                    Enumerable.Range(0, 4).All(i => candidates[i] != null && selected[i] > 0 && selected[i] <= candidates[i].Count);
                using (new EditorGUI.DisabledScope(!valid))
                {
                    if (GUILayout.Button("Review choices and apply to front-end + Build Settings", GUILayout.Height(36))) Apply();
                }
            }
        }

        private void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            string[] levels = new string[4];
            for (int i = 0; i < 4; i++)
            {
                if (candidates[i] == null || selected[i] <= 0 || selected[i] > candidates[i].Count) return;
                levels[i] = candidates[i][selected[i] - 1];
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(levels[i]) == null)
                    throw new InvalidOperationException("Scene asset missing: " + levels[i]);
            }
            string message = "Register these exact scenes in Build Settings and link them from the front-end?\n\n" +
                string.Join("\n", Enumerable.Range(0, 4).Select(i => "Level0" + (i + 1) + ": " + levels[i]).ToArray()) +
                "\n\nThe front-end scene will become build index 0. Existing other entries remain. A backup will be created.";
            if (!EditorUtility.DisplayDialog("Verify real gameplay scenes", message, "Apply", "Cancel")) return;
            Scene scene = SceneManager.GetSceneByPath(FrontScene);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            Scene before = SceneManager.GetActiveScene();
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(FrontScene, OpenSceneMode.Additive);
                else if (scene.isDirty)
                {
                    EditorUtility.DisplayDialog("Save scene first", "Save your open front-end scene (Ctrl+S), then run Apply again.", "OK");
                    return;
                }
                SDGFrontEndController ctrl = FindController(scene);
                if (ctrl == null) throw new InvalidOperationException("Front-end controller missing; no changes applied.");
                Backup();
                if (ctrl.levelScenePaths == null || ctrl.levelScenePaths.Length != 4) ctrl.levelScenePaths = new string[4];
                for (int i = 0; i < 4; i++)
                    ctrl.levelScenePaths[i] = levels[i].Substring(0, levels[i].Length - ".unity".Length);
                EditorUtility.SetDirty(ctrl);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save front-end scene.");

                List<EditorBuildSettingsScene> build = EditorBuildSettings.scenes.ToList();
                string[] wanted = (new[] { FrontScene }).Concat(levels).ToArray();
                foreach (string path in wanted)
                {
                    int index = build.FindIndex(s => string.Equals(s.path, path, StringComparison.OrdinalIgnoreCase));
                    if (index == -1) build.Add(new EditorBuildSettingsScene(path, true));
                    else if (!build[index].enabled) build[index] = new EditorBuildSettingsScene(path, true);
                }
                int frontIndex = build.FindIndex(s => string.Equals(s.path, FrontScene, StringComparison.OrdinalIgnoreCase));
                if (frontIndex > 0)
                {
                    EditorBuildSettingsScene first = build[frontIndex];
                    build.RemoveAt(frontIndex);
                    build.Insert(0, first);
                }
                EditorBuildSettings.scenes = build.ToArray();
                AssetDatabase.SaveAssets();
                string audit = "";
                for (int i = 0; i < 4; i++)
                    audit += "Level0" + (i + 1) + ": " +
                        (EditorBuildSettings.scenes.Any(s => s.enabled && s.path == levels[i]) ? "BUILD READY" : "ERROR") + "\n";
                Debug.Log("[SDG FrontEnd] Scene Build Settings fixed:\n" + audit);
                EditorUtility.DisplayDialog("SDG FrontEnd: Build Settings", audit + "\nStop/restart Play Mode to test Start and Level Selection.\nContinue Game still requires a real save bridge.", "OK");
                Scan();
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG FrontEnd] Scene Build Settings repair failed: " + ex);
                EditorUtility.DisplayDialog("Scene Build Settings error", ex.Message, "OK");
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (before.IsValid() && before.isLoaded) SceneManager.SetActiveScene(before);
            }
        }

        private static void Backup()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string folder = Path.Combine(project, "SDG_FrontEnd_Backups");
            Directory.CreateDirectory(folder);
            string prefix = "BuildSceneRepair_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string menuPath = Path.Combine(project, FrontScene.Replace('/', Path.DirectorySeparatorChar));
            File.Copy(menuPath, Path.Combine(folder, prefix + "_FrontEnd.unity.bak"));
            string settings = Path.Combine(project, "ProjectSettings", "EditorBuildSettings.asset");
            if (File.Exists(settings)) File.Copy(settings, Path.Combine(folder, prefix + "_EditorBuildSettings.asset.bak"));
        }
    }
}
#endif
