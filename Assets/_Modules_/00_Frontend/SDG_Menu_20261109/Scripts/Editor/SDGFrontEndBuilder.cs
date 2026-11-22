#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace SDGAdventure.FrontEnd.Editor
{
    /// <summary>Generate an editable 1920x1080 Unity scene from the ten supplied sprites.</summary>
    public static class SDGFrontEndBuilder
    {
        private const string Root = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109";
        private const string SceneFile = Root + "/Scenes/SDG_StartAndMainMenu.unity";
        private static readonly Color TextColor = new Color(0.89f, 1f, 0.97f, 1f);
        private static readonly Color AccentColor = new Color(0.33f, 1f, 0.84f, 1f);
        private static SDGFrontEndController controller;

        private static string Art(string filename)
        {
            foreach (string folder in new[] { "Backgrounds", "Branding", "StartScreen", "MainMenu", "Characters" })
            {
                string path = Root + "/Art/" + folder + "/" + filename;
                if (File.Exists(path)) return path;
            }
            return Root + "/Art/" + filename;
        }

        private static Sprite Sprite(string filename)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art(filename));
            if (sprite == null) Debug.LogError("[SDG FrontEnd] Missing Sprite: " + filename);
            return sprite;
        }

        [MenuItem("SDG Adventure/前端 UI/00 設定 PNG 為 UI Sprite")]
        public static void ConfigureSprites()
        {
            string[] names = { "FE_BG_RecyclingFactory.png", "FE_Logo_SDGAdventureRecycle.png",
                "FE_Start_RecycleHologram.png", "FE_Start_PressKeyFrame.png",
                "FE_Menu_CenterHUD.png", "FE_Menu_Button_Normal.png", "FE_Menu_Button_Selected.png",
                "FE_Menu_PlayerPanel.png", "FE_Menu_SavePanel.png", "FE_Menu_PlayerAvatar.png" };
            foreach (string name in names)
            {
                string path = Art(name);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool isBackground = name == "FE_BG_RecyclingFactory.png";
                bool isButton = name == "FE_Menu_Button_Normal.png" || name == "FE_Menu_Button_Selected.png";
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = !isBackground;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                // Center can grow; the corners stay intact on 9-Slice UI buttons.
                importer.spriteBorder = isButton ? new Vector4(90, 18, 90, 18) : Vector4.zero;
                importer.SaveAndReimport();
            }
            AssetDatabase.Refresh();
        }

        [MenuItem("SDG Adventure/前端 UI/01 驗證 10 張素材")]
        public static void Validate()
        {
            ConfigureSprites();
            string[] assets = { "FE_BG_RecyclingFactory.png", "FE_Logo_SDGAdventureRecycle.png",
                "FE_Start_RecycleHologram.png", "FE_Start_PressKeyFrame.png",
                "FE_Menu_CenterHUD.png", "FE_Menu_Button_Normal.png", "FE_Menu_Button_Selected.png",
                "FE_Menu_PlayerPanel.png", "FE_Menu_SavePanel.png", "FE_Menu_PlayerAvatar.png" };
            int count = 0;
            foreach (string asset in assets)
            {
                if (Sprite(asset) != null) count++;
            }
            string msg = "已驗證 " + count + "/10 張 Sprite。";
            EditorUtility.DisplayDialog("SDG FrontEnd 素材檢查", msg, "確定");
        }

        [MenuItem("SDG Adventure/前端 UI/02 建立開始畫面與主選單場景")]
        public static void Build()
        {
            ConfigureSprites();
            if (Sprite("FE_BG_RecyclingFactory.png") == null || Sprite("FE_Logo_SDGAdventureRecycle.png") == null)
            {
                EditorUtility.DisplayDialog("無法建立", "請先匯入素材，至少必須有背景及 LOGO。", "確定");
                return;
            }
            if (File.Exists(SceneFile))
            {
                if (!EditorUtility.DisplayDialog("場景已存在", "僅重建本前端套件內的 SDG_StartAndMainMenu.unity。原關卡場景不會被修改，是否繼續？", "重建", "取消"))
                    return;
            }
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObj = new GameObject("UI Camera (unused by Overlay)", typeof(Camera), typeof(AudioListener));
            Camera camera = cameraObj.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.tag = "MainCamera";
            cameraObj.transform.position = new Vector3(0, 0, -10);

            GameObject canvasGO = new GameObject("SDG FrontEnd Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            canvasGO.GetComponent<RectTransform>().anchorMax = Vector2.one;
            canvasGO.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            controller = new GameObject("SDG FrontEnd Controller", typeof(SDGFrontEndController)).GetComponent<SDGFrontEndController>();
            controller.levelScenePaths = FindLevelScenePaths();

            Image background = MakeImage(canvasGO.transform, "Background - shared", Sprite("FE_BG_RecyclingFactory.png"),
                Vector2.zero, new Vector2(1920, 1080), false);
            Stretch(background.rectTransform);
            background.preserveAspect = false; // exact 16:9 background already normalized
            background.raycastTarget = false;

            // Both screens show the same logo.
            Image logo = MakeImage(canvasGO.transform, "Game Logo - shared", Sprite("FE_Logo_SDGAdventureRecycle.png"),
                new Vector2(0, 378), new Vector2(800, 330), false);
            logo.preserveAspect = true;
            logo.raycastTarget = false;

            GameObject start = MakeUI(canvasGO.transform, "START - Press Any Key", Vector2.zero, new Vector2(1920, 1080)).gameObject;
            Stretch((RectTransform)start.transform);
            controller.startScreen = start;
            Image hologram = MakeImage(start.transform, "Recycling hologram", Sprite("FE_Start_RecycleHologram.png"),
                new Vector2(0, -55), new Vector2(785, 440), false);
            hologram.preserveAspect = true;
            Image pressFrame = MakeImage(start.transform, "Press Any Key - frame", Sprite("FE_Start_PressKeyFrame.png"),
                new Vector2(0, -360), new Vector2(570, 142), false);
            pressFrame.preserveAspect = false;
            GameObject textHolder = MakeUI(start.transform, "Press Any Key - blinking label", new Vector2(0, -351), new Vector2(440, 75)).gameObject;
            controller.pressAnyKeyGroup = textHolder.AddComponent<CanvasGroup>();
            controller.pressAnyKeyText = MakeText(textHolder.transform, "Press Any Key", "Press Any Key", Vector2.zero,
                new Vector2(430, 75), 39, TextAlignmentOptions.Center, Color.white);

            GameObject menu = MakeUI(canvasGO.transform, "MENU - six actions", Vector2.zero, new Vector2(1920, 1080)).gameObject;
            Stretch((RectTransform)menu.transform);
            controller.mainMenuScreen = menu;
            Image hud = MakeImage(menu.transform, "Center HUD ring", Sprite("FE_Menu_CenterHUD.png"),
                new Vector2(0, -55), new Vector2(740, 650), false);
            hud.raycastTarget = false;

            // Six menu buttons, common graphics. The first is selected/green in the initial state.
            string[] captions = { "開始遊戲", "繼續遊戲", "關卡選擇", "設定", "製作名單", "離開遊戲" };
            string[] symbols = { "Play", "Continue", "Flag", "Settings", "Credits", "Power" };
            for (int i = 0; i < captions.Length; i++)
            {
                Button button = MakeSciFiButton(menu.transform, "Button " + (i + 1) + " - " + captions[i],
                    new Vector2(0, 185 - 82 * i), new Vector2(515, 76),
                    i == 0 ? "FE_Menu_Button_Selected.png" : "FE_Menu_Button_Normal.png", captions[i], symbols[i], 34);
                switch (i)
                {
                    case 0: UnityEventTools.AddPersistentListener(button.onClick, controller.StartNewGame); break;
                    case 1: UnityEventTools.AddPersistentListener(button.onClick, controller.ContinueGame); controller.continueButton = button; break;
                    case 2: UnityEventTools.AddPersistentListener(button.onClick, controller.OpenLevelSelection); break;
                    case 3: UnityEventTools.AddPersistentListener(button.onClick, controller.OpenSettings); break;
                    case 4: UnityEventTools.AddPersistentListener(button.onClick, controller.OpenCredits); break;
                    case 5: UnityEventTools.AddPersistentListener(button.onClick, controller.QuitGame); break;
                }
            }

            MakePlayerPanel(menu.transform);
            MakeRecentSavePanel(menu.transform);

            controller.statusText = MakeText(menu.transform, "Status / errors", "", new Vector2(0, -490),
                new Vector2(1030, 45), 25, TextAlignmentOptions.Center, Color.yellow);

            MakeLevelsDialog(menu.transform);
            MakeSettingsDialog(menu.transform);
            MakeCreditsDialog(menu.transform);
            controller.levelsDialog.SetActive(false);
            controller.settingsDialog.SetActive(false);
            controller.creditsDialog.SetActive(false);
            // Show initial menu screen only after any key is pressed.
            menu.SetActive(false);
            start.SetActive(true);
            MakeEventSystem();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, SceneFile))
                throw new Exception("Could not save " + SceneFile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneFile);
            EditorUtility.DisplayDialog("場景已建立", "建立完成：\n" + SceneFile + "\n\n接著執行選單 03，把本前端與偵測到的關卡加入 Build Settings。", "確定");
        }

        [MenuItem("SDG Adventure/前端 UI/03 將前端與關卡加入 Build Settings")]
        public static void RegisterScenes()
        {
            if (!File.Exists(SceneFile))
            {
                EditorUtility.DisplayDialog("找不到場景", "請先執行選單 02 建立前端場景。", "確定");
                return;
            }
            var current = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var includedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in current) includedPaths.Add(entry.path);
            int added = 0;
            if (!includedPaths.Contains(SceneFile))
            {
                current.Insert(0, new EditorBuildSettingsScene(SceneFile, true));
                includedPaths.Add(SceneFile);
                added++;
            }
            foreach (string level in FindLevelScenePaths())
            {
                if (string.IsNullOrWhiteSpace(level)) continue;
                string sceneFile = level + ".unity";
                if (!includedPaths.Contains(sceneFile))
                {
                    current.Add(new EditorBuildSettingsScene(sceneFile, true));
                    includedPaths.Add(sceneFile);
                    added++;
                }
            }
            EditorBuildSettings.scenes = current.ToArray();
            EditorUtility.DisplayDialog("Build Settings", "新增 " + added + " 個場景；既有場景原順序與檔案內容沒有修改。\n\n請檢查 Level01~04 的對應場景是否正確。", "確定");
        }

        private static string[] FindLevelScenePaths()
        {
            string[] output = new string[4];
            string root = "Assets/_Modules_/02_Levels";
            if (!AssetDatabase.IsValidFolder(root)) return output;
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { root });
            foreach (string guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                for (int i = 0; i < output.Length; i++)
                {
                    string marker = "/Level" + (i + 1).ToString("00") + "/";
                    if (path.IndexOf(marker, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string withoutExtension = path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                        ? path.Substring(0, path.Length - 6) : path;
                    // Prefer Scenes/SampleScene, then another scene inside that level.
                    bool preferred = path.IndexOf("/Scenes/SampleScene.unity", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (string.IsNullOrEmpty(output[i]) || preferred) output[i] = withoutExtension;
                }
            }
            return output;
        }

        private static RectTransform MakeUI(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image MakeImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, bool nineSlice)
        {
            RectTransform rect = MakeUI(parent, name, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            if (nineSlice && sprite != null) image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, string text, Vector2 position,
            Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            RectTransform rect = MakeUI(parent, name, position, size);
            TextMeshProUGUI component = rect.gameObject.AddComponent<TextMeshProUGUI>();
            component.text = text;
            component.fontSize = fontSize;
            component.color = color;
            component.fontStyle = FontStyles.Bold;
            component.alignment = alignment;
            component.enableWordWrapping = false;
            component.overflowMode = TextOverflowModes.Ellipsis;
            component.raycastTarget = false;
            // If Chinese text displays as boxes, assign a Chinese-supporting TMP font in Inspector.
            return component;
        }

        private static Button MakeSciFiButton(Transform parent, string name, Vector2 position, Vector2 size,
            string spriteFilename, string label, string symbol, float fontSize)
        {
            Image image = MakeImage(parent, name, Sprite(spriteFilename), position, size, true);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            Sprite highlight = Sprite("FE_Menu_Button_Selected.png");
            button.spriteState = new SpriteState
            {
                highlightedSprite = highlight,
                selectedSprite = highlight,
                pressedSprite = highlight,
                disabledSprite = Sprite("FE_Menu_Button_Normal.png")
            };
            float iconX = -size.x * 0.31f;
            var icon = MakeUI(image.transform, "Geometric icon", new Vector2(iconX, 0), new Vector2(46, 46));
            var iconGraphics = icon.gameObject.AddComponent<SDGFrontEndIconGraphic>();
            iconGraphics.color = TextColor;
            iconGraphics.raycastTarget = false;
            SDGFrontEndIconGraphic.IconKind parsed;
            if (Enum.TryParse(symbol, out parsed)) iconGraphics.Kind = parsed;
            MakeText(image.transform, "Label", label, new Vector2(22, 0),
                new Vector2(size.x * .63f, size.y * .83f), fontSize, TextAlignmentOptions.Center, TextColor);
            return button;
        }

        private static void MakePlayerPanel(Transform menu)
        {
            Image panel = MakeImage(menu, "Player information panel", Sprite("FE_Menu_PlayerPanel.png"),
                new Vector2(-670, -52), new Vector2(480, 191), false);
            MakeText(panel.transform, "Player title", "玩家資訊", new Vector2(-120, 68),
                new Vector2(230, 35), 23, TextAlignmentOptions.Left, TextColor);
            Image avatar = MakeImage(panel.transform, "Player avatar", Sprite("FE_Menu_PlayerAvatar.png"),
                new Vector2(-167, -8), new Vector2(128, 132), false);
            avatar.preserveAspect = true;
            controller.playerNameText = MakeText(panel.transform, "Player name", "回收探險者", new Vector2(38, 17),
                new Vector2(255, 39), 24, TextAlignmentOptions.Left, TextColor);
            controller.playerLevelText = MakeText(panel.transform, "Player level", "Lv. 1", new Vector2(-5, -25),
                new Vector2(165, 34), 22, TextAlignmentOptions.Left, TextColor);
            controller.playerExperienceText = MakeText(panel.transform, "Experience text", "0 / 100", new Vector2(118, -62),
                new Vector2(167, 25), 17, TextAlignmentOptions.Right, TextColor);
            Image barBG = MakeImage(panel.transform, "XP bar background", null, new Vector2(49, -56), new Vector2(248, 11), false);
            barBG.color = new Color(0.04f, 0.18f, 0.13f, 0.9f);
            controller.experienceBarFill = MakeImage(barBG.transform, "XP bar filled", null, Vector2.zero, new Vector2(248, 11), false);
            controller.experienceBarFill.color = new Color(0.26f, 1f, 0.44f, 0.95f);
            controller.experienceBarFill.type = Image.Type.Filled;
            controller.experienceBarFill.fillMethod = Image.FillMethod.Horizontal;
            controller.experienceBarFill.fillOrigin = 0;
            controller.experienceBarFill.fillAmount = 0;
        }

        private static void MakeRecentSavePanel(Transform menu)
        {
            Image panel = MakeImage(menu, "Recent save panel", Sprite("FE_Menu_SavePanel.png"),
                new Vector2(670, -52), new Vector2(495, 191), false);
            MakeText(panel.transform, "Save title", "最近的存檔", new Vector2(-108, 70),
                new Vector2(258, 34), 23, TextAlignmentOptions.Center, TextColor);
            RectTransform shot = MakeUI(panel.transform, "Screenshot clipping viewport", new Vector2(-136, -15), new Vector2(173, 101));
            Image shotBG = shot.gameObject.AddComponent<Image>();
            shotBG.color = new Color(0.01f, 0.1f, 0.10f, 0.7f);
            RectMask2D mask = shot.gameObject.AddComponent<RectMask2D>();
            controller.saveThumbnail = MakeUI(shot, "Latest save screenshot (optional)", Vector2.zero, new Vector2(173, 101)).gameObject.AddComponent<RawImage>();
            controller.saveThumbnail.color = new Color(1f, 1f, 1f, 0f);
            controller.saveTitleText = MakeText(panel.transform, "Save level", "尚無存檔", new Vector2(86, 22),
                new Vector2(223, 37), 24, TextAlignmentOptions.Left, TextColor);
            controller.saveProgressText = MakeText(panel.transform, "Save progress", "進度 --", new Vector2(72, -18),
                new Vector2(190, 29), 20, TextAlignmentOptions.Left, TextColor);
            controller.saveDurationText = MakeText(panel.transform, "Save duration", "遊玩時間 --:--:--", new Vector2(80, -58),
                new Vector2(218, 28), 17, TextAlignmentOptions.Left, TextColor);
            Image track = MakeImage(panel.transform, "Save progress bar background", null, new Vector2(87, -42), new Vector2(218, 6), false);
            track.color = new Color(0.05f, 0.19f, 0.16f, 0.7f);
            controller.saveProgressBarFill = MakeImage(track.transform, "Save progress fill", null, Vector2.zero, new Vector2(218, 6), false);
            controller.saveProgressBarFill.color = new Color(0.3f, 1f, 0.61f);
            controller.saveProgressBarFill.type = Image.Type.Filled;
            controller.saveProgressBarFill.fillMethod = Image.FillMethod.Horizontal;
            controller.saveProgressBarFill.fillAmount = 0f;
        }

        private static Image MakeDialog(Transform menu, string name, string heading, Vector2 size)
        {
            RectTransform frame = MakeUI(menu, name, new Vector2(0, -45), size);
            Image background = frame.gameObject.AddComponent<Image>();
            background.color = new Color(0.012f, 0.085f, 0.085f, 0.96f);
            Outline outline = frame.gameObject.AddComponent<Outline>();
            outline.effectColor = AccentColor;
            outline.effectDistance = new Vector2(2, -2);
            MakeText(frame, "Heading", heading, new Vector2(0, size.y / 2f - 54),
                new Vector2(size.x - 80, 60), 34, TextAlignmentOptions.Center, AccentColor);
            return background;
        }

        private static void MakeLevelsDialog(Transform menu)
        {
            Image panel = MakeDialog(menu, "Level selection dialog", "關卡選擇", new Vector2(680, 575));
            controller.levelsDialog = panel.gameObject;
            for (int i = 0; i < 4; i++)
            {
                Button button = MakeSciFiButton(panel.transform, "Level " + (i + 1),
                    new Vector2(0, 145 - i * 94), new Vector2(484, 74),
                    "FE_Menu_Button_Normal.png", "第 " + (i + 1) + " 關", "Level", 29);
                controller.levelButtons[i] = button;
                controller.levelLabels[i] = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                switch (i)
                {
                    case 0: UnityEventTools.AddPersistentListener(button.onClick, controller.LaunchLevel01); break;
                    case 1: UnityEventTools.AddPersistentListener(button.onClick, controller.LaunchLevel02); break;
                    case 2: UnityEventTools.AddPersistentListener(button.onClick, controller.LaunchLevel03); break;
                    case 3: UnityEventTools.AddPersistentListener(button.onClick, controller.LaunchLevel04); break;
                }
            }
            Button back = MakeSciFiButton(panel.transform, "Close levels", new Vector2(0, -248),
                new Vector2(285, 58), "FE_Menu_Button_Normal.png", "返回", "Back", 24);
            UnityEventTools.AddPersistentListener(back.onClick, controller.CloseDialogs);
        }

        private static void MakeSettingsDialog(Transform menu)
        {
            Image panel = MakeDialog(menu, "Settings dialog", "設定", new Vector2(650, 410));
            controller.settingsDialog = panel.gameObject;
            MakeText(panel.transform, "Volume label", "主音量", new Vector2(-195, 66),
                new Vector2(165, 47), 25, TextAlignmentOptions.Left, TextColor);
            RectTransform sliderRT = MakeUI(panel.transform, "Volume slider", new Vector2(72, 65), new Vector2(340, 40));
            Slider slider = sliderRT.gameObject.AddComponent<Slider>();
            Image track = MakeImage(sliderRT, "Background", null, Vector2.zero, new Vector2(340, 11), false);
            track.color = new Color(0.10f, 0.38f, 0.36f, 0.9f);
            Image fill = MakeImage(track.transform, "Fill", null, Vector2.zero, new Vector2(340, 11), false);
            fill.color = AccentColor;
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = fill;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = PlayerPrefs.GetFloat("SDG_FE_MasterVolume", 1f);
            slider.wholeNumbers = false;
            UnityEventTools.AddPersistentListener(slider.onValueChanged, controller.SetMasterVolume);
            MakeText(panel.transform, "Fullscreen label", "全螢幕", new Vector2(-195, -22),
                new Vector2(165, 44), 25, TextAlignmentOptions.Left, TextColor);
            RectTransform toggleRT = MakeUI(panel.transform, "Fullscreen toggle", new Vector2(15, -20), new Vector2(50, 50));
            Toggle toggle = toggleRT.gameObject.AddComponent<Toggle>();
            Image toggleBG = MakeImage(toggleRT, "Background", null, Vector2.zero, new Vector2(38, 38), false);
            toggleBG.color = new Color(0.04f, 0.28f, 0.23f, 0.95f);
            Image check = MakeImage(toggleBG.transform, "Checkmark", null, Vector2.zero, new Vector2(22, 22), false);
            check.color = new Color(0.36f, 1f, 0.40f, 1f);
            toggle.targetGraphic = toggleBG;
            toggle.graphic = check;
            toggle.isOn = Screen.fullScreen;
            UnityEventTools.AddPersistentListener(toggle.onValueChanged, controller.SetFullscreen);
            Button back = MakeSciFiButton(panel.transform, "Close settings", new Vector2(0, -143),
                new Vector2(300, 60), "FE_Menu_Button_Normal.png", "返回", "Back", 24);
            UnityEventTools.AddPersistentListener(back.onClick, controller.CloseDialogs);
        }

        private static void MakeCreditsDialog(Transform menu)
        {
            Image panel = MakeDialog(menu, "Credits dialog", "製作名單", new Vector2(670, 395));
            controller.creditsDialog = panel.gameObject;
            MakeText(panel.transform, "Credits placeholder", "SDG Adventure: Recycle\n\n專題團隊資料尚未填入\n請於此面板的文字元件更新正式名單", new Vector2(0, -20),
                new Vector2(570, 184), 25, TextAlignmentOptions.Center, TextColor);
            Button back = MakeSciFiButton(panel.transform, "Close credits", new Vector2(0, -142),
                new Vector2(300, 60), "FE_Menu_Button_Normal.png", "返回", "Back", 24);
            UnityEventTools.AddPersistentListener(back.onClick, controller.CloseDialogs);
        }

        private static void MakeEventSystem()
        {
            GameObject eventGO = new GameObject("EventSystem", typeof(EventSystem));
            Type newInputModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (newInputModule != null) eventGO.AddComponent(newInputModule);
            else eventGO.AddComponent<StandaloneInputModule>();
        }
    }
}
#endif
