using SDGAdventure.SharedPlayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SDGAdventure.PlayerTest;

namespace SDGAdventure.PlayerTest.Editor
{
    public static class PlayerControlQaMenu
    {
        private const string MenuRoot = "SDG Adventure/M7 QA/";
        private const string ScenePath = "Assets/_Modules_/05_Testing/SharedPlayer/PlayerControlTest.unity";
        private const string CycleItem = MenuRoot + "Cycle Panels %#&F9";
        private const string TasksShortcutItem = MenuRoot + "Toggle Tasks %#&F10";

        [MenuItem(MenuRoot + "Wire Demo Tasks")]
        private static void WireDemoTasks()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new System.InvalidOperationException("Open PlayerControlTest.unity first.");
            PlayerControlTestHud hud = Object.FindFirstObjectByType<PlayerControlTestHud>();
            SharedFirstPersonController player = Object.FindFirstObjectByType<SharedFirstPersonController>();
            PlayerControlTestInteractable target = Object.FindFirstObjectByType<PlayerControlTestInteractable>();
            if (hud == null || player == null || target == null)
                throw new System.InvalidOperationException("The standalone test scene is incomplete.");
            if (hud.GetComponent<PlayerControlTestTaskDemo>() != null) return;
            PlayerControlTestTaskDemo demo = Undo.AddComponent<PlayerControlTestTaskDemo>(hud.gameObject);
            demo.Configure(player, target);
            EditorUtility.SetDirty(demo);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem(MenuRoot + "Show Pause")]
        private static void ShowPause() => FindMenu()?.Pause();

        [MenuItem(MenuRoot + "Show Tasks")]
        private static void ShowTasks() => FindMenu()?.OpenTasks();

        [MenuItem(MenuRoot + "Complete First Task")]
        private static void CompleteFirstTask() => FindMenu()?.Journal.SetCompleted("reach_tunnel");

        [MenuItem(MenuRoot + "Show Settings")]
        private static void ShowSettings()
        {
            PlayerPauseMenu menu = FindMenu();
            if (menu == null) return;
            if (menu.State == PlayerMenuState.Playing) menu.Pause();
            menu.OpenSettings();
        }

        [MenuItem(MenuRoot + "Resume")]
        private static void Resume() => FindMenu()?.Resume();

        [MenuItem(CycleItem)]
        private static void CyclePanels()
        {
            PlayerPauseMenu menu = FindMenu();
            if (menu == null) return;
            if (menu.State == PlayerMenuState.Playing) menu.Pause();
            else if (menu.State == PlayerMenuState.Paused) menu.OpenSettings();
            else if (menu.State == PlayerMenuState.Settings) menu.BackToPause();
        }

        [MenuItem(TasksShortcutItem)]
        private static void ToggleTasks()
        {
            PlayerPauseMenu menu = FindMenu();
            if (menu == null) return;
            if (menu.State == PlayerMenuState.Playing) menu.OpenTasks();
            else if (menu.State == PlayerMenuState.Tasks) menu.CloseTasks();
        }

        [MenuItem(MenuRoot + "Show Pause", true)]
        [MenuItem(MenuRoot + "Show Tasks", true)]
        [MenuItem(MenuRoot + "Complete First Task", true)]
        [MenuItem(MenuRoot + "Show Settings", true)]
        [MenuItem(MenuRoot + "Resume", true)]
        [MenuItem(CycleItem, true)]
        [MenuItem(TasksShortcutItem, true)]
        private static bool ValidatePlayMode() => Application.isPlaying;

        private static PlayerPauseMenu FindMenu() => Object.FindFirstObjectByType<PlayerPauseMenu>();
    }
}
