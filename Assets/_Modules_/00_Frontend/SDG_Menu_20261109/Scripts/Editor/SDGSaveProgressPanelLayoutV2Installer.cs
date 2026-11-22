#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd.Editor
{
    /// <summary>Rebuilds only the save-panel UI. All 4 gameplay scene files remain untouched.</summary>
    public static class SDGSaveProgressPanelLayoutV2Installer
    {
        private const string ScenePath = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scenes/SDG_StartAndMainMenu.unity";
        private const string Report = "SDG_SavePanelTallV2_InstallReport.txt";
        private const string Success = "SDG SAVE PANEL TALL V2 SUCCESS";
        private static readonly Color TextColor = new Color(0.84f, 1f, 0.94f, 1f);
        private static readonly Color Accent = new Color(0.16f, 1f, 0.68f, 1f);

        [MenuItem("SDG Adventure/前端 UI/31 玩家頭像存檔分頁（加高面板）")]
        public static void ApplyFromMenu()
        {
            try { Apply(); EditorUtility.DisplayDialog("SDG Save UI", "新版存檔面板已儲存；請以 Game View 驗收。", "OK"); }
            catch (Exception e) { Debug.LogError("[SDG Save Tall] " + e); EditorUtility.DisplayDialog("SDG Save UI 失敗", e.Message, "OK"); }
        }
        public static void ApplyFromBatch()
        {
            string report = Path.Combine(Directory.GetParent(Application.dataPath).FullName, Report);
            if (File.Exists(report)) File.Delete(report);
            Apply();
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) throw new FileNotFoundException(ScenePath);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform canvas = Root(scene, "SDG FrontEnd Canvas");
                Transform menu = canvas != null ? canvas.Find("MENU - six actions") : null;
                Transform panel = menu != null ? menu.Find("Recent save panel") : null;
                if (panel == null) throw new InvalidOperationException("Original 'Recent save panel' is missing.");
                var panelArt = panel.GetComponent<Image>();
                if (panelArt == null || panelArt.sprite == null || panelArt.sprite.name.IndexOf("FE_Menu_SavePanel", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new InvalidOperationException("The existing panel no longer uses FE_Menu_SavePanel artwork.");
                SDGFrontEndController controller = null;
                foreach (GameObject top in scene.GetRootGameObjects())
                    if (controller == null) controller = top.GetComponentInChildren<SDGFrontEndController>(true);
                if (controller == null || controller.saveTitleText == null || controller.saveProgressText == null ||
                    controller.saveDurationText == null || controller.saveProgressBarFill == null || controller.continueButton == null)
                    throw new InvalidOperationException("Missing SDGFrontEndController save UI references.");
                if (controller.saveTitleText.font == null) throw new InvalidOperationException("Cannot find existing Chinese TMP font.");
                TMP_FontAsset font = controller.saveTitleText.font;
                Transform existing = panel.Find("SDG Save Progress v2 Tall UI");
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

                // Keep the existing image file unchanged. Modify only its sprite importer border and UI Image type.
                // The center vertical strip stretches, while ornate top/bottom corners stay unscaled.
                string spriteAsset = AssetDatabase.GetAssetPath(panelArt.sprite);
                TextureImporter importer = AssetImporter.GetAtPath(spriteAsset) as TextureImporter;
                if (importer == null || importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
                    throw new InvalidOperationException("FE_Menu_SavePanel must be a Single Sprite for 9-slice. No scene saved.");
                Vector4 border = new Vector4(180, 262, 180, 262); // left,bottom,right,top in 2048×682 source pixels
                if (importer.spriteBorder != border)
                {
                    importer.spriteBorder = border;
                    importer.SaveAndReimport();
                }
                panelArt.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spriteAsset);
                if (panelArt.sprite == null) throw new InvalidOperationException("Sprite reimport failed.");
                panelArt.type = Image.Type.Sliced;
                panelArt.fillCenter = true;
                panelArt.pixelsPerUnitMultiplier = 0.04f; // 2048px source -> ~500px UI, preserves ornate corner widths
                panelArt.preserveAspect = false;
                RectTransform panelRect = panel.GetComponent<RectTransform>();
                panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(.5f, .5f);
                panelRect.sizeDelta = new Vector2(495f, 306f); // 191 * 1.602 = 60.2% taller

                Transform oldUI = panel.Find("SDG Save Progress v1 UI");
                SDGSaveProgressMenuV1 oldPresenter = oldUI != null ? oldUI.GetComponent<SDGSaveProgressMenuV1>() : null;
                if (oldUI != null) oldUI.gameObject.SetActive(false);
                Transform title = panel.Find("Save title");
                if (title != null) title.gameObject.SetActive(false);
                controller.saveTitleText.enabled = false;
                controller.saveProgressText.enabled = false;
                controller.saveDurationText.enabled = false;
                controller.saveProgressBarFill.enabled = false;
                Transform rail = panel.Find("Save progress bar background");
                if (rail != null) rail.gameObject.SetActive(false);
                Transform screenshot = panel.Find("Screenshot clipping viewport");
                if (screenshot != null) screenshot.gameObject.SetActive(false);

                RectTransform overlay = Box(panel, "SDG Save Progress v2 Tall UI", Vector2.zero, new Vector2(495, 306));
                var presenter = overlay.gameObject.AddComponent<SDGSaveProgressMenuV2>();
                // Hide old two/right decorative slots without covering original outer frame.
                Image rightGround = ImageBox(overlay, "Four save rows background", new Vector2(100, 0), new Vector2(225, 222), new Color(.008f, .095f, .095f, .98f));
                rightGround.raycastTarget = false;
                AddText(overlay, "Left label", font, "存檔玩家", new Vector2(-133, 127), new Vector2(182, 23), 16, TextAlignmentOptions.Center);
                AddText(overlay, "Right label", font, "此玩家的存檔紀錄", new Vector2(99, 121), new Vector2(212, 24), 16, TextAlignmentOptions.Center);

                SDGFrontEndPlayerProfile profile = null;
                Transform profileParent = menu.Find("Player information panel");
                if (profileParent != null) profile = profileParent.GetComponent<SDGFrontEndPlayerProfile>();
                if (profile == null || profile.avatarImage == null)
                    throw new InvalidOperationException("The working player avatar controller was not found. Nothing saved.");
                presenter.originalProfile = profile;

                presenter.avatarButton = MakeButton(overlay, "Click portrait to choose owner", "", font, new Vector2(-133, 65), new Vector2(92, 85), 12);
                presenter.avatarImage = presenter.avatarButton.GetComponent<Image>();
                presenter.avatarImage.sprite = profile.avatarImage.sprite;
                presenter.avatarImage.preserveAspect = true;
                presenter.avatarImage.color = Color.white;
                AddText(overlay, "Avatar hint", font, "點頭像切換玩家", new Vector2(-133, 12), new Vector2(175, 19), 12, TextAlignmentOptions.Center);
                presenter.ownerName = AddText(overlay, "Player name", font, "回收探險者", new Vector2(-133, -17), new Vector2(173, 24), 15, TextAlignmentOptions.Center);
                presenter.ownerProgress = AddText(overlay, "Owner progress", font, "通關進度 0%", new Vector2(-133, -43), new Vector2(173, 21), 12, TextAlignmentOptions.Center);
                presenter.ownerPlaytime = AddText(overlay, "Owner play time", font, "累計時間 00:00:00", new Vector2(-133, -64), new Vector2(173, 20), 12, TextAlignmentOptions.Center);
                presenter.newOwnerButton = MakeButton(overlay, "Create another save owner", "＋新增玩家", font,
                    new Vector2(-133, -114), new Vector2(142, 28), 12);

                presenter.slotButtons = new Button[4];
                presenter.slotBackgrounds = new Image[4];
                presenter.slotTitleLabels = new TextMeshProUGUI[4];
                presenter.slotDetailLabels = new TextMeshProUGUI[4];
                string[] titles = { "自動存檔", "手動存檔 1", "手動存檔 2", "手動存檔 3" };
                for (int i = 0; i < 4; i++)
                {
                    float y = 82f - 44f * i;
                    presenter.slotButtons[i] = MakeButton(overlay, "Save slot " + i, "", font,
                        new Vector2(100, y), new Vector2(218, 39), 11);
                    presenter.slotBackgrounds[i] = presenter.slotButtons[i].GetComponent<Image>();
                    presenter.slotTitleLabels[i] = AddText(presenter.slotButtons[i].transform, "Slot heading", font,
                        titles[i], new Vector2(0, 8), new Vector2(210, 17), 12, TextAlignmentOptions.Center);
                    presenter.slotDetailLabels[i] = AddText(presenter.slotButtons[i].transform, "Slot date and time", font,
                        "尚未儲存", new Vector2(0, -9), new Vector2(210, 15), 10, TextAlignmentOptions.Center);
                }
                presenter.statusText = AddText(overlay, "Status message", font, "選擇一筆存檔", new Vector2(99, -98), new Vector2(231, 20), 11, TextAlignmentOptions.Center);
                presenter.loadButton = MakeButton(overlay, "Read save", "讀取", font, new Vector2(28, -125), new Vector2(63, 26), 13);
                presenter.saveButton = MakeButton(overlay, "Write save", "儲存", font, new Vector2(100, -125), new Vector2(63, 26), 13);
                presenter.deleteButton = MakeButton(overlay, "Delete save after confirm", "刪除", font, new Vector2(172, -125), new Vector2(63, 26), 13);
                presenter.continueButton = controller.continueButton;
                if (oldPresenter != null)
                {
                    presenter.autoSaveButton = oldPresenter.autoSaveButton;
                    presenter.autoSaveLabel = oldPresenter.autoSaveLabel;
                }
                if (presenter.autoSaveButton == null && controller.settingsDialog != null)
                {
                    Transform auto = controller.settingsDialog.transform.Find("Toggle auto save");
                    if (auto != null)
                    {
                        presenter.autoSaveButton = auto.GetComponent<Button>();
                        presenter.autoSaveLabel = auto.GetComponentInChildren<TextMeshProUGUI>();
                    }
                }
                // Do not retain old Continue loading behavior; new presenter loads selected owner's newest save.
                for (int i = controller.continueButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                    UnityEventTools.RemovePersistentListener(controller.continueButton.onClick, i);
                UnityEventTools.AddPersistentListener(controller.continueButton.onClick, presenter.ContinueNewest);

                // Owner chooser: only shown after clicking portrait, pages of four owners (up to 16).
                RectTransform picker = Box(overlay, "Choose player who owns saves", new Vector2(-133, 0), new Vector2(190, 226));
                Image pickerBg = picker.gameObject.AddComponent<Image>();
                pickerBg.color = new Color(.005f, .10f, .095f, .99f);
                presenter.ownerPicker = picker.gameObject;
                AddText(picker, "Picker heading", font, "選擇存檔玩家", new Vector2(0, 93), new Vector2(178, 23), 15, TextAlignmentOptions.Center);
                presenter.ownerButtons = new Button[4];
                presenter.ownerButtonLabels = new TextMeshProUGUI[4];
                for (int i = 0; i < 4; i++)
                {
                    Button option = MakeButton(picker, "Owner choice " + i, "", font,
                        new Vector2(0, 58f - 39 * i), new Vector2(171, 32), 12);
                    TextMeshProUGUI optionText = AddText(option.transform, "Owner label", font, "", Vector2.zero,
                        new Vector2(168, 28), 12, TextAlignmentOptions.Center);
                    presenter.ownerButtons[i] = option;
                    presenter.ownerButtonLabels[i] = optionText;
                }
                presenter.ownerPrev = MakeButton(picker, "Previous owner page", "◀", font,
                    new Vector2(-63, -97), new Vector2(39, 26), 12);
                presenter.pickerPage = AddText(picker, "Page", font, "1/1", new Vector2(0, -97), new Vector2(48, 22), 12, TextAlignmentOptions.Center);
                presenter.ownerNext = MakeButton(picker, "Next owner page", "▶", font,
                    new Vector2(63, -97), new Vector2(39, 26), 12);
                Button closePicker = MakeButton(picker, "Close owner picker", "×", font,
                    new Vector2(86, 111), new Vector2(22, 21), 14);
                UnityEventTools.AddPersistentListener(closePicker.onClick, presenter.ClosePicker);
                presenter.ownerPicker.SetActive(false);

                EditorUtility.SetDirty(panelArt);
                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(controller.continueButton);
                EditorUtility.SetDirty(presenter);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save front-end scene.");
                AssetDatabase.SaveAssets();
                string lines = Success + "\n" +
                    "Scene=" + ScenePath + "\n" +
                    "Image=FE_Menu_SavePanel.png original pixels unchanged; Single Sprite 9-slice meta updated\n" +
                    "Panel=495x306 (original 495x191; height +60%)\n" +
                    "Left=click player portrait to switch save owner; new owner; name/progress/playtime\n" +
                    "Right=Auto + Manual1 + Manual2 + Manual3; choose/load/save/delete\n" +
                    "Files=original profile keeps old unscoped saves, new profiles use independent files\n" +
                    "Settings=keeps auto-save toggle; F5 & pause actions are preserved\n" +
                    "Limit=menu save unavailable outside levels; per-level mission inventories not yet adapted\n" +
                    "LevelScenes=unchanged\n" +
                    "VisualPlaymodeValidation=PENDING\n";
                File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, Report), lines, new System.Text.UTF8Encoding(false));
                Debug.Log("[SDG Save Tall] " + lines);
            }
            finally { if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
        }

        private static Transform Root(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root.transform;
            return null;
        }
        private static RectTransform Box(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }
        private static Image ImageBox(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var rect = Box(parent, name, pos, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }
        private static TextMeshProUGUI AddText(Transform parent, string name, TMP_FontAsset font, string content,
            Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align)
        {
            var rect = Box(parent, name, pos, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = TextColor;
            text.text = content;
            text.enableAutoSizing = false;
            text.alignment = align;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }
        private static Button MakeButton(Transform parent, string name, string caption, TMP_FontAsset font,
            Vector2 pos, Vector2 size, float fontSize)
        {
            var image = ImageBox(parent, name, pos, size, new Color(.02f, .20f, .19f, .97f));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (!string.IsNullOrEmpty(caption)) AddText(image.transform, "Caption", font, caption, Vector2.zero,
                size - new Vector2(2, 2), fontSize, TextAlignmentOptions.Center);
            return button;
        }
    }
}
#endif
