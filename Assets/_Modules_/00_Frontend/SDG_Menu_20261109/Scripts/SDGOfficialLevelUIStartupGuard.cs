using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd
{
    /// <summary>
    /// Startup-only guard for level scenes. Does not destroy menus or take over ESC handling.
    /// Only hides overlays whose visible text matches a known developer or pause menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SDGOfficialLevelUIStartupGuard : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // Some imported scenes enter paused because their overlay was initially active.
            // Reset only during initial scene startup; the level's own pause manager handles ESC later.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                HideKnownOverlays(gameObject.scene, true);
                Time.timeScale = 1f;
                yield return new WaitForSecondsRealtime(0.10f);
            }
        }

        public static int HideKnownOverlays(Scene scene, bool log)
        {
            int count = 0;
            GameObject dev = FindDeveloperOverlay(scene);
            if (dev != null && dev.activeSelf)
            {
                dev.SetActive(false);
                count++;
                if (log) Debug.Log("[SDG Official UI] Disabled developer selection overlay: " + PathOf(dev.transform));
            }
            GameObject pause = FindPauseOverlay(scene);
            if (pause != null && pause.activeSelf)
            {
                pause.SetActive(false);
                count++;
                if (log) Debug.Log("[SDG Official UI] Hid initial pause overlay: " + PathOf(pause.transform));
            }
            return count;
        }

        public static GameObject FindDeveloperOverlay(Scene scene)
        {
            List<Label> labels = GetLabels(scene);
            Transform title = FindLabel(labels, "M6 開發驗證選關入口", false);
            Transform action = FindLabel(labels, "進入 C3_TreasurePuzzle", false);
            if (title == null || action == null)
            {
                // Alternate wording of the development-only heading.
                title = title ?? FindLabel(labels, "僅供整合驗證", false);
                action = action ?? FindLabel(labels, "進入第一關", false);
            }
            return SafePanel(title, action);
        }

        public static GameObject FindPauseOverlay(Scene scene)
        {
            List<Label> labels = GetLabels(scene);
            Transform title = FindLabel(labels, "暫停", true);
            Transform restart = FindLabel(labels, "重新開始本關", false);
            Transform resume = FindLabel(labels, "繼續遊戲", false);
            if (title == null || restart == null || resume == null) return null;
            GameObject panel = SafePanel(title, restart);
            if (panel == null || !resume.IsChildOf(panel.transform)) return null;
            return panel;
        }

        private struct Label
        {
            public Transform transform;
            public string text;
        }

        private static List<Label> GetLabels(Scene scene)
        {
            List<Label> result = new List<Label>();
            if (!scene.IsValid() || !scene.isLoaded) return result;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                    if (text != null) result.Add(new Label { transform = text.transform, text = text.text });
                foreach (Text text in root.GetComponentsInChildren<Text>(true))
                    if (text != null) result.Add(new Label { transform = text.transform, text = text.text });
            }
            return result;
        }

        private static Transform FindLabel(List<Label> labels, string token, bool exact)
        {
            foreach (Label label in labels)
            {
                if (string.IsNullOrEmpty(label.text)) continue;
                string text = label.text.Trim();
                if (exact ? text == token : text.IndexOf(token, StringComparison.Ordinal) >= 0)
                    return label.transform;
            }
            return null;
        }

        // Returns the smallest UI grouping shared by two labels. Avoids deactivating
        // an entire mixed gameplay HUD just because it happens to contain menu labels.
        private static GameObject SafePanel(Transform first, Transform second)
        {
            if (first == null || second == null) return null;
            Transform common = null;
            for (Transform current = first; current != null; current = current.parent)
            {
                if (second.IsChildOf(current) || second == current) { common = current; break; }
            }
            if (common == null || common.GetComponent<RectTransform>() == null) return null;
            if (common.GetComponent<Canvas>() != null)
            {
                // A whole Canvas is only safe to hide when it is clearly a dedicated menu.
                string name = common.name.ToLowerInvariant();
                if (!(name.Contains("pause") || name.Contains("dev") || name.Contains("m6") ||
                      name.Contains("select") || name.Contains("menu"))) return null;
            }
            return common.gameObject;
        }

        public static string PathOf(Transform t)
        {
            if (t == null) return "(not found)";
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
