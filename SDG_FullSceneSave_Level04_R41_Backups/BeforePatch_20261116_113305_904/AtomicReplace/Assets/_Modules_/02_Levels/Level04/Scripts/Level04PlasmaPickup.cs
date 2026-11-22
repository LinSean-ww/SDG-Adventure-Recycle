using System;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace SDGAdventure.Level04 {
 public sealed class Level04PlasmaPickup : MonoBehaviour, IPlayerInteractable {
  public const string WeaponId="level04_plasma_gun";
  public GameObject modelPrefab;
  public GameObject projectileEffect;
  public GameObject lightningEffect;
  private static Level04PlasmaPickup dropped;
  private static bool worldPickupCollected;
  private static GameObject sharedModel, sharedProjectile, sharedLightning;
  public static bool HasDroppedWeapon => dropped;
  private bool committed;
  public string Prompt => "拾取電漿槍";
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics(){sharedModel=null;sharedProjectile=null;sharedLightning=null;dropped=null;worldPickupCollected=false;}
  void Awake(){if(!committed && modelPrefab){sharedModel=modelPrefab;sharedProjectile=projectileEffect;sharedLightning=lightningEffect;}}
  void Start(){if(!GetComponent<Level04ProximityHint>())gameObject.AddComponent<Level04ProximityHint>();if(!committed&&worldPickupCollected&&EcoAdventureRuntime.Instance&&EcoAdventureRuntime.Instance.Has(WeaponId))gameObject.SetActive(false);}
  public bool CanInteract(GameObject actor){var input=PlayerInputService.Instance;var bag=EcoAdventureRuntime.Instance;return actor&&isActiveAndEnabled&&input&&input.Context==PlayerInputContext.Gameplay&&!input.GameplayActionsBlocked&&bag&&!bag.Has(WeaponId)&&(!dropped||dropped==this);}
  public void Interact(GameObject actor){if(!CanInteract(actor))return;var bag=EcoAdventureRuntime.Instance;bool wasDropped=dropped==this;if(wasDropped)dropped=null;bag.AddItem(WeaponId,"電漿槍");if(!bag.Has(WeaponId)){if(wasDropped)dropped=this;return;}committed=false;if(!wasDropped)worldPickupCollected=true;int slot=Array.IndexOf(bag.quickSlots,WeaponId);if(slot>=0)bag.selectedSlot=slot;else bag.Notify("電漿槍已收入背包；快捷欄已滿，請在背包手動安排");gameObject.SetActive(false);if(wasDropped)Destroy(gameObject);}
  public static GameObject CreateModel(Transform parent){
   if(!sharedModel||!parent)return null;
   var obj=Instantiate(sharedModel,parent,false);
   foreach(var c in obj.GetComponentsInChildren<Collider>(true))c.enabled=false;
   foreach(var b in obj.GetComponentsInChildren<Rigidbody>(true))b.isKinematic=true;
   // Frame the source mesh around the shared camera mount instead of placing
   // its full 1.18m source length through the camera near plane.
   bool hasBounds=false;Bounds bounds=new Bounds();
   foreach(var mesh in obj.GetComponentsInChildren<MeshFilter>(true)){
    if(!mesh.sharedMesh)continue;Bounds local=mesh.sharedMesh.bounds;
    for(int i=0;i<8;i++){Vector3 sign=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);Vector3 p=parent.InverseTransformPoint(mesh.transform.TransformPoint(local.center+Vector3.Scale(local.extents,sign)));if(!hasBounds){bounds=new Bounds(p,Vector3.zero);hasBounds=true;}else bounds.Encapsulate(p);}
   }
   if(hasBounds){float length=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));if(length>.001f){float factor=.65f/length;obj.transform.localScale*=factor;obj.transform.localPosition-=bounds.center*factor;}}
   var muzzle=new GameObject("Level04PlasmaMuzzle");muzzle.transform.SetParent(obj.transform,false);muzzle.transform.localPosition=new Vector3(-.012f,.162f,.625f);
   return obj;
  }
  public static void ShowShot(Transform held,Vector3 fallback,Vector3 target){Transform muzzle=null;if(held)foreach(var t in held.GetComponentsInChildren<Transform>(true))if(t.name=="Level04PlasmaMuzzle"){muzzle=t;break;}Vector3 from=muzzle?muzzle.position:fallback;Quaternion rotation=muzzle?muzzle.rotation:Quaternion.LookRotation((target-from).sqrMagnitude>.0001f?target-from:Vector3.forward);if(sharedLightning){var lightning=Instantiate(sharedLightning,from,rotation);Destroy(lightning,1.5f);}if(sharedProjectile){var direction=target-from;var projectile=Instantiate(sharedProjectile,from,direction.sqrMagnitude>.0001f?Quaternion.LookRotation(direction):rotation);var travel=projectile.AddComponent<Level04ProjectileVisual>();travel.Initialize(from,target);}}
  public static bool TryDrop(EcoAdventureRuntime bag,Camera camera){
   var input=PlayerInputService.Instance;
   if(!bag||!camera||!sharedModel||dropped||!bag.Has(WeaponId)||!input||input.Context!=PlayerInputContext.Gameplay||input.GameplayActionsBlocked||PlayerCursor.LockState!=CursorLockMode.Locked)return false;
   var player=camera.GetComponentInParent<CharacterController>();if(!player)return false;
   Vector3 forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.01f)forward=player.transform.forward;
   Vector3 origin=player.transform.TransformPoint(player.center),position=origin+forward*(player.radius+1f);
   foreach(var h in Physics.SphereCastAll(origin,.35f,forward,player.radius+1f,~0,QueryTriggerInteraction.Ignore))if(h.collider&&!h.collider.transform.IsChildOf(player.transform))return false;
   foreach(var c in Physics.OverlapSphere(position,.35f,~0,QueryTriggerInteraction.Ignore))if(c&&!c.transform.IsChildOf(player.transform))return false;
   var world=new GameObject("DroppedLevel04Plasma");world.SetActive(false);SceneManager.MoveGameObjectToScene(world,camera.gameObject.scene);world.transform.SetPositionAndRotation(position,Quaternion.LookRotation(forward));
   var p=world.AddComponent<Level04PlasmaPickup>();p.modelPrefab=sharedModel;p.projectileEffect=sharedProjectile;p.lightningEffect=sharedLightning;
   var model=Instantiate(p.modelPrefab,world.transform,false);foreach(var c in model.GetComponentsInChildren<Collider>(true))c.enabled=false;
   var box=world.AddComponent<BoxCollider>();box.size=new Vector3(.5f,.5f,.7f);
   if(!bag.Consume(WeaponId,1)){Destroy(world);return false;}p.committed=true;dropped=p;world.SetActive(true);return true;
  }
  void OnDestroy(){if(dropped==this){dropped=null;if(committed&&EcoAdventureRuntime.Instance&&!EcoAdventureRuntime.Instance.Has(WeaponId))EcoAdventureRuntime.Instance.AddItem(WeaponId,"電漿槍");}}
 }
 public sealed class Level04ProjectileVisual : MonoBehaviour { private Vector3 start,target;private float elapsed;public void Initialize(Vector3 from,Vector3 to){start=from;target=to;}void Update(){elapsed+=Time.deltaTime;transform.position=Vector3.Lerp(start,target,Mathf.Clamp01(elapsed/.15f));if(elapsed>=.15f){Destroy(gameObject,2f);enabled=false;}} }
}