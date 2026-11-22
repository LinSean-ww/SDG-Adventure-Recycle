using UnityEngine;
using UnityEngine.UI;
using SDGAdventure.SharedPlayer;

namespace SDGAdventure.Experience
{
    internal static class SDGExperienceUI
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Text Text(Transform parent, string name, string value, int size,
            Vector2 position, Vector2 dimensions, Color color)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = PlayerGuiFont.Load();
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
