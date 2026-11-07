using System.Collections;
using System.Linq;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M7SharedPlayerTests
{
    public sealed class PlayerInputServiceTests
    {
        private const string Selection = "Assets/_Modules_/05_Testing/Scenes/M6_DevelopmentSelection.unity";
        private const string C3 = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";

        [Test]
        public void EscapeHasOneEnabledActionInEachContext()
        {
            PlayerInputService service = PlayerInputService.Instance;
            Assert.IsNotNull(service);
            Assert.IsNotNull(service.Actions);
            var pause = service.Actions.FindActionMap("Global").FindAction("Pause");
            var cancel = service.Actions.FindActionMap("UI").FindAction("Cancel");
            try
            {
                service.SetContext(PlayerInputContext.Gameplay);
                Assert.IsTrue(service.Actions.FindActionMap("Gameplay").enabled);
                Assert.IsTrue(service.Actions.FindActionMap("Global").enabled);
                Assert.IsFalse(service.Actions.FindActionMap("UI").enabled);
                Assert.IsTrue(pause.enabled);
                Assert.IsFalse(cancel.enabled);

                service.SetContext(PlayerInputContext.UI);
                Assert.IsFalse(service.Actions.FindActionMap("Gameplay").enabled);
                Assert.IsFalse(service.Actions.FindActionMap("Global").enabled);
                Assert.IsTrue(service.Actions.FindActionMap("UI").enabled);
                Assert.IsFalse(pause.enabled);
                Assert.IsTrue(cancel.enabled);
            }
            finally
            {
                service.SetContext(PlayerInputContext.Inactive);
            }
        }

        [UnityTest]
        public IEnumerator OneServiceSurvivesM6C3SceneChanges()
        {
            yield return SceneManager.LoadSceneAsync(Selection, LoadSceneMode.Single);
            PlayerInputService original = PlayerInputService.Instance;
            Assert.IsNotNull(original);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerInputService>(FindObjectsSortMode.None).Length);

            yield return SceneManager.LoadSceneAsync(C3, LoadSceneMode.Single);
            yield return null;
            Assert.AreSame(original, PlayerInputService.Instance);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerInputService>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(PlayerInputContext.Gameplay, original.Context);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerPauseMenu>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);

            yield return SceneManager.LoadSceneAsync(Selection, LoadSceneMode.Single);
            Assert.AreSame(original, PlayerInputService.Instance);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerInputService>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(PlayerInputContext.Inactive, original.Context);
        }

        [UnityTest]
        public IEnumerator Level03MenuPausesResumesAndUsesExistingTransitions()
        {
            yield return SceneManager.LoadSceneAsync(C3, LoadSceneMode.Single);
            yield return null;
            PlayerInputService input = PlayerInputService.Instance;
            PlayerPauseMenu menu = Object.FindFirstObjectByType<PlayerPauseMenu>();
            Assert.IsNotNull(menu);
            Assert.IsTrue(menu.HasLevelActions);
            Assert.IsTrue(menu.UsesExternalTaskView);
            var hudType = System.Type.GetType("C3TreasurePuzzle.C3TreasureHud, Assembly-CSharp", true);
            var hud = UnityEngine.Object.FindFirstObjectByType(hudType);
            var sheetOpen = hudType.GetProperty("TaskSheetOpen");
            Assert.AreEqual(5, menu.Journal.TotalCount);
            Assert.AreEqual(0, menu.Journal.CompletedCount);
            Assert.AreEqual(PlayerInputContext.Gameplay, input.Context);
            var progressType = System.Type.GetType("C3TreasurePuzzle.C3TreasureProgress, Assembly-CSharp", true);
            var progress = UnityEngine.Object.FindFirstObjectByType(progressType);
            Assert.IsTrue((bool)progress.GetType().GetMethod("TryCollect")
                .Invoke(progress, new object[] { "fuse", "Fuse" }));
            Assert.AreEqual(1, menu.Journal.CompletedCount);

            menu.Pause();
            Assert.AreEqual(PlayerMenuState.Paused, menu.State);
            Assert.IsTrue(menu.View.IsVisible);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreEqual(PlayerInputContext.UI, input.Context);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            CollectionAssert.AreEqual(new[] { "繼續遊戲", "操作設定", "任務清單", "重新開始本關", "返回選關" },
                menu.View.Panel.Find("ScrollViewport/ScrollContent").GetComponentsInChildren<Button>()
                    .Select(button => button.GetComponentInChildren<Text>().text).ToArray());
            menu.OpenTasksFromPause();
            Assert.AreEqual(PlayerMenuState.Tasks, menu.State);
            Assert.IsTrue((bool)sheetOpen.GetValue(hud));
            Assert.IsFalse(menu.View.IsVisible);
            menu.CloseTasks();
            Assert.AreEqual(PlayerMenuState.Paused, menu.State);
            Assert.IsFalse((bool)sheetOpen.GetValue(hud));
            Assert.IsTrue(menu.View.IsVisible);
            Assert.AreEqual(0f, Time.timeScale);
            menu.OpenSettings();
            Assert.AreEqual(PlayerMenuState.Settings, menu.State);
            var proneRow = menu.View.Panel.Find("ScrollViewport/ScrollContent/Binding_Prone_0");
            Assert.IsNotNull(proneRow);
            Assert.IsTrue(proneRow.GetComponentsInChildren<Text>().Any(t => t.text == "趴下"));
            Assert.AreEqual(input.Keybindings.BindingDisplay("Gameplay", "Prone", 0), proneRow.GetComponentInChildren<Button>().GetComponentInChildren<Text>().text);
            menu.BackToPause();
            menu.Resume();
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(PlayerInputContext.Gameplay, input.Context);
            Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);

            menu.OpenTasks();
            Assert.AreEqual(PlayerMenuState.Tasks, menu.State);
            Assert.IsTrue((bool)sheetOpen.GetValue(hud));
            Assert.IsFalse(menu.View.IsVisible);
            Assert.AreEqual(PlayerInputContext.UI, input.Context);
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            menu.CloseTasks();
            Assert.IsFalse((bool)sheetOpen.GetValue(hud));
            Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);
            menu.Pause();
            menu.RestartLevel();
            yield return null;
            Assert.AreEqual(C3, SceneManager.GetActiveScene().path);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(PlayerInputContext.Gameplay, input.Context);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerInputService>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(0, Object.FindFirstObjectByType<PlayerPauseMenu>().Journal.CompletedCount);

            menu = Object.FindFirstObjectByType<PlayerPauseMenu>();
            menu.Pause();
            menu.ReturnToSelection();
            yield return null;
            Assert.AreEqual(Selection, SceneManager.GetActiveScene().path);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(PlayerInputContext.Inactive, input.Context);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerInputService>(FindObjectsSortMode.None).Length);
        }
    }
}
