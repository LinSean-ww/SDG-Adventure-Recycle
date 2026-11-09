using System.Collections;
using System.Linq;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace M7SharedPlayerTests {
 public sealed class Level04PlayerIntegrationTests {
  const string ScenePath="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity";
  [UnityTest] public IEnumerator SceneHasOneInitializedSharedPlayerAndSafeSpawn() {
   Assert.IsTrue(Application.CanStreamedLevelBeLoaded(ScenePath));
   SceneManager.LoadScene(ScenePath,LoadSceneMode.Single); yield return null; yield return new WaitForFixedUpdate();
   var players=Object.FindObjectsOfType<SharedFirstPersonController>(); Assert.AreEqual(1,players.Length);
   var player=players[0]; Assert.IsTrue(player.isActiveAndEnabled); Assert.IsTrue(player.GetComponent<CharacterController>().enabled);
   Assert.AreEqual(1,Object.FindObjectsOfType<Camera>().Count(c=>c.isActiveAndEnabled));
   Assert.NotNull(Camera.main);Assert.IsTrue(Camera.main.transform.IsChildOf(player.transform));
   Assert.AreEqual(1,Object.FindObjectsOfType<PlayerHudCanvas>().Length);
   Assert.NotNull(player.GetComponent<PlayerInteractor>());Assert.NotNull(player.GetComponent<PlayerPauseMenu>());
   Assert.AreEqual(PlayerInputContext.Gameplay,PlayerInputService.Instance.Context);
   Assert.IsTrue(player.IsGrounded,"Player should spawn on ground");
   var boss=Object.FindObjectsOfType<MonoBehaviour>().Single(x=>x.GetType().Name=="ToiletMechBossAI");
   Assert.AreEqual(player.transform,boss.GetType().GetField("player").GetValue(boss));
   var legacy=Object.FindObjectsOfType<MonoBehaviour>(true).Where(x=>x!=null&&x.GetType().FullName=="SDGAdventure.Level04.Imported.PlayerMove").ToArray();
   Assert.AreEqual(1,legacy.Length);Assert.IsFalse(legacy[0].isActiveAndEnabled);
   var menu=player.GetComponent<PlayerPauseMenu>();menu.Pause();Assert.AreEqual(PlayerInputContext.UI,PlayerInputService.Instance.Context);menu.Resume();Assert.AreEqual(PlayerInputContext.Gameplay,PlayerInputService.Instance.Context);
  }
 }
}