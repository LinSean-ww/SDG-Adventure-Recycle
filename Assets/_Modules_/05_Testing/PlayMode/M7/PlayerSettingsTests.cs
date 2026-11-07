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
            Assert.AreEqual("<Keyboard>/e", gameplay.FindAction("Interact", true).bindings[0].effectivePath);
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
            Assert.AreEqual("<Keyboard>/e", actions.FindActionMap("Gameplay").FindAction("Interact").bindings[0].effectivePath);
            Assert.IsTrue(bindings.TryApply("Gameplay", "Interact", 0, "<Keyboard>/g", out error), error);
            Assert.AreEqual("<Keyboard>/G", actions.FindActionMap("Gameplay").FindAction("Interact").bindings[0].effectivePath);
        }

        [Test]
        public void RestoreDefaultsClearsOverridesAndModes()
        {
            Assert.IsTrue(bindings.TryApply("Gameplay", "Interact", 0, "<Keyboard>/g", out string error), error);
            bindings.SetSprintMode(ButtonMode.Toggle);
            bindings.SetCrouchMode(ButtonMode.Toggle);
            bindings.RestoreDefaults();
            Assert.AreEqual("<Keyboard>/e", actions.FindActionMap("Gameplay").FindAction("Interact").bindings[0].effectivePath);
            Assert.AreEqual(ButtonMode.Hold, bindings.Settings.sprintMode);
            Assert.AreEqual(ButtonMode.Hold, bindings.Settings.crouchMode);
            Assert.IsFalse(PlayerPrefs.HasKey(key));
        }

        [Test]
        public void SavedOverridesAndModesReload()
        {
            Assert.IsTrue(bindings.TryApply("Gameplay", "Interact", 0, "<Keyboard>/g", out string error), error);
            bindings.SetSprintMode(ButtonMode.Toggle);
            bindings.SetCrouchMode(ButtonMode.Toggle);
            InputActionAsset reloaded = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>("SDGPlayerControls"));
            try
            {
                var loaded = new PlayerKeybindingController(reloaded, store);
                Assert.AreEqual("<Keyboard>/G", reloaded.FindActionMap("Gameplay").FindAction("Interact").bindings[0].effectivePath);
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
        public void CorruptSettingsRecoverToDefaults()
        {
            PlayerPrefs.SetString(key, "{broken");
            PlayerPrefs.Save();
            LogAssert.Expect(LogType.Warning, new Regex("Player settings were invalid and were reset"));
            PlayerSettingsData loaded = store.Load(actions);
            Assert.AreEqual(ButtonMode.Hold, loaded.sprintMode);
            Assert.AreEqual("<Keyboard>/e", actions.FindActionMap("Gameplay").FindAction("Interact").bindings[0].effectivePath);
            Assert.IsFalse(PlayerPrefs.HasKey(key));
        }
    }
}
