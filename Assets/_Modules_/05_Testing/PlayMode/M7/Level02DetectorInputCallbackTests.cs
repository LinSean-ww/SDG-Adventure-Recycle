using System;
using System.Collections;
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
    public sealed class Level02DetectorInputCallbackTests
    {
        private const string ScenePath = "Assets/_Modules_/02_Levels/Level02/Imported/Scenes/SampleScene.unity";

        [UnityTest]
        public IEnumerator DefaultInteractClosesWithoutDisposingInsideCallback() => CheckClose(Key.F, false);

        [UnityTest]
        public IEnumerator ReboundInteractClosesWithoutDisposingInsideCallback() => CheckClose(Key.H, true);

        [UnityTest]
        public IEnumerator EscapeClosesAndRestoresGameplay() => CheckClose(Key.Escape, false);

        private IEnumerator CheckClose(Key key, bool rebind)
        {
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            PlayerInputService input = null;
            string originalBindings = null;
            UnityEngine.Object detector = null;
            Type detectorType = null;
            try
            {
                yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
                yield return null;
                input = PlayerInputService.Instance;
                Assert.NotNull(input);
                originalBindings = input.Actions.SaveBindingOverridesAsJson();
                var interact = input.Actions.FindAction("Gameplay/Interact", true);
                if (rebind) interact.ApplyBindingOverride(0, "<Keyboard>/h");
                else if (key == Key.F) interact.ApplyBindingOverride(0, "<Keyboard>/f");
                var player = UnityEngine.Object.FindObjectOfType<SharedFirstPersonController>();
                detectorType = Type.GetType("StarDetectorMap, Assembly-CSharp", true);
                detector = UnityEngine.Object.FindObjectOfType(detectorType);
                Assert.NotNull(player);
                Assert.NotNull(detector);
                var runtimeType = Type.GetType("EcoAdventureRuntime, Assembly-CSharp", true);
                var runtime = runtimeType.GetProperty("Instance").GetValue(null);
                float originalTimeScale = Time.timeScale;
                input.SetContext(PlayerInputContext.Gameplay);
                detectorType.GetMethod("Interact").Invoke(detector, new object[] { player.gameObject });
                Assert.AreEqual(PlayerInputContext.UI, input.Context);
                Assert.IsTrue((bool)runtimeType.GetProperty("QuickbarSuppressed").GetValue(runtime));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                InputSystem.Update();
                yield return null;
                yield return null;
                Assert.AreEqual(PlayerInputContext.Gameplay, input.Context);
                Assert.AreEqual(originalTimeScale, Time.timeScale);
                Assert.IsFalse((bool)runtimeType.GetProperty("QuickbarSuppressed").GetValue(runtime));
                Assert.AreEqual(PlayerMenuState.Playing, player.GetComponent<PlayerPauseMenu>().State);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (detector && detectorType != null)
                    detectorType.GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(detector, null);
                if (input && originalBindings != null) input.Actions.LoadBindingOverridesFromJson(originalBindings);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            }
        }
    }
}
