using System.Linq;
using C3TreasurePuzzle;
using SDGAdventure.SharedPlayer;
using UnityEngine;
namespace SDGAdventure.Level04 {
 [DisallowMultipleComponent]
 public sealed class Level04ProximityHint : MonoBehaviour {
  private C3HintVisuals visuals;private SharedFirstPersonController player;private IPlayerInteractable target;private readonly RaycastHit[] hits=new RaycastHit[32];
  void Start(){target=GetComponents<MonoBehaviour>().OfType<IPlayerInteractable>().FirstOrDefault();visuals=new C3HintVisuals(transform);}
  void Update(){
   if(visuals==null)return;if(!player)player=FindFirstObjectByType<SharedFirstPersonController>();var input=PlayerInputService.Instance;
   bool enabledForPlayer=player&&target!=null&&input&&input.Context==PlayerInputContext.Gameplay&&!input.GameplayActionsBlocked&&target.CanInteract(player.gameObject);
   float distance=enabledForPlayer?Vector3.Distance(player.transform.position,transform.position):float.PositiveInfinity;
   float fade=distance<3.6f&&C3ProximityHint.VisibleFrom(player.transform,transform,transform.position+Vector3.up*.25f,hits)?Mathf.SmoothStep(0,1,Mathf.Clamp01((3.6f-distance)/1.4f)):0;
   visuals.SetCue(Color.cyan,fade*(.6f+.2f*Mathf.Sin(Time.time*2f)));
  }
  void OnDisable(){visuals?.SetCue(Color.black,0);}
  void OnDestroy(){visuals?.Dispose();visuals=null;}
 }
}