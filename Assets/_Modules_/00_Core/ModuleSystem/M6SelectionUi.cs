using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using SDGAdventure.SharedPlayer;

namespace SDGAdventure.ModuleSystem
{
    // Development selection UI uses UGUI to avoid IMGUI's TextCore cache across scene reloads.
    public sealed class M6SelectionUi : MonoBehaviour
    {
        private GameObject canvasObject;
        private RectTransform panelRect, launchRect;
        private Text result, error;
        private ModuleCoordinator coordinator;

        public void Show(ModuleCoordinator owner)
        {
            coordinator = owner;
            if (canvasObject == null) Build();
            canvasObject.SetActive(true);
        }

        public void Hide() { if (canvasObject != null) canvasObject.SetActive(false); }

        private void Update()
        {
            if (!canvasObject || !canvasObject.activeSelf || !coordinator) return;
            float width = Mathf.Min(600, Screen.width - 32);
            panelRect.sizeDelta = new Vector2(width, 290);
            launchRect.sizeDelta = new Vector2(width - 40, 50);
            ModuleResult? last = coordinator.LastResult;
            result.text = last.HasValue ? "最近結果：" + last.Value.ModuleId + (last.Value.Completed ? " 通關" : " 未完成") : "";
        }

        private void Build()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject events = new GameObject("M6EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            canvasObject = new GameObject("M6SelectionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(canvasObject.transform, false);
            panelRect = (RectTransform)panel.transform; panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0, -40); panelRect.sizeDelta = new Vector2(600, 290); panel.GetComponent<Image>().color = new Color(0.22f, 0.22f, 0.22f, 0.95f);
            Font font = PlayerGuiFont.Load();
            AddText(panel.transform, font, "SDG Adventure｜M6 開發驗證選關入口", 14, 130, 28);
            AddText(panel.transform, font, "僅供整合驗證；不是正式遊戲 UI。", 14, 81, 44);
            GameObject buttonObject = new GameObject("Launch", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panel.transform, false); launchRect = (RectTransform)buttonObject.transform;
            launchRect.anchorMin = launchRect.anchorMax = launchRect.pivot = new Vector2(0.5f, 0.5f);
            launchRect.anchoredPosition = new Vector2(0, 26); launchRect.sizeDelta = new Vector2(560, 50);
            buttonObject.GetComponent<Image>().color = new Color(0.42f, 0.42f, 0.42f);
            buttonObject.GetComponent<Button>().onClick.AddListener(() =>
            { if (!coordinator.TryLaunch("c3_treasure_puzzle", out string message)) error.text = message; });
            Text buttonText = AddText(buttonObject.transform, font, "進入 C3_TreasurePuzzle", 14, 0, 50);
            buttonText.rectTransform.sizeDelta = new Vector2(-20, 50);
            result = AddText(panel.transform, font, "", 14, -37, 44);
            error = AddText(panel.transform, font, "", 14, -95, 50);
        }

        private static Text AddText(Transform parent, Font font, string value, int size, float y, float height)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform; rect.anchorMin = new Vector2(0, 0.5f); rect.anchorMax = new Vector2(1, 0.5f); rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, y); rect.sizeDelta = new Vector2(-40, height);
            Text text = go.GetComponent<Text>(); text.font = font; text.fontSize = size; text.text = value;
            text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false; return text;
        }
    }
}
