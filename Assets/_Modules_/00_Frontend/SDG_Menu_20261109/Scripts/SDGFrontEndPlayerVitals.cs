using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd
{
    /// <summary>
    /// Player health display on the existing front-end panel.
    /// XP text is owned by SDGFrontEndController, and is deliberately not overwritten by HP.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SDGFrontEndPlayerVitals : MonoBehaviour
    {
        public const string HealthKey = "SDG_FE_PlayerHealth";
        public const string MaxHealthKey = "SDG_FE_PlayerHealthMax";

        public Text healthCount; // v2 compatibility only; v3 sets to null
        public Image healthFill;
        public float healthFillMaxWidth = 290f;

        private int previousHealth = -1;
        private int previousMaximum = -1;
        private float nextRefresh;

        public static void ReportHealth(int current, int maximum)
        {
            maximum = Mathf.Max(1, maximum);
            current = Mathf.Clamp(current, 0, maximum);
            if (PlayerPrefs.HasKey(HealthKey) &&
                PlayerPrefs.GetInt(HealthKey) == current &&
                PlayerPrefs.GetInt(MaxHealthKey, 100) == maximum) return;
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
            nextRefresh = Time.unscaledTime + .2f;
            Refresh();
        }

        private void Refresh()
        {
            int maximum = Mathf.Max(1, PlayerPrefs.GetInt(MaxHealthKey, 100));
            int current = Mathf.Clamp(PlayerPrefs.GetInt(HealthKey, maximum), 0, maximum);
            if (previousHealth == current && previousMaximum == maximum) return;
            previousHealth = current;
            previousMaximum = maximum;
            if (healthCount != null) healthCount.text = current + "/" + maximum;
            if (healthFill != null)
            {
                healthFill.type = Image.Type.Simple;
                RectTransform fill = healthFill.rectTransform;
                fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    Mathf.Max(0f, healthFillMaxWidth) * Mathf.Clamp01((float)current / maximum));
                healthFill.color = current <= maximum * .25f
                    ? new Color(1f, .2f, .13f, .93f)
                    : new Color(.25f, 1f, .52f, .87f);
            }
        }
    }
}
