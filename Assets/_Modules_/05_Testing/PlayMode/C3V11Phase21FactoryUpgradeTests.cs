using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
using static M6IntegrationTests.C3V11Phase2RecyclingLineTests;

namespace M6IntegrationTests
{
    // Uses the accepted fixture's inventory/input/scene cleanup. These are native
    // geometry and CharacterController tests, not substitutes for visual review.
    public sealed class C3V11Phase21FactoryUpgradeTests
    {
        private C3V11Phase2RecyclingLineTests scope;
        private Component geometry;
        private GameObject player;
        private CharacterController body;
        private C3V11CollisionRecorder contacts;
        [UnitySetUp] public IEnumerator Open()
        {
            scope=new C3V11Phase2RecyclingLineTests(); yield return scope.Open();
            geometry=Find("C3TreasurePuzzle.C3RecyclingLineGeometry");
            player=GameObject.Find("M2_Player"); body=player.GetComponent<CharacterController>();
            contacts=player.GetComponent<C3V11CollisionRecorder>(); Assert.IsNotNull(contacts);
        }
        [UnityTearDown] public IEnumerator Close() { if (scope!=null) yield return scope.Close(); }
        private void StartAt(Vector3 position,float height=1.8f)
        {
            body.enabled=false; player.transform.SetPositionAndRotation(position,Quaternion.identity);
            body.height=height; body.center=Vector3.up*(height*.5f); body.enabled=true;
            Physics.SyncTransforms();
        }
        private IEnumerator WalkTo(Vector3 destination,Renderer[] exhaust=null,float eyeHeight=0f,float leanOffset=0f)
        {
            for (int i=0;i<1000;i++)
            {
                Vector3 delta=destination-player.transform.position; delta.y=0f;
                if (delta.magnitude<.06f) break;
                body.Move(Vector3.ClampMagnitude(delta,.05f)+Vector3.down*.025f);
                if (exhaust!=null) AssertExhaustClearOfPlayer(exhaust,eyeHeight,leanOffset);
                yield return null;
            }
            Vector3 remaining=destination-player.transform.position; remaining.y=0f;
            Assert.Less(remaining.magnitude,.15f,"Real player blocked en route to "+destination+" at "+player.transform.position);
            Assert.That(player.transform.position.y,Is.EqualTo(destination.y).Within(.16f));
        }
        private static Renderer[] ExhaustVisuals()
            => GameObject.Find("C3_ReferenceRecyclingFactory_Visuals").GetComponentsInChildren<Renderer>(true)
                .Where(r=>r.name.StartsWith("Purifier connected exhaust") || r.name.StartsWith("Purifier under-deck exhaust") ||
                    r.name.StartsWith("Purifier outboard exhaust") || r.name.StartsWith("Purifier exhaust")).ToArray();
        private static string HierarchyPath(Transform item)
        {
            string path=item.name;
            for (Transform parent=item.parent;parent;parent=parent.parent) path=parent.name+"/"+path;
            return path;
        }
        private static Vector3[] CylinderEnds(Renderer renderer)
        {
            MeshFilter filter=renderer.GetComponent<MeshFilter>(); Assert.IsNotNull(filter,renderer.name);
            Bounds local=filter.sharedMesh.bounds;
            return new[] {renderer.transform.TransformPoint(local.center-Vector3.up*local.extents.y),
                renderer.transform.TransformPoint(local.center+Vector3.up*local.extents.y)};
        }
        private static float DistanceToPipe(Vector3 point,Vector3[] ends)
        {
            Vector3 delta=ends[1]-ends[0]; Assert.Greater(delta.sqrMagnitude,.000001f);
            return Vector3.Distance(point,ends[0]+delta*Mathf.Clamp01(Vector3.Dot(point-ends[0],delta)/delta.sqrMagnitude));
        }
        private void AssertExhaustClearOfPlayer(Renderer[] exhaust,float eyeHeight,float leanOffset)
        {
            foreach (Renderer pipe in exhaust)
            {
                // Pipes were/are visual-only; an empty physics query alone
                // would miss the original fault. Check the real visible bounds
                // against the original controller and the eye/lean envelope too.
                if (pipe.bounds.Intersects(body.bounds))
                    Assert.Fail("Visible exhaust crosses player: "+HierarchyPath(pipe.transform)+" pipe="+pipe.bounds+" player="+body.bounds);
                for (int side=-1;side<=1;side++)
                {
                    Vector3 eye=player.transform.position+Vector3.up*eyeHeight+player.transform.right*(side*leanOffset);
                    if (pipe.bounds.SqrDistance(eye)<=.01f)
                        Assert.Fail("Exhaust intersects eye/lean envelope: "+HierarchyPath(pipe.transform)+" eye="+eye);
                }
            }
        }
        private sealed class RawIntrusion
        {
            internal Collider Collider;
            internal float Depth;
            internal Vector3 Direction;
        }
        private static RawIntrusion[] RawIntrusions(SphereCollider sphere,Vector3 center)
        {
            return Physics.OverlapSphere(center,sphere.radius,~0,QueryTriggerInteraction.Ignore)
                .Select(c=>Physics.ComputePenetration(sphere,center,Quaternion.identity,c,c.transform.position,c.transform.rotation,
                    out Vector3 direction,out float depth)?new RawIntrusion {Collider=c,Depth=depth,Direction=direction}:null)
                .Where(hit=>hit!=null && hit.Depth>.001f).ToArray();
        }
        private static Vector3 RawPose(object path,float distance,out int segment)
        {
            // Out parameters require explicit by-ref types at this assembly boundary.
            MethodInfo evaluate=path.GetType().GetMethod("Evaluate",BindingFlags.Instance|BindingFlags.NonPublic,null,
                new[] {typeof(float),typeof(float),typeof(Vector3),typeof(Vector3).MakeByRefType(),
                    typeof(Quaternion).MakeByRefType(),typeof(int).MakeByRefType()},null);
            Assert.IsNotNull(evaluate,"Production TransportPath.Evaluate signature changed");
            object[] args={distance,.12f,Vector3.zero,Vector3.zero,Quaternion.identity,0};
            evaluate.Invoke(path,args); segment=(int)args[5]; return (Vector3)args[3];
        }
        private static string RawDiagnostics(object path,int segment,SphereCollider sphere,Vector3 center)
        {
            object[] legs=Items(path,"Legs"); StringBuilder text=new StringBuilder();
            text.AppendLine("sphere center="+center.ToString("F6")+" radius="+sphere.radius.ToString("F6")+" segment="+segment);
            for (int i=Mathf.Max(0,segment-1);i<=Mathf.Min(legs.Length-1,segment+1);i++)
            {
                Collider surface=(Collider)Member(legs[i],"Support"); Vector3 a=(Vector3)Member(legs[i],"A"),b=(Vector3)Member(legs[i],"B");
                Vector3 closest=surface.ClosestPoint(center),normal=surface.transform.up;
                text.AppendLine((i==segment?"CurrentSection":i>segment?"NextSection":"PreviousSection")+"="+HierarchyPath(surface.transform.parent)+
                    " surface="+HierarchyPath(surface.transform)+" start="+a.ToString("F6")+" end="+b.ToString("F6")+
                    " normal="+normal.ToString("F6")+" bounds="+surface.bounds.ToString("F6")+" closest="+closest.ToString("F6")+
                    " finiteDistance="+Vector3.Distance(center,closest).ToString("F6")+" signedPlaneDistance="+Vector3.Dot(center-a,normal).ToString("F6"));
                if (i<legs.Length-1) text.AppendLine("next normal angle="+Vector3.Angle(normal,((Collider)Member(legs[i+1],"Support")).transform.up).ToString("F6"));
            }
            foreach (Collider hit in Physics.OverlapSphere(center,sphere.radius+.003f,~0,QueryTriggerInteraction.Ignore))
            {
                bool penetration=Physics.ComputePenetration(sphere,center,Quaternion.identity,hit,hit.transform.position,hit.transform.rotation,
                    out Vector3 direction,out float depth);
                text.AppendLine("HitSurface="+HierarchyPath(hit.transform)+" expectedAdjacentSupport="+
                    legs.Skip(Mathf.Max(0,segment-1)).Take(segment==0?2:3).Any(l=>(Collider)Member(l,"Support")==hit)+
                    " bounds="+hit.bounds.ToString("F6")+" closest="+hit.ClosestPoint(center).ToString("F6")+
                    " penetration="+penetration+" depth="+depth.ToString("F6")+" direction="+direction.ToString("F6"));
            }
            return text.ToString();
        }
        private static void AssertRawContact(object path,int segment,SphereCollider sphere,Vector3 center)
        {
            object[] legs=Items(path,"Legs");
            Collider[] supports=legs.Skip(Mathf.Max(0,segment-1)).Take(segment==0?2:3).Select(l=>(Collider)Member(l,"Support")).ToArray();
            bool supported=supports.Any(c=>c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy &&
                Mathf.Abs(Vector3.Distance(center,c.ClosestPoint(center))-sphere.radius)<=.003f);
            RawIntrusion[] intrusions=RawIntrusions(sphere,center);
            if (!supported || intrusions.Length>0)
                Assert.Fail((!supported?"Missing finite current/adjacent support":"Raw cargo penetrates a solid by more than 1mm")+"\n"+
                    RawDiagnostics(path,segment,sphere,center));
        }
        private static string SolidDetails(Collider collider)
        {
            Renderer renderer=collider.GetComponent<Renderer>();
            return HierarchyPath(collider.transform)+" collider="+collider.bounds.ToString("F6")+
                " renderer="+(renderer?renderer.bounds.ToString("F6"):"MISSING")+" enabled="+collider.enabled+" trigger="+collider.isTrigger;
        }
        private static void AssertVisibleSolid(Collider collider)
        {
            Assert.IsNotNull(collider); Renderer renderer=collider.GetComponent<Renderer>(); Assert.IsNotNull(renderer);
            Assert.IsTrue(collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy && renderer.enabled,SolidDetails(collider));
            Assert.Less(Vector3.Distance(collider.bounds.center,renderer.bounds.center),.001f,SolidDetails(collider));
            Assert.Less(Vector3.Distance(collider.bounds.size,renderer.bounds.size),.001f,SolidDetails(collider));
        }
        private RaycastHit[] VesselApproach(Vector3 start,float radius)
            => Physics.SphereCastAll(start,radius,Vector3.right,1.6f,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>!h.collider.transform.IsChildOf(player.transform)).OrderBy(h=>h.distance).ToArray();
        [UnityTest] public IEnumerator EndStopsProtectDeadEndsWithoutClosingLiveTransfers()
        {
            object main=Member(geometry,"MainBelt");
            Vector3 end=((Transform)Member(main,"End")).position;
            Vector3 direction=(end-((Transform)Member(main,"Start")).position).normalized;
            Collider stop=GameObject.Find("P21_MAIN_TERMINAL").transform.Find("END_STOP").GetComponent<Collider>();
            Assert.IsTrue(stop.enabled && !stop.isTrigger);
            Assert.Greater(Vector3.Dot(stop.bounds.center-end,direction),.05f);
            foreach (object machine in Items(geometry,"Machines"))
            {
                Vector3 gate=((Transform)Member(Member(machine,"Chute"),"Start")).position;
                Assert.Greater(Vector3.Dot(end-gate,direction),2f,"Stop must be downstream of all real diversions");
                Transform collection=(Transform)Member(machine,"Collection");
                Transform root=GameObject.Find("P21_COLLECTION_TERMINAL_"+Member(machine,"Index")).transform;
                Collider rear=root.Find("END_STOP").GetComponent<Collider>();
                foreach (float x in new[] {-.65f,0f,.65f})
                {
                    Vector3 front=collection.position+new Vector3(x,.215f,-.255f);
                    Assert.Greater(Vector3.Distance(front,rear.ClosestPoint(front)),.04f,"Stop invades an existing collection slot");
                }
            }
            Assert.AreEqual(30,Items(geometry,"Sections").Length);
            Assert.IsTrue((bool)Call(geometry,"ConnectionsMatch",.001f));
            yield return null;
        }
        [UnityTest] public IEnumerator HighCornersHaveNativeSupportAndClearFullRadiusCargo()
        {
            GameObject probe=new GameObject("P21_TEST_RAW_CORNER_PROBE"); probe.transform.position=Vector3.one*128f;
            try
            {
                SphereCollider sphere=probe.AddComponent<SphereCollider>(); sphere.isTrigger=true; sphere.radius=.12f;
                Physics.SyncTransforms();
                object[] common=Items(geometry,"CommonSections");
                Component line=Find("C3TreasurePuzzle.C3RecyclingLine");
                foreach (string kind in new[] {"Paper","Metal","Plastic"}) C3Phase21TestDriver.Supply(line,kind,3,"handoff-");
                object[] records=Items(line,"Materials"); object path=Member(records[0],"Path");
                object[] legs=Items(path,"Legs"); Assert.AreEqual(6,legs.Length);
                Assert.AreEqual("P2_INFEED_4",((Collider)Member(legs[4],"Support")).transform.parent.name);
                Assert.AreSame(Member(Member(geometry,"MainBelt"),"Surface"),Member(legs[5],"Support"));
                float distance=0f;
                for (int i=0;i<legs.Length;i++)
                {
                    Vector3 a=(Vector3)Member(legs[i],"A"),b=(Vector3)Member(legs[i],"B");
                    float length=Vector3.Distance(a,b),sampleLength=i==common.Length?Mathf.Min(.6f,length):length;
                    for (int sample=0;sample<=60;sample++)
                    {
                        // Production resolves the finite neighbouring surfaces by
                        // SphereCast. A per-section normal offset embeds the sphere
                        // in the next horizontal belt at the descending handoff.
                        Vector3 center=RawPose(path,distance+sampleLength*sample/60f,out int segment);
                        AssertRawContact(path,segment,sphere,center);
                    }
                    distance+=length;
                }
                float handoff=legs.Take(5).Sum(l=>Vector3.Distance((Vector3)Member(l,"A"),(Vector3)Member(l,"B")));
                Collider slope=(Collider)Member(legs[4],"Support"),main=(Collider)Member(legs[5],"Support");
                Vector3 slopeA=(Vector3)Member(legs[4],"A"),slopeB=(Vector3)Member(legs[4],"B");
                // Sample 59/60 rounds to the user's (-6.26,5.91,16.85).
                Vector3 oldCenter=Vector3.Lerp(slopeA,slopeB,59f/60f)+slope.transform.up*.121f;
                Assert.IsTrue(Physics.ComputePenetration(sphere,oldCenter,Quaternion.identity,main,main.transform.position,main.transform.rotation,
                    out _,out float oldDepth) && oldDepth>.001f,"Negative control must reproduce the old synthetic overlap");
                Vector3 safe=RawPose(path,handoff,out int atJoint);
                AssertRawContact(path,atJoint,sphere,safe);
                Vector3 beforeJoint=RawPose(path,handoff-Vector3.Distance(slopeA,slopeB)/60f,out int beforeSegment);
                AssertRawContact(path,beforeSegment,sphere,beforeJoint);
                Debug.Log("R1 old 59/60 per-normal sample (negative control)\n"+RawDiagnostics(path,4,sphere,oldCenter)+
                    "Production pose at the same route parameter\n"+RawDiagnostics(path,beforeSegment,sphere,beforeJoint)+
                    "Production joint pose\n"+RawDiagnostics(path,atJoint,sphere,safe));
                foreach (string name in new[] {"BELT_SURFACE","SIDE_GUARD","P2_ROOF_HANGER"})
                {
                    GameObject obstacle=new GameObject(name); obstacle.transform.SetParent(probe.transform,false);
                    obstacle.transform.position=safe; BoxCollider blocker=obstacle.AddComponent<BoxCollider>(); blocker.size=Vector3.one*.04f;
                    try
                    {
                        Physics.SyncTransforms();
                        Assert.IsTrue(RawIntrusions(sphere,safe).Any(hit=>hit.Collider==blocker),"Missed illegal penetration: "+name);
                    }
                    finally { Object.DestroyImmediate(obstacle); }
                }
                foreach (int index in new[] {1,2})
                {
                    Transform joint=GameObject.Find("P21_INFEED_JUNCTION_"+index).transform;
                    Assert.IsNotNull(joint.Find("INNER_GUARD_IN")); Assert.IsNotNull(joint.Find("OUTER_GUARD_OUT"));
                    Assert.IsNotNull(joint.Find("CORNER_HANGER_SADDLE")); Assert.IsNotNull(joint.Find("CORNER_ROOF_TIE"));
                    foreach (BoxCollider c in joint.GetComponentsInChildren<BoxCollider>())
                    {
                        Assert.IsTrue(c.enabled && !c.isTrigger);
                        Renderer renderer=c.GetComponent<Renderer>(); Assert.IsNotNull(renderer);
                        Assert.Less(Vector3.Distance(c.bounds.center,renderer.bounds.center),.001f);
                        Assert.Less(Vector3.Distance(c.bounds.size,renderer.bounds.size),.001f);
                    }
                }
                Assert.AreEqual(30,Items(geometry,"Sections").Length); Assert.IsTrue((bool)Call(geometry,"ConnectionsMatch",.001f));
                C3Phase21TestDriver.Activate(Find("C3TreasurePuzzle.C3DeviceState"));
                HashSet<string> approached=new HashSet<string>(),crossed=new HashSet<string>();
                Dictionary<string,Vector3> previous=records.ToDictionary(r=>(string)Member(r,"MaterialId"),r=>((Component)Member(r,"Cargo")).transform.position);
                for (int tick=0;tick<1600 && (int)Member(line,"CollectedProducts")<3;tick++)
                {
                    C3Phase21TestDriver.Tick(line);
                    foreach (object record in records)
                    {
                        string state=Member(record,"State").ToString();
                        if (state!="Feeding" && state!="MainBelt" && state!="Identified") continue;
                        int segment=(int)Member(record,"CurrentRouteSegment");
                        Transform cargo=((Component)Member(record,"Cargo")).transform;
                        AssertRawContact(Member(record,"Path"),segment,sphere,cargo.position);
                        string id=(string)Member(record,"MaterialId");
                        Assert.Less(Vector3.Distance(previous[id],cargo.position),.37f,"Real material jumped at "+HierarchyPath(cargo)); previous[id]=cargo.position;
                        if (segment==4) approached.Add((string)Member(record,"MaterialId"));
                        if (segment==5) crossed.Add((string)Member(record,"MaterialId"));
                    }
                    if (tick%25==0) yield return null;
                }
                Assert.AreEqual(9,approached.Count); Assert.AreEqual(9,crossed.Count,"All nine real materials must traverse this handoff");
                Assert.AreEqual(9,Member(line,"ProcessedUnits")); Assert.AreEqual(3,Member(line,"CollectedProducts"));
            }
            finally { Object.DestroyImmediate(probe); }
            yield return null;
        }
        [UnityTest] public IEnumerator DEntranceLosesOnlyTheDuplicateDecorativeFrame()
        {
            foreach (string name in new[] {"SENSOR_LAB monitor gantry","MATERIAL_SCANNER active monitor","Scanner gantry leg"})
                Assert.IsNull(GameObject.Find(name),"Screenshot 03 decoration still exists: "+name);
            foreach (string name in new[] {"Hidden_WeaponAssemblyBench_F","Secondary_RecyclingInspectionWorkbench_F","M4_PuzzleConsole"})
            {
                GameObject station=GameObject.Find(name); Assert.IsNotNull(station);
                Assert.Greater(station.GetComponents<MonoBehaviour>().Length,0,"Real interaction removed: "+name);
                Assert.IsTrue(station.GetComponent<Collider>().enabled);
            }
            Assert.IsNotNull(Find("C3TreasurePuzzle.C3ExitTrigger"));
            StartAt(new Vector3(-12.35f,.60f,2.1f));
            yield return WalkTo(new Vector3(-12.35f,.56f,6.1f));
            yield return WalkTo(new Vector3(-12.35f,.56f,2.1f));
        }
        [UnityTest] public IEnumerator RefuseCagesAndPurifiersStopTheOriginalPlayer()
        {
            Collider[] cages=Object.FindObjectsOfType<BoxCollider>().Where(c=>c.name=="Bin lateral welded rail" ||
                c.name=="Bin end welded rail" || c.name=="Refuse cage metal upright" || c.name=="SORTING BIN - input refuse floor").Cast<Collider>().ToArray();
            Assert.AreEqual(39,cages.Length); Assert.IsTrue(cages.All(c=>c.enabled && !c.isTrigger));
            foreach (Collider c in cages)
            {
                Renderer renderer=c.GetComponent<Renderer>(); Assert.IsNotNull(renderer);
                Assert.Less(Vector3.Distance(c.bounds.center,renderer.bounds.center),.001f);
                Assert.Less(Vector3.Distance(c.bounds.size,renderer.bounds.size),.001f);
            }
            foreach (float height in new[] {1.8f,.75f})
            {
                StartAt(new Vector3(-19.5f,.60f,20.1f),height);
                contacts.SideContacts.Clear();
                bool touched=false;
                for (int i=0;i<90;i++)
                {
                    CollisionFlags flags=body.Move(Vector3.forward*.035f+Vector3.down*.025f);
                    touched|=(flags&CollisionFlags.Sides)!=0; yield return null;
                }
                Assert.IsTrue(touched && contacts.SideContacts.Any(c=>cages.Contains(c)),"No native player/cage contact");
                Assert.Less(player.transform.position.z,21.12f,"Player entered the loaded cage");
            }
            Collider[] vessels=Object.FindObjectsOfType<Collider>().Where(c=>c.name=="POLLUTION stainless purification vessel").ToArray();
            Assert.AreEqual(3,vessels.Length); Assert.IsTrue(vessels.All(c=>c.enabled && !c.isTrigger));
            GameObject probe=new GameObject("P21_TEST_VESSEL_PROBE"); probe.transform.position=Vector3.one*128f;
            try
            {
                SphereCollider sphere=probe.AddComponent<SphereCollider>(); sphere.isTrigger=true; sphere.radius=body.radius;
                Collider foundation=Object.FindObjectsOfType<Collider>().Single(c=>c.name=="WASTEWATER treatment grated foundation");
                Collider[] scene=Object.FindObjectsOfType<Collider>();
                int vesselIndex=0;
                foreach (Collider vessel in vessels.OrderBy(c=>c.transform.position.z))
                {
                    float z=vessel.transform.position.z;
                    Collider lower=scene.Single(c=>c.name=="Purifier flange lower" && Mathf.Abs(c.transform.position.z-z)<.001f);
                    Collider upper=scene.Single(c=>c.name=="Purifier flange upper" && Mathf.Abs(c.transform.position.z-z)<.001f);
                    Collider[] assembly=scene.Where(c=>Mathf.Abs(c.transform.position.z-z)<.001f &&
                        (c==vessel || c==lower || c==upper || c.name=="POLLUTION pressure controller" || c.name=="POLLUTION controller mounting arm")).ToArray();
                    foreach (Collider part in assembly.Concat(new[] {foundation})) AssertVisibleSolid(part);
                    // Test the vessel separately between the flanges, at full
                    // player radius. No other structure may shield this approach.
                    Vector3 origin=new Vector3(-3.8f,vessel.bounds.center.y,z);
                    Assert.Greater(origin.y-sphere.radius,lower.bounds.max.y);
                    Assert.Less(origin.y+sphere.radius,upper.bounds.min.y);
                    Assert.Greater(Vector3.Distance(origin,vessel.ClosestPoint(origin)),sphere.radius,"Probe starts inside the vessel");
                    Physics.SyncTransforms(); RaycastHit[] approach=VesselApproach(origin,sphere.radius);
                    Assert.IsNotEmpty(approach,"No full-radius native approach hit: "+SolidDetails(vessel));
                    Assert.AreSame(vessel,approach[0].collider,"Body-height approach obstructed by "+HierarchyPath(approach[0].collider.transform));
                    Assert.That(approach[0].distance,Is.EqualTo(vessel.bounds.min.x-sphere.radius-origin.x).Within(.003f));
                    Assert.Less(approach[0].normal.x,-.9f);
                    Vector3 contactCenter=origin+Vector3.right*approach[0].distance;
                    Assert.IsTrue(Physics.ComputePenetration(sphere,contactCenter+Vector3.right*.03f,Quaternion.identity,
                        vessel,vessel.transform.position,vessel.transform.rotation,out Vector3 direction,out float penetration) && penetration>.015f,
                        "Independent vessel proxy must reject a 30mm intrusion: "+SolidDetails(vessel));
                    bool wasEnabled=vessel.enabled;
                    try
                    {
                        vessel.enabled=false; Physics.SyncTransforms();
                        Assert.IsFalse(VesselApproach(origin,sphere.radius).Any(h=>h.collider==vessel),"Disabled-vessel negative control was not detected");
                    }
                    finally { vessel.enabled=wasEnabled; Physics.SyncTransforms(); }
                    RaycastHit[] restored=VesselApproach(origin,sphere.radius);
                    Assert.IsNotEmpty(restored,"Vessel not restored after negative control: "+SolidDetails(vessel));
                    Assert.AreSame(vessel,restored[0].collider,"Vessel not restored after negative control");
                    StartAt(new Vector3(-3.8f,.84f,z)); contacts.SideContacts.Clear(); contacts.CaptureDetails=true;
                    Vector3 start=player.transform.position; bool touched=false; Collider firstSide=null; Vector3 firstNormal=Vector3.zero;
                    Collider firstResisting=null; Vector3 resistingNormal=Vector3.zero;
                    HashSet<Collider> unique=new HashSet<Collider>(); StringBuilder moves=new StringBuilder();
                    string setup="R2 vessel index="+(vesselIndex++)+" Z="+z.ToString("F2")+"\n"+string.Join("\n",new[] {vessel,lower,upper,foundation}.Select(SolidDetails))+
                        "\nCC start="+start.ToString("F6")+" radius="+body.radius+" height="+body.height+" skinWidth="+body.skinWidth+
                        " stepOffset="+body.stepOffset+" independent hit="+contactCenter.ToString("F6")+" intrusion="+penetration+" direction="+direction.ToString("F6");
                    try
                    {
                        for (int i=0;i<65;i++)
                        {
                            Vector3 before=player.transform.position; contacts.DetailedContacts.Clear();
                            CollisionFlags flags=body.Move(Vector3.right*.03f+Vector3.down*.025f);
                            touched|=(flags&CollisionFlags.Sides)!=0;
                            moves.AppendLine("move="+i+" requested=(0.03,-0.025,0) actual="+(player.transform.position-before).ToString("F6")+" flags="+flags);
                            foreach (C3V11CollisionRecorder.ContactEvidence hit in contacts.DetailedContacts)
                            {
                                unique.Add(hit.Collider);
                                if (!firstSide && Mathf.Abs(hit.Normal.y)<.6f) { firstSide=hit.Collider; firstNormal=hit.Normal; }
                                if (!firstResisting && hit.Normal.x<-.5f) { firstResisting=hit.Collider; resistingNormal=hit.Normal; }
                                moves.AppendLine("  hit="+HierarchyPath(hit.Collider.transform)+" normal="+hit.Normal.ToString("F6")+
                                    " point="+hit.Point.ToString("F6")+" moveDirection="+hit.MoveDirection.ToString("F6")+" moveLength="+hit.MoveLength);
                            }
                            yield return null;
                        }
                        Vector3 end=player.transform.position,atBodyHeight=new Vector3(end.x,origin.y,z);
                        float nearest=Vector3.Distance(atBodyHeight,vessel.ClosestPoint(atBodyHeight));
                        bool overlaps=Physics.ComputePenetration(sphere,atBodyHeight,Quaternion.identity,vessel,vessel.transform.position,vessel.transform.rotation,
                            out Vector3 overlapDirection,out float overlapDepth);
                        string summary=setup+"\nend="+end.ToString("F6")+" firstSide="+(firstSide?HierarchyPath(firstSide.transform):"NONE")+
                            " firstNormal="+firstNormal.ToString("F6")+" uniqueContacts="+string.Join(";",unique.Select(c=>HierarchyPath(c.transform)))+
                            " firstResisting="+(firstResisting?HierarchyPath(firstResisting.transform):"NONE")+" resistingNormal="+resistingNormal.ToString("F6")+
                            " flagsSidesSeen="+touched+" legacySideContacts="+string.Join(";",contacts.SideContacts.Select(c=>HierarchyPath(c.transform)))+
                            " nearestVesselDistance="+nearest.ToString("F6")+" bodyRadius="+sphere.radius+" penetration="+overlaps+
                            " depth="+overlapDepth+" direction="+overlapDirection.ToString("F6");
                        Func<string> details=()=>summary+"\n"+moves;
                        // Accept only this vessel's actual equipment, with a
                        // resisting side normal; unrelated Sides cannot pass.
                        if (!firstResisting || !assembly.Contains(firstResisting)) Assert.Fail("No native resisting contact with this purifier assembly\n"+details());
                        if (end.x>=-3.15f || nearest<body.radius-body.skinWidth-.005f || (overlaps && overlapDepth>body.skinWidth+.005f))
                            Assert.Fail("Player entered purifier beyond the original controller skin\n"+details());
                        Debug.Log(summary); // Full per-Move trace is emitted only on failure.
                    }
                    finally { contacts.CaptureDetails=false; contacts.DetailedContacts.Clear(); }
                }
            }
            finally { contacts.CaptureDetails=false; contacts.DetailedContacts.Clear(); Object.DestroyImmediate(probe); }
            foreach (string name in new[] {"WASTEWATER treatment grated foundation","POLLUTION pressure controller","POLLUTION controller mounting arm","Purifier flange lower","Purifier flange upper"})
            {
                Collider[] parts=Object.FindObjectsOfType<Collider>().Where(c=>c.name==name).ToArray();
                Assert.AreEqual(name.StartsWith("WASTEWATER")?1:3,parts.Length,name);
                Assert.IsTrue(parts.All(c=>c.enabled && !c.isTrigger),name);
                foreach (Collider part in parts)
                    Assert.Less(Vector3.Distance(part.bounds.size,part.GetComponent<Renderer>().bounds.size),.001f,"Oversized invisible collider: "+name);
            }
        }
        [UnityTest] public IEnumerator UpperSupportsStayBelowDeckAndLeaveBothStancesAThroughRoute()
        {
            Transform supports=GameObject.Find("V11_EDGE_SUPPORTS").transform;
            foreach (Collider c in supports.GetComponentsInChildren<Collider>())
            {
                Assert.IsTrue(c.enabled && !c.isTrigger);
                Assert.LessOrEqual(c.bounds.max.y,4.321f,"Support pierces the deck: "+c.name);
                if (c.name.StartsWith("COLUMN_EAST")) Assert.Less(c.bounds.max.x,-.42f,"East column intersects wall cladding");
            }
            GameObject box=GameObject.Find("UpperDistributionBox"); Collider cabinet=box.GetComponent<Collider>();
            Assert.IsTrue(cabinet.enabled && !cabinet.isTrigger,"Useful equipment must retain its collision");
            Assert.Greater(cabinet.bounds.min.z-19.2f,body.radius+body.skinWidth);
            foreach (float height in new[] {1.8f,.75f})
            {
                StartAt(new Vector3(-23.2f,4.52f,14.2f),height);
                yield return WalkTo(new Vector3(-23.2f,4.5f,19.2f));
                yield return WalkTo(new Vector3(-1.55f,4.5f,19.2f));
                yield return WalkTo(new Vector3(-1.55f,4.5f,14.2f));
                yield return WalkTo(new Vector3(-1.55f,4.5f,19.2f));
                yield return WalkTo(new Vector3(-23.2f,4.5f,19.2f));
                yield return WalkTo(new Vector3(-23.2f,4.5f,14.2f));
            }
        }
        [UnityTest] public IEnumerator AllThreeExhaustRoutesAvoidDeckRailsAndJoinTheExistingRoofHeader()
        {
            Transform circulation=GameObject.Find("C3_V3_VerifiedCirculation").transform;
            Collider[] decks=circulation.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("2F_")).Cast<Collider>().ToArray();
            Assert.AreEqual(3,decks.Length);
            Collider[] guards=circulation.Find("V11_UPPER_GUARDS").GetComponentsInChildren<Collider>();
            Assert.AreEqual(6,guards.Length); Collider[] protectedVolumes=decks.Concat(guards).ToArray();
            Assert.IsTrue(protectedVolumes.All(c=>c.enabled && !c.isTrigger));
            Renderer[] exhaust=ExhaustVisuals();
            Renderer[] stubs=exhaust.Where(r=>r.name=="Purifier connected exhaust riser").ToArray();
            Renderer[] offsets=exhaust.Where(r=>r.name=="Purifier under-deck exhaust offset").ToArray();
            Renderer[] outboards=exhaust.Where(r=>r.name=="Purifier outboard exhaust riser").ToArray();
            Assert.AreEqual(3,stubs.Length); Assert.AreEqual(3,offsets.Length); Assert.AreEqual(3,outboards.Length);
            Renderer header=exhaust.Single(r=>r.name=="Purifier exhaust common header");
            Renderer roofRiser=exhaust.Single(r=>r.name=="Purifier exhaust roof riser");
            Renderer connection=exhaust.Single(r=>r.name=="Purifier exhaust roof connection");
            foreach (float z in new[] {15.88f,17.44f,19f})
            {
                Renderer stub=stubs.Single(r=>Mathf.Abs(r.bounds.center.z-z)<.002f);
                Renderer offset=offsets.Single(r=>Mathf.Abs(r.bounds.center.z-z)<.002f);
                Renderer outboard=outboards.Single(r=>Mathf.Abs(r.bounds.center.z-z)<.002f);
                Vector3[] a=CylinderEnds(stub),b=CylinderEnds(offset),c=CylinderEnds(outboard);
                Assert.Less(Vector3.Distance(a[0],new Vector3(-2.7f,2.8f,z)),.002f,"Vessel exhaust disconnected");
                Assert.Less(Vector3.Distance(a[1],b[0]),.002f); Assert.Less(Vector3.Distance(b[1],c[0]),.002f);
                Assert.Less(DistanceToPipe(c[1],CylinderEnds(header)),.002f,"Riser does not enter the common header");
                float underside=decks.Min(d=>d.bounds.min.y);
                Assert.Less(stub.bounds.max.y,underside-.2f); Assert.Less(offset.bounds.max.y,underside-.2f);
                Collider eastGuard=circulation.Find("V11_UPPER_GUARDS/GUARD_EAST_WALL_OUTER").GetComponent<Collider>();
                Assert.Greater(outboard.bounds.min.x,eastGuard.bounds.max.x+.02f,"Riser clips east guard");
            }
            Assert.Less(Vector3.Distance(CylinderEnds(header)[1],CylinderEnds(roofRiser)[0]),.002f);
            Assert.Less(Vector3.Distance(CylinderEnds(roofRiser)[1],CylinderEnds(connection)[0]),.002f);
            Transform reference=GameObject.Find("C3_ReferenceRecyclingFactory_Visuals").transform;
            Renderer roofDuct=reference.GetComponentsInChildren<Renderer>().Single(r=>r.name=="Cross-hall ceiling ventilation" &&
                Mathf.Abs(r.bounds.center.z-20.3f)<.002f);
            Assert.Less(CylinderEnds(roofDuct).Min(p=>Vector3.Distance(p,CylinderEnds(connection)[1])),.002f,"Exhaust terminates short of the real roof duct");
            foreach (Renderer part in exhaust)
            {
                Assert.IsTrue(part.enabled && part.gameObject.activeInHierarchy,part.name);
                MeshFilter mesh=part.GetComponent<MeshFilter>(); Assert.IsNotNull(mesh);
                Vector3[] vertices=mesh.sharedMesh.vertices; Assert.Greater(vertices.Length,0);
                Bounds actual=new Bounds(part.transform.TransformPoint(vertices[0]),Vector3.zero);
                foreach (Vector3 vertex in vertices) actual.Encapsulate(part.transform.TransformPoint(vertex));
                Assert.Less(Vector3.Distance(actual.center,part.bounds.center),.002f);
                Assert.Less(Vector3.Distance(actual.size,part.bounds.size),.002f);
                Assert.IsTrue(part.GetComponents<Collider>().All(c=>!c.enabled),"Do not add a new invisible pipe barrier");
                foreach (Collider volume in protectedVolumes)
                    Assert.IsFalse(actual.Intersects(volume.bounds),"Visible exhaust penetrates "+HierarchyPath(volume.transform)+": "+HierarchyPath(part.transform)+" "+actual);
                if (part.name.Contains("elbow") || part.name.Contains("clamp")) continue;
                Vector3[] ends=CylinderEnds(part);
                Vector3 scale=part.transform.lossyScale; Bounds local=mesh.sharedMesh.bounds;
                float radius=Mathf.Max(Mathf.Abs(scale.x)*local.extents.x,Mathf.Abs(scale.z)*local.extents.z);
                Assert.IsFalse(Physics.OverlapCapsule(ends[0],ends[1],radius,~0,QueryTriggerInteraction.Ignore).Any(c=>protectedVolumes.Contains(c)),
                    "Native query of the rendered pipe envelope hits deck/guard: "+part.name);
            }
            // Regression positive control: the user's exact old renderer crosses
            // the real deck even after removing its collider completely.
            GameObject old=new GameObject("P21_TEST_OLD_EXHAUST_VISUAL");
            GameObject cylinder=GameObject.CreatePrimitive(PrimitiveType.Cylinder); cylinder.transform.SetParent(old.transform,false);
            try
            {
                Object.DestroyImmediate(cylinder.GetComponent<Collider>());
                cylinder.transform.position=new Vector3(-2.7f,4.76f,19f); cylinder.transform.localScale=new Vector3(.105f,1.96f,.105f);
                Physics.SyncTransforms();
                Assert.IsTrue(decks.Any(d=>cylinder.GetComponent<Renderer>().bounds.Intersects(d.bounds)),"Renderer control failed to detect the reported fault");
                Assert.IsTrue(Physics.OverlapCapsule(new Vector3(-2.7f,2.8f,19f),new Vector3(-2.7f,6.72f,19f),.0525f,~0,
                    QueryTriggerInteraction.Ignore).Any(c=>decks.Contains(c)),"Native visual-envelope control missed the original deck crossing");
            }
            finally { Object.DestroyImmediate(old); }
            // Inspect other sources too: extraction duct, roof hangers, below-
            // deck supports and any still-visible legacy station geometry.
            // Exclude only the circulation's own guard posts, which intentionally
            // start on the deck; the exhaust/guard clearance is checked above.
            Transform legacy=GameObject.Find("C_Factory_Art").transform;
            Renderer[] candidates=reference.GetComponentsInChildren<Renderer>()
                .Concat(((Transform)Member(geometry,"BuildRoot")).GetComponentsInChildren<Renderer>())
                .Concat(circulation.Find("V11_EDGE_SUPPORTS").GetComponentsInChildren<Renderer>())
                .Concat(legacy.GetComponentsInChildren<Renderer>(true))
                .Where(r=>r.enabled && r.gameObject.activeInHierarchy && r.bounds.size.y>.75f && r.bounds.size.x<.65f && r.bounds.size.z<.65f).ToArray();
            Assert.Greater(candidates.Length,3);
            foreach (Renderer candidate in candidates)
                foreach (Collider deck in decks)
                    Assert.IsFalse(candidate.bounds.Intersects(deck.bounds),"Other visible member still crosses deck: "+HierarchyPath(candidate.transform)+" "+candidate.bounds);
            Debug.Log("P21 exhaust audit: three connected vessel routes; other visible slender members checked="+candidates.Length+"; native deck crossings=0");
            yield return null;
        }
        [UnityTest] public IEnumerator ReroutedExhaustLeavesRealStandingProneAndEyeLeanEnvelopesClear()
        {
            Renderer[] exhaust=ExhaustVisuals(); Assert.Greater(exhaust.Length,9);
            Component controller=player.GetComponent(GameType("C3TreasurePuzzle.C3FirstPersonController"));
            float standing=(float)Member(controller,"standingHeight");
            float prone=Mathf.Clamp((float)Member(controller,"proneHeight"),body.radius*2f,(float)Member(controller,"crouchHeight"));
            float lean=(float)Member(controller,"leanOffset");
            for (int stance=0;stance<2;stance++)
            {
                float height=stance==0?standing:prone;
                float eye=(float)Member(controller,stance==0?"standingEyeHeight":"proneEyeHeight");
                StartAt(new Vector3(-23.2f,4.52f,19f),height); AssertExhaustClearOfPlayer(exhaust,eye,lean);
                yield return WalkTo(new Vector3(-1.55f,4.5f,19f),exhaust,eye,lean);
                yield return WalkTo(new Vector3(-1.55f,4.5f,14.2f),exhaust,eye,lean);
                yield return WalkTo(new Vector3(-1.55f,4.5f,20.8f),exhaust,eye,lean);
                yield return WalkTo(new Vector3(-1.55f,4.5f,19f),exhaust,eye,lean);
                yield return WalkTo(new Vector3(-23.2f,4.5f,19f),exhaust,eye,lean);
                Assert.That(body.height,Is.EqualTo(height).Within(.001f));
            }
        }
    }
}
