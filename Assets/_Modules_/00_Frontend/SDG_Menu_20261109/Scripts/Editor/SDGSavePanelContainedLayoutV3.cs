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
    // Repair the existing V2 layout; never rebuild the whole scene or change save files.
    // Artwork is FE_Menu_SavePanel.png (untouched); the frame and all controls are one hierarchy.
    public static class SDGSavePanelContainedLayoutV3
    {
        private const string ScenePath = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scenes/SDG_StartAndMainMenu.unity";
        private const string ReportName = "SDG_SavePanel_ContainedV3_Report.txt";
        private const string Success = "SDG SAVE PANEL CONTAINED V3 SUCCESS";
        private static readonly Color Label = new Color(.83f, 1f, .93f, 1f);

        [MenuItem("SDG Adventure/前端 UI/32 修正加高存檔面板：所有內容放回原外框")]
        public static void ApplyFromMenu()
        {
            try { Apply(); EditorUtility.DisplayDialog("Save UI V3", "已更新場景。請開啟 Game View 檢查外框與四筆紀錄。", "OK"); }
            catch (Exception e) { Debug.LogError("[Save UI V3] " + e); EditorUtility.DisplayDialog("Save UI V3 失敗", e.Message, "OK"); }
        }

        public static void ApplyFromBatch()
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName);
            if (File.Exists(path)) File.Delete(path);
            Apply();
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) throw new FileNotFoundException("Main menu scene: " + ScenePath);
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform canvas = null;
                foreach (GameObject go in scene.GetRootGameObjects()) if (go.name == "SDG FrontEnd Canvas") canvas = go.transform;
                if (canvas == null) throw new InvalidOperationException("Missing front-end canvas.");
                Transform menu = canvas.Find("MENU - six actions");
                Transform panel = menu == null ? null : menu.Find("Recent save panel");
                if (panel == null) throw new InvalidOperationException("The original save panel is missing.");
                var graphic = Required<Image>(panel);
                if (graphic.sprite == null || !graphic.sprite.name.Contains("FE_Menu_SavePanel"))
                    throw new InvalidOperationException("Save-panel image has been replaced; refusing to overwrite custom artwork.");
                RectTransform frame = Required<RectTransform>(panel);
                RectTransform overlay = ChildRect(panel, "SDG Save Progress v2 Tall UI");
                var presenter = overlay.GetComponent<SDGSaveProgressMenuV2>();
                if (presenter == null) throw new InvalidOperationException("V2 save presenter is missing. Do not install V3 on a different menu.");
                if (presenter.slotButtons == null || presenter.slotButtons.Length != 4 || presenter.avatarButton == null ||
                    presenter.ownerPicker == null || presenter.newOwnerButton == null || presenter.loadButton == null ||
                    presenter.saveButton == null || presenter.deleteButton == null)
                    throw new InvalidOperationException("V2 layout is incomplete; no changes saved.");
                if (presenter.originalProfile == null) throw new InvalidOperationException("The existing player portrait reference is missing.");

                // Correct the V2 '0.04' mistake. Border pixel conversion must match 2048 source pixels / 495 UI units.
                string spritePath = AssetDatabase.GetAssetPath(graphic.sprite);
                var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
                if (importer == null || importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != SpriteImportMode.Single)
                    throw new InvalidOperationException("Original artwork is not a single UI sprite.");
                Vector4 borders = new Vector4(180, 262, 180, 262);
                if (importer.spriteBorder != borders)
                {
                    importer.spriteBorder = borders;
                    importer.SaveAndReimport();
                }
                graphic.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (graphic.sprite == null) throw new InvalidOperationException("Save-panel sprite failed to load.");
                Canvas rootCanvas = frame.GetComponentInParent<Canvas>();
                if (rootCanvas == null) throw new InvalidOperationException("Canvas not found.");
                float refPPU = Mathf.Max(.001f, rootCanvas.referencePixelsPerUnit);
                float spritePPU = Mathf.Max(.001f, graphic.sprite.pixelsPerUnit);
                float actualWidth = Mathf.Max(1f, graphic.sprite.rect.width);
                // UGUI Image: displayed sliced border = original border / pixelsPerUnit.
                float desiredEffectivePPU = actualWidth / 495f;
                graphic.pixelsPerUnitMultiplier = desiredEffectivePPU * refPPU / spritePPU;
                graphic.type = Image.Type.Sliced;
                graphic.fillCenter = true;
                graphic.preserveAspect = false;
                graphic.raycastTarget = false;
                frame.sizeDelta = new Vector2(495, 306);
                frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);

                // Keep the overlay as a normal child of the frame, never a second full-screen Canvas.
                SetRect(overlay, Vector2.zero, new Vector2(495, 306));
                RectMask2D mask = overlay.GetComponent<RectMask2D>();
                if (mask == null) mask = overlay.gameObject.AddComponent<RectMask2D>();
                // This dark inset hides the old *three* decorative compartments without hiding the sci-fi outer border.
                var inset = ChildRect(overlay, "Four save rows background");
                SetRect(inset, new Vector2(99, 3), new Vector2(229, 218));
                var insetGraphic = Required<Image>(inset);
                insetGraphic.color = new Color(.009f, .080f, .080f, 1f);
                insetGraphic.raycastTarget = false;
                // Move any existing labels from the screen edge to INSIDE the original outline.
                SetText(overlay, "Left label", "存檔玩家", new Vector2(-133, 108), new Vector2(171, 20), 14);
                SetText(overlay, "Right label", "此玩家的存檔紀錄", new Vector2(100, 108), new Vector2(203, 20), 14);

                SetRect(presenter.avatarButton.transform as RectTransform, new Vector2(-133, 61), new Vector2(82, 78));
                SetText(overlay, "Avatar hint", "點擊頭像切換玩家", new Vector2(-133, 14), new Vector2(174, 17), 11);
                SetRect(Required<RectTransform>(presenter.ownerName.transform), new Vector2(-133, -17), new Vector2(175, 22));
                SetRect(Required<RectTransform>(presenter.ownerProgress.transform), new Vector2(-133, -43), new Vector2(174, 18));
                SetRect(Required<RectTransform>(presenter.ownerPlaytime.transform), new Vector2(-133, -65), new Vector2(174, 18));
                FontSize(presenter.ownerName, 14);
                FontSize(presenter.ownerProgress, 11);
                FontSize(presenter.ownerPlaytime, 11);
                SetRect(presenter.newOwnerButton.transform as RectTransform, new Vector2(-133, -102), new Vector2(152, 24));
                TintButton(presenter.newOwnerButton, new Color(.015f, .18f, .16f, .95f), new Color(.12f, .70f, .56f, .9f));

                // Four actual selectable slots within the right half.  Each row has one title + one date/time line.
                for (int i = 0; i < 4; i++)
                {
                    Button button = presenter.slotButtons[i];
                    if (button == null) throw new InvalidOperationException("Save slot " + i + " is missing.");
                    SetRect(button.transform as RectTransform, new Vector2(100, 73 - i * 38), new Vector2(214, 35));
                    Image bg = button.GetComponent<Image>();
                    if (bg == null) throw new InvalidOperationException("Save slot " + i + " background missing.");
                    // Runtime controls selected row colors; make both blend with the original teal panel.
                    if (presenter.slotTitleLabels[i] == null || presenter.slotDetailLabels[i] == null)
                        throw new InvalidOperationException("Slot label " + i + " missing.");
                    SetRect(presenter.slotTitleLabels[i].rectTransform, new Vector2(0, 8), new Vector2(205, 16));
                    SetRect(presenter.slotDetailLabels[i].rectTransform, new Vector2(0, -9), new Vector2(205, 14));
                    FontSize(presenter.slotTitleLabels[i], 11);
                    FontSize(presenter.slotDetailLabels[i], 9.5f);
                    Outline outline = button.GetComponent<Outline>();
                    if (outline == null) outline = button.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(.10f, .65f, .56f, .72f);
                    outline.effectDistance = new Vector2(.6f, -.6f);
                    outline.useGraphicAlpha = false;
                }
                SetRect(presenter.statusText.rectTransform, new Vector2(100, -76), new Vector2(217, 17));
                FontSize(presenter.statusText, 10);
                SetRect(presenter.loadButton.transform as RectTransform, new Vector2(29, -105), new Vector2(63, 23));
                SetRect(presenter.saveButton.transform as RectTransform, new Vector2(100, -105), new Vector2(63, 23));
                SetRect(presenter.deleteButton.transform as RectTransform, new Vector2(171, -105), new Vector2(63, 23));
                foreach (var btn in new[] { presenter.loadButton, presenter.saveButton, presenter.deleteButton })
                    TintButton(btn, new Color(.015f, .21f, .19f, .97f), new Color(.12f, .70f, .56f, .85f));

                // Player picker appears as an inset within the LEFT compartment and does not escape the shell.
                RectTransform picker = presenter.ownerPicker.GetComponent<RectTransform>();
                if (picker == null) throw new InvalidOperationException("Owner picker rect missing.");
                SetRect(picker, new Vector2(-133, 0), new Vector2(177, 223));
                var pickerImg = picker.GetComponent<Image>();
                if (pickerImg != null) pickerImg.color = new Color(.009f, .12f, .12f, 1f);
                var oldTitle = picker.Find("Picker heading") as RectTransform;
                if (oldTitle != null) SetRect(oldTitle, new Vector2(0, 92), new Vector2(167, 19));
                for (int i = 0; presenter.ownerButtons != null && i < presenter.ownerButtons.Length; i++)
                    if (presenter.ownerButtons[i] != null)
                        SetRect(presenter.ownerButtons[i].transform as RectTransform,
                            new Vector2(0, 56 - i * 39), new Vector2(165, 31));
                for (int i = 0; presenter.ownerButtonLabels != null && i < presenter.ownerButtonLabels.Length; i++)
                    if (presenter.ownerButtonLabels[i] != null) FontSize(presenter.ownerButtonLabels[i], 11);
                if (presenter.ownerPrev != null) SetRect(presenter.ownerPrev.transform as RectTransform, new Vector2(-56, -93), new Vector2(34, 23));
                if (presenter.ownerNext != null) SetRect(presenter.ownerNext.transform as RectTransform, new Vector2(56, -93), new Vector2(34, 23));
                if (presenter.pickerPage != null) SetRect(presenter.pickerPage.rectTransform, new Vector2(0, -93), new Vector2(44, 19));
                var close = picker.Find("Close owner picker") as RectTransform;
                if (close != null) SetRect(close, new Vector2(76, 99), new Vector2(20, 20));

                // The 'save' action is intentionally disabled in front-end (no level is loaded): do not alter V2 logic.
                ValidateDirectChildren(overlay, frame);
                EditorUtility.SetDirty(graphic);
                EditorUtility.SetDirty(presenter);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Failed to save main menu scene.");
                AssetDatabase.SaveAssets();
                string result = Success + "\n" +
                    "Scene=" + ScenePath + "\n" +
                    "OriginalPNG=unchanged\n" +
                    "9Slice=original frame border preserved; effectivePPU=" + desiredEffectivePPU.ToString("F4") + "\n" +
                    "UI=one 495x306 frame; left avatar and owner info; right four selectable save rows and buttons\n" +
                    "Runtime=V2 scripts unchanged; player profiles and save files untouched\n" +
                    "Levels01-04=unchanged\n" +
                    "ProgrammaticRectBounds=PASS; GameViewVisualValidation=PENDING\n";
                File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName), result, new System.Text.UTF8Encoding(false));
                Debug.Log("[Save UI V3] " + result);
            }
            finally { if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
        }

        private static RectTransform ChildRect(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child == null) throw new InvalidOperationException("Missing V2 UI: " + childName);
            RectTransform r = child as RectTransform;
            if (r == null) throw new InvalidOperationException("Not a UI rect: " + childName);
            return r;
        }
        private static T Required<T>(Component component) where T : Component
        {
            T value = component.GetComponent<T>();
            if (value == null) throw new InvalidOperationException("Missing " + typeof(T).Name + ": " + component.name);
            return value;
        }
        private static void SetRect(RectTransform rect, Vector2 pos, Vector2 size)
        {
            if (rect == null) throw new InvalidOperationException("Cannot set missing RectTransform.");
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
        private static void SetText(Transform parent, string name, string content, Vector2 pos, Vector2 size, float fontSize)
        {
            var child = ChildRect(parent, name);
            SetRect(child, pos, size);
            var text = Required<TextMeshProUGUI>(child);
            text.text = content;
            FontSize(text, fontSize);
            text.color = Label;
        }
        private static void FontSize(TextMeshProUGUI text, float size)
        {
            if (text == null) return;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
        }
        private static void TintButton(Button button, Color fill, Color stroke)
        {
            if (button == null) return;
            var img = Required<Image>(button);
            img.color = fill;
            Outline border = button.GetComponent<Outline>();
            if (border == null) border = button.gameObject.AddComponent<Outline>();
            border.effectColor = stroke;
            border.effectDistance = new Vector2(.5f, -.5f);
            border.useGraphicAlpha = false;
        }
        private static void ValidateDirectChildren(RectTransform ui, RectTransform frame)
        {
            // All elements must be children of this single frame (no floating separate canvas).
            if (ui.parent != frame) throw new InvalidOperationException("Save overlay escaped the original panel.");
            Rect rect = ui.rect;
            foreach (RectTransform child in ui.GetComponentsInChildren<RectTransform>(true))
            {
                if (child == ui) continue;
                Vector3[] corners = new Vector3[4];
                child.GetWorldCorners(corners);
                for (int i = 0; i < corners.Length; i++)
                {
                    Vector3 point = ui.InverseTransformPoint(corners[i]);
                    if (point.x < rect.xMin - 1f || point.x > rect.xMax + 1f ||
                        point.y < rect.yMin - 1f || point.y > rect.yMax + 1f)
                        throw new InvalidOperationException("Element outside panel bounds: " + child.name + " (" + point.x + "," + point.y + ")");
                }
            }
        }
    }
}
#endif
