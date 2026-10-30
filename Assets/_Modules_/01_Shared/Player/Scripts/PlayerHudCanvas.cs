using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.SharedPlayer
{
    // The gameplay HUD uses uGUI so it never participates in IMGUI font/style caching.
    public sealed class PlayerHudCanvas : MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform statusPanel;
        private Text status;
        private Text prompt;

        public string StatusText => status == null ? "" : status.text;
        public string PromptText => prompt == null ? "" : prompt.text;
        public bool IsVisible => canvas != null && canvas.enabled;

        public void Build()
        {
            if (canvas != null) return;
            Font font = PlayerGuiFont.Load();
            GameObject host = new GameObject("PlayerHudCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler));
            host.transform.SetParent(transform, false);
            canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            host.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            statusPanel = NewRect("StatusPanel", host.transform);
            statusPanel.anchorMin = statusPanel.anchorMax = new Vector2(0f, 1f);
            statusPanel.pivot = new Vector2(0f, 1f);
            statusPanel.anchoredPosition = new Vector2(14f, -14f);
            statusPanel.sizeDelta = new Vector2(610f, 196f);
            Image background = statusPanel.gameObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.13f, 0.2f, 0.85f);
            background.raycastTarget = false;
            status = NewText("Status", statusPanel, font, 16, TextAnchor.UpperLeft);
            Stretch(status.rectTransform, 10f, 10f, 8f, 8f);
            status.text = "";
            statusPanel.gameObject.SetActive(false);

            Text crosshair = NewText("Crosshair", host.transform, font, 26, TextAnchor.MiddleCenter);
            crosshair.rectTransform.anchorMin = crosshair.rectTransform.anchorMax =
                new Vector2(0.5f, 0.5f);
            crosshair.rectTransform.sizeDelta = new Vector2(40f, 40f);
            crosshair.rectTransform.anchoredPosition = Vector2.zero;
            crosshair.text = "+";

            prompt = NewText("InteractionPrompt", host.transform, font, 18, TextAnchor.MiddleCenter);
            prompt.rectTransform.anchorMin = prompt.rectTransform.anchorMax =
                new Vector2(0.5f, 0.5f);
            prompt.rectTransform.sizeDelta = new Vector2(460f, 72f);
            prompt.rectTransform.anchoredPosition = new Vector2(0f, -56f);
            prompt.text = "";
        }

        public void SetStatus(string value)
        {
            Build();
            status.text = value ?? "";
            statusPanel.gameObject.SetActive(!string.IsNullOrWhiteSpace(status.text));
        }

        public void SetPrompt(string value)
        {
            Build();
            prompt.text = value ?? "";
        }

        private void Update()
        {
            if (canvas == null) return;
            PlayerInputService input = PlayerInputService.Instance;
            canvas.enabled = input != null && input.Context == PlayerInputContext.Gameplay;
            statusPanel.sizeDelta = new Vector2(Mathf.Min(610f, Mathf.Max(0f, Screen.width - 28f)),
                Mathf.Min(196f, Mathf.Max(0f, Screen.height - 28f)));
            prompt.rectTransform.sizeDelta = new Vector2(
                Mathf.Min(460f, Mathf.Max(0f, Screen.width - 28f)), 72f);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Text NewText(string name, Transform parent, Font font, int size,
            TextAnchor alignment)
        {
            RectTransform rect = NewRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
