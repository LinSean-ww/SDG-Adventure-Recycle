using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using SDGAdventure.CompletePlayer;
using SDGAdventure.SharedPlayer;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SDGAdventure.PlayerControlTests
{
    public sealed class CompletePlayerV12InputTests
    {
        [UnityTest]
        public IEnumerator DemoInputUiAndFiveSlotsRemainAvailable()
        {
            var oldBackground = InputSystem.settings.backgroundBehavior;
            var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            bool hadView = PlayerPrefs.HasKey(PlayerViewPreference.Key);
            int previousView = PlayerPrefs.GetInt(PlayerViewPreference.Key, 0);
            Keyboard keyboard = null;
            PlayerInputService input = null;
            string originalBindings = null;
            ButtonMode oldSprint = ButtonMode.Hold, oldCrouch = ButtonMode.Hold;
            PlayerCursor.BeginSimulation(CursorLockMode.None);
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Modules_/01_Shared/CompletePlayer/Demo/CompletePlayer_Demo.unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                input = PlayerInputService.Instance;
                var menu = UnityEngine.Object.FindFirstObjectByType<PlayerPauseMenu>();
                var demo = UnityEngine.Object.FindFirstObjectByType<CompletePlayerDemoInventory>();
                Assert.NotNull(input); Assert.NotNull(menu); Assert.NotNull(demo);
                Assert.AreSame(input, typeof(CompletePlayerDemoInventory).GetField("input", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(demo), "Demo event service");
                originalBindings = input.Actions.SaveBindingOverridesAsJson();
                oldSprint = input.Keybindings.Settings.sprintMode; oldCrouch = input.Keybindings.Settings.crouchMode;
                input.Keybindings.Settings.sprintMode = ButtonMode.Hold; input.Keybindings.Settings.crouchMode = ButtonMode.Hold;
                // Temporary overrides only: never save to the user's PlayerPrefs.
                input.Actions.FindAction("Gameplay/Inventory").ApplyBindingOverride(0, "<Keyboard>/tab");
                input.Actions.FindAction("UI/Inventory").ApplyBindingOverride(0, "<Keyboard>/tab");
                input.Actions.FindAction("Global/Pause").ApplyBindingOverride(0, "<Keyboard>/escape");
                input.Actions.FindAction("UI/Cancel").ApplyBindingOverride(0, "<Keyboard>/escape");
                for (int i = 1; i <= 5; i++) input.Actions.FindAction("Gameplay/Slot" + i).ApplyBindingOverride(0, "<Keyboard>/" + i);
                PlayerCursor.LockState = CursorLockMode.Locked;
                Assert.AreEqual(PlayerInputContext.Gameplay, input.Context, "Gameplay context after loading Demo");
                Assert.IsTrue(input.Actions.FindAction("Gameplay/Slot2").enabled, "Slot2 enabled");
                Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
                for (int i = 0; i < keys.Length; i++)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[i])); InputSystem.Update();
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                    yield return null;
                    Assert.AreEqual(i + 1, demo.SelectedSlot, "key " + keys[i] + " context " + input.Context + " menu " + menu.State + " action " + input.Actions.FindAction("Gameplay/Slot" + (i+1)).bindings[0].effectivePath);
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                Assert.IsTrue(demo.InventoryOpen); Assert.AreEqual(PlayerInputContext.UI, input.Context);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                Assert.IsFalse(demo.InventoryOpen); Assert.AreEqual(PlayerInputContext.Gameplay, input.Context);
                var move = input.Actions.FindAction("Gameplay/Move");
                string[] moveKeys = { "w", "s", "a", "d" };
                for (int i = 0; i < 4; i++) move.ApplyBindingOverride(i + 1, "<Keyboard>/" + moveKeys[i]);
                foreach (var pair in new[] { ("Jump", "space"), ("Sprint", "leftShift"), ("Crouch", "leftCtrl"), ("Prone", "z"), ("LeanLeft", "q"), ("LeanRight", "e"), ("Interact", "f"), ("TaskList", "p") })
                    input.Actions.FindAction("Gameplay/" + pair.Item1).ApplyBindingOverride(0, "<Keyboard>/" + pair.Item2);
                input.Actions.FindAction("UI/TaskList").ApplyBindingOverride(0, "<Keyboard>/p");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); InputSystem.Update(); Assert.Greater(input.Move.y, .9f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S)); InputSystem.Update(); Assert.Less(input.Move.y, -.9f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); InputSystem.Update(); Assert.Less(input.Move.x, -.9f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D)); InputSystem.Update(); Assert.Greater(input.Move.x, .9f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift)); InputSystem.Update(); Assert.IsTrue(input.SprintActive);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl)); InputSystem.Update(); Assert.IsTrue(input.CrouchActive);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); InputSystem.Update(); Assert.IsTrue(input.LeanLeftActive);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); InputSystem.Update(); Assert.IsTrue(input.LeanRightActive);
                int jumps = 0, interactions = 0, prones = 0;
                Action onJump = () => jumps++; Action onInteract = () => interactions++; Action onProne = () => prones++;
                input.JumpPressed += onJump; input.InteractPressed += onInteract; input.PronePressed += onProne;
                try
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); InputSystem.Update();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); InputSystem.Update();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Z)); InputSystem.Update();
                    Assert.AreEqual(1, jumps); Assert.AreEqual(1, interactions); Assert.AreEqual(1, prones);
                }
                finally { input.JumpPressed -= onJump; input.InteractPressed -= onInteract; input.PronePressed -= onProne; }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.P)); InputSystem.Update(); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                Assert.AreEqual(PlayerMenuState.Tasks, menu.State); menu.CloseTasks();
                menu.Pause(); menu.OpenSettings(); yield return null;
                Assert.AreEqual(PlayerMenuState.Settings, menu.State);
                string[] actions = { "Prone", "LeanLeft", "LeanRight", "Inventory", "Slot1", "Slot2", "Slot3", "Slot4", "Slot5" };
                foreach (string action in actions)
                    Assert.NotNull(menu.View.Panel.Find("ScrollViewport/ScrollContent/Binding_" + action + "_0"), action);
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<PlayerInputService>(FindObjectsSortMode.None).Length);
            }
            finally
            {
                if (input && originalBindings != null)
                {
                    input.Keybindings.Settings.sprintMode = oldSprint; input.Keybindings.Settings.crouchMode = oldCrouch;
                    input.Actions.RemoveAllBindingOverrides();
                    input.Actions.LoadBindingOverridesFromJson(originalBindings);
                }
                Time.timeScale = 1;
                PlayerCursor.EndSimulation();
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
                if (hadView) PlayerPrefs.SetInt(PlayerViewPreference.Key, previousView); else PlayerPrefs.DeleteKey(PlayerViewPreference.Key);
                PlayerPrefs.Save();
            }
        }
    }
}
