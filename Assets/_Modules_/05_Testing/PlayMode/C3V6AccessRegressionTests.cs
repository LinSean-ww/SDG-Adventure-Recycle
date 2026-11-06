using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    public sealed class C3V6AccessRegressionTests
    {
        private const string Scene="Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private C3V11SafeCirculationTests scope;
        private IEnumerator Load()
        {
            // Reuse inventory/input/global isolation; each test owns its scene.
            scope=new C3V11SafeCirculationTests();
            yield return scope.OpenFactory();
            Assert.IsNotNull(GameObject.Find("C3_V3_VerifiedCirculation"));
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(scope!=null)yield return scope.Cleanup();scope=null;}

        [UnityTest]
        public IEnumerator ConveyorIsInsideTheMainHallAndConnectsToSorter()
        {
            yield return Load();
            GameObject conveyor=GameObject.Find("V8_B_To_C_ContinuousRubber");
            Assert.IsNotNull(conveyor);
            Renderer r=conveyor.GetComponentInChildren<Renderer>();
            Assert.IsNotNull(r);
            Assert.LessOrEqual(r.bounds.max.x,-.55f,"Conveyor entered B room");
            Assert.LessOrEqual(r.bounds.min.x,-6.1f,"Conveyor does not reach sorter main line");
            Assert.GreaterOrEqual(r.bounds.max.z,16.8f,"Conveyor has no feed to the sorter");
            Assert.GreaterOrEqual(r.bounds.max.y,5.6f,"Conveyor cannot reach high intake");
            Assert.LessOrEqual(r.bounds.max.y,5.72f,"Conveyor crossover is above its clearance envelope");
            Assert.IsNull(GameObject.Find("V5_BPortalContinuousBelt"),
                "Old B-inside model should not be present simultaneously");
        }

        [UnityTest]
        public IEnumerator StairApproachesClearOfQuantumBench()
        {
            yield return Load();
            GameObject desk=GameObject.Find("EASTER_EGG_EcoQuantumWorkshop");
            Assert.IsNotNull(desk);
            Assert.LessOrEqual(desk.transform.position.z,3.25f,
                "Quantum bench still blocks the east stair's entrance");
            Transform circulation=GameObject.Find("C3_V3_VerifiedCirculation").transform;
            foreach(string side in new[]{"A_WEST","B_EAST"})
            {
                Transform stair=circulation.Find("STAIR_"+side);
                Assert.IsNotNull(stair);
                Collider entry=stair.Find("BOTTOM_LANDING").GetComponent<Collider>();
                Assert.IsTrue(entry.enabled && !entry.isTrigger);
                Assert.That(Mathf.Abs(entry.bounds.center.x-desk.transform.position.x),Is.GreaterThan(entry.bounds.extents.x));
                Assert.AreEqual(22,stair.GetComponentsInChildren<Collider>().Count(c=>c.enabled && c.name.StartsWith("TREAD_")));
                string route=side=="A_WEST"?"2F_WEST_SERVICE_ROUTE":"2F_EAST_SERVICE_ROUTE";
                Collider landing=circulation.Find(route).GetComponent<Collider>();
                Physics.SyncTransforms();
                Vector3 eye=landing.bounds.center;eye.y=6f;
                Assert.IsTrue(Physics.Raycast(eye,Vector3.down,out RaycastHit deck,3f),"2F landing is missing");
                Assert.AreSame(landing,deck.collider);
                Assert.AreEqual(4.5f,deck.point.y,.08f);
            }
        }

        [UnityTest]
        public IEnumerator BothStairsCanCarryACharacterControllerOntoTheSecondFloor()
        {
            yield return Load();
            Transform root=GameObject.Find("C3_V3_VerifiedCirculation").transform;
            CharacterController cc=GameObject.Find("M2_Player").GetComponent<CharacterController>();
            Collider aEntry=root.Find("STAIR_A_WEST/BOTTOM_LANDING").GetComponent<Collider>();
            Collider bEntry=root.Find("STAIR_B_EAST/BOTTOM_LANDING").GetComponent<Collider>();
            Collider aDeck=root.Find("2F_WEST_SERVICE_ROUTE").GetComponent<Collider>();
            Collider bDeck=root.Find("2F_EAST_SERVICE_ROUTE").GetComponent<Collider>();
            float ax=aEntry.bounds.center.x,bx=bEntry.bounds.center.x;
            float az=aEntry.bounds.center.z,bz=bEntry.bounds.center.z;
            cc.enabled=false;
            cc.transform.SetPositionAndRotation(new Vector3(ax,aEntry.bounds.max.y+.08f,az),Quaternion.identity);
            cc.enabled=true;Physics.SyncTransforms(); // initial placement only; never teleport between routes
            C3V11CollisionRecorder contacts=cc.GetComponent<C3V11CollisionRecorder>();
            Assert.IsNotNull(contacts);contacts.CaptureDetails=true;
            try
            {
                yield return Walk(cc,new Vector3(ax,4.5f,aDeck.bounds.center.z));
                Assert.Greater(cc.transform.position.z,15f);
                Assert.Greater(cc.transform.position.y,4.20f);
                Assert.IsTrue(contacts.DetailedContacts.Any(c=>c.Collider && c.Collider.name.StartsWith("TREAD_") &&
                    c.Collider.transform.IsChildOf(root.Find("STAIR_A_WEST"))),"No real A tread contact");
                yield return Walk(cc,new Vector3(ax,4.5f,19.2f));
                yield return Walk(cc,new Vector3(bx,4.5f,19.2f));
                yield return Walk(cc,new Vector3(bx,4.5f,bDeck.bounds.center.z));
                yield return Walk(cc,new Vector3(bx,bEntry.bounds.max.y,bz));
                Assert.IsTrue(contacts.DetailedContacts.Any(c=>c.Collider && c.Collider.name.StartsWith("TREAD_") &&
                    c.Collider.transform.IsChildOf(root.Find("STAIR_B_EAST"))),"No real B tread contact");
                // Preserve both-stairs coverage: also climb B and descend A.
                yield return Walk(cc,new Vector3(bx,4.5f,bDeck.bounds.center.z));
                yield return Walk(cc,new Vector3(bx,4.5f,19.2f));
                yield return Walk(cc,new Vector3(ax,4.5f,19.2f));
                yield return Walk(cc,new Vector3(ax,4.5f,aDeck.bounds.center.z));
                yield return Walk(cc,new Vector3(ax,aEntry.bounds.max.y,az));
            }
            finally{contacts.CaptureDetails=false;contacts.DetailedContacts.Clear();}
        }

        private static IEnumerator Walk(CharacterController cc,Vector3 target)
        {
            for(int i=0;i<1200;i++)
            {
                Vector3 delta=target-cc.transform.position;delta.y=0;
                if(delta.magnitude<.075f)break;
                cc.Move(Vector3.ClampMagnitude(delta,.04f)+Vector3.down*.04f);
                if(i%5==0)yield return null;
            }
            Vector3 remaining=target-cc.transform.position;
            Assert.Less(new Vector2(remaining.x,remaining.z).magnitude,.08f,"Route stalled at "+cc.transform.position);
            Assert.AreEqual(target.y,cc.transform.position.y,.12f,"Wrong floor at route endpoint");
            Assert.IsTrue(cc.isGrounded,"Walk must finish on actual supporting geometry");
        }

        [UnityTest]
        public IEnumerator ActualCharacterControllerCanWalkAlongFrontAisle()
        {
            yield return Load();
            // Test a central unobstructed walking line, rather than object existence.
            GameObject walker=new GameObject("V6_AISLE_WALKER");
            CharacterController cc=walker.AddComponent<CharacterController>();
            cc.radius=.28f;cc.height=1.8f;cc.center=Vector3.up*.9f;
            cc.stepOffset=.26f;cc.slopeLimit=50f;
            walker.transform.position=new Vector3(-15f,.6f,5.8f);
            Physics.SyncTransforms(); // 診斷：同步物理位置
            for (int i=0;i<100;i++)
            {
                cc.Move(new Vector3(0f,-.03f,.047f));
                yield return null;
            }
            Assert.Greater(walker.transform.position.z,9.9f,
                "Main floor aisle obstructed: character controller unable to cross");
            Object.Destroy(walker);
        }
    }
}
