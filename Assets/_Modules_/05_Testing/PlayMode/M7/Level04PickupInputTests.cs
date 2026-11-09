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
 public sealed class Level04PickupInputTests {
  [UnityTest] public IEnumerator ReboundInteractPicksUpAndReboundDropDoesNotDuplicate() {
   var oldBackground=InputSystem.settings.backgroundBehavior;var oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
   InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
   var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();PlayerCursor.BeginSimulation(CursorLockMode.Locked);
   const string path="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity", id="level04_plasma_gun";
   var input=PlayerInputService.Instance;var interact=input.Actions.FindAction("Gameplay/Interact");var drop=input.Actions.FindAction("Gameplay/Drop");string oldI=interact.bindings[0].overridePath,oldD=drop.bindings[0].overridePath;
   object bag=null;Type bt=Type.GetType("EcoAdventureRuntime, Assembly-CSharp",true);GameObject dropped=null;GameObject bossObject=null;
   try {
    interact.ApplyBindingOverride(0,"<Keyboard>/n");drop.ApplyBindingOverride(0,"<Keyboard>/m");
    SceneManager.LoadScene(path,LoadSceneMode.Single);yield return null;yield return null;bag=bt.GetProperty("Instance").GetValue(null);var has=bt.GetMethod("Has");Assert.IsFalse((bool)has.Invoke(bag,new object[]{id,1}));
    var pickupType=Type.GetType("SDGAdventure.Level04.Level04PlasmaPickup, Assembly-CSharp",true);var pickup=UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Single(x=>x.GetType()==pickupType);
    var camera=Camera.main;var player=camera.GetComponentInParent<SharedFirstPersonController>();var interactor=player.GetComponent<PlayerInteractor>();
    pickup.transform.position=camera.transform.position+camera.transform.forward*1.25f;Physics.SyncTransforms();Assert.AreSame(pickup,interactor.FindTarget(),"Pickup must be reachable with shared raycast");
    int events=0; Action onInteract=()=>events++; input.InteractPressed+=onInteract;
    Assert.AreSame(input,PlayerInputService.Instance);Assert.AreEqual(PlayerInputContext.Gameplay,input.Context);Assert.IsTrue(interact.enabled);Assert.AreEqual("<Keyboard>/n",interact.bindings[0].effectivePath,"Override should survive scene load");
    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.N));InputSystem.Update();
    input.InteractPressed-=onInteract;Assert.Greater(events,0,"Rebound action event did not fire; keyboard N="+keyboard.nKey.isPressed+" controls="+interact.controls.Count);yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();yield return null;
    Assert.IsTrue((bool)has.Invoke(bag,new object[]{id,1}),"Rebound interact must acquire plasma gun"); Assert.IsFalse(pickup.gameObject.activeSelf,"World pickup must disappear after successful acquisition");
    var slots=(string[])bt.GetField("quickSlots").GetValue(bag);bt.GetField("selectedSlot").SetValue(bag,Array.IndexOf(slots,id));
    bossObject=GameObject.CreatePrimitive(PrimitiveType.Sphere);bossObject.transform.localScale=Vector3.one*.22f;bossObject.transform.position=camera.transform.position+camera.transform.forward*.65f;var bossType=Type.GetType("BossHealth, Assembly-CSharp",true);var boss=bossObject.AddComponent(bossType);Physics.SyncTransforms();yield return null;
    var hp=bossType.GetField("currentHealth",BindingFlags.Instance|BindingFlags.NonPublic);Assert.AreEqual(5,hp.GetValue(boss));
    input.SetContext(PlayerInputContext.UI);InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.M));yield return null;Assert.AreEqual(5,hp.GetValue(boss),"UI blocks plasma firing");Assert.IsTrue((bool)has.Invoke(bag,new object[]{id,1}),"UI blocks dropping");InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();yield return null;input.SetContext(PlayerInputContext.Gameplay);bt.GetField("nextPlasmaShotTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(bag,0f);bossObject.transform.position=camera.transform.position+camera.transform.forward*.65f;Physics.SyncTransforms();InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));yield return null;Assert.AreEqual(4,hp.GetValue(boss),"Gameplay Fire damages the Boss once");var lightning=GameObject.Find("vfx_Lightning_02(Clone)");var projectile=GameObject.Find("vfx_Projectile_01(Clone)");Assert.NotNull(lightning,"Original muzzle lightning should spawn");Assert.NotNull(projectile,"Original projectile should spawn");Vector3 start=projectile.transform.position;yield return new WaitForSeconds(.08f);Assert.Greater(Vector3.Distance(start,projectile.transform.position),.001f,"Projectile should move toward hit");Assert.AreEqual(4,hp.GetValue(boss),"Visual effects must not deal additional damage");InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;bossObject.SetActive(false);
    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.M));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();yield return null;
    Assert.IsFalse((bool)has.Invoke(bag,new object[]{id,1}),"Rebound drop should remove item from inventory");dropped=GameObject.Find("DroppedLevel04Plasma");Assert.NotNull(dropped);
    bt.GetMethod("AddItem").Invoke(bag,new object[]{id,"P",1});Assert.IsFalse((bool)has.Invoke(bag,new object[]{id,1}),"Dropped weapon prevents duplicate grants");
    var target=(IPlayerInteractable)dropped.GetComponent(pickupType);target.Interact(player.gameObject);Assert.IsTrue((bool)has.Invoke(bag,new object[]{id,1}));Assert.IsFalse((bool)has.Invoke(bag,new object[]{id,2})); Assert.AreEqual(Array.IndexOf(slots,id),bt.GetField("selectedSlot").GetValue(bag),"Reacquisition should select the same weapon without duplication");
   } finally {
    if(oldI==null)interact.RemoveBindingOverride(0);else interact.ApplyBindingOverride(0,oldI);if(oldD==null)drop.RemoveBindingOverride(0);else drop.ApplyBindingOverride(0,oldD);
    if(bossObject)UnityEngine.Object.DestroyImmediate(bossObject);if(dropped)UnityEngine.Object.DestroyImmediate(dropped);if(bag!=null&& (bool)bt.GetMethod("Has").Invoke(bag,new object[]{id,1}))bt.GetMethod("Consume").Invoke(bag,new object[]{id,1});
    input.SetContext(PlayerInputContext.Gameplay);PlayerCursor.EndSimulation();InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;
   }
  }
 }
}