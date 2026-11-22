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
    /// Adjusts button heights and a few HUD label rects in the EXISTING front-end scene.
    /// It does not generate scenes, modify sprites, change the CJK mirror implementation,
    /// create TextMeshPro atlases, or touch Level01-Level04.
    /// </summary>
    public static class SDGFrontEndButtonHeightLabelFit
    {
        private const string ScenePath =
            "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scenes/SDG_StartAndMainMenu.unity";

        private const float MainHeight = 96f;
        private const float LevelHeight = 96f;
        private const float BackHeight = 80f;

        // Kept inside the existing HUD ring. A 98-pixel pitch gives 2 pixels
        // of clearance between the 96-pixel UI Button rectangles.
        private static readonly float[] MainY = { 173f, 75f, -23f, -121f, -219f, -317f };
        private static readonly float[] LevelY = { 146f, 48f, -50f, -148f };

        [MenuItem("SDG Adventure/前端 UI/11 加高按鈕並修正面板文字（保留場景）")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog("SDG FrontEnd", "Stop Play Mode and wait for script compilation.", "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog("SDG FrontEnd", "Cannot locate the existing front-end scene. No edits made.", "OK");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedByTool = !scene.IsValid() || !scene.isLoaded;
            if (!openedByTool && scene.isDirty)
            {
                EditorUtility.DisplayDialog("SDG FrontEnd", "Please save the front-end scene with Ctrl+S before applying.", "OK");
                return;
            }

            try
            {
                if (openedByTool)
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

                Transform canvas = FindRoot(scene, "SDG FrontEnd Canvas");
                Transform menu = canvas == null ? null : canvas.Find("MENU - six actions");
                if (menu == null)
                    throw new InvalidOperationException("Existing front-end menu hierarchy was not found.");

                Transform levels = RequireChild(menu, "Level selection dialog");
                Transform player = RequireChild(menu, "Player information panel");
                Transform saves = RequireChild(menu, "Recent save panel");

                RectTransform[] main = new RectTransform[6];
                RectTransform[] level = new RectTransform[4];
                for (int i = 0; i < main.Length; ++i)
                    main[i] = RequireButton(FindButton(menu, "Button " + (i + 1) + " - "), "main " + (i + 1));
                for (int i = 0; i < level.Length; ++i)
                    level[i] = RequireButton(levels.Find("Level " + (i + 1)), "level " + (i + 1));

                string[] backPaths = {
                    "Level selection dialog/Close levels",
                    "Settings dialog/Close settings",
                    "Credits dialog/Close credits"
                };
                RectTransform[] backs = new RectTransform[backPaths.Length];
                for (int i = 0; i < backPaths.Length; ++i)
                    backs[i] = RequireButton(menu.Find(backPaths[i]), backPaths[i]);

                // Check all labels before touching any scene object.
                RequireChild(player, "Player title");
                RequireChild(player, "Experience text");
                RequireChild(saves, "Save title");
                RequireChild(saves, "Save duration");

                string backup = BackupScene();
                for (int i = 0; i < main.Length; ++i)
                    SetButton(main[i], MainHeight, MainY[i]);
                for (int i = 0; i < level.Length; ++i)
                    SetButton(level[i], LevelHeight, LevelY[i]);
                for (int i = 0; i < backs.Length; ++i)
                    SetButton(backs[i], BackHeight, backs[i].anchoredPosition.y);

                // Move the small titles INSIDE the upper frame and XP/duration
                // labels AWAY FROM the lower neon border. Font contents unchanged.
                SetPanelText(player, "Player title", new Vector2(-70f, 43f),
                    new Vector2(190f, 29f), 17f, TextAlignmentOptions.Left);
                SetPanelText(player, "Experience text", new Vector2(143f, -32f),
                    new Vector2(138f, 22f), 14f, TextAlignmentOptions.Right);
                SetPanelText(saves, "Save title", new Vector2(-102f, 42f),
                    new Vector2(220f, 28f), 17f, TextAlignmentOptions.Left);
                SetPanelText(saves, "Save duration", new Vector2(108f, -30f),
                    new Vector2(218f, 23f), 14f, TextAlignmentOptions.Left);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Unity could not save the existing front-end scene.");

                Debug.Log("[SDG FrontEnd] Button height / HUD labels OK. " +
                          "6 main (96), 4 level (96), 3 back (80); 4 HUD labels inset. Backup: " + backup);
                EditorUtility.DisplayDialog("SDG FrontEnd", "Applied to existing scene:\n" +
                    "6 menu buttons: 96px high\n" +
                    "4 level buttons: 96px high\n" +
                    "3 back buttons: 80px high\n" +
                    "4 HUD text labels moved inside frames\n\n" +
                    "No sprite, click event, background or level scene was changed.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG FrontEnd] Button height / HUD text adjustment failed: " + ex);
                EditorUtility.DisplayDialog("SDG FrontEnd", "Update failed:\n" + ex.Message, "OK");
            }
            finally
            {
                if (openedByTool && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == name) return go.transform;
            return null;
        }

        private static Transform RequireChild(Transform parent, string name)
        {
            Transform result = parent.Find(name);
            if (result == null)
                throw new InvalidOperationException("Missing expected UI node: " + name);
            return result;
        }

        private static Transform FindButton(Transform parent, string prefix)
        {
            foreach (Transform child in parent)
                if (child.name.StartsWith(prefix, StringComparison.Ordinal)) return child;
            return null;
        }

        private static RectTransform RequireButton(Transform node, string name)
        {
            if (node == null || !(node is RectTransform) ||
                node.GetComponent<Button>() == null || node.GetComponent<Image>() == null ||
                node.Find("Label") == null)
                throw new InvalidOperationException("Incomplete existing button: " + name);
            return (RectTransform)node;
        }

        private static void SetButton(RectTransform button, float height, float y)
        {
            Vector2 size = button.sizeDelta;
            size.y = height; // Keep the existing width from the v4 widening pass.
            button.sizeDelta = size;
            Vector2 pos = button.anchoredPosition;
            pos.y = y;
            button.anchoredPosition = pos;
            EditorUtility.SetDirty(button);

            Transform label = button.Find("Label");
            RectTransform rt = label as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(Mathf.Max(140f, size.x - 110f), height * 0.82f);
                EditorUtility.SetDirty(rt);
            }
            TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = false;
                EditorUtility.SetDirty(tmp);
            }
            Transform mirror = label.Find("_SDG_CJK_LegacyText");
            if (mirror != null)
            {
                Text legacy = mirror.GetComponent<Text>();
                if (legacy != null)
                {
                    legacy.alignment = TextAnchor.MiddleCenter;
                    legacy.horizontalOverflow = HorizontalWrapMode.Wrap;
                    legacy.verticalOverflow = VerticalWrapMode.Truncate;
                    legacy.resizeTextForBestFit = false;
                    EditorUtility.SetDirty(legacy);
                }
            }
            // Image, Button, sprite states and all onClick listeners are untouched.
        }

        private static void SetPanelText(Transform parent, string name, Vector2 position,
            Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            Transform child = RequireChild(parent, name);
            RectTransform rect = child as RectTransform;
            if (rect == null) throw new InvalidOperationException("Missing RectTransform on " + name);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            EditorUtility.SetDirty(rect);

            TextMeshProUGUI tmp = child.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = fontSize;
                tmp.alignment = alignment;
                tmp.enableWordWrapping = false;
                EditorUtility.SetDirty(tmp);
            }
            Transform mirror = child.Find("_SDG_CJK_LegacyText");
            if (mirror != null)
            {
                Text legacy = mirror.GetComponent<Text>();
                if (legacy != null)
                {
                    legacy.fontSize = Mathf.RoundToInt(fontSize);
                    legacy.alignment = alignment == TextAlignmentOptions.Right
                        ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                    legacy.horizontalOverflow = HorizontalWrapMode.Wrap;
                    legacy.verticalOverflow = VerticalWrapMode.Truncate;
                    legacy.resizeTextForBestFit = false;
                    EditorUtility.SetDirty(legacy);
                }
            }
        }

        private static string BackupScene()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string source = Path.Combine(projectRoot, ScenePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source)) throw new FileNotFoundException("Original front-end scene missing", source);
            string folder = Path.Combine(projectRoot, "SDG_FrontEnd_Backups");
            Directory.CreateDirectory(folder);
            string destination = Path.Combine(folder, "SDG_StartAndMainMenu_before_ButtonHeight_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
            File.Copy(source, destination, false);
            return destination;
        }
    }
}
#endif
