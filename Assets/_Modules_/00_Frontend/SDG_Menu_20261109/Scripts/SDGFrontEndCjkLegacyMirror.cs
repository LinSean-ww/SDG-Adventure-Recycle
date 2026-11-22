using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd
{
    /// <summary>
    /// Mirrors the TMP value into a Unity UI Text using a TrueType/OpenType font.
    /// The original TMP component remains for controller references, but is not rendered.
    /// This bypasses the failing TMP 3.0.6 dynamic atlas creation path.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SDGFrontEndCjkLegacyMirror : MonoBehaviour
    {
        public TextMeshProUGUI source;
        public Text legacyText;

        private void OnEnable() { Refresh(); }
        private void LateUpdate() { Refresh(); }

        private void Refresh()
        {
            if (source == null || legacyText == null) return;
            if (legacyText.text != source.text) legacyText.text = source.text;
            if (legacyText.color != source.color) legacyText.color = source.color;
            int size = Mathf.Max(1, Mathf.RoundToInt(source.fontSize));
            if (legacyText.fontSize != size) legacyText.fontSize = size;
            var alignment = GetAlignment(source.alignment);
            if (legacyText.alignment != alignment) legacyText.alignment = alignment;
        }

        private static TextAnchor GetAlignment(TextAlignmentOptions alignment)
        {
            switch (alignment)
            {
                case TextAlignmentOptions.Left: return TextAnchor.MiddleLeft;
                case TextAlignmentOptions.Right: return TextAnchor.MiddleRight;
                case TextAlignmentOptions.TopLeft: return TextAnchor.UpperLeft;
                case TextAlignmentOptions.Top: return TextAnchor.UpperCenter;
                case TextAlignmentOptions.TopRight: return TextAnchor.UpperRight;
                case TextAlignmentOptions.BottomLeft: return TextAnchor.LowerLeft;
                case TextAlignmentOptions.Bottom: return TextAnchor.LowerCenter;
                case TextAlignmentOptions.BottomRight: return TextAnchor.LowerRight;
                default: return TextAnchor.MiddleCenter;
            }
        }
    }
}
