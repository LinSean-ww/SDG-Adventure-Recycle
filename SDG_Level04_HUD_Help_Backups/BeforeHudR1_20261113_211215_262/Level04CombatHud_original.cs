using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.UI;
namespace SDGAdventure.Level04 {
 public sealed class Level04CombatHud : MonoBehaviour {
  private Level04PlayerHealth player;private BossHealth boss;private Image playerFill,bossFill,flash;private Text playerText,bossText;private RectTransform root;private float flashUntil;
  public void Initialize(Level04PlayerHealth health,BossHealth enemy,PlayerHudCanvas hud){
   player=health;boss=enemy;hud.Build();var host=hud.transform.Find("PlayerHudCanvas");if(!host){Debug.LogError("Level04 shared HUD canvas unavailable");return;}
   root=NewRect("Level04CombatStatus",host);root.anchorMin=root.anchorMax=root.pivot=new Vector2(1,1);root.anchoredPosition=new Vector2(-18,-18);root.sizeDelta=new Vector2(240,92);
   CreateBar(root,"PlayerHealth",0,out playerFill,out playerText);CreateBar(root,"BossHealth",-45,out bossFill,out bossText);
   var f=NewRect("Level04DamageFlash",host);f.SetAsFirstSibling();f.anchorMin=Vector2.zero;f.anchorMax=Vector2.one;f.offsetMin=f.offsetMax=Vector2.zero;flash=f.gameObject.AddComponent<Image>();flash.color=Color.clear;flash.raycastTarget=false;
   player.Changed+=Refresh;player.Damaged+=OnDamaged;boss.HealthChanged+=Refresh;Refresh();
  }
  private static RectTransform NewRect(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
  private static void CreateBar(Transform parent,string name,float y,out Image fill,out Text label){
   var row=NewRect(name,parent);row.anchorMin=row.anchorMax=row.pivot=new Vector2(1,1);row.anchoredPosition=new Vector2(0,y);row.sizeDelta=new Vector2(240,36);
   var text=NewRect("Label",row);text.anchorMin=new Vector2(0,1);text.anchorMax=new Vector2(1,1);text.pivot=new Vector2(.5f,1);text.sizeDelta=new Vector2(0,20);label=text.gameObject.AddComponent<Text>();label.font=PlayerGuiFont.Load();label.fontSize=15;label.alignment=TextAnchor.MiddleLeft;label.color=Color.white;label.raycastTarget=false;
   var bg=NewRect("Track",row);bg.anchorMin=new Vector2(0,0);bg.anchorMax=new Vector2(1,0);bg.pivot=new Vector2(.5f,0);bg.sizeDelta=new Vector2(0,13);var image=bg.gameObject.AddComponent<Image>();image.color=new Color(.12f,.12f,.12f,.9f);image.raycastTarget=false;
   var bar=NewRect("Fill",bg);bar.anchorMin=Vector2.zero;bar.anchorMax=Vector2.one;bar.offsetMin=bar.offsetMax=Vector2.zero;fill=bar.gameObject.AddComponent<Image>();fill.type=Image.Type.Simple;fill.raycastTarget=false;
  }
  void OnDamaged(){flashUntil=Time.unscaledTime+.18f;}
  void Refresh(){if(!player||!boss||!playerFill||!bossFill)return;float p=(float)player.CurrentHealth/Mathf.Max(1,player.MaxHealth);playerFill.fillAmount=p;playerFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(p),1f);playerFill.color=p<=.3f?new Color(.95f,.15f,.1f):new Color(.18f,.8f,.3f);playerText.text=$"生命 {player.CurrentHealth} / {player.MaxHealth}";float b=(float)boss.CurrentHealth/Mathf.Max(1,boss.maxHealth);bossFill.fillAmount=b;bossFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(b),1f);bossFill.color=new Color(.85f,.25f,.12f);bossText.text=$"Boss {boss.CurrentHealth} / {boss.maxHealth}";}
  void Update(){if(flash)flash.color=Time.unscaledTime<flashUntil?new Color(.8f,0,0,.12f):Color.clear;}
  void OnDestroy(){if(player){player.Changed-=Refresh;player.Damaged-=OnDamaged;}if(boss)boss.HealthChanged-=Refresh;if(root)Destroy(root.gameObject);if(flash)Destroy(flash.gameObject);}
 }
}