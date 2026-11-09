using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace M7SharedPlayerTests {
 public sealed class Level04DoorSpawnTests {
  [UnityTest] public IEnumerator SpawnIsClearDoorIsSolidAndBossRouteExists(){
   SceneManager.LoadScene("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",LoadSceneMode.Single);yield return null;yield return null;
   var player=UnityEngine.Object.FindFirstObjectByType<SharedFirstPersonController>();var controller=player.GetComponent<CharacterController>();var door=GameObject.Find("Level04 D North Solid Door");Assert.NotNull(door);Assert.IsTrue(door.GetComponent<Collider>().enabled);Assert.IsFalse(door.GetComponent<Collider>().isTrigger);
   Assert.That(player.transform.position.x,Is.EqualTo(-12.5f).Within(.05f));Assert.Less(player.transform.position.z,-1.5f);Assert.Less(player.transform.forward.z,-.9f);
   float radius=controller.radius-controller.skinWidth;var center=player.transform.TransformPoint(controller.center);float half=controller.height*.5f-radius;var overlaps=Physics.OverlapCapsule(center+Vector3.up*half,center-Vector3.up*half,radius,~0,QueryTriggerInteraction.Ignore).Where(c=>!c.transform.IsChildOf(player.transform)).ToArray();Assert.IsEmpty(overlaps,"Spawn must not overlap obstacles");
   var ai=(Component)UnityEngine.Object.FindFirstObjectByType(Type.GetType("ToiletMechBossAI, Assembly-CSharp",true));Assert.IsTrue(NavMesh.SamplePosition(player.transform.position,out var a,2,NavMesh.AllAreas));Assert.IsTrue(NavMesh.SamplePosition(ai.transform.position,out var b,2,NavMesh.AllAreas));var route=new NavMeshPath();Assert.IsTrue(NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,route));Assert.AreEqual(NavMeshPathStatus.PathComplete,route.status);
   player.enabled=false;for(int i=0;i<40;i++)controller.Move(Vector3.forward*.1f);Assert.Less(player.transform.position.z,-.35f,"Player must not pass through door");Assert.Greater(player.transform.position.y,.45f,"Player must not fall through factory floor");
  }
 }
}
