using System;
using System.Linq;
using SDGAdventure.ModuleSystem;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace SDGAdventure.Level04 {
 public sealed class Level04ModuleAdapter : MonoBehaviour,IModuleAdapter {
  public const string Id="level04";
  public const string ScenePath="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity";
  public string ModuleId=>Id;
  private ModuleCoordinator coordinator;private BossHealth boss;private RecycleHouse house;private Level04RecycleController recycle;
  private PlayerInputService input;private PlayerHudCanvas hud;private PlayerTaskJournal journal;private PlayerPauseMenu menu;private Level04PlayerHealth playerHealth;private Level04CombatHud combatHud;private bool completed,failed;private float previousTimeScale,startTime;private string lastStatus;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
  private static void Register(){if(!ModuleRegistry.TryRegister(new ModuleDefinition(Id,"第四關",ScenePath,typeof(Level04ModuleAdapter)),out string error))Debug.LogError(error);}
  public bool Initialize(ModuleCoordinator owner){
   coordinator=owner;var roots=gameObject.scene.GetRootGameObjects();
   boss=roots.SelectMany(r=>r.GetComponentsInChildren<BossHealth>(true)).SingleOrDefault();house=roots.SelectMany(r=>r.GetComponentsInChildren<RecycleHouse>(true)).SingleOrDefault();
   var player=roots.SelectMany(r=>r.GetComponentsInChildren<SharedFirstPersonController>(false)).SingleOrDefault();
   var items=roots.SelectMany(r=>r.GetComponentsInChildren<RecycleItem>(true)).ToArray();
   input=PlayerInputService.Instance;if(!boss||!house||!house.questionText||!player||!input||items.Length!=5||items.Select(i=>i.itemType).Distinct().Count()!=5)return false;
   var camera=player.GetComponentInChildren<Camera>();var anchor=camera?camera.transform.Find("Level04 Carry Anchor"):null;
   hud=player.GetComponent<PlayerHudCanvas>();journal=player.GetComponent<PlayerTaskJournal>();menu=player.GetComponent<PlayerPauseMenu>();playerHealth=player.GetComponent<Level04PlayerHealth>();
   if(!camera||!anchor||!hud||!journal||!menu||!playerHealth)return false;
   recycle=player.gameObject.AddComponent<Level04RecycleController>();
   try{recycle.Configure(boss,house,camera,anchor,items);}catch(Exception e){Debug.LogError("Level04 recycle setup failed: "+e.Message);return false;}
   combatHud=player.gameObject.AddComponent<Level04CombatHud>();combatHud.Initialize(playerHealth,boss,hud);playerHealth.Died+=Fail;
   startTime=Time.time;boss.Died+=UpdateState;house.ProgressChanged+=OnProgress;recycle.Changed+=UpdateState;
   menu.SetLevelActions(Restart,ReturnToSelection,()=>!completed&&!failed);UpdateState();return true;
  }
  private void OnProgress(){UpdateState();if(house&&house.CompletedQuestions==house.TotalQuestions)Complete();}
  private void Complete(){
   if(completed||failed||!coordinator||!boss||!boss.IsDead||!house||house.CompletedQuestions!=house.TotalQuestions)return;
   var result=new ModuleResult{ModuleId=Id,Completed=true,PuzzleAttempts=house.CompletedQuestions,PlaySeconds=Time.time-startTime};
   if(!coordinator.ReportCompletion(this,result))return;completed=true;previousTimeScale=Time.timeScale;Time.timeScale=0;input.SetContext(PlayerInputContext.UI);PlayerCursor.LockState=CursorLockMode.None;Cursor.visible=true;
   var view=gameObject.AddComponent<Level04CompletionView>();view.Show(ReturnToSelection,Restart,house.Feedback);
  }
  private void Fail(){
   if(completed||failed||!input)return;if(recycle && recycle.Carried)recycle.DropCarried();failed=true;previousTimeScale=Time.timeScale;Time.timeScale=0;input.SetContext(PlayerInputContext.UI);PlayerCursor.LockState=CursorLockMode.None;Cursor.visible=true;
   var view=gameObject.AddComponent<Level04FailureView>();view.Show(ReturnToSelection,Restart,"生命值歸零");
  }
  private void UpdateState(){
   if(!boss||!house||!recycle||!hud||!journal||!input)return;
   bool inRecycle=recycle.RecycleMode;string key=input.Keybindings.BindingDisplay("Gameplay","Interact",0),drop=input.Keybindings.BindingDisplay("Gameplay","Drop",0);
   string status=inRecycle?"資源回收："+house.CompletedQuestions+" / "+house.TotalQuestions+" 題\n"+(recycle.Carried?"攜帶中："+Level04RecycleController.ItemName(recycle.Carried.itemType)+" ×1　放下 ["+drop+"]":"瞄準回收物拾取 ["+key+"]")+"\n"+house.Feedback:"Boss 戰鬥：擊敗 Boss 後開始回收";
   if(status==lastStatus)return;lastStatus=status;hud.SetStatus(status);journal.SetTasks(new[]{new PlayerTaskItem("level04_boss","擊敗 Boss",boss.IsDead?"Boss 已擊敗。":"使用武器擊敗 Boss。",boss.IsDead),new PlayerTaskItem("level04_recycle","資源循環",$"完成回收題目 {house.CompletedQuestions}/{house.TotalQuestions}。",house.CompletedQuestions==house.TotalQuestions)});
  }
  private void Update(){if(!completed && !failed && recycle && recycle.RecycleMode)UpdateState();}
  private void Restart(){if(coordinator&&!coordinator.RestartCurrent(out string error))Debug.LogError(error);}
  private void ReturnToSelection(){if(coordinator&&!coordinator.ReturnToSelection(out string error))Debug.LogError(error);}
  public void Shutdown(){if(playerHealth)playerHealth.Died-=Fail;if(combatHud)Destroy(combatHud);if(boss)boss.Died-=UpdateState;if(house)house.ProgressChanged-=OnProgress;if(recycle){recycle.Changed-=UpdateState;recycle.Shutdown();Destroy(recycle);}if(completed||failed)Time.timeScale=previousTimeScale;if(hud)hud.SetStatus("");coordinator=null;boss=null;house=null;recycle=null;}
  private void OnDestroy()=>Shutdown();
 }
}