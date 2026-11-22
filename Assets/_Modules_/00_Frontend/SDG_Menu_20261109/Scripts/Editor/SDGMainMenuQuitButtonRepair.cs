#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd.Editor
{
    // Fix ONLY the Quit button in the existing front-end scene.
    // Does not edit levels, pause screens, controllers, or Build Settings.
    public static class SDGMainMenuQuitButtonRepair
    {
        private const string ScenePath =
            "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scenes/SDG_StartAndMainMenu.unity";
        private const string MenuName = "MENU - six actions";

        [MenuItem("SDG Adventure/前端 UI/20 修正主選單離開遊戲按鈕（僅前端）")]
        public static void Repair()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Quit button fix", "Stop Play Mode first.", "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError("[SDG Quit Fix] Front-end scene not found: " + ScenePath);
                EditorUtility.DisplayDialog("Quit button fix", "Front-end scene not found. No changes made.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SDGFrontEndController controller = null;
            Transform menu = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (controller == null) controller = root.GetComponentInChildren<SDGFrontEndController>(true);
                if (menu == null) menu = FindDescendantByName(root.transform, MenuName);
            }

            if (controller == null || menu == null)
            {
                Debug.LogError("[SDG Quit Fix] Front-end controller or main menu not found. No changes made.");
                EditorUtility.DisplayDialog("Quit button fix", "Main menu/controller not found. No changes made.", "OK");
                return;
            }

            Button quitButton = null;
            foreach (Button candidate in menu.GetComponentsInChildren<Button>(true))
            {
                if (!candidate.gameObject.name.StartsWith("Button 6", StringComparison.OrdinalIgnoreCase))
                    continue;

                TMP_Text label = candidate.GetComponentInChildren<TMP_Text>(true);
                if (label != null && label.text != null && label.text.Contains("離開遊戲"))
                {
                    quitButton = candidate;
                    break;
                }
            }

            if (quitButton == null)
            {
                Debug.LogError("[SDG Quit Fix] Expected Button 6 - Exit Game not found under main menu. No changes made.");
                EditorUtility.DisplayDialog("Quit button fix", "Exit button not found under the six-button main menu. No changes made.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Main menu exit", "Rebind ONLY the main-menu Exit Game button to QuitGame()?", "Apply", "Cancel"))
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string source = Path.Combine(projectRoot, ScenePath.Replace('/', Path.DirectorySeparatorChar));
            string backupDirectory = Path.Combine(projectRoot, "SDG_FrontEnd_Backups", "MainMenuQuit");
            Directory.CreateDirectory(backupDirectory);
            string backup = Path.Combine(backupDirectory,
                "SDG_StartAndMainMenu_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
            File.Copy(source, backup, false);

            Undo.RecordObject(quitButton, "Connect main menu exit");
            for (int index = quitButton.onClick.GetPersistentEventCount() - 1; index >= 0; --index)
                UnityEventTools.RemovePersistentListener(quitButton.onClick, index);
            UnityEventTools.AddPersistentListener(quitButton.onClick, controller.QuitGame);
            quitButton.interactable = true;
            Graphic graphic = quitButton.targetGraphic;
            if (graphic == null) graphic = quitButton.GetComponent<Graphic>();
            if (graphic != null)
            {
                Undo.RecordObject(graphic, "Enable exit button raycast");
                graphic.raycastTarget = true;
                quitButton.targetGraphic = graphic;
                EditorUtility.SetDirty(graphic);
            }

            EditorUtility.SetDirty(quitButton);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("[SDG Quit Fix] Save scene failed; backup exists at: " + backup);
                return;
            }

            bool success = quitButton.onClick.GetPersistentEventCount() == 1
                && quitButton.onClick.GetPersistentMethodName(0) == "QuitGame"
                && quitButton.onClick.GetPersistentTarget(0) == controller;
            Debug.Log("[SDG Quit Fix] " + (success ? "PASS" : "CHECK")
                + " | Button: " + quitButton.name
                + " | Handler: " + quitButton.onClick.GetPersistentMethodName(0)
                + " | Scene: " + ScenePath
                + " | Backup: " + backup);
            EditorUtility.DisplayDialog("Main menu exit", success
                ? "Exit Game button connected. Save completed. Rebuild the Windows EXE to apply changes."
                : "The scene was saved, but the event needs inspection in the Button On Click() list.", "OK");
        }

        private static Transform FindDescendantByName(Transform transform, string exactName)
        {
            if (transform.name == exactName) return transform;
            foreach (Transform child in transform)
            {
                Transform found = FindDescendantByName(child, exactName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
