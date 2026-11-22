using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace M7SharedPlayerTests {
 public sealed class Level04SelectionAndProgressionTests {
  static Type T(string n)=>Type.GetType(n+", Assembly-CSharp",true);
  static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n).Invoke(o,a);
  static Component Find(string n)=>(Component)UnityEngine.Object.FindObjectOfType(T(n));
  [UnityTest] public IEnumerator DevelopmentSelectionLaunchesLevel04(){
   yield return SceneManager.LoadSceneAsync("Assets/_Modules_/05_Testing/Scenes/M6_DevelopmentSelection.unity",LoadSceneMode.Single);yield return null;
   Assert.NotNull(GameObject.Find("LaunchLevel01"));Assert.NotNull(GameObject.Find("Launch"));var b=GameObject.Find("LaunchLevel04");Assert.NotNull(b);
   var type=Type.GetType("UnityEngine.UI.Button, UnityEngine.UI",true);var btn=b.GetComponent(type);((UnityEvent)type.GetProperty("onClick").GetValue(btn)).Invoke();yield return null;
   Assert.AreEqual("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",SceneManager.GetActiveScene().path);
   var coord=T("SDGAdventure.ModuleSystem.ModuleCoordinator").GetProperty("Instance").GetValue(null);Assert.AreEqual("level04",coord.GetType().GetProperty("CurrentModuleId").GetValue(coord));
  }
  [UnityTest] public IEnumerator C3CompletionRemainsVisibleBeforeAdvancingToLevel04(){
   yield return SceneManager.LoadSceneAsync("Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity",LoadSceneMode.Single);yield return null;
   var progress=Find("C3TreasurePuzzle.C3TreasureProgress");foreach(string id in new[]{"fuse","control_chip","filter_module"})Assert.IsTrue((bool)Call(progress,"TryCollect",id,id));
   var puzzle=Find("C3TreasurePuzzle.C3PuzzleState");var step=T("C3TreasurePuzzle.C3ProcessingStep");var answer=Array.CreateInstance(step,3);answer.SetValue(Enum.Parse(step,"Sorting"),0);answer.SetValue(Enum.Parse(step,"Recycling"),1);answer.SetValue(Enum.Parse(step,"PollutionTreatment"),2);Assert.IsTrue((bool)Call(puzzle,"SubmitAnswer",answer));
   var device=Find("C3TreasurePuzzle.C3DeviceState");Assert.IsTrue((bool)Call(device,"TryActivate"));Call(device,"NotifyStartupFinished");var exit=Find("C3TreasurePuzzle.C3ExitTrigger");Assert.IsTrue((bool)Call(exit,"TryEnter",GameObject.Find("M2_Player").GetComponent<CharacterController>()));
   var completion=Find("C3TreasurePuzzle.C3CompletionScreen");Assert.NotNull(completion);Assert.IsTrue((bool)completion.GetType().GetProperty("Visible").GetValue(completion));Assert.AreEqual("C_Main",SceneManager.GetActiveScene().name);Call(completion,"ContinueToNext");yield return null;
   Assert.AreEqual("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",SceneManager.GetActiveScene().path);
  }
 }
}