using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd
{
    /// <summary>
    /// Displays actual reported player HP in the original main-menu health rail.
    /// Existing avatar and editable name code remain untouched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SDGFrontEndPlayerVitals : MonoBehaviour
    {
        public const string HealthKey = "SDG_FE_PlayerHealth";
        public const string MaxHealthKey = "SDG_FE_PlayerHealthMax";

        public Text healthCount;
        public Image healthFill;
        private int previousHealth = -1;
        private int previousMaximum = -1;
        private float nextRefresh;

        public static void ReportHealth(int current, int maximum)
        {
            maximum = Mathf.Max(1, maximum);
            current = Mathf.Clamp(current, 0, maximum);
            if (PlayerPrefs.GetInt(HealthKey, 100) == current &&
                PlayerPrefs.GetInt(MaxHealthKey, 100) == maximum &&
                PlayerPrefs.HasKey(HealthKey)) return;
            PlayerPrefs.SetInt(HealthKey, current);
            PlayerPrefs.SetInt(MaxHealthKey, maximum);
            PlayerPrefs.Save();
        }

        private void OnEnable()
        {
            previousHealth = -1;
            previousMaximum = -1;
            Refresh();
            nextRefresh = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.2f;
            Refresh();
        }

        private void Refresh()
        {
            int maximum = Mathf.Max(1, PlayerPrefs.GetInt(MaxHealthKey, 100));
            int current = Mathf.Clamp(PlayerPrefs.GetInt(HealthKey, maximum), 0, maximum);
            if (current == previousHealth && maximum == previousMaximum) return;
            previousHealth = current;
            previousMaximum = maximum;
            if (healthCount != null) healthCount.text = current + "/" + maximum;
            if (healthFill != null)
            {
                // Use real geometry width: UI Image without a source sprite does not
                // reliably honor fillAmount in Unity 2023.1.
                healthFill.type = Image.Type.Simple;
                RectTransform fillRect = healthFill.rectTransform;
                fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    290f * Mathf.Clamp01((float)current / maximum));
                healthFill.color = current <= maximum * 0.25f
                    ? new Color(1f, 0.20f, 0.13f, 0.93f)
                    : new Color(0.22f, 1f, 0.46f, 0.87f);
            }
        }
    }
}
