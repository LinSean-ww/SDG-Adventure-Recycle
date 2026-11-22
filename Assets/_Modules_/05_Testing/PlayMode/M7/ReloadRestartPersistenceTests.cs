using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace M7SharedPlayerTests
{
    // Separate Unity process invocations verify actual PlayerPrefs persistence.
    public sealed class ReloadRestartPersistenceTests
    {
        private const string Key = "SDGAdventure.ReloadAcceptance.20261108";
        [Test] public void WriteReloadBeforeRestart()
        {
            Assert.IsFalse(PlayerPrefs.HasKey(Key), "An existing value must not be overwritten.");
            var actions = Object.Instantiate(Resources.Load<InputActionAsset>("SDGPlayerControls"));
            try
            {
                var bindings = new PlayerKeybindingController(actions, new PlayerSettingsStore(Key));
                Assert.IsTrue(bindings.TryApply("Gameplay", "Reload", 0, "<Keyboard>/n", out string error), error);
                Assert.IsTrue(PlayerPrefs.HasKey(Key));
            }
            finally { Object.DestroyImmediate(actions); }
        }
        [Test] public void ReadReloadAfterRestartAndRestore()
        {
            var actions = Object.Instantiate(Resources.Load<InputActionAsset>("SDGPlayerControls"));
            try
            {
                Assert.IsTrue(PlayerPrefs.HasKey(Key), "The prior Unity process did not save the test key.");
                var bindings = new PlayerKeybindingController(actions, new PlayerSettingsStore(Key));
                Assert.AreEqual("<Keyboard>/N", actions.FindAction("Gameplay/Reload").bindings[0].effectivePath);
                bindings.RestoreDefaults();
                Assert.AreEqual("<Keyboard>/r", actions.FindAction("Gameplay/Reload").bindings[0].effectivePath);
                Assert.IsFalse(PlayerPrefs.HasKey(Key));
            }
            finally { Object.DestroyImmediate(actions); }
        }
    }
}
