using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace M7SharedPlayerTests {
 public sealed class Level04QuantumDropTests {
  [UnityTest] public IEnumerator QuantumDropAndRepickRetainOneWeaponAndAmmo(){
   PlayerCursor.BeginSimulation(CursorLockMode.Locked);const string id="eco_quantum_projector";
   object bag=null;var t=Type.GetType("EcoAdventureRuntime, Assembly-CSharp",true);bool had=false;string[] old=null;int oldSlot=0,oldMag=0,oldReserve=0;GameObject dropped=null;
   try {SceneManager.LoadScene("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",LoadSceneMode.Single);yield return null;yield return null;
    bag=t.GetProperty("Instance").GetValue(null);var has=t.GetMethod("Has");had=(bool)has.Invoke(bag,new object[]{id,1});var slots=(string[])t.GetField("quickSlots").GetValue(bag);old=(string[])slots.Clone();oldSlot=(int)t.GetField("selectedSlot").GetValue(bag);oldMag=(int)t.GetField("ammoInMagazine").GetValue(bag);oldReserve=(int)t.GetField("reserveAmmo").GetValue(bag);
    if(!had)t.GetMethod("AddItem").Invoke(bag,new object[]{id,"Q",1});t.GetMethod("AssignQuickSlot").Invoke(bag,new object[]{0,id});t.GetField("selectedSlot").SetValue(bag,0);t.GetField("ammoInMagazine").SetValue(bag,13);t.GetField("reserveAmmo").SetValue(bag,37);Physics.SyncTransforms();
    var drop=t.GetMethod("TryDropCurrent",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsTrue((bool)drop.Invoke(bag,null),"Drop must find safe space");Assert.IsFalse((bool)has.Invoke(bag,new object[]{id,1}));dropped=GameObject.Find("DroppedQuantumProjector");Assert.NotNull(dropped);
    t.GetMethod("AddItem").Invoke(bag,new object[]{id,"Q",1});Assert.IsFalse((bool)has.Invoke(bag,new object[]{id,1}),"Cannot duplicate a dropped gun");var pickup=(IPlayerInteractable)dropped.GetComponent(Type.GetType("EcoQuantumWorldPickup, Assembly-CSharp",true));pickup.Interact(Camera.main.GetComponentInParent<CharacterController>().gameObject);Assert.IsTrue((bool)has.Invoke(bag,new object[]{id,1}));Assert.IsFalse((bool)has.Invoke(bag,new object[]{id,2}));Assert.AreEqual(13,t.GetField("ammoInMagazine").GetValue(bag));Assert.AreEqual(37,t.GetField("reserveAmmo").GetValue(bag));
   } finally {if(dropped)UnityEngine.Object.DestroyImmediate(dropped);if(bag!=null&&old!=null){if(!had&&(bool)t.GetMethod("Has").Invoke(bag,new object[]{id,1}))t.GetMethod("Consume").Invoke(bag,new object[]{id,1});Array.Copy(old,(string[])t.GetField("quickSlots").GetValue(bag),old.Length);t.GetField("selectedSlot").SetValue(bag,oldSlot);t.GetField("ammoInMagazine").SetValue(bag,oldMag);t.GetField("reserveAmmo").SetValue(bag,oldReserve);}PlayerCursor.EndSimulation();}
  }
 }
}