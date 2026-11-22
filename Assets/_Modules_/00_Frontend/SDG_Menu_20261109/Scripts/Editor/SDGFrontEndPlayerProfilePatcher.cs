#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd.Editor
{
    /// <summary>In-place front-end scene patch. No level scene or menu logic is rebuilt.</summary>
    public static class SDGFrontEndPlayerProfilePatcher
    {
        private const string Root = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109";
        private const string ScenePath = Root + "/Scenes/SDG_StartAndMainMenu.unity";
        private const string ReportFile = "SDG_PlayerProfile_InstallReport.txt";
        private static Font font;
        private static readonly Color Pale = new Color(0.86f, 1.0f, 0.95f, 1f);
        private static readonly Color Teal = new Color(0.18f, 1f, 0.84f, 1f);

        [MenuItem("SDG Adventure/前端 UI/24 更新玩家資料面板（不重建關卡）")]
        public static void ApplyFromMenu()
        {
            try { Apply(); EditorUtility.DisplayDialog("SDG Player Profile", "玩家資料面板已更新。請按 Play 測試輸入姓名與選擇頭像。", "OK"); }
            catch (Exception e) { Debug.LogError("[SDG Profile] " + e); EditorUtility.DisplayDialog("SDG Player Profile", e.Message, "OK"); }
        }

        public static void ApplyFromBatch()
        {
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), ReportFile);
            if (File.Exists(path)) File.Delete(path);
            Apply();
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before editing the menu scene.");
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (asset == null) throw new FileNotFoundException("Front-end scene is missing", ScenePath);
            font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/NotoSansCJKtc-Regular.otf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null) throw new InvalidOperationException("No UI Font available. Please import a Chinese font.");

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform root = FindRoot(scene, "SDG FrontEnd Canvas");
                if (root == null) throw new InvalidOperationException("Missing SDG FrontEnd Canvas in front-end scene.");
                Transform menu = root.Find("MENU - six actions");
                if (menu == null) throw new InvalidOperationException("Missing MENU - six actions in front-end scene.");
                Transform panel = menu.Find("Player information panel");
                if (panel == null) throw new InvalidOperationException("Missing Player information panel in menu.");
                if (panel.GetComponent<SDGFrontEndPlayerProfile>() != null)
                    throw new InvalidOperationException("Player Profile v1 is already installed. No duplicate panel was created.");
                var originalAvatar = FindDeep(panel, "Player avatar");
                if (originalAvatar == null || originalAvatar.GetComponent<Image>() == null)
                    throw new InvalidOperationException("Could not find existing Player avatar sprite. No changes saved.");
                var title = panel.Find("Player title");
                if (title != null) title.gameObject.SetActive(false);
                var oldName = panel.Find("Player name");
                if (oldName != null) oldName.gameObject.SetActive(false);

                // Move XP number beneath the avatar (left mini-column). The existing CJK mirror moves with its parent.
                var xpText = panel.Find("Experience text") as RectTransform;
                if (xpText != null) SetRect(xpText, new Vector2(-163, -71), new Vector2(105, 24));
                var tmpXp = xpText == null ? null : xpText.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmpXp != null)
                {
                    tmpXp.alignment = TMPro.TextAlignmentOptions.Center;
                    tmpXp.fontSize = 18;
                }
                var cjkXp = xpText == null ? null : xpText.GetComponentInChildren<Text>(true);
                if (cjkXp != null) { cjkXp.alignment = TextAnchor.MiddleCenter; cjkXp.fontSize = 18; }

                // Keep the original avatar art and clipping intact; add only a transparent click region on top.
                Image avatarImage = originalAvatar.GetComponent<Image>();
                avatarImage.preserveAspect = true;
                avatarImage.raycastTarget = false;
                var chooseRect = UI(panel, "Profile - tap avatar to choose", new Vector2(-164, -5), new Vector2(105, 94));
                Image chooseImage = chooseRect.gameObject.AddComponent<Image>();
                chooseImage.color = new Color(1f, 1f, 1f, 0.001f);
                chooseImage.raycastTarget = true;
                Button chooseButton = chooseRect.gameObject.AddComponent<Button>();
                chooseButton.targetGraphic = chooseImage;
                chooseButton.transition = Selectable.Transition.None;

                InputField name = MakeInput(panel, "Profile - player name input", new Vector2(49, 24), new Vector2(266, 49),
                    PlayerPrefs.GetString("SDG_FE_PlayerName", "回收探險者"), "輸入玩家名稱", 21);
                name.characterLimit = 20;

                var dialog = UI(menu, "SDG Profile - Avatar Picker", new Vector2(-410, 4), new Vector2(508, 510));
                Image dialogBackground = dialog.gameObject.AddComponent<Image>();
                dialogBackground.color = new Color(0.015f, 0.10f, 0.095f, 0.98f);
                dialogBackground.raycastTarget = true;
                dialog.gameObject.AddComponent<Outline>().effectColor = Teal;
                MakeText(dialog, "Picker heading", "選擇玩家頭像", new Vector2(0, 222), new Vector2(340, 42), 26, TextAnchor.MiddleCenter, Pale);
                Button close = MakeButton(dialog, "Close picker", "關閉", new Vector2(209, 222), new Vector2(75, 37), 18);

                var tiles = new SDGFrontEndPlayerProfile.PortraitTile[6];
                for (int i = 0; i < tiles.Length; i++)
                {
                    float x = (i % 3 - 1) * 149;
                    float y = i < 3 ? 116 : -13;
                    Button button = MakeButton(dialog, "Avatar choice " + (i + 1), "", new Vector2(x, y), new Vector2(122, 124), 18);
                    Image thumbnail = UI(button.transform, "Portrait", new Vector2(0, 0), new Vector2(111, 114)).gameObject.AddComponent<Image>();
                    thumbnail.raycastTarget = false;
                    thumbnail.preserveAspect = true;
                    tiles[i] = new SDGFrontEndPlayerProfile.PortraitTile { button = button, image = thumbnail };
                }
                Button prev = MakeButton(dialog, "Previous avatar page", "上一頁", new Vector2(-108, -109), new Vector2(98, 33), 17);
                Button next = MakeButton(dialog, "Next avatar page", "下一頁", new Vector2(108, -109), new Vector2(98, 33), 17);
                Text pages = MakeText(dialog, "Page label", "1 / 1", new Vector2(0, -110), new Vector2(84, 29), 17, TextAnchor.MiddleCenter, Pale);
                MakeText(dialog, "File path label", "自訂頭像 PNG（Editor 可直接選檔）", new Vector2(0, -151), new Vector2(442, 25), 15, TextAnchor.MiddleCenter, Pale);
                InputField filePath = MakeInput(dialog, "Custom PNG file path", new Vector2(-69, -187), new Vector2(329, 40), "", "PNG 檔案完整路徑", 15);
                Button import = MakeButton(dialog, "Import avatar PNG", "匯入 PNG", new Vector2(177, -187), new Vector2(126, 40), 17);
                Text message = MakeText(dialog, "Picker status", "點選頭像即可套用", new Vector2(0, -234), new Vector2(460, 28), 15, TextAnchor.MiddleCenter, Pale);

                var profile = panel.gameObject.AddComponent<SDGFrontEndPlayerProfile>();
                profile.playerNameInput = name;
                profile.avatarImage = avatarImage;
                profile.avatarOpenButton = chooseButton;
                profile.pickerDialog = dialog.gameObject;
                profile.pickerCloseButton = close;
                profile.pickerPrevButton = prev;
                profile.pickerNextButton = next;
                profile.pickerImportButton = import;
                profile.filePathInput = filePath;
                profile.pickerPageLabel = pages;
                profile.pickerMessage = message;
                profile.tiles = tiles;
                profile.builtInAvatars = FindAvatars(avatarImage.sprite).ToArray();
                dialog.gameObject.SetActive(false);

                EditorUtility.SetDirty(profile);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save front-end scene.");
                AssetDatabase.SaveAssets();
                string output = Path.Combine(Path.GetDirectoryName(Application.dataPath), ReportFile);
                string report = "SDG PLAYER PROFILE PATCH SUCCESS\n" +
                    "Scene: " + ScenePath + "\n" +
                    "Player title: hidden\n" +
                    "Player name: editable (PlayerPrefs SDG_FE_PlayerName)\n" +
                    "Experience count: left mini-column\n" +
                    "Avatar: click portrait, choose existing or import PNG\n" +
                    "Bundled project portrait choices: " + profile.builtInAvatars.Length + "\n" +
                    "Other level scenes: unchanged\n";
                File.WriteAllText(output, report, System.Text.Encoding.UTF8);
                Debug.Log("[SDG Profile] SUCCESS: " + report);
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static List<Sprite> FindAvatars(Sprite existing)
        {
            var sprites = new List<Sprite>();
            if (existing != null) sprites.Add(existing);
            string folder = Root + "/Art/Characters";
            if (!AssetDatabase.IsValidFolder(folder)) return sprites;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).OrderBy(x => x))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string filename = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!(filename.Contains("avatar") || filename.Contains("portrait") || filename.Contains("head") || filename.Contains("頭像"))) continue;
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null && !sprites.Contains(sprite)) sprites.Add(sprite);
            }
            return sprites;
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root.transform;
            return null;
        }
        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform hit = FindDeep(child, name);
                if (hit != null) return hit;
            }
            return null;
        }
        private static RectTransform UI(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            SetRect(rect, position, size);
            return rect;
        }
        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
        private static Text MakeText(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize,
            TextAnchor anchor, Color color)
        {
            Text text = UI(parent, name, position, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }
        private static InputField MakeInput(Transform parent, string name, Vector2 position, Vector2 size,
            string current, string hint, int fontSize)
        {
            RectTransform box = UI(parent, name, position, size);
            Image bg = box.gameObject.AddComponent<Image>();
            bg.color = new Color(0.018f, 0.18f, 0.17f, 0.90f);
            Outline outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.92f, 0.81f, 0.75f);
            outline.effectDistance = new Vector2(1f, -1f);
            InputField input = box.gameObject.AddComponent<InputField>();
            Text placeholder = MakeText(box, "Placeholder", hint, Vector2.zero,
                new Vector2(size.x - 24, size.y - 8), fontSize, TextAnchor.MiddleLeft, new Color(0.56f, 0.76f, 0.70f, 0.85f));
            Text value = MakeText(box, "Value", "", Vector2.zero,
                new Vector2(size.x - 24, size.y - 8), fontSize, TextAnchor.MiddleLeft, Pale);
            input.textComponent = value;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 260;
            input.SetTextWithoutNotify(current);
            return input;
        }
        private static Button MakeButton(Transform parent, string name, string caption, Vector2 position, Vector2 size, int fontSize)
        {
            RectTransform box = UI(parent, name, position, size);
            Image bg = box.gameObject.AddComponent<Image>();
            bg.color = new Color(0.025f, 0.25f, 0.22f, 0.96f);
            Button button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            if (!string.IsNullOrEmpty(caption)) MakeText(box, "Label", caption, Vector2.zero,
                new Vector2(size.x - 6, size.y - 4), fontSize, TextAnchor.MiddleCenter, Pale);
            return button;
        }
    }
}
#endif
