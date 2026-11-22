using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd
{
    /// <summary>
    /// R4.2: pick manual slots 1/2/3 directly in the pause menu, with
    /// explicit two-click overwrite confirmation. Does not fabricate
    /// Level03 world-state serialization where no adapter exists.
    /// </summary>
    public sealed class SDGSavePauseButtonsV1 : MonoBehaviour
    {
        private float nextScan;
        private int confirmSlot = -1;
        private float confirmExpires;

        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + .15f;
            foreach (PlayerPauseMenu pause in FindObjectsOfType<PlayerPauseMenu>())
            {
                if (pause == null || pause.State != PlayerMenuState.Paused ||
                    pause.View == null || !pause.View.IsVisible || pause.View.Panel == null) continue;
                Transform content = pause.View.Panel.Find("ScrollViewport/ScrollContent");
                if (!content) continue;
                Font font = null;
                foreach (Text label in content.GetComponentsInChildren<Text>(true))
                    if (label && label.font) { font = label.font; break; }
                if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                if (!content.Find("SDGSaveSlotChooseR42"))
                    MakeButton(content, "SDGSaveSlotChooseR42", "手動欄位：1 / 3", font, () =>
                    {
                        int current = CurrentManualSlot();
                        SDGSaveProgressRuntimeV1.SelectedSlot = current == 3 ? 1 : current + 1;
                        confirmSlot = -1;
                        RefreshLabels(content);
                    });
                if (!content.Find("SDGSaveManualButtonV1"))
                    MakeButton(content, "SDGSaveManualButtonV1", "儲存進度", font, () => SaveSelected(content));
                if (!content.Find("SDGSaveLoadButtonV1"))
                    MakeButton(content, "SDGSaveLoadButtonV1", "讀取進度", font, () => LoadSelected(content));
                RefreshLabels(content);
            }
        }

        private static int CurrentManualSlot()
        {
            int slot = SDGSaveProgressRuntimeV1.SelectedSlot;
            return slot >= 1 && slot <= 3 ? slot : 1;
        }

        private static int CurrentLevel()
        {
            string path = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path.Replace('\\', '/');
            for (int i = 1; i <= 4; i++)
                if (path.IndexOf("/02_Levels/Level0" + i + "/", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            return 0;
        }

        private void SaveSelected(Transform content)
        {
            int slot = CurrentManualSlot();
            int level = CurrentLevel();
            if (SDGSaveProgressRuntimeV1.TryReadSlot(slot, out SDGProgressRecordV1 existing))
            {
                if (confirmSlot != slot || Time.unscaledTime > confirmExpires)
                {
                    confirmSlot = slot;
                    confirmExpires = Time.unscaledTime + 8f;
                    string oldLevel = existing.levelNumber > 0 ? "（原第" + existing.levelNumber + "關）" : "";
                    UpdateLabel(content, "SDGSaveManualButtonV1", "覆寫手動 " + slot + oldLevel + "？再按一次");
                    Debug.LogWarning("[SDG Save R4.2] Overwrite confirmation requested: manual slot " + slot + ", old level " + existing.levelNumber + ", current level " + level);
                    return;
                }
            }
            confirmSlot = -1;
            SDGSaveProgressRuntimeV1.SelectedSlot = slot;
            bool ok = SDGSaveProgressRuntimeV1.TrySave(slot, out string message);
            Debug.Log((ok ? "[SDG Save R4.2] " : "[SDG Save R4.2] FAILED: ") + message);
            // Save service currently has no full Level03 world adapter. Never
            // present HP/position-only saves as an exact scene checkpoint.
            if (level == 3)
                Debug.LogWarning("[SDG Save R4.2] Level03 is not supported as a full world snapshot by R4.1. Inspect its quest/production/energy state adapter before accepting a checkpoint.");
            UpdateLabel(content, "SDGSaveManualButtonV1", ok ? "手動 " + slot + " 儲存完成" : "儲存失敗，請查看 Console");
            RefreshLabels(content, false);
        }

        private static void LoadSelected(Transform content)
        {
            int slot = CurrentManualSlot();
            if (!SDGSaveProgressRuntimeV1.TryLoad(slot, out string message))
            {
                Debug.LogWarning("[SDG Save R4.2] LOAD FAILED slot=" + slot + " " + message);
                UpdateLabel(content, "SDGSaveLoadButtonV1", "讀取失敗，請查看 Console");
            }
        }

        private void RefreshLabels(Transform content, bool refreshAction = true)
        {
            int slot = CurrentManualSlot();
            string old = "空白";
            if (SDGSaveProgressRuntimeV1.TryReadSlot(slot, out SDGProgressRecordV1 found))
                old = "第 " + found.levelNumber + " 關";
            UpdateLabel(content, "SDGSaveSlotChooseR42", "手動欄位 " + slot + "/3｜" + old + "（點擊切換）");
            if (refreshAction && confirmSlot != slot)
                UpdateLabel(content, "SDGSaveManualButtonV1", "儲存進度（手動 " + slot + "）");
            if (refreshAction && confirmSlot == slot && Time.unscaledTime > confirmExpires)
            {
                confirmSlot = -1;
                UpdateLabel(content, "SDGSaveManualButtonV1", "儲存進度（手動 " + slot + "）");
            }
            if (refreshAction)
                UpdateLabel(content, "SDGSaveLoadButtonV1", "讀取進度（手動 " + slot + "）");
        }

        private static void UpdateLabel(Transform content, string objectName, string text)
        {
            Transform found = content.Find(objectName);
            if (!found) return;
            Text label = found.GetComponentInChildren<Text>();
            if (label) label.text = text;
        }

        private static void MakeButton(Transform parent, string name, string text, Font font,
            UnityEngine.Events.UnityAction click)
        {
            GameObject host = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            host.transform.SetParent(parent, false);
            host.GetComponent<LayoutElement>().preferredHeight = 38f;
            var img = host.GetComponent<Image>();
            img.color = new Color(.03f, .27f, .24f, .97f);
            var button = host.GetComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(click);
            GameObject caption = new GameObject("Label", typeof(RectTransform), typeof(Text));
            caption.transform.SetParent(host.transform, false);
            RectTransform rect = caption.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text label = caption.GetComponent<Text>();
            label.text = text;
            label.font = font;
            label.fontSize = 17;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(.82f, 1f, .9f, 1f);
            label.raycastTarget = false;
        }
    }
}
