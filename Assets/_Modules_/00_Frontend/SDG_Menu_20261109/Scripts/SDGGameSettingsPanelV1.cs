using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SDGAdventure.FrontEnd
{
    [DisallowMultipleComponent]
    public sealed class SDGGameSettingsPanelV1 : MonoBehaviour
    {
        public GameObject[] pages;
        public Button[] tabs;
        public Image[] tabBackgrounds;
        public TMP_Text[] tabLabels;
        public Slider master, music, effects, sensitivity;
        public TMP_Text masterValue, musicValue, effectsValue, sensitivityValue;
        public Button fullscreenButton, resolutionPrev, resolutionNext, qualityPrev, qualityNext;
        public Button invertButton, autoButton;
        public TMP_Text fullscreenValue, resolutionValue, qualityValue, invertValue, autoValue, status;
        public Button defaultsButton, backButton, applyButton;

        SDGSettingsDataV1 draft;
        readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        int selectedTab, resolutionIndex;
        void Awake()
        {
            if (tabs != null) for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                if (tabs[i] != null) tabs[i].onClick.AddListener(() => SelectTab(index));
            }
            if (master != null) master.onValueChanged.AddListener(v => { if (draft != null) { draft.master = v; Render(); } });
            if (music != null) music.onValueChanged.AddListener(v => { if (draft != null) { draft.music = v; Render(); } });
            if (effects != null) effects.onValueChanged.AddListener(v => { if (draft != null) { draft.effects = v; Render(); } });
            if (sensitivity != null) sensitivity.onValueChanged.AddListener(v => { if (draft != null) { draft.sensitivity = v; Render(); } });
            if (fullscreenButton != null) fullscreenButton.onClick.AddListener(() => { draft.fullscreen = !draft.fullscreen; Render(); });
            if (invertButton != null) invertButton.onClick.AddListener(() => { draft.invertY = !draft.invertY; Render(); });
            if (autoButton != null) autoButton.onClick.AddListener(() => { draft.autoSave = !draft.autoSave; Render(); });
            if (resolutionPrev != null) resolutionPrev.onClick.AddListener(() => SelectResolution(-1));
            if (resolutionNext != null) resolutionNext.onClick.AddListener(() => SelectResolution(1));
            if (qualityPrev != null) qualityPrev.onClick.AddListener(() => SelectQuality(-1));
            if (qualityNext != null) qualityNext.onClick.AddListener(() => SelectQuality(1));
            if (defaultsButton != null) defaultsButton.onClick.AddListener(Defaults);
            if (backButton != null) backButton.onClick.AddListener(() => gameObject.SetActive(false));
            if (applyButton != null) applyButton.onClick.AddListener(Save);
        }
        void OnEnable()
        {
            draft = SDGGameSettingsRuntimeV1.Load();
            BuildResolutions();
            SelectTab(selectedTab);
            if (status != null) status.text = "調整後按「套用設定」儲存";
        }
        void BuildResolutions()
        {
            resolutions.Clear();
            foreach (Resolution mode in Screen.resolutions)
            {
                if (mode.width < 960 || mode.height < 540) continue;
                Vector2Int item = new Vector2Int(mode.width, mode.height);
                if (!resolutions.Contains(item)) resolutions.Add(item);
            }
            Vector2Int current = new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            if (!resolutions.Contains(current)) resolutions.Add(current);
            Vector2Int selected = new Vector2Int(draft.width, draft.height);
            if (selected.x > 0 && selected.y > 0 && !resolutions.Contains(selected)) resolutions.Add(selected);
            resolutions.Sort((a,b) => (a.x * a.y).CompareTo(b.x * b.y));
            resolutionIndex = resolutions.IndexOf(selected);
            if (resolutionIndex < 0) resolutionIndex = resolutions.IndexOf(current);
            resolutionIndex = Mathf.Max(0, resolutionIndex);
        }
        void SelectResolution(int delta)
        {
            if (resolutions.Count == 0) return;
            resolutionIndex = (resolutionIndex + delta + resolutions.Count) % resolutions.Count;
            draft.width = resolutions[resolutionIndex].x;
            draft.height = resolutions[resolutionIndex].y;
            Render();
        }
        void SelectQuality(int delta)
        {
            int length = QualitySettings.names.Length;
            if (length <= 0) return;
            draft.quality = (Mathf.Clamp(draft.quality, 0, length - 1) + delta + length) % length;
            Render();
        }
        void SelectTab(int tab)
        {
            selectedTab = Mathf.Clamp(tab, 0, 3);
            if (pages != null) for (int i = 0; i < pages.Length; i++)
                if (pages[i] != null) pages[i].SetActive(i == selectedTab);
            if (tabBackgrounds != null) for (int i = 0; i < tabBackgrounds.Length; i++)
                if (tabBackgrounds[i] != null) tabBackgrounds[i].color = i == selectedTab
                    ? new Color(.035f, .43f, .38f, .98f) : new Color(.017f, .17f, .16f, .92f);
            Render();
        }
        void Render()
        {
            if (draft == null) return;
            if (master != null) master.SetValueWithoutNotify(draft.master);
            if (music != null) music.SetValueWithoutNotify(draft.music);
            if (effects != null) effects.SetValueWithoutNotify(draft.effects);
            if (sensitivity != null) sensitivity.SetValueWithoutNotify(draft.sensitivity);
            if (masterValue != null) masterValue.text = Mathf.RoundToInt(draft.master * 100) + "%";
            if (musicValue != null) musicValue.text = Mathf.RoundToInt(draft.music * 100) + "%";
            if (effectsValue != null) effectsValue.text = Mathf.RoundToInt(draft.effects * 100) + "%";
            if (sensitivityValue != null) sensitivityValue.text = draft.sensitivity.ToString("0.0") + "x";
            if (fullscreenValue != null) fullscreenValue.text = draft.fullscreen ? "開" : "關";
            if (resolutionValue != null) resolutionValue.text = resolutions.Count == 0 ? "目前解析度" :
                resolutions[resolutionIndex].x + " × " + resolutions[resolutionIndex].y;
            if (qualityValue != null) qualityValue.text = QualitySettings.names.Length == 0 ? "預設" :
                QualitySettings.names[Mathf.Clamp(draft.quality, 0, QualitySettings.names.Length - 1)];
            if (invertValue != null) invertValue.text = draft.invertY ? "開" : "關";
            if (autoValue != null) autoValue.text = draft.autoSave ? "開" : "關";
        }
        void Save()
        {
            SDGGameSettingsRuntimeV1.SaveAndApply(draft);
            if (status != null) status.text = "設定已儲存，切換關卡後仍會保留";
        }
        void Defaults()
        {
            draft = new SDGSettingsDataV1();
            draft.fullscreen = Screen.fullScreen;
            draft.quality = QualitySettings.GetQualityLevel();
            draft.width = Screen.width;
            draft.height = Screen.height;
            BuildResolutions();
            Render();
            if (status != null) status.text = "已載入預設值，按「套用設定」才會儲存";
        }
    }
}
