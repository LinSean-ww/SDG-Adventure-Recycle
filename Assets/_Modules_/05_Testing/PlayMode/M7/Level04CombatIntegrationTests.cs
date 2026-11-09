using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace M7SharedPlayerTests {
 public sealed class Level04CombatIntegrationTests {
  const string Scene="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity";
  static Type T(string n)=>Type.GetType(n+", Assembly-CSharp",true);
  static object Get(object o,string n)=>o.GetType().GetProperty(n).GetValue(o);
  static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);
  [UnityTest] public IEnumerator HealthInvulnerabilityPauseDeathAndRetry(){
   PlayerCursor.BeginSimulation(CursorLockMode.Locked);try{
    SceneManager.LoadScene(Scene,LoadSceneMode.Single);yield return null;yield return null;
    var player=UnityEngine.Object.FindFirstObjectByType<SharedFirstPersonController>();var health=player.GetComponent(T("SDGAdventure.Level04.Level04PlayerHealth"));var boss=(Component)UnityEngine.Object.FindObjectOfType(T("BossHealth"));var input=PlayerInputService.Instance;Assert.NotNull(health);Assert.AreEqual(100,Get(health,"CurrentHealth"));
    Vector3 origin=player.transform.position;Assert.IsTrue((bool)Call(health,"TryBossHit",boss,20,boss.transform.position));Assert.AreEqual(80,Get(health,"CurrentHealth"));Assert.IsTrue(player.IsExternallyDisplaced);Assert.IsFalse((bool)Call(health,"TryBossHit",boss,20,boss.transform.position));Assert.AreEqual(80,Get(health,"CurrentHealth"));
    var menu=player.GetComponent<PlayerPauseMenu>();menu.Pause();Assert.AreEqual(PlayerInputContext.UI,input.Context);Vector3 paused=player.transform.position;yield return new WaitForSecondsRealtime(.15f);Assert.Less(Vector3.Distance(paused,player.transform.position),.001f);menu.Resume();
    yield return new WaitForSeconds(.15f);Assert.Greater(player.transform.position.y,origin.y+.05f,"Knockback should launch upward");
    yield return new WaitForSeconds(1.1f);Assert.IsTrue((bool)Call(health,"TryBossHit",boss,20,boss.transform.position));Assert.AreEqual(60,Get(health,"CurrentHealth"));
    for(int i=0;i<3;i++){health.GetType().GetField("invulnerableUntil",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(health,0f);Assert.IsTrue((bool)Call(health,"TryBossHit",boss,20,boss.transform.position));}
    Assert.IsTrue((bool)Get(health,"IsDead"));Assert.AreEqual(0,Get(health,"CurrentHealth"));Assert.IsFalse(player.IsExternallyDisplaced);Assert.AreEqual(PlayerInputContext.UI,input.Context);Assert.AreEqual(0f,Time.timeScale);Assert.NotNull(GameObject.Find("Level04FailureCanvas"));
    var coord=T("SDGAdventure.ModuleSystem.ModuleCoordinator").GetProperty("Instance").GetValue(null);Assert.IsNull(Get(coord,"LastResult"),"Failure must not report completion");
    var buttonType=Type.GetType("UnityEngine.UI.Button, UnityEngine.UI",true);var retry=GameObject.Find("Retry");((UnityEvent)buttonType.GetProperty("onClick").GetValue(retry.GetComponent(buttonType))).Invoke();yield return null;yield return null;
    player=UnityEngine.Object.FindFirstObjectByType<SharedFirstPersonController>();health=player.GetComponent(T("SDGAdventure.Level04.Level04PlayerHealth"));Assert.AreEqual(100,Get(health,"CurrentHealth"));Assert.AreEqual(1f,Time.timeScale);boss=(Component)UnityEngine.Object.FindObjectOfType(T("BossHealth"));Assert.AreEqual(5,Get(boss,"CurrentHealth"));
    Assert.IsTrue((bool)Call(health,"TryBossHit",boss,100,boss.transform.position));var back=GameObject.Find("ReturnToSelection");Assert.NotNull(back);((UnityEvent)buttonType.GetProperty("onClick").GetValue(back.GetComponent(buttonType))).Invoke();yield return null;Assert.AreEqual("M6_DevelopmentSelection",SceneManager.GetActiveScene().name);
   }finally{Time.timeScale=1f;PlayerCursor.EndSimulation();}
  }
  [UnityTest] public IEnumerator ExternalImpulseIsSingleMotorPathAndStopsAtWall(){
   var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);var obj=new GameObject("Knockback test player");
   try{floor.transform.position=new Vector3(1000,99.5f,1000);floor.transform.localScale=new Vector3(30,1,30);wall.transform.position=new Vector3(1002,101,1000);wall.transform.localScale=new Vector3(.2f,4,8);obj.transform.position=new Vector3(1000,100.03f,1000);var view=new GameObject("TestCamera");view.transform.SetParent(obj.transform);view.AddComponent<Camera>().enabled=false;obj.AddComponent<CharacterController>();var motor=obj.AddComponent<SharedFirstPersonController>();motor.enabled=false;Physics.SyncTransforms();
    motor.ApplyExternalImpulse(new Vector3(7,6,0),.25f);float peak=obj.transform.position.y;for(int i=0;i<120;i++){motor.TickMotor(new Vector2(-1,0),false,false,true,.02f);peak=Mathf.Max(peak,obj.transform.position.y);Assert.GreaterOrEqual(obj.transform.position.y,99.9f,"Must not cross the floor");Assert.LessOrEqual(obj.transform.position.x,1001.63f,"Must not cross the wall");}Assert.Greater(peak,100.3f);Assert.IsFalse(motor.IsExternallyDisplaced,"Control must return after landing");
   }finally{UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(floor);}yield return null;
  }
  [UnityTest] public IEnumerator AnimatedBossAttacksOnlyOnceAndUsesNavigation(){
   PlayerCursor.BeginSimulation(CursorLockMode.Locked);try{SceneManager.LoadScene(Scene,LoadSceneMode.Single);yield return null;yield return null;
    var ai=(Component)UnityEngine.Object.FindObjectOfType(T("ToiletMechBossAI"));var agent=ai.GetComponent<NavMeshAgent>();var animator=(Animator)ai.GetType().GetField("animator").GetValue(ai);Assert.NotNull(animator);Assert.AreEqual(ai.transform,animator.transform.parent);Assert.IsFalse(animator.applyRootMotion);Assert.IsTrue(agent.isOnNavMesh,"Boss should be on baked NavMesh");
    var player=UnityEngine.Object.FindFirstObjectByType<SharedFirstPersonController>();var motor=player.GetComponent<CharacterController>();var health=player.GetComponent(T("SDGAdventure.Level04.Level04PlayerHealth"));motor.enabled=false;player.transform.position=new Vector3(ai.transform.position.x,.55f,ai.transform.position.z+2.8f);motor.enabled=true;ai.transform.rotation=Quaternion.identity;Physics.SyncTransforms();agent.isStopped=true;
    var phase=ai.GetType().GetField("phase",BindingFlags.NonPublic|BindingFlags.Instance);phase.SetValue(ai,Enum.Parse(phase.FieldType,"Attacking"));ai.GetType().GetField("attackStateStarted",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(ai,false);ai.GetType().GetField("hitAttempted",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(ai,false);ai.GetType().GetField("attackRequestTime",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(ai,Time.time);animator.Play("Attack1",0,0);animator.Update(0);
    yield return new WaitForSeconds(3.05f);Assert.AreEqual(80,Get(health,"CurrentHealth"),"One animation attack should produce one 20 HP hit");
   }finally{Time.timeScale=1;PlayerCursor.EndSimulation();}
  }
 }
}