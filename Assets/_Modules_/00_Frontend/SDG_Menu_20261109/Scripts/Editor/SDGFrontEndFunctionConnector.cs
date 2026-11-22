#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SDGAdventure.FrontEnd;

namespace SDGAdventure.FrontEnd.Editor
{
    /// <summary>
    /// Non-destructive front-end wiring. Verifies existing handlers, resolves real level
    /// scene assets, and registers only verified scenes in Build Settings.
    /// Does not fabricate save data or edit Level01-Level04 scenes.
    /// </summary>
    public static class SDGFrontEndFunctionConnector
    {
        private const string MenuScene =
            "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scenes/SDG_StartAndMainMenu.unity";
        private const string LevelsRoot = "Assets/_Modules_/02_Levels";
        private static readonly string[] MainMethods = {
            "StartNewGame", "ContinueGame", "OpenLevelSelection",
            "OpenSettings", "OpenCredits", "QuitGame"
        };
        private static readonly string[] LevelMethods = {
            "LaunchLevel01", "LaunchLevel02", "LaunchLevel03", "LaunchLevel04"
        };

        [MenuItem("SDG Adventure/前端 UI/12 導通選單與四關功能（保留場景）")]
        public static void Connect()
        {
            if (!Ready()) return;
            Scene scene = default(Scene);
            Scene previous = SceneManager.GetActiveScene();
            bool openedHere = false;
            try
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene) == null)
                    throw new InvalidOperationException("Front-end scene not found: " + MenuScene);

                scene = SceneManager.GetSceneByPath(MenuScene);
                openedHere = !scene.IsValid() || !scene.isLoaded;
                if (!openedHere && scene.isDirty)
                {
                    EditorUtility.DisplayDialog("SDG FrontEnd", "Save the front-end scene (Ctrl+S) first.", "OK");
                    return;
                }
                if (openedHere)
                    scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Additive);

                Transform menu;
                SDGFrontEndController controller = GetControllerAndMenu(scene, out menu);
                Button[] main = GetMainButtons(menu);
                Button[] levels = new Button[4];
                Transform levelDialog = Require(menu, "Level selection dialog");
                for (int i = 0; i < 4; i++)
                    levels[i] = RequireButton(Require(levelDialog, "Level " + (i + 1)));

                Button[] backs = {
                    RequireButton(Require(menu, "Level selection dialog/Close levels")),
                    RequireButton(Require(menu, "Settings dialog/Close settings")),
                    RequireButton(Require(menu, "Credits dialog/Close credits"))
                };
                Slider volume = Require(menu, "Settings dialog/Volume slider").GetComponent<Slider>();
                Toggle fullscreen = Require(menu, "Settings dialog/Fullscreen toggle").GetComponent<Toggle>();
                if (volume == null || fullscreen == null)
                    throw new InvalidOperationException("Missing settings Slider or Toggle. Nothing changed.");

                string[] paths = new string[4];
                string[] notes = new string[4];
                int available = 0;
                for (int i = 0; i < 4; i++)
                {
                    string current = controller.levelScenePaths != null && i < controller.levelScenePaths.Length
                        ? controller.levelScenePaths[i] : null;
                    paths[i] = ResolveScenePath(i, current, out notes[i]);
                    if (!string.IsNullOrEmpty(paths[i])) available++;
                }
                if (available == 0)
                    throw new InvalidOperationException(
                        "No unique Level01-Level04 scene could be resolved. Existing project level scenes must be checked; no changes made.");

                string preview = "Front-end functions to verify/connect:\n" +
                    "Start, Continue, Level Selection, Settings, Credits, Quit, 3 Back buttons.\n" +
                    "Volume slider + Fullscreen toggle.\n\n";
                for (int i = 0; i < 4; ++i)
                    preview += "Level0" + (i + 1) + ": " + (paths[i] ?? "NOT FOUND") + "\n" + notes[i] + "\n";
                preview += "\nOnly verified scenes will be enabled in Build Settings.\n" +
                    "Continue is enabled ONLY when the real save bridge reports a save.\n" +
                    "No level scene content or existing button layout will be changed.\n\nApply?";
                if (!EditorUtility.DisplayDialog("SDG FrontEnd - Function Wiring", preview, "Apply", "Cancel")) return;

                string backup = BackupSceneAndBuildSettings();
                int added = 0;
                int verified = 0;
                int conflicts = 0;
                for (int i = 0; i < MainMethods.Length; i++)
                    WireButton(main[i], controller, MainMethods[i], ref added, ref verified, ref conflicts);
                for (int i = 0; i < LevelMethods.Length; i++)
                    WireButton(levels[i], controller, LevelMethods[i], ref added, ref verified, ref conflicts);
                foreach (Button back in backs)
                    WireButton(back, controller, "CloseDialogs", ref added, ref verified, ref conflicts);
                WireSlider(volume, controller, ref added, ref verified, ref conflicts);
                WireToggle(fullscreen, controller, ref added, ref verified, ref conflicts);

                if (controller.levelScenePaths == null || controller.levelScenePaths.Length != 4)
                    controller.levelScenePaths = new string[4];
                for (int i = 0; i < 4; i++)
                    if (!string.IsNullOrEmpty(paths[i])) controller.levelScenePaths[i] = paths[i];
                EditorUtility.SetDirty(controller);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Failed to save menu scene. Backup: " + backup);

                int registered = RegisterVerifiedScenes(paths);
                AssetDatabase.SaveAssets();
                string result = "Existing event links verified: " + verified + "\n" +
                    "Missing event links added: " + added + "\n" +
                    "Potential conflicting links (left untouched): " + conflicts + "\n" +
                    "Level scenes detected: " + available + "/4\n" +
                    "New/updated Build Settings entries: " + registered + "\n\n" +
                    "Backup: " + backup + "\n\n" +
                    "Continue/save restoration still requires integration with the project's actual save system.";
                Debug.Log("[SDG FrontEnd] Function wiring complete.\n" + result);
                EditorUtility.DisplayDialog("SDG FrontEnd - Result", result, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG FrontEnd] Function wiring aborted: " + ex);
                EditorUtility.DisplayDialog("SDG FrontEnd", "Function wiring stopped:\n" + ex.Message, "OK");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
        }

        [MenuItem("SDG Adventure/前端 UI/13 檢查功能導通狀態（唯讀）")]
        public static void Audit()
        {
            if (!Ready()) return;
            Scene scene = default(Scene);
            Scene previous = SceneManager.GetActiveScene();
            bool openedHere = false;
            try
            {
                scene = SceneManager.GetSceneByPath(MenuScene);
                openedHere = !scene.IsValid() || !scene.isLoaded;
                if (openedHere) scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Additive);
                Transform menu;
                SDGFrontEndController ctrl = GetControllerAndMenu(scene, out menu);
                Button[] buttons = GetMainButtons(menu);
                string summary = "Six buttons:\n";
                for (int i = 0; i < 6; i++)
                    summary += (i + 1) + ": " + MainMethods[i] + " = " +
                        (HasPersistent(buttons[i].onClick, ctrl, MainMethods[i]) ? "LINKED" : "MISSING") + "\n";
                summary += "\nLevel scenes:\n";
                for (int i = 0; i < 4; i++)
                {
                    string path = ctrl.levelScenePaths != null && ctrl.levelScenePaths.Length > i
                        ? ctrl.levelScenePaths[i] : null;
                    string asset = string.IsNullOrEmpty(path) ? null : path + ".unity";
                    bool valid = !string.IsNullOrEmpty(asset) &&
                                 AssetDatabase.LoadAssetAtPath<SceneAsset>(asset) != null;
                    bool included = valid && EditorBuildSettings.scenes.Any(s => s.path == asset && s.enabled);
                    summary += "Level0" + (i + 1) + ": " +
                        (valid ? (included ? "BUILD READY" : "NOT IN BUILD") : "UNRESOLVED") + "\n";
                }
                summary += "\nContinue: requires SDGFrontEndSaveBridge.ReportLatestSave()\n" +
                    "(and the game's actual checkpoint/load integration).\n" +
                    "Settings: master volume and fullscreen methods available.\n" +
                    "Credits: dialog is functional; actual team credits may still be placeholders.";
                Debug.Log("[SDG FrontEnd] Wiring audit:\n" + summary);
                EditorUtility.DisplayDialog("SDG FrontEnd - Audit", summary, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG FrontEnd] Audit failed: " + ex);
                EditorUtility.DisplayDialog("SDG FrontEnd", "Audit could not finish:\n" + ex.Message, "OK");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
        }

        private static bool Ready()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog("SDG FrontEnd", "Stop Play Mode and let Unity finish compiling.", "OK");
                return false;
            }
            return true;
        }

        private static SDGFrontEndController GetControllerAndMenu(Scene scene, out Transform menu)
        {
            Transform canvas = null;
            SDGFrontEndController ctrl = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "SDG FrontEnd Canvas") canvas = root.transform;
                if (root.name == "SDG FrontEnd Controller") ctrl = root.GetComponent<SDGFrontEndController>();
            }
            if (ctrl == null || canvas == null)
                throw new InvalidOperationException("Front-end controller or Canvas not found. Existing scene was not modified.");
            menu = Require(canvas, "MENU - six actions");
            return ctrl;
        }

        private static Transform Require(Transform parent, string path)
        {
            Transform child = parent.Find(path);
            if (child == null) throw new InvalidOperationException("Missing UI node: " + path);
            return child;
        }

        private static Button RequireButton(Transform node)
        {
            Button button = node.GetComponent<Button>();
            if (button == null) throw new InvalidOperationException("Missing Button on " + node.name);
            return button;
        }

        private static Button[] GetMainButtons(Transform menu)
        {
            Button[] buttons = new Button[6];
            for (int i = 0; i < buttons.Length; i++)
            {
                Transform found = null;
                foreach (Transform child in menu)
                {
                    if (child.name.StartsWith("Button " + (i + 1) + " - ", StringComparison.Ordinal))
                    { found = child; break; }
                }
                if (found == null) throw new InvalidOperationException("Missing main Button " + (i + 1));
                buttons[i] = RequireButton(found);
            }
            return buttons;
        }

        private static bool HasPersistent(UnityEventBase e, UnityEngine.Object target, string method)
        {
            for (int i = 0; i < e.GetPersistentEventCount(); i++)
                if (e.GetPersistentTarget(i) == target && e.GetPersistentMethodName(i) == method)
                    return true;
            return false;
        }

        private static bool HasOtherPersistent(UnityEventBase e, UnityEngine.Object target, string method)
        {
            for (int i = 0; i < e.GetPersistentEventCount(); i++)
            {
                if (e.GetPersistentTarget(i) == target && e.GetPersistentMethodName(i) == method)
                    continue;
                if (e.GetPersistentTarget(i) != null) return true;
            }
            return false;
        }

        private static void WireButton(Button button, SDGFrontEndController ctrl, string method,
            ref int added, ref int verified, ref int conflicts)
        {
            if (HasPersistent(button.onClick, ctrl, method)) { verified++; return; }
            if (HasOtherPersistent(button.onClick, ctrl, method)) { conflicts++; return; }
            UnityAction callback;
            switch (method)
            {
                case "StartNewGame": callback = ctrl.StartNewGame; break;
                case "ContinueGame": callback = ctrl.ContinueGame; break;
                case "OpenLevelSelection": callback = ctrl.OpenLevelSelection; break;
                case "OpenSettings": callback = ctrl.OpenSettings; break;
                case "OpenCredits": callback = ctrl.OpenCredits; break;
                case "QuitGame": callback = ctrl.QuitGame; break;
                case "LaunchLevel01": callback = ctrl.LaunchLevel01; break;
                case "LaunchLevel02": callback = ctrl.LaunchLevel02; break;
                case "LaunchLevel03": callback = ctrl.LaunchLevel03; break;
                case "LaunchLevel04": callback = ctrl.LaunchLevel04; break;
                case "CloseDialogs": callback = ctrl.CloseDialogs; break;
                default: throw new InvalidOperationException("Unknown UI method: " + method);
            }
            UnityEventTools.AddPersistentListener(button.onClick, callback);
            EditorUtility.SetDirty(button);
            added++;
        }

        private static void WireSlider(Slider slider, SDGFrontEndController ctrl,
            ref int added, ref int verified, ref int conflicts)
        {
            if (HasPersistent(slider.onValueChanged, ctrl, "SetMasterVolume")) { verified++; return; }
            if (HasOtherPersistent(slider.onValueChanged, ctrl, "SetMasterVolume")) { conflicts++; return; }
            UnityEventTools.AddPersistentListener(slider.onValueChanged, ctrl.SetMasterVolume);
            EditorUtility.SetDirty(slider);
            added++;
        }

        private static void WireToggle(Toggle toggle, SDGFrontEndController ctrl,
            ref int added, ref int verified, ref int conflicts)
        {
            if (HasPersistent(toggle.onValueChanged, ctrl, "SetFullscreen")) { verified++; return; }
            if (HasOtherPersistent(toggle.onValueChanged, ctrl, "SetFullscreen")) { conflicts++; return; }
            UnityEventTools.AddPersistentListener(toggle.onValueChanged, ctrl.SetFullscreen);
            EditorUtility.SetDirty(toggle);
            added++;
        }

        private static string ResolveScenePath(int index, string existing, out string note)
        {
            if (!string.IsNullOrEmpty(existing) &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(existing + ".unity") != null)
            { note = "Kept existing valid path."; return existing; }

            string folder = "Level" + (index + 1).ToString("00");
            string root = AssetDatabase.IsValidFolder(LevelsRoot) ? LevelsRoot : "Assets";
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { root });
            List<string> candidates = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf("/" + folder + "/", StringComparison.OrdinalIgnoreCase) < 0) continue;
                string lower = path.ToLowerInvariant();
                if (lower.Contains("/tests/") || lower.Contains("/test/") ||
                    lower.Contains("/editor/") || lower.Contains("/backup/") ||
                    lower.Contains("/samples/")) continue;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                    candidates.Add(path);
            }
            candidates.Sort(StringComparer.OrdinalIgnoreCase);
            if (candidates.Count == 0)
            { note = "No scene asset under /" + folder + "/."; return null; }

            string preferredEnd = "/" + folder + "/Scenes/SampleScene.unity";
            List<string> preferred = candidates.Where(p => p.EndsWith(preferredEnd, StringComparison.OrdinalIgnoreCase)).ToList();
            if (preferred.Count == 1)
            { note = "Matched the standard integrated level main scene."; return StripExtension(preferred[0]); }
            if (candidates.Count == 1)
            { note = "Only one scene was found in this level folder."; return StripExtension(candidates[0]); }

            note = "Multiple scenes found; needs manual identification in Controller > Level Scene Paths. Candidates: " +
                   string.Join(" | ", candidates.ToArray());
            return null;
        }

        private static string StripExtension(string assetPath)
        { return assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
            ? assetPath.Substring(0, assetPath.Length - 6) : assetPath; }

        private static int RegisterVerifiedScenes(string[] levelPaths)
        {
            List<EditorBuildSettingsScene> current = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            string[] wanted = (new[] { MenuScene }).Concat(levelPaths
                .Where(p => !string.IsNullOrEmpty(p)).Select(p => p + ".unity")).ToArray();
            int count = 0;
            foreach (string scenePath in wanted)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) continue;
                int existing = current.FindIndex(s => string.Equals(s.path, scenePath, StringComparison.OrdinalIgnoreCase));
                if (existing < 0) { current.Add(new EditorBuildSettingsScene(scenePath, true)); count++; }
                else if (!current[existing].enabled)
                { current[existing] = new EditorBuildSettingsScene(scenePath, true); count++; }
            }
            // Front-end becomes first boot scene. Other existing scenes retain their relative order.
            int frontIndex = current.FindIndex(s => string.Equals(s.path, MenuScene, StringComparison.OrdinalIgnoreCase));
            if (frontIndex > 0)
            {
                EditorBuildSettingsScene front = current[frontIndex];
                current.RemoveAt(frontIndex);
                current.Insert(0, front);
                count++;
            }
            EditorBuildSettings.scenes = current.ToArray();
            return count;
        }

        private static string BackupSceneAndBuildSettings()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string backupRoot = Path.Combine(projectRoot, "SDG_FrontEnd_Backups");
            Directory.CreateDirectory(backupRoot);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string prefix = Path.Combine(backupRoot, "FunctionLink_" + stamp);
            string originalScene = Path.Combine(projectRoot, MenuScene.Replace('/', Path.DirectorySeparatorChar));
            File.Copy(originalScene, prefix + ".unity.bak", false);
            string settings = "";
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                settings += (s.enabled ? "ENABLED " : "DISABLED ") + s.path + "\n";
            File.WriteAllText(prefix + "_BuildSettings_before.txt", settings);
            return prefix;
        }
    }
}
#endif
