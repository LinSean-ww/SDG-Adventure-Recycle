using System;
using System.Collections;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M7SharedPlayerTests
{
    public sealed class Level02IntegrationTests
    {
        private const string LevelScene = "Assets/_Modules_/02_Levels/Level02/Imported/Scenes/SampleScene.unity";

        [UnityTest]
        public IEnumerator SelectionLaunchesOneConfiguredLevel02Player()
        {
            yield return SceneManager.LoadSceneAsync("Assets/_Modules_/05_Testing/Scenes/M6_DevelopmentSelection.unity", LoadSceneMode.Single);
            yield return null;
            var launch = GameObject.Find("LaunchLevel02");
            Assert.NotNull(launch);
            var buttonType = Type.GetType("UnityEngine.UI.Button, UnityEngine.UI", true);
            var button = launch.GetComponent(buttonType);
            ((UnityEvent)buttonType.GetProperty("onClick").GetValue(button)).Invoke();
            yield return null;
            Assert.AreEqual(LevelScene, SceneManager.GetActiveScene().path);
            var players = UnityEngine.Object.FindObjectsOfType<SharedFirstPersonController>();
            Assert.AreEqual(1, players.Length);
            Assert.AreEqual(1, GameObject.FindGameObjectsWithTag("MainCamera").Length);
            Assert.NotNull(players[0].GetComponent<PlayerInteractor>().Extension);
            Assert.NotNull(players[0].GetComponent<CharacterController>());
            var coordinatorType = Type.GetType("SDGAdventure.ModuleSystem.ModuleCoordinator, Assembly-CSharp", true);
            var coordinator = coordinatorType.GetProperty("Instance").GetValue(null);
            Assert.AreEqual("level02", coordinatorType.GetProperty("CurrentModuleId").GetValue(coordinator));
        }

        [UnityTest]
        public IEnumerator DetectorRequiresEmptyHandsAndRestoresTimeOnCancel()
        {
            yield return SceneManager.LoadSceneAsync(LevelScene, LoadSceneMode.Single);
            yield return null;
            var player = UnityEngine.Object.FindObjectOfType<SharedFirstPersonController>();
            var detectorType = Type.GetType("StarDetectorMap, Assembly-CSharp", true);
            var detector = UnityEngine.Object.FindObjectOfType(detectorType);
            Assert.NotNull(detector);
            var input = PlayerInputService.Instance;
            input.SetContext(PlayerInputContext.Gameplay);
            Assert.IsTrue((bool)detectorType.GetMethod("CanInteract").Invoke(detector, new object[] { player.gameObject }));
            float previous = Time.timeScale;
            detectorType.GetMethod("Interact").Invoke(detector, new object[] { player.gameObject });
            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreEqual(PlayerInputContext.UI, input.Context);
            detectorType.GetMethod("Close", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(detector, null);
            Assert.AreEqual(previous, Time.timeScale);
            Assert.AreEqual(PlayerInputContext.Gameplay, input.Context);
        }
    }
}
