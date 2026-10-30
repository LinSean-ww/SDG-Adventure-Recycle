using System;

namespace SDGAdventure.SharedPlayer
{
    public enum ButtonMode { Hold = 0, Toggle = 1 }

    [Serializable]
    public sealed class PlayerSettingsData
    {
        public const int CurrentVersion = 2;
        public int version = CurrentVersion;
        public ButtonMode sprintMode = ButtonMode.Hold;
        public ButtonMode crouchMode = ButtonMode.Hold;
        public float mouseSensitivity = 2f;
        public string bindingOverridesJson = "";
        public string checksum = "";
    }
}
