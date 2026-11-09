using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace M7SharedPlayerTests {
 public sealed class Level04DualWeaponTests {
  const string ScenePath="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity";
  const string Quantum="eco_quantum_projector",Plasma="level04_plasma_gun";
  static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(obj,args);
  static object Get(object obj,string name)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(obj);
  static void Set(object obj,string name,object val)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(obj,val);
  [UnityTest] public IEnumerator BothWeaponsShareInventoryAndEachRayDamagesBossOnce() {
   SceneManager.LoadScene(ScenePath,LoadSceneMode.Single);yield return null;yield return null;
   var type=Type.GetType("EcoAdventureRuntime, Assembly-CSharp",true);var bag=type.GetProperty("Instance").GetValue(null);Assert.NotNull(bag);
   var slots=(string[])Get(bag,"quickSlots");var saved=(string[])slots.Clone();int oldSlot=(int)Get(bag,"selectedSlot"),ammo=(int)Get(bag,"ammoInMagazine"),reserve=(int)Get(bag,"reserveAmmo");
   bool hadQ=(bool)Call(bag,"Has",Quantum,1),hadP=(bool)Call(bag,"Has",Plasma,1);
   var target=GameObject.CreatePrimitive(PrimitiveType.Sphere);var bt=Type.GetType("BossHealth, Assembly-CSharp",true);var boss=target.AddComponent(bt);
   try {
    if(!hadQ)Call(bag,"AddItem",Quantum,"Q",1);if(!hadP)Call(bag,"AddItem",Plasma,"P",1);
    Assert.IsTrue((bool)Call(bag,"Has",Quantum,1));Assert.IsTrue((bool)Call(bag,"Has",Plasma,1));
    Assert.IsTrue((bool)Call(bag,"AssignQuickSlot",0,Quantum));Assert.IsTrue((bool)Call(bag,"AssignQuickSlot",1,Plasma));
    var camera=Camera.main;Assert.NotNull(camera);target.transform.localScale=Vector3.one*.22f;target.transform.position=camera.transform.position+camera.transform.forward*.65f;Physics.SyncTransforms();yield return null;
    target.transform.position=camera.transform.position+camera.transform.forward*.65f;Physics.SyncTransforms();
    int hp=(int)Get(boss,"currentHealth");Assert.AreEqual(5,hp);
    Set(bag,"selectedSlot",0);Set(bag,"ammoInMagazine",10);Set(bag,"nextShotTime",0f);Call(bag,"Shoot");Assert.AreEqual(hp-1,Get(boss,"currentHealth"));Assert.AreEqual(9,Get(bag,"ammoInMagazine"));
    Set(bag,"selectedSlot",1);Set(bag,"nextPlasmaShotTime",0f);Call(bag,"ShootPlasma");Assert.AreEqual(hp-2,Get(boss,"currentHealth"));Call(bag,"ShootPlasma");Assert.AreEqual(hp-2,Get(boss,"currentHealth"),"Cooldown prevents duplicate damage");
    Assert.AreEqual(9,Get(bag,"ammoInMagazine"));Assert.AreEqual(reserve,Get(bag,"reserveAmmo"));Assert.IsTrue((bool)Call(bag,"Has",Quantum,1));Assert.IsTrue((bool)Call(bag,"Has",Plasma,1));
   } finally {if(!hadQ)Call(bag,"Consume",Quantum,1);if(!hadP)Call(bag,"Consume",Plasma,1);Array.Copy(saved,(string[])Get(bag,"quickSlots"),saved.Length);Set(bag,"selectedSlot",oldSlot);Set(bag,"ammoInMagazine",ammo);Set(bag,"reserveAmmo",reserve);UnityEngine.Object.Destroy(target);}
  }
 }
}