using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace M7SharedPlayerTests
{
    public sealed class PlayerSettingsTests
    {
        private InputActionAsset actions;
        private PlayerSettingsStore store;
        private PlayerKeybindingController bindings;
        private string key;

        [SetUp]
        public void SetUp()
        {
            InputActionAsset source = Resources.Load<InputActionAsset>("SDGPlayerControls");
            Assert.IsNotNull(source, "The shared actions asset must import as an InputActionAsset.");
            actions = UnityEngine.Object.Instantiate(source);
            key = "SDGAdventure.M7.Tests." + Guid.NewGuid().ToString("N");
            store = new PlayerSettingsStore(key);
            bindings = new PlayerKeybindingController(actions, store);
        }

        [TearDown]
        public void TearDown()
        {
            store.Reset(actions);
            UnityEngine.Object.DestroyImmediate(actions);
        }

        [Test]
        public void DefaultsMatchUnifiedControls()
        {
            InputActionMap gameplay = actions.FindActionMap("Gameplay", true);
            InputAction move = gameplay.FindAction("Move", true);
            Assert.AreEqual("<Keyboard>/w", move.bindings[1].effectivePath);
            Assert.AreEqual("<Keyboard>/s", move.bindings[2].effectivePath);
            Assert.AreEqual("<Keyboard>/a", move.bindings[3].effectivePath);
            Assert.AreEqual("<Keyboard>/d", move.bindings[4].effectivePath);
            Assert.AreEqual("<Mouse>/delta", gameplay.FindAction("Look", true).bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/space", gameplay.FindAction("Jump", true).bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/leftShift", gameplay.FindAction("Sprint", true).bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/leftCtrl", gameplay.FindAction("Crouch", true).bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/f", gameplay.FindAction("Interact", true).bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/escape", actions.FindActionMap("Global", true)
                .FindAction("Pause", true).bindings[0].effectivePath);
            Assert.AreEqual(ButtonMode.Hold, bindings.Settings.sprintMode);
            Assert.AreEqual(ButtonMode.Hold, bindings.Settings.crouchMode);
        }

        [Test]
        public void RebindRejectsConflictWithoutChangingOldBinding()
        {
            Assert.IsFalse(bindings.TryApply("Gameplay", "Interact", 0, "<Keyboard>/space", out string error));
            StringAssert.Contains("Gameplay/Jump", error);
            Assert.AreEqual("<Keyboard>/f", actions.FindActionMap("Gameplay").FindAction("Interact").bindings[0].effectivePath);
            Assert.IsTrue(bindings.TryApply("Gameplay", "Interact", 0, "<Keyboard>/n", out error), error);
            Assert.AreEqual("<Keyboard>/N", actions.FindActionMap("Gameplay").FindAction("Interact").bindings[0].effectivePath);
        }

        [Test]
        public void RestoreDefaultsClearsOverridesAndModes()
        {
            Assert.IsTrue(bindings.TryApply("Gameplay", "Interact", 0, "<Keyboard>/n", out string error), error);
            bindings.SetSprintMode(ButtonMode.Toggle);
            bindings.SetCrouchMode(ButtonMode.Toggle);
            bindings.RestoreDefaults();
            Assert.AreEqual("<Keyboard>/f", actions.FindActionMap("Gameplay").FindAction("Interact").bindings[0].effectivePath);
            Assert.AreEqual(ButtonMode.Hold, bindings.Settings.sprintMode);
            Assert.AreEqual(ButtonMode.Hold, bindings.Settings.crouchMode);
            Assert.IsFalse(PlayerPrefs.HasKey(key));
        }

        [Test]
        public void SavedOverridesAndModesReload()
        {
            Assert.IsTrue(bindings.TryApply("Gameplay", "Interact", 0, "<Keyboard>/n", out string error), error);
            bindings.SetSprintMode(ButtonMode.Toggle);
            bindings.SetCrouchMode(ButtonMode.Toggle);
            InputActionAsset reloaded = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>("SDGPlayerControls"));
            try
            {
                var loaded = new PlayerKeybindingController(reloaded, store);
                Assert.AreEqual("<Keyboard>/N", reloaded.FindActionMap("Gameplay").FindAction("Interact").bindings[0].effectivePath);
                Assert.AreEqual(ButtonMode.Toggle, loaded.Settings.sprintMode);
                Assert.AreEqual(ButtonMode.Toggle, loaded.Settings.crouchMode);
            }
            finally { UnityEngine.Object.DestroyImmediate(reloaded); }
        }

        [Test]
        public void ProneActionRespondsOnlyToCurrentKey()
        {
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var prone = actions.FindAction("Gameplay/Prone", true);
            int presses = 0;
            prone.performed += _ => presses++;
            try
            {
                prone.Enable(); InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Z)); InputSystem.Update();
                Assert.AreEqual(1, presses);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                Assert.IsTrue(bindings.TryApply("Gameplay", "Prone", 0, "<Keyboard>/v", out string error), error);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Z)); InputSystem.Update();
                Assert.AreEqual(1, presses);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.V)); InputSystem.Update();
                Assert.AreEqual(2, presses);
            }
            finally
            {
                prone.Disable(); InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
                InputSystem.settings.backgroundBehavior = previousBackground;
            }
        }

        [Test]
        public void ProneBindingCanRebindRejectConflictReloadAndReset()
        {
            var prone = actions.FindAction("Gameplay/Prone", true);
            Assert.AreEqual("<Keyboard>/z", prone.bindings[0].effectivePath);
            Assert.IsFalse(bindings.TryApply("Gameplay", "Prone", 0, "<Keyboard>/space", out string error));
            StringAssert.Contains("Jump", error);
            Assert.IsTrue(bindings.TryApply("Gameplay", "Prone", 0, "<Keyboard>/v", out error), error);
            var reloaded = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>("SDGPlayerControls"));
            try
            {
                var loaded = new PlayerKeybindingController(reloaded, store);
                Assert.AreEqual("<Keyboard>/V", reloaded.FindAction("Gameplay/Prone", true).bindings[0].effectivePath);
                loaded.RestoreDefaults();
                Assert.AreEqual("<Keyboard>/z", reloaded.FindAction("Gameplay/Prone", true).bindings[0].effectivePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(reloaded); }
        }

        [Test]
        public void NewBindingsRebindPersistRejectConflictsAndReset()
        {
            string[] names = { "LeanLeft", "LeanRight", "Inventory", "Slot1", "Slot2", "Slot3", "Slot4", "Slot5" };
            string[] defaults = { "q", "e", "tab", "1", "2", "3", "4", "5" };
            string[] keys = { "b", "c", "n", "h", "j", "k", "l", "v" };
            for (int i = 0; i < names.Length; i++)
            {
                Assert.AreEqual("<Keyboard>/" + defaults[i], actions.FindAction("Gameplay/" + names[i], true).bindings[0].effectivePath);
                Assert.IsFalse(bindings.TryApply("Gameplay", names[i], 0, "<Keyboard>/space", out _));
                Assert.IsTrue(bindings.TryApply("Gameplay", names[i], 0, "<Keyboard>/" + keys[i], out string error), error);
            }
            Assert.IsTrue(bindings.TryApply("Gameplay", "Slot1", 0, "<Keyboard>/h", out _));
            Assert.IsFalse(bindings.TryApply("Gameplay", "Slot2", 0, "<Keyboard>/h", out _));
            Assert.IsFalse(bindings.TryApply("Gameplay", "Inventory", 0, "<Keyboard>/enter", out _));
            Assert.IsFalse(bindings.TryApply("Gameplay", "Slot1", 0, "<Keyboard>/r", out _));
            Assert.IsTrue(bindings.TryApply("Gameplay", "Slot1", 0, "<Keyboard>/9", out _));
            Assert.AreEqual("<Keyboard>/9", actions.FindAction("Gameplay/Slot1").bindings[0].effectivePath);
            Assert.IsTrue(bindings.TryApply("Gameplay", "Slot1", 0, "<Keyboard>/h", out _));
            var reloaded = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>("SDGPlayerControls"));
            try
            {
                var loaded = new PlayerKeybindingController(reloaded, store);
                for (int i = 0; i < names.Length; i++)
                    Assert.AreEqual("<Keyboard>/" + keys[i].ToUpperInvariant(), reloaded.FindAction("Gameplay/" + names[i], true).bindings[0].effectivePath);
                Assert.AreEqual(reloaded.FindAction("Gameplay/Inventory").bindings[0].effectivePath, reloaded.FindAction("UI/Inventory").bindings[0].effectivePath);
                loaded.RestoreDefaults();
                for (int i = 0; i < names.Length; i++)
                    Assert.AreEqual("<Keyboard>/" + defaults[i], reloaded.FindAction("Gameplay/" + names[i], true).bindings[0].effectivePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(reloaded); }
        }

        [Test]
        public void NewActionsRespondOnlyToReboundKeys()
        {
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            string[] names = { "LeanLeft", "LeanRight", "Inventory", "Slot1", "Slot2", "Slot3", "Slot4", "Slot5" };
            Key[] oldKeys = { Key.Q, Key.E, Key.Tab, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
            Key[] newKeys = { Key.B, Key.C, Key.N, Key.H, Key.J, Key.K, Key.L, Key.V };
            try
            {
                for (int i = 0; i < names.Length; i++)
                {
                    var action = actions.FindAction("Gameplay/" + names[i], true);
                    int count = 0;
                    action.performed += _ => count++;
                    action.Enable(); InputSystem.Update();
                    Assert.IsTrue(bindings.TryApply("Gameplay", names[i], 0, "<Keyboard>/" + newKeys[i], out string error), error);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(oldKeys[i])); InputSystem.Update();
                    Assert.AreEqual(0, count, names[i] + " old key");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(newKeys[i])); InputSystem.Update();
                    Assert.AreEqual(1, count, names[i] + " new key");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                    action.Disable();
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
                InputSystem.settings.backgroundBehavior = previousBackground;
            }
        }

        [Test]
        public void ReloadRebindChecksConflictsPersistsAndRestores()
        {
            var reload = actions.FindAction("Gameplay/Reload", true);
            Assert.AreEqual("<Keyboard>/r", reload.bindings[0].effectivePath);
            Assert.IsFalse(bindings.TryApply("Gameplay", "Reload", 0, "<Keyboard>/space", out string error));
            StringAssert.Contains("Jump", error);
            Assert.IsFalse(bindings.TryApply("Gameplay", "Slot1", 0, "<Keyboard>/r", out error));
            StringAssert.Contains("Reload", error);
            Assert.IsTrue(bindings.TryApply("Gameplay", "Reload", 0, "<Keyboard>/n", out error), error);
            Assert.IsFalse(bindings.TryApply("Gameplay", "Slot1", 0, "<Keyboard>/n", out error));
            Assert.IsTrue(bindings.TryApply("Gameplay", "Slot1", 0, "<Keyboard>/r", out error), error);
            var clone = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>("SDGPlayerControls"));
            try
            {
                var loaded = new PlayerKeybindingController(clone, store);
                Assert.AreEqual("<Keyboard>/N", clone.FindAction("Gameplay/Reload").bindings[0].effectivePath);
                loaded.RestoreDefaults();
                Assert.AreEqual("<Keyboard>/r", clone.FindAction("Gameplay/Reload").bindings[0].effectivePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }

        [Test]
        public void ReloadNewKeyImmediatelyReplacesOldKey()
        {
            var background = InputSystem.settings.backgroundBehavior;
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var reload = actions.FindAction("Gameplay/Reload", true);
            int count = 0;
            reload.performed += _ => count++;
            try
            {
                reload.Enable(); InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R)); InputSystem.Update();
                Assert.AreEqual(1, count, "default R");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                Assert.IsTrue(bindings.TryApply("Gameplay", "Reload", 0, "<Keyboard>/n", out string error), error);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R)); InputSystem.Update(); Assert.AreEqual(1, count, "old R");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.N)); InputSystem.Update(); Assert.AreEqual(2, count, "new N");
            }
            finally
            {
                reload.Disable(); InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                InputSystem.settings.backgroundBehavior = background;
            }
        }

        [Test]
        public void LegacyGOverrideMovesOnlyDropAndPersists()
        {
            actions.FindAction("Gameplay/Reload", true).ApplyBindingOverride(0, "<Keyboard>/g");
            store.Save(actions, bindings.Settings);
            var clone = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>("SDGPlayerControls"));
            try
            {
                LogAssert.Expect(LogType.Warning, "Drop binding moved to H to preserve an existing key binding.");
                var loaded = new PlayerKeybindingController(clone, store);
                Assert.AreEqual("<Keyboard>/g", clone.FindAction("Gameplay/Reload").bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/h", clone.FindAction("Gameplay/Drop").bindings[0].effectivePath);
                var again = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>("SDGPlayerControls"));
                try
                {
                    new PlayerKeybindingController(again, store);
                    Assert.AreEqual("<Keyboard>/h", again.FindAction("Gameplay/Drop").bindings[0].effectivePath);
                }
                finally { UnityEngine.Object.DestroyImmediate(again); }
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }

        [Test]
        public void DropBindingRejectsConflictsPersistsAndRestores()
        {
            var drop = actions.FindAction("Gameplay/Drop", true);
            Assert.AreEqual("<Keyboard>/g", drop.bindings[0].effectivePath);
            Assert.IsFalse(bindings.TryApply("Gameplay", "Drop", 0, "<Keyboard>/space", out string error));
            Assert.IsFalse(bindings.TryApply("Gameplay", "Interact", 0, "<Keyboard>/g", out error));
            Assert.IsTrue(bindings.TryApply("Gameplay", "Drop", 0, "<Keyboard>/n", out error), error);
            var clone = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>("SDGPlayerControls"));
            try
            {
                var loaded = new PlayerKeybindingController(clone, store);
                Assert.AreEqual("<Keyboard>/N", clone.FindAction("Gameplay/Drop").bindings[0].effectivePath);
                loaded.RestoreDefaults();
                Assert.AreEqual("<Keyboard>/g", clone.FindAction("Gameplay/Drop").bindings[0].effectivePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }

        [Test]
        public void CorruptSettingsRecoverToDefaults()
        {
            PlayerPrefs.SetString(key, "{broken");
            PlayerPrefs.Save();
            LogAssert.Expect(LogType.Warning, new Regex("Player settings were invalid and were reset"));
            PlayerSettingsData loaded = store.Load(actions);
            Assert.AreEqual(ButtonMode.Hold, loaded.sprintMode);
            Assert.AreEqual("<Keyboard>/f", actions.FindActionMap("Gameplay").FindAction("Interact").bindings[0].effectivePath);
            Assert.IsFalse(PlayerPrefs.HasKey(key));
        }
    }
}
