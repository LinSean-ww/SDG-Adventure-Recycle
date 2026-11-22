using System;
using UnityEngine;
using UnityEngine.UI;
using SDGAdventure.SharedPlayer;

/// <summary>
/// Adds only the missing Level02 continuation action; no scene or prefab changes.
/// If the existing completion UI has a Retry/NextLevel button, reuses that layout.
/// Otherwise creates a small overlay at the bottom of the completion screen.
/// </summary>
public static class SDGLevel02NextButtonV1
{
    public static void Attach(Transform host, Action next)
    {
        if (host == null || next == null) return;
        Canvas[] canvases = host.GetComponentsInChildren<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || !canvas.gameObject.activeInHierarchy) continue;
            Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
            Button existing = null, retry = null;
            foreach (Button b in buttons)
            {
                if (b == null) continue;
                if (b.name == "NextLevel") existing = b;
                if (b.name == "Retry" || b.name == "Restart") retry = b;
            }
            if (existing != null)
            {
                Configure(existing, "前往第三關", next);
                return;
            }
            if (retry != null)
            {
                GameObject copied = UnityEngine.Object.Instantiate(retry.gameObject, retry.transform.parent, false);
                copied.name = "SDG_NextLevelV1";
                RectTransform oldRect = retry.GetComponent<RectTransform>();
                RectTransform newRect = copied.GetComponent<RectTransform>();
                if (oldRect != null && newRect != null)
                    newRect.anchoredPosition = oldRect.anchoredPosition + new Vector2(0f, -58f);
                Button button = copied.GetComponent<Button>();
                Configure(button, "前往第三關", next);
                return;
            }
        }
        MakeFallback(host, next);
    }

    private static void Configure(Button button, string label, Action next)
    {
        if (button == null) return;
        button.interactable = true;
        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => next());
        Text legacy = button.GetComponentInChildren<Text>(true);
        if (legacy != null) legacy.text = label;
        else
        {
            // Avoid TMPro dependency for assemblies that do not already reference it.
            Debug.LogWarning("[SDG Next] Level02 next button has no legacy Text label; UI may need review.");
        }
    }

    private static void MakeFallback(Transform host, Action next)
    {
        GameObject canvasGo = new GameObject("SDG Level02 Next Canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(host, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 850;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject go = new GameObject("SDG_NextLevelV1", typeof(RectTransform),
            typeof(Image), typeof(Button));
        go.transform.SetParent(canvasGo.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 28f);
        rect.sizeDelta = new Vector2(320f, 52f);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.03f, 0.30f, 0.27f, 0.97f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => next());

        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(go.transform, false);
        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        Text text = labelGo.GetComponent<Text>();
        text.text = "前往第三關";
        text.font = PlayerGuiFont.Load();
        text.fontSize = 21;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        Debug.Log("[SDG Next] Level02 continuation is shown in a separate HUD; review visual alignment.");
    }
}
