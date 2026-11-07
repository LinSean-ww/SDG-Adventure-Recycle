using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    public sealed class C3V7CZoneBoundaryAndAisleTests
    {
        private const string Scene = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameObject.Find("C3_V3_VerifiedCirculation"));
        }
        [UnityTest]
        public IEnumerator CToBIsClosedAndTheReceivingBeltStopsAtThreshold()
        {
            yield return Load();
            // V11 intentionally replaces the opaque V7 shutter with a PVC opening
            // and an overhead material interface; preserve its actual safety checks.
            Assert.IsNull(GameObject.Find("V7_OPAQUE_C_TO_B_RECEIVING_SHUTTER"));
            GameObject curtain=GameObject.Find("V11_BC_PVC_CURTAIN");
            Assert.IsNotNull(curtain);
            Assert.IsEmpty(curtain.GetComponentsInChildren<Collider>(true));
            Assert.Greater(curtain.GetComponentsInChildren<Renderer>().Length,0);
            foreach(string name in new[]{"C_Wall_4_Near","C_Wall_4_Far","C_Wall_4_Lintel","B_Door_Floor","B_Safety_End","B_Safety_Near","B_Safety_Far"})
            {
                var collider=GameObject.Find(name).GetComponent<Collider>();
                Assert.IsTrue(collider.enabled && !collider.isTrigger,name);
            }
            var geometry=C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3RecyclingLineGeometry");
            Assert.IsNotNull(geometry);
            Transform entry=(Transform)C3V11Phase2RecyclingLineTests.Member(geometry,"BMaterialInterface");
            Assert.Greater(entry.position.y,6.8f);
            C3V11Phase2RecyclingLineTests.AssertEquipmentConnections(geometry);
        }
        [UnityTest]
        public IEnumerator FormerObstructingStairRampHasMovedAwayFromReportedPosition()
        {
            yield return Load();
            var root=GameObject.Find("C3_V3_VerifiedCirculation").transform;
            Transform west=root.Find("STAIR_A_WEST"),east=root.Find("STAIR_B_EAST");
            Assert.IsNotNull(west);Assert.IsNotNull(east);
            Assert.AreEqual(22,west.GetComponentsInChildren<Collider>().Count(c=>c.enabled && c.name.StartsWith("TREAD_")));
            Assert.AreEqual(22,east.GetComponentsInChildren<Collider>().Count(c=>c.enabled && c.name.StartsWith("TREAD_")));
            foreach(Collider tread in west.GetComponentsInChildren<Collider>())
                if(tread.enabled && tread.name.StartsWith("TREAD_"))
                    Assert.Less(tread.bounds.max.x,-20.8f,"West stairs must stay outside central aisle");
            Assert.IsNotNull(root.Find("2F_WEST_SERVICE_ROUTE").GetComponent<Collider>());
            Assert.IsNotNull(root.Find("2F_EAST_SERVICE_ROUTE").GetComponent<Collider>());
            Assert.IsNotNull(root.Find("2F_CONNECTING_REAR_ROUTE").GetComponent<Collider>());
            Assert.IsNull(GameObject.Find("STAIR_EAST_CONTINUOUS_WALKING_RAMP"),
                "Retired east stair must not have invisible collision");
        }
        [UnityTest]
        public IEnumerator RealCapsuleCanCrossPreviouslyBlockedFloorStripeTowardsDExit()
        {
            yield return Load();
            GameObject temp=new GameObject("V7_TEMP_REAL_CONTROLLER_AISLE_CHECK");
            CharacterController cc=temp.AddComponent<CharacterController>();
            cc.height=1.8f; cc.radius=.29f; cc.center=Vector3.up*.9f;
            cc.slopeLimit=48f; cc.stepOffset=.25f;
            temp.transform.position=new Vector3(-5.14f,.62f,9.82f);
            // Walk from the reported coordinate towards the D side of the hall.
            for (int k=0;k<145;k++)
            {
                cc.Move(new Vector3(0f,-.04f,-.034f));
                yield return null;
            }
            Assert.Less(temp.transform.position.z,5.85f,
                "A physical collider STILL obstructs the floor stripe at x=-5.14");
            Object.Destroy(temp);
        }
    }
}
