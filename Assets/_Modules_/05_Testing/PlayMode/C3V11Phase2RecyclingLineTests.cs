using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace M6IntegrationTests
{
    // Reuse the accepted fixture's persistent-inventory/input/global isolation.
    // Manual ticks call the SAME production integrator on real scene equipment;
    // native physics queries below are never replaced by calculated constants.
    public sealed class C3V11Phase2RecyclingLineTests
    {
        private const string ScenePath="Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private const BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private C3V11SafeCirculationTests scope;
        private Component line,geometry,device,runtime;
        internal static Type GameType(string name) => Type.GetType(name+", Assembly-CSharp",true);
        internal static Component Find(string name) => Object.FindObjectOfType(GameType(name)) as Component;
        internal static object Member(object owner,string name)
        {
            Assert.IsNotNull(owner,"Missing owner for "+name);
            Type type=owner.GetType(); PropertyInfo property=type.GetProperty(name,Any);
            if (property!=null) return property.GetValue(owner);
            FieldInfo field=type.GetField(name,Any); Assert.IsNotNull(field,type.FullName+"."+name);
            return field.GetValue(owner);
        }
        private static void Set(object owner,string name,object value)
        {
            FieldInfo field=owner.GetType().GetField(name,Any);
            Assert.IsNotNull(field,owner.GetType().FullName+"."+name); field.SetValue(owner,value);
        }
        internal static object Call(object owner,string name,params object[] args)
        {
            MethodInfo method=owner.GetType().GetMethod(name,Any,null,args.Select(a=>a.GetType()).ToArray(),null);
            Assert.IsNotNull(method,owner.GetType().FullName+"."+name+" signature");
            return method.Invoke(owner,args);
        }
        internal static object[] Items(object owner,string name) => ((IEnumerable)Member(owner,name)).Cast<object>().ToArray();
        private object Kind(string name) => Enum.Parse(GameType("EcoRecycleCargo+Kind"),name);
        private object[] Materials => Items(line,"Materials");
        private object[] Products => Items(line,"Products");
        private int Count(string name) => (int)Member(line,name);
        private void Capture()
        {
            line=Find("C3TreasurePuzzle.C3RecyclingLine");
            geometry=Find("C3TreasurePuzzle.C3RecyclingLineGeometry");
            device=Find("C3TreasurePuzzle.C3DeviceState");
            runtime=GameType("EcoAdventureRuntime").GetProperty("Instance").GetValue(null) as Component;
            Assert.IsNotNull(line); Assert.IsNotNull(geometry); Assert.IsNotNull(runtime);
            ((Behaviour)line).enabled=false; Set(line,"useFiniteTestSupply",false);
            Set(device,"useTestStartupDelay",false);
        }
        [UnitySetUp] public IEnumerator Open()
        {
            scope=new C3V11SafeCirculationTests();
            yield return scope.OpenFactory(); Capture(); Physics.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator Close()
        {
            if (scope!=null) yield return scope.Cleanup();
        }
        private void Step(int ticks=1)
        {
            for (int i=0;i<ticks;i++) Call(line,"AdvanceSimulation",.05f);
            Physics.SyncTransforms();
        }
        private IEnumerator Until(Func<bool> condition,string reason,int limit=1600)
        {
            for (int i=0;i<limit && !condition();i++) { Step(); yield return null; }
            Assert.IsTrue(condition(),reason+"; blocked="+Member(line,"LastBlockedReason"));
        }
        private void Supply(string kind,int units=3,string prefix="input")
        {
            for (int i=0;i<units;i++) Assert.IsTrue((bool)Call(line,"TrySupply",prefix+"-"+i,Kind(kind)));
        }
        private void Activate(bool run=true)
        {
            Component progress=Find("C3TreasurePuzzle.C3TreasureProgress");
            foreach (string id in new[] {"fuse","control_chip","filter_module"})
                Assert.IsTrue((bool)Call(progress,"TryCollect",id,id));
            Type step=GameType("C3TreasurePuzzle.C3ProcessingStep");
            Array answer=Array.CreateInstance(step,3);
            string[] names={"Sorting","Recycling","PollutionTreatment"};
            for (int i=0;i<3;i++) answer.SetValue(Enum.Parse(step,names[i]),i);
            Assert.IsTrue((bool)Call(Find("C3TreasurePuzzle.C3PuzzleState"),"SubmitAnswer",answer));
            Assert.IsTrue((bool)Call(device,"TryActivate"));
            Assert.AreEqual("Starting",Member(device,"FactoryState").ToString());
            if (run) { Call(device,"NotifyStartupFinished"); Assert.AreEqual("Running",Member(device,"FactoryState").ToString()); }
        }
        // Probes are enabled triggers, physically outside the factory. Explicit
        // ComputePenetration poses never move them into gameplay or apply forces.
        private static readonly Vector3 ProbeParking=new Vector3(128f,128f,128f);
        private const float ContactLift=.02f,ContactSweep=.04f,MaxSupportGap=.003f;
        private const float RawPenetrationLimit=.001f,ProductPenetrationLimit=.01f;
        private static void Require(bool condition,string reason,Func<string> details)
        {
            if (!condition) Assert.Fail(reason+"\n"+details());
        }
        private static string ColliderPath(Collider collider)
        {
            string path=collider.name;
            for (Transform p=collider.transform.parent;p;p=p.parent) path=p.name+"/"+path;
            return path;
        }
        private static Collider[] RouteSupports(object owner)
        {
            object[] legs=Items(Member(owner,"Path"),"Legs");
            int segment=(int)Member(owner,"CurrentRouteSegment");
            return legs.Skip(Mathf.Max(0,segment-1)).Take(segment==0?2:3)
                .Select(s=>(Collider)Member(s,"Support")).Distinct().ToArray();
        }
        private static Bounds ProductEnvelope(Transform product)
        {
            MeshFilter[] meshes=product.GetComponentsInChildren<MeshFilter>();
            Assert.Greater(meshes.Length,0);
            Bounds envelope=new Bounds(Vector3.zero,Vector3.zero);
            foreach (MeshFilter mesh in meshes)
                foreach (Vector3 vertex in mesh.sharedMesh.vertices)
                    envelope.Encapsulate(product.InverseTransformPoint(mesh.transform.TransformPoint(vertex)));
            Assert.Less(Vector3.Distance(product.lossyScale,Vector3.one),.001f,"Probe assumes production's unit-scale product root");
            return envelope;
        }
        private static Dictionary<Collider,float> BoxContactGaps(BoxCollider shape,Vector3 position,Quaternion rotation,
            Collider[] supports,QueryTriggerInteraction triggers=QueryTriggerInteraction.Ignore)
        {
            // A cast measures separation without requiring an overlap. Restrict
            // support to actual finite current/adjacent transport surfaces.
            Vector3 center=position+rotation*shape.center;
            RaycastHit[] hits=Physics.BoxCastAll(center+Vector3.up*ContactLift,shape.size*.5f,
                Vector3.down,rotation,ContactSweep,~0,triggers);
            Dictionary<Collider,float> gaps=supports.Distinct().ToDictionary(s=>s,s=>float.PositiveInfinity);
            foreach (RaycastHit hit in hits)
                if (gaps.ContainsKey(hit.collider)) gaps[hit.collider]=Mathf.Min(gaps[hit.collider],hit.distance-ContactLift);
            return gaps;
        }
        private static bool HasSupport(Dictionary<Collider,float> gaps) =>
            gaps.Values.Any(g=>g>=-ProductPenetrationLimit && g<=MaxSupportGap);
        private static string GapText(float gap) => float.IsPositiveInfinity(gap)
            ?"no finite-surface hit in 40mm sweep":gap.ToString("F6")+"m vertical gap";
        private static string Penetrations(Collider probe,Vector3 position,Quaternion rotation,IEnumerable<Collider> obstacles)
        {
            StringBuilder text=new StringBuilder();
            foreach (Collider obstacle in obstacles.Distinct())
                if (Physics.ComputePenetration(probe,position,rotation,obstacle,obstacle.transform.position,
                    obstacle.transform.rotation,out Vector3 direction,out float depth))
                    text.AppendLine(ColliderPath(obstacle)+" depth="+depth.ToString("F6")+"m direction="+direction.ToString("F6"));
            return text.Length==0?"none":text.ToString();
        }
        private static string ValidateSphereProbe(SphereCollider sphere,BoxCollider box)
        {
            box.center=Vector3.zero; box.size=Vector3.one;
            Physics.SyncTransforms();
            bool overlap=Physics.ComputePenetration(sphere,Vector3.up*.60f,Quaternion.identity,
                box,Vector3.zero,Quaternion.identity,out Vector3 direction,out float depth);
            Assert.IsTrue(overlap,"Enabled raw SphereCollider positive overlap control failed");
            Assert.That(depth,Is.EqualTo(.02f).Within(.0002f));
            Assert.Greater(Vector3.Dot(direction,Vector3.up),.99f);
            Assert.IsFalse(Physics.ComputePenetration(sphere,Vector3.up*.63f,Quaternion.identity,
                box,Vector3.zero,Quaternion.identity,out _,out _),"Separated raw probe control failed");
            return "enabled sphere: overlap depth="+depth.ToString("F6")+"m; separated=false";
        }
        private static string ValidateBoxProbe(BoxCollider shape)
        {
            GameObject control=new GameObject("P2_TEST_QUERY_CONTROL");
            control.transform.position=ProbeParking+Vector3.right*8f;
            StringBuilder observations=new StringBuilder();
            try
            {
                BoxCollider support=control.AddComponent<BoxCollider>(); support.isTrigger=true;
                shape.center=Vector3.zero; shape.size=Vector3.one;
                foreach (bool enabled in new[] {true,false})
                {
                    shape.enabled=enabled; Physics.SyncTransforms();
                    bool overlap=Physics.ComputePenetration(shape,Vector3.up*.9f,Quaternion.identity,
                        support,Vector3.zero,Quaternion.identity,out Vector3 direction,out float depth);
                    bool separated=Physics.ComputePenetration(shape,Vector3.up*1.1f,Quaternion.identity,
                        support,Vector3.zero,Quaternion.identity,out _,out float separatedDepth);
                    bool touching=Physics.ComputePenetration(shape,Vector3.up,Quaternion.identity,
                        support,Vector3.zero,Quaternion.identity,out _,out float touchDepth);
                    bool smallGap=Physics.ComputePenetration(shape,Vector3.up*1.001f,Quaternion.identity,
                        support,Vector3.zero,Quaternion.identity,out _,out float gapDepth);
                    observations.AppendLine("enabled="+enabled+" overlap="+overlap+" depth="+depth.ToString("F6")+
                        " direction="+direction.ToString("F6")+" tangent="+touching+" tangentDepth="+touchDepth.ToString("F6")+
                        " separatedOverlap="+separated+" separatedDepth="+separatedDepth.ToString("F6")+
                        " 1mmGapOverlap="+smallGap+" gapDepth="+gapDepth.ToString("F6"));
                    // Observe disabled behaviour rather than assume it matches
                    // enabled geometry. Only the calibrated ENABLED probe is used
                    // for all subsequent scene penetration verdicts.
                    if (enabled)
                    {
                        Assert.IsTrue(overlap,"Enabled BoxCollider positive overlap control failed; "+observations);
                        Assert.That(depth,Is.EqualTo(.1f).Within(.0002f));
                        Assert.Greater(Vector3.Dot(direction,Vector3.up),.99f);
                        Assert.IsFalse(separated,"Separated BoxCollider control failed; "+observations);
                        if (touching) Assert.LessOrEqual(touchDepth,.0002f,"Tangent boxes must not have substantial penetration");
                        Assert.IsFalse(smallGap,"1mm gap must not be penetration; "+observations);
                    }
                }
                shape.enabled=true; Physics.SyncTransforms();
                foreach (float gap in new[] {0f,.001f,.01f})
                {
                    Dictionary<Collider,float> measured=BoxContactGaps(shape,support.transform.position+Vector3.up*(1f+gap),
                        Quaternion.identity,new Collider[] {support},QueryTriggerInteraction.Collide);
                    Assert.That(measured[support],Is.EqualTo(gap).Within(.0002f),"Finite-surface cast calibration; "+observations);
                    Assert.AreEqual(gap<=MaxSupportGap,HasSupport(measured),"Contact / 1mm gap / 10mm floating control; "+observations);
                    observations.AppendLine("cast expectedGap="+gap.ToString("F6")+" actual="+GapText(measured[support]));
                }
                return observations.ToString();
            }
            finally { shape.enabled=true; Object.DestroyImmediate(control); }
        }
        private string CargoDetails(object record,SphereCollider sphere,string calibration)
        {
            Component cargo=(Component)Member(record,"Cargo");
            Vector3 center=cargo.transform.position;
            object[] legs=Items(Member(record,"Path"),"Legs");
            int segment=(int)Member(record,"CurrentRouteSegment");
            Collider[] obstacles=Physics.OverlapSphere(center,sphere.radius,~0,QueryTriggerInteraction.Ignore)
                .Where(c=>!c.transform.IsChildOf(cargo.transform)).ToArray();
            StringBuilder text=new StringBuilder("MaterialId="+Member(record,"MaterialId")+" type="+Member(record,"MaterialType")+
                " state="+Member(record,"State")+" segment="+segment+" object="+cargo.name+
                " center="+center.ToString("F6")+" radius="+sphere.radius.ToString("F6")+"\n");
            for (int i=Mathf.Max(0,segment-1);i<=Mathf.Min(legs.Length-1,segment+1);i++)
            {
                Collider support=(Collider)Member(legs[i],"Support");
                text.AppendLine("leg="+i+" support="+ColliderPath(support)+" start="+((Vector3)Member(legs[i],"A")).ToString("F6")+
                    " end="+((Vector3)Member(legs[i],"B")).ToString("F6")+
                    " surfaceGap="+(Vector3.Distance(center,support.ClosestPoint(center))-sphere.radius).ToString("F6"));
            }
            text.AppendLine("Actual penetrations: "+Penetrations(sphere,center,cargo.transform.rotation,obstacles));
            return text.ToString()+"\nProbe calibration: "+calibration;
        }
        private static void CheckCargoVisual(SphereCollider sphere,Transform cargo,BoxCollider probe,MeshFilter visual,Func<string> details)
        {
            // These authored primitives are boxes. Use their mesh-local box and
            // actual rotation, not a world AABB, including non-solid moving parts.
            probe.center=Vector3.Scale(visual.sharedMesh.bounds.center,visual.transform.lossyScale);
            probe.size=Vector3.Scale(visual.sharedMesh.bounds.size,visual.transform.lossyScale);
            bool overlap=Physics.ComputePenetration(sphere,cargo.position,cargo.rotation,probe,
                visual.transform.position,visual.transform.rotation,out Vector3 direction,out float depth);
            Require(!overlap || depth<=RawPenetrationLimit,"Cargo penetrates visible "+visual.name+
                " depth="+depth.ToString("F6")+"m direction="+direction.ToString("F6"),details);
        }
        private string OutputDetails(object batch,object machine,BoxCollider shape,string calibration)
        {
            Transform product=(Transform)Member(batch,"Product");
            object[] sections=Items(machine,"OutputSections");
            object[] legs=Items(Member(batch,"Path"),"Legs");
            Collider[] surfaces=sections.Select(s=>(Collider)Member(s,"Surface")).Concat(new[] {(Collider)Member(machine,"ProcessBed")}).ToArray();
            Dictionary<Collider,float> gaps=BoxContactGaps(shape,product.position,product.rotation,surfaces);
            Collider[] obstacles=Physics.OverlapBox(product.TransformPoint(shape.center),shape.size*.5f,product.rotation,~0,QueryTriggerInteraction.Ignore)
                .Where(c=>!c.transform.IsChildOf(product)).ToArray();
            StringBuilder text=new StringBuilder("BatchId="+Member(batch,"BatchId")+" type="+Member(batch,"MaterialType")+
                " segment="+Member(batch,"CurrentRouteSegment")+" object="+product.name+
                " position="+product.position.ToString("F6")+" rotation="+product.rotation.ToString("F6")+
                " envelopeCenter="+shape.center.ToString("F6")+" envelopeSize="+shape.size.ToString("F6")+"\n");
            for (int i=0;i<sections.Length;i++)
            {
                Component section=(Component)sections[i]; Collider surface=(Collider)Member(section,"Surface");
                text.AppendLine("section="+i+" "+section.name+" start="+((Transform)Member(section,"Start")).position.ToString("F6")+
                    " end="+((Transform)Member(section,"End")).position.ToString("F6")+
                    " actualPathStart="+((Vector3)Member(legs[i],"A")).ToString("F6")+" actualPathEnd="+((Vector3)Member(legs[i],"B")).ToString("F6")+
                    " boundsCenter="+surface.bounds.center.ToString("F6")+" boundsSize="+surface.bounds.size.ToString("F6")+" "+GapText(gaps[surface]));
            }
            text.AppendLine("PROCESS_BED: "+GapText(gaps[(Collider)Member(machine,"ProcessBed")]));
            text.AppendLine("Actual penetrations: "+Penetrations(shape,product.position,product.rotation,obstacles));
            return text.ToString()+"\nProbe calibration: "+calibration;
        }
        internal static void AssertEquipmentConnections(Component geometry)
        {
            object[] sections=Items(geometry,"Sections");
            Assert.AreEqual(30,sections.Length,"Missing or duplicate transport sections");
            foreach (Component section in sections)
            {
                Collider surface=(Collider)Member(section,"Surface");
                Renderer renderer=(Renderer)Member(section,"SurfaceRenderer");
                Transform a=(Transform)Member(section,"Start"),b=(Transform)Member(section,"End");
                Assert.IsTrue(surface.enabled && !surface.isTrigger && surface.gameObject.activeInHierarchy,section.name);
                Assert.IsTrue(renderer.enabled,section.name);
                Assert.IsNotNull(renderer.sharedMaterial.shader,section.name);
                Assert.IsTrue(renderer.sharedMaterial.shader.isSupported,section.name+" shader");
                Assert.Less(Vector3.Distance(renderer.bounds.center,surface.bounds.center),.01f,section.name);
                Assert.Less(Vector3.Distance(renderer.bounds.size,surface.bounds.size),.01f,section.name);
                Assert.Greater((float)Member(section,"Width"),.75f,section.name);
                foreach (float t in new[] {0f,.25f,.5f,.75f,1f})
                {
                    Vector3 point=Vector3.Lerp(a.position,b.position,t);
                    Vector3 normal=surface.transform.up;
                    // Query 1mm inside the finite end face to avoid a ray exactly
                    // on a floating-point box edge; still compare to the real endpoint.
                    Vector3 inset=(b.position-a.position).normalized*(t==0f?.001f:t==1f?-.001f:0f);
                    Assert.IsTrue(surface.Raycast(new Ray(point+inset+normal*.3f,-normal),out RaycastHit hit,.6f),section.name);
                    Assert.Less(Vector3.Distance(hit.point,point),.01f,"Endpoint/mesh top mismatch "+section.name);
                }
            }
            Action<object,object> joint=(left,right)=>
            {
                Vector3 p=((Transform)Member(left,"End")).position;
                Vector3 q=((Transform)Member(right,"Start")).position;
                Assert.Less(Vector3.Distance(p,q),.01f,"Endpoint gap");
                Collider a=(Collider)Member(left,"Surface"),b=(Collider)Member(right,"Surface");
                Collider[] touching=Physics.OverlapSphere((p+q)*.5f+Vector3.up*.01f,.025f,~0,QueryTriggerInteraction.Ignore);
                Assert.Contains(a,touching,"First actual surface missing at joint");
                Assert.Contains(b,touching,"Second actual surface missing at joint");
            };
            object[] common=Items(geometry,"CommonSections");
            for (int i=1;i<common.Length;i++) joint(common[i-1],common[i]);
            joint(common[common.Length-1],Member(geometry,"MainBelt"));
            Collider main=(Collider)Member(Member(geometry,"MainBelt"),"Surface");
            foreach (object machine in Items(geometry,"Machines"))
            {
                Vector3 gate=((Transform)Member(Member(machine,"Chute"),"Start")).position;
                Assert.Less(Vector3.Distance(main.ClosestPoint(gate),gate),.01f,"Diversion does not meet real main belt");
                object[] input=Items(machine,"InputSections");
                for (int i=1;i<input.Length;i++) joint(input[i-1],input[i]);
                object[] output=Items(machine,"OutputSections");
                for (int i=1;i<output.Length;i++) joint(output[i-1],output[i]);
                Assert.Less(Vector3.Distance(((Collider)Member(machine,"ProcessBed")).ClosestPoint(
                    ((Transform)Member(Member(machine,"ReceivingBed"),"End")).position),
                    ((Transform)Member(Member(machine,"ReceivingBed"),"End")).position),.01f,"Input misses processing bed");
            }
        }
        [UnityTest] public IEnumerator ElevatedInfeed()
        {
            Transform entry=(Transform)Member(geometry,"BMaterialInterface");
            Assert.Greater(entry.position.y,6.8f); Assert.Greater(entry.position.x,0f);
            object first=Items(geometry,"CommonSections")[0];
            Collider belt=(Collider)Member(first,"Surface");
            Assert.That(belt.bounds.max.y,Is.EqualTo(entry.position.y).Within(.01f));
            Assert.Greater(belt.bounds.size.z,1f);
            AssertEquipmentConnections(geometry); yield break;
        }
        [UnityTest] public IEnumerator WallOpeningClearance()
        {
            Vector3 a=new Vector3(.7f,7.12f,12.5f);
            Transform proxies=GameObject.Find("C_CollisionProxies").transform;
            foreach (RaycastHit hit in Physics.SphereCastAll(a,.12f,Vector3.left,2f,~0,QueryTriggerInteraction.Ignore))
                Assert.IsFalse(hit.collider.transform.IsChildOf(proxies),"Upper material route hits wall proxy "+hit.collider.name);
            foreach (string name in new[] {"C_Wall_4_Lintel","C_Wall_4_Near","C_Wall_4_Far","B_Door_Floor",
                "B_Safety_End","B_Safety_Near","B_Safety_Far","P2_WALL_HIGH_NEAR","P2_WALL_HIGH_FAR","P2_WALL_HIGH_HEADER"})
                Assert.IsTrue(GameObject.Find(name).GetComponent<Collider>().enabled,name);
            Assert.IsNotNull(GameObject.Find("V11_BC_PVC_CURTAIN"));
            yield return scope.GroundBCDoorApproachRemainsOpenUnderTheEastFlight();
        }
        [UnityTest] public IEnumerator ContinuousConveyorRoute()
        { AssertEquipmentConnections(geometry); yield break; }
        [UnityTest] public IEnumerator IngressIdentityAndTriggerDedup()
        {
            GameObject upstream=new GameObject("P2_TEST_UPSTREAM_MATERIAL");
            upstream.transform.position=((Transform)Member(geometry,"BMaterialInterface")).position+Vector3.up*.12f;
            GameObject mesh=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mesh.transform.SetParent(upstream.transform,false); mesh.transform.localScale=Vector3.one*.24f;
            mesh.GetComponent<Collider>().isTrigger=true;
            Component cargo=upstream.AddComponent(GameType("EcoRecycleCargo"));
            Set(cargo,"materialId","external-material"); Set(cargo,"materialType",Kind("Metal"));
            GameObject detector=new GameObject("P2_TEST_INGRESS_DETECTOR");
            bool transferred=false;
            try
            {
                Component sorter=detector.AddComponent(GameType("EcoConveyorSorter"));
                detector.transform.position=((Transform)Member(geometry,"BMaterialInterface")).position;
                BoxCollider gate=detector.AddComponent<BoxCollider>(); gate.isTrigger=true;
                gate.center=Vector3.up*.12f; gate.size=new Vector3(.30f,.40f,.50f);
                Rigidbody body=detector.AddComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
                Physics.SyncTransforms(); int id=cargo.GetInstanceID();
                yield return new WaitForFixedUpdate(); yield return null;
                transferred=Materials.Length==1;
                Assert.IsTrue(transferred,"Native OnTriggerEnter did not transfer upstream material");
                Assert.AreEqual(id,((Component)Member(Materials.Single(),"Cargo")).GetInstanceID(),"Ingress must transfer the same object");
                Assert.IsFalse((bool)Call(sorter,"TrySort",cargo));
                Assert.AreEqual(1,Count("AcceptedUnits")); Assert.AreEqual(0,Count("ProcessedUnits"));
                Assert.AreEqual(0,Member(runtime,"RecyclingReceiptCount"));
            }
            finally { Object.DestroyImmediate(detector); if (!transferred) Object.DestroyImmediate(upstream); }
            yield break;
        }
        [UnityTest] public IEnumerator CargoFollowsRoute()
        {
            GameObject probes=new GameObject("P2_TEST_RAW_PROBES"); probes.transform.position=ProbeParking;
            try
            {
                SphereCollider sphere=probes.AddComponent<SphereCollider>(); sphere.isTrigger=true;
                sphere.radius=(float)GameType("C3TreasurePuzzle.C3RecyclingLineGeometry").GetField("RawRadius").GetRawConstantValue();
                Assert.That(sphere.radius,Is.EqualTo(.12f).Within(.000001f));
                GameObject visualProbe=new GameObject("P2_TEST_VISUAL_BOX"); visualProbe.transform.SetParent(probes.transform,false);
                visualProbe.transform.localPosition=Vector3.right*4f;
                BoxCollider box=visualProbe.AddComponent<BoxCollider>(); box.isTrigger=true;
                string calibration=ValidateSphereProbe(sphere,box);
                foreach (string kind in new[] {"Paper","Metal","Plastic"}) Supply(kind,1,"route-"+kind);
                object[] records=Materials;
                Dictionary<object,string> ids=records.ToDictionary(r=>r,r=>(string)Member(r,"MaterialId"));
                Dictionary<object,string> types=records.ToDictionary(r=>r,r=>Member(r,"MaterialType").ToString());
                Dictionary<object,int> identities=records.ToDictionary(r=>r,r=>((Component)Member(r,"Cargo")).GetInstanceID());
                Dictionary<object,Vector3> positions=records.ToDictionary(r=>r,r=>((Component)Member(r,"Cargo")).transform.position);
                MeshFilter[] visuals=Items(geometry,"Machines").SelectMany(m=>((Transform)Member(m,"Root")).GetComponentsInChildren<MeshFilter>())
                    .Where(m=>new[] {"IDENTIFIER_UPRIGHT","IDENTIFIER_GANTRY","IDENTIFIER_SENSOR","COMPRESSION_PRESS","DIVERT_PUSHER"}.Contains(m.name)).ToArray();
                Activate();
                for (int i=0;i<600 && records.Any(r=>Member(r,"State").ToString()!="MachineInput");i++)
                {
                    Step();
                    foreach (object record in records)
                    {
                        Component cargo=(Component)Member(record,"Cargo");
                        Func<string> details=()=>CargoDetails(record,sphere,calibration);
                        Require(identities[record]==cargo.GetInstanceID() && ids[record]==(string)Member(record,"MaterialId") &&
                            ids[record]==(string)Member(cargo,"materialId"),"Raw identity changed",details);
                        Require(types[record]==Member(record,"MaterialType").ToString() && types[record]==Member(cargo,"materialType").ToString(),
                            "Raw material type changed",details);
                        Require(Vector3.Distance(positions[record],cargo.transform.position)<.37f,"Cargo jumped between route segments",details);
                        positions[record]=cargo.transform.position;
                        if (Member(record,"State").ToString()=="Spawned") continue;
                        Collider[] supports=RouteSupports(record);
                        float contact=supports.Min(s=>Mathf.Abs(Vector3.Distance(cargo.transform.position,s.ClosestPoint(cargo.transform.position))-sphere.radius));
                        Require(contact<.01f,"Cargo lost its actual current/adjacent conveyor support",details);
                        foreach (Collider obstacle in Physics.OverlapSphere(cargo.transform.position,sphere.radius,~0,QueryTriggerInteraction.Ignore))
                            if (!obstacle.transform.IsChildOf(cargo.transform) && Physics.ComputePenetration(sphere,cargo.transform.position,
                                cargo.transform.rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out Vector3 direction,out float depth))
                                Require(depth<=RawPenetrationLimit,"Cargo penetrates "+ColliderPath(obstacle)+
                                    " depth="+depth.ToString("F6")+"m direction="+direction.ToString("F6"),details);
                        foreach (MeshFilter visual in visuals) CheckCargoVisual(sphere,cargo.transform,box,visual,details);
                    }
                    yield return null;
                }
                foreach (object record in records)
                {
                    Require(Member(record,"State").ToString()=="MachineInput","Raw material did not reach its machine",()=>CargoDetails(record,sphere,calibration));
                    int lane=(int)Member(record,"TargetMachine");
                    Assert.AreEqual(Member(record,"MaterialType").ToString()=="Paper"?0:Member(record,"MaterialType").ToString()=="Metal"?1:2,lane);
                    Assert.Less(Vector3.Distance(((Component)Member(record,"Cargo")).transform.position,
                        ((Transform)Member(Items(geometry,"Machines")[lane],"Tray")).position),.6f);
                    CollectionAssert.AreEqual(new[] {"Spawned","Feeding","MainBelt","Identified","Diverting","MachineInput"},
                        Items(record,"History").Select(x=>x.ToString()).ToArray());
                }
            }
            finally { Object.DestroyImmediate(probes); }
        }
        private IEnumerator Sorting(string kind,int expectedMachine,int legacyKind)
        {
            Supply(kind); Activate();
            yield return Until(()=>Products.Length==1,kind+" never completed");
            Assert.IsTrue(Materials.All(r=>(int)Member(r,"TargetMachine")==expectedMachine));
            Assert.IsTrue(Materials.All(r=>Member(r,"MaterialType").ToString()==kind && (bool)Member(r,"Counted")));
            Transform tray=(Transform)Member(Items(geometry,"Machines")[expectedMachine],"Tray");
            foreach (object raw in Materials)
                Assert.Less(Vector3.Distance(((Component)Member(raw,"Cargo")).transform.position,tray.position),.6f,
                    "Raw material did not actually reach the assigned machine");
            Assert.AreEqual(kind,Member(Products[0],"MaterialType").ToString());
            Assert.IsTrue((bool)Call(runtime,"HasSortedMaterial",legacyKind));
            Assert.AreEqual(3,((string[])Member(Products[0],"SourceMaterialIds")).Distinct().Count());
        }
        [UnityTest] public IEnumerator PaperSorting() { yield return Sorting("Paper",0,2); }
        [UnityTest] public IEnumerator MetalSorting() { yield return Sorting("Metal",1,1); }
        [UnityTest] public IEnumerator PlasticSorting() { yield return Sorting("Plastic",2,0); }
        [UnityTest] public IEnumerator ProcessingState()
        {
            Supply("Paper",2); Activate(); Step(400);
            Assert.AreEqual(0,Count("ProcessingUnits")); Assert.AreEqual(0,Products.Length);
            Assert.IsFalse((bool)Call(runtime,"HasSortedMaterial",2),"Two unprocessed units must not unlock paper statistics");
            Assert.IsTrue((bool)Call(line,"TrySupply","third",Kind("Paper")));
            yield return Until(()=>Count("ProcessingUnits")==3,"Processing did not wait for all three actual arrivals");
            Assert.IsTrue(Materials.All(r=>Items(r,"History").Select(s=>s.ToString()).Contains("MachineInput")));
            Assert.AreEqual(0,Count("ProcessedUnits")); Assert.AreEqual(0,Products.Length);
        }
        [UnityTest] public IEnumerator MachineOutput()
        {
            Supply("Metal"); Activate();
            yield return Until(()=>Count("ProcessingUnits")==3,"No metal processing");
            Transform press=(Transform)Member(Items(geometry,"Machines")[1],"Press");
            Vector3 before=press.position; Step(12);
            Assert.Less(press.position.y,before.y-.1f,"Compression head must visibly move before output");
            Assert.AreEqual(0,Products.Length,"Product appeared before processing finished");
            yield return Until(()=>Products.Length==1,"No processed metal block");
            object batch=Products.Single(); Transform product=(Transform)Member(batch,"Product");
            Assert.IsTrue(product.gameObject.activeInHierarchy);
            Assert.IsFalse((bool)Member(batch,"OutputReached"));
            Assert.IsTrue(Materials.All(r=>(bool)Member(r,"ProcessingComplete") && (bool)Member(r,"ProductCreated")));
            Assert.Less(Vector3.Distance(product.position,((Transform)Member(Items(geometry,"Machines")[1],"Tray")).position),.5f,
                "New product must form inside the machine, not teleport to the external belt");
        }
        [UnityTest] public IEnumerator OutputConveyor()
        {
            int seen=0;
            Dictionary<string,int> identities=new Dictionary<string,int>();
            Dictionary<string,Vector3> positions=new Dictionary<string,Vector3>();
            Dictionary<int,string> batchIds=new Dictionary<int,string>();
            Dictionary<string,string> types=new Dictionary<string,string>();
            Dictionary<string,string[]> sources=new Dictionary<string,string[]>();
            Dictionary<string,HashSet<int>> segments=new Dictionary<string,HashSet<int>>();
            GameObject probe=new GameObject("P2_TEST_OUTPUT_SHAPE"); probe.transform.position=ProbeParking;
            try
            {
                BoxCollider shape=probe.AddComponent<BoxCollider>(); shape.isTrigger=true;
                string calibration=ValidateBoxProbe(shape);
                Supply("Paper",9,"paper"); Supply("Metal",9,"metal"); Supply("Plastic",9,"plastic"); Activate();
                for (int i=0;i<1600 && Count("CollectedProducts")<9;i++)
                {
                    Step();
                    foreach (object batch in Products)
                    {
                        int lane=Member(batch,"MaterialType").ToString()=="Paper"?0:Member(batch,"MaterialType").ToString()=="Metal"?1:2;
                        object machine=Items(geometry,"Machines")[lane]; Transform product=(Transform)Member(batch,"Product");
                        string id=(string)Member(batch,"BatchId");
                        string type=Member(batch,"MaterialType").ToString();
                        Bounds envelope=ProductEnvelope(product);
                        shape.center=envelope.center; shape.size=envelope.size;
                        Func<string> details=()=>OutputDetails(batch,machine,shape,calibration);
                        if (identities.ContainsKey(id))
                        {
                            Require(identities[id]==product.GetInstanceID() && batchIds[product.GetInstanceID()]==id,"Output swapped product/Batch IDs",details);
                            Require(Vector3.Distance(positions[id],product.position)<.18f,"Output product jumped",details);
                            Require(types[id]==type && sources[id].SequenceEqual((string[])Member(batch,"SourceMaterialIds")),"Output material/source identities changed",details);
                        }
                        else
                        {
                            Require(!batchIds.ContainsKey(product.GetInstanceID()),"Existing product acquired a new Batch ID",details);
                            identities.Add(id,product.GetInstanceID()); batchIds.Add(product.GetInstanceID(),id); types.Add(id,type);
                            sources.Add(id,(string[])((string[])Member(batch,"SourceMaterialIds")).Clone()); segments.Add(id,new HashSet<int>());
                            Require(sources[id].Length==3 && sources[id].Distinct().Count()==3,"Batch must retain three unique source IDs",details);
                            Require(Vector3.Distance(product.position,((Transform)Member(machine,"Tray")).position)<.5f,"Product did not form inside its processing machine",details);
                            Require(Materials.Count(r=>sources[id].Contains((string)Member(r,"MaterialId")) &&
                                Member(r,"MaterialType").ToString()==type && (bool)Member(r,"ProcessingComplete") && (bool)Member(r,"Counted"))==3,
                                "Unprocessed or wrong-type source in finished batch",details);
                        }
                        segments[id].Add((int)Member(batch,"CurrentRouteSegment"));
                        positions[id]=product.position;
                        Require(Mathf.Abs(product.position.x-((Transform)Member(machine,"Root")).position.x)<1f,"Product entered another machine's lane",details);
                        Collider[] supports=RouteSupports(batch);
                        if ((int)Member(batch,"CurrentRouteSegment")<=1) supports=supports.Concat(new[] {(Collider)Member(machine,"ProcessBed")}).Distinct().ToArray();
                        object section=Items(machine,"OutputSections")[(int)Member(batch,"CurrentRouteSegment")];
                        Collider surface=(Collider)Member(section,"Surface");
                        float tilt=Vector3.Angle(product.up,surface.transform.up);
                        float jointTilt=supports.Max(s=>Vector3.Angle(surface.transform.up,s.transform.up))*.5f;
                        Vector3 a=((Transform)Member(section,"Start")).position,b=((Transform)Member(section,"End")).position;
                        float along=Vector3.Dot(product.position-a,(b-a).normalized);
                        float allowedTilt=along>.5f && along<Vector3.Distance(a,b)-.5f ? .1f : jointTilt+.1f;
                        Require(tilt<=allowedTilt,"Product tilt disagrees with its real belt / adjacent joint",details);
                        Dictionary<Collider,float> gaps=BoxContactGaps(shape,product.position,product.rotation,supports);
                        foreach (Collider obstacle in Physics.OverlapBox(product.TransformPoint(shape.center),shape.size*.5f,product.rotation,~0,QueryTriggerInteraction.Ignore))
                            if (!obstacle.transform.IsChildOf(product) && Physics.ComputePenetration(shape,product.position,product.rotation,
                                obstacle,obstacle.transform.position,obstacle.transform.rotation,out Vector3 direction,out float depth))
                                Require(depth<=ProductPenetrationLimit,"Finished product penetrates "+ColliderPath(obstacle)+
                                    " depth="+depth.ToString("F6")+"m direction="+direction.ToString("F6"),details);
                        Require(HasSupport(gaps),"Finished product has no finite current/adjacent output surface within 3mm",details);
                        seen++;
                    }
                    yield return null;
                }
            }
            finally { Object.DestroyImmediate(probe); }
            Assert.Greater(seen,30,"Must observe actual transport, not only final counts");
            Assert.AreEqual(9,Count("CollectedProducts")); Assert.AreEqual(9,Products.Length);
            Assert.AreEqual(27,sources.Values.SelectMany(s=>s).Distinct().Count());
            for (int lane=0;lane<3;lane++)
            {
                string type=lane==0?"Paper":lane==1?"Metal":"Plastic";
                object[] laneProducts=Products.Where(b=>Member(b,"MaterialType").ToString()==type).ToArray();
                Assert.AreEqual(3,laneProducts.Length);
                Vector3 collection=((Transform)Member(Items(geometry,"Machines")[lane],"Collection")).position;
                float[] expectedSlots={collection.x-.65f,collection.x,collection.x+.65f};
                float[] occupied=laneProducts.Select(b=>((Transform)Member(b,"Product")).position.x).OrderBy(x=>x).ToArray();
                for (int slot=0;slot<3;slot++) Assert.That(occupied[slot],Is.EqualTo(expectedSlots[slot]).Within(.01f));
                foreach (object batch in laneProducts)
                {
                    string id=(string)Member(batch,"BatchId");
                    Vector3 destination=(Vector3)Member(Items(Member(batch,"Path"),"Legs").Last(),"B");
                    Transform product=(Transform)Member(batch,"Product");
                    Assert.That(product.position.x,Is.EqualTo(destination.x).Within(.01f));
                    Assert.That(product.position.z,Is.EqualTo(collection.z).Within(.01f));
                    Assert.IsTrue((bool)Member(batch,"OutputReached") && (bool)Member(batch,"Collected"));
                    CollectionAssert.AreEquivalent(new[] {0,1,2,3},segments[id],"Must observe every real output section: "+id);
                }
            }
        }
        [UnityTest] public IEnumerator MaterialAccounting()
        {
            Assert.AreEqual(0,Convert.ToInt32(Kind("Metal"))); Assert.AreEqual(1,Convert.ToInt32(Kind("Plastic")));
            Assert.AreEqual(2,Convert.ToInt32(Kind("Electronics"))); Assert.AreEqual(3,Convert.ToInt32(Kind("Paper")));
            Supply("Paper",9,"account");
            Assert.IsFalse((bool)Call(line,"TrySupply","account-0",Kind("Metal")));
            Assert.IsFalse((bool)Call(line,"TrySupply","unsupported",Kind("Electronics")));
            Assert.IsFalse((bool)Call(line,"TrySupply","full",Kind("Paper")));
            int events=0; Action<string> handler=id=>events++;
            EventInfo completed=line.GetType().GetEvent("BatchCompleted"); completed.AddEventHandler(line,handler);
            try
            {
                Activate(); string inventory=string.Join("|",Items(runtime,"inventory").Select(i=>JsonUtility.ToJson(i)));
                string[] slots=(string[])((string[])Member(runtime,"quickSlots")).Clone();
                int ammo=(int)Member(runtime,"ammoInMagazine"),reserve=(int)Member(runtime,"reserveAmmo");
                for (int i=0;i<1500 && Count("CollectedProducts")<3;i++)
                {
                    Step();
                    Assert.AreEqual(Count("AcceptedUnits"),Count("WaitingUnits")+Count("ProcessingUnits")+Count("ProcessedUnits"));
                    Assert.AreEqual(Count("ProcessedUnits"),Count("ProcessedBatches")*3);
                    Assert.LessOrEqual(Count("CollectedProducts"),Count("OutputProducts"));
                    Assert.LessOrEqual(Count("OutputProducts"),Count("ProcessedBatches"));
                    yield return null;
                }
                Assert.AreEqual(3,events); Assert.AreEqual(3,Count("CollectedProducts"));
                Assert.AreEqual(9,Member(runtime,"RecyclingReceiptCount"));
                Assert.AreEqual(inventory,string.Join("|",Items(runtime,"inventory").Select(i=>JsonUtility.ToJson(i))),"Sorting must not grant inventory resources");
                CollectionAssert.AreEqual(slots,(string[])Member(runtime,"quickSlots"));
                Assert.AreEqual(ammo,Member(runtime,"ammoInMagazine")); Assert.AreEqual(reserve,Member(runtime,"reserveAmmo"));
                object batch=Products[0];
                Assert.IsFalse((bool)Call(runtime,"TryCommitRecyclingBatch",(string)Member(batch,"BatchId"),Kind("Paper"),(string[])Member(batch,"SourceMaterialIds")));
                GameObject trigger=new GameObject("P2_TEST_LEGACY_TRIGGER");
                try
                {
                    Component sorter=trigger.AddComponent(GameType("EcoConveyorSorter"));
                    foreach (object raw in Materials) Assert.IsFalse((bool)Call(sorter,"TrySort",Member(raw,"Cargo")));
                }
                finally { Object.DestroyImmediate(trigger); }
                Step(200); Assert.AreEqual(3,events); Assert.AreEqual(3,Products.Length);
            }
            finally { completed.RemoveEventHandler(line,handler); }
        }
        [UnityTest] public IEnumerator ActivationState()
        {
            Supply("Plastic"); Step(100);
            Assert.AreEqual(0,Count("FedUnits")); Assert.AreEqual(0,Count("ProcessedUnits"));
            Activate(false); Step(100);
            Assert.AreEqual("Starting",Member(device,"FactoryState").ToString()); Assert.AreEqual(0,Products.Length);
            Call(device,"NotifyStartupFinished"); Step(12);
            Vector3 position=((Component)Member(Materials[0],"Cargo")).transform.position;
            try { Time.timeScale=0f; Step(100); Assert.AreEqual(position,((Component)Member(Materials[0],"Cargo")).transform.position); }
            finally { Time.timeScale=1f; }
            Assert.IsTrue((bool)Call(device,"TryCompleteAtExit"));
            Assert.IsFalse((bool)Call(line,"TrySupply","after-completion",Kind("Metal")));
            yield return Until(()=>Count("CollectedProducts")==1,"Completed must drain existing material without new admissions");
        }
        [UnityTest] public IEnumerator ReloadSafety()
        {
            Supply("Paper",3,"replay"); Activate();
            yield return Until(()=>Count("CollectedProducts")==1,"No first-run receipt");
            int equipment=((Transform)Member(geometry,"BuildRoot")).GetComponentsInChildren<Transform>(true).Length;
            ((Behaviour)geometry).enabled=false; ((Behaviour)geometry).enabled=true;
            ((Behaviour)line).enabled=true; ((Behaviour)line).enabled=false;
            Assert.AreEqual(equipment,((Transform)Member(geometry,"BuildRoot")).GetComponentsInChildren<Transform>(true).Length);
            Assert.AreEqual(3,Count("AcceptedUnits")); Assert.AreEqual(1,Count("ProcessedBatches"));
            Component previous=line;
            yield return SceneManager.LoadSceneAsync(ScenePath,LoadSceneMode.Single); yield return null;
            Assert.IsTrue(previous==null,"Old scene controller survived reload"); Capture(); Physics.SyncTransforms();
            Assert.AreEqual(1,Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(t=>t.name=="C3_P2_RecyclingLine"));
            Assert.AreEqual(0,Materials.Length); Assert.AreEqual(0,Products.Length);
            Assert.AreEqual(3,Member(runtime,"RecyclingReceiptCount"));
            for (int i=0;i<3;i++) Assert.IsFalse((bool)Call(line,"TrySupply","replay-"+i,Kind("Paper")));
            AssertEquipmentConnections(geometry);
        }
        [UnityTest] public IEnumerator UpperWalkwayClearance()
        {
            yield return scope.WorldGeometryMatchesEnabledCollidersAndHasNoHeadObstructions();
            yield return scope.WalkBThenRearConnectionAndDescendA();
        }
        [UnityTest] public IEnumerator ChipAndGuardRegression()
        {
            yield return scope.WalkACollectExactlyOneChipAndReturnDownB();
            yield return scope.EveryGuardStopsStandingAndProneCapsulesIncludingCorners();
            yield return scope.GroundBCDoorApproachRemainsOpenUnderTheEastFlight();
        }
        [UnityTest] public IEnumerator ProneWalkwayClearance()
        {
            GameObject player=GameObject.Find("M2_Player");
            Component controller=player.GetComponent(GameType("C3TreasurePuzzle.C3FirstPersonController"));
            CharacterController body=player.GetComponent<CharacterController>();
            Call(scope,"StartAt",new Vector3(-1.55f,4.5f,14.2f)); Set(controller,"isProne",true);
            try
            {
                foreach (Vector3 target in new[] {new Vector3(-1.55f,4.5f,19.2f),new Vector3(-23.2f,4.5f,19.2f),new Vector3(-23.2f,4.5f,14.2f)})
                {
                    for (int i=0;i<1800;i++)
                    {
                        Vector3 delta=target-player.transform.position; delta.y=0f;
                        if (delta.magnitude<.09f) break;
                        player.transform.rotation=Quaternion.LookRotation(delta);
                        Call(controller,"TickMovement",Vector2.up,false,false,false,1f/60f);
                        Assert.Greater(player.transform.position.y,4.2f,"Prone player fell through deck");
                        yield return null;
                    }
                    Assert.Less(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(target.x,target.z)),.15f);
                    Assert.That(body.height,Is.EqualTo((float)Member(controller,"proneHeight")).Within(.01f));
                }
            }
            finally { Set(controller,"isProne",false); }
        }
        [UnityTest] public IEnumerator Regression()
        {
            Set(line,"useFiniteTestSupply",true); ((Behaviour)line).enabled=true;
            yield return scope.OriginalPartsConsoleSortingBenchesAndWeaponStillOperate();
        }
    }
}
