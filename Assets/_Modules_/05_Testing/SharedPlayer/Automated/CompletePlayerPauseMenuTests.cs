using System.Collections;
using System.Linq;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SDGAdventure.PlayerControlTests
{
    public sealed class CompletePlayerPauseMenuTests
    {
        private static Button[] Buttons(PlayerPauseMenu menu) => menu.View.Panel
            .Find("ScrollViewport/ScrollContent").GetComponentsInChildren<Button>();

        [UnityTest]
        public IEnumerator EscapeOpensCompletePlayerFiveButtonMenu()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Modules_/01_Shared/CompletePlayer/Prefabs/CompletePlayer.prefab");
            var player = Object.Instantiate(prefab);
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = Keyboard.current;
            bool added = keyboard == null;
            if (added) keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return null;
                var menu = player.GetComponent<PlayerPauseMenu>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                Assert.AreEqual(PlayerMenuState.Paused, menu.State);
                CollectionAssert.AreEqual(new[] { "繼續遊戲", "操作設定", "任務清單", "重新開始本關", "返回選關" },
                    Buttons(menu).Select(x => x.GetComponentInChildren<Text>().text).ToArray());
                menu.Resume();
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                if (added) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
                Object.Destroy(player);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator StandalonePrefabShowsFiveButtonsAndHandlesMissingScenes()
        {
            Scene old = SceneManager.GetActiveScene();
            Scene scene = SceneManager.CreateScene("CompletePlayerUnlistedTest");
            SceneManager.SetActiveScene(scene);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Modules_/01_Shared/CompletePlayer/Prefabs/CompletePlayer.prefab");
            Assert.IsNotNull(prefab);
            var player = Object.Instantiate(prefab);
            try
            {
                yield return null;
                var menu = player.GetComponent<PlayerPauseMenu>();
                menu.Pause();
                CollectionAssert.AreEqual(new[] { "繼續遊戲", "操作設定", "任務清單", "重新開始本關", "返回選關" },
                    Buttons(menu).Select(x => x.GetComponentInChildren<Text>().text).ToArray());
                Canvas.ForceUpdateCanvases();
                var viewport = menu.View.Panel.Find("ScrollViewport").GetComponent<RectTransform>();
                var viewportCorners = new Vector3[4];
                viewport.GetWorldCorners(viewportCorners);
                foreach (var button in Buttons(menu))
                {
                    var corners = new Vector3[4];
                    button.GetComponent<RectTransform>().GetWorldCorners(corners);
                    Assert.GreaterOrEqual(corners[0].y, viewportCorners[0].y - 1f, "Button is clipped by the viewport");
                    Assert.LessOrEqual(corners[2].y, viewportCorners[2].y + 1f, "Button is clipped by the viewport");
                }
                Buttons(menu)[2].onClick.Invoke();
                Assert.AreEqual(PlayerMenuState.Tasks, menu.State);
                Assert.IsTrue(menu.View.Panel.GetComponentsInChildren<Text>().Any(x => x.text == "目前沒有任務。"));
                menu.CloseTasks();
                Assert.AreEqual(PlayerMenuState.Paused, menu.State);
                Buttons(menu)[3].onClick.Invoke();
                StringAssert.Contains("Build Settings", menu.Feedback);
                Assert.AreEqual(PlayerMenuState.Paused, menu.State);
                Assert.IsTrue(menu.View.Panel.GetComponentsInChildren<Text>().Any(x => x.text == menu.Feedback));
                Buttons(menu)[4].onClick.Invoke();
                StringAssert.Contains("尚未設定", menu.Feedback);
                menu.SetSelectionScene("MissingSelectionScene");
                Buttons(menu)[4].onClick.Invoke();
                StringAssert.Contains("Build Settings", menu.Feedback);
                Buttons(menu)[1].onClick.Invoke();
                Assert.AreEqual(PlayerMenuState.Settings, menu.State);
                menu.BackToPause();
                Buttons(menu)[0].onClick.Invoke();
                Assert.AreEqual(PlayerMenuState.Playing, menu.State);
            }
            finally
            {
                Object.Destroy(player);
                SceneManager.SetActiveScene(old);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ConfiguredCallbacksTakePrecedence()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Modules_/01_Shared/CompletePlayer/Prefabs/CompletePlayer.prefab");
            var player = Object.Instantiate(prefab);
            yield return null;
            var menu = player.GetComponent<PlayerPauseMenu>();
            int restart = 0, selection = 0;
            menu.SetLevelActions(() => restart++, () => selection++, null);
            menu.Pause();
            Buttons(menu)[3].onClick.Invoke();
            Buttons(menu)[4].onClick.Invoke();
            Assert.AreEqual(1, restart);
            Assert.AreEqual(1, selection);
            menu.Resume();
            Object.Destroy(player);
            yield return null;
        }
    }
}
