using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SDGAdventure.FrontEnd
{
    /// <summary>
    /// R1.2: runtime layout replacement on the existing front-end scene.
    /// Does not destroy original menu, profile, save scripts or save files.
    /// Uses the existing CJK legacy-mirrored button labels so Chinese text renders.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class SDGMenuReworkR1 : MonoBehaviour
    {
        public SDGFrontEndController frontEnd;
        private Transform menu;
        private Button template;
        private Button startButton, settingsButton, quitButton, creditsButton;
        private GameObject startPopup, savePopup, playerPopup, ownerPopup, playerPanel, oldSavePanel;
        private GameObject shade, circle;
        private RectTransform playerSurface;
        private TMP_Text playerStatus, playerSummaryName, playerSummaryStats, startStatus, saveStatus, ownerPageText;
        private TMP_Text[] slotTexts = new TMP_Text[4];
        private Button[] slotButtons = new Button[4];
        private Button[] owners = new Button[4];
        private TMP_Text[] ownerNames = new TMP_Text[4];
        private Image playerSummaryAvatar;
        private SDGFrontEndPlayerProfile profile;
        private string originalName, originalAvatar, originalOwner;
        private int originalSeconds, originalCompleted, ownerPage, selectedSaveSlot = 1;
        private float deleteConfirmUntil;
        private int deleteConfirmSlot = -1;
        private bool installed;

        private void Start()
        {
            try { Install(); }
            catch (Exception e) { Debug.LogError("[SDG Menu R1.2] UI setup failed; check original front-end assets: " + e); }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform result = FindDeep(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private static Button MainButton(Transform parent, int index)
        {
            foreach (Transform child in parent)
                if (child.name.StartsWith("Button " + index + " - ", StringComparison.Ordinal))
                    return child.GetComponent<Button>();
            return null;
        }

        private void Install()
        {
            if (installed) return;
            if (frontEnd == null) frontEnd = GetComponent<SDGFrontEndController>();
            if (frontEnd == null || frontEnd.mainMenuScreen == null || frontEnd.settingsDialog == null || frontEnd.creditsDialog == null)
                throw new InvalidOperationException("Required front-end screen/dialog is missing");
            menu = frontEnd.mainMenuScreen.transform;
            Button originalStart = MainButton(menu, 1);
            Button originalSettings = MainButton(menu, 4);
            Button originalCredits = MainButton(menu, 5);
            Button originalQuit = MainButton(menu, 6);
            Transform player = FindDeep(menu, "Player information panel");
            Transform save = FindDeep(menu, "SDG Save Progress v2 Tall UI");
            if (originalStart == null || originalSettings == null || originalCredits == null || originalQuit == null || player == null || save == null)
                throw new InvalidOperationException("Original buttons/profile/save panel not found; refusing partial migration");
            template = originalStart;
            playerPanel = player.gameObject;
            oldSavePanel = save.gameObject;
            profile = playerPanel.GetComponentInChildren<SDGFrontEndPlayerProfile>(true);
            if (profile == null) profile = menu.GetComponentInChildren<SDGFrontEndPlayerProfile>(true);
            if (profile == null) throw new InvalidOperationException("Player profile component missing");

            circle = FindDeep(menu, "Center HUD ring") != null ? FindDeep(menu, "Center HUD ring").gameObject : null;
            if (circle != null) circle.SetActive(false);
            playerPanel.SetActive(false);
            oldSavePanel.SetActive(false);
            Transform recent = FindDeep(menu, "Recent save panel");
            if (recent != null) recent.gameObject.SetActive(false);
            for (int i = 1; i <= 6; i++)
            {
                Button original = MainButton(menu, i);
                if (original != null) original.gameObject.SetActive(false);
            }

            startButton = SpawnMainButton(originalStart, "開始遊戲", new Vector2(0, 130), () => OpenStart());
            settingsButton = SpawnMainButton(originalSettings, "設定", new Vector2(0, 0), OpenSettings);
            quitButton = SpawnMainButton(originalQuit, "離開遊戲", new Vector2(0, -130), frontEnd.QuitGame);
            creditsButton = SpawnMainButton(originalCredits, "製作名單", Vector2.zero, () => { ClosePopups(false); frontEnd.OpenCredits(); });
            RectTransform cr = (RectTransform)creditsButton.transform;
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1, 0);
            cr.anchoredPosition = new Vector2(-34, 38);
            cr.sizeDelta = new Vector2(245, 76);

            shade = Rect(menu, "SDG R1.2 Legacy Dialog Dim", Vector2.zero, Vector2.zero).gameObject;
            RectTransform dimRect = (RectTransform)shade.transform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;
            Image shadeImage = shade.AddComponent<Image>();
            shadeImage.color = new Color(0f, .022f, .024f, .82f);
            shadeImage.raycastTarget = true;
            shade.SetActive(false);

            BuildStartPopup();
            BuildSavePopup();
            BuildPlayerPopup();
            BuildOwnerPopup();
            InstallSettingsPanel();
            AdjustCreditsLayout();
            installed = true;
            Debug.Log("[SDG Menu R1.2] UI built, Chinese labels mirrored, old center ring removed");
        }

        private Button SpawnMainButton(Button source, string caption, Vector2 position, Action action)
        {
            Button clone = Instantiate(source, menu);
            clone.name = "SDG R1.2 Main - " + caption;
            RectTransform rt = (RectTransform)clone.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(585, 108);
            rt.localScale = Vector3.one;
            clone.onClick = new Button.ButtonClickedEvent();
            clone.onClick.AddListener(() => action());
            TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = caption;
                label.fontSize = 35;
                RectTransform lrt = label.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(64, 6); lrt.offsetMax = new Vector2(-16, -6);
            }
            clone.gameObject.SetActive(true);
            return clone;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position; rt.sizeDelta = size;
            return rt;
        }

        private TMP_Text Label(Transform parent, string name, string caption, Vector2 position, Vector2 size, int fontSize)
        {
            // The existing buttons use a CJK-safe UnityEngine.UI.Text mirror.
            // Instantiate the WHOLE original label (including the mirror), not a bare TMP component.
            TMP_Text source = template.GetComponentInChildren<TMP_Text>(true);
            if (source == null) throw new InvalidOperationException("No original CJK text label available");
            GameObject labelObject = Instantiate(source.gameObject, parent, false);
            labelObject.name = name;
            RectTransform rt = (RectTransform)labelObject.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position; rt.sizeDelta = size;
            rt.localScale = Vector3.one;
            TextMeshProUGUI tmp = labelObject.GetComponent<TextMeshProUGUI>();
            if (tmp == null) throw new InvalidOperationException("Original TMP label not found after clone");
            tmp.text = caption;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(.92f, 1f, .98f, 1f);
            tmp.raycastTarget = false;
            var mirror = labelObject.GetComponentInChildren<SDGFrontEndCjkLegacyMirror>(true);
            if (mirror != null)
            {
                mirror.source = tmp;
                mirror.legacyText = mirror.GetComponent<Text>();
                if (mirror.legacyText != null)
                {
                    mirror.legacyText.raycastTarget = false;
                    mirror.legacyText.fontSize = fontSize;
                    mirror.legacyText.text = caption;
                    mirror.legacyText.color = tmp.color;
                }
            }
            else
            {
                // Do not generate a TMP atlas at runtime. Use a Windows CJK system font.
                RectTransform legacyRect = Rect(rt, "SDG R1.2 CJK Text", Vector2.zero, Vector2.zero);
                legacyRect.anchorMin = Vector2.zero; legacyRect.anchorMax = Vector2.one;
                legacyRect.offsetMin = legacyRect.offsetMax = Vector2.zero;
                Text legacy = legacyRect.gameObject.AddComponent<Text>();
                legacy.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Noto Sans CJK TC", "Microsoft YaHei" }, fontSize);
                legacy.fontSize = fontSize;
                legacy.text = caption;
                legacy.alignment = TextAnchor.MiddleCenter;
                legacy.color = tmp.color;
                legacy.raycastTarget = false;
                tmp.enabled = false;
            }
            return tmp;
        }

        private Button ActionButton(Transform parent, string caption, Vector2 pos, Vector2 size, Action action)
        {
            RectTransform rt = Rect(parent, "SDG R1.2 Action - " + caption, pos, size);
            Image background = rt.gameObject.AddComponent<Image>();
            background.color = new Color(.012f, .18f, .17f, .98f);
            Outline outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.21f, .87f, .69f, .8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => action());
            Label(rt, "Label", caption, Vector2.zero, size - new Vector2(25, 14), 29);
            return button;
        }

        private GameObject Overlay(string name, out RectTransform surface, Vector2 size)
        {
            RectTransform overlay = Rect(menu, "SDG R1.2 " + name, Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            Image dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, .018f, .02f, .88f);
            dim.raycastTarget = true;
            surface = Rect(overlay, "Dialog content", Vector2.zero, size);
            Image bg = surface.gameObject.AddComponent<Image>();
            bg.color = new Color(.015f, .1f, .095f, .98f);
            bg.raycastTarget = true;
            Outline edge = surface.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(.16f, .75f, .60f, .9f);
            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        private void BuildStartPopup()
        {
            RectTransform p;
            startPopup = Overlay("Start submenu", out p, new Vector2(720, 620));
            Label(p, "Heading", "開始遊戲", new Vector2(0, 235), new Vector2(630, 65), 39);
            ActionButton(p, "繼續遊戲", new Vector2(0, 120), new Vector2(560, 87), ContinueLatest);
            ActionButton(p, "選擇關卡", new Vector2(0, 6), new Vector2(560, 87), () => { startPopup.SetActive(false); frontEnd.OpenLevelSelection(); });
            ActionButton(p, "存檔／讀檔", new Vector2(0, -108), new Vector2(560, 87), OpenSave);
            startStatus = Label(p, "Status", "", new Vector2(0, -180), new Vector2(630, 46), 23);
            ActionButton(p, "返回主選單", new Vector2(0, -245), new Vector2(335, 66), () => startPopup.SetActive(false));
        }

        private void BuildSavePopup()
        {
            RectTransform p;
            savePopup = Overlay("Save and load", out p, new Vector2(960, 780));
            Label(p, "Heading", "存檔／讀檔", new Vector2(0, 308), new Vector2(850, 70), 38);
            Label(p, "Description", "請選擇目前玩家的存檔欄位", new Vector2(0, 248), new Vector2(830, 45), 25);
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                slotButtons[i] = ActionButton(p, "存檔欄位", new Vector2(0, 155 - i * 105), new Vector2(770, 87), () => SelectSlot(slot));
                slotTexts[i] = slotButtons[i].GetComponentInChildren<TMP_Text>(true);
            }
            saveStatus = Label(p, "Save status", "", new Vector2(0, -247), new Vector2(880, 60), 21);
            ActionButton(p, "讀取選定存檔", new Vector2(-283, -327), new Vector2(278, 70), LoadSlot);
            ActionButton(p, "刪除存檔", new Vector2(0, -327), new Vector2(248, 70), DeleteSlot);
            ActionButton(p, "返回", new Vector2(283, -327), new Vector2(248, 70), OpenStart);
        }

        private void BuildPlayerPopup()
        {
            playerPopup = Overlay("Create player", out playerSurface, new Vector2(920, 620));
            Label(playerSurface, "Heading", "新增玩家｜玩家資訊", new Vector2(0, 238), new Vector2(790, 65), 36);
            RectTransform pr = playerPanel.transform as RectTransform;
            if (pr != null)
            {
                pr.SetParent(playerSurface, false);
                pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(.5f, .5f);
                pr.anchoredPosition = new Vector2(0, 55);
                pr.localScale = new Vector3(1.25f, 1.25f, 1f);
            }
            playerStatus = Label(playerSurface, "Player status", "請輸入姓名並設定頭像", new Vector2(0, -133), new Vector2(805, 57), 24);
            ActionButton(playerSurface, "建立玩家", new Vector2(-200, -237), new Vector2(350, 78), ConfirmNewPlayer);
            ActionButton(playerSurface, "取消", new Vector2(200, -237), new Vector2(350, 78), CancelNewPlayer);
        }

        private void BuildOwnerPopup()
        {
            RectTransform p;
            ownerPopup = Overlay("Choose player", out p, new Vector2(780, 790));
            Label(p, "Heading", "選擇玩家", new Vector2(0, 308), new Vector2(670, 65), 38);
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                owners[i] = ActionButton(p, "玩家", new Vector2(0, 192 - i * 112), new Vector2(600, 92), () => ChoosePlayer(slot));
                ownerNames[i] = owners[i].GetComponentInChildren<TMP_Text>(true);
            }
            ActionButton(p, "上一頁", new Vector2(-216, -282), new Vector2(180, 65), () => { ownerPage = Mathf.Max(0, ownerPage - 1); RefreshOwners(); });
            ownerPageText = Label(p, "Page", "1 / 1", new Vector2(0, -282), new Vector2(160, 65), 25);
            ActionButton(p, "下一頁", new Vector2(216, -282), new Vector2(180, 65), () => { ownerPage++; RefreshOwners(); });
            ActionButton(p, "返回設定", new Vector2(0, -359), new Vector2(330, 58), CloseOwnerPopup);
        }

        private void InstallSettingsPanel()
        {
            RectTransform settingsRect = frontEnd.settingsDialog.transform as RectTransform;
            if (settingsRect == null) throw new InvalidOperationException("Settings rect missing");
            settingsRect.anchorMin = settingsRect.anchorMax = settingsRect.pivot = new Vector2(.5f, .5f);
            settingsRect.anchoredPosition = Vector2.zero;
            settingsRect.sizeDelta = new Vector2(980, 920);
            Transform settingsRoot = FindDeep(frontEnd.settingsDialog.transform, "SDG Settings V1 Root");
            if (settingsRoot != null)
            {
                RectTransform srt = settingsRoot as RectTransform;
                if (srt != null) srt.anchoredPosition = new Vector2(0, 164);
                for (int i = 1; i <= 4; i++)
                {
                    Transform tab = FindDeep(settingsRoot, "Tab " + i);
                    if (tab is RectTransform tabRect) tabRect.sizeDelta = new Vector2(166, 60);
                }
                foreach (string footerName in new[] { "Restore defaults", "Back", "Apply" })
                {
                    Transform footer = FindDeep(settingsRoot, footerName);
                    if (footer is RectTransform footerRect) footerRect.sizeDelta = new Vector2(190, 66);
                }
                SDGGameSettingsPanelV1 component = settingsRoot.GetComponent<SDGGameSettingsPanelV1>();
                if (component != null && component.backButton != null)
                    component.backButton.onClick.AddListener(frontEnd.CloseDialogs);
                Transform oldClose = frontEnd.settingsDialog.transform.Find("Close settings");
                if (oldClose != null) oldClose.gameObject.SetActive(false);
            }
            RectTransform summary = Rect(frontEnd.settingsDialog.transform, "SDG R1.2 Player info in Settings", new Vector2(0, -225), new Vector2(800, 200));
            Image bg = summary.gameObject.AddComponent<Image>();
            bg.color = new Color(.018f, .15f, .135f, .97f);
            bg.raycastTarget = false;
            Outline border = summary.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(.19f, .77f, .64f, .78f);
            Label(summary, "Header", "目前玩家資訊", new Vector2(0, 67), new Vector2(670, 52), 30);
            RectTransform avatarRect = Rect(summary, "Player avatar", new Vector2(-320, 0), new Vector2(118, 118));
            playerSummaryAvatar = avatarRect.gameObject.AddComponent<Image>();
            playerSummaryAvatar.preserveAspect = true;
            playerSummaryAvatar.raycastTarget = false;
            playerSummaryAvatar.color = Color.white;
            playerSummaryName = Label(summary, "Name", "玩家", new Vector2(60, 12), new Vector2(600, 54), 27);
            playerSummaryStats = Label(summary, "Stats", "", new Vector2(60, -52), new Vector2(615, 54), 22);
            ActionButton(frontEnd.settingsDialog.transform, "新增玩家", new Vector2(-226, -384), new Vector2(395, 82), OpenNewPlayer);
            ActionButton(frontEnd.settingsDialog.transform, "切換玩家", new Vector2(226, -384), new Vector2(395, 82), OpenOwnerPopup);
        }

        private void AdjustCreditsLayout()
        {
            RectTransform c = frontEnd.creditsDialog.transform as RectTransform;
            if (c == null) return;
            c.anchorMin = c.anchorMax = c.pivot = new Vector2(.5f, .5f);
            c.anchoredPosition = Vector2.zero;
            c.sizeDelta = new Vector2(1020, 750);
            Transform heading = FindDeep(c, "Heading");
            if (heading is RectTransform h)
            {
                h.anchoredPosition = new Vector2(0, 285);
                h.sizeDelta = new Vector2(890, 70);
            }
            Transform content = FindDeep(c, "Credits placeholder");
            if (content is RectTransform body)
            {
                body.anchoredPosition = new Vector2(0, 10);
                body.sizeDelta = new Vector2(900, 460);
                TMP_Text tmp = body.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.alignment = TextAlignmentOptions.Left;
                    tmp.fontSize = 27;
                    tmp.enableWordWrapping = true;
                }
            }
            Transform close = FindDeep(c, "Close credits");
            if (close is RectTransform button)
            {
                button.anchoredPosition = new Vector2(0, -308);
                button.sizeDelta = new Vector2(440, 80);
            }
        }

        private void RefreshSettingsInfo()
        {
            if (playerSummaryName == null || playerSummaryStats == null) return;
            SDGSaveOwnerV2 owner = SDGSaveProfilesV2.Active;
            string name = owner != null ? owner.name : PlayerPrefs.GetString("SDG_FE_PlayerName", "回收探險者");
            int hp = PlayerPrefs.GetInt("SDG_FE_PlayerHealth", owner != null ? owner.health : 100);
            int maxHp = Mathf.Max(1, PlayerPrefs.GetInt("SDG_FE_PlayerHealthMax", owner != null ? owner.healthMax : 100));
            int xp = PlayerPrefs.GetInt("SDG_FE_Experience", owner != null ? owner.xp : 0);
            playerSummaryName.text = "玩家：「" + name + "」";
            playerSummaryStats.text = "生命 " + hp + " / " + maxHp + "   ｜   經驗 " + xp + "   ｜   已完成 " + SDGSaveProgressRuntimeV1.CompletedCount + " 關";
            if (playerSummaryAvatar != null && profile != null)
            {
                Sprite portrait = profile.avatarImage != null ? profile.avatarImage.sprite : null;
                string avatarKey = owner != null ? owner.avatar : PlayerPrefs.GetString("SDG_FE_SelectedAvatar", "builtin:0");
                if (!string.IsNullOrEmpty(avatarKey) && avatarKey.StartsWith("builtin:", StringComparison.Ordinal) && profile.builtInAvatars != null)
                {
                    if (int.TryParse(avatarKey.Substring(8), out int index) && index >= 0 && index < profile.builtInAvatars.Length)
                        portrait = profile.builtInAvatars[index];
                }
                if (playerSummaryAvatar.sprite != portrait) playerSummaryAvatar.sprite = portrait;
            }
        }

        private void ContinueLatest()
        {
            int newest = SDGSaveProgressRuntimeV1.NewestSlot();
            if (newest < 0)
            {
                if (startStatus != null) startStatus.text = "目前玩家尚無有效存檔；請選擇關卡開始";
                return;
            }
            if (!SDGSaveProgressRuntimeV1.TryLoad(newest, out string result) && startStatus != null)
                startStatus.text = result;
        }

        private void SelectSlot(int slot)
        {
            selectedSaveSlot = Mathf.Clamp(slot, 0, 3);
            deleteConfirmSlot = -1;
            SDGSaveProgressRuntimeV1.SelectedSlot = selectedSaveSlot;
            RefreshSaveSlots();
        }

        private void RefreshSaveSlots()
        {
            for (int i = 0; i < slotTexts.Length; i++)
            {
                if (slotTexts[i] == null) continue;
                bool valid = SDGSaveProgressRuntimeV1.TryReadSlot(i, out SDGProgressRecordV1 record);
                string label = i == 0 ? "自動存檔" : "手動存檔 " + i;
                string text = valid ? label + "   ｜   第 " + record.levelNumber + " 關   ｜   HP " + record.health + "/" + record.maxHealth
                    : label + "   ｜   （空白）";
                slotTexts[i].text = (i == selectedSaveSlot ? "▶ " : "   ") + text;
                Image bg = slotButtons[i] != null ? slotButtons[i].GetComponent<Image>() : null;
                if (bg != null) bg.color = i == selectedSaveSlot ? new Color(.05f, .39f, .29f, .98f) : new Color(.016f, .18f, .17f, .98f);
            }
            if (saveStatus != null) saveStatus.text = "主選單只提供讀取；請在正式關卡內使用 F5 手動存檔";
        }

        private void LoadSlot()
        {
            if (!SDGSaveProgressRuntimeV1.TryLoad(selectedSaveSlot, out string result) && saveStatus != null)
                saveStatus.text = result;
        }

        private void DeleteSlot()
        {
            if (deleteConfirmSlot != selectedSaveSlot || Time.unscaledTime > deleteConfirmUntil)
            {
                deleteConfirmSlot = selectedSaveSlot;
                deleteConfirmUntil = Time.unscaledTime + 6f;
                if (saveStatus != null) saveStatus.text = "再次點擊『刪除存檔』才會移到備份資料夾";
                return;
            }
            SDGSaveProfilesV2.DeleteSlot(selectedSaveSlot, out string result);
            deleteConfirmSlot = -1;
            RefreshSaveSlots();
            if (saveStatus != null) saveStatus.text = result;
        }

        private void OpenStart()
        {
            ClosePopups(false);
            frontEnd.CloseDialogs();
            startPopup.SetActive(true);
            startPopup.transform.SetAsLastSibling();
            if (startStatus != null) startStatus.text = "";
        }

        private void OpenSave()
        {
            startPopup.SetActive(false);
            savePopup.SetActive(true);
            savePopup.transform.SetAsLastSibling();
            deleteConfirmSlot = -1;
            selectedSaveSlot = Mathf.Clamp(SDGSaveProgressRuntimeV1.SelectedSlot, 0, 3);
            RefreshSaveSlots();
        }

        private void OpenSettings()
        {
            ClosePopups(false);
            frontEnd.OpenSettings();
            Transform root = FindDeep(frontEnd.settingsDialog.transform, "SDG Settings V1 Root");
            if (root != null && !root.gameObject.activeSelf) root.gameObject.SetActive(true);
            RefreshSettingsInfo();
        }

        private void OpenNewPlayer()
        {
            originalName = PlayerPrefs.GetString("SDG_FE_PlayerName", "回收探險者");
            originalAvatar = PlayerPrefs.GetString("SDG_FE_SelectedAvatar", "builtin:0");
            originalOwner = SDGSaveProfilesV2.ActiveId;
            SDGSaveOwnerV2 owner = SDGSaveProfilesV2.Active;
            originalSeconds = owner != null ? owner.totalPlaySeconds : 0;
            originalCompleted = owner != null ? owner.completedMask : 0;
            frontEnd.settingsDialog.SetActive(false);
            playerPopup.SetActive(true);
            playerPopup.transform.SetAsLastSibling();
            playerPanel.SetActive(true);
            if (profile.playerNameInput != null) profile.playerNameInput.SetTextWithoutNotify("新玩家");
            if (playerStatus != null) playerStatus.text = "請輸入姓名並選擇頭像，再按建立玩家";
        }

        private void RestoreOldIdentity()
        {
            PlayerPrefs.SetString("SDG_FE_PlayerName", originalName ?? "回收探險者");
            PlayerPrefs.SetString("SDG_FE_SelectedAvatar", originalAvatar ?? "builtin:0");
            PlayerPrefs.Save();
            if (!string.IsNullOrEmpty(originalOwner) && SDGSaveProfilesV2.ActiveId == originalOwner)
                SDGSaveProfilesV2.CaptureActive(originalSeconds, originalCompleted);
            if (profile != null && profile.playerNameInput != null)
                profile.playerNameInput.SetTextWithoutNotify(originalName ?? "回收探險者");
        }

        private void ConfirmNewPlayer()
        {
            string name = profile.playerNameInput != null ? profile.playerNameInput.text.Trim() : "新玩家";
            if (string.IsNullOrEmpty(name)) { playerStatus.text = "請輸入玩家名稱"; return; }
            if (name.Length > 20) name = name.Substring(0, 20);
            string avatar = PlayerPrefs.GetString("SDG_FE_SelectedAvatar", originalAvatar ?? "builtin:0");
            try
            {
                PlayerPrefs.SetString("SDG_FE_PlayerName", name);
                PlayerPrefs.SetString("SDG_FE_SelectedAvatar", avatar);
                SDGSaveOwnerV2 created = SDGSaveProfilesV2.CreateFromCurrentPlayer();
                RestoreOldIdentity();
                if (created == null) { playerStatus.text = "最多只能建立 16 位玩家"; return; }
                if (!SDGSaveProgressRuntimeV1.SwitchOwner(created.id)) SDGSaveProfilesV2.SetActive(created.id);
                playerPanel.SetActive(false);
                playerPopup.SetActive(false);
                frontEnd.RefreshUI();
                frontEnd.settingsDialog.SetActive(true);
                RefreshSettingsInfo();
            }
            catch (Exception e)
            {
                RestoreOldIdentity();
                playerStatus.text = "建立失敗：" + e.Message;
                Debug.LogException(e);
            }
        }

        private void CancelNewPlayer()
        {
            RestoreOldIdentity();
            playerPanel.SetActive(false);
            playerPopup.SetActive(false);
            if (frontEnd != null && frontEnd.mainMenuScreen != null && frontEnd.mainMenuScreen.activeSelf)
            {
                frontEnd.settingsDialog.SetActive(true);
                RefreshSettingsInfo();
            }
        }

        private void OpenOwnerPopup()
        {
            ownerPage = 0;
            RefreshOwners();
            frontEnd.settingsDialog.SetActive(false);
            ownerPopup.SetActive(true);
            ownerPopup.transform.SetAsLastSibling();
        }

        private void RefreshOwners()
        {
            List<SDGSaveOwnerV2> list = SDGSaveProfilesV2.Owners;
            int pages = Mathf.Max(1, (list.Count + 3) / 4);
            ownerPage = Mathf.Clamp(ownerPage, 0, pages - 1);
            if (ownerPageText != null) ownerPageText.text = (ownerPage + 1) + " / " + pages;
            for (int i = 0; i < 4; i++)
            {
                int index = ownerPage * 4 + i;
                bool valid = index < list.Count;
                owners[i].gameObject.SetActive(valid);
                if (valid && ownerNames[i] != null)
                    ownerNames[i].text = (list[index].id == SDGSaveProfilesV2.ActiveId ? "▶ " : "") + list[index].name;
            }
        }

        private void ChoosePlayer(int slot)
        {
            List<SDGSaveOwnerV2> list = SDGSaveProfilesV2.Owners;
            int index = ownerPage * 4 + slot;
            if (index < 0 || index >= list.Count) return;
            if (!SDGSaveProgressRuntimeV1.SwitchOwner(list[index].id) && !SDGSaveProfilesV2.SetActive(list[index].id))
            {
                frontEnd.ShowStatus("切換玩家失敗");
                return;
            }
            frontEnd.RefreshUI();
            CloseOwnerPopup();
        }

        private void CloseOwnerPopup()
        {
            ownerPopup.SetActive(false);
            frontEnd.settingsDialog.SetActive(true);
            RefreshSettingsInfo();
        }

        private void ClosePopups(bool restoreSettings)
        {
            if (startPopup != null) startPopup.SetActive(false);
            if (savePopup != null) savePopup.SetActive(false);
            if (playerPopup != null && playerPopup.activeSelf)
            {
                if (restoreSettings) CancelNewPlayer();
                else { RestoreOldIdentity(); playerPanel.SetActive(false); playerPopup.SetActive(false); }
            }
            if (ownerPopup != null) ownerPopup.SetActive(false);
        }

        private static bool Esc()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
            return false;
        }

        private void LateUpdate()
        {
            if (!installed || frontEnd == null || frontEnd.mainMenuScreen == null) return;
            if (circle != null && circle.activeSelf) circle.SetActive(false);
            // Original controller handles Escape before this component. Restore main menu
            // if a custom dialog was interrupted by the legacy Escape handler.
            if (!frontEnd.mainMenuScreen.activeSelf)
            {
                // Base controller also handles Escape. If it switched to the start screen
                // while our modal was visible, restore the menu instead of losing modal state.
                if (Esc() && (startPopup.activeSelf || savePopup.activeSelf || playerPopup.activeSelf || ownerPopup.activeSelf))
                {
                    bool returnSettings = playerPopup.activeSelf || ownerPopup.activeSelf;
                    frontEnd.ShowMainMenu();
                    ClosePopups(false);
                    if (returnSettings) OpenSettings();
                }
                else if (startPopup.activeSelf || savePopup.activeSelf || playerPopup.activeSelf || ownerPopup.activeSelf)
                    ClosePopups(false);
                return;
            }
            if (Esc())
            {
                if (playerPopup.activeSelf) CancelNewPlayer();
                else if (ownerPopup.activeSelf) CloseOwnerPopup();
                else if (savePopup.activeSelf) OpenStart();
                else if (startPopup.activeSelf) startPopup.SetActive(false);
            }
            bool settings = frontEnd.settingsDialog.activeSelf;
            bool credits = frontEnd.creditsDialog.activeSelf;
            bool levels = frontEnd.levelsDialog != null && frontEnd.levelsDialog.activeSelf;
            bool custom = startPopup.activeSelf || savePopup.activeSelf || playerPopup.activeSelf || ownerPopup.activeSelf;
            bool normalMenu = !settings && !credits && !levels && !custom;
            if (startButton != null && startButton.gameObject.activeSelf != normalMenu) startButton.gameObject.SetActive(normalMenu);
            if (settingsButton != null && settingsButton.gameObject.activeSelf != normalMenu) settingsButton.gameObject.SetActive(normalMenu);
            if (quitButton != null && quitButton.gameObject.activeSelf != normalMenu) quitButton.gameObject.SetActive(normalMenu);
            if (creditsButton != null && creditsButton.gameObject.activeSelf != normalMenu) creditsButton.gameObject.SetActive(normalMenu);
            bool showShade = settings || credits || levels;
            if (shade != null && shade.activeSelf != showShade) shade.SetActive(showShade);
            if (showShade)
            {
                // Ensure legacy modal draws above the dim background and all main menu controls.
                shade.transform.SetAsLastSibling();
                GameObject active = settings ? frontEnd.settingsDialog : credits ? frontEnd.creditsDialog : frontEnd.levelsDialog;
                if (active != null) active.transform.SetAsLastSibling();
            }
            if (settings) RefreshSettingsInfo();
        }
    }
}
