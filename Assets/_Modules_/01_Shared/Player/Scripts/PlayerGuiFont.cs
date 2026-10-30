using System;
using UnityEngine;

namespace SDGAdventure.SharedPlayer
{
    public static class PlayerGuiFont
    {
        private static Font font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode() => font = null;

        public static Font Load()
        {
            if (font == null) font = Resources.Load<Font>("SharedPlayer/Fonts/NotoSansCJKtc-Regular");
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch (ArgumentException exception)
                {
                    Debug.LogWarning("SharedPlayer fallback font is unavailable: " + exception.Message);
                }
            }
            return font;
        }
    }
}
