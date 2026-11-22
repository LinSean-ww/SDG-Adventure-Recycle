using System;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd
{
    [DisallowMultipleComponent]
    public sealed class SDGSaveProgressMenuV2 : MonoBehaviour
    {
        public Button avatarButton;
        public Image avatarImage;
        public TextMeshProUGUI ownerName;
        public TextMeshProUGUI ownerProgress;
        public TextMeshProUGUI ownerPlaytime;
        public TextMeshProUGUI statusText;
        public Button newOwnerButton;
        public GameObject ownerPicker;
        public Button[] ownerButtons;
        public TextMeshProUGUI[] ownerButtonLabels;
        public Button ownerPrev;
        public Button ownerNext;
        public TextMeshProUGUI pickerPage;
        public Button[] slotButtons;
        public Image[] slotBackgrounds;
        public TextMeshProUGUI[] slotTitleLabels;
        public TextMeshProUGUI[] slotDetailLabels;
        public Button loadButton;
        public Button saveButton;
        public Button deleteButton;
        public TextMeshProUGUI autoSaveLabel;
        public Button autoSaveButton;
        public Button continueButton;
        public SDGFrontEndPlayerProfile originalProfile;
        private int selectedSlot = 1;
        private int ownerPage;
        private float nextRefresh;
        private float deleteConfirmUntil;
        private int confirmDeleteSlot = -1;
        private string message = "選擇右側的存檔紀錄";
        private Sprite customSprite;
        private Texture2D customTexture;
        private string loadedAvatarKey;
        private readonly Color selected = new Color(0.02f, 0.39f, 0.33f, 0.94f);
        private readonly Color ordinary = new Color(0.02f, 0.16f, 0.16f, 0.94f);

        private void Awake()
        {
            SDGSaveProfilesV2.Ensure();
            selectedSlot = Mathf.Clamp(SDGSaveProgressRuntimeV1.SelectedSlot, 0, 3);
            if (selectedSlot < 0) selectedSlot = 1;
            if (avatarButton != null) avatarButton.onClick.AddListener(TogglePicker);
            if (newOwnerButton != null) newOwnerButton.onClick.AddListener(CreateOwner);
            if (ownerPrev != null) ownerPrev.onClick.AddListener(PreviousOwners);
            if (ownerNext != null) ownerNext.onClick.AddListener(NextOwners);
            for (int i = 0; ownerButtons != null && i < ownerButtons.Length; i++)
            {
                int buttonIndex = i;
                if (ownerButtons[i] != null) ownerButtons[i].onClick.AddListener(() => SelectOwner(buttonIndex));
            }
            for (int i = 0; slotButtons != null && i < slotButtons.Length; i++)
            {
                int slot = i;
                if (slotButtons[i] != null) slotButtons[i].onClick.AddListener(() => ChooseSlot(slot));
            }
            if (loadButton != null) loadButton.onClick.AddListener(LoadSelected);
            if (saveButton != null) saveButton.onClick.AddListener(SaveSelected);
            if (deleteButton != null) deleteButton.onClick.AddListener(DeleteSelected);
            if (autoSaveButton != null) autoSaveButton.onClick.AddListener(ToggleAutoSave);
            if (ownerPicker != null) ownerPicker.SetActive(false);
            Refresh();
        }

        private void OnDestroy()
        {
            if (avatarButton != null) avatarButton.onClick.RemoveListener(TogglePicker);
            if (newOwnerButton != null) newOwnerButton.onClick.RemoveListener(CreateOwner);
            if (ownerPrev != null) ownerPrev.onClick.RemoveListener(PreviousOwners);
            if (ownerNext != null) ownerNext.onClick.RemoveListener(NextOwners);
            if (loadButton != null) loadButton.onClick.RemoveListener(LoadSelected);
            if (saveButton != null) saveButton.onClick.RemoveListener(SaveSelected);
            if (deleteButton != null) deleteButton.onClick.RemoveListener(DeleteSelected);
            if (autoSaveButton != null) autoSaveButton.onClick.RemoveListener(ToggleAutoSave);
            if (customSprite != null) Destroy(customSprite);
            if (customTexture != null) Destroy(customTexture);
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.5f;
                Refresh();
            }
        }

        public void ClosePicker() { if (ownerPicker != null) ownerPicker.SetActive(false); }

        private void TogglePicker()
        {
            if (ownerPicker == null) return;
            bool show = !ownerPicker.activeSelf;
            ownerPicker.SetActive(show);
            if (show) RefreshOwnerPicker();
        }
        private void PreviousOwners() { ownerPage = Mathf.Max(0, ownerPage - 1); RefreshOwnerPicker(); }
        private void NextOwners()
        {
            int count = SDGSaveProfilesV2.Owners.Count;
            ownerPage = Mathf.Min(Mathf.Max(0, (count - 1) / 4), ownerPage + 1);
            RefreshOwnerPicker();
        }
        private void RefreshOwnerPicker()
        {
            var owners = SDGSaveProfilesV2.Owners;
            for (int i = 0; ownerButtons != null && i < ownerButtons.Length; i++)
            {
                int index = ownerPage * 4 + i;
                bool valid = index < owners.Count;
                if (ownerButtons[i] != null) ownerButtons[i].gameObject.SetActive(valid);
                if (valid && ownerButtonLabels != null && i < ownerButtonLabels.Length && ownerButtonLabels[i] != null)
                    ownerButtonLabels[i].text = (owners[index].id == SDGSaveProfilesV2.ActiveId ? "▶ " : "") + owners[index].name;
            }
            if (ownerPrev != null) ownerPrev.interactable = ownerPage > 0;
            if (ownerNext != null) ownerNext.interactable = (ownerPage + 1) * 4 < owners.Count;
            if (pickerPage != null) pickerPage.text = (ownerPage + 1) + "/" + Mathf.Max(1, (owners.Count + 3) / 4);
        }
        private void SelectOwner(int option)
        {
            var owners = SDGSaveProfilesV2.Owners;
            int index = ownerPage * 4 + option;
            if (index < 0 || index >= owners.Count) return;
            if (!SDGSaveProgressRuntimeV1.SwitchOwner(owners[index].id))
            {
                message = "切換玩家失敗：存檔服務尚未啟動";
                return;
            }
            selectedSlot = 1;
            if (ownerPicker != null) ownerPicker.SetActive(false);
            loadedAvatarKey = null;
            ApplyOriginalProfileDisplay();
            message = "已選擇「" + owners[index].name + "」的獨立存檔";
            Refresh();
        }
        private void CreateOwner()
        {
            // Capture the current main-menu name/avatar as a NEW identity; GUID keeps identical portraits separate.
            SDGSaveOwnerV2 added = SDGSaveProfilesV2.CreateFromCurrentPlayer();
            if (added == null) { message = "最多支援 16 位玩家"; Refresh(); return; }
            SelectOwnerById(added.id);
        }
        private void SelectOwnerById(string id)
        {
            if (!SDGSaveProgressRuntimeV1.SwitchOwner(id)) { message = "無法啟用新玩家"; return; }
            if (ownerPicker != null) ownerPicker.SetActive(false);
            selectedSlot = 1;
            loadedAvatarKey = null;
            ApplyOriginalProfileDisplay();
            message = "新玩家已建立，存檔獨立保存";
            Refresh();
        }
        private void ApplyOriginalProfileDisplay()
        {
            if (originalProfile == null) return;
            if (originalProfile.playerNameInput != null)
                originalProfile.playerNameInput.SetTextWithoutNotify(PlayerPrefs.GetString("SDG_FE_PlayerName", "回收探險者"));
            Sprite image = ResolveAvatar(SDGSaveProfilesV2.Active.avatar);
            if (image != null && originalProfile.avatarImage != null) originalProfile.avatarImage.sprite = image;
        }
        private void ChooseSlot(int slot)
        {
            selectedSlot = slot;
            SDGSaveProgressRuntimeV1.SelectedSlot = slot;
            confirmDeleteSlot = -1;
            message = slot == 0 ? "已選擇自動存檔" : "已選擇手動存檔 " + slot;
            Refresh();
        }
        public void LoadSelected()
        {
            if (!SDGSaveProgressRuntimeV1.TryLoad(selectedSlot, out string result))
            {
                message = result; Refresh();
            }
        }
        public void ContinueNewest()
        {
            int newest = SDGSaveProgressRuntimeV1.NewestSlot();
            if (newest < 0 || !SDGSaveProgressRuntimeV1.TryLoad(newest, out string result))
            { message = "此玩家尚無有效存檔"; Refresh(); }
        }
        public void SaveSelected()
        {
            if (selectedSlot == 0) { message = "自動存檔由系統管理；請選手動欄位"; Refresh(); return; }
            // Scene check is enforced by TrySave; menu never creates an invalid checkpoint.
            if (!SDGSaveProgressRuntimeV1.TrySave(selectedSlot, out string result)) message = result;
            else message = result;
            Refresh();
        }
        private void DeleteSelected()
        {
            if (confirmDeleteSlot != selectedSlot || Time.unscaledTime > deleteConfirmUntil)
            {
                confirmDeleteSlot = selectedSlot;
                deleteConfirmUntil = Time.unscaledTime + 6f;
                message = "再次按『刪除』才會移走此筆存檔（可從備份救回）";
                Refresh();
                return;
            }
            SDGSaveProfilesV2.DeleteSlot(selectedSlot, out message);
            confirmDeleteSlot = -1;
            Refresh();
        }
        private void ToggleAutoSave()
        {
            SDGSaveProgressRuntimeV1.AutoSaveEnabled = !SDGSaveProgressRuntimeV1.AutoSaveEnabled;
            message = "自動存檔已" + (SDGSaveProgressRuntimeV1.AutoSaveEnabled ? "啟用" : "關閉");
            Refresh();
        }
        private Sprite ResolveAvatar(string key)
        {
            if (originalProfile == null) return null;
            if (string.IsNullOrEmpty(key)) key = "builtin:0";
            if (key.StartsWith("builtin:", StringComparison.Ordinal))
            {
                if (customSprite != null) { Destroy(customSprite); customSprite = null; }
                if (customTexture != null) { Destroy(customTexture); customTexture = null; }
                loadedAvatarKey = key;
                if (int.TryParse(key.Substring(8), out int index) && originalProfile.builtInAvatars != null &&
                    index >= 0 && index < originalProfile.builtInAvatars.Length)
                    return originalProfile.builtInAvatars[index];
                return originalProfile.avatarImage != null ? originalProfile.avatarImage.sprite : null;
            }
            if (!key.StartsWith("custom:", StringComparison.Ordinal)) return null;
            if (key == loadedAvatarKey) return customSprite;
            if (customSprite != null) Destroy(customSprite);
            if (customTexture != null) Destroy(customTexture);
            customSprite = null; customTexture = null;
            loadedAvatarKey = key;
            try
            {
                string file = Path.GetFileName(key.Substring(7));
                string path = Path.Combine(Application.persistentDataPath, "SDG_ProfileAvatars", file);
                if (!File.Exists(path) || new FileInfo(path).Length > 8 * 1024 * 1024) return null;
                byte[] bytes = File.ReadAllBytes(path);
                customTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!customTexture.LoadImage(bytes)) return null;
                customSprite = Sprite.Create(customTexture, new Rect(0, 0, customTexture.width, customTexture.height), new Vector2(0.5f, 0.5f), 100f);
                return customSprite;
            }
            catch (Exception ex) { Debug.LogWarning("[SDG Save UI] Avatar: " + ex.Message); return null; }
        }
        private void Refresh()
        {
            var p = SDGSaveProfilesV2.Active;
            if (p == null) return;
            if (ownerName != null) ownerName.text = p.name;
            if (ownerProgress != null) ownerProgress.text = "通關進度 " + Bits(p.completedMask) * 25 + "%";
            if (ownerPlaytime != null) ownerPlaytime.text = "累計時間 " + FormatTime(p.totalPlaySeconds);
            if (statusText != null) statusText.text = message;
            if (avatarImage != null)
            {
                Sprite sprite = ResolveAvatar(p.avatar);
                if (sprite != null) avatarImage.sprite = sprite;
                avatarImage.preserveAspect = true;
            }
            bool selectedValid = false;
            for (int i = 0; i < 4; i++)
            {
                bool valid = SDGSaveProgressRuntimeV1.TryReadSlot(i, out SDGProgressRecordV1 record);
                if (i == selectedSlot) selectedValid = valid;
                if (slotBackgrounds != null && i < slotBackgrounds.Length && slotBackgrounds[i] != null)
                    slotBackgrounds[i].color = i == selectedSlot ? selected : ordinary;
                if (slotTitleLabels != null && i < slotTitleLabels.Length && slotTitleLabels[i] != null)
                    slotTitleLabels[i].text = (i == 0 ? "自動存檔" : "手動存檔 " + i) +
                        (valid ? "   第" + record.levelNumber.ToString("00") + "關 · " + (Bits(record.completedMask) * 25) + "%" : "   — 空白 —");
                if (slotDetailLabels != null && i < slotDetailLabels.Length && slotDetailLabels[i] != null)
                {
                    string timestamp = "尚未儲存";
                    if (valid && DateTime.TryParse(record.savedUtc, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out DateTime date))
                        timestamp = date.ToLocalTime().ToString("MM/dd HH:mm") + "  ·  " + FormatTime(record.totalPlaySeconds);
                    if (valid)
                        timestamp += "  ·  HP " + Mathf.Clamp(record.health, 0, Mathf.Max(1, record.maxHealth))
                            + "/" + Mathf.Max(1, record.maxHealth);
                    slotDetailLabels[i].text = timestamp;
                }
            }
            if (loadButton != null) loadButton.interactable = selectedValid;
            // The menu is not a valid checkpoint; F5 / pause menu is the correct save context.
            if (saveButton != null) saveButton.interactable = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path.IndexOf("/02_Levels/Level0", StringComparison.OrdinalIgnoreCase) >= 0 && selectedSlot != 0;
            if (deleteButton != null) deleteButton.interactable = selectedValid;
            if (continueButton != null) continueButton.interactable = SDGSaveProgressRuntimeV1.NewestSlot() >= 0;
            if (autoSaveLabel != null) autoSaveLabel.text = "自動存檔：" + (SDGSaveProgressRuntimeV1.AutoSaveEnabled ? "開" : "關");
        }
        private static int Bits(int mask) { int n = 0; for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) n++; return n; }
        private static string FormatTime(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            return string.Format("{0:00}:{1:00}:{2:00}", seconds / 3600, (seconds / 60) % 60, seconds % 60);
        }
    }
}
