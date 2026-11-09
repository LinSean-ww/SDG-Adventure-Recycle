using System;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.UI;

public sealed class Level02CompletionView : MonoBehaviour
{
    public void Show(Action onReturn, Action onRetry)
    {
        var root = new GameObject("Level02CompletionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 600;
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image)); background.transform.SetParent(root.transform, false);
        var rect = (RectTransform)background.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0, 0, 0, .8f);
        Label(background.transform, "第二關完成！", 100, 26);
        Button(background.transform, "ReturnToSelection", "返回選關", 20, onReturn);
        Button(background.transform, "Retry", "重新挑戰", -45, onRetry);
    }
    private static Text Label(Transform parent, string text, float y, int size)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f); rect.anchoredPosition = new Vector2(0,y); rect.sizeDelta = new Vector2(360,45);
        var label = go.GetComponent<Text>(); label.font = PlayerGuiFont.Load(); label.text = text; label.fontSize = size; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label;
    }
    private static void Button(Transform parent, string name, string label, float y, Action action)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f); rect.anchoredPosition = new Vector2(0,y); rect.sizeDelta = new Vector2(360,45);
        go.GetComponent<Image>().color = new Color(.22f,.31f,.41f,1); go.GetComponent<Button>().onClick.AddListener(() => action?.Invoke()); Label(go.transform, label, 0, 18);
    }
}
