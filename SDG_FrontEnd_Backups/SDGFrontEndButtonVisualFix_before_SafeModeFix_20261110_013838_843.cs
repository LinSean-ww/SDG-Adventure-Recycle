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
    // Patches only existing Button images and labels; it NEVER rebuilds a scene.
    // Does not touch original artwork, TMP font atlases, or level scenes.
    public static class SDGFrontEndButtonVisualFix
    {
        private const string Root = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109";
        private const string ScenePath = Root + "/Scenes/SDG_StartAndMainMenu.unity";
        private const string ArtPath = Root + "/Art/ButtonFix/";
        private const string Normal = "FE_Menu_Button_Normal_Fitted.png";
        private const string Selected = "FE_Menu_Button_Selected_Fitted.png";
        private const string Bright = "FE_Menu_Button_Bright_Fitted.png";
        private const string Press = "FE_Start_PressKeyFrame_Fitted_Optional.png";

        [MenuItem("SDG Adventure/前端 UI/07 套用正式按鈕及文字置中（保留場景）")]
        public static void FixButtons()
        {
            RunFix(true, false);
        }

        [MenuItem("SDG Adventure/前端 UI/08 首個按鈕改用 Selected 綠色款")]
        public static void UseStandardSelected()
        {
            RunFix(false, false);
        }

        // Optional only: start screen previously worked, so do not change it by default.
        [MenuItem("SDG Adventure/前端 UI/09 可選：更新 Press Any Key 外框")]
        public static void UpdatePressFrame()
        {
            RunFix(true, true);
        }

        private static bool CanRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog("SDG FrontEnd", "Stop Play mode and allow Unity to finish importing before applying the button fix.", "OK");
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog("SDG FrontEnd", "Existing front-end scene was not found. This patch does not build or recreate the scene.", "OK");
                return false;
            }
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded && scene.isDirty)
            {
                EditorUtility.DisplayDialog("SDG FrontEnd", "Save the current scene with Ctrl+S first, then run this command again.", "OK");
                return false;
            }
            return true;
        }

        private static Sprite LoadSprite(string filename)
        {
            string path = ArtPath + filename;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing button image: " + path);
            // The source art has ~200 px of empty alpha above/below the button.
            // The new PNGs are pre-trimmed; configure a FULL RECT 9-slice sprite.
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           importer.spritePixelsPerUnit != 600f ||
                           importer.spriteBorder != new Vector4(380, 90, 380, 90) ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed ||
                           importer.mipmapEnabled;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 600f;
                importer.spriteMeshType = SpriteMeshType.FullRect;
                importer.spriteBorder = new Vector4(380, 90, 380, 90);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException("Button image did not import as a Sprite: " + path);
            return sprite;
        }

        private static void RunFix(bool brightFirst, bool pressOnly)
        {
            if (!CanRun()) return;
            Scene scene = default(Scene);
            bool openedByTool = false;
            try
            {
                // Load images before touching the scene.
                Sprite normal = LoadSprite(Normal);
                Sprite selected = LoadSprite(Selected);
                Sprite bright = LoadSprite(Bright);
                Sprite press = pressOnly ? LoadSprite(Press) : null;

                scene = SceneManager.GetSceneByPath(ScenePath);
                openedByTool = !scene.IsValid() || !scene.isLoaded;
                if (openedByTool)
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

                Transform canvas = FindRoot(scene, "SDG FrontEnd Canvas");
                Transform menu = canvas == null ? null : canvas.Find("MENU - six actions");
                Transform levels = menu == null ? null : menu.Find("Level selection dialog");
                if (canvas == null || menu == null || levels == null)
                    throw new InvalidOperationException("Original UI hierarchy is missing. The front-end scene was not modified.");

                int count = 0;
                if (!pressOnly)
                {
                    // Validate all buttons first; no partial patch on an incomplete scene.
                    for (int i = 1; i <= 6; i++)
                        if (FindMainButton(menu, i) == null)
                            throw new InvalidOperationException("Missing main-menu button " + i + ". Scene was not modified.");
                    for (int i = 1; i <= 4; i++)
                        if (levels.Find("Level " + i) == null)
                            throw new InvalidOperationException("Missing Level " + i + " button. Scene was not modified.");
                    for (int i = 1; i <= 6; i++)
                    {
                        Transform node = FindMainButton(menu, i);
                        if (node.GetComponent<Button>() == null || node.GetComponent<Image>() == null)
                            throw new InvalidOperationException("Main-menu button " + i + " has no Button or Image component.");
                    }
                }
                else if (canvas.Find("START - Press Any Key/Press Any Key - frame") == null)
                    throw new InvalidOperationException("Start frame not found. Scene was not modified.");

                // Back up the existing scene before editing it.
                string backup = BackupScene();
                if (pressOnly)
                {
                    Transform frameNode = canvas.Find("START - Press Any Key/Press Any Key - frame");
                    Image image = frameNode.GetComponent<Image>();
                    if (image == null) throw new InvalidOperationException("Start frame has no Image component.");
                    image.sprite = press;
                    image.type = Image.Type.Sliced;
                    image.preserveAspect = false;
                    RectTransform frame = image.rectTransform;
                    SetRect(frame, new Vector2(0, -360), new Vector2(570, 108));
                    EditorUtility.SetDirty(image);
                    // Preserve the text-holder and the controller's blinking label reference.
                    Transform textHolder = canvas.Find("START - Press Any Key/Press Any Key - blinking label");
                    if (textHolder != null)
                        SetRect(textHolder as RectTransform, new Vector2(0, -352), new Vector2(470, 76));
                    count = 1;
                }
                else
                {
                    for (int i = 1; i <= 6; i++)
                    {
                        Transform button = FindMainButton(menu, i);
                        // First button is bright green by default; use alternate menu item 08
                        // if you prefer the official Selected sprite instead of MENU-03.
                        Sprite baseSprite = i == 1 ? (brightFirst ? bright : selected) : normal;
                        PatchButton(button, baseSprite, selected, normal, i == 1, false);
                        count++;
                    }
                    for (int i = 1; i <= 4; i++)
                    {
                        PatchButton(levels.Find("Level " + i), normal, selected, normal, false, true);
                        count++;
                    }
                    string[] dialogBackPaths = { "Level selection dialog/Close levels",
                        "Settings dialog/Close settings", "Credits dialog/Close credits" };
                    foreach (string path in dialogBackPaths)
                    {
                        Transform t = menu.Find(path);
                        if (t != null) { PatchButton(t, normal, selected, normal, false, true); count++; }
                    }
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Unity could not save the front-end scene.");
                AssetDatabase.SaveAssets();
                Debug.Log("[SDG FrontEnd] Button artwork and centered text updated. Button count=" + count + ". Scene backup=" + backup);
                EditorUtility.DisplayDialog("SDG FrontEnd", pressOnly
                    ? "Start frame updated. Button layouts were not changed.\nBackup: " + backup
                    : "Updated " + count + " buttons (menu + levels + Back) and centered their labels.\nNo click events or other scenes were changed.\nBackup: " + backup,
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG FrontEnd] Button visual fix failed: " + ex);
                EditorUtility.DisplayDialog("SDG FrontEnd", "Button fix failed:\n" + ex.Message + "\nThe tool did not intentionally rebuild the scene.", "OK");
            }
            finally
            {
                if (openedByTool && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject obj in scene.GetRootGameObjects())
                if (obj.name == name) return obj.transform;
            return null;
        }

        private static Transform FindMainButton(Transform menu, int index)
        {
            foreach (Transform child in menu)
            {
                if (child.name.StartsWith("Button " + index + " - ", StringComparison.Ordinal))
                    return child;
            }
            return null;
        }

        private static string BackupScene()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string source = Path.Combine(projectRoot, ScenePath.Replace('/', Path.DirectorySeparatorChar));
            string folder = Path.Combine(projectRoot, "SDG_FrontEnd_Backups");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "SDG_StartAndMainMenu_before_ButtonFix_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity");
            File.Copy(source, file, false);
            return file;
        }

        private static void SetRect(RectTransform rect, Vector2 pos, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            EditorUtility.SetDirty(rect);
        }

        private static void PatchButton(Transform node, Sprite baseSprite, Sprite selected,
            Sprite normal, bool first, bool insideDialog)
        {
            if (node == null) return;
            Image image = node.GetComponent<Image>();
            Button button = node.GetComponent<Button>();
            if (image == null || button == null)
            {
                Debug.LogWarning("[SDG FrontEnd] Ignoring non-button node " + node.name);
                return;
            }
            RectTransform rootRect = node as RectTransform;
            if (rootRect != null)
            {
                Vector2 size = rootRect.sizeDelta;
                // Keep the original layout's positions and hitboxes; only normalize height.
                size.y = Mathf.Max(size.y, insideDialog ? 70f : 78f);
                SetRect(rootRect, rootRect.anchoredPosition, size);
            }
            image.sprite = baseSprite;
            image.overrideSprite = null;
            image.color = Color.white;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 1f;
            image.raycastTarget = true;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState {
                highlightedSprite = selected,
                selectedSprite = selected,
                pressedSprite = selected,
                disabledSprite = normal
            };
            // The original onClick events are intentionally untouched.
            Transform label = node.Find("Label");
            if (label != null)
            {
                RectTransform labelRect = label as RectTransform;
                if (labelRect != null)
                {
                    float width = rootRect == null ? 430f : rootRect.sizeDelta.x;
                    float height = rootRect == null ? 70f : rootRect.sizeDelta.y;
                    SetRect(labelRect, Vector2.zero, new Vector2(Mathf.Max(150f, width - 118f), height * 0.86f));
                }
                TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.enableWordWrapping = false;
                    tmp.overflowMode = TextOverflowModes.Ellipsis;
                    EditorUtility.SetDirty(tmp);
                }
                Transform mirror = label.Find("_SDG_CJK_LegacyText");
                if (mirror != null)
                {
                    RectTransform mirrorRect = mirror as RectTransform;
                    if (mirrorRect != null)
                    {
                        mirrorRect.anchorMin = Vector2.zero;
                        mirrorRect.anchorMax = Vector2.one;
                        mirrorRect.pivot = new Vector2(0.5f, 0.5f);
                        mirrorRect.offsetMin = Vector2.zero;
                        mirrorRect.offsetMax = Vector2.zero;
                        mirrorRect.anchoredPosition = Vector2.zero;
                        EditorUtility.SetDirty(mirrorRect);
                    }
                    Text legacy = mirror.GetComponent<Text>();
                    if (legacy != null)
                    {
                        legacy.alignment = TextAnchor.MiddleCenter;
                        legacy.horizontalOverflow = HorizontalWrapMode.Wrap;
                        legacy.verticalOverflow = VerticalWrapMode.Truncate;
                        legacy.raycastTarget = false;
                        EditorUtility.SetDirty(legacy);
                    }
                }
            }
            // Keep main-menu icon nodes, since the original concept uses icons.
            // Hide them on level-selection and Back buttons; center text is clearer there.
            Transform icon = node.Find("Geometric icon");
            if (icon != null && insideDialog)
            {
                icon.gameObject.SetActive(false);
                EditorUtility.SetDirty(icon.gameObject);
            }
            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(button);
        }
    }
}
#endif
