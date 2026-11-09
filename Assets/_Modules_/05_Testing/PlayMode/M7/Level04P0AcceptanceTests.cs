using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace M7SharedPlayerTests {
 public class Level04P0AcceptanceTests {
  const string Scene="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity", Plasma="level04_plasma_gun", Quantum="eco_quantum_projector";
  static Type T(string n)=>Type.GetType(n+", Assembly-CSharp",true);
  static object Get(object o,string n)=>o.GetType().GetProperty(n).GetValue(o);
  static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);
  static void VerifyBar(int hp){
   var track=GameObject.Find("PlayerHealth/Track").GetComponent<RectTransform>();var bar=track.Find("Fill").GetComponent<RectTransform>();var image=bar.GetComponent(Type.GetType("UnityEngine.UI.Image, UnityEngine.UI",true));var label=track.parent.Find("Label").GetComponent(Type.GetType("UnityEngine.UI.Text, UnityEngine.UI",true));Canvas.ForceUpdateCanvases();Assert.IsTrue(bar.gameObject.activeInHierarchy);Assert.AreEqual(hp/100f,bar.rect.width/track.rect.width,.002f,"Rendered bar width");Assert.AreEqual(hp/100f,bar.anchorMax.x,.001f);Assert.AreEqual(hp/100f,Convert.ToSingle(image.GetType().GetProperty("fillAmount").GetValue(image)),.001f);Assert.That((string)label.GetType().GetProperty("text").GetValue(label),Does.Contain(hp+" / 100"));
  }
  [UnityTest] public IEnumerator HealthUpdatesActualUiImmediatelyThroughDeathAndRetry(){
   PlayerCursor.BeginSimulation(CursorLockMode.Locked);try{SceneManager.LoadScene(Scene);yield return null;yield return null;var player=UnityEngine.Object.FindFirstObjectByType<SharedFirstPersonController>();var health=player.GetComponent(T("SDGAdventure.Level04.Level04PlayerHealth"));var boss=(Component)UnityEngine.Object.FindFirstObjectByType(T("BossHealth"));Assert.AreEqual(1,player.GetComponents(T("SDGAdventure.Level04.Level04PlayerHealth")).Length);VerifyBar(100);
    var menu=player.GetComponent<PlayerPauseMenu>();menu.Pause();VerifyBar(100);Assert.IsFalse((bool)Call(health,"TryBossHit",boss,20,boss.transform.position));menu.Resume();PlayerViewPreference.Mode=PlayerViewMode.ThirdPerson;yield return null;VerifyBar(100);
    for(int hp=80;hp>=0;hp-=20){health.GetType().GetField("invulnerableUntil",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(health,0f);Assert.IsTrue((bool)Call(health,"TryBossHit",boss,20,boss.transform.position));Assert.AreEqual(hp,Get(health,"CurrentHealth"));VerifyBar(hp);if(hp>0){Assert.IsFalse((bool)Call(health,"TryBossHit",boss,20,boss.transform.position),"Invulnerability blocks duplicate hit");VerifyBar(hp);}}
    Assert.NotNull(GameObject.Find("Level04FailureCanvas"));var button=GameObject.Find("Retry");var type=Type.GetType("UnityEngine.UI.Button, UnityEngine.UI",true);((UnityEvent)type.GetProperty("onClick").GetValue(button.GetComponent(type))).Invoke();yield return null;yield return null;VerifyBar(100);Assert.AreEqual(1,UnityEngine.Object.FindFirstObjectByType<SharedFirstPersonController>().GetComponents(T("SDGAdventure.Level04.Level04PlayerHealth")).Length);
   }finally{Time.timeScale=1;PlayerViewPreference.Mode=PlayerViewMode.FirstPerson;PlayerCursor.EndSimulation();}
  }
  [UnityTest] public IEnumerator PickupSelectsPlasmaAndRendersInBothPerspectives(){
   object bag=null;Type bt=null;bool hadQ=false,hadP=false;PlayerCursor.BeginSimulation(CursorLockMode.Locked);try{SceneManager.LoadScene(Scene);yield return null;yield return null;bt=T("EcoAdventureRuntime");bag=bt.GetProperty("Instance").GetValue(null);hadQ=(bool)Call(bag,"Has",Quantum,1);hadP=(bool)Call(bag,"Has",Plasma,1);var pickup=(Component)UnityEngine.Object.FindFirstObjectByType(T("SDGAdventure.Level04.Level04PlasmaPickup"));var player=UnityEngine.Object.FindFirstObjectByType<SharedFirstPersonController>();var cam=Camera.main;
    Call(bag,"AddItem",Quantum,"Quantum",1);var slots=(string[])bt.GetField("quickSlots").GetValue(bag);int q=Array.IndexOf(slots,Quantum);bt.GetField("selectedSlot").SetValue(bag,q);((IPlayerInteractable)pickup).Interact(player.gameObject);Assert.IsTrue((bool)Call(bag,"Has",Plasma,1));int p=Array.IndexOf(slots,Plasma);Assert.GreaterOrEqual(p,0);Assert.AreEqual(p,bt.GetField("selectedSlot").GetValue(bag));Assert.IsFalse(pickup.gameObject.activeSelf);
    foreach(var mode in new[]{PlayerViewMode.FirstPerson,PlayerViewMode.ThirdPerson}){PlayerViewPreference.Mode=mode;yield return null;yield return null;var model=(GameObject)bt.GetField("weaponModel",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bag);Assert.NotNull(model);Assert.IsTrue(model.activeInHierarchy);var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&(cam.cullingMask&(1<<r.gameObject.layer))!=0).ToArray();Assert.Greater(renderers.Length,0);Assert.IsTrue(renderers.Any(r=>{var v=cam.WorldToViewportPoint(r.bounds.center);return v.z>cam.nearClipPlane&&v.x>0&&v.x<1&&v.y>0&&v.y<1;}),"At least one visible model renderer must project into camera view");}
    bt.GetField("selectedSlot").SetValue(bag,q);yield return null;Assert.IsTrue((bool)Call(bag,"Has",Plasma,1));bt.GetField("selectedSlot").SetValue(bag,p);yield return null;Assert.IsTrue((bool)Call(bag,"Has",Quantum,1));
   }finally{if(bag!=null){if(!hadP&&(bool)Call(bag,"Has",Plasma,1))Call(bag,"Consume",Plasma,1);if(!hadQ&&(bool)Call(bag,"Has",Quantum,1))Call(bag,"Consume",Quantum,1);}PlayerViewPreference.Mode=PlayerViewMode.FirstPerson;PlayerCursor.EndSimulation();}
  }
 }
}
