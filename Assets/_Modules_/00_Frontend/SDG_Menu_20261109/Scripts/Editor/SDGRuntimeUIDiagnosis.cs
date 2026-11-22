#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd.Editor
{
    /// <summary>
    /// Read-only snapshot of visible development and pause UI in the running game.
    /// Never changes scene objects, prefabs, build settings, or Time.timeScale.
    /// </summary>
    public static class SDGRuntimeUIDiagnosis
    {
        private struct UiLabel
        {
            public Component source;
            public string value;
        }

        private static readonly string[] MatchTokens = {
            "M6", "C3_TreasurePuzzle", "開發", "驗證", "僅供整合", "選關",
            "暫停", "繼續遊戲", "重新開始本關", "返回選關", "操作設定", "任務清單",
            "進入第一關", "進入第二關", "進入第四關"
        };

        [MenuItem("SDG Adventure/前端 UI/18 擷取執行時 UI 來源（唯讀）")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("SDG Runtime UI Diagnosis",
                    "Press Play, enter a level, wait until the unwanted menu is visible, then run this command.", "OK");
                return;
            }

            try
            {
                StringBuilder sb = new StringBuilder(32768);
                sb.AppendLine("SDG Runtime UI Diagnosis - READ ONLY");
                sb.AppendLine("Captured UTC: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("Unity: " + Application.unityVersion);
                sb.AppendLine("Active scene: " + SceneManager.GetActiveScene().path);
                sb.AppendLine("Time.timeScale: " + Time.timeScale);
                sb.AppendLine();

                sb.AppendLine("=== Loaded scenes ===");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    sb.AppendLine("[" + i + "] " + scene.name + " | " + scene.path + " | loaded=" + scene.isLoaded);
                    if (!scene.isLoaded) continue;
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        if (NameHintsUI(root.name))
                            sb.AppendLine("  Possible UI root: " + root.name + " | active=" + root.activeInHierarchy + " | scripts=" + ScriptNames(root));
                    }
                }
                sb.AppendLine();

                sb.AppendLine("=== All loaded UI Canvases (including persistent scene) ===");
                Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
                int canvasCount = 0;
                foreach (Canvas canvas in canvases)
                {
                    if (canvas == null || !IsLoadedSceneObject(canvas.gameObject)) continue;
                    canvasCount++;
                    sb.AppendLine("Canvas: " + PathOf(canvas.transform));
                    sb.AppendLine("  scene=" + SceneName(canvas.gameObject.scene) + " active=" + canvas.gameObject.activeInHierarchy +
                        " canvasEnabled=" + canvas.enabled + " sortOrder=" + canvas.sortingOrder);
                    AppendScriptsOnAncestors(sb, canvas.transform, 4);
                }
                sb.AppendLine("Total loaded-scene Canvases: " + canvasCount);
                sb.AppendLine();

                List<UiLabel> labels = new List<UiLabel>();
                foreach (TMP_Text tmp in Resources.FindObjectsOfTypeAll<TMP_Text>())
                    if (tmp != null && IsLoadedSceneObject(tmp.gameObject))
                        labels.Add(new UiLabel { source = tmp, value = tmp.text });
                foreach (Text uiText in Resources.FindObjectsOfTypeAll<Text>())
                    if (uiText != null && IsLoadedSceneObject(uiText.gameObject))
                        labels.Add(new UiLabel { source = uiText, value = uiText.text });

                sb.AppendLine("=== Matches (exact object paths and owning scripts) ===");
                int matches = 0;
                foreach (UiLabel item in labels)
                {
                    if (!HasMatch(item.value)) continue;
                    matches++;
                    GameObject go = item.source.gameObject;
                    sb.AppendLine("--- Match #" + matches + " ---");
                    sb.AppendLine("Text: " + OneLine(item.value));
                    sb.AppendLine("Object: " + PathOf(item.source.transform));
                    sb.AppendLine("Scene: " + SceneName(go.scene));
                    sb.AppendLine("activeSelf=" + go.activeSelf + " activeInHierarchy=" + go.activeInHierarchy +
                        " componentEnabled=" + ComponentEnabled(item.source));
                    AppendScriptsOnAncestors(sb, go.transform, 12);
                }
                sb.AppendLine("Total matching labels: " + matches);
                sb.AppendLine();
                sb.AppendLine("=== Candidate UI objects by name ===");
                int named = 0;
                foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (go == null || !IsLoadedSceneObject(go) || !NameHintsUI(go.name)) continue;
                    named++;
                    if (named > 200) { sb.AppendLine("... truncated after 200 objects"); break; }
                    sb.AppendLine(PathOf(go.transform) + " | scene=" + SceneName(go.scene) +
                        " | active=" + go.activeInHierarchy + " | scripts=" + ScriptNames(go));
                }
                sb.AppendLine("Total name matches: " + named);
                sb.AppendLine();
                sb.AppendLine("NOTE: Source lines above identify the scene and components that currently own the UI.");
                sb.AppendLine("This diagnostic has not disabled or changed anything.");

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string directory = Path.Combine(projectRoot, "SDG_UI_Diagnosis");
                Directory.CreateDirectory(directory);
                string filePath = Path.Combine(directory, "UI_Runtime_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
                Debug.Log("[SDG UI Diagnosis] Report saved: " + filePath + " | matching labels=" + matches + " | canvases=" + canvasCount);
                EditorUtility.DisplayDialog("SDG Runtime UI Diagnosis",
                    "Saved read-only report:\n" + filePath + "\n\nSend the text report for a source-level fix.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG UI Diagnosis] Failed to capture report:\n" + ex);
                EditorUtility.DisplayDialog("SDG Runtime UI Diagnosis", ex.ToString(), "OK");
            }
        }

        private static string ComponentEnabled(Component comp)
        {
            Behaviour behaviour = comp as Behaviour;
            return behaviour == null ? "n/a" : behaviour.enabled.ToString();
        }

        private static bool IsLoadedSceneObject(GameObject go)
        {
            Scene scene = go.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static string SceneName(Scene scene)
        {
            return scene.IsValid() ? scene.name + " (" + scene.path + ")" : "(invalid)";
        }

        private static bool HasMatch(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (string token in MatchTokens)
                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool NameHintsUI(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            string name = value.ToLowerInvariant();
            return name.Contains("pause") || name.Contains("menu") || name.Contains("m6") ||
                name.Contains("dev") || name.Contains("select") || name.Contains("canvas") ||
                name.Contains("暫停") || name.Contains("選關") || name.Contains("測試");
        }

        private static string OneLine(string source)
        {
            string value = (source ?? "").Replace("\r", " ").Replace("\n", " / ");
            return value.Length > 300 ? value.Substring(0, 300) + " ..." : value;
        }

        private static void AppendScriptsOnAncestors(StringBuilder sb, Transform start, int limit)
        {
            Transform current = start;
            int depth = 0;
            while (current != null && depth < limit)
            {
                MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
                if (behaviours.Length > 0)
                    sb.AppendLine("  owner[" + depth + "] " + PathOf(current) + " => " + ScriptNames(current.gameObject));
                current = current.parent;
                depth++;
            }
        }

        private static string ScriptNames(GameObject go)
        {
            List<string> names = new List<string>();
            foreach (MonoBehaviour component in go.GetComponents<MonoBehaviour>())
            {
                if (component == null) { names.Add("MISSING SCRIPT"); continue; }
                names.Add(component.GetType().FullName + " [enabled=" + component.enabled + "]");
            }
            return names.Count == 0 ? "(none)" : string.Join(", ", names.ToArray());
        }

        private static string PathOf(Transform transform)
        {
            if (transform == null) return "(none)";
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }
    }
}
#endif
