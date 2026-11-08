using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M7SharedPlayerTests
{
    public class Level01FlowTests
    {
        [UnityTest]
        public IEnumerator DirectLoadFromSelectionDoesNotDuplicateEventSystem()
        {
            var warnings = new System.Collections.Generic.List<string>();
            void Capture(string condition, string stackTrace, LogType type)
            {
                if (condition.Contains("only one active Event System")) warnings.Add(condition);
            }
            Application.logMessageReceived += Capture;
            try
            {
                yield return SceneManager.LoadSceneAsync("Assets/_Modules_/05_Testing/Scenes/M6_DevelopmentSelection.unity");
                yield return null;
                Assert.AreEqual(1, ActiveEventSystems());
                yield return SceneManager.LoadSceneAsync("Assets/_Modules_/02_Levels/Level01/Imported/Scenes/WTF.unity");
                yield return null;
                Assert.AreEqual(1, ActiveEventSystems());
                Assert.IsEmpty(warnings, "Scene transition should not briefly enable duplicate EventSystems");
            }
            finally { Application.logMessageReceived -= Capture; }
        }

        [UnityTest]
        public IEnumerator SelectionButtonLoadsLevel01()
        {
            yield return SceneManager.LoadSceneAsync("Assets/_Modules_/05_Testing/Scenes/M6_DevelopmentSelection.unity");
            yield return null;
            yield return null;
            var launch = GameObject.Find("LaunchLevel01");
            Assert.NotNull(launch, "Level01 button should exist in the selection screen");
            var buttonType = Type.GetType("UnityEngine.UI.Button, UnityEngine.UI");
            Assert.NotNull(buttonType);
            var button = launch.GetComponent(buttonType);
            Assert.NotNull(button);
            var onClick = (UnityEngine.Events.UnityEvent)buttonType.GetProperty("onClick").GetValue(button);
            Assert.AreEqual(1, ActiveEventSystems());
            onClick.Invoke();
            float timeout = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != "WTF" && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.AreEqual("WTF", SceneManager.GetActiveScene().name);
            yield return null;
            Assert.NotNull(GameObject.Find("Level01 CompletePlayer"));
            Assert.AreEqual(1, ActiveEventSystems(), "Only the active level EventSystem should remain");
            var menu = UnityEngine.Object.FindFirstObjectByType<PlayerPauseMenu>();
            Assert.NotNull(menu);
            menu.Pause();
            yield return null;
            Assert.AreEqual(1, ActiveEventSystems(), "Pause must not create a duplicate EventSystem");
            menu.Resume();
            menu.ReturnToSelection();
            while (SceneManager.GetActiveScene().name != "M6_DevelopmentSelection" && Time.realtimeSinceStartup < timeout) yield return null;
            yield return null;
            Assert.AreEqual("M6_DevelopmentSelection", SceneManager.GetActiveScene().name);
            Assert.AreEqual(1, ActiveEventSystems(), "Selection EventSystem must reactivate after return");
        }

        private static int ActiveEventSystems()
        {
            var type = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI");
            Assert.NotNull(type);
            return Resources.FindObjectsOfTypeAll(type).OfType<Behaviour>()
                .Count(item => item.gameObject.scene.IsValid() && item.isActiveAndEnabled);
        }

        [UnityTest]
        public IEnumerator LevelLoadsAndScanDigUpdatesOrderedProgress()
        {
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            PlayerCursor.BeginSimulation(CursorLockMode.Locked);
            PlayerInputService input = null;
            string originalBindings = null;
            try
            {
                yield return SceneManager.LoadSceneAsync("Assets/_Modules_/02_Levels/Level01/Imported/Scenes/WTF.unity");
                yield return null;
                var player = GameObject.Find("Level01 CompletePlayer");
                Assert.NotNull(player);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled));
                var detectorType = Type.GetType("TreasureDetector, Assembly-CSharp");
                Assert.NotNull(detectorType);
                var detector = player.GetComponentInChildren(detectorType, true);
                Assert.NotNull(detector);
                var targets = (IList)detectorType.GetField("targetBottles").GetValue(detector);
                Assert.AreEqual(10, targets.Count);
                var target = (Transform)targets[0];
                target.position = detector.transform.position;
                input = PlayerInputService.Instance;
                Assert.NotNull(input);
                originalBindings = input.Actions.SaveBindingOverridesAsJson();
                input.Actions.FindAction("Gameplay/Fire").ApplyBindingOverride(0, "<Mouse>/leftButton");
                input.Actions.FindAction("Gameplay/ScanAim").ApplyBindingOverride(0, "<Mouse>/rightButton");
                input.Actions.FindAction("Gameplay/Interact").ApplyBindingOverride(0, "<Keyboard>/f");
                input.SetContext(PlayerInputContext.Gameplay);
                PlayerCursor.LockState = CursorLockMode.Locked;
                var tipObject = GameObject.Find("tips");
                var rect = tipObject.GetComponent<RectTransform>();
                Assert.AreEqual(new Vector2(0.5f, 1f), rect.anchorMin);
                Assert.AreEqual(new Vector2(0.5f, 1f), rect.anchorMax);
                Assert.Less(rect.anchoredPosition.y, 0f);
                var textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
                var tips = tipObject.GetComponent(textType);
                yield return null;
                Assert.AreEqual("掃描：滑鼠右鍵　互動：F　挖掘：滑鼠左鍵", (string)textType.GetProperty("text").GetValue(tips));
                var graphicType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI");
                Assert.IsFalse(((Behaviour)GameObject.Find("aim1").GetComponent(graphicType)).enabled);
                Assert.IsFalse(((Behaviour)GameObject.Find("aim2").GetComponent(graphicType)).enabled);
                Assert.NotNull(GameObject.Find("Crosshair"), "Shared player crosshair remains visible");
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                yield return null;
                yield return null;
                var history = (IList)detectorType.GetField("distanceHistory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(detector);
                Assert.AreEqual(1, history.Count, "Scan records the next ordered target");
                Assert.IsTrue(input.GameplayActionsBlocked, "Weapon actions are blocked during scanning");
                InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return null;
                yield return null;
                Assert.IsFalse((bool)detectorType.GetProperty("IsAnimating").GetValue(detector), "Dig is blocked while scanning");
                InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
                var scan = detector.GetComponent(Type.GetType("scanball, Assembly-CSharp"));
                var scanRunning = scan.GetType().GetProperty("IsRunning");
                float timeout = Time.realtimeSinceStartup + 10f;
                while ((bool)scanRunning.GetValue(scan) && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.IsFalse((bool)scanRunning.GetValue(scan), "Scan effect must finish");
                Assert.IsFalse(input.GameplayActionsBlocked, "Weapon actions resume after scanning");
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); yield return null;
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return new WaitForSeconds(3.5f);
                Assert.AreEqual(1, detectorType.GetProperty("CollectedCount").GetValue(detector));
                Assert.IsFalse(target.gameObject.activeSelf);
                Assert.AreEqual(0, history.Count);
                input.Actions.FindAction("Gameplay/ScanAim").ApplyBindingOverride(0, "<Keyboard>/n");
                input.Actions.FindAction("Gameplay/Fire").ApplyBindingOverride(0, "<Keyboard>/m");
                yield return null;
                string visibleTips = (string)textType.GetProperty("text").GetValue(tips);
                Assert.AreEqual("掃描：N　互動：F　挖掘：M", visibleTips);
                var secondTarget = (Transform)targets[1];
                secondTarget.position = detector.transform.position;
                yield return new WaitForSeconds(4.6f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.N)); yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.AreEqual(1, history.Count, "Rebound ScanAim key must trigger a scan");
                timeout = Time.realtimeSinceStartup + 10f;
                while ((bool)scanRunning.GetValue(scan) && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.IsFalse((bool)scanRunning.GetValue(scan));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.M)); yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(3.5f);
                Assert.AreEqual(2, detectorType.GetProperty("CollectedCount").GetValue(detector), "Rebound Fire key must dig");
                Assert.IsFalse(secondTarget.gameObject.activeSelf);
            }
            finally
            {
                if (input != null && originalBindings != null) input.Actions.LoadBindingOverridesFromJson(originalBindings);
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
                PlayerCursor.EndSimulation();
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditor;
            }
        }
    }
}
