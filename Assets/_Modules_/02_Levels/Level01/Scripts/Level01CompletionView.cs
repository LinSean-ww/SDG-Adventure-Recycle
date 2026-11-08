using System;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.UI;

public sealed class Level01CompletionView : MonoBehaviour
{
    private GameObject overlay;
    public bool Visible => overlay != null && overlay.activeSelf;

    public void Show(Action returnToSelection, Action retry)
    {
        if (overlay != null) return;
        var canvasObject = new GameObject("Level01CompletionCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        overlay = canvasObject;
        var dim = CreateRect("Overlay", canvasObject.transform);
        dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one; dim.offsetMin = dim.offsetMax = Vector2.zero;
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        var panel = CreateRect("Panel", dim);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(440f, 300f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.11f, 0.15f, 0.21f, 1f);
        var heading = AddText("Heading", panel, "第一關完成！", 26);
        Place(heading.rectTransform, 0f, 100f, 400f, 48f);
        AddButton(panel, "ReturnToSelection", "返回選關", 40f, returnToSelection, true);
        AddButton(panel, "Retry", "重新挑戰", -18f, retry, true);
        AddButton(panel, "NextLevel", "下一關（尚未開放）", -76f, null, false);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }
    private static Text AddText(string name, Transform parent, string value, int size)
    {
        var rect = CreateRect(name, parent);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = PlayerGuiFont.Load(); text.text = value; text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false;
        return text;
    }
    private static void AddButton(Transform parent, string name, string label, float y, Action callback, bool enabled)
    {
        var rect = CreateRect(name, parent); Place(rect, 0f, y, 360f, 46f);
        rect.gameObject.AddComponent<Image>().color = new Color(0.22f, 0.31f, 0.41f, 1f);
        var button = rect.gameObject.AddComponent<Button>(); button.interactable = enabled;
        if (callback != null) button.onClick.AddListener(() => callback());
        var text = AddText("Label", rect, label, 18);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
    }
}
