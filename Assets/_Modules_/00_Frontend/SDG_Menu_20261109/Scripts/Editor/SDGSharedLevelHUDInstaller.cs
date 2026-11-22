#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
    /// Copies the ALREADY APPROVED v3.3 menu player panel into one Resources
    /// prefab. Runtime bootstrap displays that same prefab in all four levels.
    /// No Level01-04 .unity files, menu files or other scripts are modified.
    /// </summary>
    public static class SDGSharedLevelHUDInstaller
    {
        private const string Root = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109";
        private const string MenuScene = Root + "/Scenes/SDG_StartAndMainMenu.unity";
        private const string PrefabDir = Root + "/Resources";
        private const string PrefabPath = PrefabDir + "/SDG_LevelPlayerHUD.prefab";
        private const string ReportFile = "SDG_SharedLevelHUD_InstallReport.txt";
        private const string Success = "SDG SHARED LEVEL HUD SUCCESS";

        [MenuItem("SDG Adventure/前端 UI/28 建立四關共用玩家面板（不修改關卡）")]
        public static void ApplyFromMenu()
        {
            try
            {
                Apply();
                EditorUtility.DisplayDialog("SDG Shared HUD",
                    "已建立四關共用面板 Prefab。請進入各關 Game 視窗驗收。", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError("[SDG Shared HUD] " + e);
                EditorUtility.DisplayDialog("SDG Shared HUD - FAILED", e.Message, "OK");
            }
        }

        public static void ApplyFromBatch()
        {
            string report = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ReportFile);
            if (File.Exists(report)) File.Delete(report);
            Apply();
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene) == null)
                throw new FileNotFoundException("Main menu scene was not found: " + MenuScene);

            // Before writing anything, verify that all four playable
            // level groups appear in enabled Build Settings entries.
            string[] levelPaths = FindEnabledLevelScenes();

            Scene scene = SceneManager.GetSceneByPath(MenuScene);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Additive);
            GameObject temporaryRoot = null;
            try
            {
                Transform canvas = FindRoot(scene, "SDG FrontEnd Canvas");
                Transform menu = canvas == null ? null : canvas.Find("MENU - six actions");
                Transform existing = menu == null ? null : menu.Find("Player information panel");
                if (existing == null) throw new InvalidOperationException("Approved menu profile panel is missing.");

                RectTransform originalRect = existing as RectTransform;
                Image art = existing.GetComponent<Image>();
                SDGFrontEndPlayerProfile existingProfile = existing.GetComponent<SDGFrontEndPlayerProfile>();
                SDGFrontEndPlayerVitals existingVitals = existing.GetComponent<SDGFrontEndPlayerVitals>();
                RectTransform originalHP = existing.Find("Profile v2 - Health rail/Profile v2 - HP fill") as RectTransform;
                TextMeshProUGUI originalXP = existing.Find("Experience text") == null ? null :
                    existing.Find("Experience text").GetComponent<TextMeshProUGUI>();
                if (originalRect == null || art == null || art.sprite == null ||
                    !art.sprite.name.Contains("FE_Menu_PlayerPanel") ||
                    existingProfile == null || existingProfile.avatarImage == null ||
                    existingProfile.playerNameInput == null || existingVitals == null ||
                    existingVitals.healthFill == null || originalHP == null || originalXP == null ||
                    existingVitals.healthFill.transform != originalHP)
                    throw new InvalidOperationException("Menu profile is not the verified v3.3 panel. Nothing saved.");

                temporaryRoot = new GameObject("SDG HUD - temporary prefab build", typeof(RectTransform));
                RectTransform tempRect = temporaryRoot.GetComponent<RectTransform>();
                tempRect.anchorMin = tempRect.anchorMax = new Vector2(0.5f, 0.5f);
                tempRect.pivot = new Vector2(0.5f, 0.5f);
                tempRect.sizeDelta = new Vector2(480f, 191f);

                GameObject copy = UnityEngine.Object.Instantiate(existing.gameObject, tempRect, false);
                copy.name = "Player information panel";
                RectTransform copyRect = copy.GetComponent<RectTransform>();
                copyRect.anchorMin = copyRect.anchorMax = new Vector2(0.5f, 0.5f);
                copyRect.pivot = new Vector2(0.5f, 0.5f);
                copyRect.anchoredPosition = Vector2.zero;
                copyRect.sizeDelta = originalRect.sizeDelta;
                copyRect.localScale = Vector3.one;
                copyRect.localRotation = Quaternion.identity;
                copy.SetActive(true);

                // v1's avatar dialog is under MENU, OUTSIDE the panel.
                // Strip these external scene-object links from the level prefab.
                // The selected sprite/name are still loaded by profile.Awake().
                SDGFrontEndPlayerProfile levelProfile = copy.GetComponent<SDGFrontEndPlayerProfile>();
                levelProfile.pickerDialog = null;
                levelProfile.pickerCloseButton = null;
                levelProfile.pickerImportButton = null;
                levelProfile.pickerPrevButton = null;
                levelProfile.pickerNextButton = null;
                levelProfile.filePathInput = null;
                levelProfile.pickerPageLabel = null;
                levelProfile.pickerMessage = null;
                levelProfile.tiles = new SDGFrontEndPlayerProfile.PortraitTile[0];
                // The read-only level panel is not an extra menu for FPS input.
                // Runtime CanvasGroup blocksRaycasts=false handles interaction.

                SDGSharedLevelPlayerHUD binder = temporaryRoot.AddComponent<SDGSharedLevelPlayerHUD>();
                Transform xp = copy.transform.Find("Experience text");
                binder.experienceSource = xp == null ? null : xp.GetComponent<TextMeshProUGUI>();
                Transform xpFill = copy.transform.Find("XP bar background/XP bar filled");
                binder.originalExperienceFill = xpFill == null ? null : xpFill.GetComponent<Image>();
                if (binder.experienceSource == null)
                    throw new InvalidOperationException("Experience text was not cloned.");

                if (!AssetDatabase.IsValidFolder(PrefabDir))
                    AssetDatabase.CreateFolder(Root, "Resources");
                bool saved;
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(temporaryRoot, PrefabPath, out saved);
                if (!saved || asset == null)
                    throw new IOException("Unity could not save shared HUD prefab: " + PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (savedPrefab == null || savedPrefab.GetComponent<SDGSharedLevelPlayerHUD>() == null ||
                    savedPrefab.GetComponentInChildren<SDGFrontEndPlayerProfile>(true) == null ||
                    savedPrefab.GetComponentInChildren<SDGFrontEndPlayerVitals>(true) == null)
                    throw new IOException("Prefab verification failed. See Unity log.");

                var lines = new List<string> {
                    Success,
                    "ResourcePrefab=" + PrefabPath,
                    "VisualSource=real existing main-menu profile (v3.3)",
                    "Mode=automatic runtime overlay; no Level scene editing",
                    "Display=top-left, original artwork and HP rail",
                    "XP=reads SDG_FE_Experience and SDG_FE_ExperienceMax",
                    "HP=reads previously installed SDG_FE_PlayerHealth via player vitals",
                    "AvatarName=uses existing saved PlayerPrefs",
                    "LevelInputs=read-only; FPS mouse unaffected",
                    "NOTE=Level01-03 HP must be reported by their gameplay health controllers",
                    "NOTE=Game View and builds still require local validation",
                };
                for (int i = 0; i < 4; i++) lines.Add("Level0" + (i + 1) + "=" + levelPaths[i]);
                string content = string.Join("\n", lines.ToArray()) + "\n";
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                File.WriteAllText(Path.Combine(projectRoot, ReportFile), content, System.Text.Encoding.UTF8);
                Debug.Log("[SDG Shared HUD] " + content);
            }
            finally
            {
                if (temporaryRoot != null) UnityEngine.Object.DestroyImmediate(temporaryRoot);
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject item in scene.GetRootGameObjects())
                if (item.name == name) return item.transform;
            return null;
        }

        private static string[] FindEnabledLevelScenes()
        {
            string[] scenes = new string[4];
            for (int i = 1; i <= 4; i++)
            {
                string part = "/_Modules_/02_Levels/Level0" + i + "/";
                var matches = new List<string>();
                foreach (EditorBuildSettingsScene item in EditorBuildSettings.scenes)
                {
                    if (!item.enabled || string.IsNullOrEmpty(item.path)) continue;
                    string path = item.path.Replace('\\', '/');
                    if (path.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                        matches.Add(path);
                }
                if (matches.Count == 0)
                    throw new InvalidOperationException("Level0" + i + ": no enabled scenes " +
                        "found in Build Settings. No assets changed.");
                scenes[i - 1] = string.Join(" | ", matches.ToArray());
            }
            return scenes;
        }
    }
}
#endif
