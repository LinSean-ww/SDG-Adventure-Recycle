using System;
using System.Linq;
using SDGAdventure.SharedPlayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
public static class Level04DoorSpawnSetup {
 public static void Apply(){
  var scene=EditorSceneManager.OpenScene("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",OpenSceneMode.Single);
  var player=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SharedFirstPersonController>(true)).Single(x=>x.gameObject.activeInHierarchy);
  var controller=player.GetComponent<CharacterController>(); if(!controller)throw new Exception("Missing player CharacterController");
  var boss=UnityEngine.Object.FindFirstObjectByType<ToiletMechBossAI>(); if(!boss)throw new Exception("Missing boss");
  if(GameObject.Find("Level04 D North Solid Door"))throw new Exception("Door already exists; refusing to overwrite");
  Physics.SyncTransforms(); var groundHits=Physics.RaycastAll(new Vector3(-12.5f,3f,-1.8f),Vector3.down,6f,~0,QueryTriggerInteraction.Ignore).Where(h=>!h.collider.transform.IsChildOf(player.transform)).OrderBy(h=>h.distance).ToArray();
  foreach(var h in groundHits)Debug.Log("L04_SPAWN_GROUND "+h.collider.name+" point="+h.point+" normal="+h.normal);
  if(groundHits.Length==0||groundHits[0].normal.y<.8f)throw new Exception("No verified level floor at entrance");
  float ground=groundHits[0].point.y;var door=GameObject.CreatePrimitive(PrimitiveType.Cube);door.name="Level04 D North Solid Door";door.transform.position=new Vector3(-12.5f,ground+1.6f,0);door.transform.localScale=new Vector3(4.5f,3.2f,.24f);
  var factory=GameObject.Find("ONLYfactory/factory");if(!factory)throw new Exception("Factory mesh unavailable");var wall=factory.GetComponent<Renderer>().sharedMaterials.FirstOrDefault(m=>m&&m.name=="siteD_wall_2");if(!wall)throw new Exception("Existing D wall material unavailable");door.GetComponent<Renderer>().sharedMaterial=wall;door.isStatic=true;
  var scale=player.transform.lossyScale;if(Mathf.Abs(scale.x-1)>.001f||Mathf.Abs(scale.y-1)>.001f||Mathf.Abs(scale.z-1)>.001f)throw new Exception("Unexpected player scaling");
  player.transform.position=new Vector3(-12.5f,ground-(controller.center.y-controller.height*.5f)+.05f,-1.8f);Vector3 facing=Vector3.ProjectOnPlane(boss.transform.position-player.transform.position,Vector3.up);if(facing.z>=0)throw new Exception("Boss is not toward factory interior");player.transform.rotation=Quaternion.LookRotation(facing.normalized,Vector3.up);Physics.SyncTransforms();
  float radius=Mathf.Max(.01f,controller.radius-controller.skinWidth);Vector3 center=player.transform.TransformPoint(controller.center);float half=Mathf.Max(0,controller.height*.5f-radius);var touching=Physics.OverlapCapsule(center+Vector3.up*half,center-Vector3.up*half,radius,~0,QueryTriggerInteraction.Ignore).Where(c=>!c.transform.IsChildOf(player.transform)).ToArray();foreach(var c in touching)Debug.Log("L04_SPAWN_OVERLAP "+c.name);if(touching.Length>0)throw new Exception("Spawn overlaps collider; refusing to save");
  if(!Physics.Raycast(new Vector3(-12.5f,ground+1.5f,-1),Vector3.forward,out var doorHit,2f)||doorHit.collider.gameObject!=door)throw new Exception("Door collision does not seal entrance");
  if(NavMesh.SamplePosition(player.transform.position,out var p,2f,NavMesh.AllAreas)&&NavMesh.SamplePosition(boss.transform.position,out var b,2f,NavMesh.AllAreas)){var route=new NavMeshPath();bool ok=NavMesh.CalculatePath(p.position,b.position,NavMesh.AllAreas,route);Debug.Log("L04_SPAWN_BOSS_ROUTE success="+ok+" status="+route.status);if(!ok||route.status!=NavMeshPathStatus.PathComplete)throw new Exception("Spawn to boss route could not be verified");}else Debug.LogWarning("L04_SPAWN_BOSS_ROUTE NavMesh sample unavailable in editor; manual verification required");
  EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save Level04 scene");Debug.Log("L04_DOOR_SPAWN_OK ground="+ground+" spawn="+player.transform.position.ToString("F3")+" yaw="+player.transform.eulerAngles.y+" controller="+controller.height+"/"+controller.radius+" door="+door.transform.position+" size="+door.transform.localScale+" collider="+door.GetComponent<Collider>().isTrigger);
 }
}
