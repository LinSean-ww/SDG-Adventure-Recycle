using System;
using System.Collections;
using System.Linq;
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
                for (int i=0;i<common.Length;i++)
                {
                    Vector3 a=((Transform)Member(common[i],"Start")).position,b=((Transform)Member(common[i],"End")).position;
                    Collider support=(Collider)Member(common[i],"Surface");
                    for (int sample=0;sample<=60;sample++)
                    {
                        Vector3 center=Vector3.Lerp(a,b,sample/60f)+support.transform.up*.121f;
                        Assert.LessOrEqual(Vector3.Distance(center,support.ClosestPoint(center)),.123f,"Missing finite infeed surface");
                        foreach (Collider c in Physics.OverlapSphere(center,.12f,~0,QueryTriggerInteraction.Ignore))
                            if (Physics.ComputePenetration(sphere,center,Quaternion.identity,c,c.transform.position,c.transform.rotation,out _,out float depth))
                                Assert.LessOrEqual(depth,.001f,"Infeed cargo blocked by "+c.name+" at "+center);
                    }
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
            foreach (Collider vessel in vessels)
            {
                Renderer renderer=vessel.GetComponent<Renderer>();
                Assert.Less(Vector3.Distance(vessel.bounds.center,renderer.bounds.center),.001f);
                Assert.Less(Vector3.Distance(vessel.bounds.size,renderer.bounds.size),.001f);
                StartAt(new Vector3(-3.8f,.84f,vessel.transform.position.z));
                contacts.SideContacts.Clear();
                bool touched=false;
                for (int i=0;i<65;i++)
                {
                    CollisionFlags flags=body.Move(Vector3.right*.03f+Vector3.down*.025f);
                    touched|=(flags&CollisionFlags.Sides)!=0; yield return null;
                }
                Assert.IsTrue(touched && contacts.SideContacts.Contains(vessel),"No native player/vessel contact");
                Assert.Less(player.transform.position.x,-3.15f,"Player entered purifier body");
            }
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
