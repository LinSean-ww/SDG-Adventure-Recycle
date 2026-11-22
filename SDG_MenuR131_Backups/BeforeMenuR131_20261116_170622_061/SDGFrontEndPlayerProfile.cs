using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd
{
    /// <summary>Runtime player name and selectable avatar for the existing front-end menu.
    /// Never modifies gameplay or level scenes.</summary>
    public sealed class SDGFrontEndPlayerProfile : MonoBehaviour
    {
        private const string NameKey = "SDG_FE_PlayerName";
        private const string AvatarKey = "SDG_FE_SelectedAvatar";
        [Serializable]
        public class PortraitTile
        {
            public Button button;
            public Image image;
        }
        public InputField playerNameInput;
        public Image avatarImage;
        public Button avatarOpenButton;
        public GameObject pickerDialog;
        public Button pickerCloseButton;
        public Button pickerImportButton;
        public Button pickerPrevButton;
        public Button pickerNextButton;
        public InputField filePathInput;
        public Text pickerPageLabel;
        public Text pickerMessage;
        public PortraitTile[] tiles;
        public Sprite[] builtInAvatars;

        private class Option
        {
            public string id;
            public Sprite sprite;
        }
        private readonly List<Option> options = new List<Option>();
        private readonly List<UnityEngine.Object> ownedImages = new List<UnityEngine.Object>();
        private int page;
        private bool wiringDone;

        private string CustomDir { get { return Path.Combine(Application.persistentDataPath, "SDG_ProfileAvatars"); } }

        private void Awake()
        {
            if (playerNameInput != null)
            {
                playerNameInput.characterLimit = 20;
                playerNameInput.onEndEdit.AddListener(SaveName);
                playerNameInput.SetTextWithoutNotify(PlayerPrefs.GetString(NameKey, "回收探險者"));
            }
            if (avatarOpenButton != null) avatarOpenButton.onClick.AddListener(OpenPicker);
            if (pickerCloseButton != null) pickerCloseButton.onClick.AddListener(ClosePicker);
            if (pickerImportButton != null) pickerImportButton.onClick.AddListener(ImportAvatar);
            if (pickerPrevButton != null) pickerPrevButton.onClick.AddListener(PreviousPage);
            if (pickerNextButton != null) pickerNextButton.onClick.AddListener(NextPage);
            wiringDone = true;
            BuildOptions();
            ApplySavedAvatar();
            if (pickerDialog != null) pickerDialog.SetActive(false);
        }

        private void OnEnable()
        {
            if (playerNameInput != null && !playerNameInput.isFocused)
                playerNameInput.SetTextWithoutNotify(PlayerPrefs.GetString(NameKey, "回收探險者"));
        }

        private void OnDisable()
        {
            if (pickerDialog != null) pickerDialog.SetActive(false);
        }

        private void OnDestroy()
        {
            if (wiringDone && playerNameInput != null) playerNameInput.onEndEdit.RemoveListener(SaveName);
            if (wiringDone && avatarOpenButton != null) avatarOpenButton.onClick.RemoveListener(OpenPicker);
            if (wiringDone && pickerCloseButton != null) pickerCloseButton.onClick.RemoveListener(ClosePicker);
            if (wiringDone && pickerImportButton != null) pickerImportButton.onClick.RemoveListener(ImportAvatar);
            if (wiringDone && pickerPrevButton != null) pickerPrevButton.onClick.RemoveListener(PreviousPage);
            if (wiringDone && pickerNextButton != null) pickerNextButton.onClick.RemoveListener(NextPage);
            foreach (UnityEngine.Object obj in ownedImages) if (obj != null) Destroy(obj);
        }

        private void SaveName(string candidate)
        {
            string clean = string.IsNullOrEmpty(candidate) ? string.Empty : candidate.Trim();
            if (string.IsNullOrEmpty(clean)) clean = "回收探險者";
            if (clean.Length > 20) clean = clean.Substring(0, 20);
            PlayerPrefs.SetString(NameKey, clean);
            PlayerPrefs.Save();
            if (playerNameInput != null) playerNameInput.SetTextWithoutNotify(clean);
        }

        private void BuildOptions()
        {
            foreach (UnityEngine.Object obj in ownedImages) if (obj != null) Destroy(obj);
            ownedImages.Clear();
            options.Clear();
            if (builtInAvatars != null)
            {
                for (int i = 0; i < builtInAvatars.Length; i++)
                    if (builtInAvatars[i] != null)
                        options.Add(new Option { id = "builtin:" + i, sprite = builtInAvatars[i] });
            }
            try
            {
                if (!Directory.Exists(CustomDir)) return;
                string[] files = Directory.GetFiles(CustomDir, "*.png");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                int limit = Mathf.Min(files.Length, 36);
                for (int i = 0; i < limit; i++)
                {
                    Sprite sprite;
                    if (!TryLoadSprite(files[i], out sprite)) continue;
                    options.Add(new Option { id = "custom:" + Path.GetFileName(files[i]), sprite = sprite });
                }
            }
            catch (Exception e) { Debug.LogWarning("[SDG Profile] Avatar list read failed: " + e.Message); }
        }

        private bool TryLoadSprite(string file, out Sprite sprite)
        {
            sprite = null;
            try
            {
                var info = new FileInfo(file);
                if (!info.Exists || info.Length < 8 || info.Length > 8 * 1024 * 1024) return false;
                byte[] bytes = File.ReadAllBytes(file);
                if (bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71) return false;
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes) || texture.width > 4096 || texture.height > 4096)
                {
                    Destroy(texture);
                    return false;
                }
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                ownedImages.Add(texture);
                ownedImages.Add(sprite);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SDG Profile] Could not read avatar: " + e.Message);
                return false;
            }
        }

        private void ApplySavedAvatar()
        {
            if (avatarImage == null || options.Count == 0) return;
            string id = PlayerPrefs.GetString(AvatarKey, "builtin:0");
            Option chosen = options.Find(x => x.id == id);
            if (chosen == null) chosen = options[0];
            avatarImage.sprite = chosen.sprite;
            avatarImage.preserveAspect = true;
        }

        private void SelectAvatar(string id)
        {
            Option chosen = options.Find(x => x.id == id);
            if (chosen == null) return;
            PlayerPrefs.SetString(AvatarKey, id);
            PlayerPrefs.Save();
            ApplySavedAvatar();
            ClosePicker();
        }

        private void OpenPicker()
        {
            if (pickerDialog == null) return;
            BuildOptions();
            ApplySavedAvatar();
            page = 0;
            SetMessage("選擇頭像，或匯入自己的 PNG 圖片");
            pickerDialog.SetActive(true);
            pickerDialog.transform.SetAsLastSibling();
            RefreshPicker();
        }

        private void ClosePicker() { if (pickerDialog != null) pickerDialog.SetActive(false); }
        private void PreviousPage() { page = Mathf.Max(0, page - 1); RefreshPicker(); }
        private void NextPage()
        {
            int count = tiles == null ? 0 : tiles.Length;
            if (count == 0) return;
            page = Mathf.Min(Mathf.Max(0, (options.Count - 1) / count), page + 1);
            RefreshPicker();
        }

        private void RefreshPicker()
        {
            int count = tiles == null ? 0 : tiles.Length;
            if (count == 0) return;
            int pages = Mathf.Max(1, (options.Count + count - 1) / count);
            page = Mathf.Clamp(page, 0, pages - 1);
            for (int i = 0; i < count; i++)
            {
                PortraitTile tile = tiles[i];
                if (tile == null || tile.button == null) continue;
                int index = page * count + i;
                bool available = index < options.Count;
                tile.button.gameObject.SetActive(available);
                tile.button.onClick.RemoveAllListeners();
                if (!available) continue;
                if (tile.image != null) { tile.image.sprite = options[index].sprite; tile.image.preserveAspect = true; }
                string id = options[index].id;
                tile.button.onClick.AddListener(() => SelectAvatar(id));
            }
            if (pickerPageLabel != null) pickerPageLabel.text = (page + 1) + " / " + pages;
            if (pickerPrevButton != null) pickerPrevButton.interactable = page > 0;
            if (pickerNextButton != null) pickerNextButton.interactable = page < pages - 1;
        }

        private void SetMessage(string message) { if (pickerMessage != null) pickerMessage.text = message; }

        private void ImportAvatar()
        {
            string source = filePathInput == null ? "" : filePathInput.text.Trim().Trim('"');
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(source))
                source = UnityEditor.EditorUtility.OpenFilePanel("選擇玩家頭像 PNG", "", "png");
#endif
            if (string.IsNullOrEmpty(source))
            {
                SetMessage("請貼上 PNG 路徑，或在 Unity Editor 中選取檔案");
                return;
            }
            try
            {
                if (!File.Exists(source) || !string.Equals(Path.GetExtension(source), ".png", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("檔案不存在或不是 PNG");
                FileInfo info = new FileInfo(source);
                if (info.Length > 8 * 1024 * 1024) throw new InvalidDataException("PNG 不能超過 8 MB");
                byte[] bytes = File.ReadAllBytes(source);
                if (bytes.Length < 8 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71)
                    throw new InvalidDataException("不是有效的 PNG 檔案");
                Directory.CreateDirectory(CustomDir);
                string filename = "avatar_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png";
                string destination = Path.Combine(CustomDir, filename);
                File.WriteAllBytes(destination, bytes);
                BuildOptions();
                string id = "custom:" + filename;
                if (options.Find(x => x.id == id) == null)
                {
                    File.Delete(destination);
                    throw new InvalidDataException("PNG 無法讀取或尺寸超過限制");
                }
                SelectAvatar(id);
                if (filePathInput != null) filePathInput.SetTextWithoutNotify("");
                Debug.Log("[SDG Profile] Imported avatar into user data: " + filename);
            }
            catch (Exception e)
            {
                SetMessage("匯入失敗：" + e.Message);
                Debug.LogWarning("[SDG Profile] Avatar import failed: " + e);
            }
        }
    }
}
