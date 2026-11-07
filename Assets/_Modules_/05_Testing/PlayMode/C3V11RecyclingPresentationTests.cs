using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using SDGAdventure.SharedPlayer;
using Object=UnityEngine.Object;
using static M6IntegrationTests.C3V11Phase2RecyclingLineTests;

namespace M6IntegrationTests
{
    public sealed class C3V11RecyclingPresentationTests
    {
        private C3V11Phase2RecyclingLineTests scope;
        private Component line,device,console,runtime,library,input;
        private const BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        [UnitySetUp] public IEnumerator Open()
        {
            scope=new C3V11Phase2RecyclingLineTests(); yield return scope.Open();
            PlayerCursor.BeginSimulation(CursorLockMode.None);
            line=Find("C3TreasurePuzzle.C3RecyclingLine"); device=Find("C3TreasurePuzzle.C3DeviceState");
            console=Find("C3TreasurePuzzle.C3PuzzleConsole"); runtime=Find("EcoAdventureRuntime"); library=Find("C3TreasurePuzzle.C3RecyclingVisuals");
            Type inputType=Type.GetType("SDGAdventure.SharedPlayer.PlayerInputService, SDGAdventure.SharedPlayer",true);
            input=inputType.GetProperty("Instance").GetValue(null) as Component;
            if (input) Call(input,"SetContext",Enum.Parse(Member(input,"Context").GetType(),"Gameplay"));
        }
        [UnityTearDown] public IEnumerator Close() { PlayerCursor.EndSimulation(); if (scope!=null) yield return scope.Close(); }
        private IEnumerator FirstProduction()
        {
            foreach (string kind in new[] {"Plastic","Metal","Paper"}) C3Phase21TestDriver.Supply(line,kind);
            C3Phase21TestDriver.Activate(device);
            for (int i=0;i<1600 && (int)Member(line,"CollectedProducts")<3;i++)
            { C3Phase21TestDriver.Tick(line); if (i%25==0) yield return null; }
            Assert.AreEqual(9,Member(line,"ProcessedUnits")); Assert.AreEqual(3,Member(line,"CollectedProducts"));
            Assert.IsTrue((bool)Member(line,"FirstProductionCollected")); ((Behaviour)line).enabled=true;
        }
        private object Begin()
        {
            Assert.IsTrue((bool)Call(console,"OpenPanel")); Assert.AreEqual("ProductionControl",Member(console,"Mode").ToString());
            Assert.IsTrue((bool)Call(console,"StartPresentation")); Assert.IsFalse((bool)Member(console,"PanelOpen"));
            object session=Member(line,"ActivePresentation"); Assert.IsNotNull(session);
            Assert.AreEqual(9,Items(session,"Materials").Length); Assert.AreEqual(0,Items(session,"Products").Length);
            Assert.IsTrue((bool)Member(session,"IsPresentation")); return session;
        }
        private string ProtectedState()
        {
            string[] fields={"quickSlots","selectedSlot","factoryPowered","weaponCrafted","ammoInMagazine","reserveAmmo",
                "completedPlasticSorts","completedMetalSorts","completedPaperSorts","RecyclingReceiptCount"};
            string text=string.Join("|",Items(runtime,"inventory").Select(i=>JsonUtility.ToJson(i)));
            foreach (string name in fields)
            {
                object value=Member(runtime,name); text+="|"+name+"="+(value is string[]?string.Join(",",(string[])value):value.ToString());
            }
            foreach (string field in new[] {"recyclingReceipts","recyclingBatches"})
                text+="|"+field+"="+string.Join(",",Items(runtime,field).Cast<string>().OrderBy(s=>s));
            text+="|work="+string.Join(";",Items(line,"work").Select(w=>Member(w,"Completed")+":"+Member(w,"Elapsed")+":"+Member(w,"Processing")+":"+
                string.Join(",",Items(w,"Slots").Select(s=>s==null?"_":(string)Member(s,"MaterialId")))));
            text+="|batches="+string.Join(";",Items(line,"Products").Select(b=>Member(b,"BatchId")+":"+Member(b,"MaterialType")+":"+
                string.Join(",",(string[])Member(b,"SourceMaterialIds"))+":"+((Transform)Member(b,"Product")).GetInstanceID()+":"+
                ((Transform)Member(b,"Product")).position.ToString("F6")+":"+((Transform)Member(b,"Product")).rotation.ToString("F6")+":"+
                Member(b,"Collected")+":"+Member(b,"OutputReached")));
            Component progress=Find("C3TreasurePuzzle.C3TreasureProgress"),puzzle=Find("C3TreasurePuzzle.C3PuzzleState");
            return text+"|parts="+Member(progress,"CollectedCount")+"|puzzle="+Member(puzzle,"SubmissionCount")+
                "|factory="+Member(device,"FactoryState")+"|completed="+Member(device,"LevelCompleted")+
                "|lastKind="+Member(line,"LastCompletedLegacyKind")+
                "|official="+string.Join(";",Items(line,"Materials").Select(r=>Member(r,"MaterialId")+":"+Member(r,"MaterialType")+":"+
                    Member(r,"State")+":"+Member(r,"Counted")+":"+Member(r,"ProcessingComplete")+":"+Member(r,"BatchId")+":"+
                    string.Join(",",Items(r,"History").Select(s=>s.ToString()))));
        }
        private static Collider[] Supports(object record)
        {
            object[] legs=Items(Member(record,"Path"),"Legs"); int segment=(int)Member(record,"CurrentRouteSegment");
            return legs.Skip(Mathf.Max(0,segment-1)).Take(segment==0?2:3).Select(l=>(Collider)Member(l,"Support")).Distinct().ToArray();
        }
        private static string UiPath(Transform item)
        {
            if (!item) return "MISSING";
            string path=item.name; for (Transform parent=item.parent;parent;parent=parent.parent) path=parent.name+"/"+path;
            return path;
        }
        private static Rect ScreenRect(RectTransform rect,out Vector3[] corners)
        {
            corners=new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2[] screen=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(null,p)).ToArray();
            return Rect.MinMaxRect(screen.Min(p=>p.x),screen.Min(p=>p.y),screen.Max(p=>p.x),screen.Max(p=>p.y));
        }
        private static Vector2 ButtonPoint(Button button)
        {
            RectTransform rect=(RectTransform)button.transform;
            return RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        }
        private static bool HitsButton(List<RaycastResult> hits,Button button)
            => hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject;
        private sealed class UiRaycastEvidence
        {
            internal PointerEventData Pointer;
            internal EventSystem Owner;
            internal readonly List<RaycastResult> Direct=new List<RaycastResult>(),Events=new List<RaycastResult>();
            internal string Errors="";
        }
        private static UiRaycastEvidence QueryUi(Canvas canvas,EventSystem events,Vector2 point)
        {
            // Compare the SAME event/coordinate, with independent result lists.
            UiRaycastEvidence evidence=new UiRaycastEvidence {Owner=events,Pointer=new PointerEventData(events) {
                position=point,button=PointerEventData.InputButton.Left}};
            GraphicRaycaster raycaster=canvas.GetComponent<GraphicRaycaster>();
            try { if (raycaster) raycaster.Raycast(evidence.Pointer,evidence.Direct); else evidence.Errors+="Missing GraphicRaycaster\n"; }
            catch (Exception error) { evidence.Errors+="Direct query: "+error+"\n"; }
            try { if (events) events.RaycastAll(evidence.Pointer,evidence.Events); else evidence.Errors+="Missing EventSystem\n"; }
            catch (Exception error) { evidence.Errors+="EventSystem query: "+error+"\n"; }
            return evidence;
        }
        private static string HitDetails(IEnumerable<RaycastResult> hits)
            => string.Join("\n",hits.Select(h=>"  "+UiPath(h.gameObject?h.gameObject.transform:null)+
                " module="+(h.module?h.module.GetType().FullName+"@"+UiPath(h.module.transform):"MISSING")+
                " order="+h.sortingOrder+" depth="+h.depth+" distance="+h.distance.ToString("F6")+" screen="+h.screenPosition));
        private static int CountRaycastCandidates(Canvas canvas)
        {
            // UGUI IndexedSet implements IList, but its enumerator can throw.
            IList<Graphic> graphics=GraphicRegistry.GetGraphicsForCanvas(canvas);
            int count=0;
            for (int i=0;i<graphics.Count;i++)
            {
                Graphic graphic=graphics[i];
                if (graphic && graphic.isActiveAndEnabled && graphic.raycastTarget && graphic.depth>=0 && !graphic.canvasRenderer.cull)
                    count++;
            }
            return count;
        }
        private static T[] CopyIndexedOrNull<T>(object source)
        {
            if (!(source is IList<T> list)) return null;
            T[] result=new T[list.Count];
            for (int i=0;i<result.Length;i++) result[i]=list[i];
            return result;
        }
        private static bool IsAssertionFailure(Exception error)
        {
            for (Exception current=error;current!=null;current=current.InnerException)
                if (current is AssertionException) return true;
            return false;
        }
        private static string DiagnosticField(string field,Func<string> read)
        {
            // Only optional diagnostic reads belong here. Required UI checks,
            // raycasts, scrolling, pointer dispatch and assertions stay outside.
            try { return read(); }
            catch (Exception error) when (!IsAssertionFailure(error))
            { return "UNAVAILABLE: "+field+" ["+error.GetType().FullName+"]\n"+error; }
        }
        private static Graphic[] RaycastGraphicRegistry(Canvas canvas,out string status)
        {
            // GetGraphicsForCanvas is public. Some bundled UGUI revisions also
            // expose a separate public raycast registry; read it if available.
            string operation="public raycast-registry method lookup";
            try
            {
                MethodInfo get=typeof(GraphicRegistry).GetMethod("GetRaycastableGraphicsForCanvas",BindingFlags.Static|BindingFlags.Public);
                if (get==null) { status="UNAVAILABLE: public GetRaycastableGraphicsForCanvas method missing"; return null; }
                operation="public raycast-registry reflection invocation";
                object source=get.Invoke(null,new object[] {canvas});
                if (source==null) { status="UNAVAILABLE: GetRaycastableGraphicsForCanvas returned null"; return null; }
                operation="public raycast-registry indexed copy";
                Graphic[] result=CopyIndexedOrNull<Graphic>(source);
                if (result==null) { status="UNAVAILABLE: raycast-registry return type "+source.GetType().FullName+" does not implement IList<Graphic>"; return null; }
                status="public GetRaycastableGraphicsForCanvas; returnType="+source.GetType().FullName+"; indexed count="+result.Length; return result;
            }
            catch (Exception error) when (!IsAssertionFailure(error))
            { status="UNAVAILABLE: "+operation+" failed ["+error.GetType().FullName+"]\n"+error; return null; }
        }
        private static BaseRaycaster[] RegisteredRaycasters(out string status)
        {
            // RaycasterManager is INTERNAL in UGUI 1.0.0. This read-only,
            // optional diagnostic must never register/remove a raycaster.
            string operation="internal raycaster-manager method lookup";
            try
            {
                Type manager=typeof(EventSystem).Assembly.GetType("UnityEngine.EventSystems.RaycasterManager");
                MethodInfo get=manager?.GetMethod("GetRaycasters",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
                if (get==null) { status="UNAVAILABLE: RaycasterManager.GetRaycasters method missing"; return null; }
                operation="internal raycaster-manager reflection invocation";
                object source=get.Invoke(null,null);
                if (source==null) { status="UNAVAILABLE: RaycasterManager.GetRaycasters returned null"; return null; }
                operation="internal raycaster-manager indexed copy";
                BaseRaycaster[] result=CopyIndexedOrNull<BaseRaycaster>(source);
                if (result==null) { status="UNAVAILABLE: raycaster-manager return type "+source.GetType().FullName+" does not implement IList<BaseRaycaster>"; return null; }
                status="read-only reflection; internal RaycasterManager; returnType="+source.GetType().FullName+"; indexed count="+result.Length; return result;
            }
            catch (Exception error) when (!IsAssertionFailure(error))
            { status="UNAVAILABLE: "+operation+" failed ["+error.GetType().FullName+"]\n"+error; return null; }
        }
        private static string GraphicState(Graphic graphic,Vector2 point,Camera camera,bool filters=false)
            => DiagnosticField("GraphicState "+UiPath(graphic?graphic.transform:null),()=>GraphicStateDetails(graphic,point,camera,filters));
        private static string GraphicStateDetails(Graphic graphic,Vector2 point,Camera camera,bool filters)
        {
            if (!graphic) return "MISSING Graphic";
            Canvas canvas=graphic.canvas; Graphic[] raycast=canvas?RaycastGraphicRegistry(canvas,out _):null;
            string registration=canvas?GraphicRegistry.GetGraphicsForCanvas(canvas).Contains(graphic).ToString():"NO CANVAS";
            Rect rect=ScreenRect(graphic.rectTransform,out Vector3[] corners);
            float facing=Vector3.Dot(camera?camera.transform.forward:Vector3.forward,graphic.transform.forward);
            StringBuilder text=new StringBuilder((filters?UiPath(graphic.transform):graphic.name+"#"+graphic.GetInstanceID())+" type="+graphic.GetType().FullName+
                " active="+graphic.isActiveAndEnabled+" raycastTarget="+graphic.raycastTarget+" depth="+graphic.depth+
                " cull="+graphic.canvasRenderer.cull+" color.a="+graphic.color.a+" rendererAlpha="+graphic.canvasRenderer.GetAlpha()+
                " canvas="+(filters?UiPath(canvas?canvas.transform:null):canvas?canvas.name+"#"+canvas.GetInstanceID():"MISSING")+" registered="+registration+
                " raycastRegistered="+(raycast==null?"UNAVAILABLE":raycast.Contains(graphic).ToString())+
                " rectContains="+RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform,point,camera)+
                " Graphic.Raycast="+graphic.Raycast(point,camera)+" facingDot="+facing.ToString("F6"));
            if (!filters) return text.ToString();
            text.AppendLine("\n  rect="+graphic.rectTransform.rect+" screenRect="+rect+" worldCorners="+string.Join(";",corners.Select(p=>p.ToString("F3")))+
                " position="+graphic.transform.position.ToString("F3")+" rotation="+graphic.transform.rotation.ToString("F3")+
                " lossyScale="+graphic.transform.lossyScale.ToString("F3")+" forward="+graphic.transform.forward.ToString("F3"));
            for (Transform t=graphic.transform;t;t=t.parent)
            {
                Canvas ancestor=t.GetComponent<Canvas>();
                if (ancestor) text.AppendLine("  canvasAncestor="+UiPath(t)+" overrideSorting="+ancestor.overrideSorting);
                foreach (Component component in t.GetComponents<Component>())
                {
                    if (!(component is ICanvasRaycastFilter filter)) continue;
                    Behaviour behaviour=component as Behaviour; CanvasGroup group=component as CanvasGroup;
                    text.AppendLine("  rawFilter="+UiPath(t)+":"+component.GetType().FullName+
                        " active="+(!behaviour || behaviour.isActiveAndEnabled)+" accepts="+filter.IsRaycastLocationValid(point,camera)+
                        (group?" alpha="+group.alpha+" blocksRaycasts="+group.blocksRaycasts+" interactable="+group.interactable+
                            " ignoreParentGroups="+group.ignoreParentGroups:""));
                }
            }
            text.AppendLine("  Raw filter entries are not combined; Graphic.Raycast above applies ignoreParentGroups/overrideSorting.");
            return text.ToString();
        }
        private static bool UiReady(Canvas canvas,ScrollRect scroll,Button button,UiRaycastEvidence evidence,bool forButton)
        {
            Graphic graphic=forButton?button.image:scroll.viewport.GetComponent<Image>();
            Vector2 point=evidence.Pointer.position; GraphicRaycaster rc=canvas.GetComponent<GraphicRaycaster>(); Camera camera=rc?rc.eventCamera:null;
            if (!evidence.Owner || evidence.Owner!=EventSystem.current || !evidence.Owner.isActiveAndEnabled ||
                !evidence.Owner.currentInputModule || !evidence.Owner.currentInputModule.isActiveAndEnabled ||
                !rc || !rc.isActiveAndEnabled || !canvas.isActiveAndEnabled || !graphic || !graphic.isActiveAndEnabled || !graphic.raycastTarget ||
                graphic.depth<0 || graphic.canvasRenderer.cull || !GraphicRegistry.GetGraphicsForCanvas(canvas).Contains(graphic) ||
                !RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform,point,camera) ||
                !new Rect(0f,0f,Screen.width,Screen.height).Contains(point) || !graphic.Raycast(point,camera) || evidence.Errors.Length>0) return false;
            if (forButton) return button.interactable && scroll.viewport.GetComponent<RectMask2D>().IsRaycastLocationValid(point,camera) &&
                HitsButton(evidence.Direct,button) && HitsButton(evidence.Events,button);
            return evidence.Direct.Any(h=>h.gameObject==graphic.gameObject) && evidence.Events.Any(h=>h.gameObject==graphic.gameObject) &&
                evidence.Events.Count>0 && ExecuteEvents.GetEventHandler<IScrollHandler>(evidence.Events[0].gameObject)==scroll.gameObject;
        }
        private static string UiDiagnostics(Canvas canvas,ScrollRect scroll,Button button,EventSystem events,Vector2 point,UiRaycastEvidence evidence)
        {
            Rect viewport=ScreenRect(scroll.viewport,out Vector3[] viewportCorners),target=ScreenRect((RectTransform)button.transform,out Vector3[] buttonCorners);
            RectMask2D mask=scroll.viewport.GetComponent<RectMask2D>(); GraphicRaycaster raycaster=canvas.GetComponent<GraphicRaycaster>();
            CanvasScaler scaler=canvas.GetComponent<CanvasScaler>(); StringBuilder text=new StringBuilder();
            text.AppendLine("R3 ROOT CAUSE: NOT YET CONFIRMED\nUnity="+Application.unityVersion+" batch="+Application.isBatchMode+
                " graphicsDevice="+SystemInfo.graphicsDeviceType+" frame="+Time.frameCount+" UGUI="+typeof(Graphic).Assembly.FullName);
            text.AppendLine("R3 screen="+Screen.width+"x"+Screen.height+" canvas="+UiPath(canvas.transform)+" renderMode="+canvas.renderMode+
                " sortingOrder="+canvas.sortingOrder+" active="+canvas.gameObject.activeInHierarchy+" scaleFactor="+canvas.scaleFactor+
                " Canvas.enabled="+canvas.enabled+" Canvas.isActiveAndEnabled="+canvas.isActiveAndEnabled+
                " CanvasScaler.scaleFactor="+(scaler?scaler.scaleFactor.ToString():"MISSING")+" GraphicRaycaster.enabled="+(raycaster && raycaster.isActiveAndEnabled));
            text.AppendLine("EventSystem="+(events?UiPath(events.transform):"MISSING")+" enabled="+(events && events.isActiveAndEnabled)+
                " inputModule="+(events && events.currentInputModule?events.currentInputModule.GetType().FullName:"MISSING")+
                " inputModuleEnabled="+(events && events.currentInputModule && events.currentInputModule.isActiveAndEnabled)+
                " panelActive="+scroll.transform.parent.gameObject.activeInHierarchy);
            text.AppendLine("viewportWorld="+string.Join(";",viewportCorners.Select(p=>p.ToString("F3")))+" viewportScreen="+viewport+
                " contentHeight="+scroll.content.rect.height+" contentPosition="+scroll.content.anchoredPosition+
                " normalized="+scroll.verticalNormalizedPosition+" scrollEnabled="+scroll.isActiveAndEnabled);
            text.AppendLine("button="+UiPath(button.transform)+" active="+button.gameObject.activeInHierarchy+" interactable="+button.interactable+
                " image.raycastTarget="+(button.image && button.image.raycastTarget)+" graphicCulled="+(button.image && button.image.canvasRenderer.cull)+
                " buttonWorld="+string.Join(";",buttonCorners.Select(p=>p.ToString("F3")))+" buttonScreen="+target+" point="+point+
                " overlapsViewport="+viewport.Overlaps(target)+" maskEnabled="+(mask && mask.enabled)+
                " maskAcceptsPoint="+(mask && mask.IsRaycastLocationValid(point,null)));
            text.AppendLine("rootCanvas="+UiPath(canvas.rootCanvas?canvas.rootCanvas.transform:null)+" targetDisplay="+canvas.targetDisplay+
                " pixelRect="+canvas.pixelRect+" canvasRect="+((RectTransform)canvas.transform).rect+
                " canvasPosition="+canvas.transform.position.ToString("F3")+" rotation="+canvas.transform.rotation.ToString("F3")+
                " lossyScale="+canvas.transform.lossyScale.ToString("F3")+" canvasForward="+canvas.transform.forward.ToString("F3")+
                " Display.RelativeMouseAt="+Display.RelativeMouseAt(point)+" displayCount="+Display.displays.Length);
            text.AppendLine("Direct count="+evidence.Direct.Count+"\n"+HitDetails(evidence.Direct)+
                "\nEventSystem count="+evidence.Events.Count+"\n"+HitDetails(evidence.Events)+"\nqueryErrors="+evidence.Errors);
            text.AppendLine("Direct nonempty and all objects also in EventSystem="+(evidence.Direct.Count>0 &&
                evidence.Direct.All(d=>evidence.Events.Any(e=>e.gameObject==d.gameObject && e.module==d.module))));
            foreach (Graphic graphic in new Graphic[] {scroll.viewport.GetComponent<Image>(),scroll.transform.parent.GetComponent<Image>(),button.image})
                text.AppendLine(GraphicState(graphic,point,raycaster?raycaster.eventCamera:null,true));
            Graphic[] raycastGraphics=RaycastGraphicRegistry(canvas,out string graphicsStatus);
            text.AppendLine(DiagnosticField("GraphicRegistry summary",()=>"GraphicRegistry all="+GraphicRegistry.GetGraphicsForCanvas(canvas).Count+
                " raycastRegistry="+(raycastGraphics==null?"UNAVAILABLE":raycastGraphics.Length.ToString())+" API="+graphicsStatus+
                " candidatesBeforeRectAndFilters="+CountRaycastCandidates(canvas)+
                " descendants="+canvas.GetComponentsInChildren<Graphic>(true).Length));
            BaseRaycaster[] registered=RegisteredRaycasters(out string registryStatus);
            text.AppendLine("Raycaster registry: "+registryStatus+" entries="+(registered==null?"UNAVAILABLE":registered.Length.ToString()));
            foreach (BaseRaycaster rc in Object.FindObjectsOfType<BaseRaycaster>(true))
            {
                GraphicRaycaster gr=rc as GraphicRaycaster; Canvas owner=rc.GetComponent<Canvas>();
                text.AppendLine("Raycaster="+UiPath(rc.transform)+":"+rc.GetType().FullName+
                    " active="+rc.isActiveAndEnabled+" IsActive="+rc.IsActive()+" registered="+(registered==null?"UNAVAILABLE":registered.Contains(rc).ToString())+
                    " camera="+UiPath(rc.eventCamera?rc.eventCamera.transform:null)+" rootRaycaster="+UiPath(rc.rootRaycaster?rc.rootRaycaster.transform:null)+
                    (gr?" ignoreReversed="+gr.ignoreReversedGraphics+" blockingObjects="+gr.blockingObjects+" blockingMask="+gr.blockingMask.value:"")+
                    (owner?" rootCanvas="+UiPath(owner.rootCanvas?owner.rootCanvas.transform:null)+" targetDisplay="+owner.targetDisplay:""));
                if (owner && gr && gr.isActiveAndEnabled)
                {
                    UiRaycastEvidence other=QueryUi(owner,events,point);
                    text.AppendLine("  otherCanvas direct="+other.Direct.Count+" allGraphics="+
                        DiagnosticField("other Canvas GraphicRegistry count",()=>GraphicRegistry.GetGraphicsForCanvas(owner).Count.ToString())+
                        "\n"+HitDetails(other.Direct)+"\n"+other.Errors);
                }
            }
            text.AppendLine("EventSystems="+string.Join(";",Object.FindObjectsOfType<EventSystem>(true).Select(e=>UiPath(e.transform)+
                " active="+e.isActiveAndEnabled+" current="+(e==EventSystem.current))));
            text.AppendLine("Active canvases="+string.Join(";",Object.FindObjectsOfType<Canvas>().Where(c=>c.isActiveAndEnabled)
                .Select(c=>UiPath(c.transform)+" order="+c.sortingOrder+" mode="+c.renderMode)));
            return text.ToString();
        }
        private static UiRaycastEvidence RecordUiStage(StringBuilder history,string stage,Canvas canvas,ScrollRect scroll,Button button,EventSystem events,bool forButton=false)
        {
            Vector2 point=forButton?ButtonPoint(button):RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.TransformPoint(scroll.viewport.rect.center));
            UiRaycastEvidence evidence=QueryUi(canvas,events,point); GraphicRaycaster rc=canvas.GetComponent<GraphicRaycaster>();
            BaseRaycaster[] registered=RegisteredRaycasters(out string status);
            history.AppendLine(stage+" frame="+Time.frameCount+" point="+point+" contentHeight="+scroll.content.rect.height+
                " normalized="+scroll.verticalNormalizedPosition+" direct="+evidence.Direct.Count+" event="+evidence.Events.Count+
                " targetReady="+UiReady(canvas,scroll,button,evidence,forButton)+
                " raycasterRegistered="+(registered==null?status:registered.Contains(rc).ToString()));
            foreach (Graphic g in new Graphic[] {scroll.viewport.GetComponent<Image>(),scroll.transform.parent.GetComponent<Image>(),button.image})
                history.AppendLine("  "+GraphicState(g,point,rc?rc.eventCamera:null));
            return evidence;
        }
        private static bool RecordFormalControls(StringBuilder history,Canvas canvas,ScrollRect scroll,Button button,EventSystem events)
        {
            RectTransform panel=(RectTransform)scroll.transform.parent;
            Vector2 background=RectTransformUtility.WorldToScreenPoint(null,panel.TransformPoint(new Vector2(panel.rect.xMin+9f,panel.rect.center.y)));
            UiRaycastEvidence panelQuery=QueryUi(canvas,events,background);
            history.AppendLine("Formal panel-background control point="+background+" direct="+panelQuery.Direct.Count+" event="+panelQuery.Events.Count+
                "\n"+HitDetails(panelQuery.Direct)+"\n"+HitDetails(panelQuery.Events)+panelQuery.Errors);
            Button visible=scroll.content.GetComponentsInChildren<Button>().FirstOrDefault(b=>b!=button && b.IsActive() && b.interactable &&
                new Rect(0f,0f,Screen.width,Screen.height).Contains(ButtonPoint(b)) &&
                RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,ButtonPoint(b),null));
            bool anyHits=panelQuery.Direct.Count>0 || panelQuery.Events.Count>0;
            if (!visible) { history.AppendLine("Formal other visible Button control: NONE in current viewport"); return anyHits; }
            UiRaycastEvidence query=QueryUi(canvas,events,ButtonPoint(visible));
            history.AppendLine("Formal visible Button control="+UiPath(visible.transform)+" point="+ButtonPoint(visible)+
                " direct="+query.Direct.Count+" event="+query.Events.Count+" directButton="+HitsButton(query.Direct,visible)+
                " eventButton="+HitsButton(query.Events,visible)+"\n"+HitDetails(query.Direct)+"\n"+HitDetails(query.Events)+query.Errors);
            return anyHits || query.Direct.Count>0 || query.Events.Count>0;
        }
        private static IEnumerator TemporaryCanvasControl(Canvas canvas,ScrollRect scroll,Button button,EventSystem events,StringBuilder history)
        {
            // Failure-only control, never used to scroll or click the real UI.
            GameObject owner=new GameObject("R3_DIAGNOSTIC_IMAGE",typeof(RectTransform),typeof(Image));
            try
            {
                owner.transform.SetParent(canvas.transform,false); RectTransform rect=(RectTransform)owner.transform;
                rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f; rect.sizeDelta=new Vector2(48f,48f);
                Vector2 point=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.TransformPoint(scroll.viewport.rect.center));
                bool placed=RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform,point,null,out Vector3 world);
                owner.transform.position=world;
                Image image=owner.GetComponent<Image>(); image.color=new Color(1f,0f,1f,.5f); image.raycastTarget=true;
                for (int frame=0;frame<=2;frame++)
                {
                    if (frame>0) yield return null;
                    Canvas.ForceUpdateCanvases(); UiRaycastEvidence query=QueryUi(canvas,events,point);
                    history.AppendLine("Temporary same-Canvas control frame="+Time.frameCount+" placed="+placed+
                        " direct="+query.Direct.Count+" event="+query.Events.Count+"\n"+GraphicState(image,point,null,frame==0)+
                        "\n"+HitDetails(query.Direct)+"\n"+HitDetails(query.Events)+query.Errors);
                    if (query.Direct.Any(h=>h.gameObject==owner) || query.Events.Any(h=>h.gameObject==owner)) break;
                }
            }
            finally { Object.DestroyImmediate(owner); }
        }
        private static IEnumerator AwaitUiReady(Canvas canvas,ScrollRect scroll,Button button,EventSystem events,StringBuilder history,string phase,bool forButton=false)
        {
            const int maximumFrames=3;
            UiRaycastEvidence evidence=RecordUiStage(history,phase+" before Force",canvas,scroll,button,events,forButton);
            RebuildScroll(scroll);
            evidence=RecordUiStage(history,phase+" after Force",canvas,scroll,button,events,forButton);
            for (int frame=1;frame<=maximumFrames && !UiReady(canvas,scroll,button,evidence,forButton);frame++)
            {
                yield return null;
                evidence=RecordUiStage(history,phase+" next frame "+frame,canvas,scroll,button,events,forButton);
                if (UiReady(canvas,scroll,button,evidence,forButton)) break;
                RebuildScroll(scroll);
                evidence=RecordUiStage(history,phase+" next frame "+frame+" after Force",canvas,scroll,button,events,forButton);
            }
            if (UiReady(canvas,scroll,button,evidence,forButton)) yield break;
            bool otherControlHits=RecordFormalControls(history,canvas,scroll,button,events);
            if (evidence.Direct.Count==0 && evidence.Events.Count==0 && !otherControlHits)
            {
                yield return TemporaryCanvasControl(canvas,scroll,button,events,history);
                evidence=RecordUiStage(history,phase+" after temporary control removed (timeout retained)",canvas,scroll,button,events,forButton);
            }
            Assert.Fail("R3 UI readiness timed out after "+maximumFrames+" frames; no raycast assertion was bypassed.\n"+
                history+"\n"+UiDiagnostics(canvas,scroll,button,events,evidence.Pointer.position,evidence));
        }
        private static void RebuildScroll(ScrollRect scroll)
        {
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content); Canvas.ForceUpdateCanvases();
        }
        private static IEnumerator WheelButtonIntoView(Canvas canvas,ScrollRect scroll,Button button,EventSystem events,StringBuilder history,string phase)
        {
            RebuildScroll(scroll); scroll.StopMovement();
            Bounds target=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,button.transform);
            Rect viewport=scroll.viewport.rect;
            if (target.min.y<viewport.yMin+2f || target.max.y>viewport.yMax-2f)
            {
                float hidden=Mathf.Max(0f,scroll.content.rect.height-viewport.height);
                float pixels=Mathf.Clamp(viewport.center.y-target.center.y,-scroll.content.anchoredPosition.y,hidden-scroll.content.anchoredPosition.y);
                Vector2 point=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.TransformPoint(viewport.center));
                UiRaycastEvidence query=QueryUi(canvas,events,point); List<RaycastResult> hits=query.Events;
                Func<string> evidence=()=>history+"\n"+UiDiagnostics(canvas,scroll,button,events,point,query);
                bool canScroll=scroll.vertical && scroll.scrollSensitivity>0f;
                Assert.IsTrue(canScroll,canScroll?null:"ScrollRect cannot scroll\n"+evidence());
                bool wheelReceiver=hits.Count>0 && ExecuteEvents.GetEventHandler<IScrollHandler>(hits[0].gameObject)==scroll.gameObject;
                Assert.IsTrue(wheelReceiver,wheelReceiver?null:"Wheel over the real viewport cannot reach ScrollRect\n"+evidence());
                Vector2 before=scroll.content.anchoredPosition; float beforeNormalized=scroll.verticalNormalizedPosition;
                PointerEventData wheel=query.Pointer; wheel.scrollDelta=new Vector2(0f,-pixels/scroll.scrollSensitivity);
                wheel.pointerCurrentRaycast=hits[0];
                Assert.AreSame(scroll.gameObject,ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,wheel,ExecuteEvents.scrollHandler));
                scroll.StopMovement(); RebuildScroll(scroll);
                Vector2 after=scroll.content.anchoredPosition;
                history.AppendLine(phase+" wheel pixels="+pixels+" contentBefore="+before+" contentAfter="+after+
                    " normalizedBefore="+beforeNormalized+" normalizedAfter="+scroll.verticalNormalizedPosition);
                float movement=after.y-before.y;
                Assert.Greater(Mathf.Abs(movement),.5f,Mathf.Abs(movement)>.5f?null:"Wheel receiver did not move Content\n"+evidence());
                Assert.That(movement,Is.EqualTo(pixels).Within(.5f),
                    Math.Abs((double)movement-pixels)<=.5?null:"Unexpected wheel movement\n"+evidence());
            }
            yield return AwaitUiReady(canvas,scroll,button,events,history,phase+" visible button",true);
            Vector2 buttonPoint=ButtonPoint(button); UiRaycastEvidence final=QueryUi(canvas,events,buttonPoint);
            Func<string> details=()=>history+"\n"+UiDiagnostics(canvas,scroll,button,events,buttonPoint,final);
            bool available=button.gameObject.activeInHierarchy && button.interactable && button.image && button.image.raycastTarget;
            Assert.IsTrue(available,available?null:"Button unavailable\n"+details());
            bool visible=RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,buttonPoint,null) &&
                scroll.viewport.GetComponent<RectMask2D>().IsRaycastLocationValid(buttonPoint,null) &&
                new Rect(0f,0f,Screen.width,Screen.height).Contains(buttonPoint);
            Assert.IsTrue(visible,visible?null:"Button centre is clipped/off-screen\n"+details());
            bool receivesClick=HitsButton(final.Events,button);
            Assert.IsTrue(receivesClick,receivesClick?null:"No unobstructed Button event receiver at the visible point\n"+details());
        }
        private sealed class NativeOutputProbe : IDisposable
        {
            private readonly GameObject owner;
            private readonly SphereCollider sphere;
            private readonly BoxCollider box;
            private readonly MethodInfo contact,hasSupport;
            internal NativeOutputProbe()
            {
                owner=new GameObject("P22_TEST_NATIVE_PROBES"); owner.transform.position=Vector3.one*128f;
                sphere=owner.AddComponent<SphereCollider>(); sphere.isTrigger=true; sphere.radius=.12f;
                GameObject shape=new GameObject("P22_TEST_BALE_PROBE"); shape.transform.SetParent(owner.transform,false);
                shape.transform.localPosition=Vector3.right*4f; box=shape.AddComponent<BoxCollider>(); box.isTrigger=true;
                BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic; Type type=typeof(C3V11Phase2RecyclingLineTests);
                contact=type.GetMethod("BoxContactGaps",flags); hasSupport=type.GetMethod("HasSupport",flags);
                try
                {
                    Assert.IsNotNull(contact); Assert.IsNotNull(hasSupport);
                    type.GetMethod("ValidateBoxProbe",flags).Invoke(null,new object[] {box});
                    type.GetMethod("ValidateSphereProbe",flags).Invoke(null,new object[] {sphere,box});
                }
                catch { Object.DestroyImmediate(owner); throw; }
            }
            internal void Frame(object session)
            {
                foreach (object record in Items(session,"Materials"))
                {
                    string state=Member(record,"State").ToString();
                    if (state=="Spawned" || state=="Processing" || (bool)Member(record,"ProcessingComplete")) continue;
                    Transform cargo=((Component)Member(record,"Cargo")).transform; Collider[] supports=Supports(record);
                    Assert.Less(supports.Min(s=>Mathf.Abs(Vector3.Distance(cargo.position,s.ClosestPoint(cargo.position))-.12f)),.01f);
                    foreach (Collider c in Physics.OverlapSphere(cargo.position,.12f,~0,QueryTriggerInteraction.Ignore))
                        if (!c.transform.IsChildOf(cargo) && Physics.ComputePenetration(sphere,cargo.position,cargo.rotation,c,c.transform.position,c.transform.rotation,out _,out float depth))
                            Assert.LessOrEqual(depth,.001f,"Presentation raw penetrates "+c.name+" at "+cargo.position);
                }
                foreach (object batch in Items(session,"Products"))
                {
                    Transform product=(Transform)Member(batch,"Product"); Bounds envelope=C3Phase21TestDriver.LocalEnvelope(product);
                    box.size=envelope.size; box.center=envelope.center; Collider[] supports=Supports(batch);
                    if ((int)Member(batch,"CurrentRouteSegment")<=1)
                    {
                        int lane=Member(batch,"MaterialType").ToString()=="Paper"?0:Member(batch,"MaterialType").ToString()=="Metal"?1:2;
                        supports=supports.Concat(new[] {(Collider)Member(Items(Find("C3TreasurePuzzle.C3RecyclingLineGeometry"),"Machines")[lane],"ProcessBed")}).Distinct().ToArray();
                    }
                    object gaps=contact.Invoke(null,new object[] {box,product.position,product.rotation,supports,QueryTriggerInteraction.Ignore});
                    Assert.IsTrue((bool)hasSupport.Invoke(null,new[] {gaps}),"Presentation bale lost finite 3mm support: "+Member(batch,"BatchId")+" at "+product.position);
                    foreach (Collider c in Physics.OverlapBox(product.TransformPoint(box.center),box.size*.5f,product.rotation,~0,QueryTriggerInteraction.Ignore))
                        if (!c.transform.IsChildOf(product) && Physics.ComputePenetration(box,product.position,product.rotation,c,c.transform.position,c.transform.rotation,out _,out float depth))
                            Assert.LessOrEqual(depth,.01f,"Presentation bale penetrates "+c.name+" at "+product.position);
                }
            }
            public void Dispose() { if (owner) Object.DestroyImmediate(owner); }
        }
        [UnityTest] public IEnumerator PresentationRequiresRunningCollectedProductionAndTheOpenRealConsole()
        {
            Assert.IsFalse((bool)Member(line,"CanStartPresentation")); Assert.IsFalse((bool)Call(console,"StartPresentation"));
            C3Phase21TestDriver.Activate(device,false); ((Behaviour)line).enabled=true;
            Assert.AreEqual("Starting",Member(device,"FactoryState").ToString()); Assert.IsFalse((bool)Member(line,"CanStartPresentation"));
            Call(device,"NotifyStartupFinished");
            Assert.IsTrue((bool)Call(console,"CanInteract",GameObject.Find("M2_Player")));
            Assert.IsTrue((bool)Call(console,"OpenPanel")); Assert.AreEqual("ProductionControl",Member(console,"Mode").ToString());
            Assert.IsFalse((bool)Member(console,"CanStartPresentation")); Assert.IsFalse((bool)Call(console,"StartPresentation"));
            Assert.IsFalse((bool)Call(console,"Confirm")); Assert.IsFalse((bool)Call(console,"ConfirmActivation"));
            Assert.IsTrue((bool)Call(device,"TryCompleteAtExit"));
            Call(console,"Update"); Assert.IsFalse((bool)Member(console,"PanelOpen"));
            Assert.IsFalse((bool)Call(console,"OpenPanel")); Assert.IsFalse((bool)Member(line,"CanStartPresentation"));
            Assert.AreEqual(CursorLockMode.None,PlayerCursor.LockState); Assert.IsTrue(Cursor.visible);
            yield return null;
        }
        [UnityTest] public IEnumerator ProductionUguiReceivesARealClickWithoutReactivationOrPuzzleSubmission()
        {
            int activations=0; Action activated=()=>activations++;
            EventInfo evt=device.GetType().GetEvent("Activated"); evt.AddEventHandler(device,activated);
            EventSystem eventOwner=EventSystem.current; GameObject previousSelection=eventOwner?eventOwner.currentSelectedGameObject:null;
            StringBuilder history=new StringBuilder();
            try
            {
                yield return FirstProduction();
                string formalBefore=ProtectedState();
                int attempts=(int)Member(Find("C3TreasurePuzzle.C3PuzzleState"),"SubmissionCount");
                PlayerCursor.LockState=CursorLockMode.Locked; Cursor.visible=false;
                Assert.IsTrue((bool)Call(console,"OpenPanel"));
                Transform content=GameObject.Find("C3UiCanvas").transform.Find("PuzzlePanel/Viewport/Content");
                Button button=content.Find("Presentation").GetComponent<Button>();
                Canvas canvas=content.GetComponentInParent<Canvas>(); ScrollRect scroll=content.parent.GetComponent<ScrollRect>();
                Assert.IsNotNull(scroll); Assert.IsNotNull(canvas); Assert.AreEqual(RenderMode.ScreenSpaceOverlay,canvas.renderMode);
                EventSystem events=EventSystem.current;
                history.AppendLine("Input context="+(input?Member(input,"Context").ToString():"No PlayerInputService"));
                RecordUiStage(history,"OpenPanel immediate",canvas,scroll,button,events);
                yield return null;
                RecordUiStage(history,"OpenPanel next frame before rebuild",canvas,scroll,button,events);
                yield return AwaitUiReady(canvas,scroll,button,events,history,"normal viewport");
                RecordFormalControls(history,canvas,scroll,button,events);
                Func<string> initial=()=>history+"\n"+UiDiagnostics(canvas,scroll,button,events,ButtonPoint(button),QueryUi(canvas,events,ButtonPoint(button)));
                Assert.IsNotNull(events,events?null:initial());
                Assert.IsTrue(events.isActiveAndEnabled,events.isActiveAndEnabled?null:initial());
                Assert.IsNotNull(events.currentInputModule,events.currentInputModule?null:initial());
                Assert.IsTrue(events.currentInputModule.isActiveAndEnabled,events.currentInputModule.isActiveAndEnabled?null:initial());
                Assert.IsTrue(canvas.GetComponent<GraphicRaycaster>().isActiveAndEnabled,
                    canvas.GetComponent<GraphicRaycaster>().isActiveAndEnabled?null:initial());
                Assert.AreEqual("回收產線控制",content.Find("Title").GetComponent<Text>().text);
                foreach (string name in new[] {"Choice0","Choice1","Choice2","Confirm","Retry","Activate"}) Assert.IsFalse(content.Find(name).gameObject.activeSelf,name);
                Assert.IsTrue(button.interactable);
                // Exercise a short real viewport even when this test runs in a
                // tall Game View. The production mask stays enabled throughout.
                Vector2 originalMin=scroll.viewport.offsetMin,originalMax=scroll.viewport.offsetMax;
                float originalPosition=scroll.verticalNormalizedPosition;
                Vector2 originalContent=scroll.content.anchoredPosition,originalVelocity=scroll.velocity;
                bool originalInertia=scroll.inertia;
                try
                {
                    float shortHeight=Mathf.Min(180f,scroll.viewport.rect.height);
                    scroll.viewport.offsetMin=new Vector2(originalMin.x,((RectTransform)scroll.viewport.parent).rect.height+originalMax.y-shortHeight);
                    RebuildScroll(scroll); scroll.verticalNormalizedPosition=1f; RebuildScroll(scroll);
                    yield return AwaitUiReady(canvas,scroll,button,events,history,"short viewport");
                    Vector2 clipped=ButtonPoint(button); UiRaycastEvidence clippedQuery=QueryUi(canvas,events,clipped);
                    Func<string> evidence=()=>history+"\n"+UiDiagnostics(canvas,scroll,button,events,clipped,clippedQuery);
                    Assert.IsTrue(scroll.viewport.GetComponent<RectMask2D>().enabled);
                    bool maskAccepts=scroll.viewport.GetComponent<RectMask2D>().IsRaycastLocationValid(clipped,null);
                    Assert.IsFalse(maskAccepts,maskAccepts?"Negative control did not clip the button\n"+evidence():null);
                    bool clippedReceivesClick=clippedQuery.Direct.Concat(clippedQuery.Events).Any(h=>
                        ExecuteEvents.GetEventHandler<IPointerClickHandler>(h.gameObject)==button.gameObject);
                    Assert.IsFalse(clippedReceivesClick,clippedReceivesClick?"An invisible button still receives raycasts\n"+evidence():null);
                    Assert.IsNull(Member(line,"ActivePresentation"));
                    history.AppendLine("Clipped button control: maskRejects=True direct="+clippedQuery.Direct.Count+" event="+clippedQuery.Events.Count+
                        " Presentation receiver absent, ActivePresentation null\n"+HitDetails(clippedQuery.Direct)+"\n"+HitDetails(clippedQuery.Events));
                    yield return WheelButtonIntoView(canvas,scroll,button,events,history,"short viewport");
                    // The viewport hit surface is necessary: a wheel event over
                    // non-raycast text must reach the same production ScrollRect.
                    Assert.IsTrue(scroll.viewport.GetComponent<Image>().raycastTarget);
                }
                finally
                {
                    scroll.viewport.offsetMin=originalMin; scroll.viewport.offsetMax=originalMax;
                    RebuildScroll(scroll); scroll.verticalNormalizedPosition=originalPosition; scroll.content.anchoredPosition=originalContent;
                    scroll.inertia=originalInertia; scroll.velocity=originalVelocity; RebuildScroll(scroll);
                }
                try
                {
                    yield return AwaitUiReady(canvas,scroll,button,events,history,"restored normal viewport");
                    yield return WheelButtonIntoView(canvas,scroll,button,events,history,"normal viewport");
                    Vector2 point=ButtonPoint(button); UiRaycastEvidence query=QueryUi(canvas,events,point); List<RaycastResult> hits=query.Events;
                    bool ready=UiReady(canvas,scroll,button,query,true);
                    Assert.IsTrue(ready,ready?null:history+"\n"+UiDiagnostics(canvas,scroll,button,events,point,query));
                    PointerEventData pointer=query.Pointer; pointer.pressPosition=point; pointer.eligibleForClick=true;
                    pointer.pointerCurrentRaycast=hits[0]; pointer.pointerPressRaycast=hits[0];
                    GameObject press=ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerDownHandler);
                    Assert.AreSame(button.gameObject,press); pointer.pointerPress=press; pointer.rawPointerPress=hits[0].gameObject;
                    List<RaycastResult> release=new List<RaycastResult>(); events.RaycastAll(pointer,release);
                    Assert.IsTrue(HitsButton(release,button),history.ToString()); pointer.pointerCurrentRaycast=release[0];
                    Assert.IsTrue(ExecuteEvents.Execute(press,pointer,ExecuteEvents.pointerUpHandler));
                    Assert.IsTrue(ExecuteEvents.Execute(press,pointer,ExecuteEvents.pointerClickHandler));
                    object session=Member(line,"ActivePresentation"); Assert.IsNotNull(session); Assert.IsTrue((bool)Member(session,"IsPresentation"));
                    Assert.IsFalse((bool)Member(console,"PanelOpen")); Assert.AreEqual(CursorLockMode.Locked,PlayerCursor.LockState); Assert.IsFalse(Cursor.visible);
                    Assert.AreEqual(formalBefore,ProtectedState(),"UI presentation start changed formal records/rewards/inventory");
                    Assert.IsFalse((bool)Call(console,"StartPresentation"));
                    yield return null; // closedFrame deliberately blocks gameplay only for the click frame
                    Assert.IsFalse((bool)GameType("C3TreasurePuzzle.C3PuzzleConsole").GetProperty("BlocksGameplayInputThisFrame").GetValue(null));
                    if (input) Assert.AreEqual("Gameplay",Member(input,"Context").ToString());
                    Assert.IsTrue((bool)Call(console,"OpenPanel")); yield return null;
                    yield return AwaitUiReady(canvas,scroll,button,events,history,"busy reopened viewport");
                    Assert.IsFalse(button.interactable); Assert.IsTrue(Member(console,"ProductionStatus").ToString().Contains("展示進行中"));
                    Assert.IsFalse((bool)Call(console,"Confirm")); Assert.IsFalse((bool)Call(console,"ConfirmActivation"));
                    Assert.AreEqual(attempts,Member(Find("C3TreasurePuzzle.C3PuzzleState"),"SubmissionCount")); Assert.AreEqual(1,activations);
                    Debug.Log("R3 synthetic pointer event chain completed; hardware mouse/InputSystem processing still requires manual acceptance.\n"+
                        history+"\n"+UiDiagnostics(canvas,scroll,button,events,point,QueryUi(canvas,events,point)));
                    Call(console,"ClosePanel");
                }
                finally
                {
                    scroll.viewport.offsetMin=originalMin; scroll.viewport.offsetMax=originalMax;
                    RebuildScroll(scroll); scroll.verticalNormalizedPosition=originalPosition; scroll.content.anchoredPosition=originalContent;
                    scroll.inertia=originalInertia; scroll.velocity=originalVelocity; RebuildScroll(scroll);
                }
            }
            finally
            {
                Call(console,"ClosePanel");
                if (eventOwner) eventOwner.SetSelectedGameObject(previousSelection);
                evt.RemoveEventHandler(device,activated);
            }
        }
        [UnityTest] public IEnumerator TenCompleteNativeSessionsPreserveRewardsIdsPhysicsAndBoundedResources()
        {
            yield return FirstProduction(); string protectedState=ProtectedState();
            object[] formalRecords=Items(line,"Materials"),formalBatches=Items(line,"Products");
            string[] formalIds=formalBatches.Select(b=>(string)Member(b,"BatchId")).ToArray();
            Transform[] products=formalBatches.Select(b=>(Transform)Member(b,"Product")).ToArray();
            int models=(int)Member(library,"LoadedModelCount"),materials=(int)Member(library,"LoadedMaterialCount");
            int formalEvents=0; Action<string> handler=id=>formalEvents++;
            EventInfo evt=line.GetType().GetEvent("BatchCompleted");evt.AddEventHandler(line,handler);
            HashSet<string> sessionIds=new HashSet<string>(); int? previousPeak=null;
            try
            {
                using (NativeOutputProbe physics=new NativeOutputProbe())
                {
                    int sceneObjects=Object.FindObjectsOfType<Transform>(true).Count(t=>t.gameObject.scene==line.gameObject.scene);
                    for (int round=1;round<=10;round++)
                    {
                        Assert.IsFalse((bool)Call(line,"TryStartPresentation",console),"Closed console bypassed the presentation boundary");
                        object session=Begin(); string sessionId=(string)Member(session,"SessionId"); Assert.IsTrue(sessionIds.Add(sessionId));
                        Assert.IsTrue(products.All(p=>!p.gameObject.activeInHierarchy));
                        object[] raw=Items(session,"Materials");
                        Dictionary<string,int> ids=raw.ToDictionary(r=>(string)Member(r,"MaterialId"),r=>((Component)Member(r,"Cargo")).GetInstanceID());
                        Dictionary<string,Vector3> lastRaw=new Dictionary<string,Vector3>();
                        Dictionary<string,Vector3> lastProducts=new Dictionary<string,Vector3>();
                        Dictionary<string,int> productIds=new Dictionary<string,int>();
                        Dictionary<string,string[]> productSources=new Dictionary<string,string[]>();
                        Dictionary<string,string> productTypes=new Dictionary<string,string>();
                        Dictionary<string,HashSet<int>> seen=new Dictionary<string,HashSet<int>>();
                        float[] processing=new float[3];
                        for (int tick=0;tick<1600 && Member(line,"ActivePresentation")!=null;tick++)
                        {
                            C3Phase21TestDriver.Tick(line);
                            if (Member(line,"ActivePresentation")==null) break; // final poses are copied before cleanup below
                            foreach (object record in Items(session,"Materials"))
                            {
                                string id=(string)Member(record,"MaterialId"); Component cargo=(Component)Member(record,"Cargo");
                                Assert.AreEqual(ids[id],cargo.GetInstanceID()); Assert.AreEqual(id,Member(cargo,"materialId")); Assert.IsTrue(id.StartsWith(sessionId+"/"));
                                Assert.AreEqual(Member(record,"MaterialType"),Member(cargo,"materialType"));
                                if (Member(record,"State").ToString()=="Spawned") continue;
                                if (lastRaw.ContainsKey(id) && !(bool)Member(record,"ProcessingComplete")) Assert.Less(Vector3.Distance(lastRaw[id],cargo.transform.position),.37f);
                                lastRaw[id]=cargo.transform.position;
                                Assert.AreEqual("RAW_MATERIAL_VISUAL_ROOT",((Transform)Member(record,"Visual")).name);
                                if (Member(record,"State").ToString()=="Processing") processing[(int)Member(record,"TargetMachine")]+=.05f/3f;
                            }
                            foreach (object batch in Items(session,"Products"))
                            {
                                string id=(string)Member(batch,"BatchId"); Transform product=(Transform)Member(batch,"Product");
                                if (lastProducts.ContainsKey(id))
                                {
                                    Assert.Less(Vector3.Distance(lastProducts[id],product.position),.18f); Assert.AreEqual(productIds[id],product.GetInstanceID());
                                    Assert.AreEqual(productTypes[id],Member(batch,"MaterialType").ToString());
                                    CollectionAssert.AreEqual(productSources[id],(string[])Member(batch,"SourceMaterialIds"));
                                }
                                else
                                {
                                    productIds[id]=product.GetInstanceID(); seen[id]=new HashSet<int>();
                                    string kind=Member(batch,"MaterialType").ToString(); int lane=kind=="Paper"?0:kind=="Metal"?1:2;
                                    Assert.IsTrue(id.StartsWith(sessionId+"/")); productTypes[id]=kind;
                                    productSources[id]=(string[])((string[])Member(batch,"SourceMaterialIds")).Clone();
                                    Assert.AreEqual(3,productSources[id].Distinct().Count());
                                    Assert.IsTrue(productSources[id].All(s=>raw.Any(r=>(string)Member(r,"MaterialId")==s && Member(r,"MaterialType").ToString()==kind)));
                                    Assert.GreaterOrEqual(processing[lane],2.44f,"Bale bypassed the real press cycle");
                                    Assert.AreEqual(3,product.GetComponentsInChildren<MeshFilter>().Length);
                                }
                                lastProducts[id]=product.position; seen[id].Add((int)Member(batch,"CurrentRouteSegment"));
                                Assert.IsTrue(((string[])Member(batch,"SourceMaterialIds")).All(s=>ids.ContainsKey(s)));
                            }
                            physics.Frame(session);
                        }
                        Assert.IsNull(Member(line,"ActivePresentation"),"Presentation timed out at round "+round);
                        object summary=Member(line,"LastPresentation"); Assert.AreEqual("Completed",Member(summary,"State").ToString());
                        Assert.AreEqual(9,Items(summary,"Materials").Length); Assert.AreEqual(3,Items(summary,"Products").Length);
                        Assert.AreEqual(0,Member(summary,"ReceiptsCommitted")); Assert.Greater((float)Member(summary,"ElapsedSeconds"),2.5f);
                        Assert.AreEqual(3,Items(summary,"Materials").GroupBy(m=>Member(m,"MaterialType")).Count());
                        Assert.IsTrue(Items(summary,"Materials").GroupBy(m=>Member(m,"MaterialType")).All(g=>g.Count()==3));
                        foreach (object item in Items(summary,"Materials"))
                        {
                            Assert.IsFalse((bool)Member(item,"Counted")); Assert.IsTrue((bool)Member(item,"ProcessingComplete"));
                            CollectionAssert.AreEqual(Enumerable.Range(0,10),Items(item,"History").Select(s=>Convert.ToInt32(s)));
                            Assert.AreEqual(ids[(string)Member(item,"MaterialId")],Member(item,"ObjectId"));
                        }
                        foreach (object product in Items(summary,"Products"))
                        {
                            string id=(string)Member(product,"BatchId"); string kind=Member(product,"MaterialType").ToString();
                            int lane=kind=="Paper"?0:kind=="Metal"?1:2;
                            object model=Items(Find("C3TreasurePuzzle.C3RecyclingLineGeometry"),"Machines")[lane];
                            Vector3 target=((Transform)Member(model,"Collection")).position;
                            Vector3 position=(Vector3)Member(product,"Position");
                            Assert.Less(Vector3.Distance(lastProducts[id],position),.18f,"Final collection step jumped");
                            Assert.AreEqual(productTypes[id],kind); CollectionAssert.AreEqual(productSources[id],(string[])Member(product,"SourceMaterialIds"));
                            Assert.That(position.x,Is.EqualTo(target.x-.65f).Within(.01f)); Assert.That(position.z,Is.EqualTo(target.z).Within(.01f));
                            Assert.IsTrue((bool)Member(product,"Collected") && (bool)Member(product,"OutputReached"));
                            Assert.AreEqual(productIds[id],Member(product,"ObjectId"));
                            CollectionAssert.AreEquivalent(new[] {0,1,2,3},seen[id]);
                        }
                        yield return null; // allow deferred Destroy to finish before counting objects
                        int leftovers=Object.FindObjectsOfType<Transform>(true).Count(t=>t.name.StartsWith("P22_PRESENTATION_")); Assert.AreEqual(0,leftovers);
                        int afterObjects=Object.FindObjectsOfType<Transform>(true).Count(t=>t.gameObject.scene==line.gameObject.scene);
                        Assert.AreEqual(sceneObjects,afterObjects,"GameObjects accumulated after replay round "+round);
                        Assert.AreEqual(0,Items(session,"Materials").Length); Assert.AreEqual(0,Items(session,"Products").Length); Assert.AreEqual(0,Items(session,"Accepted").Length);
                        Assert.IsTrue(Items(session,"Work").All(w=>Items(w,"Slots").All(s=>s==null)));
                        Assert.IsTrue(products.All(p=>p.gameObject.activeSelf)); Assert.AreEqual(protectedState,ProtectedState());
                        CollectionAssert.AreEqual(formalRecords,Items(line,"Materials")); CollectionAssert.AreEqual(formalBatches,Items(line,"Products"));
                        CollectionAssert.AreEqual(formalIds,Items(line,"Products").Select(b=>(string)Member(b,"BatchId")));
                        Assert.AreEqual(models,Member(library,"LoadedModelCount")); Assert.AreEqual(materials,Member(library,"LoadedMaterialCount"));
                        int peak=(int)Member(summary,"MaximumObjectCount"); if (previousPeak.HasValue) Assert.AreEqual(previousPeak.Value,peak); previousPeak=peak;
                        Debug.Log("P22 native replay evidence: round="+round+" peakObjects="+peak+" sceneObjects="+afterObjects+
                            " remainingSessionRoots="+leftovers+" sharedModels="+models+" sharedMaterials="+materials);
                    }
                }
                Assert.AreEqual(0,formalEvents,"Presentation emitted a formal BatchCompleted event");
            }
            finally { evt.RemoveEventHandler(line,handler); }
        }
        [UnityTest] public IEnumerator PauseCloseReopenAndLeavingConsoleDoNotRestartOrCancelTheSession()
        {
            yield return FirstProduction(); object session=Begin();
            for (int i=0;i<1200 && !Items(session,"Work").Any(w=>(bool)Member(w,"Processing"));i++) C3Phase21TestDriver.Tick(line);
            Assert.IsTrue(Items(session,"Work").Any(w=>(bool)Member(w,"Processing")),"No real press phase observed");
            float elapsed=(float)Member(session,"ElapsedSeconds"); Vector3[] positions=Items(session,"Materials").Select(r=>((Component)Member(r,"Cargo")).transform.position).ToArray();
            float[] clocks=Items(session,"Work").Select(w=>(float)Member(w,"Elapsed")).ToArray();
            object[] machines=Items(Find("C3TreasurePuzzle.C3RecyclingLineGeometry"),"Machines");
            Vector3[] presses=machines.Select(m=>((Transform)Member(m,"Press")).position).ToArray();
            float previous=Time.timeScale;
            try
            {
                Time.timeScale=0f; C3Phase21TestDriver.Tick(line,100);
                Assert.AreEqual(elapsed,Member(session,"ElapsedSeconds"));
                CollectionAssert.AreEqual(positions,Items(session,"Materials").Select(r=>((Component)Member(r,"Cargo")).transform.position));
                CollectionAssert.AreEqual(clocks,Items(session,"Work").Select(w=>(float)Member(w,"Elapsed")));
                CollectionAssert.AreEqual(presses,machines.Select(m=>((Transform)Member(m,"Press")).position));
            }
            finally { Time.timeScale=previous; }
            Assert.IsTrue((bool)Call(console,"OpenPanel")); Assert.IsFalse((bool)Call(console,"StartPresentation")); Call(console,"ClosePanel");
            GameObject.Find("M2_Player").transform.position=Vector3.one*64f;
            C3Phase21TestDriver.Tick(line,60); Assert.AreSame(session,Member(line,"ActivePresentation")); Assert.Greater((float)Member(session,"ElapsedSeconds"),elapsed);
            object[] movingProducts=Items(session,"Products"); Assert.Greater(movingProducts.Length,0,"No real output stage observed");
            Vector3[] outputPositions=movingProducts.Select(b=>((Transform)Member(b,"Product")).position).ToArray();
            try
            {
                Time.timeScale=0f; C3Phase21TestDriver.Tick(line,100);
                CollectionAssert.AreEqual(outputPositions,movingProducts.Select(b=>((Transform)Member(b,"Product")).position));
            }
            finally { Time.timeScale=previous; }
            Assert.IsTrue((bool)Call(console,"OpenPanel")); Assert.IsTrue(Member(console,"ProductionStatus").ToString().Contains("展示進行中")); Call(console,"ClosePanel");
            if (input)
            {
                Assert.IsTrue((bool)Call(console,"OpenPanel")); object gameplay=Member(input,"Context");
                string nonGameplay=Enum.GetNames(gameplay.GetType()).First(n=>n!="Gameplay");
                Call(input,"SetContext",Enum.Parse(gameplay.GetType(),nonGameplay)); Call(console,"Update");
                Assert.IsFalse((bool)Member(console,"PanelOpen")); Assert.IsFalse((bool)Call(console,"StartPresentation"));
                Call(input,"SetContext",gameplay);
            }
            Assert.AreSame(session,Member(line,"ActivePresentation"));
            yield return null;
        }
        [UnityTest] public IEnumerator DisableAndCancellationRestoreFormalActiveRendererAndColliderStates()
        {
            yield return FirstProduction(); Transform product=(Transform)Member(Items(line,"Products")[0],"Product");
            Renderer renderer=product.GetComponentInChildren<Renderer>(); renderer.enabled=false;
            Collider collider=product.GetComponent<Collider>(); Assert.IsTrue(collider.enabled);
            object session=Begin(); Assert.IsFalse(product.gameObject.activeInHierarchy);
            ((Behaviour)line).enabled=false;
            Assert.IsNull(Member(line,"ActivePresentation")); Assert.AreEqual("Cancelled",Member(line,"PresentationStatus").ToString());
            Assert.IsTrue(product.gameObject.activeSelf); Assert.IsFalse(renderer.enabled); Assert.IsTrue(collider.enabled);
            Assert.AreEqual(0,Items(session,"Accepted").Length); yield return null;
            Assert.AreEqual(0,Object.FindObjectsOfType<Transform>(true).Count(t=>t.name.StartsWith("P22_PRESENTATION_")));
            ((Behaviour)line).enabled=true; renderer.enabled=true;
            Begin(); Call(line,"CancelPresentation"); Assert.IsTrue(product.gameObject.activeSelf);
        }
        [UnityTest] public IEnumerator FailedNativeRouteCleansTheSessionAndRestoresFormalProducts()
        {
            yield return FirstProduction(); object session=Begin();
            Collider[] surfaces=Items(Find("C3TreasurePuzzle.C3RecyclingLineGeometry"),"CommonSections").Select(s=>(Collider)Member(s,"Surface")).ToArray();
            try
            {
                foreach (Collider surface in surfaces) surface.enabled=false; Physics.SyncTransforms();
                LogAssert.Expect(LogType.Exception,new Regex("InvalidOperationException: Material lost its physical conveyor"));
                C3Phase21TestDriver.Tick(line,10);
                Assert.IsNull(Member(line,"ActivePresentation")); Assert.AreEqual("Failed",Member(line,"PresentationStatus").ToString());
                Assert.AreEqual(0,Items(session,"Accepted").Length);
                Assert.IsTrue(Items(line,"Products").All(b=>((Transform)Member(b,"Product")).gameObject.activeSelf));
            }
            finally { foreach (Collider surface in surfaces) if (surface) surface.enabled=true; Physics.SyncTransforms(); }
            yield return null;
        }
        [UnityTest] public IEnumerator LevelCompletionCancelsReplayAndKeepsCompletionInputPriority()
        {
            yield return FirstProduction(); Begin(); Assert.IsTrue((bool)Call(console,"OpenPanel"));
            Assert.IsTrue((bool)Call(device,"TryCompleteAtExit")); Call(console,"Update");
            Assert.IsNull(Member(line,"ActivePresentation")); Assert.IsTrue((bool)Member(device,"LevelCompleted"));
            Assert.IsTrue((bool)Member(Find("C3TreasurePuzzle.C3CompletionScreen"),"Visible"));
            Assert.IsFalse((bool)Member(console,"PanelOpen")); Assert.IsFalse((bool)Call(console,"OpenPanel"));
            Assert.AreEqual(CursorLockMode.None,PlayerCursor.LockState); Assert.IsTrue(Cursor.visible); yield return null;
        }
        [UnityTest] public IEnumerator SceneReloadDropsAllReplayStateAndKeepsSharedAssetsBounded()
        {
            yield return FirstProduction(); object session=Begin(); GameObject oldRoot=((Transform)Member(session,"ObjectRoot")).gameObject;
            yield return SceneManager.LoadSceneAsync("Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity",LoadSceneMode.Single); yield return null;
            Assert.IsTrue(oldRoot==null); Component next=Find("C3TreasurePuzzle.C3RecyclingLine");
            Assert.IsNull(Member(next,"ActivePresentation")); Assert.IsNull(Member(next,"LastPresentation"));
            Assert.AreEqual(0,Object.FindObjectsOfType<Transform>(true).Count(t=>t.name.StartsWith("P22_PRESENTATION_")));
            Assert.AreEqual(0,Items(next,"Products").Length); Assert.AreEqual("Dormant",Member(Find("C3TreasurePuzzle.C3DeviceState"),"FactoryState").ToString());
        }
    }
}
