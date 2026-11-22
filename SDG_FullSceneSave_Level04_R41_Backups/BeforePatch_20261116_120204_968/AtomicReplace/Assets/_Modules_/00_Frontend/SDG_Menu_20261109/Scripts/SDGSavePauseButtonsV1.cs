using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd
{
    /// <summary>Adds two non-invasive actions to the existing runtime pause menu.
    /// No edits to PlayerPauseMenu.cs, PlayerMenuCanvas.cs or the four level scenes.</summary>
    public sealed class SDGSavePauseButtonsV1 : MonoBehaviour
    {
        private float nextScan;

        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.15f;
            foreach (PlayerPauseMenu pause in FindObjectsOfType<PlayerPauseMenu>())
            {
                if (pause == null || pause.State != PlayerMenuState.Paused ||
                    pause.View == null || !pause.View.IsVisible || pause.View.Panel == null) continue;
                Transform content = pause.View.Panel.Find("ScrollViewport/ScrollContent");
                if (content == null || content.Find("SDGSaveManualButtonV1") != null) continue;
                Font font = null;
                foreach (Text sibling in content.GetComponentsInChildren<Text>(true))
                    if (sibling != null && sibling.font != null) { font = sibling.font; break; }
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                int slot = Mathf.Max(1, SDGSaveProgressRuntimeV1.SelectedSlot);
                MakeButton(content, "SDGSaveManualButtonV1", "儲存進度（手動 " + slot + "）", font, () =>
                {
                    int target = Mathf.Max(1, SDGSaveProgressRuntimeV1.SelectedSlot);
                    SDGSaveProgressRuntimeV1.SelectedSlot = target;
                    bool ok = SDGSaveProgressRuntimeV1.TrySave(target, out string text);
                    Debug.Log((ok ? "[SDG Progress] " : "[SDG Progress] FAILED: ") + text);
                    UpdateLabel(content, "SDGSaveManualButtonV1", text);
                });
                MakeButton(content, "SDGSaveLoadButtonV1", "讀取進度（所選欄位）", font, () =>
                {
                    int target = SDGSaveProgressRuntimeV1.SelectedSlot;
                    if (target < 0) target = SDGSaveProgressRuntimeV1.NewestSlot();
                    if (!SDGSaveProgressRuntimeV1.TryLoad(target, out string text))
                    {
                        Debug.LogWarning("[SDG Progress] " + text);
                        UpdateLabel(content, "SDGSaveLoadButtonV1", text);
                    }
                });
            }
        }

        private static void UpdateLabel(Transform content, string name, string value)
        {
            Transform child = content.Find(name);
            if (child == null) return;
            Text t = child.GetComponentInChildren<Text>();
            if (t != null) t.text = value;
        }

        private static void MakeButton(Transform parent, string name, string text, Font font,
                                       UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var layout = go.GetComponent<LayoutElement>();
            layout.preferredHeight = 38f;
            var image = go.GetComponent<Image>();
            image.color = new Color(0.03f, 0.27f, 0.24f, 0.97f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            RectTransform rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text label = textGo.GetComponent<Text>();
            label.text = text;
            label.font = font;
            label.fontSize = 17;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.82f, 1f, 0.9f, 1f);
            label.raycastTarget = false;
        }
    }
}
