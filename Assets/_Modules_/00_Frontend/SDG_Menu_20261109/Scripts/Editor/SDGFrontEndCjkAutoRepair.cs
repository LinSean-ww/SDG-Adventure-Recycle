#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd.Editor
{
    /// <summary>
    /// Unity 2023.1 / TextMeshPro 3.0.6 safe CJK repair. Uses the imported Noto OTF
    /// with UnityEngine.UI.Text. No TMP_FontAsset or Texture2D atlas is created.
    /// Only the front-end scene is edited; level scenes are never opened.
    /// </summary>
    [InitializeOnLoad]
    public static class SDGFrontEndCjkAutoRepair
    {
        private const string Root = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109";
        private const string FontSource = Root + "/Fonts/NotoSansCJKtc-Regular.otf";
        private const string ScenePath = Root + "/Scenes/SDG_StartAndMainMenu.unity";
        private const string BackupPath = Root + "/Scenes/Backups/SDG_StartAndMainMenu_before_LegacyCJK.unity";
        private const string MirrorName = "_SDG_CJK_LegacyText";
        private static bool working;
        private static bool triedAuto;
        private static double nextCheck;

        static SDGFrontEndCjkAutoRepair()
        {
            EditorApplication.update += AutoCheck;
            EditorSceneManager.sceneSaved += SceneSaved;
        }

        private static void AutoCheck()
        {
            if (triedAuto)
            {
                EditorApplication.update -= AutoCheck;
                return;
            }
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 2.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<Font>(FontSource) == null) return;
            if (!File.Exists(ScenePath)) return;
            triedAuto = true; // Never spam Console if an unexpected error occurs.
            Apply(false);
        }

        private static void SceneSaved(Scene scene)
        {
            if (working || scene.path != ScenePath) return;
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) Apply(false);
            };
        }

        [MenuItem("SDG Adventure/前端 UI/04 一鍵修正繁體中文（免 TMP Atlas）")]
        public static void RepairFromMenu() { Apply(true); }

        private static void Apply(bool showDialog)
        {
            if (working || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            working = true;
            try
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(FontSource);
                if (font == null) throw new InvalidOperationException("Missing imported CJK font: " + FontSource);
                if (!File.Exists(ScenePath)) throw new FileNotFoundException("Missing front-end scene", ScenePath);
                if (Selection.activeObject is TMP_FontAsset) Selection.activeObject = null;
                int count = PatchScene(font);
                AssetDatabase.SaveAssets();
                Debug.Log("[SDG FrontEnd] CJK UI Text repair OK. Checked " + count +
                          " text labels in front-end scene. No TMP atlas created.");
                if (showDialog)
                    EditorUtility.DisplayDialog("SDG CJK Repair", "Ready: " + count +
                        " UI text labels. Open Game and press Play.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG FrontEnd] CJK UI Text repair failed: " + ex);
                if (showDialog) EditorUtility.DisplayDialog("SDG CJK Repair", ex.Message, "OK");
            }
            finally { working = false; }
        }

        private static int PatchScene(Font font)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedByTool = !scene.IsValid() || !scene.isLoaded;
            if (openedByTool) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                int count = 0;
                bool changed = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (tmp == null) continue;
                        count++;
                        var child = tmp.transform.Find(MirrorName);
                        if (child == null)
                        {
                            GameObject go = new GameObject(MirrorName, typeof(RectTransform),
                                typeof(CanvasRenderer), typeof(Text),
                                typeof(SDGAdventure.FrontEnd.SDGFrontEndCjkLegacyMirror));
                            go.transform.SetParent(tmp.transform, false);
                            child = go.transform;
                            changed = true;
                        }

                        var rt = child as RectTransform;
                        if (rt != null)
                        {
                            rt.anchorMin = Vector2.zero;
                            rt.anchorMax = Vector2.one;
                            rt.pivot = new Vector2(0.5f, 0.5f);
                            rt.offsetMin = Vector2.zero;
                            rt.offsetMax = Vector2.zero;
                            rt.localScale = Vector3.one;
                            rt.localRotation = Quaternion.identity;
                        }
                        var ui = child.GetComponent<Text>();
                        if (ui == null) { ui = child.gameObject.AddComponent<Text>(); changed = true; }
                        ui.font = font;
                        ui.text = tmp.text;
                        ui.fontSize = Mathf.Max(1, Mathf.RoundToInt(tmp.fontSize));
                        ui.color = tmp.color;
                        ui.fontStyle = FontStyle.Normal;
                        ui.raycastTarget = false;
                        ui.supportRichText = false;
                        ui.resizeTextForBestFit = false;
                        ui.horizontalOverflow = HorizontalWrapMode.Wrap;
                        ui.verticalOverflow = VerticalWrapMode.Truncate;

                        var mirror = child.GetComponent<SDGAdventure.FrontEnd.SDGFrontEndCjkLegacyMirror>();
                        if (mirror == null)
                        {
                            mirror = child.gameObject.AddComponent<SDGAdventure.FrontEnd.SDGFrontEndCjkLegacyMirror>();
                            changed = true;
                        }
                        mirror.source = tmp;
                        mirror.legacyText = ui;
                        // Retain TMP for serialized controller fields and runtime updates.
                        if (tmp.enabled) { tmp.enabled = false; changed = true; }
                        // Important: changed must be true when an existing mirror was fixed.
                        EditorUtility.SetDirty(tmp);
                        EditorUtility.SetDirty(ui);
                        EditorUtility.SetDirty(mirror);
                    }
                }
                if (count == 0) throw new InvalidOperationException("No front-end TMP labels were found.");

                // Saving an already patched scene is harmless; keeps edited font assignments.
                if (changed || scene.isDirty)
                {
                    if (!File.Exists(BackupPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(BackupPath));
                        if (!AssetDatabase.CopyAsset(ScenePath, BackupPath))
                            throw new IOException("Could not make front-end scene backup.");
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                        throw new IOException("Could not save front-end scene.");
                }
                return count;
            }
            finally
            {
                if (openedByTool && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
