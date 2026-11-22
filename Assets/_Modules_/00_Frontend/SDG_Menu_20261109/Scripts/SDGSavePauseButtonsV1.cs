using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd
{
    /// <summary>Pause-menu entry points only; slot choice and confirmation belong to the V5 dialog.</summary>
    public sealed class SDGSavePauseButtonsV1 : MonoBehaviour
    {
        private float nextScan;
        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + .25f;
            foreach (PlayerPauseMenu pause in FindObjectsOfType<PlayerPauseMenu>())
            {
                if (!pause || pause.State != PlayerMenuState.Paused ||
                    pause.View == null || !pause.View.IsVisible || !pause.View.Panel) continue;
                Transform content = pause.View.Panel.Find("ScrollViewport/ScrollContent");
                if (!content) continue;
                Font font = null;
                foreach (Text label in content.GetComponentsInChildren<Text>(true))
                    if (label && label.font) { font = label.font; break; }
                if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                // Remove the old three-slot cycling button if present (it was created dynamically).
                Transform obsolete = content.Find("SDGSaveSlotChooseR42");
                if (obsolete) Destroy(obsolete.gameObject);
                if (!content.Find("SDGSaveManualButtonV1"))
                    MakeButton(content, "SDGSaveManualButtonV1", "手動儲存：覆蓋或另存", font,
                        SDGSaveSlotsOverlayV5.OpenSave);
                if (!content.Find("SDGSaveLoadButtonV1"))
                    MakeButton(content, "SDGSaveLoadButtonV1", "讀取進度：選擇存檔", font,
                        SDGSaveSlotsOverlayV5.OpenLoad);
            }
        }
        private static void MakeButton(Transform parent, string name, string label, Font font,
            UnityEngine.Events.UnityAction click)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            host.transform.SetParent(parent, false);
            host.GetComponent<LayoutElement>().preferredHeight = 38f;
            Image img = host.GetComponent<Image>();
            img.color = new Color(.03f, .27f, .24f, .97f);
            Button button = host.GetComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(click);
            var caption = new GameObject("Label", typeof(RectTransform), typeof(Text));
            caption.transform.SetParent(host.transform, false);
            RectTransform rect = caption.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            Text text = caption.GetComponent<Text>();
            text.text = label; text.font = font; text.fontSize = 17;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.82f, 1f, .9f, 1f); text.raycastTarget = false;
        }
    }
}
