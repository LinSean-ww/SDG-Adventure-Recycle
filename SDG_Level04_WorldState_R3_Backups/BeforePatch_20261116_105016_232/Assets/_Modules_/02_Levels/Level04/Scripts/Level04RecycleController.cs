using System;
using System.Collections.Generic;
using System.Linq;
using SDGAdventure.SharedPlayer;
using UnityEngine;
namespace SDGAdventure.Level04 {
 public sealed class Level04RecycleController : MonoBehaviour {
  public static Level04RecycleController Instance { get; private set; }
  public bool RecycleMode { get; private set; }
  public RecycleItem Carried { get; private set; }
  private BossHealth boss; private RecycleHouse house; private Transform carry; private Camera cam; private PlayerInputService input;
  private readonly Dictionary<RecycleItem,Collider> proxies=new Dictionary<RecycleItem,Collider>();
  private readonly List<Level04RecycleInteractable> interactables=new List<Level04RecycleInteractable>();
  private Collider houseProxy; private int lastInteractionFrame=-1;
  public event Action Changed;
  public static string ItemName(RecycleItemType type){switch(type){case RecycleItemType.Brick:return "磚塊";case RecycleItemType.Tire:return "輪胎";case RecycleItemType.GlassBottle:return "玻璃瓶";case RecycleItemType.Disc:return "光碟";case RecycleItemType.Motherboard:return "主機板";default:return "回收物";}}
  public void Configure(BossHealth b,RecycleHouse h,Camera c,Transform anchor,RecycleItem[] items){
   if(Instance && Instance!=this)throw new InvalidOperationException("Duplicate Level04 recycle controller");
   if(!b||!h||!c||!anchor||items==null||items.Length!=5)throw new ArgumentException("Level04 recycle references incomplete");
   boss=b;house=h;cam=c;carry=anchor;input=PlayerInputService.Instance;Instance=this;
   foreach(var item in items){if(!item)throw new ArgumentException("Missing recycle item");var col=item.GetComponent<Collider>();if(!col)throw new InvalidOperationException("Recycle collider missing: "+item.name);var proxy=CloneCollider(col);proxy.enabled=false;proxies.Add(item,proxy);var interact=item.gameObject.AddComponent<Level04RecycleInteractable>();interact.Configure(this,item);interactables.Add(interact);item.gameObject.AddComponent<Level04ProximityHint>();}
   var point=house.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="SubmitPoint");if(!point)throw new InvalidOperationException("SubmitPoint missing");var original=point.GetComponent<Collider>();if(!original)throw new InvalidOperationException("SubmitPoint collider missing");houseProxy=CloneCollider(original);houseProxy.enabled=false;
   var houseInteract=house.gameObject.AddComponent<Level04RecycleInteractable>();houseInteract.Configure(this,null);interactables.Add(houseInteract);house.gameObject.AddComponent<Level04ProximityHint>();
   boss.Died+=BeginRecycle;RecycleMode=boss.IsDead;if(RecycleMode)EnableProxies();
  }
  private static Collider CloneCollider(Collider source){
   if(source is BoxCollider box){var copy=source.gameObject.AddComponent<BoxCollider>();copy.center=box.center;copy.size=box.size;copy.isTrigger=false;return copy;}
   if(source is SphereCollider sphere){var copy=source.gameObject.AddComponent<SphereCollider>();copy.center=sphere.center;copy.radius=sphere.radius;copy.isTrigger=false;return copy;}
   throw new InvalidOperationException("Unsupported recycle collider: "+source.GetType().Name);
  }
  private void BeginRecycle(){if(RecycleMode)return;RecycleMode=true;EnableProxies();Changed?.Invoke();}
  private void EnableProxies(){foreach(var p in proxies.Values)if(p)p.enabled=true;if(houseProxy)houseProxy.enabled=true;}
  public bool CanInteract(RecycleItem item){return RecycleMode && input && input.Context==PlayerInputContext.Gameplay && !input.GameplayActionsBlocked && PlayerCursor.LockState==CursorLockMode.Locked && (item ? !Carried && proxies.ContainsKey(item) : Carried!=null && house && house.CompletedQuestions<house.TotalQuestions);}
  public bool CanTarget(RecycleItem item){return CanInteract(item)||(item && Carried && item!=Carried && proxies.ContainsKey(item) && RecycleMode && input && input.Context==PlayerInputContext.Gameplay && !input.GameplayActionsBlocked && PlayerCursor.LockState==CursorLockMode.Locked);}
  public void Interact(RecycleItem item){
   if(!CanInteract(item)||lastInteractionFrame==Time.frameCount)return;lastInteractionFrame=Time.frameCount;
   if(item){Carried=item;proxies[item].enabled=false;item.PickUp(carry);}
   else {var current=Carried;Carried=null;bool correct=house.TrySubmit(current);if(!correct && current){current.Drop(DropPosition());if(proxies.TryGetValue(current,out var proxy)&&proxy)proxy.enabled=true;}}
   Changed?.Invoke();
  }
  public static bool TryHandleDrop(){if(!Instance || !Instance.Carried)return false;Instance.DropCarried();return true;}
  public bool DropCarried(){if(!Carried||!RecycleMode||!input||input.Context!=PlayerInputContext.Gameplay||input.GameplayActionsBlocked||PlayerCursor.LockState!=CursorLockMode.Locked)return false;var current=Carried;Carried=null;current.Drop(DropPosition());if(proxies.TryGetValue(current,out var proxy)&&proxy)proxy.enabled=true;Changed?.Invoke();return true;}
  private Vector3 DropPosition(){Vector3 p=cam.transform.position+cam.transform.forward*1.2f;var hits=Physics.RaycastAll(p+Vector3.up,Vector3.down,10f,~0,QueryTriggerInteraction.Ignore);Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));foreach(var h in hits)if(h.collider&&!h.collider.transform.IsChildOf(transform))return h.point+Vector3.up*.25f;return p;}
  public void Shutdown(){if(boss)boss.Died-=BeginRecycle;if(Carried)DropCarried();foreach(var p in proxies.Values)if(p)Destroy(p);proxies.Clear();if(houseProxy)Destroy(houseProxy);foreach(var i in interactables)if(i){var hint=i.GetComponent<Level04ProximityHint>();if(hint)Destroy(hint);Destroy(i);}interactables.Clear();if(Instance==this)Instance=null;Changed=null;}
  void OnDestroy()=>Shutdown();
 }
 public sealed class Level04RecycleInteractable : MonoBehaviour,IPlayerInteractable {
  private Level04RecycleController owner; private RecycleItem item;
  public void Configure(Level04RecycleController value,RecycleItem target){owner=value;item=target;}
  public string Prompt=>item?(owner && owner.Carried ? "請先放下目前攜帶的物品" : "拾取回收物："+Level04RecycleController.ItemName(item.itemType)):"提交回收物";
  public bool CanInteract(GameObject actor)=>actor && owner && owner.CanTarget(item);
  public void Interact(GameObject actor){if(actor && owner && owner.CanInteract(item))owner.Interact(item);}
 }
}