using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace M7SharedPlayerTests {
 public sealed class Level04WeaponPersistenceTests {
  [UnityTest] public IEnumerator BothWeaponSlotsAndQuantumAmmoSurviveSceneTransitions(){
   const string q="eco_quantum_projector",p="level04_plasma_gun",level="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity";
   SceneManager.LoadScene(level,LoadSceneMode.Single);yield return null;yield return null;var t=Type.GetType("EcoAdventureRuntime, Assembly-CSharp",true);var bag=t.GetProperty("Instance").GetValue(null);var has=t.GetMethod("Has");bool hadQ=(bool)has.Invoke(bag,new object[]{q,1}),hadP=(bool)has.Invoke(bag,new object[]{p,1});var slots=(string[])t.GetField("quickSlots").GetValue(bag);var old=(string[])slots.Clone();var select=t.GetField("selectedSlot");int oldSelect=(int)select.GetValue(bag);var mag=t.GetField("ammoInMagazine");var reserve=t.GetField("reserveAmmo");int oldMag=(int)mag.GetValue(bag),oldReserve=(int)reserve.GetValue(bag);
   try{if(!hadQ)t.GetMethod("AddItem").Invoke(bag,new object[]{q,"Q",1});if(!hadP)t.GetMethod("AddItem").Invoke(bag,new object[]{p,"P",1});t.GetMethod("AssignQuickSlot").Invoke(bag,new object[]{0,q});t.GetMethod("AssignQuickSlot").Invoke(bag,new object[]{1,p});select.SetValue(bag,1);mag.SetValue(bag,13);reserve.SetValue(bag,37);
    SceneManager.LoadScene("Assets/_Modules_/02_Levels/Level01/Imported/Scenes/WTF.unity",LoadSceneMode.Single);yield return null;SceneManager.LoadScene(level,LoadSceneMode.Single);yield return null;yield return null;
    Assert.AreSame(bag,t.GetProperty("Instance").GetValue(null));Assert.IsTrue((bool)has.Invoke(bag,new object[]{q,1}));Assert.IsTrue((bool)has.Invoke(bag,new object[]{p,1}));var now=(string[])t.GetField("quickSlots").GetValue(bag);Assert.AreEqual(q,now[0]);Assert.AreEqual(p,now[1]);Assert.AreEqual(1,select.GetValue(bag));Assert.AreEqual(13,mag.GetValue(bag));Assert.AreEqual(37,reserve.GetValue(bag));
   } finally {if(!hadQ)t.GetMethod("Consume").Invoke(bag,new object[]{q,1});if(!hadP)t.GetMethod("Consume").Invoke(bag,new object[]{p,1});Array.Copy(old,(string[])t.GetField("quickSlots").GetValue(bag),old.Length);select.SetValue(bag,oldSelect);mag.SetValue(bag,oldMag);reserve.SetValue(bag,oldReserve);}
  }
 }
}