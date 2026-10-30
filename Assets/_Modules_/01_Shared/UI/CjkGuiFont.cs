using UnityEngine;

namespace SDGAdventure.UI
{
    // Legacy IMGUI does not reliably inherit a CJK font in Windows Player.
    // The font is a project asset in Resources, so it is bundled with the build.
    public static class CjkGuiFont
    {
        public const string ResourcePath = "Fonts/NotoSansCJKtc-Regular";
        private static Font font;
        private static bool loadAttempted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            font = null;
            loadAttempted = false;
        }

        public static Font Asset
        {
            get
            {
                if (font == null)
                {
                    font = Resources.Load<Font>(ResourcePath);
                    if (font == null && !loadAttempted)
                        Debug.LogError("Required Traditional Chinese GUI font is missing: Resources/" + ResourcePath);
                    loadAttempted = true;
                }
                return font;
            }
        }

        public static void Apply()
        {
            Font selected = Asset;
            if (selected == null) return;
            GUISkin skin = GUI.skin;
            skin.font = selected;
            skin.label.font = selected;
            skin.button.font = selected;
            skin.box.font = selected;
            skin.window.font = selected;
            skin.textField.font = selected;
            skin.textArea.font = selected;
            skin.toggle.font = selected;
        }
    }
}
