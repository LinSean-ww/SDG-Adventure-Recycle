#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SDG.Cinematics.EditorTools
{
    /// <summary>Explicit, editable mapping. Only writes our own StreamingAssets/SDGCinematics/config.json.</summary>
    public sealed class SDGCinematicEditor : EditorWindow
    {
        private SDGCinematicSettings data;
        private string[] paths;
        private string[] labels;
        private string report = "";

        [MenuItem("Tools/SDG Cinematics/Configure Level Scenes")]
        public static void Open()
        {
            var window = GetWindow<SDGCinematicEditor>("SDG Cinematics");
            window.minSize = new Vector2(630, 370);
            window.Reload();
        }

        [MenuItem("Tools/SDG Cinematics/Auto Detect Level Scenes")]
        public static void AutoDetectWithReport()
        {
            SDGCinematicSettings settings = SDGCinematicSettings.Load();
            if (settings == null) { Debug.LogError("[SDG Cinematics] Missing config."); return; }
            string reportText = Detect(settings);
            WriteConfig(settings);
            Debug.Log("[SDG Cinematics] Auto-map report:\n" + reportText);
            EditorUtility.DisplayDialog("SDG Cinematics - Auto Detect", reportText, "OK");
        }

        [MenuItem("Tools/SDG Cinematics/Validate Package and Scene Mappings")]
        public static void Validate()
        {
            SDGCinematicSettings settings = SDGCinematicSettings.Load();
            if (settings == null) { EditorUtility.DisplayDialog("SDG Cinematics", "Missing config.json", "OK"); return; }
            var enabledPaths = new HashSet<string>(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path), StringComparer.OrdinalIgnoreCase);
            List<string> messages = new List<string>();
            int failures = 0;
            foreach (string name in settings.startupFiles ?? new string[0])
                VerifyFile(name, messages, ref failures);
            foreach (SDGCinematicLevel level in settings.levels ?? new SDGCinematicLevel[0])
            {
                VerifyFile(level.clipFile, messages, ref failures);
                if (string.IsNullOrEmpty(level.scenePath) || !enabledPaths.Contains(level.scenePath))
                {
                    messages.Add("LEVEL " + level.levelNumber + ": not mapped to an enabled Build Settings scene.");
                    failures++;
                }
                else messages.Add("LEVEL " + level.levelNumber + ": OK -> " + level.scenePath);
            }
            string summary = failures == 0 ? "PASS: 7 MP4 files and all 4 level mappings are ready." :
                "CHECK REQUIRED: " + failures + " problem(s). Set the level scene paths before building EXE.";
            Debug.Log("[SDG Cinematics] " + summary + "\n" + string.Join("\n", messages.ToArray()));
            EditorUtility.DisplayDialog("SDG Cinematics - Validation", summary + "\n\nDetails are in Unity Console.", "OK");
        }

        [MenuItem("Tools/SDG Cinematics/Enable Editor Play Preview")]
        public static void EnablePreview()
        {
            EditorPrefs.SetBool("SDG.Cinematics.EditorPreview", true);
            Debug.Log("[SDG Cinematics] Preview ON. Enter Play Mode from the main-menu scene to test startup video.");
        }

        [MenuItem("Tools/SDG Cinematics/Disable Editor Play Preview")]
        public static void DisablePreview()
        {
            EditorPrefs.SetBool("SDG.Cinematics.EditorPreview", false);
            Debug.Log("[SDG Cinematics] Preview OFF. Play Mode tests remain unaffected.");
        }

        // The *first* import only: fill unambiguous scene mappings. No other assets are edited.
        [InitializeOnLoadMethod]
        private static void DetectOnFirstImport()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SDGCinematicSettings settings = SDGCinematicSettings.Load();
                if (settings == null || settings.levels == null) return;
                if (settings.levels.All(l => l != null && !string.IsNullOrEmpty(l.scenePath))) return;
                string report = Detect(settings);
                WriteConfig(settings);
                Debug.Log("[SDG Cinematics] First import: " + report.Replace('\n', ' '));
            };
        }

        private static string Detect(SDGCinematicSettings settings)
        {
            List<string> notes = new List<string>();
            string[] enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            foreach (SDGCinematicLevel level in settings.levels ?? new SDGCinematicLevel[0])
            {
                if (level == null) continue;
                if (!string.IsNullOrEmpty(level.scenePath) && enabled.Any(p => string.Equals(p, level.scenePath, StringComparison.OrdinalIgnoreCase)))
                {
                    notes.Add("Level " + level.levelNumber + ": retained existing mapping.");
                    continue;
                }
                string pattern = @"(?:^|[/_\-\s])(?:SDG[_\-]?)?(?:Level|LVL)[_\-\s]*0*" + level.levelNumber + @"(?=$|[/_\-\s.])";
                string[] matches = enabled.Where(p => Regex.IsMatch(p, pattern, RegexOptions.IgnoreCase)).ToArray();
                if (matches.Length == 1)
                {
                    level.scenePath = matches[0];
                    notes.Add("Level " + level.levelNumber + ": " + matches[0]);
                }
                else
                {
                    // Never guess between duplicate SampleScene.unity files.
                    level.scenePath = "";
                    notes.Add("Level " + level.levelNumber + ": " + (matches.Length == 0 ? "NOT FOUND" : "AMBIGUOUS (" + matches.Length + " scenes)") + ". Configure manually.");
                }
            }
            return string.Join("\n", notes.ToArray());
        }

        private static void VerifyFile(string filename, List<string> output, ref int failures)
        {
            if (string.IsNullOrEmpty(filename) || !File.Exists(Path.Combine(SDGCinematicSettings.RootDirectory, filename)))
            {
                output.Add("VIDEO MISSING: " + filename);
                failures++;
            }
        }

        private static void WriteConfig(SDGCinematicSettings value)
        {
            File.WriteAllText(SDGCinematicSettings.ConfigPath, JsonUtility.ToJson(value, true));
            AssetDatabase.Refresh();
        }

        private void Reload()
        {
            data = SDGCinematicSettings.Load();
            paths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            labels = (new[] { "-- Select scene --" }).Concat(paths).ToArray();
        }

        private void OnGUI()
        {
            if (data == null) { if (GUILayout.Button("Reload")) Reload(); return; }
            EditorGUILayout.LabelField("SDG Adventure: Recycle - Safe Cinematic Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("The playable menu is not replaced. Select the actual enabled Build Settings scene for each level. Changes affect only SDGCinematics/config.json.", MessageType.Info);
            data.enableStartup = EditorGUILayout.Toggle("Enable opening sequence", data.enableStartup);
            data.enableLevels = EditorGUILayout.Toggle("Enable level introductions", data.enableLevels);
            data.skipWithSpace = EditorGUILayout.Toggle("Space skips current clip", data.skipWithSpace);
            EditorGUILayout.Space();
            foreach (SDGCinematicLevel level in data.levels ?? new SDGCinematicLevel[0])
            {
                int index = 0;
                for (int i = 0; i < paths.Length; ++i)
                    if (string.Equals(paths[i], level.scenePath, StringComparison.OrdinalIgnoreCase)) { index = i + 1; break; }
                int picked = EditorGUILayout.Popup("Level " + level.levelNumber + " (" + level.clipFile + ")", index, labels);
                level.scenePath = picked == 0 ? "" : paths[picked - 1];
            }
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto detect (only unambiguous)", GUILayout.Height(30))) report = Detect(data);
            if (GUILayout.Button("Save config", GUILayout.Height(30)))
            {
                WriteConfig(data);
                report = "Saved SDGCinematics/config.json. Build Settings and existing scenes were not changed.";
            }
            if (GUILayout.Button("Validate", GUILayout.Height(30))) Validate();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(report) ? "Preview: Tools > SDG Cinematics > Enable Editor Play Preview" : report, MessageType.None);
        }
    }
}
#endif
