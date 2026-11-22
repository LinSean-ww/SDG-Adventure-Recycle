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
    /// Widen existing UI Button rects without recreating the scene or changing sprites,
    /// font assets, onClick listeners, layout heights, or Level01-Level04 assets.
    /// </summary>
    public static class SDGFrontEndWiderButtons
    {
        private const string ScenePath =
            "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scenes/SDG_StartAndMainMenu.unity";

        // Original builder: main = 515, levels = 484, back = 285-300.
        // Wider but still within the central HUD (740) and dialog (680).
        private const float MainWidth = 620f;
        private const float LevelWidth = 560f;
        private const float BackWidth = 360f;

        [MenuItem("SDG Adventure/前端 UI/10 加寬主選單與關卡按鈕（保留場景）")]
        public static void WidenExistingButtons()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog("SDG FrontEnd",
                    "Stop Play mode and wait for Unity to finish importing.", "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog("SDG FrontEnd",
                    "Front-end scene not found. No changes made.", "OK");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (!openedHere && scene.isDirty)
            {
                EditorUtility.DisplayDialog("SDG FrontEnd",
                    "Save your current scene (Ctrl+S) before applying this patch.", "OK");
                return;
            }

            try
            {
                if (openedHere)
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

                Transform canvas = FindRoot(scene, "SDG FrontEnd Canvas");
                Transform menu = canvas != null ? canvas.Find("MENU - six actions") : null;
                Transform levels = menu != null ? menu.Find("Level selection dialog") : null;
                if (menu == null || levels == null)
                    throw new InvalidOperationException("Front-end UI hierarchy not found; no changes made.");

                RectTransform[] mains = new RectTransform[6];
                RectTransform[] levelButtons = new RectTransform[4];
                for (int i = 0; i < mains.Length; ++i)
                {
                    Transform node = FindNamedButton(menu, "Button " + (i + 1) + " - ");
                    mains[i] = ValidateButton(node, "Main menu button " + (i + 1));
                }
                for (int i = 0; i < levelButtons.Length; ++i)
                    levelButtons[i] = ValidateButton(levels.Find("Level " + (i + 1)),
                        "Level selection button " + (i + 1));

                string[] backPaths = {
                    "Level selection dialog/Close levels",
                    "Settings dialog/Close settings",
                    "Credits dialog/Close credits"
                };
                RectTransform[] backs = new RectTransform[backPaths.Length];
                for (int i = 0; i < backPaths.Length; i++)
                {
                    Transform item = menu.Find(backPaths[i]);
                    if (item != null)
                        backs[i] = ValidateButton(item, "Dialog Back button");
                }

                // Only back up after validating the entire target hierarchy.
                string backup = BackUpScene();
                for (int i = 0; i < mains.Length; i++)
                    ApplyWidth(mains[i], MainWidth);
                for (int i = 0; i < levelButtons.Length; i++)
                    ApplyWidth(levelButtons[i], LevelWidth);
                int count = mains.Length + levelButtons.Length;
                for (int i = 0; i < backs.Length; i++)
                {
                    if (backs[i] == null) continue;
                    ApplyWidth(backs[i], BackWidth);
                    count++;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Unity could not save the UI scene.");

                Debug.Log("[SDG FrontEnd] Wider buttons OK. Updated " + count +
                    " buttons (main=620, level=560, back=360). Backup: " + backup);
                EditorUtility.DisplayDialog("SDG FrontEnd",
                    "Widened " + count + " buttons.\nMain: 620px\nLevels: 560px\nBack: 360px" +
                    "\n\nButton heights, labels, clicks and sprites preserved.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG FrontEnd] Wider buttons failed: " + ex);
                EditorUtility.DisplayDialog("SDG FrontEnd", "Unable to resize buttons:\n" + ex.Message, "OK");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject obj in scene.GetRootGameObjects())
                if (obj.name == name) return obj.transform;
            return null;
        }

        private static Transform FindNamedButton(Transform parent, string prefix)
        {
            foreach (Transform child in parent)
                if (child.name.StartsWith(prefix, StringComparison.Ordinal)) return child;
            return null;
        }

        private static RectTransform ValidateButton(Transform node, string descriptor)
        {
            if (node == null || node.GetComponent<Button>() == null ||
                node.GetComponent<Image>() == null || !(node is RectTransform))
                throw new InvalidOperationException(descriptor + " is missing or incomplete. No changes made.");
            return (RectTransform)node;
        }

        private static void ApplyWidth(RectTransform buttonRoot, float width)
        {
            // Keep the same height, anchor, pivot, Y position and Image/Sprite.
            Vector2 size = buttonRoot.sizeDelta;
            size.x = width;
            buttonRoot.sizeDelta = size;
            EditorUtility.SetDirty(buttonRoot);

            Transform label = buttonRoot.Find("Label");
            if (label != null)
            {
                RectTransform textRect = label as RectTransform;
                if (textRect != null)
                {
                    textRect.anchorMin = new Vector2(0.5f, 0.5f);
                    textRect.anchorMax = new Vector2(0.5f, 0.5f);
                    textRect.pivot = new Vector2(0.5f, 0.5f);
                    textRect.anchoredPosition = Vector2.zero;
                    Vector2 textSize = textRect.sizeDelta;
                    textSize.x = width - 110f;
                    textRect.sizeDelta = textSize;
                    EditorUtility.SetDirty(textRect);
                }
                TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.alignment = TextAlignmentOptions.Center;
                    EditorUtility.SetDirty(tmp);
                }
                Text legacyOnLabel = label.GetComponent<Text>();
                if (legacyOnLabel != null)
                {
                    legacyOnLabel.alignment = TextAnchor.MiddleCenter;
                    EditorUtility.SetDirty(legacyOnLabel);
                }
                Transform cjkMirror = label.Find("_SDG_CJK_LegacyText");
                if (cjkMirror != null)
                {
                    Text legacy = cjkMirror.GetComponent<Text>();
                    if (legacy != null)
                    {
                        legacy.alignment = TextAnchor.MiddleCenter;
                        EditorUtility.SetDirty(legacy);
                    }
                }
            }
            Transform icon = buttonRoot.Find("Geometric icon");
            if (icon != null && icon.gameObject.activeSelf)
            {
                RectTransform rt = icon as RectTransform;
                if (rt != null)
                {
                    Vector2 p = rt.anchoredPosition;
                    p.x = -width * 0.31f;
                    rt.anchoredPosition = p;
                    EditorUtility.SetDirty(rt);
                }
            }
        }

        private static string BackUpScene()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string source = Path.Combine(projectRoot, ScenePath.Replace('/', Path.DirectorySeparatorChar));
            string backupDir = Path.Combine(projectRoot, "SDG_FrontEnd_Backups");
            Directory.CreateDirectory(backupDir);
            string backup = Path.Combine(backupDir,
                "SDG_StartAndMainMenu_before_WiderButtons_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
            File.Copy(source, backup, false);
            return backup;
        }
    }
}
#endif
