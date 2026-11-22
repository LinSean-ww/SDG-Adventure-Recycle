using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SDGAdventure.FrontEnd
{
    /// <summary>
    /// Front-end only. Existing level scenes and existing save system remain untouched.
    /// Existing save system may call SDGFrontEndSaveBridge.ReportLatestSave().
    /// </summary>
    public sealed class SDGFrontEndController : MonoBehaviour
    {
        [Header("Screen objects")]
        public GameObject startScreen;
        public GameObject mainMenuScreen;
        public GameObject levelsDialog;
        public GameObject settingsDialog;
        public GameObject creditsDialog;

        [Header("Start screen")]
        public TextMeshProUGUI pressAnyKeyText;
        public CanvasGroup pressAnyKeyGroup;

        [Header("Menu data")]
        public TextMeshProUGUI playerNameText;
        public TextMeshProUGUI playerLevelText;
        public TextMeshProUGUI playerExperienceText;
        public Image experienceBarFill;
        public TextMeshProUGUI saveTitleText;
        public TextMeshProUGUI saveProgressText;
        public TextMeshProUGUI saveDurationText;
        public RawImage saveThumbnail;
        public Image saveProgressBarFill;
        public Button continueButton;
        public Button[] levelButtons = new Button[4];
        public TextMeshProUGUI[] levelLabels = new TextMeshProUGUI[4];
        public TextMeshProUGUI statusText;

        [Header("Detected scene paths (WITHOUT .unity)")]
        public string[] levelScenePaths = new string[4];

        private const string MenuVersionLabel = "V2.6.2";
        private float statusUntil;
        private Texture2D runtimeThumbnail;

        private void Start()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("SDG_FE_MasterVolume", 1f));
            if (pressAnyKeyGroup != null) pressAnyKeyGroup.alpha = 1f;
            EnsureVersionLabel();
            // R2: the entire START / Press Any Key screen has been retired.
            // Go directly to the actual main menu; nothing from the splash UI is displayed.
            ShowMainMenu();
        }

        private void OnDestroy()
        {
            if (runtimeThumbnail != null) Destroy(runtimeThumbnail);
        }

        private void Update()
        {
            // Defense against legacy UI events that accidentally enable the old splash.
            if (startScreen != null && startScreen.activeSelf)
            {
                ShowMainMenu();
                return;
            }

            if (EscapeDown())
            {
                if (AnyDialogOpen()) CloseDialogs();
                else ShowMainMenu();
            }

            if (statusText != null && statusUntil > 0 && Time.unscaledTime > statusUntil)
            {
                statusUntil = 0;
                statusText.text = string.Empty;
            }
        }

        private static bool AnyKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0)) return true;
#endif
            return false;
        }

        private static bool EscapeDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
            return false;
        }

        public void ShowStartScreen()
        {
            // Compatibility with old scene button/event hookups: never re-open the splash.
            ShowMainMenu();
        }

        public void ShowMainMenu()
        {
            if (startScreen != null) startScreen.SetActive(false);
            if (mainMenuScreen != null) mainMenuScreen.SetActive(true);
            CloseDialogs();
            RefreshUI();
        }

        private void EnsureVersionLabel()
        {
            if (mainMenuScreen == null)
            {
                Debug.LogError("[SDG Menu R2] Main menu reference is missing; version cannot be shown.");
                return;
            }
            // Disable R1's older label to prevent duplicate text if this is reinitialised.
            Transform oldLabel = mainMenuScreen.transform.Find("SDG Menu Version - Static");
            if (oldLabel != null) oldLabel.gameObject.SetActive(false);
            const string overlayName = "SDG Version Overlay R2";
            Transform existing = mainMenuScreen.transform.Find(overlayName);
            if (existing != null) { existing.SetAsLastSibling(); return; }

            GameObject overlay = new GameObject(overlayName,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            overlay.transform.SetParent(mainMenuScreen.transform, false);
            RectTransform full = overlay.GetComponent<RectTransform>();
            full.anchorMin = Vector2.zero;
            full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero;
            full.offsetMax = Vector2.zero;
            full.localScale = Vector3.one;
            Canvas canvas = overlay.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 300;
            CanvasGroup group = overlay.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            GameObject labelObject = new GameObject("Version V2.6.2", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(overlay.transform, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(22f, 16f);
            rect.sizeDelta = new Vector2(240f, 40f);
            TextMeshProUGUI versionText = labelObject.GetComponent<TextMeshProUGUI>();
            if (playerNameText != null && playerNameText.font != null)
                versionText.font = playerNameText.font;
            else if (pressAnyKeyText != null && pressAnyKeyText.font != null)
                versionText.font = pressAnyKeyText.font;
            else if (TMP_Settings.defaultFontAsset != null)
                versionText.font = TMP_Settings.defaultFontAsset;
            versionText.text = MenuVersionLabel;
            versionText.fontSize = 22f;
            versionText.fontStyle = FontStyles.Bold;
            versionText.alignment = TextAlignmentOptions.BottomLeft;
            versionText.color = Color.white;
            versionText.outlineColor = new Color(0f, 0f, 0f, 0.95f);
            versionText.outlineWidth = 0.15f;
            versionText.enableWordWrapping = false;
            versionText.raycastTarget = false;
            overlay.transform.SetAsLastSibling();
            Debug.Log("[SDG Menu R2] Main menu shown; version overlay V2.6.2 added.");
        }

        public void StartNewGame() { LoadLevel(0); }

        public void ContinueGame()
        {
            // R2: the old implementation only changed scenes and never restored any slot.
            // Continue must go through the same validated record as the Save/Load UI.
            int slot = SDGSaveProgressRuntimeV1.NewestSlot();
            if (slot < 0)
            {
                ShowStatus("此玩家尚無有效的存檔欄位");
                return;
            }
            if (!SDGSaveProgressRuntimeV1.TryLoad(slot, out string message))
            {
                ShowStatus(message);
                return;
            }
            Debug.Log("[SDG Menu R2] Continue requested slot " + slot + ". " + message);
        }

        public void OpenLevelSelection()
        {
            CloseDialogs();
            if (levelsDialog != null) levelsDialog.SetActive(true);
        }

        public void OpenSettings()
        {
            CloseDialogs();
            if (settingsDialog != null) settingsDialog.SetActive(true);
        }

        public void OpenCredits()
        {
            CloseDialogs();
            if (creditsDialog != null) creditsDialog.SetActive(true);
        }

        public void CloseDialogs()
        {
            if (levelsDialog != null) levelsDialog.SetActive(false);
            if (settingsDialog != null) settingsDialog.SetActive(false);
            if (creditsDialog != null) creditsDialog.SetActive(false);
        }

        private bool AnyDialogOpen()
        {
            return (levelsDialog != null && levelsDialog.activeSelf)
                || (settingsDialog != null && settingsDialog.activeSelf)
                || (creditsDialog != null && creditsDialog.activeSelf);
        }

        public void LaunchLevel01() { LoadLevel(0); }
        public void LaunchLevel02() { LoadLevel(1); }
        public void LaunchLevel03() { LoadLevel(2); }
        public void LaunchLevel04() { LoadLevel(3); }

        private void LoadLevel(int index)
        {
            if (levelScenePaths == null || index < 0 || index >= levelScenePaths.Length
                || string.IsNullOrEmpty(levelScenePaths[index]))
            {
                ShowStatus("此關卡尚未連結場景，請先設定關卡路徑");
                return;
            }
            LoadScene(levelScenePaths[index]);
        }

        private void LoadScene(string scenePath)
        {
            // SDG_FE_BUILD_INDEX_FIX_V1
            if (string.IsNullOrEmpty(scenePath))
            {
                ShowStatus("關卡場景路徑未設定");
                return;
            }
            string normalized = scenePath.Replace('\\', '/').Trim();
            string assetPath = normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                ? normalized : normalized + ".unity";
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(assetPath);
            if (buildIndex < 0)
            {
                Debug.LogError("[SDG Scene Index] Scene not enabled in Build Settings: " + assetPath);
                ShowStatus("未登錄關卡場景：" + assetPath);
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(buildIndex))
            {
                Debug.LogError("[SDG Scene Index] Build index cannot load: " + buildIndex + " | " + assetPath);
                ShowStatus("無法載入關卡索引：" + buildIndex);
                return;
            }
            try
            {
                Debug.Log("[SDG Scene Index] Loading index " + buildIndex + " | " + assetPath);
                SceneManager.LoadScene(buildIndex);
            }
            catch (Exception e)
            {
                Debug.LogError("[SDG Scene Index] Scene load exception: " + e);
                ShowStatus("載入關卡失敗，請檢查 Console");
            }
        }

        public void SetMasterVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("SDG_FE_MasterVolume", AudioListener.volume);
            PlayerPrefs.Save();
        }

        public void SetFullscreen(bool enabled) { Screen.fullScreen = enabled; }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void RefreshUI()
        {
            string name = PlayerPrefs.GetString(SDGFrontEndSaveBridge.KeyPlayerName, "回收探險者");
            int level = Mathf.Max(1, PlayerPrefs.GetInt(SDGFrontEndSaveBridge.KeyPlayerLevel, 1));
            int xp = Mathf.Max(0, PlayerPrefs.GetInt(SDGFrontEndSaveBridge.KeyXP, 0));
            int xpMax = Mathf.Max(1, PlayerPrefs.GetInt(SDGFrontEndSaveBridge.KeyXPMax, 100));
            bool hasSave = PlayerPrefs.HasKey(SDGFrontEndSaveBridge.KeyScenePath)
                         && !string.IsNullOrEmpty(PlayerPrefs.GetString(SDGFrontEndSaveBridge.KeyScenePath));

            if (playerNameText != null) playerNameText.text = name;
            if (playerLevelText != null) playerLevelText.text = "Lv. " + level;
            if (playerExperienceText != null) playerExperienceText.text = xp.ToString("N0") + " / " + xpMax.ToString("N0");
            if (experienceBarFill != null) experienceBarFill.fillAmount = Mathf.Clamp01((float)xp / xpMax);

            if (saveTitleText != null) saveTitleText.text = hasSave
                ? PlayerPrefs.GetString(SDGFrontEndSaveBridge.KeyLevelTitle, "最近的存檔")
                : "尚無存檔";
            float progress = hasSave ? Mathf.Clamp01(PlayerPrefs.GetFloat(SDGFrontEndSaveBridge.KeyProgress, 0)) : 0;
            if (saveProgressText != null) saveProgressText.text = hasSave ? "進度 " + Mathf.RoundToInt(progress * 100) + "%" : "進度 --";
            if (saveProgressBarFill != null) saveProgressBarFill.fillAmount = progress;
            int seconds = hasSave ? Mathf.Max(0, PlayerPrefs.GetInt(SDGFrontEndSaveBridge.KeyDurationSeconds, 0)) : 0;
            if (saveDurationText != null) saveDurationText.text = hasSave
                ? string.Format("遊玩時間 {0:00}:{1:00}:{2:00}", seconds / 3600, (seconds / 60) % 60, seconds % 60)
                : "遊玩時間 --:--:--";
            if (continueButton != null) continueButton.interactable = hasSave;

            if (levelButtons != null)
            {
                for (int i = 0; i < levelButtons.Length; i++)
                {
                    bool configured = levelScenePaths != null && i < levelScenePaths.Length
                                      && !string.IsNullOrEmpty(levelScenePaths[i]);
                    // Leave unconfigured options clickable to report a helpful error rather than silently failing.
                    if (levelButtons[i] != null) levelButtons[i].interactable = true;
                    if (levelLabels != null && i < levelLabels.Length && levelLabels[i] != null)
                        levelLabels[i].text = string.Format("第 {0} 關{1}", i + 1, configured ? "" : "（待設定）");
                }
            }
            LoadSaveThumbnail();
        }

        private void LoadSaveThumbnail()
        {
            if (saveThumbnail == null) return;
            string file = Path.Combine(Application.persistentDataPath, "SDG_FE_LastSaveThumbnail.png");
            if (!File.Exists(file)) return;
            try
            {
                byte[] bytes = File.ReadAllBytes(file);
                Texture2D next = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (next.LoadImage(bytes))
                {
                    if (runtimeThumbnail != null) Destroy(runtimeThumbnail);
                    runtimeThumbnail = next;
                    saveThumbnail.texture = runtimeThumbnail;
                    saveThumbnail.color = Color.white;
                }
                else Destroy(next);
            }
            catch (Exception ex) { Debug.LogWarning("FrontEnd thumbnail could not be loaded: " + ex.Message); }
        }

        public void ShowStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            statusUntil = Time.unscaledTime + 5f;
            Debug.Log("[SDG FrontEnd] " + message);
        }
    }

    /// <summary>
    /// Optional adapter for the REAL game's save system to call on a successful save.
    /// It does not create a checkpoint or capture a screenshot by itself.
    /// </summary>
    public static class SDGFrontEndSaveBridge
    {
        public const string KeyScenePath = "SDG_FE_LastSaveScene";
        public const string KeyLevelTitle = "SDG_FE_LastSaveTitle";
        public const string KeyProgress = "SDG_FE_LastSaveProgress01";
        public const string KeyDurationSeconds = "SDG_FE_LastSaveSeconds";
        public const string KeyPlayerName = "SDG_FE_PlayerName";
        public const string KeyPlayerLevel = "SDG_FE_PlayerLevel";
        public const string KeyXP = "SDG_FE_Experience";
        public const string KeyXPMax = "SDG_FE_ExperienceMax";

        public static void ReportLatestSave(string scenePathWithoutUnityExtension, string levelTitle,
            float progress01, int durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(scenePathWithoutUnityExtension)) return;
            PlayerPrefs.SetString(KeyScenePath, scenePathWithoutUnityExtension);
            PlayerPrefs.SetString(KeyLevelTitle, string.IsNullOrWhiteSpace(levelTitle) ? "最近的存檔" : levelTitle);
            PlayerPrefs.SetFloat(KeyProgress, Mathf.Clamp01(progress01));
            PlayerPrefs.SetInt(KeyDurationSeconds, Mathf.Max(0, durationSeconds));
            PlayerPrefs.Save();
        }

        public static void ReportPlayer(string playerName, int playerLevel, int experience, int experienceMax)
        {
            PlayerPrefs.SetString(KeyPlayerName, string.IsNullOrWhiteSpace(playerName) ? "回收探險者" : playerName);
            PlayerPrefs.SetInt(KeyPlayerLevel, Mathf.Max(1, playerLevel));
            PlayerPrefs.SetInt(KeyXP, Mathf.Max(0, experience));
            PlayerPrefs.SetInt(KeyXPMax, Mathf.Max(1, experienceMax));
            PlayerPrefs.Save();
        }

        public static bool WriteThumbnailPNG(byte[] pngBytes)
        {
            if (pngBytes == null || pngBytes.Length == 0) return false;
            try
            {
                File.WriteAllBytes(Path.Combine(Application.persistentDataPath, "SDG_FE_LastSaveThumbnail.png"), pngBytes);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Unable to save front-end thumbnail: " + ex.Message);
                return false;
            }
        }
    }
}
