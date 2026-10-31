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
        private enum ContactStatus { ValidSupport,Unsupported,ExcessiveGap,ExcessivePenetration,InvalidProbe,NoSurface,AmbiguousContact }
        private sealed class SupportMeasurement
        {
            public ContactStatus Status=ContactStatus.AmbiguousContact;
            public double Lower=double.NaN,Upper=double.NaN;
            public double GeometricEntry=double.NaN,GeometricExit=double.NaN;
            public float PenetrationDepth;
            public Vector3 PenetrationDirection;
            public bool OriginPenetrating,NativeWitness;
            public int Iterations;
            public string Reason="";
            public double Gap => (Lower+Upper)*.5;
        }
        private sealed class BoxFrame
        {
            public Vector3 Center,Half;
            public Vector3[] Axes;
            public double Radius(Vector3 axis) => Math.Abs(Dot(Axes[0],axis))*Half.x+
                Math.Abs(Dot(Axes[1],axis))*Half.y+Math.Abs(Dot(Axes[2],axis))*Half.z;
        }
        private const double ContactResolution=.00002; // search bound, never added to the 3mm acceptance limit
        private static double Dot(Vector3 a,Vector3 b) => (double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z;
        private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        private static bool TryBoxFrame(BoxCollider box,Vector3 position,Quaternion rotation,out BoxFrame frame)
        {
            frame=null;
            if (!box || !Finite(position) || !Finite(box.size) || !Finite(box.center)) return false;
            Vector3 scale=box.transform.lossyScale;
            Vector3 half=Vector3.Scale(box.size*.5f,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
            if (!Finite(half) || half.x<=0f || half.y<=0f || half.z<=0f) return false;
            Vector3[] basis={box.transform.localToWorldMatrix.MultiplyVector(Vector3.right).normalized,
                box.transform.localToWorldMatrix.MultiplyVector(Vector3.up).normalized,
                box.transform.localToWorldMatrix.MultiplyVector(Vector3.forward).normalized};
            // A sheared transform cannot be represented as the exact oriented box
            // used here. Reject it rather than silently substituting a world AABB.
            if (Mathf.Abs(Vector3.Dot(basis[0],basis[1]))>.0001f || Mathf.Abs(Vector3.Dot(basis[0],basis[2]))>.0001f ||
                Mathf.Abs(Vector3.Dot(basis[1],basis[2]))>.0001f) return false;
            frame=new BoxFrame {Center=position+rotation*Vector3.Scale(box.center,scale),Half=half,
                Axes=new[] {rotation*Vector3.right,rotation*Vector3.up,rotation*Vector3.forward}};
            return Finite(frame.Center) && frame.Axes.All(Finite);
        }
        private static bool VerticalOverlapInterval(BoxFrame moving,BoxFrame surface,out double entry,out double exit)
        {
            entry=double.NegativeInfinity; exit=double.PositiveInfinity;
            List<Vector3> axes=moving.Axes.Concat(surface.Axes).ToList();
            foreach (Vector3 a in moving.Axes) foreach (Vector3 b in surface.Axes)
            {
                Vector3 cross=Vector3.Cross(a,b);
                if (cross.sqrMagnitude>1e-12f) axes.Add(cross.normalized);
            }
            // For fixed rotations, box/box overlap under a -Y translation is the
            // intersection of 15 SAT intervals: a single convex interval. This
            // proves monotonicity only BEFORE an interior witness, not across an
            // arbitrary sweep that could enter and later leave a finite edge.
            foreach (Vector3 axis in axes)
            {
                double radius=moving.Radius(axis)+surface.Radius(axis);
                double distance=Dot(moving.Center-surface.Center,axis),approach=Dot(Vector3.up,axis);
                if (Math.Abs(approach)<1e-9)
                {
                    if (Math.Abs(distance)>=radius) return false;
                    continue;
                }
                double a=(distance-radius)/approach,b=(distance+radius)/approach;
                entry=Math.Max(entry,Math.Min(a,b)); exit=Math.Min(exit,Math.Max(a,b));
                if (entry>=exit) return false;
            }
            return !double.IsInfinity(entry) && !double.IsInfinity(exit) && entry<exit;
        }
        private static bool NativeBoxOverlap(BoxCollider probe,Vector3 position,Quaternion rotation,Collider surface,
            double down,out Vector3 direction,out float depth)
        {
            bool overlap=Physics.ComputePenetration(probe,position-Vector3.up*(float)down,rotation,
                surface,surface.transform.position,surface.transform.rotation,out direction,out depth);
            return overlap && depth>0f; // exact tangency may return true with zero depth
        }
        private static SupportMeasurement MeasureBoxSupport(BoxCollider probe,Vector3 position,Quaternion rotation,
            Collider target,QueryTriggerInteraction triggers)
        {
            SupportMeasurement result=new SupportMeasurement();
            if (!probe || !probe.enabled || !probe.gameObject.activeInHierarchy || !TryBoxFrame(probe,position,rotation,out BoxFrame moving))
            { result.Status=ContactStatus.InvalidProbe; result.Reason="inactive/invalid/sheared probe"; return result; }
            bool triggerAllowed=triggers==QueryTriggerInteraction.Collide || (triggers==QueryTriggerInteraction.UseGlobal && Physics.queriesHitTriggers);
            BoxCollider surface=target as BoxCollider;
            if (!surface || !surface.enabled || !surface.gameObject.activeInHierarchy || (surface.isTrigger && !triggerAllowed))
            { result.Status=ContactStatus.NoSurface; result.Reason="no usable allowed BoxCollider surface"; return result; }
            if (!TryBoxFrame(surface,surface.transform.position,surface.transform.rotation,out BoxFrame fixedBox) || surface.transform.up.y<=.05f)
            { result.Status=ContactStatus.AmbiguousContact; result.Reason="invalid/sheared/vertical surface"; return result; }
            result.OriginPenetrating=NativeBoxOverlap(probe,position,rotation,surface,0,out result.PenetrationDirection,out result.PenetrationDepth);
            if (!VerticalOverlapInterval(moving,fixedBox,out double entry,out double exit))
            { result.Status=ContactStatus.Unsupported; result.Reason="no positive finite-footprint overlap on the vertical approach"; return result; }
            result.GeometricEntry=entry; result.GeometricExit=exit;
            double width=exit-entry;
            if (width<ContactResolution*4)
            { result.Status=ContactStatus.AmbiguousContact; result.Reason="degenerate finite-edge interval"; return result; }
            double low=entry-.0005,high=entry+Math.Min(width*.25,.0005);
            if (NativeBoxOverlap(probe,position,rotation,surface,low,out _,out _))
            { result.Status=ContactStatus.AmbiguousContact; result.Reason="expected separated upper bracket overlaps"; return result; }
            result.NativeWitness=NativeBoxOverlap(probe,position,rotation,surface,high,out Vector3 witnessDirection,out _);
            if (!result.NativeWitness || Vector3.Dot(witnessDirection,Vector3.up)<=.05f || Vector3.Dot(witnessDirection,surface.transform.up)<=.5f ||
                !NativeBoxOverlap(probe,position,rotation,surface,Math.Min(exit-ContactResolution,high+Math.Min(width*.125,.0005)),out _,out _))
            { result.Status=ContactStatus.AmbiguousContact; result.Reason="no consistent native interior/top-side witness"; return result; }
            for (int i=0;i<32 && high-low>ContactResolution;i++)
            {
                double mid=(low+high)*.5;
                if (NativeBoxOverlap(probe,position,rotation,surface,mid,out _,out _)) high=mid; else low=mid;
                result.Iterations++;
            }
            result.Lower=low; result.Upper=high;
            if (high-low>ContactResolution) { result.Status=ContactStatus.AmbiguousContact; result.Reason="search failed to converge"; }
            else if (result.PenetrationDepth>ProductPenetrationLimit) result.Status=ContactStatus.ExcessivePenetration;
            else if (low<-ProductPenetrationLimit)
            {
                result.Status=result.OriginPenetrating?ContactStatus.ExcessivePenetration:ContactStatus.Unsupported;
                result.Reason="below the top-support boundary; penetration flag/depth remain independent";
            }
            else if (low>MaxSupportGap) result.Status=ContactStatus.ExcessiveGap;
            else if (high>MaxSupportGap) { result.Status=ContactStatus.AmbiguousContact; result.Reason="uncertainty straddles the strict 3mm boundary"; }
            else result.Status=ContactStatus.ValidSupport;
            return result;
        }
        private static Dictionary<Collider,SupportMeasurement> BoxContactGaps(BoxCollider shape,Vector3 position,Quaternion rotation,
            Collider[] supports,QueryTriggerInteraction triggers=QueryTriggerInteraction.Ignore) => supports.Where(s=>s).Distinct()
                .ToDictionary(s=>s,s=>MeasureBoxSupport(shape,position,rotation,s,triggers));
        private static bool HasSupport(Dictionary<Collider,SupportMeasurement> gaps) => gaps.Values.Any(g=>g.Status==ContactStatus.ValidSupport);
        private static string GapText(SupportMeasurement gap) => gap.Status+" verticalGap=["+gap.Lower.ToString("F7")+","+
            gap.Upper.ToString("F7")+"]m originPenetrating="+gap.OriginPenetrating+" depth="+gap.PenetrationDepth.ToString("F7")+
            " direction="+gap.PenetrationDirection.ToString("F6")+" nativeWitness="+gap.NativeWitness+" iterations="+gap.Iterations+
            " geometricInterval=["+gap.GeometricEntry.ToString("F7")+","+gap.GeometricExit.ToString("F7")+"]m "+gap.Reason;
        private static string BoxQueryDetails(BoxCollider shape,Vector3 position,Quaternion rotation,Collider[] supports,
            QueryTriggerInteraction triggers,float lift=ContactLift,float sweep=ContactSweep,float? expectedGap=null)
        {
            // BoxCast is observed here, never used as a gap or a support verdict.
            Vector3 scale=shape.transform.lossyScale;
            Vector3 half=Vector3.Scale(shape.size*.5f,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
            Vector3 origin=position+rotation*Vector3.Scale(shape.center,scale)+Vector3.up*lift;
            StringBuilder text=new StringBuilder("Method=native ComputePenetration boundary / WORLD -Y; BoxCast diagnostic only\n"+
                "Probe="+shape.name+" enabled="+shape.enabled+" isTrigger="+shape.isTrigger+
                " localCenter="+shape.center.ToString("F7")+" size="+shape.size.ToString("F7")+" scale="+scale.ToString("F7")+
                    " parkedPosition="+shape.transform.position.ToString("F7")+" parkedRotation="+shape.transform.rotation.ToString("F7")+
                    " queryPosition="+position.ToString("F7")+
                " queryRotation="+rotation.ToString("F7")+" probeContactOffset="+shape.contactOffset.ToString("F7")+
                " defaultContactOffset="+Physics.defaultContactOffset.ToString("F7")+" queryTriggers="+triggers+
                " castOrigin="+origin.ToString("F7")+" halfExtents="+half.ToString("F7")+
                " direction="+Vector3.down.ToString("F7")+" lift="+lift.ToString("F7")+" distance="+sweep.ToString("F7")+"\n");
            Dictionary<Collider,bool> initial=new Dictionary<Collider,bool>();
            foreach (Collider support in supports.Where(s=>s).Distinct())
            {
                bool overlaps=NativeBoxOverlap(shape,position+Vector3.up*lift,rotation,support,0,out Vector3 direction,out float depth);
                initial[support]=overlaps;
                BoxCollider box=support as BoxCollider;
                text.AppendLine("AllowedSurface="+ColliderPath(support)+" enabled="+support.enabled+" isTrigger="+support.isTrigger+
                    " position="+support.transform.position.ToString("F7")+" rotation="+support.transform.rotation.ToString("F7")+
                    (box?" center="+box.center.ToString("F7")+" size="+box.size.ToString("F7"):"")+
                    " boundsCenter="+support.bounds.center.ToString("F7")+" boundsSize="+support.bounds.size.ToString("F7")+
                    " normal="+support.transform.up.ToString("F7")+" contactOffset="+support.contactOffset.ToString("F7")+
                    " initialOverlap="+overlaps+" initialDepth="+depth.ToString("F7")+" initialDirection="+direction.ToString("F7"));
            }
            RaycastHit[] hits=Physics.BoxCastAll(origin,half,Vector3.down,rotation,sweep,~0,triggers);
            bool validHit=false;
            foreach (RaycastHit hit in hits)
            {
                if (!hit.collider) { text.AppendLine("InvalidHit: null collider"); continue; }
                bool allowed=initial.ContainsKey(hit.collider); validHit|=allowed;
                string category=!allowed?"NonTargetHit":!shape.enabled?"UnverifiedStart_DisabledProbe":initial[hit.collider]?"InitialOverlap":"NormalHit";
                text.AppendLine(category+" collider="+ColliderPath(hit.collider)+" hit.distance="+hit.distance.ToString("F9")+
                    " hit.point="+hit.point.ToString("F7")+" hit.normal="+hit.normal.ToString("F7")+
                    " UNTRUSTED_castGap="+(hit.distance-lift).ToString("F9")+
                    (expectedGap.HasValue?" knownGap="+expectedGap.Value.ToString("F9")+
                        " UNTRUSTED_castGapMinusExpected="+(hit.distance-lift-expectedGap.Value).ToString("F9"):""));
            }
            if (!validHit) text.AppendLine("NoValidTargetHit; foreign hits never establish support");
            return text.ToString();
        }
        private static string ContactCaseDetails(string name,float expected,BoxCollider shape,Vector3 position,Quaternion rotation,
            Collider[] supports,Dictionary<Collider,SupportMeasurement> measurements)
        {
            StringBuilder text=new StringBuilder("CalibrationCase="+name+" expected WORLD-Y gap="+expected.ToString("F7")+"m\n");
            foreach (var pair in measurements)
                text.AppendLine(ColliderPath(pair.Key)+" "+GapText(pair.Value)+" measuredMinusExpected="+(pair.Value.Gap-expected).ToString("F7"));
            if (measurements.Count==0) text.AppendLine("NoSurface: no allowed target");
            foreach (float lift in new[] {.005f,ContactLift,.08f})
                text.AppendLine(BoxQueryDetails(shape,position,rotation,supports,QueryTriggerInteraction.Collide,lift,lift+.02f,expected));
            return text.ToString();
        }
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
        private static Vector3 KnownContactPose(BoxCollider shape,BoxCollider surface,Quaternion rotation,Vector3 tangentOffset)
        {
            Assert.IsTrue(TryBoxFrame(shape,Vector3.zero,rotation,out BoxFrame probeBox));
            Assert.IsTrue(TryBoxFrame(surface,surface.transform.position,surface.transform.rotation,out BoxFrame supportBox));
            Vector3 normal=surface.transform.up;
            return supportBox.Center+normal*(float)(supportBox.Radius(normal)+probeBox.Radius(normal))+tangentOffset-
                rotation*Vector3.Scale(shape.center,shape.transform.lossyScale);
        }
        private static void CalibrateKnownGaps(BoxCollider shape,BoxCollider reference,Quaternion rotation,Vector3 tangentOffset,
            string name,StringBuilder observations,Collider[] allowed=null,bool requireEverySurface=true)
        {
            Collider[] surfaces=allowed??new Collider[] {reference};
            Vector3 contact=KnownContactPose(shape,reference,rotation,tangentOffset);
            foreach (float expected in new[] {0f,.001f,.002f,.0029f,.0031f,.01f})
            {
                Vector3 position=contact+Vector3.up*expected;
                Dictionary<Collider,SupportMeasurement> measured=BoxContactGaps(shape,position,rotation,surfaces,QueryTriggerInteraction.Collide);
                Func<string> details=()=>ContactCaseDetails(name,expected,shape,position,rotation,surfaces,measured);
                Require(measured.Count>0,"Calibration has no actual allowed surfaces",details);
                foreach (var pair in measured)
                {
                    if (!requireEverySurface && pair.Key!=reference) continue;
                    SupportMeasurement result=pair.Value;
                    Require(!double.IsNaN(result.Gap) && Math.Abs(result.Gap-expected)<=.0002,
                        "Native boundary calibration differs from known geometry",details);
                    Require(result.Status==(expected<=MaxSupportGap?ContactStatus.ValidSupport:ContactStatus.ExcessiveGap),
                        "0/1/2/2.9mm must support; 3.1/10mm must reject",details);
                }
                Require(HasSupport(measured)==(expected<=MaxSupportGap),"Incorrect support boundary classification",details);
                observations.AppendLine(name+" expected="+expected.ToString("F7")+" "+string.Join("; ",measured.Values.Select(GapText)));
                // Observe different sweep starts, without assuming a fixed sweep
                // bias or using those distances to correct the native boundary.
                if (expected==0f)
                    foreach (float lift in new[] {.005f,ContactLift,.08f})
                        observations.AppendLine(BoxQueryDetails(shape,position,rotation,surfaces,QueryTriggerInteraction.Collide,lift,lift+.02f,expected));
            }
        }
        private static void ValidateFiniteContactCases(BoxCollider shape,BoxCollider support,StringBuilder observations)
        {
            shape.enabled=true; shape.center=Vector3.zero; shape.size=Vector3.one;
            support.center=Vector3.zero; support.size=Vector3.one;
            support.transform.SetPositionAndRotation(ProbeParking+Vector3.right*8f,Quaternion.identity);
            support.transform.localScale=Vector3.one; Physics.SyncTransforms();
            CalibrateKnownGaps(shape,support,Quaternion.identity,Vector3.zero,"original horizontal unit boxes",observations);
            foreach (float angle in new[] {0f,30f,-30f})
            {
                support.transform.SetPositionAndRotation(new Vector3(136f+angle*.1f,128f,128f-angle*.1f),Quaternion.Euler(angle,20f,0f));
                support.transform.localScale=new Vector3(1.2f,.8f,.9f);
                support.size=new Vector3(3f,.18f,4f);
                shape.size=new Vector3(.52f,.43f,.51f); shape.center=new Vector3(.03f,-.02f,.04f);
                Physics.SyncTransforms();
                CalibrateKnownGaps(shape,support,support.transform.rotation*Quaternion.Euler(0f,37f,0f),Vector3.zero,
                    "scaled finite slope "+angle+" / yawed product / nonzero local center",observations);
            }
            support.transform.SetPositionAndRotation(new Vector3(-136f,96f,-128f),Quaternion.Euler(30f,0f,0f));
            support.transform.localScale=Vector3.one; support.size=new Vector3(3f,.18f,4f); Physics.SyncTransforms();
            CalibrateKnownGaps(shape,support,Quaternion.Euler(15f,0f,0f),Vector3.zero,"transition pitch differs from slope",observations);
            support.transform.SetPositionAndRotation(ProbeParking+Vector3.right*8f,Quaternion.identity);
            support.size=new Vector3(.08f,.12f,.10f); Physics.SyncTransforms();
            CalibrateKnownGaps(shape,support,Quaternion.Euler(0f,25f,0f),Vector3.zero,"small finite support under larger product",observations);
            support.size=new Vector3(2f,.18f,2f); Physics.SyncTransforms();
            CalibrateKnownGaps(shape,support,Quaternion.Euler(0f,25f,0f),Vector3.right,"partial finite edge",observations);

            Vector3 outside=KnownContactPose(shape,support,Quaternion.identity,Vector3.right*2f);
            Dictionary<Collider,SupportMeasurement> noEdge=BoxContactGaps(shape,outside,Quaternion.identity,new Collider[] {support},QueryTriggerInteraction.Collide);
            Require(!HasSupport(noEdge) && noEdge[support].Status==ContactStatus.Unsupported,"Outside finite edge accepted as support",
                ()=>ContactCaseDetails("outside edge",0f,shape,outside,Quaternion.identity,new Collider[] {support},noEdge));
            var noSurface=BoxContactGaps(shape,outside,Quaternion.identity,new Collider[0]);
            Require(!HasSupport(noSurface),"NoSurface accepted as support",
                ()=>ContactCaseDetails("no allowed surface",0f,shape,outside,Quaternion.identity,new Collider[0],noSurface));
            Vector3 contact=KnownContactPose(shape,support,Quaternion.identity,Vector3.zero);
            Vector3 penetrating=contact-Vector3.up*.02f;
            Dictionary<Collider,SupportMeasurement> penetration=BoxContactGaps(shape,penetrating,Quaternion.identity,new Collider[] {support},QueryTriggerInteraction.Collide);
            Require(penetration[support].OriginPenetrating && penetration[support].PenetrationDepth>ProductPenetrationLimit &&
                penetration[support].Status==ContactStatus.ExcessivePenetration && !HasSupport(penetration),"20mm penetration not reported separately",
                ()=>ContactCaseDetails("explicit 20mm penetration",-.02f,shape,penetrating,Quaternion.identity,new Collider[] {support},penetration));
            Require(NativeBoxOverlap(shape,penetrating+Vector3.up*.005f,Quaternion.identity,support,0,out _,out _),
                "Initial-overlap diagnostic control is not actually overlapping",
                ()=>ContactCaseDetails("initial-overlap control",-.02f,shape,penetrating,Quaternion.identity,new Collider[] {support},penetration));
            observations.AppendLine("Explicit penetration: "+GapText(penetration[support]));
            observations.AppendLine(BoxQueryDetails(shape,penetrating,Quaternion.identity,new Collider[] {support},QueryTriggerInteraction.Collide,.005f,.04f));
            Vector3 below=contact-Vector3.up*(support.size.y+shape.size.y+.05f);
            var under=BoxContactGaps(shape,below,Quaternion.identity,new Collider[] {support},QueryTriggerInteraction.Collide);
            Require(!HasSupport(under) && !under[support].OriginPenetrating && under[support].Status==ContactStatus.Unsupported,
                "A separated product below the belt is not supported or penetrating",
                ()=>ContactCaseDetails("separated below the belt",-(support.size.y+shape.size.y+.05f),shape,below,Quaternion.identity,new Collider[] {support},under));
            shape.enabled=false; Physics.SyncTransforms();
            var invalid=BoxContactGaps(shape,contact,Quaternion.identity,new Collider[] {support},QueryTriggerInteraction.Collide);
            Require(invalid[support].Status==ContactStatus.InvalidProbe && !HasSupport(invalid),"Disabled probe used for a support verdict",
                ()=>ContactCaseDetails("disabled probe",0f,shape,contact,Quaternion.identity,new Collider[] {support},invalid));
            observations.AppendLine(BoxQueryDetails(shape,contact,Quaternion.identity,new Collider[] {support},QueryTriggerInteraction.Collide,ContactLift,ContactSweep,0f));
            shape.enabled=true; Physics.SyncTransforms();

            // Real foreign colliders are present, but never supplied as legal
            // belt surfaces. Their penetration is independently demonstrable.
            support.center=Vector3.right*8f;
            foreach (string name in new[] {"P2_TEST_CONTACT_GUARD","P2_TEST_COLLECTED_PRODUCT"})
            {
                GameObject foreign=new GameObject(name); foreign.transform.SetParent(support.transform,false);
                try
                {
                    BoxCollider obstacle=foreign.AddComponent<BoxCollider>(); obstacle.isTrigger=true; obstacle.size=new Vector3(2f,.18f,2f);
                    Physics.SyncTransforms();
                    Vector3 pose=KnownContactPose(shape,obstacle,Quaternion.identity,Vector3.zero);
                    var wrong=BoxContactGaps(shape,pose,Quaternion.identity,new Collider[] {support},QueryTriggerInteraction.Collide);
                    Require(!HasSupport(wrong),"Foreign collider counted as a conveyor surface",
                        ()=>ContactCaseDetails(name,0,shape,pose,Quaternion.identity,new Collider[] {support},wrong));
                    Require(NativeBoxOverlap(shape,pose-Vector3.up*.02f,Quaternion.identity,obstacle,0,out _,out float depth) && depth>ProductPenetrationLimit,
                        "Foreign penetration positive control failed",
                        ()=>ContactCaseDetails(name+" penetration",-.02f,shape,pose-Vector3.up*.02f,Quaternion.identity,new Collider[] {obstacle},
                            BoxContactGaps(shape,pose-Vector3.up*.02f,Quaternion.identity,new Collider[] {obstacle},QueryTriggerInteraction.Collide)));
                    observations.AppendLine(BoxQueryDetails(shape,pose,Quaternion.identity,new Collider[] {support},QueryTriggerInteraction.Collide));
                }
                finally { Object.DestroyImmediate(foreign); }
            }
            support.center=Vector3.back*.5f; support.size=new Vector3(2f,.18f,1f);
            GameObject neighbor=new GameObject("P2_TEST_CONTACT_JOINT"); neighbor.transform.SetParent(support.transform,false);
            try
            {
                BoxCollider next=neighbor.AddComponent<BoxCollider>(); next.isTrigger=true; next.center=Vector3.forward*.5f; next.size=support.size;
                shape.center=Vector3.zero; Physics.SyncTransforms();
                CalibrateKnownGaps(shape,support,Quaternion.identity,Vector3.forward*.5f,"two finite belt joint",observations,new Collider[] {support,next});
                Vector3 joint=support.transform.TransformPoint(support.center+Vector3.up*(support.size.y*.5f)+Vector3.forward*(support.size.z*.5f));
                Quaternion slope=Quaternion.Euler(30f,0f,0f);
                next.center=Vector3.zero;
                next.transform.SetPositionAndRotation(joint+slope*Vector3.forward*(next.size.z*.5f)-slope*Vector3.up*(next.size.y*.5f),slope);
                Physics.SyncTransforms();
                // The flat portion truly bears the horizontal box; contact with
                // only the ramp end cap may be ambiguous and must not be forced
                // into a second "valid" surface merely because it is adjacent.
                CalibrateKnownGaps(shape,support,Quaternion.identity,Vector3.forward*.4f,"flat-to-sloped finite joint",observations,
                    new Collider[] {support,next},false);
                next.transform.localPosition=Vector3.zero; next.transform.localRotation=Quaternion.identity;
                next.center=Vector3.forward*.5f; Physics.SyncTransforms();
                Vector3 jointPose=KnownContactPose(shape,support,Quaternion.identity,Vector3.forward*.5f);
                support.enabled=false; Physics.SyncTransforms();
                var adjacent=BoxContactGaps(shape,jointPose,Quaternion.identity,new Collider[] {support,next},QueryTriggerInteraction.Collide);
                Require(HasSupport(adjacent),"Valid adjacent surface cannot support independently",
                    ()=>ContactCaseDetails("only adjacent surface enabled",0f,shape,jointPose,Quaternion.identity,new Collider[] {support,next},adjacent));
                support.enabled=true; support.center=Vector3.back*.9f; next.center=Vector3.forward*.9f; Physics.SyncTransforms();
                Vector3 brokenPose=KnownContactPose(shape,support,Quaternion.identity,Vector3.forward*.9f);
                var broken=BoxContactGaps(shape,brokenPose,Quaternion.identity,new Collider[] {support,next},QueryTriggerInteraction.Collide);
                Require(!HasSupport(broken),"Broken joint with no finite surface accepted",
                    ()=>ContactCaseDetails("broken finite joint",0,shape,brokenPose,Quaternion.identity,new Collider[] {support,next},broken));
                observations.AppendLine("NoSurface, outside edge, foreign guard/product, disabled probe, adjacent-only support and broken joint controls checked");
            }
            finally { support.enabled=true; Object.DestroyImmediate(neighbor); }
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
                ValidateFiniteContactCases(shape,support,observations);
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
        private static string ProductionContactDetails(object batch)
        {
            // Read-only comparison with the unchanged production Evaluate. This
            // runs only after a failed assertion, never to place a product or
            // establish the independent contact verdict.
            try
            {
                object path=Member(batch,"Path"); object[] legs=Items(path,"Legs");
                float distance=(float)Member(batch,"Distance");
                Vector3 size=(Vector3)GameType("C3TreasurePuzzle.C3RecyclingLine").GetField("ProductSize").GetValue(null);
                object[] args={distance,0f,size,Vector3.zero,Quaternion.identity,0};
                path.GetType().GetMethod("Evaluate",Any).Invoke(path,args);
                Vector3 evaluated=(Vector3)args[3]; Quaternion rotation=(Quaternion)args[4]; int segment=(int)args[5];
                float local=distance;
                for (int i=0;i<segment;i++) local-=(float)Member(legs[i],"Length");
                Vector3 a=(Vector3)Member(legs[segment],"A"),b=(Vector3)Member(legs[segment],"B");
                Vector3 normal=((Collider)Member(legs[segment],"Support")).transform.up,up=rotation*Vector3.up;
                float extent=(Mathf.Abs(Vector3.Dot(rotation*Vector3.right,normal))*size.x+
                    Mathf.Abs(Vector3.Dot(up,normal))*size.y+Mathf.Abs(Vector3.Dot(rotation*Vector3.forward,normal))*size.z)*.5f;
                Vector3 initial=Vector3.Lerp(a,b,Mathf.Clamp01(local/Vector3.Distance(a,b)))+up*(extent/Mathf.Max(.15f,Vector3.Dot(up,normal)));
                Collider[] allowed=legs.Skip(Mathf.Max(0,segment-1)).Take(segment==0?2:3).Select(l=>(Collider)Member(l,"Support")).Distinct().ToArray();
                float margin=size.magnitude*.5f+.2f,startY=initial.y+margin,lowest=initial.y;
                foreach (Collider c in allowed) { startY=Mathf.Max(startY,c.bounds.max.y+margin); lowest=Mathf.Min(lowest,c.bounds.min.y); }
                Vector3 origin=new Vector3(initial.x,startY,initial.z);
                Transform product=(Transform)Member(batch,"Product");
                StringBuilder text=new StringBuilder("PRODUCTION Evaluate re-query position="+evaluated.ToString("F7")+
                    " actualMinusRequery="+(product.position-evaluated).ToString("F7")+" segment="+segment+
                    " source-reconstructed origin="+origin.ToString("F7")+" halfExtents="+(size*.5f).ToString("F7")+
                    " rotation="+rotation.ToString("F7")+" direction=world -Y sweep="+(startY-lowest+margin).ToString("F7")+"\n");
                foreach (RaycastHit hit in Physics.BoxCastAll(origin,size*.5f,Vector3.down,rotation,startY-lowest+margin,~0,QueryTriggerInteraction.Ignore))
                    if (hit.collider) text.AppendLine("production "+(allowed.Contains(hit.collider)?"ALLOWED":"NON_TARGET")+
                        " "+ColliderPath(hit.collider)+" distance="+hit.distance.ToString("F9")+
                        " point="+hit.point.ToString("F7")+" normal="+hit.normal.ToString("F7"));
                return text.ToString();
            }
            catch (Exception error) { return "Production diagnostic query failed: "+error.GetBaseException().Message; }
        }
        private string OutputDetails(object batch,object machine,BoxCollider shape,string calibration)
        {
            Transform product=(Transform)Member(batch,"Product");
            object[] sections=Items(machine,"OutputSections");
            object[] legs=Items(Member(batch,"Path"),"Legs");
            Collider[] surfaces=sections.Select(s=>(Collider)Member(s,"Surface")).Concat(new[] {(Collider)Member(machine,"ProcessBed")}).ToArray();
            Dictionary<Collider,SupportMeasurement> gaps=BoxContactGaps(shape,product.position,product.rotation,surfaces);
            Collider[] obstacles=Physics.OverlapBox(product.TransformPoint(shape.center),shape.size*.5f,product.rotation,~0,QueryTriggerInteraction.Ignore)
                .Where(c=>!c.transform.IsChildOf(product)).ToArray();
            StringBuilder text=new StringBuilder("BatchId="+Member(batch,"BatchId")+" type="+Member(batch,"MaterialType")+
                " segment="+Member(batch,"CurrentRouteSegment")+" object="+product.name+
                " sources="+string.Join(",",(string[])Member(batch,"SourceMaterialIds"))+" OutputReached="+Member(batch,"OutputReached")+" Collected="+Member(batch,"Collected")+
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
            text.AppendLine("Supporting colliders: "+string.Join(",",gaps.Where(p=>p.Value.Status==ContactStatus.ValidSupport).Select(p=>ColliderPath(p.Key))));
            text.AppendLine(BoxQueryDetails(shape,product.position,product.rotation,surfaces,QueryTriggerInteraction.Ignore));
            text.AppendLine(ProductionContactDetails(batch));
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
                        Dictionary<Collider,SupportMeasurement> gaps=BoxContactGaps(shape,product.position,product.rotation,supports);
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
