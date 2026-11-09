using System;
using System.Collections;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M7SharedPlayerTests
{
    public sealed class Level02V12Tests
    {
        [Test]
        public void HeadlampBindingRejectsConflictPersistsAndRestores()
        {
            var source = Resources.Load<InputActionAsset>("SDGPlayerControls");
            Assert.NotNull(source);
            var asset = UnityEngine.Object.Instantiate(source);
            var key = "SDGAdventure.Level02V12.Tests." + Guid.NewGuid().ToString("N");
            var store = new PlayerSettingsStore(key);
            InputActionAsset reloaded = null;
            try
            {
                var bindings = new PlayerKeybindingController(asset, store);
                var action = asset.FindAction("Gameplay/Headlamp", true);
                Assert.AreEqual("<Keyboard>/l", action.bindings[0].effectivePath);
                Assert.IsFalse(bindings.TryApply("Gameplay", "Headlamp", 0, "<Keyboard>/f", out _));
                Assert.AreEqual("<Keyboard>/l", action.bindings[0].effectivePath);
                Assert.IsTrue(bindings.TryApply("Gameplay", "Headlamp", 0, "<Keyboard>/n", out string error), error);
                reloaded = UnityEngine.Object.Instantiate(source);
                var loaded = new PlayerKeybindingController(reloaded, store);
                Assert.AreEqual("<Keyboard>/N", reloaded.FindAction("Gameplay/Headlamp", true).bindings[0].effectivePath);
                loaded.RestoreDefaults();
                Assert.AreEqual("<Keyboard>/l", reloaded.FindAction("Gameplay/Headlamp", true).bindings[0].effectivePath);
            }
            finally
            {
                store.Reset(asset);
                UnityEngine.Object.DestroyImmediate(asset);
                if (reloaded) UnityEngine.Object.DestroyImmediate(reloaded);
            }
        }

        [Test]
        public void ExistingLBindingIsPreservedWhenHeadlampIsAdded()
        {
            var source = Resources.Load<InputActionAsset>("SDGPlayerControls");
            Assert.NotNull(source);
            var asset = UnityEngine.Object.Instantiate(source);
            var reloaded = UnityEngine.Object.Instantiate(source);
            var key = "SDGAdventure.Level02V12.Tests." + Guid.NewGuid().ToString("N");
            var store = new PlayerSettingsStore(key);
            try
            {
                asset.FindAction("Gameplay/Move", true).ApplyBindingOverride(1, "<Keyboard>/l");
                store.Save(asset, new PlayerSettingsData());
                new PlayerKeybindingController(reloaded, store);
                Assert.AreEqual("<Keyboard>/l", reloaded.FindAction("Gameplay/Move", true).bindings[1].effectivePath);
                Assert.AreNotEqual("<Keyboard>/l", reloaded.FindAction("Gameplay/Headlamp", true).bindings[0].effectivePath);
            }
            finally
            {
                store.Reset(asset);
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(reloaded);
            }
        }

        [UnityTest]
        public IEnumerator HeadlampUsesWorkerHeadAndDetectorSuppressesOnlyQuickbar()
        {
            yield return SceneManager.LoadSceneAsync("Assets/_Modules_/02_Levels/Level02/Imported/Scenes/SampleScene.unity", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForEndOfFrame();
            var player = UnityEngine.Object.FindObjectOfType<SharedFirstPersonController>();
            Assert.NotNull(player);
            var head = player.transform.Find("V11_RecyclingEngineer_Visual/WorkerPelvis/WorkerHead");
            var mount = GameObject.Find("Level02HeadlampMount");
            Assert.NotNull(head);
            Assert.NotNull(mount);
            Assert.Less(Vector3.Distance(head.position, mount.transform.position), 0.31f);
            Assert.AreNotSame(player.GetComponentInChildren<Camera>().transform, mount.transform.parent);
            Assert.AreEqual(1, CountHeadlamps());
            var runtimeType = Type.GetType("EcoAdventureRuntime, Assembly-CSharp", true);
            var runtime = runtimeType.GetProperty("Instance").GetValue(null);
            var detectorType = Type.GetType("StarDetectorMap, Assembly-CSharp", true);
            var detector = UnityEngine.Object.FindObjectOfType(detectorType);
            var input = PlayerInputService.Instance;
            input.SetContext(PlayerInputContext.Gameplay);
            detectorType.GetMethod("Interact").Invoke(detector, new object[] { player.gameObject });
            Assert.IsTrue((bool)runtimeType.GetProperty("QuickbarSuppressed").GetValue(runtime));
            detectorType.GetMethod("Close", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(detector, null);
            Assert.IsFalse((bool)runtimeType.GetProperty("QuickbarSuppressed").GetValue(runtime));
        }

        private static int CountHeadlamps()
        {
            int count = 0;
            foreach (var light in UnityEngine.Object.FindObjectsOfType<Light>()) if (light.gameObject.name == "Level02HeadlampMount") count++;
            return count;
        }
    }
}
