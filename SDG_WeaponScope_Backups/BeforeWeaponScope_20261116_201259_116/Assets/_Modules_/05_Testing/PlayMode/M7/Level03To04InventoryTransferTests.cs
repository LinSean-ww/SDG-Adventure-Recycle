using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace M7SharedPlayerTests {
 public sealed class Level03To04InventoryTransferTests {
  const string C3="Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity",L4="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",Q="eco_quantum_projector",P="level04_plasma_gun";
  static Type T(string n)=>Type.GetType(n+", Assembly-CSharp",true);
  static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n).Invoke(o,a);
  static Component Find(string n)=>(Component)UnityEngine.Object.FindFirstObjectByType(T(n));
  [UnityTest] public IEnumerator CompletionTransitionsFromC3ToLevel04WithBothWeaponsAmmoSlotsAndLoot(){
   yield return SceneManager.LoadSceneAsync(C3,LoadSceneMode.Single);yield return null;yield return null;
   var rt=T("EcoAdventureRuntime");var bag=rt.GetProperty("Instance").GetValue(null);Assert.NotNull(bag);var has=rt.GetMethod("Has");bool oldQ=(bool)has.Invoke(bag,new object[]{Q,1}),oldP=(bool)has.Invoke(bag,new object[]{P,1});var slots=(string[])rt.GetField("quickSlots").GetValue(bag);var before=(string[])slots.Clone();int selected=(int)rt.GetField("selectedSlot").GetValue(bag),mag=(int)rt.GetField("ammoInMagazine").GetValue(bag),reserve=(int)rt.GetField("reserveAmmo").GetValue(bag);string loot="l04_transition_"+Guid.NewGuid().ToString("N");
   try{
    if(!oldQ)Call(bag,"AddItem",Q,"量子槍",1);if(!oldP)Call(bag,"AddItem",P,"電漿槍",1);Call(bag,"AddItem",loot,"測試物品",1);Call(bag,"AssignQuickSlot",0,Q);Call(bag,"AssignQuickSlot",1,P);Call(bag,"AssignQuickSlot",2,loot);rt.GetField("selectedSlot").SetValue(bag,1);rt.GetField("ammoInMagazine").SetValue(bag,13);rt.GetField("reserveAmmo").SetValue(bag,37);
    var progress=Find("C3TreasurePuzzle.C3TreasureProgress");foreach(string id in new[]{"fuse","control_chip","filter_module"})Assert.IsTrue((bool)Call(progress,"TryCollect",id,id));var puzzle=Find("C3TreasurePuzzle.C3PuzzleState");var step=T("C3TreasurePuzzle.C3ProcessingStep");var answer=Array.CreateInstance(step,3);answer.SetValue(Enum.Parse(step,"Sorting"),0);answer.SetValue(Enum.Parse(step,"Recycling"),1);answer.SetValue(Enum.Parse(step,"PollutionTreatment"),2);Assert.IsTrue((bool)Call(puzzle,"SubmitAnswer",answer));var device=Find("C3TreasurePuzzle.C3DeviceState");Assert.IsTrue((bool)Call(device,"TryActivate"));Call(device,"NotifyStartupFinished");var exit=Find("C3TreasurePuzzle.C3ExitTrigger");Assert.IsTrue((bool)Call(exit,"TryEnter",GameObject.Find("M2_Player").GetComponent<CharacterController>()));var finish=Find("C3TreasurePuzzle.C3CompletionScreen");Assert.IsTrue((bool)finish.GetType().GetProperty("Visible").GetValue(finish));Assert.AreEqual("C_Main",SceneManager.GetActiveScene().name);Call(finish,"ContinueToNext");yield return null;yield return null;
    Assert.AreEqual(L4,SceneManager.GetActiveScene().path);Assert.AreSame(bag,rt.GetProperty("Instance").GetValue(null));Assert.IsTrue((bool)has.Invoke(bag,new object[]{Q,1}));Assert.IsTrue((bool)has.Invoke(bag,new object[]{P,1}));Assert.IsTrue((bool)has.Invoke(bag,new object[]{loot,1}));Assert.IsFalse((bool)has.Invoke(bag,new object[]{Q,2}));Assert.IsFalse((bool)has.Invoke(bag,new object[]{P,2}));var after=(string[])rt.GetField("quickSlots").GetValue(bag);Assert.AreEqual(Q,after[0]);Assert.AreEqual(P,after[1]);Assert.AreEqual(loot,after[2]);Assert.AreEqual(1,rt.GetField("selectedSlot").GetValue(bag));Assert.AreEqual(13,rt.GetField("ammoInMagazine").GetValue(bag));Assert.AreEqual(37,rt.GetField("reserveAmmo").GetValue(bag));
   }finally{Call(bag,"Consume",loot,1);if(!oldQ)Call(bag,"Consume",Q,1);if(!oldP)Call(bag,"Consume",P,1);Array.Copy(before,(string[])rt.GetField("quickSlots").GetValue(bag),before.Length);rt.GetField("selectedSlot").SetValue(bag,selected);rt.GetField("ammoInMagazine").SetValue(bag,mag);rt.GetField("reserveAmmo").SetValue(bag,reserve);Time.timeScale=1;}
  }
 }
}
