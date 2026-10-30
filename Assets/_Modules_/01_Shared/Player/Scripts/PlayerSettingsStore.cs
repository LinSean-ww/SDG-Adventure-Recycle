using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SDGAdventure.SharedPlayer
{
    // Stores only user overrides. The .inputactions asset remains the source of defaults.
    public sealed class PlayerSettingsStore
    {
        public const string DefaultKey = "SDGAdventure.Player.Settings.v1";
        private readonly string key;

        public PlayerSettingsStore(string key = DefaultKey)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A settings key is required.", nameof(key));
            this.key = key;
        }

        public PlayerSettingsData Load(InputActionAsset actions)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            actions.RemoveAllBindingOverrides();
            if (!PlayerPrefs.HasKey(key)) return new PlayerSettingsData();

            try
            {
                PlayerSettingsData data = JsonUtility.FromJson<PlayerSettingsData>(PlayerPrefs.GetString(key));
                if (data == null || (data.version != 1 && data.version != PlayerSettingsData.CurrentVersion) ||
                    !ValidMode(data.sprintMode) || !ValidMode(data.crouchMode) ||
                    (data.version == PlayerSettingsData.CurrentVersion &&
                     (float.IsNaN(data.mouseSensitivity) || data.mouseSensitivity < 0.1f || data.mouseSensitivity > 10f)) ||
                    data.bindingOverridesJson == null || data.checksum != Checksum(data))
                    throw new FormatException("Invalid or unsupported player settings.");
                if (data.bindingOverridesJson.Length > 0)
                    actions.LoadBindingOverridesFromJson(data.bindingOverridesJson);
                if (data.version == 1)
                {
                    data.mouseSensitivity = 2f;
                    Save(actions, data);
                }
                return data;
            }
            catch (Exception exception)
            {
                actions.RemoveAllBindingOverrides();
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                Debug.LogWarning("Player settings were invalid and were reset to defaults: " + exception.Message);
                return new PlayerSettingsData();
            }
        }

        public void Save(InputActionAsset actions, PlayerSettingsData data)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (data == null || !ValidMode(data.sprintMode) || !ValidMode(data.crouchMode) ||
                float.IsNaN(data.mouseSensitivity) || data.mouseSensitivity < 0.1f || data.mouseSensitivity > 10f)
                throw new ArgumentException("Invalid player settings.", nameof(data));
            data.version = PlayerSettingsData.CurrentVersion;
            data.bindingOverridesJson = actions.SaveBindingOverridesAsJson();
            data.checksum = Checksum(data);
            PlayerPrefs.SetString(key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public void Reset(InputActionAsset actions)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        private static bool ValidMode(ButtonMode mode) => mode == ButtonMode.Hold || mode == ButtonMode.Toggle;

        private static string Checksum(PlayerSettingsData data)
        {
            string value = data.version + "|" + (int)data.sprintMode + "|" + (int)data.crouchMode +
                           "|" + data.bindingOverridesJson;
            if (data.version >= 2) value += "|" + data.mouseSensitivity.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
    }
}
