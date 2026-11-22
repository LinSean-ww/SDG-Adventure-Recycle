using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SDGAdventure.SharedPlayer;
using D4=SDGAdventure.Level04.Imported;
public static class Level04PlayerSetup {
 const string S="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity";
 const string P="Assets/_Modules_/01_Shared/CompletePlayer/Prefabs/CompletePlayer.prefab";
 public static void Inspect() {
  var scene=EditorSceneManager.OpenScene(S,OpenSceneMode.Single); Physics.SyncTransforms();
  var old=Object.FindObjectsOfType<D4.PlayerMove>(true).Single();
  var boss=Object.FindObjectsOfType<ToiletMechBossAI>(true).Single();
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(P);var motor=prefab.GetComponent<CharacterController>();
  Debug.Log($"L04_PLAYER_PROBE old={old.name} pos={old.transform.position.ToString("F3")} scale={old.transform.lossyScale.ToString("F3")} active={old.gameObject.activeInHierarchy} boss={boss.transform.position.ToString("F3")} playerTarget={(boss.player?boss.player.name:"null")} prefabControllerHeight={motor.height} center={motor.center} radius={motor.radius}");
  foreach(var c in Object.FindObjectsOfType<Camera>(true)) Debug.Log($"L04_CAM {c.name} parent={(c.transform.parent?c.transform.parent.name:"null")} active={c.gameObject.activeInHierarchy} enabled={c.enabled} position={c.transform.position.ToString("F3")}");
  foreach(var w in Object.FindObjectsOfType<WeaponPickup>(true)) Debug.Log($"L04_PICKUP {w.name} pos={w.transform.position.ToString("F3")} root={(w.playerRoot?w.playerRoot.name:"null")} gun={(w.weaponInHand?w.weaponInHand.name:"null")}");
  var origin=old.transform.position;for(int i=0;i<6;i++){var p=origin+new Vector3(i*2,15,0);var hits=Physics.RaycastAll(p,Vector3.down,50,~0,QueryTriggerInteraction.Ignore).OrderBy(x=>x.distance).Take(5);foreach(var h in hits)Debug.Log($"L04_GROUND offset={i*2} name={h.collider.name} pos={h.point.ToString("F3")} normal={h.normal.ToString("F2")}");}
 }
 static string PathOf(Transform t){string p=t.name;while(t.parent){t=t.parent;p=t.name+"/"+p;}return p;}
 public static void Setup() {
  var scene=EditorSceneManager.OpenScene(S,OpenSceneMode.Single);
  if(scene.GetRootGameObjects().Any(x=>x.name=="Level04 CompletePlayer")) throw new System.Exception("Level04 player already exists; refusing overwrite");
  var old=Object.FindObjectsOfType<D4.PlayerMove>(true).Single();
  var boss=Object.FindObjectsOfType<ToiletMechBossAI>(true).Single();
  var pickup=Object.FindObjectsOfType<WeaponPickup>(true).Single();
  var recycle=old.GetComponent<PlayerRecycleController>();
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(P);
  if(!recycle||!prefab||!boss.player||!pickup.weaponInHand)throw new System.Exception("Missing original references");
  Vector3 previous=old.transform.position; Quaternion facing=old.transform.rotation;
  old.gameObject.SetActive(false); Physics.SyncTransforms();
  if(!Physics.Raycast(previous+Vector3.up*2,Vector3.down,out RaycastHit ground,10,~0,QueryTriggerInteraction.Ignore)||ground.normal.y<0.8f)throw new System.Exception("No safe ground below original spawn");
  Vector3 spawn=ground.point+Vector3.up*0.05f;
  var collisions=Physics.OverlapCapsule(spawn+Vector3.up*.33f,spawn+Vector3.up*1.48f,.27f,~0,QueryTriggerInteraction.Ignore);
  if(collisions.Length>0)throw new System.Exception("Spawn obstructed: "+string.Join(", ",collisions.Select(c=>c.name)));
  var player=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);player.name="Level04 CompletePlayer";player.transform.SetPositionAndRotation(spawn,facing);
  var camera=player.GetComponentInChildren<Camera>(true); if(!camera||!player.GetComponent<SharedFirstPersonController>()||!player.GetComponent<PlayerInteractor>())throw new System.Exception("CompletePlayer missing required components");
  boss.player=player.transform; pickup.playerRoot=player.transform; pickup.enabled=false;
  recycle.playerCamera=camera;
  var carry=new GameObject("Level04 Carry Anchor");carry.transform.SetParent(camera.transform,false);carry.transform.localPosition=new Vector3(.25f,-.25f,1f);recycle.carryPoint=carry.transform;
  foreach(var c in Object.FindObjectsOfType<Camera>())if(c!=camera&&c.enabled&&c.gameObject.activeInHierarchy)throw new System.Exception("Multiple active cameras: "+c.name);
  EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new System.Exception("Cannot save Level04 scene");
  var scenes=EditorBuildSettings.scenes.ToList(); if(!scenes.Any(x=>x.path==S)){scenes.Add(new EditorBuildSettingsScene(S,true));EditorBuildSettings.scenes=scenes.ToArray();}
  Debug.Log("L04_PLAYER_SETUP_OK spawn="+spawn.ToString("F3")+" ground="+ground.collider.name);
 }

 public static void SetupWeapon() {
  var scene=EditorSceneManager.OpenScene(S,OpenSceneMode.Single);
  var pickup=Object.FindObjectsOfType<WeaponPickup>(true).Single();
  if(pickup.GetComponent<SDGAdventure.Level04.Level04PlasmaPickup>())throw new System.Exception("Pickup already installed");
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Modules_/02_Levels/Level04/Imported/Plasma Guns Lite/Prefabs/Plasma_SMG_Dirty_Prefab.prefab");
  var fx=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Modules_/02_Levels/Level04/Imported/特效/FreeQuickEffectsVol1/Prefabs/vfx_Projectile_01.prefab");
  if(!prefab||!fx)throw new System.Exception("Missing source plasma prefab or projectile");
  pickup.enabled=false;
  var cp=pickup.gameObject.AddComponent<SDGAdventure.Level04.Level04PlasmaPickup>();cp.modelPrefab=prefab;cp.projectileEffect=fx;
  var box=pickup.gameObject.AddComponent<BoxCollider>();box.size=new Vector3(.8f,.8f,.8f);box.isTrigger=false;
  EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new System.Exception("Could not save weapon setup");
  Debug.Log("L04_WEAPON_SETUP_OK pickup="+pickup.name+" position="+pickup.transform.position.ToString("F3"));
 }

}