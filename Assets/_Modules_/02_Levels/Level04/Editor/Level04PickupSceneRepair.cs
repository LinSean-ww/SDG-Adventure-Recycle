using System;
using System.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using SDGAdventure.Level04;
public static class Level04PickupSceneRepair {
 public static void Inspect(){
  var scene=EditorSceneManager.OpenScene("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",OpenSceneMode.Single);var p=UnityEngine.Object.FindObjectsOfType<Level04PlasmaPickup>(true).Single();Physics.SyncTransforms();Debug.Log("PICKUP_PROBE parent="+p.transform.position+" active="+p.gameObject.activeInHierarchy+" legacyEnabled="+p.GetComponent<WeaponPickup>().enabled);
  foreach(var c in p.GetComponentsInChildren<Collider>(true))Debug.Log("PICKUP_COLL "+c.name+" trigger="+c.isTrigger+" enabled="+c.enabled+" center="+c.bounds.center+" size="+c.bounds.size);
  foreach(var r in p.GetComponentsInChildren<Renderer>(true))Debug.Log("PICKUP_RENDER "+r.name+" active="+r.gameObject.activeInHierarchy+" enabled="+r.enabled+" center="+r.bounds.center+" size="+r.bounds.size);
 }
 public static void Apply(){
  var scene=EditorSceneManager.OpenScene("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",OpenSceneMode.Single);var p=UnityEngine.Object.FindObjectsOfType<Level04PlasmaPickup>(true).Single();var legacy=p.GetComponent<WeaponPickup>();if(!legacy||legacy.enabled)throw new Exception("Legacy pickup state unexpected");var model=p.transform.Find("PlasmaSMG_Model");if(!model)throw new Exception("Visual model missing");var renderers=model.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).ToArray();if(renderers.Length==0)throw new Exception("No visible model renderers");Bounds bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
  var children=p.transform.Cast<Transform>().ToArray();foreach(var child in children)child.SetParent(null,true);Vector3 shift=new Vector3(0,0,-2.5f);p.transform.position=bounds.center+shift;foreach(var child in children){child.SetParent(p.transform,true);child.position+=shift;}
  if(!model.IsChildOf(p.transform))throw new Exception("Model reparent failed");var box=p.GetComponent<BoxCollider>();box.center=Vector3.zero;box.size=Vector3.Max(bounds.size,new Vector3(.35f,.35f,.35f));box.isTrigger=false;var trigger=p.transform.Find("PickupTrigger");if(trigger){foreach(var c in trigger.GetComponents<Collider>())c.enabled=false;}Physics.SyncTransforms();
  var obstacles=Physics.OverlapBox(box.bounds.center,box.bounds.extents*.95f,Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Where(c=>!c.transform.IsChildOf(p.transform)).ToArray();if(obstacles.Length>0)throw new Exception("Weapon placement obstructed: "+string.Join(",",obstacles.Select(c=>c.name)));EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");Debug.Log("PICKUP_REPAIR_OK pos="+p.transform.position.ToString("F3")+" box="+box.bounds+" model="+model.position.ToString("F3"));
 }
}
