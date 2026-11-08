using System;
using System.Collections;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace M7SharedPlayerTests
{
    public class Level01BindingTests
    {
        [UnityTest]
        public IEnumerator InteractiveRebindCapturesMouseButton()
        {
            var source = Resources.Load<InputActionAsset>("SDGPlayerControls");
            var actions = UnityEngine.Object.Instantiate(source);
            var store = new PlayerSettingsStore("Level01.InteractiveTest." + Guid.NewGuid().ToString("N"));
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse = InputSystem.AddDevice<Mouse>();
            bool complete = false, accepted = false;
            InputActionRebindingExtensions.RebindingOperation operation = null;
            try
            {
                var bindings = new PlayerKeybindingController(actions, store);
                operation = bindings.BeginInteractiveRebind("Gameplay", "Fire", 0,
                    (ok, _) => { complete = true; accepted = ok; operation = null; });
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Middle));
                yield return null;
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.IsTrue(complete, "Rebind should finish after a mouse button");
                Assert.IsTrue(accepted);
                Assert.AreEqual("<Mouse>/middleButton", actions.FindAction("Gameplay/Fire").bindings[0].effectivePath);
            }
            finally
            {
                operation?.Cancel();
                store.Reset(actions);
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
                UnityEngine.Object.DestroyImmediate(actions);
            }
        }

        [Test]
        public void MouseBindingsRejectConflictsPersistAndRestore()
        {
            var source = Resources.Load<InputActionAsset>("SDGPlayerControls");
            Assert.NotNull(source);
            var actions = UnityEngine.Object.Instantiate(source);
            var key = "Level01.BindingTest." + Guid.NewGuid().ToString("N");
            var store = new PlayerSettingsStore(key);
            InputActionAsset reloaded = null;
            try
            {
                var bindings = new PlayerKeybindingController(actions, store);
                Assert.AreEqual("<Mouse>/leftButton", actions.FindAction("Gameplay/Fire").bindings[0].effectivePath);
                Assert.AreEqual("<Mouse>/rightButton", actions.FindAction("Gameplay/ScanAim").bindings[0].effectivePath);
                Assert.IsFalse(bindings.TryApply("Gameplay", "Fire", 0, "<Mouse>/rightButton", out _));
                Assert.IsTrue(bindings.TryApply("Gameplay", "Fire", 0, "<Mouse>/middleButton", out string error), error);
                Assert.IsTrue(bindings.TryApply("Gameplay", "ScanAim", 0, "<Keyboard>/n", out error), error);
                Assert.IsTrue(bindings.TryApply("Gameplay", "Interact", 0, "<Keyboard>/b", out error), error);
                bindings.SetSprintMode(ButtonMode.Toggle);
                reloaded = UnityEngine.Object.Instantiate(source);
                var restored = new PlayerKeybindingController(reloaded, store);
                Assert.AreEqual("<Mouse>/middleButton", reloaded.FindAction("Gameplay/Fire").bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/N", reloaded.FindAction("Gameplay/ScanAim").bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/B", reloaded.FindAction("Gameplay/Interact").bindings[0].effectivePath);
                Assert.AreEqual(ButtonMode.Toggle, restored.Settings.sprintMode);
                restored.RestoreDefaults();
                Assert.AreEqual("<Mouse>/leftButton", reloaded.FindAction("Gameplay/Fire").bindings[0].effectivePath);
                Assert.AreEqual("<Mouse>/rightButton", reloaded.FindAction("Gameplay/ScanAim").bindings[0].effectivePath);
            }
            finally
            {
                store.Reset(actions);
                UnityEngine.Object.DestroyImmediate(actions);
                if (reloaded != null) UnityEngine.Object.DestroyImmediate(reloaded);
            }
        }
    }
}
