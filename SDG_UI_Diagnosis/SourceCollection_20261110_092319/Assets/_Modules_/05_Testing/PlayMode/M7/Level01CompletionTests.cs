using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M7SharedPlayerTests
{
    public class Level01CompletionTests
    {
        private sealed class Counter { public int Count; public void OnCompleted<T>(T value) { Count++; } }
        private static Type Runtime(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static IEnumerator Until(Func<bool> predicate, float seconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(predicate(), message);
        }

        [UnityTest] public IEnumerator CompleteThenRetryResetsLevel() => Exercise(true);
        [UnityTest] public IEnumerator CompleteThenReturnToSelection() => Exercise(false);

        private static IEnumerator Exercise(bool retry)
        {
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            PlayerCursor.BeginSimulation(CursorLockMode.Locked);
            PlayerInputService input = null;
            string originalBindings = null;
            object coordinator = null;
            EventInfo completionEvent = null;
            Delegate handler = null;
            try
            {
                yield return SceneManager.LoadSceneAsync("Assets/_Modules_/02_Levels/Level01/Imported/Scenes/WTF.unity");
                yield return null;
                var player = GameObject.Find("Level01 CompletePlayer");
                var controller = player.GetComponent<CharacterController>();
                var detector = player.GetComponentInChildren(Runtime("TreasureDetector"), true);
                var detectorType = detector.GetType();
                var door = UnityEngine.Object.FindFirstObjectByType(Runtime("DoorInteraction"));
                var doorType = door.GetType();
                var exitObject = GameObject.Find("Level01 Exit Trigger");
                Assert.NotNull(exitObject);
                var coordinatorType = Runtime("SDGAdventure.ModuleSystem.ModuleCoordinator");
                coordinator = coordinatorType.GetProperty("Instance").GetValue(null);
                Assert.NotNull(coordinator);
                var launchNext = coordinatorType.GetMethod("TryLaunchNext");
                object[] nextArgs = { null };
                Assert.IsFalse((bool)launchNext.Invoke(coordinator, nextArgs), "Cannot advance before completion");
                Assert.AreEqual("WTF", SceneManager.GetActiveScene().name);
                completionEvent = coordinatorType.GetEvent("Completed");
                var counter = new Counter();
                var method = typeof(Counter).GetMethod("OnCompleted").MakeGenericMethod(Runtime("SDGAdventure.ModuleSystem.ModuleResult"));
                handler = Delegate.CreateDelegate(completionEvent.EventHandlerType, counter, method);
                completionEvent.AddEventHandler(coordinator, handler);
                input = PlayerInputService.Instance;
                Assert.NotNull(input);
                originalBindings = input.Actions.SaveBindingOverridesAsJson();
                input.Actions.FindAction("Gameplay/Interact").ApplyBindingOverride(0, "<Keyboard>/f");
                input.SetContext(PlayerInputContext.Gameplay);
                PlayerCursor.LockState = CursorLockMode.Locked;

                void Place(Vector3 position)
                {
                    controller.enabled = false; player.transform.position = position; controller.enabled = true;
                    Physics.SyncTransforms();
                }
                var inside = new Vector3(10.8f, 0.5f, -1.2f);
                var beyond = new Vector3(12.478f, 0.5f, 1.5f);
                Place(inside); yield return null;
                Place(beyond); exitObject.SendMessage("OnTriggerEnter", controller); yield return null;
                Assert.AreEqual(0, counter.Count, "Incomplete collection cannot finish");
                Place(inside); yield return null;
                detectorType.GetField("currentIndex", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(detector, 10);
                Place(beyond); exitObject.SendMessage("OnTriggerEnter", controller); yield return null;
                Assert.AreEqual(0, counter.Count, "Ten targets without an open door cannot finish");
                Place(inside); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.IsTrue((bool)doorType.GetProperty("IsBusy").GetValue(door), "F starts original door animation");
                Assert.IsFalse((bool)doorType.GetProperty("IsOpen").GetValue(door));
                Place(beyond); exitObject.SendMessage("OnTriggerEnter", controller); yield return null;
                Assert.AreEqual(0, counter.Count, "Door animation must finish before exit");
                Place(new Vector3(12.478f, 0.5f, -1.2f)); yield return null;
                yield return Until(() => (bool)doorType.GetProperty("IsOpen").GetValue(door), 10f, "Door did not finish opening");
                Assert.AreEqual(0, counter.Count, "Opening alone cannot finish");
                var nonPlayer = new GameObject("Non-player exit test");
                var nonPlayerCollider = nonPlayer.AddComponent<BoxCollider>();
                exitObject.SendMessage("OnTriggerEnter", nonPlayerCollider);
                UnityEngine.Object.Destroy(nonPlayer);
                Assert.AreEqual(0, counter.Count, "Non-player colliders cannot finish the level");
                for (int i = 0; i < 35 && counter.Count == 0; i++)
                {
                    controller.Move(Vector3.forward * 0.12f);
                    yield return null;
                }
                Assert.AreEqual(1, counter.Count, "Crossing exit must report exactly once");
                nextArgs[0] = null;
                Assert.IsFalse((bool)launchNext.Invoke(coordinator, nextArgs), "Missing Level02 must not be skipped");
                StringAssert.Contains("level02", (string)nextArgs[0]);
                Assert.AreEqual("WTF", SceneManager.GetActiveScene().name, "Unavailable next level must leave the current scene active");
                Assert.AreEqual(PlayerInputContext.UI, input.Context);
                Assert.AreEqual(CursorLockMode.None, PlayerCursor.LockState);
                Assert.AreEqual(0f, Time.timeScale);
                var view = GameObject.Find("Level01CompletionCanvas");
                Assert.NotNull(view);
                var buttonType = Type.GetType("UnityEngine.UI.Button, UnityEngine.UI", true);
                var textType = Type.GetType("UnityEngine.UI.Text, UnityEngine.UI", true);
                var heading = view.transform.Find("Overlay/Panel/Heading").GetComponent(textType);
                Assert.AreEqual("第一關完成！", textType.GetProperty("text").GetValue(heading));
                var next = view.transform.Find("Overlay/Panel/NextLevel").GetComponent(buttonType);
                Assert.IsFalse((bool)buttonType.GetProperty("interactable").GetValue(next));
                var nextLabel = view.transform.Find("Overlay/Panel/NextLevel/Label").GetComponent(textType);
                Assert.AreEqual("下一關（尚未開放）", textType.GetProperty("text").GetValue(nextLabel));
                var eventType = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI", true);
                Assert.AreEqual(1, Resources.FindObjectsOfTypeAll(eventType).OfType<Behaviour>()
                    .Count(x => x.gameObject.scene.IsValid() && x.isActiveAndEnabled));
                exitObject.SendMessage("OnTriggerEnter", controller);
                Assert.AreEqual(1, counter.Count, "Repeated trigger is ignored");
                var actionButton = view.transform.Find(retry ? "Overlay/Panel/Retry" : "Overlay/Panel/ReturnToSelection").GetComponent(buttonType);
                ((UnityEngine.Events.UnityEvent)buttonType.GetProperty("onClick").GetValue(actionButton)).Invoke();
                yield return Until(() => retry ? SceneManager.GetActiveScene().name == "WTF" && GameObject.Find("Level01CompletionCanvas") == null :
                    SceneManager.GetActiveScene().name == "M6_DevelopmentSelection", 10f, "Requested scene did not load");
                yield return null;
                if (retry)
                {
                    var nextPlayer = GameObject.Find("Level01 CompletePlayer");
                    var nextDetector = nextPlayer.GetComponentInChildren(Runtime("TreasureDetector"), true);
                    Assert.AreEqual(0, detectorType.GetProperty("CollectedCount").GetValue(nextDetector));
                    var targets = (IList)detectorType.GetField("targetBottles").GetValue(nextDetector);
                    Assert.AreEqual(10, targets.Count);
                    Assert.IsTrue(targets.Cast<Transform>().All(x => x.gameObject.activeSelf));
                    var nextDoor = UnityEngine.Object.FindFirstObjectByType(Runtime("DoorInteraction"));
                    Assert.IsFalse((bool)doorType.GetProperty("IsOpen").GetValue(nextDoor));
                }
                Assert.AreEqual(1, Resources.FindObjectsOfTypeAll(eventType).OfType<Behaviour>()
                    .Count(x => x.gameObject.scene.IsValid() && x.isActiveAndEnabled));
            }
            finally
            {
                if (completionEvent != null && coordinator != null && handler != null) completionEvent.RemoveEventHandler(coordinator, handler);
                if (input != null && originalBindings != null) input.Actions.LoadBindingOverridesFromJson(originalBindings);
                InputSystem.RemoveDevice(keyboard);
                PlayerCursor.EndSimulation();
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
                Time.timeScale = 1f;
            }
        }
    }
}
