using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd
{
    /// <summary>Presenter for the existing FE_Menu_SavePanel.png. Creates no new background art.</summary>
    [DisallowMultipleComponent]
    public sealed class SDGSaveProgressMenuV1 : MonoBehaviour
    {
        public TextMeshProUGUI leftPlayer;
        public TextMeshProUGUI leftSaved;
        public TextMeshProUGUI leftSlot;
        public TextMeshProUGUI rightLevel;
        public TextMeshProUGUI rightProgress;
        public TextMeshProUGUI rightDuration;
        public TextMeshProUGUI autoSaveLabel;
        public Image progressFill;
        public Button previousButton;
        public Button nextButton;
        public Button loadButton;
        public Button continueButton;
        public Button autoSaveButton;
        private int currentSlot;
        private float nextRefresh;

        private void Awake()
        {
            currentSlot = SDGSaveProgressRuntimeV1.SelectedSlot;
            if (currentSlot < 0)
            {
                currentSlot = SDGSaveProgressRuntimeV1.NewestSlot();
                if (currentSlot < 0) currentSlot = 1;
            }
            SDGSaveProgressRuntimeV1.SelectedSlot = currentSlot;
            if (previousButton != null) previousButton.onClick.AddListener(Previous);
            if (nextButton != null) nextButton.onClick.AddListener(Next);
            if (loadButton != null) loadButton.onClick.AddListener(LoadSelected);
            if (autoSaveButton != null) autoSaveButton.onClick.AddListener(ToggleAutoSave);
            Refresh();
        }

        private void OnDestroy()
        {
            if (previousButton != null) previousButton.onClick.RemoveListener(Previous);
            if (nextButton != null) nextButton.onClick.RemoveListener(Next);
            if (loadButton != null) loadButton.onClick.RemoveListener(LoadSelected);
            if (autoSaveButton != null) autoSaveButton.onClick.RemoveListener(ToggleAutoSave);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.35f;
            Refresh();
        }

        public void Previous() { currentSlot = (currentSlot + 3) % 4; SDGSaveProgressRuntimeV1.SelectedSlot = currentSlot; Refresh(); }
        public void Next() { currentSlot = (currentSlot + 1) % 4; SDGSaveProgressRuntimeV1.SelectedSlot = currentSlot; Refresh(); }

        public void LoadSelected()
        {
            // Keep the legacy panel visually intact but open the all-slots v5 picker.
            SDGSaveSlotsOverlayV5.OpenLoad();
        }

        // Bound as the original menu's Continue button via editor persistent UnityEvent.
        public void ContinueNewest()
        {
            int newest = SDGSaveProgressRuntimeV1.NewestSlot();
            if (newest < 0 || !SDGSaveProgressRuntimeV1.TryLoad(newest, out string result))
            {
                if (leftSaved != null) leftSaved.text = "尚無有效存檔";
                Debug.LogWarning("[SDG Progress] No valid Continue save.");
            }
        }

        public void ToggleAutoSave()
        {
            SDGSaveProgressRuntimeV1.AutoSaveEnabled = !SDGSaveProgressRuntimeV1.AutoSaveEnabled;
            Refresh();
        }

        private void Refresh()
        {
            if (autoSaveLabel != null)
                autoSaveLabel.text = "自動存檔：" + (SDGSaveProgressRuntimeV1.AutoSaveEnabled ? "開" : "關");

            bool found = SDGSaveProgressRuntimeV1.TryReadSlot(currentSlot, out SDGProgressRecordV1 record);
            if (leftSlot != null)
                leftSlot.text = currentSlot == 0 ? "自動存檔" : "手動存檔 " + currentSlot + "/3";
            if (leftPlayer != null)
                leftPlayer.text = "玩家：" + (found ? (record.playerName ?? "回收探險者") :
                                            PlayerPrefs.GetString("SDG_FE_PlayerName", "回收探險者"));
            if (leftSaved != null)
            {
                string stamp = "尚無存檔（F5 可儲存）";
                if (found && DateTime.TryParse(record.savedUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTime dt))
                    stamp = dt.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
                leftSaved.text = stamp;
            }
            if (rightLevel != null)
                rightLevel.text = found ? "第 " + record.levelNumber.ToString("00") + " 關" : "尚無存檔";
            float percent = found ? Progress(record.completedMask) : 0f;
            if (rightProgress != null)
                rightProgress.text = "遊戲進度 " + Mathf.RoundToInt(percent * 100f) + "%";
            if (rightDuration != null)
            {
                int total = found ? Mathf.Max(0, record.totalPlaySeconds) : SDGSaveProgressRuntimeV1.CurrentPlaySeconds;
                rightDuration.text = string.Format("遊玩時間 {0:00}:{1:00}:{2:00}",
                    total / 3600, (total / 60) % 60, total % 60);
            }
            if (progressFill != null)
            {
                progressFill.type = Image.Type.Simple;
                progressFill.enabled = percent > 0.001f;
                progressFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    218f * percent);
            }
            if (loadButton != null) loadButton.interactable = found;
            if (continueButton != null) continueButton.interactable = SDGSaveProgressRuntimeV1.NewestSlot() >= 0;
        }

        private static float Progress(int completedMask)
        {
            int total = 0;
            for (int i = 0; i < 4; i++) if ((completedMask & (1 << i)) != 0) total++;
            return total / 4f;
        }
    }
}
