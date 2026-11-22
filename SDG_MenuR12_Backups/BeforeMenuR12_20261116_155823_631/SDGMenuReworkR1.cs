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
    /// Additive layout controller. Preserves all original menu buttons, game scripts,
    /// player catalog, save files and scene routes. Only the four-layer navigation
    /// is shown differently; it never creates a save point from the main menu.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class SDGMenuReworkR1 : MonoBehaviour
    {
        public SDGFrontEndController frontEnd;
        private Transform menu;
        private Button template;
        private GameObject startPopup, savePopup, playerPopup, ownerPopup;
        private GameObject playerPanel, savePanel;
        private Button[] owners = new Button[4];
        private TMP_Text[] ownerNames = new TMP_Text[4];
        private TMP_Text playerStatus, ownerPageText;
        private SDGFrontEndPlayerProfile profile;
        private SDGSaveProgressMenuV2 saveUi;
        private string originalName, originalAvatar, originalOwner;
        private int originalSeconds, originalCompleted;
        private int ownerPage;
        private bool installed;

        private void Start()
        {
            try { Install(); }
            catch (Exception e) { Debug.LogError("[SDG Menu R1] Navigation setup stopped: " + e); }
        }

        private void Update()
        {
            // Prevent draft profile edits from leaking into the active owner if the
            // menu is hidden externally (for example, the old Escape handler).
            if (playerPopup != null && playerPopup.activeSelf &&
                frontEnd != null && frontEnd.mainMenuScreen != null &&
                !frontEnd.mainMenuScreen.activeSelf)
                CancelNewPlayer();
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
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
            if (frontEnd == null || frontEnd.mainMenuScreen == null)
                throw new InvalidOperationException("Front-end controller or main menu was not found");
            menu = frontEnd.mainMenuScreen.transform;
            Button start = MainButton(menu, 1), settings = MainButton(menu, 4), quit = MainButton(menu, 6), credits = MainButton(menu, 5);
            if (start == null || settings == null || quit == null || credits == null)
                throw new InvalidOperationException("One or more of the six original menu buttons are missing");
            template = start;
            // Locate the complete profile and the real v2 save view BEFORE modifying visibility.
            Transform player = FindDeep(menu, "Player information panel");
            Transform save = FindDeep(menu, "SDG Save Progress v2 Tall UI");
            if (player == null || save == null)
                throw new InvalidOperationException("Player-information or v2 save UI missing; old UI left unchanged");
            playerPanel = player.gameObject;
            savePanel = save.gameObject;
            profile = playerPanel.GetComponentInChildren<SDGFrontEndPlayerProfile>(true);
            if (profile == null) profile = menu.GetComponentInChildren<SDGFrontEndPlayerProfile>(true);
            saveUi = savePanel.GetComponent<SDGSaveProgressMenuV2>();
            if (saveUi == null) saveUi = savePanel.GetComponentInChildren<SDGSaveProgressMenuV2>(true);
            if (profile == null || saveUi == null)
                throw new InvalidOperationException("Original profile or save component missing; old UI left unchanged");

            // Hide the visible left-side profile and v2 save panel. Never Destroy them.
            playerPanel.SetActive(false);
            savePanel.SetActive(false);
            Transform recent = FindDeep(menu, "Recent save panel");
            if (recent != null) recent.gameObject.SetActive(false);
            for (int i = 1; i <= 6; i++)
            {
                Button button = MainButton(menu, i);
                if (button != null) button.gameObject.SetActive(false);
            }
            SpawnMainButton(start, "開始遊戲", new Vector2(0, 125), () => OpenStart());
            SpawnMainButton(settings, "設定", new Vector2(0, 15), () => OpenSettings());
            SpawnMainButton(quit, "離開遊戲", new Vector2(0, -95), () => frontEnd.QuitGame());
            Button creditButton = SpawnMainButton(credits, "製作名單", Vector2.zero, () => frontEnd.OpenCredits());
            RectTransform creditRect = (RectTransform)creditButton.transform;
            creditRect.anchorMin = creditRect.anchorMax = creditRect.pivot = new Vector2(1, 0);
            creditRect.anchoredPosition = new Vector2(-32, 24);
            creditRect.sizeDelta = new Vector2(240, 58);
            creditRect.SetAsLastSibling();

            BuildStartPopup();
            BuildSavePopup();
            BuildPlayerPopup();
            BuildOwnerPopup();
            InstallSettingsButtons();
            if (saveUi.newOwnerButton != null) saveUi.newOwnerButton.gameObject.SetActive(false);
            installed = true;
            Debug.Log("[SDG Menu R1] Main menu reorganization activated, existing save logic preserved");
        }

        private Button SpawnMainButton(Button source, string text, Vector2 position, Action action)
        {
            Button clone = Instantiate(source, menu);
            clone.name = "SDG R1 - " + text;
            RectTransform rt = (RectTransform)clone.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(500, 77);
            clone.onClick = new Button.ButtonClickedEvent(); // No inherited legacy action.
            clone.onClick.AddListener(() => action());
            TextMeshProUGUI label = clone.transform.Find("Label") != null
                ? clone.transform.Find("Label").GetComponent<TextMeshProUGUI>() : null;
            if (label != null) label.text = text;
            clone.gameObject.SetActive(true);
            return clone;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private TMP_Text Label(Transform parent, string name, string caption, Vector2 position, Vector2 size, int fontSize)
        {
            RectTransform rt = Rect(parent, name, position, size);
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_Text example = template.GetComponentInChildren<TMP_Text>(true);
            if (example != null) text.font = example.font;
            text.text = caption;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(.84f, 1f, .96f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private Button ActionButton(Transform parent, string caption, Vector2 position, Vector2 size, Action callback)
        {
            RectTransform rt = Rect(parent, "SDG R1 Action - " + caption, position, size);
            Image background = rt.gameObject.AddComponent<Image>();
            background.color = new Color(.016f, .22f, .19f, .95f);
            Outline outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.20f, .85f, .70f, .85f);
            outline.effectDistance = new Vector2(1.6f, -1.6f);
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => callback());
            Label(rt, "Text", caption, Vector2.zero, size - new Vector2(12, 5), 23);
            return button;
        }

        private GameObject Overlay(string name, out RectTransform surface, Vector2 size)
        {
            RectTransform overlay = Rect(menu, "SDG R1 " + name, Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            Image dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0, .025f, .027f, .88f);
            dim.raycastTarget = true;
            surface = Rect(overlay, "Dialog content", Vector2.zero, size);
            Image panel = surface.gameObject.AddComponent<Image>();
            panel.color = new Color(.016f, .12f, .115f, .96f);
            panel.raycastTarget = true;
            Outline edge = surface.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(.19f, .87f, .68f, .92f);
            edge.effectDistance = new Vector2(2f, -2f);
            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        private void BuildStartPopup()
        {
            RectTransform panel;
            startPopup = Overlay("Start submenu", out panel, new Vector2(600, 540));
            Label(panel, "Heading", "開始遊戲", new Vector2(0, 211), new Vector2(510, 58), 34);
            ActionButton(panel, "繼續遊戲", new Vector2(0, 113), new Vector2(440, 67), ContinueFromLatest);
            ActionButton(panel, "選擇關卡", new Vector2(0, 28), new Vector2(440, 67), () => { startPopup.SetActive(false); frontEnd.OpenLevelSelection(); });
            ActionButton(panel, "存檔／讀檔", new Vector2(0, -57), new Vector2(440, 67), OpenSave);
            ActionButton(panel, "返回", new Vector2(0, -189), new Vector2(260, 56), () => startPopup.SetActive(false));
        }

        private void BuildSavePopup()
        {
            RectTransform panel;
            savePopup = Overlay("Save and load", out panel, new Vector2(750, 580));
            Label(panel, "Heading", "存檔／讀檔", new Vector2(0, 239), new Vector2(650, 48), 29);
            RectTransform rt = savePanel.transform as RectTransform;
            if (rt != null)
            {
                rt.SetParent(panel, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
                rt.anchoredPosition = new Vector2(0, 5);
                rt.SetAsLastSibling();
            }
            ActionButton(panel, "關閉存檔視窗", new Vector2(0, -245), new Vector2(290, 49), () => { savePopup.SetActive(false); OpenStart(); });
        }

        private void BuildPlayerPopup()
        {
            RectTransform panel;
            playerPopup = Overlay("Create player", out panel, new Vector2(720, 500));
            Label(panel, "Heading", "新增玩家｜玩家資訊", new Vector2(0, 208), new Vector2(650, 55), 32);
            RectTransform rt = playerPanel.transform as RectTransform;
            if (rt != null)
            {
                rt.SetParent(panel, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
                rt.anchoredPosition = new Vector2(0, 38);
            }
            playerStatus = Label(panel, "New player status", "請設定姓名與頭像，再按建立玩家", new Vector2(0, -108), new Vector2(650, 50), 21);
            ActionButton(panel, "建立玩家", new Vector2(-174, -196), new Vector2(268, 54), ConfirmNewPlayer);
            ActionButton(panel, "取消", new Vector2(174, -196), new Vector2(268, 54), CancelNewPlayer);
        }

        private void BuildOwnerPopup()
        {
            RectTransform panel;
            ownerPopup = Overlay("Switch player", out panel, new Vector2(640, 640));
            Label(panel, "Heading", "選擇玩家", new Vector2(0, 252), new Vector2(520, 60), 31);
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                owners[i] = ActionButton(panel, "玩家", new Vector2(0, 145 - i * 91), new Vector2(490, 65), () => ChoosePlayer(slot));
                ownerNames[i] = owners[i].GetComponentInChildren<TMP_Text>();
            }
            ActionButton(panel, "上一頁", new Vector2(-167, -236), new Vector2(170, 49), () => { ownerPage = Mathf.Max(0, ownerPage - 1); RefreshOwners(); });
            ownerPageText = Label(panel, "Page", "1 / 1", new Vector2(0, -236), new Vector2(132, 49), 21);
            ActionButton(panel, "下一頁", new Vector2(167, -236), new Vector2(170, 49), () => { ownerPage++; RefreshOwners(); });
            ActionButton(panel, "返回設定", new Vector2(0, -291), new Vector2(220, 44), () => ownerPopup.SetActive(false));
        }

        private void InstallSettingsButtons()
        {
            // These buttons are children of the actual settings dialog, not the main menu.
            if (frontEnd.settingsDialog == null) throw new InvalidOperationException("Settings dialog missing");
            Transform settings = frontEnd.settingsDialog.transform;
            ActionButton(settings, "新增玩家", new Vector2(-205, -292), new Vector2(250, 57), OpenNewPlayer);
            ActionButton(settings, "選擇玩家", new Vector2(205, -292), new Vector2(250, 57), () => { ownerPage = 0; RefreshOwners(); ownerPopup.SetActive(true); ownerPopup.transform.SetAsLastSibling(); });
        }

        private void OpenSettings()
        {
            CloseR1Popups();
            frontEnd.OpenSettings();
        }

        private void OpenStart()
        {
            CloseR1Popups();
            frontEnd.CloseDialogs();
            startPopup.SetActive(true);
            startPopup.transform.SetAsLastSibling();
        }

        private void OpenSave()
        {
            startPopup.SetActive(false);
            savePopup.SetActive(true);
            savePopup.transform.SetAsLastSibling();
            savePanel.SetActive(true);
        }

        private void ContinueFromLatest()
        {
            if (SDGSaveProgressRuntimeV1.NewestSlot() < 0)
            {
                frontEnd.ShowStatus("目前玩家尚無可繼續的存檔");
                return;
            }
            saveUi.ContinueNewest();
        }

        private void OpenNewPlayer()
        {
            originalName = PlayerPrefs.GetString("SDG_FE_PlayerName", "回收探險者");
            originalAvatar = PlayerPrefs.GetString("SDG_FE_SelectedAvatar", "builtin:0");
            originalOwner = SDGSaveProfilesV2.ActiveId;
            SDGSaveOwnerV2 oldOwner = SDGSaveProfilesV2.Active;
            originalSeconds = oldOwner != null ? oldOwner.totalPlaySeconds : 0;
            originalCompleted = oldOwner != null ? oldOwner.completedMask : 0;
            playerPopup.SetActive(true);
            playerPopup.transform.SetAsLastSibling();
            playerPanel.SetActive(true);
            if (profile.playerNameInput != null) profile.playerNameInput.SetTextWithoutNotify("新玩家");
            if (playerStatus != null) playerStatus.text = "請設定姓名與頭像，再按建立玩家";
        }

        private void RestoreOldIdentity()
        {
            PlayerPrefs.SetString("SDG_FE_PlayerName", originalName ?? "回收探險者");
            PlayerPrefs.SetString("SDG_FE_SelectedAvatar", originalAvatar ?? "builtin:0");
            PlayerPrefs.Save();
            if (!string.IsNullOrEmpty(originalOwner) && SDGSaveProfilesV2.ActiveId == originalOwner)
                SDGSaveProfilesV2.CaptureActive(originalSeconds, originalCompleted);
            if (profile.playerNameInput != null)
                profile.playerNameInput.SetTextWithoutNotify(originalName ?? "回收探險者");
        }

        private void ConfirmNewPlayer()
        {
            string newName = profile.playerNameInput == null ? "新玩家" : profile.playerNameInput.text.Trim();
            if (string.IsNullOrEmpty(newName)) { if (playerStatus != null) playerStatus.text = "請輸入玩家名稱"; return; }
            if (newName.Length > 20) newName = newName.Substring(0, 20);
            string avatar = PlayerPrefs.GetString("SDG_FE_SelectedAvatar", originalAvatar ?? "builtin:0");
            try
            {
                // Create the separate owner record, but do NOT allow a draft name to be
                // captured into the previous owner's profile when SwitchOwner persists it.
                PlayerPrefs.SetString("SDG_FE_PlayerName", newName);
                PlayerPrefs.SetString("SDG_FE_SelectedAvatar", avatar);
                SDGSaveOwnerV2 created = SDGSaveProfilesV2.CreateFromCurrentPlayer();
                RestoreOldIdentity();
                if (created == null)
                {
                    if (playerStatus != null) playerStatus.text = "最多只能建立 16 位玩家";
                    return;
                }
                if (!SDGSaveProgressRuntimeV1.SwitchOwner(created.id)) SDGSaveProfilesV2.SetActive(created.id);
                playerPopup.SetActive(false);
                frontEnd.RefreshUI();
                frontEnd.ShowStatus("新玩家「" + created.name + "」已建立；存檔分開管理");
            }
            catch (Exception ex)
            {
                RestoreOldIdentity();
                if (playerStatus != null) playerStatus.text = "建立失敗：" + ex.Message;
                Debug.LogException(ex);
            }
        }

        private void CancelNewPlayer()
        {
            RestoreOldIdentity();
            playerPopup.SetActive(false);
        }

        private void RefreshOwners()
        {
            List<SDGSaveOwnerV2> list = SDGSaveProfilesV2.Owners;
            int pageCount = Mathf.Max(1, (list.Count + 3) / 4);
            ownerPage = Mathf.Clamp(ownerPage, 0, pageCount - 1);
            if (ownerPageText != null) ownerPageText.text = (ownerPage + 1) + " / " + pageCount;
            for (int i = 0; i < owners.Length; i++)
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
            bool ok = SDGSaveProgressRuntimeV1.SwitchOwner(list[index].id);
            if (!ok) ok = SDGSaveProfilesV2.SetActive(list[index].id);
            if (!ok) { frontEnd.ShowStatus("切換玩家失敗"); return; }
            ownerPopup.SetActive(false);
            frontEnd.RefreshUI();
            frontEnd.ShowStatus("目前玩家：" + list[index].name);
        }

        private void CloseR1Popups()
        {
            if (startPopup != null) startPopup.SetActive(false);
            if (savePopup != null) savePopup.SetActive(false);
            if (playerPopup != null && playerPopup.activeSelf) CancelNewPlayer();
            if (ownerPopup != null) ownerPopup.SetActive(false);
            if (savePanel != null) savePanel.SetActive(false);
        }
    }
}
