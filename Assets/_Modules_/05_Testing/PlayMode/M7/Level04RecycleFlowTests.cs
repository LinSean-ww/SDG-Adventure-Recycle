using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace M7SharedPlayerTests {
 public sealed class Level04RecycleFlowTests {
  private sealed class Counter { public int Value; public void Count<T>(T value){Value++;} }
  const string Path="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity";
  static Type T(string n)=>Type.GetType(n+", Assembly-CSharp",true);
  static object Prop(object o,string n)=>o.GetType().GetProperty(n).GetValue(o);
  static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n).Invoke(o,a);
  [UnityTest] public IEnumerator BossDeathRecycleProgressCompletionAndTransitions(){
   var background=InputSystem.settings.backgroundBehavior;var editor=InputSystem.settings.editorInputBehaviorInPlayMode;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
   var keyboard=InputSystem.AddDevice<Keyboard>();PlayerCursor.BeginSimulation(CursorLockMode.Locked);var input=PlayerInputService.Instance;var interact=input.Actions.FindAction("Gameplay/Interact");var drop=input.Actions.FindAction("Gameplay/Drop");string oldI=interact.bindings[0].overridePath,oldD=drop.bindings[0].overridePath;object boss=null,coord=null,bag=null;Action death=null;Type rt=null;bool addedQ=false,addedP=false;string[] previousSlots=null;int previousSelection=0;EventInfo completeEvent=null;Delegate completeHandler=null;
   try{
    interact.ApplyBindingOverride(0,"<Keyboard>/n");drop.ApplyBindingOverride(0,"<Keyboard>/m");SceneManager.LoadScene(Path,LoadSceneMode.Single);yield return null;yield return null;
    coord=T("SDGAdventure.ModuleSystem.ModuleCoordinator").GetProperty("Instance").GetValue(null);Assert.AreEqual("level04",Prop(coord,"CurrentModuleId"));
    var counter=new Counter();completeEvent=coord.GetType().GetEvent("Completed");var method=typeof(Counter).GetMethod("Count").MakeGenericMethod(T("SDGAdventure.ModuleSystem.ModuleResult"));completeHandler=Delegate.CreateDelegate(completeEvent.EventHandlerType,counter,method);completeEvent.AddEventHandler(coord,completeHandler);
    boss=UnityEngine.Object.FindObjectOfType(T("BossHealth"));var recycle=T("SDGAdventure.Level04.Level04RecycleController").GetProperty("Instance").GetValue(null);var house=UnityEngine.Object.FindObjectOfType(T("RecycleHouse"));Assert.NotNull(recycle);Assert.NotNull(house);
    int deaths=0;death=()=>deaths++;boss.GetType().GetEvent("Died").AddEventHandler(boss,death);Assert.AreEqual(5,Prop(boss,"CurrentHealth"));Assert.IsFalse((bool)Prop(recycle,"RecycleMode"));
    rt=T("EcoAdventureRuntime");bag=rt.GetProperty("Instance").GetValue(null);var slots=(string[])rt.GetField("quickSlots").GetValue(bag);previousSlots=(string[])slots.Clone();previousSelection=(int)rt.GetField("selectedSlot").GetValue(bag);var has=rt.GetMethod("Has");addedQ=!(bool)has.Invoke(bag,new object[]{"eco_quantum_projector",1});addedP=!(bool)has.Invoke(bag,new object[]{"level04_plasma_gun",1});if(addedQ)rt.GetMethod("AddItem").Invoke(bag,new object[]{"eco_quantum_projector","Q",1});if(addedP)rt.GetMethod("AddItem").Invoke(bag,new object[]{"level04_plasma_gun","P",1});var deathSlots=(string[])slots.Clone();var ammo=rt.GetField("ammoInMagazine").GetValue(bag);var reserve=rt.GetField("reserveAmmo").GetValue(bag);
    for(int i=0;i<5;i++)Call(boss,"TakeDamage",1);Call(boss,"TakeDamage",1);Assert.AreEqual(1,deaths);Assert.IsTrue((bool)Prop(recycle,"RecycleMode"));Assert.AreEqual(ammo,rt.GetField("ammoInMagazine").GetValue(bag));Assert.AreEqual(reserve,rt.GetField("reserveAmmo").GetValue(bag));Assert.IsTrue((bool)has.Invoke(bag,new object[]{"eco_quantum_projector",1}));Assert.IsTrue((bool)has.Invoke(bag,new object[]{"level04_plasma_gun",1}));CollectionAssert.AreEqual(deathSlots,(string[])rt.GetField("quickSlots").GetValue(bag));
    var items=UnityEngine.Object.FindObjectsOfType(T("RecycleItem")).Cast<Component>().ToArray();Assert.AreEqual(5,items.Length);Component GetItem(string type)=>items.Single(x=>x.GetType().GetField("itemType").GetValue(x).ToString()==type);
    var tyre=GetItem("Tire");var player=UnityEngine.Object.FindObjectOfType<SharedFirstPersonController>();var cam=Camera.main;var interactor=player.GetComponent<PlayerInteractor>();tyre.transform.position=cam.transform.position+cam.transform.forward*1.25f;Physics.SyncTransforms();Assert.NotNull(interactor.FindTarget());
    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.N));InputSystem.Update();yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();yield return null;Assert.AreSame(tyre,Prop(recycle,"Carried"));
    input.SetContext(PlayerInputContext.UI);InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.M));yield return null;Assert.AreSame(tyre,Prop(recycle,"Carried"));InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();yield return null;input.SetContext(PlayerInputContext.Gameplay);
    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.M));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();yield return null;Assert.IsNull(Prop(recycle,"Carried"));Assert.IsTrue((bool)has.Invoke(bag,new object[]{"eco_quantum_projector",1}));Assert.IsTrue((bool)has.Invoke(bag,new object[]{"level04_plasma_gun",1}));
    var hud=player.GetComponent<PlayerHudCanvas>();StringAssert.Contains("N",hud.StatusText.ToUpperInvariant());
    string[] answers={"Tire","Tire","GlassBottle","Disc","Motherboard"};
    for(int i=0;i<5;i++){
      var item=GetItem(answers[i]);Call(recycle,"Interact",item);yield return null;
      if(i==0){
       var root=((Component)house).GetComponentsInChildren<Transform>(true).Single(x=>x.name=="SubmitPoint");var proxy=root.GetComponents<BoxCollider>().Single(x=>!x.isTrigger);root.position+=(cam.transform.position+cam.transform.forward*1.25f)-proxy.bounds.center;Physics.SyncTransforms();
       Assert.NotNull(interactor.FindTarget(),"Submit point must be targetable through shared raycast");LogAssert.Expect(LogType.Warning,"答案錯誤！物品退回地面。");
       InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.N));InputSystem.Update();InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
      } else Call(recycle,"Interact",new object[]{null});
      Assert.AreEqual(i+1,Prop(house,"CompletedQuestions"));Call(recycle,"Interact",new object[]{null});Assert.AreEqual(i+1,Prop(house,"CompletedQuestions"));
      if(i<4)Assert.IsNull(Prop(coord,"LastResult"),"No premature completion");yield return null;
    }
    Assert.NotNull(Prop(coord,"LastResult"));Assert.AreEqual(1,counter.Value,"Completion event must occur once");Assert.AreEqual(PlayerInputContext.UI,input.Context);Assert.NotNull(GameObject.Find("Level04CompletionCanvas"));
    var buttonType=Type.GetType("UnityEngine.UI.Button, UnityEngine.UI",true);var retry=GameObject.Find("Retry");Assert.NotNull(retry);((UnityEngine.Events.UnityEvent)buttonType.GetProperty("onClick").GetValue(retry.GetComponent(buttonType))).Invoke();yield return null;yield return null;
    Assert.AreEqual("level04",Prop(coord,"CurrentModuleId"));Assert.IsEmpty((string)Prop(bag,"CarriedTaskItemText"),"Retry must clear the task item display");var newBoss=UnityEngine.Object.FindObjectOfType(T("BossHealth"));Assert.IsFalse((bool)Prop(newBoss,"IsDead"));Assert.IsNull(Prop(coord,"LastResult"));
    Call(newBoss,"TakeDamage",5);var newHouse=UnityEngine.Object.FindObjectOfType(T("RecycleHouse"));var newItems=UnityEngine.Object.FindObjectsOfType(T("RecycleItem")).Cast<Component>().ToArray();
    foreach(string name in new[]{"Brick","Tire","GlassBottle","Disc","Motherboard"}){var next=newItems.Single(x=>x.GetType().GetField("itemType").GetValue(x).ToString()==name);Assert.IsTrue((bool)Call(newHouse,"TrySubmit",next));yield return null;}
    Assert.AreEqual(2,counter.Value,"One completion event for each attempt");var back=GameObject.Find("ReturnToSelection");Assert.NotNull(back);((UnityEngine.Events.UnityEvent)buttonType.GetProperty("onClick").GetValue(back.GetComponent(buttonType))).Invoke();yield return null;Assert.AreEqual("M6_DevelopmentSelection",SceneManager.GetActiveScene().name);Assert.IsEmpty((string)Prop(bag,"CarriedTaskItemText"),"Returning to selection must not retain a ghost task item");
   }finally{if(completeEvent!=null&&coord!=null)completeEvent.RemoveEventHandler(coord,completeHandler);if(bag!=null&&previousSlots!=null){if(addedQ)rt.GetMethod("Consume").Invoke(bag,new object[]{"eco_quantum_projector",1});if(addedP)rt.GetMethod("Consume").Invoke(bag,new object[]{"level04_plasma_gun",1});Array.Copy(previousSlots,(string[])rt.GetField("quickSlots").GetValue(bag),previousSlots.Length);rt.GetField("selectedSlot").SetValue(bag,previousSelection);}if(boss!=null && (UnityEngine.Object)boss)boss.GetType().GetEvent("Died").RemoveEventHandler(boss,death);if(oldI==null)interact.RemoveBindingOverride(0);else interact.ApplyBindingOverride(0,oldI);if(oldD==null)drop.RemoveBindingOverride(0);else drop.ApplyBindingOverride(0,oldD);PlayerCursor.EndSimulation();InputSystem.RemoveDevice(keyboard);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;Time.timeScale=1f;}
  }
 }
}