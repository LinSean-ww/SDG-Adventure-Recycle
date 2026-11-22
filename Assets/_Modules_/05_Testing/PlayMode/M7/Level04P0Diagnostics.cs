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
 public class Level04P0Diagnostics {
  [UnityTest] public IEnumerator InspectActualPickupAndHud(){
   object bag=null;Type bt=null;bool hadQ=false,hadP=false;PlayerCursor.BeginSimulation(CursorLockMode.Locked);try {
    SceneManager.LoadScene("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity");yield return null;yield return null;
    bt=Type.GetType("EcoAdventureRuntime, Assembly-CSharp",true);bag=bt.GetProperty("Instance").GetValue(null);hadQ=(bool)bt.GetMethod("Has").Invoke(bag,new object[]{"eco_quantum_projector",1});hadP=(bool)bt.GetMethod("Has").Invoke(bag,new object[]{"level04_plasma_gun",1});var pickup=(Component)UnityEngine.Object.FindFirstObjectByType(Type.GetType("SDGAdventure.Level04.Level04PlasmaPickup, Assembly-CSharp",true));var player=UnityEngine.Object.FindFirstObjectByType<SharedFirstPersonController>();var cam=Camera.main;
    bt.GetMethod("AddItem").Invoke(bag,new object[]{"eco_quantum_projector","Quantum",1});var slots=(string[])bt.GetField("quickSlots").GetValue(bag);int qi=Array.IndexOf(slots,"eco_quantum_projector");bt.GetField("selectedSlot").SetValue(bag,qi);((IPlayerInteractable)pickup).Interact(player.gameObject);yield return null;yield return null;
    Debug.Log("P0_PICKUP owned="+bt.GetMethod("Has").Invoke(bag,new object[]{"level04_plasma_gun",1})+" slots="+string.Join(",",slots)+" selected="+bt.GetField("selectedSlot").GetValue(bag));bt.GetField("selectedSlot").SetValue(bag,Array.IndexOf(slots,"level04_plasma_gun"));
    foreach(var mode in new[]{PlayerViewMode.FirstPerson,PlayerViewMode.ThirdPerson}){PlayerViewPreference.Mode=mode;yield return null;yield return null;var model=(GameObject)bt.GetField("weaponModel",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bag);Debug.Log("P0_MODEL mode="+mode+" obj="+(model?model.name:"NULL")+" camera="+cam.name+" mask="+cam.cullingMask+" near="+cam.nearClipPlane);if(model)foreach(var r in model.GetComponentsInChildren<Renderer>(true)){var v=cam.WorldToViewportPoint(r.bounds.center);Debug.Log("P0_RENDER "+r.name+" enabled="+r.enabled+" active="+r.gameObject.activeInHierarchy+" layer="+r.gameObject.layer+" viewport="+v.ToString("F3")+" bounds="+r.bounds.size.ToString("F3"));}}
    var fill=GameObject.Find("PlayerHealth/Track/Fill");var image=fill.GetComponent(Type.GetType("UnityEngine.UI.Image, UnityEngine.UI",true));Debug.Log("P0_HUD sprite="+image.GetType().GetProperty("sprite").GetValue(image)+" type="+image.GetType().GetProperty("type").GetValue(image)+" fill="+image.GetType().GetProperty("fillAmount").GetValue(image));
   }finally{if(bag!=null){if(!hadP&&(bool)bt.GetMethod("Has").Invoke(bag,new object[]{"level04_plasma_gun",1}))bt.GetMethod("Consume").Invoke(bag,new object[]{"level04_plasma_gun",1});if(!hadQ&&(bool)bt.GetMethod("Has").Invoke(bag,new object[]{"eco_quantum_projector",1}))bt.GetMethod("Consume").Invoke(bag,new object[]{"eco_quantum_projector",1});}PlayerViewPreference.Mode=PlayerViewMode.FirstPerson;PlayerCursor.EndSimulation();}
  }
 }
}
