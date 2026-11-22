using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace M7SharedPlayerTests {
 public sealed class Level04WeaponPersistenceTests {
  [UnityTest] public IEnumerator Story3WeaponPersistsAndLevel04GunDoesNotCrossScenes(){
   const string q="eco_quantum_projector",p="level04_plasma_gun";
   const string level4="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity";
   const string level1="Assets/_Modules_/02_Levels/Level01/Imported/Scenes/WTF.unity";
   SceneManager.LoadScene(level4,LoadSceneMode.Single);yield return null;yield return null;
   var t=Type.GetType("EcoAdventureRuntime, Assembly-CSharp",true);
   var bag=t.GetProperty("Instance").GetValue(null);
   var has=t.GetMethod("Has");var add=t.GetMethod("AddItem");var consume=t.GetMethod("Consume");
   var slots=(string[])t.GetField("quickSlots").GetValue(bag);
   var old=(string[])slots.Clone();int oldSelected=(int)t.GetField("selectedSlot").GetValue(bag);
   bool oldQ=(bool)has.Invoke(bag,new object[]{q,1}),oldP=(bool)has.Invoke(bag,new object[]{p,1});
   var mag=t.GetField("ammoInMagazine");var reserve=t.GetField("reserveAmmo");
   int oldMag=(int)mag.GetValue(bag),oldReserve=(int)reserve.GetValue(bag);
   try{
    if(!oldQ)add.Invoke(bag,new object[]{q,"Story3 reward",1});
    if(!oldP)add.Invoke(bag,new object[]{p,"Level04 local",1});
    Assert.IsTrue((bool)bag.GetType().GetMethod("AssignQuickSlot").Invoke(bag,new object[]{0,q}));
    Assert.IsTrue((bool)bag.GetType().GetMethod("AssignQuickSlot").Invoke(bag,new object[]{1,p}));
    t.GetField("selectedSlot").SetValue(bag,1);mag.SetValue(bag,13);reserve.SetValue(bag,37);
    SceneManager.LoadScene(level1,LoadSceneMode.Single);yield return null;yield return null;
    Assert.IsTrue((bool)has.Invoke(bag,new object[]{q,1}),"Story3 reward is global");
    Assert.IsFalse((bool)has.Invoke(bag,new object[]{p,1}),"Level04 gun must be unavailable outside Level04");
    var current=(string[])t.GetField("quickSlots").GetValue(bag);
    Assert.IsFalse(Array.Exists(current,id=>id==p),"Level04 gun quick slot must be cleared");
    Assert.AreEqual(0,t.GetField("selectedSlot").GetValue(bag),"Fallback selects Story3 gun in slot 1");
    Assert.AreEqual(13,mag.GetValue(bag));Assert.AreEqual(37,reserve.GetValue(bag));
    add.Invoke(bag,new object[]{p,"Forbidden outside L4",1});
    Assert.IsFalse((bool)has.Invoke(bag,new object[]{p,1}),"Cannot re-add Level04 gun outside its scene");
    SceneManager.LoadScene(level4,LoadSceneMode.Single);yield return null;yield return null;
    Assert.IsTrue((bool)has.Invoke(bag,new object[]{q,1}));
    Assert.IsFalse((bool)has.Invoke(bag,new object[]{p,1}),"Returning to Level04 starts with its pickup unowned");
    add.Invoke(bag,new object[]{p,"Level04 local",1});
    Assert.IsTrue((bool)has.Invoke(bag,new object[]{p,1}),"Level04 gun can be acquired in Level04");
   } finally {
    if(!oldQ&&(bool)has.Invoke(bag,new object[]{q,1}))consume.Invoke(bag,new object[]{q,1});
    if((bool)has.Invoke(bag,new object[]{p,1}))consume.Invoke(bag,new object[]{p,1});
    for(int i=0;i<slots.Length;i++) slots[i]=old[i]==p?null:old[i];
    t.GetField("selectedSlot").SetValue(bag,oldSelected>=0&&oldSelected<slots.Length&&slots[oldSelected]!=null?oldSelected:0);
    mag.SetValue(bag,oldMag);reserve.SetValue(bag,oldReserve);
   }
  }
 }
}
