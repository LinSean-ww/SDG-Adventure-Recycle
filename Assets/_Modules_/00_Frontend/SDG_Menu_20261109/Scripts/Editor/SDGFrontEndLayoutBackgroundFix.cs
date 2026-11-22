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
    /// Repairs positions in the existing front-end menu and rebinds its shared background.
    /// Does not regenerate a scene, create TMP font atlases, or touch level scenes.
    /// Compatible with the CJK legacy Text mirror already used by this project.
    /// </summary>
    public static class SDGFrontEndLayoutBackgroundFix
    {
        private const string Root = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109";
        private const string ScenePath = Root + "/Scenes/SDG_StartAndMainMenu.unity";
        private const string BackgroundPath = Root + "/Art/Backgrounds/FE_BG_RecyclingFactory.png";

        [MenuItem("SDG Adventure/前端 UI/05 修正主選單文字與頭像排版（更新現有背景）")]
        private static void RepairExisting()
        {
            if (!CheckPreconditions()) return;
            try
            {
                ImportBackground();
                ApplyLayout(true);
            }
            catch (Exception ex) { ReportError(ex); }
        }

        [MenuItem("SDG Adventure/前端 UI/06 選取新背景 PNG 並修正排版")]
        private static void ReplaceBackgroundAndRepair()
        {
            if (!CheckPreconditions()) return;
            string file = EditorUtility.OpenFilePanel("Choose replacement factory background PNG (16:9)", "", "png");
            if (string.IsNullOrEmpty(file)) return;
            if (!File.Exists(file)) { EditorUtility.DisplayDialog("SDG FrontEnd", "Selected file does not exist.", "OK"); return; }
            try
            {
                byte[] signature = new byte[8];
                using (var stream = File.OpenRead(file))
                {
                    if (stream.Read(signature, 0, 8) != 8 || signature[0] != 137 ||
                        signature[1] != 80 || signature[2] != 78 || signature[3] != 71)
                        throw new InvalidDataException("The selected file is not a valid PNG file.");
                }
                // Ensure the old asset keeps its .meta GUID. Overwrite image bytes only.
                // Import the texture again so both start/menu screens see the replacement.
                string fullDestination = AbsoluteAssetPath(BackgroundPath);
                if (!string.Equals(Path.GetFullPath(file), fullDestination, StringComparison.OrdinalIgnoreCase))
                {
                    BackupBackground(fullDestination);
                    File.Copy(file, fullDestination, true);
                }
                ImportBackground();
                ApplyLayout(true);
            }
            catch (Exception ex) { ReportError(ex); }
        }

        private static bool CheckPreconditions()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog("SDG FrontEnd", "Stop Play Mode and finish compiling before running the fix.", "OK");
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog("SDG FrontEnd", "Missing existing scene:\n" + ScenePath, "OK");
                return false;
            }
            Scene opened = SceneManager.GetSceneByPath(ScenePath);
            if (opened.IsValid() && opened.isLoaded && opened.isDirty)
            {
                EditorUtility.DisplayDialog("SDG FrontEnd", "The front-end scene has unsaved changes. Save it (Ctrl+S), then run this command again.", "OK");
                return false;
            }
            return true;
        }

        private static string AbsoluteAssetPath(string assetRelativePath)
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(project, assetRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void BackupBackground(string path)
        {
            if (File.Exists(path))
                File.Copy(path, MakeBackupPath("FE_BG_RecyclingFactory", ".png"), false);
        }

        private static string MakeBackupPath(string stem, string extension)
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "SDG_FrontEnd_Backups");
            Directory.CreateDirectory(folder);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            return Path.Combine(folder, stem + "_" + timestamp + extension);
        }

        private static Sprite ImportBackground()
        {
            if (!File.Exists(AbsoluteAssetPath(BackgroundPath)))
                throw new FileNotFoundException("Background image does not exist", BackgroundPath);
            var importer = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Unity cannot import background PNG at " + BackgroundPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(BackgroundPath, ImportAssetOptions.ForceUpdate);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            if (sprite == null) throw new InvalidOperationException("Background did not import as Sprite: " + BackgroundPath);
            return sprite;
        }

        private static void ApplyLayout(bool showDialog)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform canvas = FindRoot(scene, "SDG FrontEnd Canvas");
                if (canvas == null) throw new InvalidOperationException("Missing SDG FrontEnd Canvas. This fix requires the existing scene generated by SDGFrontEndBuilder.");
                Transform menu = canvas.Find("MENU - six actions");
                if (menu == null) throw new InvalidOperationException("Missing MENU - six actions. The scene hierarchy differs from the original package.");
                Transform playerPanel = menu.Find("Player information panel");
                Transform savePanel = menu.Find("Recent save panel");
                if (playerPanel == null || savePanel == null)
                    throw new InvalidOperationException("Missing player/save panel; original scene hierarchy not found.");

                // Back up the entire front-end scene before modifying it.
                File.Copy(AbsoluteAssetPath(ScenePath), MakeBackupPath("SDG_StartAndMainMenu", ".unity"), false);

                Transform background = canvas.Find("Background - shared");
                if (background == null) throw new InvalidOperationException("Missing Background - shared.");
                Image bg = background.GetComponent<Image>();
                if (bg == null) throw new InvalidOperationException("Background - shared has no Image component.");
                bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
                bg.type = Image.Type.Simple;
                bg.preserveAspect = false;
                bg.raycastTarget = false;
                RectTransform bgRect = bg.rectTransform;
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.pivot = new Vector2(0.5f, 0.5f);
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
                bgRect.anchoredPosition = Vector2.zero;
                EditorUtility.SetDirty(bg);

                RepairPlayer(playerPanel);
                RepairSave(savePanel);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save front-end scene.");
                AssetDatabase.SaveAssets();
                Debug.Log("[SDG FrontEnd] Layout + shared background refreshed successfully. Player avatar and HUD text fit their panels. Scene backup saved in SDG_FrontEnd_Backups.");
                if (showDialog)
                    EditorUtility.DisplayDialog("SDG FrontEnd", "Layout and background updated.\n\nOpen the Game tab and press Play.\nBackup: SDG_FrontEnd_Backups (project root)", "OK");
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject obj in scene.GetRootGameObjects())
                if (obj.name == name) return obj.transform;
            return null;
        }

        private static void FixRect(Transform element, Vector2 pos, Vector2 size)
        {
            if (element == null) return;
            RectTransform rt = element.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        private static void SetLabel(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize,
            TextAlignmentOptions align)
        {
            Transform child = parent.Find(name);
            if (child == null) { Debug.LogWarning("[SDG FrontEnd] Missing label: " + name); return; }
            FixRect(child, pos, size);
            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.fontSize = fontSize;
                text.alignment = align;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Ellipsis;
                EditorUtility.SetDirty(text);
            }
            // The existing Chinese font mirror follows the disabled TMP component.
            // It stretches to this label's RectTransform, so no font atlas is generated.
            Transform mirror = child.Find("_SDG_CJK_LegacyText");
            if (mirror != null)
            {
                var legacy = mirror.GetComponent<Text>();
                if (legacy != null)
                {
                    legacy.horizontalOverflow = HorizontalWrapMode.Wrap;
                    legacy.verticalOverflow = VerticalWrapMode.Truncate;
                    legacy.resizeTextForBestFit = false;
                    EditorUtility.SetDirty(legacy);
                }
            }
        }

        private static void RepairPlayer(Transform panel)
        {
            FixRect(panel, new Vector2(-668, -51), new Vector2(510, 160));
            SetLabel(panel, "Player title", new Vector2(-70, 58), new Vector2(180, 27), 19, TextAlignmentOptions.Left);
            SetLabel(panel, "Player name", new Vector2(57, 19), new Vector2(305, 31), 22, TextAlignmentOptions.Left);
            SetLabel(panel, "Player level", new Vector2(-29, -20), new Vector2(132, 26), 19, TextAlignmentOptions.Left);
            SetLabel(panel, "Experience text", new Vector2(149, -56), new Vector2(141, 24), 15, TextAlignmentOptions.Right);
            FixRect(panel.Find("XP bar background"), new Vector2(61, -54), new Vector2(297, 10));
            Transform xpBg = panel.Find("XP bar background");
            if (xpBg != null) FixRect(xpBg.Find("XP bar filled"), Vector2.zero, new Vector2(297, 10));

            Transform avatar = panel.Find("Player avatar");
            Transform clip = panel.Find("Avatar window - clipped");
            if (clip == null)
            {
                GameObject container = new GameObject("Avatar window - clipped", typeof(RectTransform), typeof(RectMask2D));
                container.transform.SetParent(panel, false);
                clip = container.transform;
            }
            FixRect(clip, new Vector2(-164, -5), new Vector2(98, 86));
            if (avatar == null) avatar = clip.Find("Player avatar");
            if (avatar != null)
            {
                if (avatar.parent != clip) avatar.SetParent(clip, false);
                FixRect(avatar, new Vector2(0, -7), new Vector2(114, 114));
                Image avatarImage = avatar.GetComponent<Image>();
                if (avatarImage != null) { avatarImage.preserveAspect = true; avatarImage.raycastTarget = false; EditorUtility.SetDirty(avatarImage); }
            }
            else Debug.LogWarning("[SDG FrontEnd] Player avatar was not found. Text alignment was still repaired.");
        }

        private static void RepairSave(Transform panel)
        {
            FixRect(panel, new Vector2(668, -51), new Vector2(510, 165));
            SetLabel(panel, "Save title", new Vector2(-102, 61), new Vector2(218, 26), 18, TextAlignmentOptions.Left);
            FixRect(panel.Find("Screenshot clipping viewport"), new Vector2(-137, -5), new Vector2(185, 89));
            Transform shot = panel.Find("Screenshot clipping viewport");
            if (shot != null) FixRect(shot.Find("Latest save screenshot (optional)"), Vector2.zero, new Vector2(185, 89));
            SetLabel(panel, "Save level", new Vector2(106, 27), new Vector2(230, 28), 20, TextAlignmentOptions.Left);
            SetLabel(panel, "Save progress", new Vector2(104, -26), new Vector2(215, 22), 17, TextAlignmentOptions.Left);
            SetLabel(panel, "Save duration", new Vector2(105, -53), new Vector2(220, 22), 14, TextAlignmentOptions.Left);
            FixRect(panel.Find("Save progress bar background"), new Vector2(108, -2), new Vector2(218, 6));
            Transform track = panel.Find("Save progress bar background");
            if (track != null)
            {
                FixRect(track.Find("Save progress fill"), Vector2.zero, new Vector2(218, 6));
                Image img = track.GetComponent<Image>();
                if (img != null) { img.color = new Color(0.01f, 0.11f, 0.11f, 0.18f); EditorUtility.SetDirty(img); }
            }
        }

        private static void ReportError(Exception ex)
        {
            Debug.LogError("[SDG FrontEnd] Layout/background fix failed: " + ex);
            EditorUtility.DisplayDialog("SDG FrontEnd", "Fix failed:\n" + ex.Message + "\n\nOriginal scene was not intentionally rebuilt.", "OK");
        }
    }
}
#endif
