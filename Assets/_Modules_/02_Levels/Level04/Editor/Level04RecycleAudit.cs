using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class Level04RecycleAudit {
 public static void Run(){
  var s=EditorSceneManager.OpenScene("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",OpenSceneMode.Single);
  foreach(var root in s.GetRootGameObjects())foreach(var i in root.GetComponentsInChildren<RecycleItem>(true)){var cs=i.GetComponentsInChildren<Collider>(true);Debug.Log($"L04_RECYCLE type={i.itemType} object={i.name} pos={i.transform.position.ToString("F2")} active={i.gameObject.activeInHierarchy} colliders={cs.Length} details="+string.Join("|",cs.Select(c=>c.name+":"+c.GetType().Name+":enabled="+c.enabled+":trigger="+c.isTrigger)));}
  foreach(var root in s.GetRootGameObjects())foreach(var h in root.GetComponentsInChildren<RecycleHouse>(true)){var cs=h.GetComponentsInChildren<Collider>(true);Debug.Log($"L04_HOUSE name={h.name} pos={h.transform.position.ToString("F2")} text={(h.questionText?h.questionText.name:"null")} colliders="+string.Join("|",cs.Select(c=>c.name+":"+c.GetType().Name+":enabled="+c.enabled+":trigger="+c.isTrigger)));}
  foreach(var root in s.GetRootGameObjects())foreach(var b in root.GetComponentsInChildren<BossHealth>(true))Debug.Log($"L04_BOSS max={b.maxHealth} parts={(b.recycleParts?b.recycleParts.name:"null")} recycle={(b.playerRecycleController?b.playerRecycleController.name:"null")}");
 }
}