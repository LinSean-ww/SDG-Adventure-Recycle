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
        public const string PatchVersion = "V2.6.2-R5.0";
        private enum Screen { Hidden, Calibration, Trace, Allocation, Assembly }
        private enum Quest { Explore, Grid1, Grid2, Grid3, Production, Weapon, Range, Exit }
        private const float RailWidth = 1070f;
        // R4.1 timing challenge: each success narrows the target and raises the needle speed.
        // The first zone uses 9.5% of the rail; the third uses only 5.3%.
        private static readonly float[] CalibrationWidths = {74f, 55f, 38f};
        private static readonly float[] CalibrationSpeeds = {.34f, .42f, .49f};
        private static readonly float[] CalibrationCenters = {285f, -245f, 105f};
        private const int CalibrationRoundCount = 3;
        private const int CalibrationMaxMisses = 3;
        private const float PathTolerance = 16f; // Radius inside the physically rendered tube lumen.
        private const int TargetCount = 3;
        private static readonly Color Navy = new Color(.025f,.075f,.092f,.98f);
        private static readonly Color Teal = new Color(.18f,.87f,.78f);
        private static readonly Color Lime = new Color(.25f,.89f,.45f);
        private static readonly Color Red = new Color(.89f,.25f,.26f);
        // The SAME control points drive the visible tube and the actual collision path.
        // These are local RectTransform coordinates in the 900 x 675 trace panel.
        private static readonly Vector2[] R4ControlPoints =
        {
            new Vector2(-350f, 92f), new Vector2(-313f, 83f),
            new Vector2(-275f, 35f), new Vector2(-233f, -10f),
            new Vector2(-186f, -16f), new Vector2(-141f, 27f),
            new Vector2(-104f, 85f), new Vector2(-58f, 109f),
            new Vector2(-14f, 82f), new Vector2(30f, 0f),
            new Vector2(74f, -93f), new Vector2(121f, -117f),
            new Vector2(172f, -91f), new Vector2(215f, -14f),
            new Vector2(254f, 65f), new Vector2(293f, 91f),
            new Vector2(350f, 92f)
        };
        private static Vector2[] BuildR4Route()
        {
            const int samplesPerSegment = 12;
            List<Vector2> points = new List<Vector2>((R4ControlPoints.Length - 1) * samplesPerSegment + 1);
            for(int i=0; i<R4ControlPoints.Length-1; i++)
            {
                Vector2 p0=R4ControlPoints[Mathf.Max(i-1,0)];
                Vector2 p1=R4ControlPoints[i];
                Vector2 p2=R4ControlPoints[i+1];
                Vector2 p3=R4ControlPoints[Mathf.Min(i+2,R4ControlPoints.Length-1)];
                for(int k=0; k<samplesPerSegment; k++)
                {
                    float t=k/(float)samplesPerSegment;
                    float t2=t*t, t3=t2*t;
                    Vector2 p=0.5f*((2f*p1)+(-p0+p2)*t+
                        (2f*p0-5f*p1+4f*p2-p3)*t2+
                        (-p0+3f*p1-3f*p2+p3)*t3);
                    points.Add(p);
                }
            }
            points.Add(R4ControlPoints[R4ControlPoints.Length-1]);
            return points.ToArray();
        }
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
        private GameObject calibrationRoot;
        private Sprite r42PanelSprite, r42PointerSprite;
        private SDGLevel03R5SortingEnergyGame sortingEnergy;
        private Text calibrationRoundLabel, calibrationAccuracyLabel, calibrationFeedback, calibrationMissLabel;
        private readonly Image[] calibrationRoundBlocks = new Image[CalibrationRoundCount];
        private int calibrationRound, calibrationMisses;
        private float calibrationResumeAt;
        private bool calibrationAdvanceAfterPause;
        private SDGLevel03TraceGraphic traceGraphic; // Legacy v2.3 renderer; intentionally not instantiated.
        private Texture2D r4TubeTexture;
        private Sprite r4TubeSprite;
        private Image r4TubeImage;
        private Canvas r4TubeCanvas;
        private SDGLevel03V4EnergyStream energyStream;
        private SDGLevel03V262EmbeddedSparks shortCircuitFx;
        private Vector2[] smoothTrace;
        // R3: arc-length windows are independent of spline sampling density.
        private float[] traceArcLengths;
        private GameObject traceDebugOverlay;
        private int traceStepIndex;
        private Vector2 lastTraceCursor;
        private Text traceProgressText;
        private SDGLevel03AssemblyPreview assemblyPreview;
        private GameObject[] assemblyCards = new GameObject[3];
        private RectTransform[] assemblySockets = new RectTransform[3];
        private Text[] assemblySocketLabels = new Text[3];
        private Text assemblyInstruction;
        private Text targetStatus;
        private float nextTargetRetry;
        private readonly List<Material> targetMaterials = new List<Material>();
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
        // Real 3D parts are snapped into the workbench; button-only stages removed.
        private bool tracing;
        private int segmentProgress;
        private float pulseTime;
        private float earliestNextTrace;
        private bool runningNotified;
        private float scanAt;
        private float viewportAt;
        private bool waitingForScreen;
        private int completedTargets;
        private readonly GameObject[] targetObjects = new GameObject[TargetCount];
        private readonly SDGLevel03PracticeTarget[] targetScripts = new SDGLevel03PracticeTarget[TargetCount];
        private readonly Vector3[] targetHomes = new Vector3[TargetCount];
        private readonly Vector3[] targetDirections = new Vector3[TargetCount];
        private readonly bool[] targetCompleted = new bool[TargetCount];
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
            Debug.Log("[SDG Story V2.6.2 R4.2] BOOT OK: reference-style HUD skin + truly dynamic pointer/goal + 3-stage challenge + shared R4 live tube.");
        }

        private void OnDestroy()
        {
            if (device != null) device.Running -= FactoryRunning;
            foreach(Material m in targetMaterials)if(m)Destroy(m);
            for(int i=0;i<TargetCount;i++)
            {
                if(!targetObjects[i])continue;
                foreach(MeshFilter f in targetObjects[i].GetComponentsInChildren<MeshFilter>(true))
                    if(f && f.sharedMesh && f.sharedMesh.name=="StoryV2_FloatingRing")Destroy(f.sharedMesh);
                Destroy(targetObjects[i]);
            }
            if (screen != Screen.Hidden) RestoreInput();
            if (lightingSaved)
            {
                RenderSettings.ambientLight = oldAmbient;
                RenderSettings.ambientIntensity = oldAmbientIntensity;
                for (int i=0; i<originalLights.Count; ++i)
                    if (originalLights[i]) originalLights[i].intensity=originalIntensities[i];
            }
            if(r42PanelSprite)Destroy(r42PanelSprite);
            if(r42PointerSprite)Destroy(r42PointerSprite);
            if(r4TubeSprite)Destroy(r4TubeSprite);
            if(r4TubeTexture)Destroy(r4TubeTexture);
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
                // No F8 action is needed. The target course deploys itself.
            }
            if (quest == Quest.Range)
            {
                if(!rangePlaced && Time.unscaledTime>=nextTargetRetry)
                {nextTargetRetry=Time.unscaledTime+1.0f;DeployAllFloatingTargets();}
                UpdateTarget();
            }
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
            if (!cam || cam.GetComponentInParent<C3FirstPersonController>() == null)
            {
                C3FirstPersonController player = FindObjectOfType<C3FirstPersonController>();
                cam = player ? player.GetComponentInChildren<Camera>() : null;
            }
            if (!cam) return;
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
            if(assemblyPreview)assemblyPreview.SetVisible(false);
            if(sortingEnergy)sortingEnergy.Hide();
            if(task)task.gameObject.SetActive(true);
            screen=Screen.Hidden;
            RestoreInput();
        }
        private void ResetElements()
        {
            // V2.5 photo screen is larger; all other minigames keep their original size.
            panelRect.sizeDelta = new Vector2(850f,620f);
            header.gameObject.SetActive(true);
            stage.gameObject.SetActive(true);
            details.gameObject.SetActive(true);
            feedback.gameObject.SetActive(true);
            for(int i=0;i<loadRows.Length;i++)if(loadRows[i])loadRows[i].SetActive(false);
            traceArea.gameObject.SetActive(false);
            if(assemblyPreview)assemblyPreview.SetVisible(false);
            for(int i=0;i<3;i++)
            {
                if(assemblyCards[i])assemblyCards[i].SetActive(false);
                if(assemblySockets[i])assemblySockets[i].gameObject.SetActive(false);
            }
            if(assemblyInstruction)assemblyInstruction.gameObject.SetActive(false);
            if(calibrationRoot)calibrationRoot.SetActive(false);
            if(sortingEnergy)sortingEnergy.Hide();
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
            // R4.3 keeps the panoramic reference HUD and swaps the arrow for a timing rod.
            panelRect.sizeDelta=new Vector2(1480f,834f);
            header.gameObject.SetActive(false);
            stage.gameObject.SetActive(false);
            details.gameObject.SetActive(false);
            feedback.gameObject.SetActive(false);
            calibrationRoot.SetActive(true);
            needle.parent.gameObject.SetActive(true);
            calibrationRound=0;
            calibrationMisses=0;
            calibrationResumeAt=0f;
            calibrationAdvanceAfterPause=false;
            ConfigureCalibrationRound();
            quest=Quest.Grid1;RefreshTask();
        }
        private void ConfigureCalibrationRound()
        {
            calibrationRound=Mathf.Clamp(calibrationRound,0,CalibrationRoundCount-1);
            targetZone.anchoredPosition=new Vector2(CalibrationCenters[calibrationRound],0f);
            targetZone.sizeDelta=new Vector2(CalibrationWidths[calibrationRound],104f);
            needle.anchoredPosition=new Vector2(-RailWidth*.5f,0f);
            pulseTime=0f;
            calibrationResumeAt=0f;
            calibrationAdvanceAfterPause=false;
            float precision=CalibrationWidths[calibrationRound] /
                (2f*RailWidth*CalibrationSpeeds[calibrationRound]);
            calibrationRoundLabel.text="挑戰次數   " + (calibrationRound+1) + " / " + CalibrationRoundCount;
            calibrationAccuracyLabel.text="目標精準度   ±" + precision.ToString("F2") + " 秒";
            calibrationMissLabel.text="失誤  " + calibrationMisses + " / " + CalibrationMaxMisses;
            for(int i=0;i<CalibrationRoundCount;i++)
                calibrationRoundBlocks[i].color=i<calibrationRound ? new Color(.12f,.86f,.92f) :
                    i==calibrationRound ? new Color(.18f,1f,.42f) : new Color(.12f,.28f,.36f);
            calibrationFeedback.text="左鍵點擊 / SPACE　•　命中綠色目標才算成功";
            calibrationFeedback.color=Teal;
        }
        private void TickCalibration()
        {
            if(Time.unscaledTime<calibrationResumeAt)return;
            if(calibrationAdvanceAfterPause)
            {
                ConfigureCalibrationRound();
                return;
            }
            pulseTime+=Time.unscaledDeltaTime*CalibrationSpeeds[calibrationRound];
            float pos=Mathf.PingPong(pulseTime,1f);
            float x=(pos-.5f)*RailWidth;
            needle.anchoredPosition=new Vector2(x,0f);
            // Buttons in the top-right corner are interactive; do not count those clicks as misses.
            if(Input.GetMouseButtonDown(0) && calibrationRoot)
            {
                Vector2 local;
                if(RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    calibrationRoot.GetComponent<RectTransform>(),Input.mousePosition,null,out local) &&
                    local.x>525f && local.y>288f) return;
            }
            // The rod's center line (anchoredPosition.x) is compared with the visible goal's real width.
            if(Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                bool hit=Mathf.Abs(x-targetZone.anchoredPosition.x)<=targetZone.sizeDelta.x*.5f;
                if(hit)
                {
                    if(audio&&blip)audio.PlayOneShot(blip);
                    calibrationRound++;
                    if(calibrationRound>=CalibrationRoundCount)
                    {
                        quest=Quest.Grid2;ApplyLighting(1);
                        screen=Screen.Trace;ResetElements();ShowTrace();
                        return;
                    }
                    calibrationFeedback.text="校準成功！下一回合目標更窄、更難。";
                    calibrationFeedback.color=new Color(.30f,1f,.54f);
                    calibrationAdvanceAfterPause=true;
                    calibrationResumeAt=Time.unscaledTime+.45f;
                }
                else
                {
                    calibrationMisses++;
                    if(calibrationMisses>=CalibrationMaxMisses)
                    {
                        calibrationRound=0;
                        calibrationMisses=0;
                        calibrationFeedback.text="失誤達 3 次，從第一回合重新校準。";
                    }
                    else calibrationFeedback.text="時機偏差！請重新抓準綠色小格。";
                    calibrationFeedback.color=Red;
                    calibrationMissLabel.text="失誤  " + calibrationMisses + " / " + CalibrationMaxMisses;
                    calibrationAdvanceAfterPause=true;
                    calibrationResumeAt=Time.unscaledTime+.45f;
                }
            }
        }
        private void ShowTrace()
        {
            // R4 tube is generated from the shared collision spline, not from a reference photo.
            panelRect.sizeDelta = new Vector2(960f,740f);
            traceArea.anchoredPosition = Vector2.zero;
            header.text="電網②　工業光纖導通 · R4";
            stage.text="左側電力端口 → 右側端口";
            details.text="按住滑鼠左鍵沿發光電纜中央移動；碰壁就短路重來。";
            header.gameObject.SetActive(false);
            stage.gameObject.SetActive(false);
            details.gameObject.SetActive(false);
            feedback.transform.SetAsLastSibling();
            traceArea.gameObject.SetActive(true);
            if(r4TubeImage != null)
            {
                Canvas.ForceUpdateCanvases();
                Debug.Log("[SDG Story V2.6.2 R4] TRACE_SCREEN_OPEN sharedSpline=" +
                    smoothTrace.Length + " sprite=" + (r4TubeImage.sprite ? "READY" : "MISSING") +
                    " active=" + r4TubeImage.isActiveAndEnabled +
                    " canvasOrder=" + (r4TubeCanvas ? r4TubeCanvas.sortingOrder.ToString() : "NULL"));
            }
            marker.gameObject.SetActive(true);
            marker.anchoredPosition=smoothTrace[0];
            tracing=false;traceStepIndex=0;
            if(traceGraphic)traceGraphic.Progress=0f;
            if(traceProgressText)traceProgressText.text="左鍵按住綠色起點，沿中央導通　0%";
            if(shortCircuitFx)shortCircuitFx.ResetEffect();
            if(energyStream)energyStream.SetPlayerState(false,0f);
            earliestNextTrace=Time.unscaledTime;
            if(traceDebugOverlay)traceDebugOverlay.SetActive(false);
            quest=Quest.Grid2;RefreshTask();
        }
        private int ClosestTracePoint(Vector2 p, out float nearest)
        {
            int best=0; nearest=float.MaxValue;
            for(int i=0;i<smoothTrace.Length-1;i++)
            {
                float d=DistanceToSegment(p,smoothTrace[i],smoothTrace[i+1]);
                if(d<nearest){nearest=d;best=i;}
            }
            return best;
        }

        // A fixed +/-10 index rule breaks at high-density bends: index steps
        // have no consistent physical length. Search only the connected portion
        // of the visual centerline within a distance measured in UI units.
        private int ClosestConnectedTracePoint(Vector2 p,int anchor,float cursorStep,out float nearest)
        {
            anchor=Mathf.Clamp(anchor,0,smoothTrace.Length-2);
            float window=Mathf.Max(PathTolerance*3.0f,cursorStep*8f);
            int from=anchor;
            int last=smoothTrace.Length-2;
            while(from>0 && traceArcLengths[anchor]-traceArcLengths[from-1]<=window)from--;
            int to=anchor;
            while(to<last && traceArcLengths[to+1]-traceArcLengths[anchor]<=window)to++;
            int best=anchor;nearest=float.MaxValue;
            for(int i=from;i<=to;i++)
            {
                float d=DistanceToSegment(p,smoothTrace[i],smoothTrace[i+1]);
                if(d<nearest){nearest=d;best=i;}
            }
            return best;
        }

        private void TickTrace()
        {
            // F9 shows the SAME path used to render the actual tube and test collisions.
            if(Input.GetKeyDown(KeyCode.F9) && traceDebugOverlay)
                traceDebugOverlay.SetActive(!traceDebugOverlay.activeSelf);
            if(traceGraphic)traceGraphic.AdvancePulse(Time.unscaledTime);
            if(Time.unscaledTime<earliestNextTrace)return;
            Vector2 pos;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(traceArea,
                Input.mousePosition,null,out pos))return;
            if(tracing)marker.anchoredPosition=pos;
            if(!tracing)
            {
                // Start must be within the same tolerance used during tracing.
                if(Vector2.Distance(pos,smoothTrace[0])<=PathTolerance && Input.GetMouseButtonDown(0))
                {
                    tracing=true;traceStepIndex=0;lastTraceCursor=pos;
                    if(energyStream)energyStream.SetPlayerState(true,0f);
                    marker.anchoredPosition=pos;
                    feedback.text="電流導通中 · 維持按住左鍵";
                }
                return;
            }
            if(!Input.GetMouseButton(0))
            {
                // Releasing the mouse is not a wall collision: cancel without false sparks.
                tracing=false;traceStepIndex=0;
                if(energyStream)energyStream.SetPlayerState(false,0f);
                marker.anchoredPosition=smoothTrace[0];
                if(traceProgressText)traceProgressText.text="放開滑鼠，導通中斷（非短路）。請從左側重新開始。";
                earliestNextTrace=Time.unscaledTime+.25f;
                return;
            }
            // Preserve R2's sweep against cursor teleporting through a wall.
            // Replace density-dependent index jumps with connected arc-length search.
            int steps=Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(lastTraceCursor,pos)/7f),1,128);
            int localIndex=traceStepIndex;
            Vector2 previousSample=lastTraceCursor;
            for(int j=1;j<=steps;j++)
            {
                Vector2 sample=Vector2.Lerp(lastTraceCursor,pos,j/(float)steps);
                float distance;
                int idx=ClosestConnectedTracePoint(sample,localIndex,Vector2.Distance(previousSample,sample),out distance);
                if(distance>PathTolerance)
                {
                    float globalDistance;
                    int globalIndex=ClosestTracePoint(sample,out globalDistance);
                    string reason=globalDistance<=PathTolerance ? "DISCONNECTED_PATH" : "OFF_CENTERLINE";
                    Debug.LogWarning("[SDG Story V2.6.2 BendR3] TRACE_FAIL reason="+reason+
                        " cursor="+sample+" connectedDist="+distance.ToString("F1")+
                        " globalDist="+globalDistance.ToString("F1")+
                        " connectedIndex="+idx+" globalIndex="+globalIndex+
                        " tolerance="+PathTolerance.ToString("F1"));
                    FailTrace(reason=="DISCONNECTED_PATH" ?
                        "電流偏離目前管線段；按 F9 顯示判定中心線。" :
                        "超出判定中心線範圍；按 F9 檢查是否與圖片對齊。",sample);
                    return;
                }
                localIndex=Mathf.Max(localIndex,idx);
                previousSample=sample;
            }
            traceStepIndex=localIndex;
            lastTraceCursor=pos;
            float ratio=traceArcLengths[traceStepIndex]/traceArcLengths[traceArcLengths.Length-1];
            if(energyStream)energyStream.SetPlayerState(true,ratio);
            if(traceGraphic)traceGraphic.Progress=ratio;
            if(traceProgressText)traceProgressText.text="導通進度　"+Mathf.RoundToInt(ratio*100f)+"%";
            if(traceStepIndex>=smoothTrace.Length-8 && Vector2.Distance(pos,smoothTrace[smoothTrace.Length-1])<22f)
            {
                tracing=false;
                quest=Quest.Grid3;ApplyLighting(2);
                screen=Screen.Allocation;ResetElements();ShowAllocation();
            }
        }
        private void FailTrace(string message,Vector2 at)
        {
            tracing=false;traceStepIndex=0;
            if(energyStream)energyStream.SetPlayerState(false,0f);
            earliestNextTrace=Time.unscaledTime+.7f;
            feedback.text=message;
            if(audio&&blip)audio.PlayOneShot(blip);
            if(traceGraphic)traceGraphic.Progress=0f;
            if(traceProgressText)traceProgressText.text="短路："+message+"　請從左側重新開始。";
            if(shortCircuitFx)shortCircuitFx.Emit(at);
            marker.anchoredPosition=smoothTrace[0];
        }
        private static float DistanceToSegment(Vector2 p,Vector2 a,Vector2 b)
        {
            Vector2 v=b-a;
            float t=Mathf.Clamp01(Vector2.Dot(p-a,v)/Mathf.Max(.0001f,v.sqrMagnitude));
            return Vector2.Distance(p,a+v*t);
        }
        // R5: allocation is now a playable recycling-to-energy challenge.
        // The sorting component owns live drag targets, countdown, energy and load switches.
        private void ShowAllocation()
        {
            panelRect.sizeDelta=new Vector2(1480f,834f);
            header.gameObject.SetActive(false);
            stage.gameObject.SetActive(false);
            details.gameObject.SetActive(false);
            feedback.gameObject.SetActive(false);
            if(!sortingEnergy)
                throw new InvalidOperationException("R5 sorting energy game was not constructed.");
            sortingEnergy.Begin();
            if(task)task.gameObject.SetActive(false);
            quest=Quest.Grid3;RefreshTask();
        }
        private void CompleteSortingEnergy()
        {
            // Only the live sorting game's checked supply path may reach this callback.
            if(screen!=Screen.Allocation || gridComplete)return;
            gridComplete=true;
            ApplyLighting(3);
            quest=Quest.Production;
            ClosePanel();
            Toast("垃圾分類發電成功！電網③ 已供電。返回資源處理控制台啟動產線，再取得三種組裝材料。",8f);
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
            assemblyStage=0;
            assemblyOpen=true;
            if(assemblyPreview)assemblyPreview.ResetParts();
            OpenPanel(Screen.Assembly);
            return true;
        }
        private void ShowAssembly()
        {
            header.text="雷射槍 · 真正的 3D 拖曳組裝 V2.3";
            stage.text=assemblyStage<3
                ? "第 "+(assemblyStage+1)+" / 3 件："+AssemblySteps[assemblyStage]
                : "槍身、電路與能源核心已完成安裝";
            details.text=assemblyStage<3
                ? "在下方 3D 預覽裡抓取左側模型，拖進右側跳動的綠色球形插槽；不再拖曳文字卡片。"
                : "三件真正的 3D 模組都已放到正確位置。請領取原有背包中的循環量子投射器。";
            if(assemblyPreview)
            {
                assemblyPreview.Initialize(panelRect);
                assemblyPreview.SetStage(assemblyStage);
                assemblyPreview.SetVisible(true);
            }
            if(assemblyInstruction)
            {
                assemblyInstruction.gameObject.SetActive(true);
                assemblyInstruction.text=assemblyStage<3
                    ? "拖動 3D 零件 → 綠色球形插槽（鬆開滑鼠完成安裝）"
                    : "實體 3D 組裝完成，點擊下方領取雷射槍";
            }
            // Make instructions and completion button visible above the viewport.
            if(assemblyInstruction)assemblyInstruction.transform.SetAsLastSibling();
            if(assemblyStage>=3)
            {
                SetAction("完成組裝 · 領取真正的雷射槍",FinishAssembly);
                actionButton.transform.SetAsLastSibling();
            }
            else actionButton.gameObject.SetActive(false);
        }
        private void OnReal3DPartSnapped(int partId)
        {
            if(screen!=Screen.Assembly || partId!=assemblyStage)return;
            assemblyStage++;
            if(audio && blip)audio.PlayOneShot(blip);
            feedback.text="3D 零件已裝入實體槍座！";
            ShowAssembly();
        }
        private void FinishAssembly()
        {
            assemblyAuthorized=true;
            ClosePanel();
            EcoAdventureRuntime runtime=EcoAdventureRuntime.Instance;
            bool ok=runtime!=null && runtime.CraftWeapon();
            assemblyAuthorized=false;
            assemblyOpen=false;
            if(ok) {Toast("雷射槍已入背包！三個半空中的靶標正在自動部署，不用按 F8。",7f);CheckProductionAndWeapon();}
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
                rangePlaced=false;
                for(int i=0;i<TargetCount;i++)targetCompleted[i]=false;
                RefreshTask();
                if(targetStatus)targetStatus.transform.parent.gameObject.SetActive(true);
                DeployAllFloatingTargets();
            }
            else
            {
                quest=Quest.Production;
                RefreshTask();
            }
        }
        private Material TargetMaterial(Color color)
        {
            Shader shader=Shader.Find("Unlit/Color");
            if(!shader)shader=Shader.Find("Standard");
            Material m=new Material(shader);m.color=color;
            if(m.HasProperty("_EmissionColor"))
            {m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.7f);}
            targetMaterials.Add(m);return m;
        }
        private GameObject TargetPart(PrimitiveType type,string name,Transform parent,Vector3 pos,Vector3 size,Material mat)
        {
            GameObject go=GameObject.CreatePrimitive(type);go.name=name;
            go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;
            Collider col=go.GetComponent<Collider>();if(col)Destroy(col);
            Renderer rend=go.GetComponent<Renderer>();if(rend)rend.sharedMaterial=mat;
            return go;
        }
        private void TargetRing(Transform root,string name,float radius,float width,Material mat)
        {
            GameObject ring=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            ring.transform.SetParent(root,false);
            const int segments=40;
            Vector3[] vertices=new Vector3[segments*4];int[] tris=new int[segments*6];
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2f/segments,b=(i+1)*Mathf.PI*2f/segments;
                Vector3 dirA=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0),dirB=new Vector3(Mathf.Cos(b),Mathf.Sin(b),0);
                int o=i*4;
                vertices[o]=dirA*(radius-width);vertices[o+1]=dirA*radius;
                vertices[o+2]=dirB*radius;vertices[o+3]=dirB*(radius-width);
                int t=i*6;tris[t]=o;tris[t+1]=o+1;tris[t+2]=o+2;
                tris[t+3]=o;tris[t+4]=o+2;tris[t+5]=o+3;
            }
            Mesh mesh=new Mesh();mesh.name="StoryV2_FloatingRing";mesh.vertices=vertices;mesh.triangles=tris;mesh.RecalculateNormals();
            ring.GetComponent<MeshFilter>().sharedMesh=mesh;
            ring.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
        private bool IsObstructionFree(Vector3 start,Vector3 finish,CharacterController player)
        {
            Vector3 direction=finish-start;
            float distance=direction.magnitude;
            if(distance<.05f)return false;
            foreach(Collider nearby in Physics.OverlapSphere(finish,.40f,~0,QueryTriggerInteraction.Ignore))
            {
                if(!nearby)continue;
                if(player && nearby.transform.IsChildOf(player.transform))continue;
                return false;
            }
            RaycastHit[] hits=Physics.RaycastAll(start,direction/distance,distance-.15f,~0,QueryTriggerInteraction.Ignore);
            foreach(RaycastHit h in hits)
            {
                if(!h.collider)continue;
                if(player && h.collider.transform.IsChildOf(player.transform))continue;
                // Previously deployed training targets should not block a redeployment.
                bool own=false;
                for(int i=0;i<TargetCount;i++)
                    if(targetObjects[i] && h.collider.transform.IsChildOf(targetObjects[i].transform))own=true;
                if(!own)return false;
            }
            return true;
        }
        private void DeployAllFloatingTargets()
        {
            if(quest!=Quest.Range || rangePlaced)return;
            if(!viewCamera){if(targetStatus)targetStatus.text="正在尋找玩家攝影機，準備自動開始射擊測試…";return;}
            Transform camTransform=viewCamera.transform;
            CharacterController player=camTransform.GetComponentInParent<CharacterController>();
            Vector3 forward=Vector3.ProjectOnPlane(camTransform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.01f)forward=camTransform.forward;
            Vector3 origin=camTransform.position;
            Vector3 chosenForward=forward;
            Vector3[] candidatePositions=new Vector3[TargetCount];
            bool found=false;
            // Find a genuinely visible, unobstructed direction rather than refusing
            // to spawn once because a wall happens to face the workbench.
            float[] yaw={0f,-18f,18f,-38f,38f,-60f,60f};
            float[] distances={5.2f,4.0f,3.1f};
            for(int a=0;a<yaw.Length&&!found;a++)
            for(int d=0;d<distances.Length&&!found;d++)
            {
                Vector3 dir=Quaternion.AngleAxis(yaw[a],Vector3.up)*forward;
                Vector3 right=Vector3.Cross(Vector3.up,dir).normalized;
                Vector3 center=origin+dir*distances[d]+Vector3.up*.45f;
                candidatePositions[0]=center-right*1.42f+Vector3.up*.10f;
                candidatePositions[1]=center+Vector3.up*.77f;
                candidatePositions[2]=center+right*1.42f+Vector3.up*.24f;
                bool clear=true;
                for(int i=0;i<TargetCount;i++)
                    if(!IsObstructionFree(origin,candidatePositions[i],player))clear=false;
                if(clear){found=true;chosenForward=dir;}
            }
            if(!found)
            {
                if(targetStatus)targetStatus.text="射擊訓練會自動開始：請離開貼牆的位置，走到較寬敞的走道。無須按 F8。";
                return;
            }
            // All THREE real 3D targets appear in the air at once.
            for(int i=0;i<TargetCount;i++)
            {
                if(targetObjects[i])Destroy(targetObjects[i]);
                targetHomes[i]=candidatePositions[i];
                targetDirections[i]=Vector3.Cross(Vector3.up,chosenForward).normalized;
                GameObject target=new GameObject("V23_AUTO_AIRBORNE_LASER_TARGET_"+(i+1));
                if(target.scene!=gameObject.scene)SceneManager.MoveGameObjectToScene(target,gameObject.scene);
                target.transform.position=candidatePositions[i];
                target.transform.rotation=Quaternion.LookRotation(-chosenForward,Vector3.up);
                Material active=TargetMaterial(i==2?Lime:(i==1?new Color(.15f,.68f,1f):Teal));
                Material white=TargetMaterial(new Color(.89f,.98f,1f));
                TargetRing(target.transform,"OUTER floating circle",.84f,.12f,active);
                TargetRing(target.transform,"INNER visible crosshair ring",.55f,.07f,white);
                TargetRing(target.transform,"CENTER energy ring",.32f,.045f,active);
                GameObject core=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                core.name="V23 SHOOT THE CORE WITH REAL QUANTUM RIFLE";
                core.transform.SetParent(target.transform,false);
                core.transform.localScale=Vector3.one*.76f;
                Renderer rr=core.GetComponent<Renderer>();if(rr)rr.sharedMaterial=active;
                SDGLevel03PracticeTarget hit=target.AddComponent<SDGLevel03PracticeTarget>();
                hit.Owner=this;
                targetObjects[i]=target;targetScripts[i]=hit;
            }
            rangeStarted=Time.unscaledTime;
            rangePlaced=true;
            if(targetStatus)targetStatus.text="三個懸浮靶已自動出現！瞄準空中發光圓環的中心射擊：0 / 3（不用 F8）";
            Debug.Log("[SDG Story V2.3] AUTO RANGE DEPLOYED: three airborne targets at "+
                candidatePositions[0]+" | "+candidatePositions[1]+" | "+candidatePositions[2]);
            RefreshTask();
        }
        private void UpdateTarget()
        {
            if(targetStatus)targetStatus.transform.parent.gameObject.SetActive(true);
            if(!rangePlaced)return;
            float elapsed=Time.unscaledTime-rangeStarted;
            for(int i=0;i<TargetCount;i++)
            {
                GameObject target=targetObjects[i];
                if(!target || targetCompleted[i])continue;
                float bob=Mathf.Sin(elapsed*2.0f+i*1.3f)*.14f;
                Vector3 shift=Vector3.up*bob;
                if(i==1)shift+=targetDirections[i]*Mathf.Sin(elapsed*1.5f)*.42f;
                target.transform.position=targetHomes[i]+shift;
                if(i==2)
                {
                    bool visible=Mathf.Repeat(elapsed,4f)<2.8f;
                    if(target.activeSelf!=visible)target.SetActive(visible);
                }
            }
        }
        public void TargetHit(SDGLevel03PracticeTarget target)
        {
            if(quest!=Quest.Range || !rangePlaced || target==null ||
                EcoAdventureRuntime.Instance==null || !EcoAdventureRuntime.Instance.IsQuantumWeaponEquipped)return;
            int idx=-1;
            for(int i=0;i<TargetCount;i++)if(targetScripts[i]==target){idx=i;break;}
            if(idx<0 || targetCompleted[idx])return;
            targetCompleted[idx]=true;completedTargets++;
            if(targetObjects[idx]){Destroy(targetObjects[idx]);targetObjects[idx]=null;}
            if(targetStatus)targetStatus.text="真實雷射槍命中懸浮靶："+completedTargets+" / 3";
            if(completedTargets>=TargetCount)
            {
                quest=Quest.Exit;rangePlaced=false;
                if(targetStatus)targetStatus.transform.parent.gameObject.SetActive(false);
                Toast("三座空中靶全數通過（3/3）！D 區出口正式解鎖。",8f);
            }
            else Toast("已擊中懸浮靶 "+completedTargets+"/3，繼續擊中剩餘空中靶。",3f);
            RefreshTask();
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
            if(transientFeedback.Length>0){task.text="[V2.3] "+transientFeedback;return;}
            switch(quest)
            {
                case Quest.Explore: task.text="第三關 · 黑暗工廠｜手電筒已啟動。追蹤紅色故障燈，收集三個維修零件並完成控制台解謎。";break;
                case Quest.Grid1:task.text="能源異常！自動啟動電網① 電壓校準。";break;
                case Quest.Grid2:task.text="電網① 完成｜電網② 使用滑鼠沿曲線管道導通。";break;
                case Quest.Grid3:task.text="電網② 完成｜電網③ 拖曳垃圾分類發電，維持必要負載並避免超載。";break;
                case Quest.Production:task.text="電網 3/3 完成｜回控制台啟動回收產線，將九件原料加工為三份再生資源；找齊金屬、塑膠和電路板，到工作台按 F 組裝雷射槍。";break;
                case Quest.Weapon:task.text="到組裝台完成循環量子投射器製作。";break;
                case Quest.Range:task.text="V2.3 自動射擊測試｜空中懸浮靶 "+completedTargets+"/3。組裝完成就自動出靶，選擇雷射槍快捷欄後射擊發光中心；無須 F8。";break;
                default:task.text="第三關完成條件已達成！前往 D 區出口，進入第四關。";break;
            }
            task.text="[V2.6.2] "+task.text;
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
            Image hud=CreateImage("V2.3 Objective background",root,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(1140,76),new Vector2(0,-20),Navy);
            hud.raycastTarget=false;
            task=Label("Story quest HUD",root,"",21,Teal,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(1114,70),new Vector2(0,-23));
            Image main=CreateImage("Mission Panel",root,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(850,620),Vector2.zero,Navy);
            panel=main.gameObject;panelRect=main.rectTransform;
            header=Label("Title",panelRect,"",33,Teal,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(810,62),new Vector2(0,-49));
            stage=Label("Stage",panelRect,"",23,Color.white,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(810,46),new Vector2(0,-113));
            details=Label("Description",panelRect,"",19,Teal,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(810,62),new Vector2(0,-171));
            feedback=Label("Warning",panelRect,"",21,Red,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(810,65),new Vector2(0,87));
            traceArea=CreateImage("R4 Generated industrial power panel",panelRect,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(900,675),Vector2.zero,
                new Color(.018f,.036f,.052f)).rectTransform;
            traceArea.GetComponent<Image>().raycastTarget=false;
            // Keep the proven overlay Canvas / uGUI Image rendering path used by R3.
            r4TubeCanvas=traceArea.gameObject.AddComponent<Canvas>();
            r4TubeCanvas.overrideSorting=true;
            r4TubeCanvas.sortingOrder=1100;
            smoothTrace=BuildR4Route();
            traceArcLengths=new float[smoothTrace.Length];
            for(int i=1;i<smoothTrace.Length;i++)
                traceArcLengths[i]=traceArcLengths[i-1]+Vector2.Distance(smoothTrace[i-1],smoothTrace[i]);

            BuildR4IndustrialFrame();
            // This texture contains ONLY the tube. No electricity or red sparks are baked in.
            // The same smoothTrace array is used by collision and animated energy sprites.
            r4TubeTexture=SDGLevel03V4TubeTexture.Build(smoothTrace,900,675);
            r4TubeSprite=Sprite.Create(r4TubeTexture,
                new Rect(0,0,900,675),new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect);
            r4TubeSprite.name="R4_Real_Tube_From_Collision_Path";
            r4TubeImage=CreateImage("R4 actual tube - same path as collision",traceArea,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(900,675),Vector2.zero,Color.white);
            r4TubeImage.sprite=r4TubeSprite;
            r4TubeImage.type=Image.Type.Simple;
            r4TubeImage.preserveAspect=false;
            r4TubeImage.raycastTarget=false;
            BuildR4PipeClamps();
            energyStream=SDGLevel03V4EnergyStream.Create(traceArea,smoothTrace,traceArcLengths);

            // F9 is an optional visual check. The green guide and drawn tube use same points.
            traceDebugOverlay=new GameObject("R4 exact shared collision centerline",typeof(RectTransform));
            traceDebugOverlay.transform.SetParent(traceArea,false);
            RectTransform debugRect=traceDebugOverlay.GetComponent<RectTransform>();
            debugRect.anchorMin=debugRect.anchorMax=new Vector2(.5f,.5f);
            debugRect.pivot=new Vector2(.5f,.5f);
            debugRect.sizeDelta=new Vector2(900f,675f);
            debugRect.anchoredPosition=Vector2.zero;
            for(int i=0;i<smoothTrace.Length-1;i++)
                Segment("Exact shared path",debugRect,smoothTrace[i],smoothTrace[i+1],2.3f,
                    new Color(.05f,1f,.1f,.75f));
            Label("R4 debug guide",debugRect,"F9 判定中心線 = 真實可見管道中心",16,
                new Color(.18f,1f,.35f),new Vector2(.5f,1f),new Vector2(.5f,1f),
                new Vector2(710,30),new Vector2(0,-88));
            traceDebugOverlay.SetActive(false);
            BuildR4Port("左側供電端",smoothTrace[0],new Color(.18f,1f,.5f));
            BuildR4Port("右側接收端",smoothTrace[smoothTrace.Length-1],new Color(1f,.78f,.21f));
            marker=Dot("R4 current following mouse",traceArea,smoothTrace[0],12f,Color.white).rectTransform;
            marker.gameObject.SetActive(false);
            // Dynamic burst is generated ONLY on collision, never permanently pictured.
            shortCircuitFx=SDGLevel03V262EmbeddedSparks.Create(traceArea);
            traceProgressText=Label("R4 progress",traceArea,"左鍵按住綠色起點，沿發光管內前進　0%",20,Teal,
                new Vector2(.5f,0f),new Vector2(.5f,0f),new Vector2(820,36),new Vector2(0,41));
            Label("R4 trace instructions",traceArea,
                "左側啟動｜沿管內移動滑鼠｜F9 顯示真實判定中心線｜右側完成",17,Color.white,
                new Vector2(.5f,0f),new Vector2(.5f,0f),new Vector2(840,32),new Vector2(0,77));
            Label("R4 active render",traceArea,"[R4] REAL PIPE / LIVE ENERGY",16,
                new Color(.52f,.94f,1f),new Vector2(0f,1f),new Vector2(0f,1f),
                new Vector2(370,28),new Vector2(26,-28));
            // Actual 3D assembly preview: the player must drag 3 parts into sockets.
            assemblyPreview=gameObject.AddComponent<SDGLevel03AssemblyPreview>();
            assemblyPreview.OnPartSnapped=OnReal3DPartSnapped;
            assemblyInstruction=Label("Assembly drag guide",panelRect,"",20,Lime,
                new Vector2(.5f,0f),new Vector2(.5f,0f),new Vector2(760,43),new Vector2(0,152));
            // V2.3 uses the actual 3D mesh viewport, NOT draggable text cards.
            assemblyInstruction.gameObject.SetActive(false);
            Image targetBanner=CreateImage("Floating target mission aid",root,new Vector2(.5f,0f),new Vector2(.5f,0f),
                new Vector2(860,61),new Vector2(0,91),new Color(.025f,.13f,.17f,.94f));
            targetBanner.raycastTarget=false;
            targetStatus=Label("Visible target deployment status",targetBanner.rectTransform,"",23,Lime,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(840,55),Vector2.zero);
            targetBanner.gameObject.SetActive(false);
            // R5 removes the old four text-toggle allocation controls.
            Button button=MakeButton("Action",panelRect,new Vector2(390,53),new Vector2(0,-245),"",()=>{});
            actionButton=button;action=button.GetComponentInChildren<Text>();
            BuildCalibrationUI();
            // Built once and kept as a separate real minigame; earlier stages unchanged.
            GameObject sortingObject=new GameObject("R5 LIVE Waste Sorting Power Game",
                typeof(RectTransform),typeof(SDGLevel03R5SortingEnergyGame));
            sortingObject.transform.SetParent(panelRect,false);
            sortingEnergy=sortingObject.GetComponent<SDGLevel03R5SortingEnergyGame>();
            sortingEnergy.Setup(panelRect,CompleteSortingEnergy);
            panel.SetActive(false);
        }
        // R4.2: imported image is a DECORATIVE SKIN ONLY, not an interactive screenshot.
        // The reference arrow, wide green box and stat values were erased from that skin.
        // Pointer, target, 3-round markers, statistics and hit detection are live uGUI elements.
        private static Sprite R42CreateSprite(string resource)
        {
            Texture2D tex=Resources.Load<Texture2D>(resource);
            if(!tex)
            {
                Debug.LogError("[SDG R4.2] MISSING DECORATIVE ASSET: Resources/"+resource);
                return null;
            }
            return Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),
                new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect);
        }

        private void BuildCalibrationUI()
        {
            // 1480 x 834 matches the supplied widescreen reference proportions.
            // All controls remain independent Unity uGUI objects.
            Image skin=CreateImage("R4.2 Reference-style decorative machine bezel",panelRect,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(1480f,834f),Vector2.zero,Color.white);
            calibrationRoot=skin.gameObject;
            RectTransform root=skin.rectTransform;
            // R4.2 uses the same decorative HUD under a different Resources key.
            // Prefer R4.3, but fall back without logging a false missing-resource error.
            string hudResource=Resources.Load<Texture2D>("SDG_R43_ReactionPanel")
                ? "SDG_R43_ReactionPanel" : "SDG_R42_ReactionPanel";
            r42PanelSprite=R42CreateSprite(hudResource);
            if(r42PanelSprite) skin.sprite=r42PanelSprite;
            else skin.color=new Color(.014f,.054f,.076f,1f);
            skin.raycastTarget=false;

            // This invisible layout layer aligns live UI objects to the rail in the backdrop.
            Image trackImage=CreateImage("R4.2 REAL hit region / moving control rail",root,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(RailWidth,130f),new Vector2(0,27f),new Color(0,0,0,0));
            trackImage.raycastTarget=false;
            RectTransform track=trackImage.rectTransform;

            // Green goal is constructed from separate UI components, NOT baked into the skin.
            Image goal=CreateImage("R4.2 Live target: visible rect equals hit rect",track,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(CalibrationWidths[0],104f),new Vector2(CalibrationCenters[0],0f),
                new Color(0,0,0,0));
            goal.raycastTarget=false;
            targetZone=goal.rectTransform;
            Image halo=CreateImage("R4.2 Green pulse halo",targetZone,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(4f,115f),Vector2.zero,
                new Color(.20f,1f,.14f,.18f));
            halo.rectTransform.anchorMin=Vector2.zero;
            halo.rectTransform.anchorMax=Vector2.one;
            halo.rectTransform.sizeDelta=new Vector2(18f,20f);
            halo.raycastTarget=false;
            Image core=CreateImage("R4.2 Target cell animated by time-of-click logic",targetZone,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero,
                new Color(.12f,.94f,.18f,.82f));
            core.rectTransform.anchorMin=Vector2.zero;
            core.rectTransform.anchorMax=Vector2.one;
            core.rectTransform.sizeDelta=Vector2.zero;
            core.raycastTarget=false;
            // Relative x-anchors keep the diagonal texture INSIDE narrow goals.
            for(int i=1;i<=4;i++)
            {
                float x=i/5f;
                Image stripe=CreateImage("R4.2 diagonal goal stripe "+i,targetZone,
                    new Vector2(x,.5f),new Vector2(.5f,.5f),new Vector2(4,63),Vector2.zero,
                    new Color(.74f,1f,.54f,.28f));
                stripe.rectTransform.localRotation=Quaternion.Euler(0,0,-22f);
                stripe.raycastTarget=false;
            }
            for(int j=0;j<2;j++)
            {
                float anchor=j==0?0f:1f;
                Image bracket=CreateImage("R4.2 green bracket "+j,targetZone,
                    new Vector2(anchor,.5f),new Vector2(.5f,.5f),new Vector2(4,117),Vector2.zero,
                    new Color(.69f,1f,.45f,.96f));
                bracket.raycastTarget=false;
                for(int k=0;k<2;k++)
                {
                    float sy=k==0?1f:-1f;
                    Image bar=CreateImage("R4.2 corner lip "+j+" "+k,targetZone,
                        new Vector2(anchor,.5f),new Vector2(.5f,.5f),new Vector2(13,4),
                        new Vector2(j==0?6f:-6f,sy*56f),new Color(.84f,1f,.77f));
                    bar.raycastTarget=false;
                }
            }
            // R4.3: use a luminous vertical rod instead of an arrow.
            // The rod's CENTER line is the actual timing coordinate.
            Image livePointer=CreateImage("R4.3 real moving cyan rod",track,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(26f,106f),
                new Vector2(-RailWidth*.5f,0f),new Color(.82f,.98f,1f,.97f));
            needle=livePointer.rectTransform;
            livePointer.raycastTarget=false;
            Image rodGlow=CreateImage("R4.3 cyan rod glow",needle,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(44f,122f),Vector2.zero,
                new Color(.18f,.92f,1f,.24f));
            rodGlow.raycastTarget=false;
            Image rodCore=CreateImage("R4.3 cyan rod core",needle,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(14f,94f),Vector2.zero,
                new Color(.86f,1f,1f,.98f));
            rodCore.raycastTarget=false;
            for(int cap=0;cap<2;cap++)
            {
                float y=cap==0?46f:-46f;
                Image tip=CreateImage("R4.3 rod cap "+cap,needle,
                    new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(28f,8f),
                    new Vector2(0f,y),new Color(.62f,.96f,1f,.95f));
                tip.raycastTarget=false;
            }

            // These statistics are live and overwrite the old static values.
            calibrationRoundLabel=Label("R4.2 Dynamic round count",root,"",30,Color.white,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(336,55),
                new Vector2(-440,-199));
            for(int i=0;i<CalibrationRoundCount;i++)
            {
                calibrationRoundBlocks[i]=CreateImage("R4.2 live round tick "+i,root,
                    new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(38,33),
                    new Vector2(-513+i*69,-257),new Color(.12f,.28f,.36f));
                calibrationRoundBlocks[i].raycastTarget=false;
            }
            calibrationAccuracyLabel=Label("R4.2 REAL precision window",root,"",31,
                new Color(.61f,.95f,1f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(315,100),new Vector2(440,-219));
            calibrationMissLabel=Label("R4.2 dynamic misses",root,"",19,
                new Color(1f,.77f,.40f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(300,30),new Vector2(440,-276));
            calibrationFeedback=Label("R4.2 challenge feedback",root,"",18,
                new Color(.48f,.93f,1f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(730,35),new Vector2(0,-355));

            // Reference decorative X and '?' must not look like non-functional controls.
            Image closeSurface=CreateImage("R4.2 functional close hit target",root,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(72,69),
                new Vector2(644,331),new Color(1f,1f,1f,0f));
            closeSurface.raycastTarget=true;
            closeSurface.gameObject.AddComponent<Button>().onClick.AddListener(ClosePanel);
            Image helpSurface=CreateImage("R4.2 functional help hit target",root,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(70,66),
                new Vector2(574,331),new Color(1f,1f,1f,0f));
            helpSurface.raycastTarget=true;
            helpSurface.gameObject.AddComponent<Button>().onClick.AddListener(()=>
            {
                calibrationFeedback.text="移動桿子的中心線進入綠色小格時按左鍵，3 回合成功才過關。";
                calibrationFeedback.color=new Color(.6f,1f,1f);
            });
            calibrationRoot.SetActive(false);
        }
        private void BuildR4IndustrialFrame()
        {
            // The industrial enclosure is separate from the drawn tube, and does not
            // contain any printed cable or fixed short-circuit effects.
            RectTransform plate=CreateImage("R4 removable steel backing",traceArea,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(860,633),Vector2.zero,
                new Color(.026f,.062f,.079f)).rectTransform;
            CreateImage("R4 inner shadow",plate,new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(830,585),Vector2.zero,new Color(.010f,.024f,.038f));
            Color steel=new Color(.13f,.20f,.24f);
            Color accent=new Color(.22f,.33f,.38f);
            CreateImage("Top steel beam",traceArea,new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(880,29),new Vector2(0,313),steel);
            CreateImage("Bottom steel beam",traceArea,new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(880,29),new Vector2(0,-313),steel);
            CreateImage("Left steel column",traceArea,new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(22,631),new Vector2(-426,0),steel);
            CreateImage("Right steel column",traceArea,new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(22,631),new Vector2(426,0),steel);
            CreateImage("Top metal shine",traceArea,new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(854,3),new Vector2(0,326),accent);
            CreateImage("Bottom metal shine",traceArea,new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(854,3),new Vector2(0,-301),accent);
            // Safety stripes are actual UI rectangles; no reference image required.
            for(int i=0;i<13;i++)
            {
                float x=-198f+i*33f;
                Segment("Upper hazard stripe",traceArea,new Vector2(x,316),
                    new Vector2(x+15,326),5f,new Color(.85f,.59f,.12f,.9f));
                Segment("Lower hazard stripe",traceArea,new Vector2(x,-327),
                    new Vector2(x+15,-316),5f,new Color(.85f,.59f,.12f,.9f));
            }
            for(int i=0;i<7;i++)
            {
                float x=-383f+i*128f;
                Dot("Upper rivet",traceArea,new Vector2(x,311),8f,new Color(.41f,.49f,.52f));
                Dot("Lower rivet",traceArea,new Vector2(x,-311),8f,new Color(.41f,.49f,.52f));
            }
            for(int i=0;i<4;i++)
            {
                float y=-220f+i*145f;
                Dot("Left rivet",traceArea,new Vector2(-426,y),8f,new Color(.41f,.49f,.52f));
                Dot("Right rivet",traceArea,new Vector2(426,y),8f,new Color(.41f,.49f,.52f));
            }
            CreateImage("Diagnostic strip",traceArea,new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(640,2),new Vector2(0,-233),new Color(.10f,.28f,.34f,.6f));
        }
        private void BuildR4PipeClamps()
        {
            for(int i=22;i<smoothTrace.Length-8;i+=43)
            {
                Vector2 p=smoothTrace[i];
                Vector2 dir=(smoothTrace[i+1]-smoothTrace[i-1]).normalized;
                Image metal=CreateImage("R4 tube bracket",traceArea,new Vector2(.5f,.5f),
                    new Vector2(.5f,.5f),new Vector2(10,57),p,new Color(.20f,.30f,.35f,.88f));
                metal.rectTransform.localRotation=Quaternion.Euler(0,0,
                    Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg);
                metal.raycastTarget=false;
                Dot("Bracket bolt",metal.rectTransform,new Vector2(0,0),6f,
                    new Color(.70f,.78f,.82f));
            }
        }
        private void BuildR4Port(string portName,Vector2 position,Color core)
        {
            Image mount=CreateImage(portName+" steel housing",traceArea,new Vector2(.5f,.5f),
                new Vector2(.5f,.5f),new Vector2(58,86),position,
                new Color(.13f,.22f,.27f));
            mount.raycastTarget=false;
            Dot(portName+" cyan insulation",traceArea,position,45f,
                new Color(.04f,.62f,.77f));
            Dot(portName+" dark socket",traceArea,position,32f,
                new Color(.015f,.08f,.12f));
            Dot(portName+" energized center",traceArea,position,17f,core);
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


// Compiled in the same C# compilation unit as the controller to avoid the CS0246
// caused by a missing/unresolvable external ShortCircuitFX type.
namespace SDGAdventure.Level03StoryV2
{
// UI sparks are intentionally built from standard Image/RawImage objects.
    // They remain visible in ScreenSpaceOverlay even when the factory is completely dark.
    // All 26 rays are pooled: failed attempts cause no GameObject allocation.
    public sealed class SDGLevel03V262EmbeddedSparks : MonoBehaviour
    {
        const int Count = 26;
        readonly RectTransform[] rays = new RectTransform[Count];
        readonly Image[] rayImages = new Image[Count];
        readonly Vector2[] velocities = new Vector2[Count];
        readonly Vector2[] locations = new Vector2[Count];
        readonly float[] spans = new float[Count];
        RawImage glow;
        Texture2D glowTexture;
        RectTransform glowRect;
        float startedAt = -10f;
        Vector2 origin;
        bool playing;

        public static SDGLevel03V262EmbeddedSparks Create(RectTransform parent)
        {
            GameObject root = new GameObject("V2.6.2 SHORT CIRCUIT - pooled electric sparks", typeof(RectTransform), typeof(SDGLevel03V262EmbeddedSparks));
            root.transform.SetParent(parent, false);
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = parent.rect.size;
            SDGLevel03V262EmbeddedSparks fx = root.GetComponent<SDGLevel03V262EmbeddedSparks>();
            fx.Build();
            return fx;
        }

        void Build()
        {
            GameObject flashObject = new GameObject("ShortCircuitRadialFlash", typeof(RectTransform), typeof(RawImage));
            flashObject.transform.SetParent(transform, false);
            glowRect = flashObject.GetComponent<RectTransform>();
            glowRect.anchorMin = glowRect.anchorMax = new Vector2(.5f, .5f);
            glowRect.sizeDelta = new Vector2(112f, 112f);
            glowRect.pivot = new Vector2(.5f, .5f);
            glow = flashObject.GetComponent<RawImage>();
            glow.raycastTarget = false;
            glowTexture = MakeGlow();
            glow.texture = glowTexture;
            glow.gameObject.SetActive(false);

            for (int i = 0; i < Count; i++)
            {
                GameObject child = new GameObject("Spark streak " + i, typeof(RectTransform), typeof(Image));
                child.transform.SetParent(transform, false);
                RectTransform rect = child.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(16f, 2.5f);
                rays[i] = rect;
                rayImages[i] = child.GetComponent<Image>();
                rayImages[i].raycastTarget = false;
                child.SetActive(false);
            }
        }

        static Texture2D MakeGlow()
        {
            const int n = 64;
            Color32[] data = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - 31.5f) / 31.5f;
                    float dy = (y - 31.5f) / 31.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 2f);
                    data[y * n + x] = new Color32(255, 113, 28, (byte)Mathf.RoundToInt(alpha * 235f));
                }
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.name = "SDG_Level03_V24_ShortCircuitGlow";
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            t.SetPixels32(data);
            t.Apply(false, true);
            return t;
        }

        public void ResetEffect()
        {
            playing = false;
            if (glow) glow.gameObject.SetActive(false);
            for (int i = 0; i < Count; i++) if (rays[i]) rays[i].gameObject.SetActive(false);
        }

        public void Emit(Vector2 at)
        {
            origin = at;
            startedAt = Time.unscaledTime;
            playing = true;
            if (glow)
            {
                glowRect.anchoredPosition = origin;
                glowRect.localScale = Vector3.one;
                glow.color = Color.white;
                glow.gameObject.SetActive(true);
            }
            for (int i = 0; i < Count; i++)
            {
                // Stable fan-like burst with small deterministic variation.
                float angle = i * Mathf.PI * 2f / Count + Mathf.Sin(i * 3.73f) * .14f;
                float speed = 160f + (i * 29 % 167);
                velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                locations[i] = origin;
                spans[i] = 11f + (i % 5) * 3f;
                rays[i].anchoredPosition = origin;
                rays[i].sizeDelta = new Vector2(spans[i], i % 3 == 0 ? 3.8f : 2.3f);
                rays[i].localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
                rayImages[i].color = i % 4 == 0 ? Color.white : (i % 2 == 0 ? new Color(1f, .93f, .24f) : new Color(1f, .42f, .12f));
                rays[i].gameObject.SetActive(true);
            }
        }

        void Update()
        {
            if (!playing) return;
            float age = Time.unscaledTime - startedAt;
            if (age >= .90f) { ResetEffect(); return; }
            float fade = 1f - age / .90f;
            if (glow)
            {
                glowRect.localScale = Vector3.one * (1f + age * 1.9f);
                glow.color = new Color(1f, 1f, 1f, fade);
            }
            for (int i = 0; i < Count; i++)
            {
                Vector2 p = origin + velocities[i] * age * (.94f - age * .25f);
                p.y -= age * age * 105f;
                locations[i] = p;
                rays[i].anchoredPosition = p;
                rays[i].sizeDelta = new Vector2(Mathf.Max(1f, spans[i] * fade), i % 3 == 0 ? 3f : 2f);
                Color c = rayImages[i].color;
                c.a = fade;
                rayImages[i].color = c;
            }
        }

        void OnDestroy()
        {
            if (glowTexture) Destroy(glowTexture);
        }
    }


    // R4: Pixel art of ONLY the metal-walled tube, rendered to the same uGUI Image
    // path proven to appear in the Editor. There is no source photograph and no
    // static current or red spark. Every rendered pixel derives from `path`.
    public static class SDGLevel03V4TubeTexture
    {
        public static Texture2D Build(Vector2[] path,int width,int height)
        {
            if(path==null || path.Length<2)throw new ArgumentException("R4 tube path missing");
            const float outerGlow=42f;
            float[] distances=new float[width*height];
            for(int i=0;i<distances.Length;i++)distances[i]=outerGlow*outerGlow+1f;
            float hx=(width-1)*.5f, hy=(height-1)*.5f;
            for(int k=0;k<path.Length-1;k++)
            {
                Vector2 a=path[k],b=path[k+1];
                int x0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,b.x)+hx-outerGlow));
                int x1=Mathf.Min(width-1,Mathf.CeilToInt(Mathf.Max(a.x,b.x)+hx+outerGlow));
                int y0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,b.y)+hy-outerGlow));
                int y1=Mathf.Min(height-1,Mathf.CeilToInt(Mathf.Max(a.y,b.y)+hy+outerGlow));
                Vector2 v=b-a;
                float len=Mathf.Max(.000001f,v.sqrMagnitude);
                for(int y=y0;y<=y1;y++)
                {
                    float py=y-hy;
                    int row=y*width;
                    for(int x=x0;x<=x1;x++)
                    {
                        float px=x-hx;
                        float dot=(px-a.x)*v.x+(py-a.y)*v.y;
                        float t=Mathf.Clamp01(dot/len);
                        float dx=px-(a.x+t*v.x),dy=py-(a.y+t*v.y);
                        float d2=dx*dx+dy*dy;
                        int id=row+x;
                        if(d2<distances[id])distances[id]=d2;
                    }
                }
            }
            Color32[] colors=new Color32[width*height];
            for(int i=0;i<colors.Length;i++)
            {
                float d=Mathf.Sqrt(distances[i]);
                if(d<3f)colors[i]=new Color32(54,165,218,245);
                else if(d<9f)colors[i]=new Color32(18,109,174,250);
                else if(d<15f)colors[i]=new Color32(6,47,83,255);
                else if(d<17.5f)colors[i]=new Color32(12,93,131,255);
                else if(d<19f)colors[i]=new Color32(47,237,255,255);
                else if(d<22f)colors[i]=new Color32(8,48,65,255);
                else if(d<24f)colors[i]=new Color32(113,174,190,255);
                else if(d<27f)colors[i]=new Color32(26,40,47,255);
                else if(d<30f)colors[i]=new Color32(6,148,186,145);
                else if(d<outerGlow)
                {
                    byte alpha=(byte)Mathf.Clamp(Mathf.RoundToInt((outerGlow-d)/(outerGlow-30f)*78f),0,78);
                    colors[i]=new Color32(0,165,226,alpha);
                }
                else colors[i]=new Color32(0,0,0,0);
            }
            Texture2D texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            texture.name="R4_PathGeneratedTube_Only_NotPhoto";
            texture.filterMode=FilterMode.Bilinear;
            texture.wrapMode=TextureWrapMode.Clamp;
            texture.SetPixels32(colors);
            texture.Apply(false,false);
            return texture;
        }
    }

    // Live electricity is updated every frame, independently of the static tube.
    // Moving / flickering cores and halos remain inside the same path that collision uses.
    public sealed class SDGLevel03V4EnergyStream : MonoBehaviour
    {
        const int PulseCount=32;
        Vector2[] path;
        float[] lengths;
        float totalLength;
        readonly RectTransform[] halos=new RectTransform[PulseCount];
        readonly RectTransform[] cores=new RectTransform[PulseCount];
        readonly Image[] haloImages=new Image[PulseCount];
        readonly Image[] coreImages=new Image[PulseCount];
        bool tracing;
        float playerProgress;

        public static SDGLevel03V4EnergyStream Create(RectTransform parent,Vector2[] sharedPath,float[] arclengths)
        {
            GameObject g=new GameObject("R4 LIVE flowing electricity",typeof(RectTransform),typeof(SDGLevel03V4EnergyStream));
            g.transform.SetParent(parent,false);
            RectTransform rect=g.GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=parent.rect.size;
            rect.anchoredPosition=Vector2.zero;
            SDGLevel03V4EnergyStream stream=g.GetComponent<SDGLevel03V4EnergyStream>();
            stream.path=sharedPath;
            stream.lengths=arclengths;
            stream.totalLength=arclengths[arclengths.Length-1];
            stream.Build();
            return stream;
        }
        void Build()
        {
            for(int i=0;i<PulseCount;i++)
            {
                GameObject halo=new GameObject("Energy halo "+i,typeof(RectTransform),typeof(Image));
                halo.transform.SetParent(transform,false);
                RectTransform hr=halo.GetComponent<RectTransform>();
                hr.anchorMin=hr.anchorMax=new Vector2(.5f,.5f);
                hr.pivot=new Vector2(.5f,.5f);
                hr.sizeDelta=new Vector2(34f,12f);
                haloImages[i]=halo.GetComponent<Image>();
                haloImages[i].raycastTarget=false;
                halos[i]=hr;
                GameObject core=new GameObject("Moving electric filament "+i,typeof(RectTransform),typeof(Image));
                core.transform.SetParent(transform,false);
                RectTransform cr=core.GetComponent<RectTransform>();
                cr.anchorMin=cr.anchorMax=new Vector2(.5f,.5f);
                cr.pivot=new Vector2(.5f,.5f);
                cr.sizeDelta=new Vector2(18f,3f);
                coreImages[i]=core.GetComponent<Image>();
                coreImages[i].raycastTarget=false;
                cores[i]=cr;
            }
        }
        public void SetPlayerState(bool active,float progress)
        {
            tracing=active;
            playerProgress=Mathf.Clamp01(progress);
        }
        Vector2 Position(float s,out Vector2 tangent)
        {
            int lo=0,hi=lengths.Length-1;
            while(lo<hi)
            {
                int mid=(lo+hi)/2;
                if(lengths[mid]<s)lo=mid+1;else hi=mid;
            }
            int at=Mathf.Clamp(lo,1,path.Length-1);
            float size=lengths[at]-lengths[at-1];
            float t=(s-lengths[at-1])/Mathf.Max(.000001f,size);
            tangent=(path[at]-path[at-1]).normalized;
            return Vector2.Lerp(path[at-1],path[at],t);
        }
        void Update()
        {
            if(path==null || lengths==null || totalLength<=0f)return;
            float time=Time.unscaledTime;
            float speed=tracing?160f:110f;
            for(int i=0;i<PulseCount;i++)
            {
                float phase=i/(float)PulseCount;
                float arc=Mathf.Repeat(time*speed+totalLength*phase,totalLength);
                Vector2 tangent;
                Vector2 center=Position(arc,out tangent);
                Vector2 normal=new Vector2(-tangent.y,tangent.x);
                // Small wandering current inside an actual 17px-wide lumen.
                float wander=5f*Mathf.Sin(time*10.2f+i*2.15f);
                center+=normal*wander;
                float rotation=Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg;
                float flicker=.52f+.48f*Mathf.Abs(Mathf.Sin(time*13.8f+i*1.9f));
                float energized=tracing && (arc/totalLength)<=playerProgress+.035f?1f:.73f;
                halos[i].anchoredPosition=center;
                halos[i].localRotation=Quaternion.Euler(0,0,rotation);
                halos[i].sizeDelta=new Vector2(26f+flicker*21f,9f+flicker*5f);
                haloImages[i].color=new Color(.12f,.79f,1f,.08f+.26f*flicker*energized);
                cores[i].anchoredPosition=center;
                cores[i].localRotation=Quaternion.Euler(0,0,rotation);
                cores[i].sizeDelta=new Vector2(9f+flicker*19f,2f+flicker*2f);
                coreImages[i].color=new Color(.66f,.95f,1f,.28f+.71f*flicker*energized);
            }
        }
    }

}
