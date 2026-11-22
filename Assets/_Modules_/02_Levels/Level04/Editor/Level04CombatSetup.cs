using System;
using System.Linq;
using SDGAdventure.Level04;
using SDGAdventure.SharedPlayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class Level04CombatSetup {
 public static void Apply(){
  const string path="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity";var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
  var player=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<SharedFirstPersonController>(true)).Single(x=>x.gameObject.activeInHierarchy);
  var boss=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ToiletMechBossAI>(true)).Single();
  if(!boss.animator||!boss.agent||boss.player!=player.transform)throw new Exception("Boss binding is not valid");
  if(boss.animator.transform.parent && boss.animator.transform.parent!=boss.transform)throw new Exception("Boss visual has an unexpected owner; refusing reparent");
  Vector3 before=boss.animator.transform.position;Quaternion rotation=boss.animator.transform.rotation;Vector3 scale=boss.animator.transform.lossyScale;
  if(!boss.animator.transform.parent)boss.animator.transform.SetParent(boss.transform,true);
  if(Vector3.Distance(before,boss.animator.transform.position)>.001f||Quaternion.Angle(rotation,boss.animator.transform.rotation)>.1f||Vector3.Distance(scale,boss.animator.transform.lossyScale)>.001f)throw new Exception("Boss visual world transform changed");
  boss.animator.applyRootMotion=false;
  if(!player.GetComponent<Level04PlayerHealth>())player.gameObject.AddComponent<Level04PlayerHealth>();
  EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save Level04 scene");
  Debug.Log("L04_COMBAT_SETUP_OK visualParent="+boss.animator.transform.parent.name+" rootMotion="+boss.animator.applyRootMotion+" playerHealth="+player.GetComponent<Level04PlayerHealth>().name);
 }
}