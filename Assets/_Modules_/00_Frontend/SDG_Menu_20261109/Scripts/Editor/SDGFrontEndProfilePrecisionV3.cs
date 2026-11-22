#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd.Editor
{
    /// <summary>
    /// In-place precision repair for FE_Menu_PlayerPanel.png, no new art and no level changes.
    /// Shows the original experience counter in the small trapezoid, and aligns the
    /// functional HP fill with the existing bottom slot.
    /// </summary>
    public static class SDGFrontEndProfilePrecisionV3
    {
        private const string Root = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109";
        private const string ScenePath = Root + "/Scenes/SDG_StartAndMainMenu.unity";
        private const string ReportName = "SDG_PlayerProfile_PrecisionV3_Report.txt";
        private const string OldNumber = "Profile v2 - HP number";
        private const string RailName = "Profile v2 - Health rail";
        private const string FillName = "Profile v2 - HP fill";

        // Measured from the ORIGINAL 2048 x 682 FE_Menu_PlayerPanel artwork.
        private const float ArtWidth = 2048f;
        private const float ArtHeight = 682f;
        private const float MiniLeft = 642f;
        private const float MiniRight = 988f;
        private const float MiniTop = 387f;
        private const float MiniBottom = 445f;
        private const float RailLeft = 648f;
        private const float RailRight = 1854f;
        private const float RailTop = 505f;
        private const float RailBottom = 535f;

        [MenuItem("SDG Adventure/前端 UI/26 精準對齊原圖數值與生命條（保留頭像姓名）")]
        public static void ApplyFromMenu()
        {
            try { Apply(); EditorUtility.DisplayDialog("SDG Profile v3", "原圖小欄位數值及生命條已對齊，請按 Play 檢查。", "OK"); }
            catch (Exception e) { Debug.LogError("[SDG Profile v3] " + e); EditorUtility.DisplayDialog("SDG Profile v3", e.Message, "OK"); }
        }

        public static void ApplyFromBatch()
        {
            string report = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName);
            if (File.Exists(report)) File.Delete(report);
            Apply();
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new FileNotFoundException("Front-end scene not found", ScenePath);

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform canvas = FindRoot(scene, "SDG FrontEnd Canvas");
                Transform menu = canvas == null ? null : canvas.Find("MENU - six actions");
                Transform panel = menu == null ? null : menu.Find("Player information panel");
                if (panel == null) throw new InvalidOperationException("Original player panel missing; scene not changed.");

                var artwork = panel.GetComponent<Image>();
                var panelRect = panel as RectTransform;
                if (panelRect == null || artwork == null || artwork.sprite == null)
                    throw new InvalidOperationException("Original panel sprite/RectTransform unavailable.");
                if (!artwork.sprite.name.Contains("FE_Menu_PlayerPanel"))
                    throw new InvalidOperationException("Unexpected panel sprite: " + artwork.sprite.name + ". Refusing to guess positions.");

                var profile = panel.GetComponent<SDGFrontEndPlayerProfile>();
                var vitals = panel.GetComponent<SDGFrontEndPlayerVitals>();
                if (profile == null || profile.avatarImage == null || profile.playerNameInput == null || vitals == null)
                    throw new InvalidOperationException("Profile v1 or health v2 missing. Install existing v1/v2 first; no changes made.");

                // Unhide the ORIGINAL XP value (already wired to SDGFrontEndController.RefreshUI).
                // This keeps the requested 0/100 meaningful rather than turning it into static text.
                RectTransform xp = panel.Find("Experience text") as RectTransform;
                if (xp == null) throw new InvalidOperationException("Original Experience text missing.");
                TextMeshProUGUI source = xp.GetComponent<TextMeshProUGUI>();
                if (source == null) throw new InvalidOperationException("Experience TMP source missing.");
                xp.gameObject.SetActive(true);
                SetRect(xp, GetArtCenter(panelRect, MiniLeft, MiniRight, MiniTop, MiniBottom),
                           new Vector2(GetArtSize(panelRect, MiniRight - MiniLeft, MiniBottom - MiniTop).x, 29f));
                source.alignment = TextAlignmentOptions.Center;
                source.fontSize = 16f;
                source.enableWordWrapping = false;
                // Continue using the installed CJK mirror: the TMP value is updated by
                // the existing front-end controller, while Unity UI Text draws digits reliably.
                Transform mirrorTransform = xp.Find("_SDG_CJK_LegacyText");
                if (mirrorTransform == null)
                {
                    GameObject mirrorObject = new GameObject("_SDG_CJK_LegacyText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                    mirrorObject.transform.SetParent(xp, false);
                    mirrorTransform = mirrorObject.transform;
                }
                RectTransform mirrorRect = mirrorTransform as RectTransform;
                mirrorRect.anchorMin = Vector2.zero;
                mirrorRect.anchorMax = Vector2.one;
                mirrorRect.offsetMin = Vector2.zero;
                mirrorRect.offsetMax = Vector2.zero;
                mirrorRect.pivot = new Vector2(.5f, .5f);
                mirrorRect.localScale = Vector3.one;
                mirrorRect.localRotation = Quaternion.identity;
                mirrorTransform.gameObject.SetActive(true);

                Text xpDisplay = mirrorTransform.GetComponent<Text>();
                if (xpDisplay == null) xpDisplay = mirrorTransform.gameObject.AddComponent<Text>();
                Font readableDigits = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (readableDigits == null) readableDigits = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/NotoSansCJKtc-Regular.otf");
                if (readableDigits == null) throw new InvalidOperationException("No digit font available.");
                xpDisplay.font = readableDigits;
                xpDisplay.text = source.text;
                xpDisplay.fontSize = 16;
                xpDisplay.alignment = TextAnchor.MiddleCenter;
                xpDisplay.horizontalOverflow = HorizontalWrapMode.Overflow;
                xpDisplay.verticalOverflow = VerticalWrapMode.Truncate;
                xpDisplay.color = new Color(.96f, 1f, .98f, 1f);
                xpDisplay.raycastTarget = false;
                xpDisplay.enabled = true;

                var mirror = mirrorTransform.GetComponent<SDGFrontEndCjkLegacyMirror>();
                if (mirror == null) mirror = mirrorTransform.gameObject.AddComponent<SDGFrontEndCjkLegacyMirror>();
                mirror.source = source;
                mirror.legacyText = xpDisplay;
                mirror.enabled = true;
                source.enabled = false; // source only; original controller still updates its .text

                // Old v2 number failed to render and overlaps an independent concept (HP).
                // Keep it in the scene for rollback but don't display it.
                Transform duplicate = panel.Find(OldNumber);
                if (duplicate != null) duplicate.gameObject.SetActive(false);
                vitals.healthCount = null;

                // Position health rail using original sprite pixels rather than guessed UI offsets.
                RectTransform rail = panel.Find(RailName) as RectTransform;
                if (rail == null) throw new InvalidOperationException("v2 health rail missing.");
                rail.gameObject.SetActive(true);
                Vector2 railSize = GetArtSize(panelRect, RailRight - RailLeft, RailBottom - RailTop);
                SetRect(rail, GetArtCenter(panelRect, RailLeft, RailRight, RailTop, RailBottom), railSize);
                Image railBackground = rail.GetComponent<Image>();
                if (railBackground != null) { railBackground.color = new Color(0f, 0f, 0f, 0f); railBackground.raycastTarget = false; }
                RectMask2D mask = rail.GetComponent<RectMask2D>();
                if (mask == null) mask = rail.gameObject.AddComponent<RectMask2D>();
                mask.enabled = true;

                RectTransform fill = rail.Find(FillName) as RectTransform;
                if (fill == null) throw new InvalidOperationException("v2 health fill missing.");
                fill.gameObject.SetActive(true);
                fill.anchorMin = fill.anchorMax = new Vector2(0f, .5f);
                fill.pivot = new Vector2(0f, .5f);
                fill.anchoredPosition = Vector2.zero;
                fill.sizeDelta = new Vector2(railSize.x, railSize.y * .76f);
                fill.localScale = Vector3.one;
                fill.localRotation = Quaternion.identity;
                Image hp = fill.GetComponent<Image>();
                if (hp == null) hp = fill.gameObject.AddComponent<Image>();
                hp.type = Image.Type.Simple;
                hp.raycastTarget = false;
                hp.color = new Color(.25f, 1f, .52f, .87f);
                vitals.healthFill = hp;
                vitals.healthFillMaxWidth = railSize.x;

                // Order: original panel artwork first, then XP counter and HP fill.
                xp.SetAsLastSibling();
                rail.SetAsLastSibling();
                // Avatar picker/name input are maintained exactly as-is.
                EditorUtility.SetDirty(xpDisplay);
                EditorUtility.SetDirty(mirror);
                EditorUtility.SetDirty(source);
                EditorUtility.SetDirty(vitals);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Failed to save updated scene.");
                AssetDatabase.SaveAssets();

                string reportText =
                    "SDG PROFILE PRECISION V3 SUCCESS\n" +
                    "Scene=" + ScenePath + "\n" +
                    "OriginalArt=" + artwork.sprite.name + "\n" +
                    "XP mini slot=Experience text + legacy mirror, visible, linked to front-end XP\n" +
                    "XP mini position=" + xp.anchoredPosition + "; size=" + xp.sizeDelta + "\n" +
                    "XP mirror text=" + xpDisplay.text + "; enabled=" + xpDisplay.enabled + "\n" +
                    "HP track position=" + rail.anchoredPosition + "; size=" + rail.sizeDelta + "\n" +
                    "HP max fill width=" + vitals.healthFillMaxWidth + "\n" +
                    "HP values=previous v2 stored health (Level04 auto sync, others need adapters)\n" +
                    "Avatar picker and player name unchanged; Level01-04 scenes unchanged\n";
                File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName), reportText);
                Debug.Log("[SDG Profile v3] " + reportText);
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Vector2 GetArtCenter(RectTransform rect, float x1, float x2, float y1, float y2)
        {
            return new Vector2(((x1 + x2) * .5f / ArtWidth - .5f) * rect.rect.width,
                               (.5f - (y1 + y2) * .5f / ArtHeight) * rect.rect.height);
        }

        private static Vector2 GetArtSize(RectTransform rect, float w, float h)
        {
            return new Vector2(w / ArtWidth * rect.rect.width, h / ArtHeight * rect.rect.height);
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root.transform;
            return null;
        }
    }
}
#endif
