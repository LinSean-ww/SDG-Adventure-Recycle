#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd.Editor
{
    /// <summary>
    /// Fine-tunes ONLY the vertical position of the preexisting HP rail.
    /// Re-running the patch is idempotent and will not accumulate offsets.
    /// It does not touch the avatar, name, experience counter, or HP logic.
    /// </summary>
    public static class SDGFrontEndHealthBarCenterV33
    {
        private const string Root = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109";
        private const string ScenePath = Root + "/Scenes/SDG_StartAndMainMenu.unity";
        private const string ReportName = "SDG_PlayerProfile_BarCenterV33_Report.txt";
        private const string RailName = "Profile v2 - Health rail";
        private const string FillName = "Profile v2 - HP fill";

        // Original FE_Menu_PlayerPanel.png is 2048 x 682.
        // The v3 bar's flat green pixels appear ~2 screenshot pixels too high.
        // Move the rail 14 artwork pixels DOWN (about 2 px at the user's Game-view scale).
        private const float OriginalArtHeight = 682f;
        private const float OriginalArtRailCenterY = 520f; // v3 used (505 + 535) / 2
        private const float DownwardArtPixels = 14f;
        private const string Marker = "SDG PROFILE BAR CENTER V3.3 SUCCESS";

        [MenuItem("SDG Adventure/前端 UI/27 將生命條置中（僅調整垂直位置）")]
        public static void ApplyFromMenu()
        {
            try
            {
                Apply();
                EditorUtility.DisplayDialog("SDG Profile v3.3", "生命條垂直位置已修正並儲存。請按 Play 驗收。", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogError("[SDG Profile v3.3] " + exception);
                EditorUtility.DisplayDialog("SDG Profile v3.3", exception.Message, "OK");
            }
        }

        public static void ApplyFromBatch()
        {
            var report = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName);
            if (File.Exists(report)) File.Delete(report);
            Apply();
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before changing the front-end scene.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new FileNotFoundException("Front-end main menu scene missing", ScenePath);

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform canvas = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "SDG FrontEnd Canvas") { canvas = root.transform; break; }
                }
                Transform menu = canvas == null ? null : canvas.Find("MENU - six actions");
                Transform panel = menu == null ? null : menu.Find("Player information panel");
                if (panel == null)
                    throw new InvalidOperationException("Original player panel not found. Nothing changed.");
                var panelRect = panel as RectTransform;
                var panelImage = panel.GetComponent<Image>();
                if (panelRect == null || panelImage == null || panelImage.sprite == null ||
                    !panelImage.sprite.name.Contains("FE_Menu_PlayerPanel"))
                    throw new InvalidOperationException("Unexpected panel image. Refusing to guess its coordinates.");

                // Ensure all previously confirmed features remain installed.
                var profile = panel.GetComponent<SDGFrontEndPlayerProfile>();
                var vitals = panel.GetComponent<SDGFrontEndPlayerVitals>();
                if (profile == null || profile.avatarImage == null || profile.playerNameInput == null || vitals == null)
                    throw new InvalidOperationException("Avatar/name/HP components missing; no scene changes applied.");
                if (panel.Find("Experience text") == null)
                    throw new InvalidOperationException("Original XP counter missing; no scene changes applied.");

                RectTransform rail = panel.Find(RailName) as RectTransform;
                RectTransform fill = rail == null ? null : rail.Find(FillName) as RectTransform;
                Image healthImage = fill == null ? null : fill.GetComponent<Image>();
                if (rail == null || fill == null || healthImage == null || vitals.healthFill != healthImage)
                    throw new InvalidOperationException("Existing v3 HP fill wiring differs; refused to edit.");
                if (panelRect.rect.height <= 0f)
                    throw new InvalidOperationException("Player panel has invalid height; no scene changes applied.");

                float beforeY = rail.anchoredPosition.y;
                // Original art y is measured down from the top; anchored UI y is up.
                // Recompute absolute y each time to avoid cumulative movement.
                float targetY = (0.5f - (OriginalArtRailCenterY + DownwardArtPixels) / OriginalArtHeight)
                              * panelRect.rect.height;
                var before = rail.anchoredPosition;
                rail.anchoredPosition = new Vector2(before.x, targetY);

                // Do not modify fill size, x alignment, pivot, mask, width, color, or the HP logic.
                EditorUtility.SetDirty(rail);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Could not save the updated main-menu scene.");
                AssetDatabase.SaveAssets();
                if (Mathf.Abs(rail.anchoredPosition.y - targetY) > 0.01f)
                    throw new InvalidOperationException("Rail center verification failed.");

                string content = Marker + "\n" +
                    "Scene=" + ScenePath + "\n" +
                    "OriginalSprite=" + panelImage.sprite.name + "\n" +
                    "RailYBefore=" + beforeY.ToString("F3") + "\n" +
                    "RailYAfter=" + rail.anchoredPosition.y.ToString("F3") + "\n" +
                    "RailXPreserved=" + before.x.ToString("F3") + "\n" +
                    "ArtworkDownshift=14/682 of panel height\n" +
                    "HP fill size, HP data binding, avatar, player name, XP counter=untouched\n" +
                    "Level01-04 scenes=untouched\n" +
                    "Visual alignment still requires Game-view inspection\n";
                File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName), content);
                Debug.Log("[SDG Profile v3.3] " + content);
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
