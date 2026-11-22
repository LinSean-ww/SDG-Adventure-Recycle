#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.FrontEnd.Editor
{
    public static class SDGMenuReworkR1Installer
    {
        public const string ScenePath = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scenes/SDG_StartAndMainMenu.unity";
        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform node = FindDeep(child, name);
                if (node != null) return node;
            }
            return null;
        }
        private static void CheckStructure(Scene scene, out SDGFrontEndController controller)
        {
            controller = null;
            Transform canvas = null;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == "SDG FrontEnd Controller") controller = go.GetComponent<SDGFrontEndController>();
                if (go.name == "SDG FrontEnd Canvas") canvas = go.transform;
            }
            if (controller == null || canvas == null) throw new InvalidOperationException("Missing front-end controller/canvas");
            Transform menu = canvas.Find("MENU - six actions");
            if (menu == null) throw new InvalidOperationException("Main menu node not found");
            for (int i = 1; i <= 6; i++)
            {
                bool found = false;
                foreach (Transform child in menu)
                    if (child.name.StartsWith("Button " + i + " - ", StringComparison.Ordinal) && child.GetComponent<UnityEngine.UI.Button>() != null)
                        found = true;
                if (!found) throw new InvalidOperationException("Missing original action button " + i);
            }
            string[] nodes = { "Player information panel", "SDG Save Progress v2 Tall UI", "Settings dialog", "Level selection dialog", "Credits dialog" };
            foreach (string name in nodes)
                if (FindDeep(menu, name) == null) throw new InvalidOperationException("Required original UI object missing: " + name);
            if (FindDeep(menu, "Player information panel").GetComponentInChildren<SDGFrontEndPlayerProfile>(true) == null &&
                menu.GetComponentInChildren<SDGFrontEndPlayerProfile>(true) == null)
                throw new InvalidOperationException("Original player-profile behaviour not found");
            if (FindDeep(menu, "SDG Save Progress v2 Tall UI").GetComponentInChildren<SDGSaveProgressMenuV2>(true) == null)
                throw new InvalidOperationException("Original save UI behaviour not found");
        }
        [MenuItem("SDG Adventure/前端 UI/80 R1 選單整合修正")]
        public static void ApplyFromMenu() { Apply(); }
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new FileNotFoundException("Front-end scene missing", ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SDGFrontEndController ctrl;
            CheckStructure(scene, out ctrl);
            SDGMenuReworkR1 nav = ctrl.GetComponent<SDGMenuReworkR1>();
            if (nav == null) nav = ctrl.gameObject.AddComponent<SDGMenuReworkR1>();
            nav.frontEnd = ctrl;
            EditorUtility.SetDirty(nav);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("SaveScene returned false");
            Debug.Log("SDG_MENU_REWORK_R1_APPLY_PASS");
        }
        public static void Validate()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) throw new FileNotFoundException(ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SDGFrontEndController ctrl;
            CheckStructure(scene, out ctrl);
            SDGMenuReworkR1 nav = ctrl.GetComponent<SDGMenuReworkR1>();
            if (nav == null || nav.frontEnd != ctrl) throw new InvalidOperationException("SDGMenuReworkR1 not wired");
            if (typeof(SDGLevel01ClampGuardR1) == null) throw new InvalidOperationException("Clamp guard missing");
            Debug.Log("SDG_MENU_REWORK_R1_VALIDATION_PASS");
        }
    }
}
#endif
