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
    /// <summary>Fixes profile v1 in place. DOES NOT rebuild the menu or replace its artwork.</summary>
    public static class SDGFrontEndProfileLayoutFixV2
    {
        private const string Root = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109";
        private const string ScenePath = Root + "/Scenes/SDG_StartAndMainMenu.unity";
        private const string ReportName = "SDG_PlayerProfile_LayoutV2_Report.txt";

        [MenuItem("SDG Adventure/前端 UI/25 修正名稱欄與生命條（保留頭像）")]
        public static void ApplyFromMenu()
        {
            try { Apply(); EditorUtility.DisplayDialog("SDG Profile v2", "名稱輸入與生命條位置已更新。請按 Play 檢查。", "OK"); }
            catch (Exception e) { Debug.LogError("[SDG Profile v2] " + e); EditorUtility.DisplayDialog("SDG Profile v2", e.Message, "OK"); }
        }

        public static void ApplyFromBatch()
        {
            string report = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName);
            if (File.Exists(report)) File.Delete(report);
            Apply();
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before applying UI layout changes.");
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
                if (panel == null) throw new InvalidOperationException("Original player panel not found; scene not modified.");

                SDGFrontEndPlayerProfile profile = panel.GetComponent<SDGFrontEndPlayerProfile>();
                if (profile == null || profile.playerNameInput == null || profile.avatarImage == null)
                    throw new InvalidOperationException("Profile v1 missing; do not replace existing avatar UI. Scene unchanged.");

                // Keep the original upper engraved name slot. Do not add a second visible panel.
                InputField input = profile.playerNameInput;
                RectTransform nameRect = input.GetComponent<RectTransform>();
                Position(nameRect, new Vector2(54f, 22f), new Vector2(292f, 40f));
                Image nameBackground = input.GetComponent<Image>();
                if (nameBackground != null)
                {
                    nameBackground.color = new Color(0f, 0f, 0f, 0.001f);
                    nameBackground.raycastTarget = true;
                }
                Outline oldOutline = input.GetComponent<Outline>();
                if (oldOutline != null) oldOutline.enabled = false;
                if (input.textComponent != null)
                {
                    Position(input.textComponent.rectTransform, new Vector2(2f, 0f), new Vector2(272f, 34f));
                    input.textComponent.alignment = TextAnchor.MiddleLeft;
                    input.textComponent.fontSize = 20;
                    input.textComponent.color = new Color(0.91f, 1f, 0.95f, 1f);
                }
                Text placeholder = input.placeholder as Text;
                if (placeholder != null)
                {
                    Position(placeholder.rectTransform, new Vector2(2f, 0f), new Vector2(272f, 34f));
                    placeholder.alignment = TextAnchor.MiddleLeft;
                    placeholder.fontSize = 19;
                    placeholder.text = "輸入玩家名稱";
                }
                input.interactable = true;
                input.characterLimit = 20;

                // The old TMP texts often have CJK mirrored children; hide parent whole.
                DisableChild(panel, "Player title");
                DisableChild(panel, "Player name");
                DisableChild(panel, "Player level");
                DisableChild(panel, "Experience text");
                DisableChild(panel, "XP bar background");

                // The SMALL engraved slot is to the right of the portrait, NOT below it.
                Font font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/NotoSansCJKtc-Regular.otf");
                if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (font == null) throw new InvalidOperationException("No UI Font available.");
                RectTransform countRect = GetOrCreate(panel, "Profile v2 - HP number");
                Position(countRect, new Vector2(-49f, -20f), new Vector2(94f, 23f));
                Text count = countRect.GetComponent<Text>();
                if (count == null) count = countRect.gameObject.AddComponent<Text>();
                count.font = font;
                count.fontSize = 16;
                count.text = "100/100";
                count.alignment = TextAnchor.MiddleCenter;
                count.color = new Color(.93f, 1f, .95f, 1f);
                count.raycastTarget = false;
                count.horizontalOverflow = HorizontalWrapMode.Overflow;
                count.verticalOverflow = VerticalWrapMode.Truncate;

                // Align the functional HP fill INSIDE the long, bottom green rail art.
                RectTransform rail = GetOrCreate(panel, "Profile v2 - Health rail");
                Position(rail, new Vector2(55f, -49f), new Vector2(290f, 10f));
                Image railImage = rail.GetComponent<Image>();
                if (railImage == null) railImage = rail.gameObject.AddComponent<Image>();
                railImage.color = new Color(0f, .08f, .07f, .15f);
                railImage.raycastTarget = false;
                RectTransform fillRect = GetOrCreate(rail, "Profile v2 - HP fill");
                Position(fillRect, Vector2.zero, new Vector2(290f, 8f));
                fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, 0.5f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.anchoredPosition = Vector2.zero;
                Image fill = fillRect.GetComponent<Image>();
                if (fill == null) fill = fillRect.gameObject.AddComponent<Image>();
                fill.type = Image.Type.Simple;
                fill.raycastTarget = false;
                fill.color = new Color(.22f, 1f, .46f, .87f);

                var vitals = panel.GetComponent<SDGFrontEndPlayerVitals>();
                if (vitals == null) vitals = panel.gameObject.AddComponent<SDGFrontEndPlayerVitals>();
                vitals.healthCount = count;
                vitals.healthFill = fill;

                // Place input on top for reliable clicking; leave avatar selection unchanged.
                nameRect.SetAsLastSibling();
                countRect.SetAsLastSibling();
                rail.SetAsLastSibling();

                EditorUtility.SetDirty(profile);
                EditorUtility.SetDirty(vitals);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Failed to save front-end scene.");
                AssetDatabase.SaveAssets();

                string report = "SDG PROFILE LAYOUT V2 SUCCESS\n" +
                    "Scene=" + ScenePath + "\n" +
                    "Avatar=original chooser preserved\n" +
                    "Name=existing input, transparent over original upper slot\n" +
                    "0-100=left mini-slot, now real player HP\n" +
                    "HealthRail=original long lower track, fill driven by HP\n" +
                    "Level04=auto syncs CurrentHealth/MaxHealth\n" +
                    "Other levels=may call SDGFrontEndPlayerVitals.ReportHealth(current,max)\n" +
                    "Level01-04 scenes=not modified\n";
                File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportName), report);
                Debug.Log("[SDG Profile v2] " + report);
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void DisableChild(Transform panel, string name)
        {
            Transform t = panel.Find(name);
            if (t != null) t.gameObject.SetActive(false);
        }
        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects()) if (go.name == name) return go.transform;
            return null;
        }
        private static RectTransform GetOrCreate(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                var rect = existing as RectTransform;
                if (rect == null) throw new InvalidOperationException("Unexpected object named " + name);
                return rect;
            }
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        private static void Position(RectTransform t, Vector2 pos, Vector2 size)
        {
            t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
            t.pivot = new Vector2(0.5f, 0.5f);
            t.anchoredPosition = pos;
            t.sizeDelta = size;
            t.localScale = Vector3.one;
            t.localRotation = Quaternion.identity;
        }
    }
}
#endif
