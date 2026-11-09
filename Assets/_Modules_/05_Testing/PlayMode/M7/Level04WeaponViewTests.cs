using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace M7SharedPlayerTests {
 public sealed class Level04WeaponViewTests {
  [UnityTest] public IEnumerator SelectedWeaponsHaveOneViewInBothPerspectives(){
   const string level="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",q="eco_quantum_projector",p="level04_plasma_gun";
   SceneManager.LoadScene(level,LoadSceneMode.Single);yield return null;yield return null;var t=Type.GetType("EcoAdventureRuntime, Assembly-CSharp",true);var bag=t.GetProperty("Instance").GetValue(null);var has=t.GetMethod("Has");bool hadQ=(bool)has.Invoke(bag,new object[]{q,1}),hadP=(bool)has.Invoke(bag,new object[]{p,1});var slots=(string[])t.GetField("quickSlots").GetValue(bag);var old=(string[])slots.Clone();var selection=t.GetField("selectedSlot");int oldSelect=(int)selection.GetValue(bag);var original=PlayerViewPreference.Mode;
   try {if(!hadQ)t.GetMethod("AddItem").Invoke(bag,new object[]{q,"Q",1});if(!hadP)t.GetMethod("AddItem").Invoke(bag,new object[]{p,"P",1});t.GetMethod("AssignQuickSlot").Invoke(bag,new object[]{0,q});t.GetMethod("AssignQuickSlot").Invoke(bag,new object[]{1,p});
    foreach(PlayerViewMode mode in new[]{PlayerViewMode.FirstPerson,PlayerViewMode.ThirdPerson}) {PlayerViewPreference.Mode=mode;foreach(int slot in new[]{0,1}) {selection.SetValue(bag,slot);yield return null;yield return null;var view=(GameObject)t.GetField("weaponModel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(bag);Assert.NotNull(view,"Missing selected weapon view in "+mode+" slot "+slot);Assert.NotNull(view.GetComponent(Type.GetType("SDGAdventure.Level04.Level04WeaponPose, Assembly-CSharp",true)));}}
   } finally {PlayerViewPreference.Mode=original;if(!hadQ)t.GetMethod("Consume").Invoke(bag,new object[]{q,1});if(!hadP)t.GetMethod("Consume").Invoke(bag,new object[]{p,1});Array.Copy(old,(string[])t.GetField("quickSlots").GetValue(bag),old.Length);selection.SetValue(bag,oldSelect);}
  }
 }
}