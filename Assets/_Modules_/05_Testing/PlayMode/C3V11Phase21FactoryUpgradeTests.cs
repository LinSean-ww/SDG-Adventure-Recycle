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
        private IEnumerator WalkTo(Vector3 destination)
        {
            for (int i=0;i<1000;i++)
            {
                Vector3 delta=destination-player.transform.position; delta.y=0f;
                if (delta.magnitude<.06f) break;
                body.Move(Vector3.ClampMagnitude(delta,.05f)+Vector3.down*.025f);
                yield return null;
            }
            Vector3 remaining=destination-player.transform.position; remaining.y=0f;
            Assert.Less(remaining.magnitude,.15f,"Real player blocked en route to "+destination+" at "+player.transform.position);
            Assert.That(player.transform.position.y,Is.EqualTo(destination.y).Within(.16f));
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
            Assert.IsNotNull(Find("C3TreasurePuzzle.C3ExitDoor"));
            StartAt(new Vector3(-12.35f,.60f,2.1f));
            yield return WalkTo(new Vector3(-12.35f,.56f,6.1f));
            yield return WalkTo(new Vector3(-12.35f,.56f,2.1f));
        }
        [UnityTest] public IEnumerator RefuseCagesAndPurifiersStopTheOriginalPlayer()
        {
            Collider[] cages=Object.FindObjectsOfType<BoxCollider>().Where(c=>c.name=="Bin lateral welded rail" ||
                c.name=="Bin end welded rail" || c.name=="Refuse cage metal upright" || c.name=="SORTING BIN - input refuse floor").Cast<Collider>().ToArray();
            Assert.AreEqual(39,cages.Length); Assert.IsTrue(cages.All(c=>c.enabled && !c.isTrigger));
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
            foreach (string name in new[] {"WASTEWATER treatment grated foundation","POLLUTION pressure controller","POLLUTION controller mounting arm"})
            {
                Collider[] parts=Object.FindObjectsOfType<Collider>().Where(c=>c.name==name).ToArray();
                Assert.AreEqual(name.StartsWith("WASTEWATER")?1:3,parts.Length,name);
                Assert.IsTrue(parts.All(c=>c.enabled && !c.isTrigger),name);
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
    }
}
