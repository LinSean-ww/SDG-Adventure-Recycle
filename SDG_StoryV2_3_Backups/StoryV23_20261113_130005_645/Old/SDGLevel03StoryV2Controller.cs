using System;
using System.Collections.Generic;
using C3TreasurePuzzle;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace SDGAdventure.Level03StoryV2
{
    // Add-on for the existing Level03 scene. It preserves collection, sorting, inventory,
    // crafting, ammo and scene transition authorities. No per-frame FindObjectsOfType.
    [DisallowMultipleComponent]
    public sealed class SDGLevel03StoryV2Controller : MonoBehaviour
    {
        private enum Screen { Hidden, Calibration, Trace, Allocation, Assembly }
        private enum Quest { Explore, Grid1, Grid2, Grid3, Production, Weapon, Range, Exit }
        private const float RailWidth = 570f;
        private const float PathTolerance = 15.5f;
        private const int TargetCount = 3;
        private static readonly Color Navy = new Color(.025f,.075f,.092f,.98f);
        private static readonly Color Teal = new Color(.18f,.87f,.78f);
        private static readonly Color Lime = new Color(.25f,.89f,.45f);
        private static readonly Color Red = new Color(.89f,.25f,.26f);
        private static readonly Vector2[] Route =
        {
            new Vector2(-318f,-25f),new Vector2(-255f,12f),new Vector2(-190f,65f),
            new Vector2(-120f,56f),new Vector2(-54f,1f),new Vector2(6f,-56f),
            new Vector2(71f,-67f),new Vector2(139f,-23f),new Vector2(203f,36f),
            new Vector2(260f,40f),new Vector2(318f,-5f)
        };
        private static readonly int[] Watts = {35,30,25,20};
        private static readonly string[] Loads = {"污染處理系統","安全通風系統","回收輸送帶","裝飾照明"};
        private static readonly string[] AssemblySteps = {"再生金屬槍身","廢電路板控制晶片","再生能源充填"};

        private C3DeviceState device;
        private C3TreasureProgress progress;
        private C3PuzzleState puzzle;
        private C3PuzzleConsole console;
        private Quest quest = Quest.Explore;
        private Screen screen = Screen.Hidden;
        private Canvas canvas;
        private GameObject panel;
        private RectTransform panelRect, traceArea, marker, targetZone, needle, loadBar;
        private Text header, task, stage, details, feedback, action, loadText;
        private Button actionButton;
        private GameObject[] loadRows = new GameObject[4];
        private Text[] loadLabels = new Text[4];
        private readonly bool[] enabledLoads = new bool[4];
        private readonly List<GameObject> sceneVisuals = new List<GameObject>();
        private readonly List<Light> originalLights = new List<Light>();
        private readonly List<float> originalIntensities = new List<float>();
        private Color oldAmbient;
        private float oldAmbientIntensity;
        private Camera viewCamera;
        private CameraClearFlags oldClear;
        private Color oldCameraBackground;
        private Light flashlight;
        private bool lightingSaved;
        private bool gridComplete;
        private bool assemblyAuthorized;
        private bool assemblyOpen;
        private int assemblyStage;
        private int connectStep;
        private int chargeCount;
        private bool tracing;
        private int segmentProgress;
        private float pulseTime;
        private float earliestNextTrace;
        private bool runningNotified;
        private float scanAt;
        private float viewportAt;
        private bool waitingForScreen;
        private int completedTargets;
        private GameObject targetObject;
        private SDGLevel03PracticeTarget practice;
        private Vector3 targetHome, targetRight;
        private float rangeStarted;
        private bool rangePlaced;
        private bool wasGameplayContext;
        private CursorLockMode formerLock;
        private bool formerVisible;
        private AudioSource audio;
        private AudioClip blip;
        private float feedbackUntil;
        private string transientFeedback = "";

        public bool ExitUnlocked { get { return gridComplete && completedTargets >= TargetCount; } }

        private void Awake()
        {
            SDGLevel03StoryV2Bridge.Bind(this);
            // A C3-owned delegate bridge avoids assembly-definition circular references.
            C3StoryV2Hooks.ExitCheck = () => ExitUnlocked;
            C3StoryV2Hooks.CraftCheck = () => TryInterceptCraft();
        }

        private void Start()
        {
            device = FindObjectOfType<C3DeviceState>();
            progress = FindObjectOfType<C3TreasureProgress>();
            puzzle = FindObjectOfType<C3PuzzleState>();
            console = FindObjectOfType<C3PuzzleConsole>();
            if (!device || !progress || !puzzle || !console)
            {
                Debug.LogError("[SDG Story V2] Missing C3 device, progress, puzzle or console; new exit requirement will remain locked.");
                enabled = false;
                return;
            }
            BuildUI();
            BuildAtmosphere();
            device.Running += FactoryRunning;
            audio = gameObject.AddComponent<AudioSource>();
            audio.playOnAwake=false; audio.spatialBlend=0f; audio.volume=.15f;
            blip = Tone();
            if (device.FactoryState == C3FactoryState.Running) FactoryRunning();
            RefreshTask();
        }

        private void OnDestroy()
        {
            if (device != null) device.Running -= FactoryRunning;
            if (screen != Screen.Hidden) RestoreInput();
            if (lightingSaved)
            {
                RenderSettings.ambientLight = oldAmbient;
                RenderSettings.ambientIntensity = oldAmbientIntensity;
                for (int i=0; i<originalLights.Count; ++i)
                    if (originalLights[i]) originalLights[i].intensity=originalIntensities[i];
            }
            if (viewCamera) {viewCamera.clearFlags=oldClear;viewCamera.backgroundColor=oldCameraBackground;}
            if (SDGLevel03StoryV2Bridge.Instance == this)
            {
                C3StoryV2Hooks.ExitCheck = null;
                C3StoryV2Hooks.CraftCheck = null;
                SDGLevel03StoryV2Bridge.Bind(null);
            }
        }

        private void Update()
        {
            if (device == null || panel == null) return;
            if (!viewCamera && Time.unscaledTime >= viewportAt)
            {
                viewportAt=Time.unscaledTime+1f;
                FindPlayerCamera();
            }
            if (screen == Screen.Calibration) TickCalibration();
            if (screen == Screen.Trace) TickTrace();
            if (screen == Screen.Hidden)
            {
                if (waitingForScreen && device.FactoryState == C3FactoryState.Running && CanOpenPanel())
                { waitingForScreen=false; OpenPanel(Screen.Calibration); }
                if (gridComplete && Time.unscaledTime >= scanAt)
                { scanAt=Time.unscaledTime+.4f; CheckProductionAndWeapon(); }
                if (quest == Quest.Range && Input.GetKeyDown(KeyCode.F8)) PlaceTarget();
            }
            if (quest == Quest.Range) UpdateTarget();
            if (Time.unscaledTime >= feedbackUntil && transientFeedback.Length > 0)
            { transientFeedback=""; RefreshTask(); }
        }

        private void FactoryRunning()
        {
            if (runningNotified || gridComplete) return;
            runningNotified=true;
            quest=Quest.Grid1;
            waitingForScreen=true;
            RefreshTask();
        }

        private void FindPlayerCamera()
        {
            Camera cam = Camera.main;
            if (!cam || cam.GetComponentInParent<C3FirstPersonController>() == null) return;
            viewCamera=cam;
            oldClear=viewCamera.clearFlags;
            oldCameraBackground=viewCamera.backgroundColor;
            if (!flashlight)
            {
                GameObject g = new GameObject("StoryV2_Flashlight_NoShadows");
                g.transform.SetParent(cam.transform,false);
                g.transform.localPosition=Vector3.zero;
                flashlight=g.AddComponent<Light>();
                flashlight.type=LightType.Spot;flashlight.range=13f;flashlight.spotAngle=56f;
                flashlight.intensity=2.0f;flashlight.shadows=LightShadows.None;
                sceneVisuals.Add(g);
            }
            ApplyLighting(gridComplete ? 3 : (quest==Quest.Grid1 ? 0 : quest==Quest.Grid2 ? 1 : quest==Quest.Grid3 ? 2 : 0));
        }

        private void BuildAtmosphere()
        {
            oldAmbient=RenderSettings.ambientLight;
            oldAmbientIntensity=RenderSettings.ambientIntensity;
            foreach (Light light in FindObjectsOfType<Light>())
            {
                if (!light || light.gameObject.scene != gameObject.scene) continue;
                originalLights.Add(light);
                originalIntensities.Add(light.intensity);
            }
            lightingSaved=true;
            // Four inexpensive emissive markers: console + first 3 quest items.
            CreateRedMarker(console.transform.position + Vector3.up*1.4f);
            int count=0;
            foreach (C3CollectiblePart part in FindObjectsOfType<C3CollectiblePart>())
            {
                if (!part || count>=3) break;
                CreateRedMarker(part.transform.position + Vector3.up*.45f);
                count++;
            }
            ApplyLighting(0);
        }
        private void CreateRedMarker(Vector3 pos)
        {
            GameObject g=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name="StoryV2_RedFaultLED";
            g.transform.position=pos;
            if(g.scene != gameObject.scene) SceneManager.MoveGameObjectToScene(g,gameObject.scene);
            g.transform.localScale=Vector3.one*.11f;
            Collider col=g.GetComponent<Collider>();if(col) Destroy(col);
            Renderer r=g.GetComponent<Renderer>();
            Shader s=Shader.Find("Unlit/Color");
            if(s) {Material m=new Material(s);m.color=Red;r.material=m;}
            sceneVisuals.Add(g);
        }
        private void ApplyLighting(int phase)
        {
            if (!lightingSaved) return;
            float factor=phase==0?.025f:phase==1?.24f:phase==2?.48f:1f;
            RenderSettings.ambientLight=Color.Lerp(new Color(.006f,.008f,.012f),oldAmbient,factor);
            RenderSettings.ambientIntensity=Mathf.Lerp(.015f,oldAmbientIntensity,factor);
            for(int i=0;i<originalLights.Count;i++)
                if(originalLights[i]) originalLights[i].intensity=originalIntensities[i]*factor;
            if(viewCamera)
            {
                viewCamera.clearFlags=phase==0?CameraClearFlags.SolidColor:oldClear;
                viewCamera.backgroundColor=phase==0?new Color(.006f,.01f,.018f):oldCameraBackground;
            }
            if(flashlight) flashlight.enabled=phase<3;
            foreach(GameObject g in sceneVisuals)
                if(g && g.name=="StoryV2_RedFaultLED") g.SetActive(phase<3);
        }

        private bool CanOpenPanel()
        {
            if (Time.timeScale<=0 || C3PuzzleConsole.BlocksGameplayInputThisFrame ||
                C3CompletionScreen.BlocksGameplayInputThisFrame) return false;
            PlayerInputService input=PlayerInputService.Instance;
            return input==null || input.Context==PlayerInputContext.Gameplay;
        }
        private void OpenPanel(Screen mode)
        {
            if (panel == null || screen!=Screen.Hidden || !CanOpenPanel()) return;
            formerLock=PlayerCursor.LockState;formerVisible=Cursor.visible;
            PlayerInputService service=PlayerInputService.Instance;
            wasGameplayContext=service != null && service.Context==PlayerInputContext.Gameplay;
            if(service!=null) service.SetContext(PlayerInputContext.UI);
            PlayerCursor.LockState=CursorLockMode.None; Cursor.visible=true;
            screen=mode;panel.SetActive(true);
            ResetElements();
            if(mode==Screen.Calibration) ShowCalibration();
            else if(mode==Screen.Trace) ShowTrace();
            else if(mode==Screen.Allocation) ShowAllocation();
            else if(mode==Screen.Assembly) ShowAssembly();
        }
        private void RestoreInput()
        {
            PlayerInputService service=PlayerInputService.Instance;
            if (service!=null && wasGameplayContext && service.Context==PlayerInputContext.UI)
                service.SetContext(PlayerInputContext.Gameplay);
            if(!C3CompletionScreen.BlocksGameplayInputThisFrame)
            {PlayerCursor.LockState=formerLock;Cursor.visible=formerVisible;}
        }
        private void ClosePanel()
        {
            if(screen==Screen.Hidden) return;
            if(panel)panel.SetActive(false);
            screen=Screen.Hidden;
            RestoreInput();
        }
        private void ResetElements()
        {
            for(int i=0;i<loadRows.Length;i++)if(loadRows[i])loadRows[i].SetActive(false);
            traceArea.gameObject.SetActive(false);
            if(needle)needle.parent.gameObject.SetActive(false);
            if(actionButton)actionButton.gameObject.SetActive(false);
            if(loadBar) loadBar.parent.gameObject.SetActive(false);
            if(loadText) loadText.gameObject.SetActive(false);
            transientFeedback="";
            feedback.text="";
        }
        private void SetAction(string name, Action onPress)
        {
            actionButton.gameObject.SetActive(true);
            action.text=name;
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(()=>onPress());
        }
        private void ShowCalibration()
        {
            header.text="電網①　電壓頻率校準";
            stage.text="簡單 · 讓指針進入綠色區域";
            details.text="按 SPACE 或 ENTER 將主電壓同步至安全範圍。";
            needle.parent.gameObject.SetActive(true);
            targetZone.anchoredPosition=new Vector2(-105f,0);
            targetZone.sizeDelta=new Vector2(RailWidth*.25f,52f);
            pulseTime=0f;
            quest=Quest.Grid1;RefreshTask();
        }
        private void TickCalibration()
        {
            pulseTime+=Time.unscaledDeltaTime*.62f;
            float pos=Mathf.PingPong(pulseTime,1f);
            needle.anchoredPosition=new Vector2((pos-.5f)*RailWidth,0f);
            if(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Return))
            {
                if(Mathf.Abs(pos-(.5f-105f/RailWidth))<=.125f)
                {
                    if(audio&&blip)audio.PlayOneShot(blip);
                    quest=Quest.Grid2;ApplyLighting(1);
                    screen=Screen.Trace;ResetElements();ShowTrace();
                }
                else {feedback.text="未命中安全區。可以立即重試，不必重新探索。";}
            }
        }
        private void ShowTrace()
        {
            header.text="電網②　曲線電纜導通";
            stage.text="中等 · 滑鼠沿管線中央移動，不可碰紅色管壁";
            details.text="滑鼠移至左側綠色起點，按住左鍵慢慢拖到右側黃色終點。碰壁立即重來。";
            traceArea.gameObject.SetActive(true);
            tracing=false;segmentProgress=0;earliestNextTrace=Time.unscaledTime;
            quest=Quest.Grid2;RefreshTask();
        }
        private void TickTrace()
        {
            if(Time.unscaledTime<earliestNextTrace) return;
            Vector2 pos;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(traceArea,
                Input.mousePosition,null,out pos))return;
            marker.anchoredPosition=pos;
            float start=Vector2.Distance(pos,Route[0]);
            if(!tracing)
            {
                marker.gameObject.SetActive(true);
                if(start<=20f && Input.GetMouseButtonDown(0))
                {tracing=true;segmentProgress=0;feedback.text="導通中 · 保持按住滑鼠左鍵";}
                return;
            }
            if(!Input.GetMouseButton(0)){FailTrace("放開滑鼠，電流中斷。從起點重來。");return;}
            float closest=float.MaxValue;int nearIndex=-1;
            int min=Mathf.Max(0,segmentProgress-1);
            int max=Mathf.Min(Route.Length-2,segmentProgress+2);
            for(int i=min;i<=max;i++)
            {
                float d=DistanceToSegment(pos,Route[i],Route[i+1]);
                if(d<closest){closest=d;nearIndex=i;}
            }
            if(closest>PathTolerance || nearIndex<0){FailTrace("碰到紅色管壁！電流短路，重新走整條管線。");return;}
            // No teleport from the start to the finish; progression is local.
            if(nearIndex>segmentProgress)segmentProgress=nearIndex;
            if(segmentProgress>=Route.Length-3 && Vector2.Distance(pos,Route[Route.Length-1])<=19f)
            {
                tracing=false;
                quest=Quest.Grid3;ApplyLighting(2);
                screen=Screen.Allocation;ResetElements();ShowAllocation();
            }
        }
        private void FailTrace(string message)
        {
            tracing=false;segmentProgress=0;
            earliestNextTrace=Time.unscaledTime+.7f;
            feedback.text=message;
            if(audio&&blip)audio.PlayOneShot(blip);
            marker.anchoredPosition=Route[0];
        }
        private static float DistanceToSegment(Vector2 p,Vector2 a,Vector2 b)
        {
            Vector2 v=b-a;
            float t=Mathf.Clamp01(Vector2.Dot(p-a,v)/Mathf.Max(.0001f,v.sqrMagnitude));
            return Vector2.Distance(p,a+v*t);
        }
        private void ShowAllocation()
        {
            header.text="電網③　能源分配與過載保護";
            stage.text="困難 · 總供電只有 100 單位";
            details.text="必要設備都必須供電，但所有設備全開會超載。請選擇適當的供電組合。";
            for(int i=0;i<4;i++){enabledLoads[i]=false;loadRows[i].SetActive(true);}
            loadBar.parent.gameObject.SetActive(true);
            loadText.gameObject.SetActive(true);
            UpdateLoadUI();
            SetAction("確認供電配置",ConfirmAllocation);
            quest=Quest.Grid3;RefreshTask();
        }
        private void UpdateLoadUI()
        {
            int sum=0;
            for(int i=0;i<4;i++)
            {
                if(enabledLoads[i]) sum+=Watts[i];
                loadLabels[i].text=(enabledLoads[i]?"●  ":"○  ")+Loads[i]+"　"+Watts[i]+" 單位";
                loadLabels[i].color=enabledLoads[i]?Lime:Color.white;
            }
            loadBar.sizeDelta=new Vector2(Mathf.Min(sum,100)*5.6f,17f);
            loadText.text="主電網負載　"+sum+" / 100"+(sum>100?"  過載！":"");
            loadText.color=sum>100?Red:Teal;
        }
        private void ConfirmAllocation()
        {
            int sum=0;for(int i=0;i<4;i++)if(enabledLoads[i])sum+=Watts[i];
            if(sum>100){feedback.text="負載過高！請暫停非必要電路。";return;}
            if(!enabledLoads[0]||!enabledLoads[1]||!enabledLoads[2])
            {feedback.text="安全通風、污染處理與回收產線都不能停止。";return;}
            gridComplete=true;ApplyLighting(3);
            quest=Quest.Production;
            ClosePanel();
            Toast("綠能電網全部恢復！返回資源處理控制台啟動產線，再取得三種組裝材料。",8f);
            RefreshTask();
        }

        public bool TryInterceptCraft()
        {
            // Called from the ORIGINAL EcoAdventureRuntime.CraftWeapon() after its
            // normal power/material checks and before any material is consumed.
            if (device == null || device.FactoryState != C3FactoryState.Running || assemblyAuthorized) return false;
            if (!gridComplete) { Toast("請先完成三階段電網修復，再使用回收材料組裝武器。",5f); return true; }
            if (screen==Screen.Assembly) return true;
            if(screen!=Screen.Hidden || !CanOpenPanel())
            {Toast("請先關閉其他視窗再使用工作台。",3f);return true;}
            assemblyStage=connectStep=chargeCount=0;
            assemblyOpen=true;
            OpenPanel(Screen.Assembly);
            return true;
        }
        private void ShowAssembly()
        {
            header.text="回收能源雷射槍 · 組裝挑戰";
            stage.text="組裝步驟　"+(assemblyStage+1)+" / 3 · "+AssemblySteps[assemblyStage];
            if(assemblyStage==0)
            {
                details.text="將回收產線產出的再生金屬安裝為槍身結構。";
                SetAction("滑鼠安裝金屬槍身",()=>{assemblyStage=1;ShowAssembly();});
            }
            else if(assemblyStage==1)
            {
                details.text="依序連接電路：能源輸入 → 控制晶片 → 雷射發射模組。";
                SetAction(new[]{"連接① 能源輸入","連接② 控制晶片","連接③ 發射模組"}[connectStep],()=>
                {
                    connectStep++;
                    if(connectStep>=3){assemblyStage=2;ShowAssembly();}
                    else ShowAssembly();
                });
            }
            else
            {
                details.text="充填能源核心，完成三次安全充能。";
                SetAction("能源充填　"+chargeCount+" / 3",()=>
                {
                    chargeCount++;
                    if(chargeCount>=3) FinishAssembly();else ShowAssembly();
                });
            }
        }
        private void FinishAssembly()
        {
            assemblyAuthorized=true;
            ClosePanel();
            EcoAdventureRuntime runtime=EcoAdventureRuntime.Instance;
            bool ok=runtime!=null && runtime.CraftWeapon();
            assemblyAuthorized=false;
            assemblyOpen=false;
            if(ok) {Toast("真正的循環量子投射器已加入背包！裝備武器後自動進入三靶射擊測試。",7f);CheckProductionAndWeapon();}
            else Toast("武器組裝未成功：檢查材料與背包，返回工作台可重新組裝。",6f);
        }
        private void CheckProductionAndWeapon()
        {
            if(quest==Quest.Exit || quest==Quest.Range) return;
            EcoAdventureRuntime runtime=EcoAdventureRuntime.Instance;
            if(runtime==null) return;
            if(runtime.Has(EcoAdventureRuntime.WeaponId))
            {
                quest=Quest.Range;
                completedTargets=0;
                RefreshTask();
                PlaceTarget();
            }
            else
            {
                quest=Quest.Production;
                RefreshTask();
            }
        }
        private void PlaceTarget()
        {
            if(quest!=Quest.Range || !viewCamera) return;
            if(targetObject)Destroy(targetObject);
            Transform cam=viewCamera.transform;
            RaycastHit wall;
            float distance=6.5f;
            if(Physics.Raycast(cam.position,cam.forward,out wall,7.5f,~0,QueryTriggerInteraction.Ignore))
            {
                if(wall.distance<3.5f)
                {rangePlaced=false;Toast("射擊練習需要空曠位置。請移動到開闊的工廠走道後按 F8 重新部署。",6f);return;}
                distance=Mathf.Min(6.5f,wall.distance-.8f);
            }
            targetHome=cam.position+cam.forward*distance;
            targetRight=cam.right;
            targetObject=GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name="SDG_StoryV2_REAL_WEAPON_Target_"+(completedTargets+1);
            if(targetObject.scene != gameObject.scene) SceneManager.MoveGameObjectToScene(targetObject,gameObject.scene);
            targetObject.transform.position=targetHome;
            targetObject.transform.rotation=Quaternion.LookRotation(-cam.forward,Vector3.up);
            targetObject.transform.localScale=new Vector3(.88f,.88f,.18f);
            Renderer r=targetObject.GetComponent<Renderer>();
            Shader shader=Shader.Find("Unlit/Color");
            if(shader){Material m=new Material(shader);m.color=completedTargets==2?Lime:Teal;r.material=m;}
            practice=targetObject.AddComponent<SDGLevel03PracticeTarget>();
            practice.Owner=this;
            rangeStarted=Time.unscaledTime;
            rangePlaced=true;
            RefreshTask();
        }
        private void UpdateTarget()
        {
            if(!targetObject || !rangePlaced)return;
            if(completedTargets==1)
                targetObject.transform.position=targetHome+targetRight*Mathf.Sin((Time.unscaledTime-rangeStarted)*1.8f)*1.3f;
            else if(completedTargets==2)
            {
                // Time-window target: it appears for 1.6s every 3.2s.
                float cycle=Mathf.Repeat(Time.unscaledTime-rangeStarted,3.2f);
                bool shown=cycle<1.6f;
                if(targetObject.activeSelf!=shown)targetObject.SetActive(shown);
            }
        }
        public void TargetHit(SDGLevel03PracticeTarget target)
        {
            if(quest!=Quest.Range || target!=practice || !rangePlaced ||
                EcoAdventureRuntime.Instance==null || !EcoAdventureRuntime.Instance.IsQuantumWeaponEquipped) return;
            completedTargets++;
            if(targetObject)Destroy(targetObject);
            targetObject=null;practice=null;
            rangePlaced=false;
            if(completedTargets<TargetCount)
            {
                Toast("命中！開始下一個射擊目標。",3f);
                PlaceTarget();
            }
            else
            {
                quest=Quest.Exit;
                Toast("射擊測試 3/3 通過！D 區鐵閘門已解鎖，帶著雷射槍前往第四關。",8f);
                RefreshTask();
            }
        }
        private void Toast(string text,float seconds)
        {
            transientFeedback=text;feedbackUntil=Time.unscaledTime+seconds;
            RefreshTask();
            Debug.Log("[SDG Story V2] "+text);
        }
        private void RefreshTask()
        {
            if(task==null)return;
            if(transientFeedback.Length>0){task.text=transientFeedback;return;}
            switch(quest)
            {
                case Quest.Explore: task.text="第三關 · 黑暗工廠｜手電筒已啟動。追蹤紅色故障燈，收集三個維修零件並完成控制台解謎。";break;
                case Quest.Grid1:task.text="能源異常！自動啟動電網① 電壓校準。";break;
                case Quest.Grid2:task.text="電網① 完成｜電網② 使用滑鼠沿曲線管道導通。";break;
                case Quest.Grid3:task.text="電網② 完成｜電網③ 配置必要用電，避免超載。";break;
                case Quest.Production:task.text="電網 3/3 完成｜回控制台啟動回收產線，將九件原料加工為三份再生資源；找齊金屬、塑膠和電路板，到工作台按 F 組裝雷射槍。";break;
                case Quest.Weapon:task.text="到組裝台完成循環量子投射器製作。";break;
                case Quest.Range:task.text="雷射槍訓練　"+completedTargets+"/3 ｜裝備真正的量子武器射擊 "+(completedTargets==0?"固定靶":completedTargets==1?"移動靶":"限時靶")+"（按 F8 可重新部署靶標）。";break;
                default:task.text="第三關完成條件已達成！前往 D 區出口，進入第四關。";break;
            }
        }

        private void BuildUI()
        {
            GameObject g=new GameObject("StoryV2_Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            g.transform.SetParent(transform,false);
            canvas=g.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=730;
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject system = new GameObject("StoryV2_UiEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                system.transform.SetParent(transform, false);
            }
            CanvasScaler scaler=g.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            RectTransform root=g.GetComponent<RectTransform>();
            Image hud=CreateImage("Objective background",root,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(1140,76),new Vector2(0,-20),Navy);
            hud.raycastTarget=false;
            task=Label("Story quest HUD",root,"",21,Teal,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(1114,70),new Vector2(0,-23));
            Image main=CreateImage("Mission Panel",root,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(850,620),Vector2.zero,Navy);
            panel=main.gameObject;panelRect=main.rectTransform;
            header=Label("Title",panelRect,"",33,Teal,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(810,62),new Vector2(0,-49));
            stage=Label("Stage",panelRect,"",23,Color.white,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(810,46),new Vector2(0,-113));
            details=Label("Description",panelRect,"",19,Teal,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(810,62),new Vector2(0,-171));
            feedback=Label("Warning",panelRect,"",21,Red,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(810,65),new Vector2(0,87));
            Image rail=CreateImage("Calibration rail",panelRect,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(RailWidth,52),new Vector2(0,3),new Color(.11f,.23f,.24f));
            rail.raycastTarget=false;
            targetZone=CreateImage("Safe zone",rail.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(140,52),Vector2.zero,Lime).rectTransform;
            needle=CreateImage("Pulse needle",rail.rectTransform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(6,75),Vector2.zero,Color.white).rectTransform;
            traceArea=CreateImage("Trace background",panelRect,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(720,260),new Vector2(0,-17),new Color(.035f,.12f,.13f)).rectTransform;
            for(int i=0;i<Route.Length-1;i++)
            {
                Segment("Danger_"+i,traceArea,Route[i],Route[i+1],44f,Red);
                Segment("Cable_"+i,traceArea,Route[i],Route[i+1],32f,new Color(.10f,.36f,.39f));
            }
            Dot("Start",traceArea,Route[0],32f,Lime);
            Dot("Finish",traceArea,Route[Route.Length-1],32f,new Color(1f,.79f,.27f));
            marker=Dot("Mouse current",traceArea,Route[0],12f,Color.white).rectTransform;
            marker.gameObject.SetActive(false);
            for(int i=0;i<4;i++)
            {
                int captured=i;
                Button btn=MakeButton("Load selector "+i,panelRect,new Vector2(610,48),new Vector2(0,91-i*62),"",()=>{enabledLoads[captured]=!enabledLoads[captured];UpdateLoadUI();});
                loadRows[i]=btn.gameObject;
                loadLabels[i]=btn.GetComponentInChildren<Text>();
            }
            Image barBack=CreateImage("Load background",panelRect,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(560,18),new Vector2(0,159),new Color(.12f,.2f,.23f));
            barBack.raycastTarget=false;
            loadBar=CreateImage("Load meter",barBack.rectTransform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(0,17),Vector2.zero,Lime).rectTransform;
            loadText=Label("Load label",panelRect,"",20,Teal,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(600,39),new Vector2(0,187));
            Button button=MakeButton("Action",panelRect,new Vector2(390,53),new Vector2(0,-245),"",()=>{});
            actionButton=button;action=button.GetComponentInChildren<Text>();
            panel.SetActive(false);
        }
        private static Image CreateImage(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 size,Vector2 pos,Color color)
        {
            GameObject g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);
            RectTransform r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.sizeDelta=size;r.anchoredPosition=pos;
            Image im=g.GetComponent<Image>();im.color=color;return im;
        }
        private static Text Label(string name,Transform parent,string content,int fontsize,Color color,Vector2 anchor,Vector2 pivot,Vector2 size,Vector2 pos)
        {
            GameObject g=new GameObject(name,typeof(RectTransform),typeof(Text));g.transform.SetParent(parent,false);
            RectTransform r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.sizeDelta=size;r.anchoredPosition=pos;
            Text t=g.GetComponent<Text>();
            // Prefer a Taiwanese Chinese font; the Unity built-in font may lack CJK glyphs.
            t.font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft JhengHei","Microsoft YaHei","Noto Sans CJK TC","Arial"},fontsize);
            if(!t.font) t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text=content;t.fontSize=fontsize;t.color=color;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;
            return t;
        }
        private static Button MakeButton(string name,Transform parent,Vector2 size,Vector2 pos,string caption,Action pressed)
        {
            Image image=CreateImage(name,parent,new Vector2(.5f,.5f),new Vector2(.5f,.5f),size,pos,new Color(.10f,.33f,.35f));
            Button button=image.gameObject.AddComponent<Button>();button.onClick.AddListener(()=>pressed());
            Label(name+" label",image.rectTransform,caption,20,Color.white,new Vector2(.5f,.5f),new Vector2(.5f,.5f),size,Vector2.zero);
            return button;
        }
        private static void Segment(string name,Transform parent,Vector2 a,Vector2 b,float thickness,Color c)
        {
            Vector2 d=b-a;
            Image im=CreateImage(name,parent,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(d.magnitude+2f,thickness),(a+b)*.5f,c);
            im.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
            im.raycastTarget=false;
        }
        private static Image Dot(string name,Transform parent,Vector2 p,float size,Color c)
        {
            Image im=CreateImage(name,parent,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(size,size),p,c);
            im.raycastTarget=false;return im;
        }
        private static AudioClip Tone()
        {
            const int n=2800;float[] data=new float[n];
            for(int i=0;i<n;i++)data[i]=Mathf.Sin(2f*Mathf.PI*660f*i/44100f)*.12f*(1f-i/(float)n);
            AudioClip clip=AudioClip.Create("StoryV2_Alert",n,1,44100,false);clip.SetData(data,0);return clip;
        }
    }
}
