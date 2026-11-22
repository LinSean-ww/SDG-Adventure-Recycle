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
    /// <summary>Updates only the EXISTING FE_Menu_SavePanel-backed panel in the main menu scene.</summary>
    public static class SDGSaveProgressPanelInstallerV1
    {
        private const string MenuScene = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scenes/SDG_StartAndMainMenu.unity";
        private const string ReportName = "SDG_SaveProgressPanel_InstallReport.txt";
        private const string SuccessMarker = "SDG SAVE PROGRESS PANEL SUCCESS";
        private static readonly Color TextColor = new Color(0.84f, 1f, 0.94f, 1f);
        private static readonly Color ButtonBackground = new Color(0.03f, 0.22f, 0.20f, 0.92f);

        [MenuItem("SDG Adventure/前端 UI/30 更新儲存進度面板與存讀檔")]
        public static void ApplyFromMenu()
        {
            try
            {
                Apply();
                EditorUtility.DisplayDialog("SDG Save Progress", "存檔面板已寫入。請進入 Game 視窗驗收。", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG Save Progress] " + ex);
                EditorUtility.DisplayDialog("SDG Save Progress - FAILED", ex.Message, "OK");
            }
        }

        public static void ApplyFromBatch()
        {
            string report = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName);
            if (File.Exists(report)) File.Delete(report);
            Apply();
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene) == null)
                throw new FileNotFoundException("Missing main menu scene: " + MenuScene);

            Scene scene = SceneManager.GetSceneByPath(MenuScene);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Additive);
            try
            {
                Transform canvas = FindRoot(scene, "SDG FrontEnd Canvas");
                Transform menu = canvas != null ? canvas.Find("MENU - six actions") : null;
                Transform panel = menu != null ? menu.Find("Recent save panel") : null;
                if (panel == null) throw new InvalidOperationException("Existing 'Recent save panel' not found. No scene saved.");
                Image panelArt = panel.GetComponent<Image>();
                if (panelArt == null || panelArt.sprite == null ||
                    panelArt.sprite.name.IndexOf("FE_Menu_SavePanel", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new InvalidOperationException("Save panel does not use approved FE_Menu_SavePanel artwork.");

                SDGFrontEndController controller = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (SDGFrontEndController candidate in root.GetComponentsInChildren<SDGFrontEndController>(true))
                        if (candidate != null) { controller = candidate; break; }
                if (controller == null || controller.saveTitleText == null || controller.saveProgressText == null ||
                    controller.saveDurationText == null || controller.saveProgressBarFill == null ||
                    controller.continueButton == null)
                    throw new InvalidOperationException("Main menu controller/save UI bindings missing. No scene saved.");

                if (panel.Find("SDG Save Progress v1 UI") != null)
                    throw new InvalidOperationException("Save Progress v1 is already installed; refusing duplicate UI.");
                if (controller.settingsDialog == null)
                    throw new InvalidOperationException("Settings dialog not bound; cannot install auto-save switch.");

                TMP_FontAsset font = controller.saveProgressText.font;
                if (font == null) throw new InvalidOperationException("Cannot reuse current CJK TMP font.");

                // Existing background Image / title / save text are preserved.
                GameObject overlayObject = new GameObject("SDG Save Progress v1 UI", typeof(RectTransform));
                RectTransform overlay = overlayObject.GetComponent<RectTransform>();
                overlay.SetParent(panel, false);
                overlay.anchorMin = overlay.anchorMax = overlay.pivot = new Vector2(0.5f, 0.5f);
                overlay.anchoredPosition = Vector2.zero;
                overlay.sizeDelta = new Vector2(495, 191);
                overlay.localScale = Vector3.one;
                SDGSaveProgressMenuV1 ui = overlayObject.AddComponent<SDGSaveProgressMenuV1>();

                // The rectangular left inset of FE_Menu_SavePanel is about 173 x 101 at (-136, -15).
                // Use this inset for the latest save/player metadata, not a second panel background.
                ui.leftPlayer = AddLabel(overlay, "Save player", font, new Vector2(-137, 20),
                    new Vector2(170, 23), 14, TextAlignmentOptions.MidlineLeft);
                ui.leftSaved = AddLabel(overlay, "Saved at", font, new Vector2(-137, -4),
                    new Vector2(170, 22), 11, TextAlignmentOptions.MidlineLeft);
                ui.leftSlot = AddLabel(overlay, "Save slot", font, new Vector2(-137, -28),
                    new Vector2(166, 21), 13, TextAlignmentOptions.Center);
                ui.previousButton = AddButton(overlay, "Previous save", "◀", font,
                    new Vector2(-201, -52), new Vector2(29, 24), 13);
                ui.loadButton = AddButton(overlay, "Load selected save", "讀取", font,
                    new Vector2(-136, -52), new Vector2(87, 25), 14);
                ui.nextButton = AddButton(overlay, "Next save", "▶", font,
                    new Vector2(-72, -52), new Vector2(29, 24), 13);

                ui.rightLevel = controller.saveTitleText;
                ui.rightProgress = controller.saveProgressText;
                ui.rightDuration = controller.saveDurationText;
                ui.progressFill = controller.saveProgressBarFill;
                ui.continueButton = controller.continueButton;

                // The existing Progress Fill was originally a sprite-less Filled Image;
                // a simple Image with left anchored width renders reliably in Unity 2023.1.
                Image fill = ui.progressFill;
                fill.type = Image.Type.Simple;
                fill.raycastTarget = false;
                RectTransform fillRect = fill.rectTransform;
                fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, 0.5f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = new Vector2(0f, 6f);

                // The optional screenshot was never required. Hide only its render
                // so player/save text remains legible. Keep original art and component intact.
                if (controller.saveThumbnail != null) controller.saveThumbnail.enabled = false;

                Transform settings = controller.settingsDialog.transform;
                AddLabel(settings, "Auto save setting title", font, new Vector2(-190f, -83f),
                    new Vector2(165, 34), 23, TextAlignmentOptions.MidlineLeft).text = "自動存檔";
                ui.autoSaveButton = AddButton(settings, "Toggle auto save", "自動存檔：開", font,
                    new Vector2(67f, -83f), new Vector2(225, 42), 20);
                ui.autoSaveLabel = ui.autoSaveButton.GetComponentInChildren<TextMeshProUGUI>();

                // Continue now reads the newest verified v2 JSON save, not old metadata.
                Button continueButton = controller.continueButton;
                for (int i = continueButton.onClick.GetPersistentEventCount() - 1; i >= 0; --i)
                    UnityEventTools.RemovePersistentListener(continueButton.onClick, i);
                UnityEventTools.AddPersistentListener(continueButton.onClick, ui.ContinueNewest);
                EditorUtility.SetDirty(continueButton);
                EditorUtility.SetDirty(ui);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Unity could not save menu scene.");
                AssetDatabase.SaveAssets();

                string content = SuccessMarker + "\n" +
                    "Scene=" + MenuScene + "\n" +
                    "Artwork=existing FE_Menu_SavePanel.png untouched\n" +
                    "Slots=Auto,Manual1,Manual2,Manual3 (separate JSON in persistentDataPath)\n" +
                    "Manual=F5 or pause menu; Load=menu or F9\n" +
                    "Auto=Settings toggle, interval 150 seconds and focus/quit\n" +
                    "Progress=actual module completion events; four levels, 25% each\n" +
                    "Save=level/player position,name,avatar,XP,reported HP,old shutter states,time\n" +
                    "LIMITATION=inventory, mission flags, puzzle objects and live health restoration require game-specific adapters\n" +
                    "Other scenes=not modified\n" +
                    "GameView=not yet verified\n";
                File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName),
                    content, new System.Text.UTF8Encoding(false));
                Debug.Log("[SDG Save Progress] " + content);
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root.transform;
            return null;
        }

        private static TextMeshProUGUI AddLabel(Transform parent, string name, TMP_FontAsset font,
            Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.alignment = align;
            text.color = TextColor;
            text.text = "";
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static Button AddButton(Transform parent, string name, string caption,
            TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = ButtonBackground;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            TextMeshProUGUI label = AddLabel(go.transform, "Label", font, Vector2.zero,
                size - new Vector2(3f, 2f), fontSize, TextAlignmentOptions.Center);
            label.text = caption;
            return button;
        }
    }
}
#endif
