using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
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
            line=Find("C3TreasurePuzzle.C3RecyclingLine"); device=Find("C3TreasurePuzzle.C3DeviceState");
            console=Find("C3TreasurePuzzle.C3PuzzleConsole"); runtime=Find("EcoAdventureRuntime"); library=Find("C3TreasurePuzzle.C3RecyclingVisuals");
            Type inputType=Type.GetType("SDGAdventure.SharedPlayer.PlayerInputService, SDGAdventure.SharedPlayer",true);
            input=inputType.GetProperty("Instance").GetValue(null) as Component;
            if (input) Call(input,"SetContext",Enum.Parse(Member(input,"Context").GetType(),"Gameplay"));
        }
        [UnityTearDown] public IEnumerator Close() { if (scope!=null) yield return scope.Close(); }
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
            Assert.AreEqual(CursorLockMode.None,Cursor.lockState); Assert.IsTrue(Cursor.visible);
            yield return null;
        }
        [UnityTest] public IEnumerator ProductionUguiReceivesARealClickWithoutReactivationOrPuzzleSubmission()
        {
            int activations=0; Action activated=()=>activations++;
            EventInfo evt=device.GetType().GetEvent("Activated"); evt.AddEventHandler(device,activated);
            try
            {
                yield return FirstProduction();
                int attempts=(int)Member(Find("C3TreasurePuzzle.C3PuzzleState"),"SubmissionCount");
                Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false;
                Assert.IsTrue((bool)Call(console,"OpenPanel")); yield return null;
                Transform content=GameObject.Find("C3UiCanvas").transform.Find("PuzzlePanel/Viewport/Content");
                Assert.AreEqual("回收產線控制",content.Find("Title").GetComponent<Text>().text);
                foreach (string name in new[] {"Choice0","Choice1","Choice2","Confirm","Retry","Activate"}) Assert.IsFalse(content.Find(name).gameObject.activeSelf,name);
                Button button=content.Find("Presentation").GetComponent<Button>(); Assert.IsTrue(button.interactable);
                Canvas.ForceUpdateCanvases();
                Vector2 point=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
                EventSystem events=Object.FindObjectOfType<EventSystem>(); Assert.IsNotNull(events);
                PointerEventData pointer=new PointerEventData(events) {position=point}; List<RaycastResult> hits=new List<RaycastResult>(); events.RaycastAll(pointer,hits);
                Assert.IsNotEmpty(hits); Assert.AreEqual(button.gameObject,hits[0].gameObject);
                ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
                Assert.IsNotNull(Member(line,"ActivePresentation")); Assert.IsFalse((bool)Member(console,"PanelOpen"));
                Assert.AreEqual(CursorLockMode.Locked,Cursor.lockState);
                Assert.IsFalse((bool)Call(console,"StartPresentation"));
                Assert.IsTrue((bool)Call(console,"OpenPanel")); yield return null;
                Assert.IsFalse(button.interactable); Assert.IsTrue(Member(console,"ProductionStatus").ToString().Contains("展示進行中"));
                Assert.IsFalse((bool)Call(console,"Confirm")); Assert.IsFalse((bool)Call(console,"ConfirmActivation"));
                Assert.AreEqual(attempts,Member(Find("C3TreasurePuzzle.C3PuzzleState"),"SubmissionCount")); Assert.AreEqual(1,activations);
                Call(console,"ClosePanel");
            }
            finally { evt.RemoveEventHandler(device,activated); }
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
            Assert.AreEqual(CursorLockMode.None,Cursor.lockState); Assert.IsTrue(Cursor.visible); yield return null;
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
