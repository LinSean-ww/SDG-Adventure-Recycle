using System.Collections;
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
            GameObject shutter=GameObject.Find("V7_OPAQUE_C_TO_B_RECEIVING_SHUTTER");
            Assert.IsNotNull(shutter,"The B room must be hidden from the C factory");
            Collider door=shutter.GetComponent<Collider>();
            Assert.IsNotNull(door);
            Assert.IsTrue(door.enabled);
            Assert.LessOrEqual(door.bounds.max.x,-.27f);
            var belt=GameObject.Find("V8_B_To_C_ContinuousRubber");
            Assert.IsNotNull(belt);
            var r=belt.GetComponentInChildren<Renderer>();
            Assert.IsNotNull(r);
            Assert.Less(r.bounds.max.x,-.48f,"Conveyor must not enter B zone");
            Assert.Greater(r.bounds.max.x,-.90f,"Conveyor should visibly meet the doorway");
        }
        [UnityTest]
        public IEnumerator FormerObstructingStairRampHasMovedAwayFromReportedPosition()
        {
            yield return Load();
            var ramp=GameObject.Find("V8_MAIN_STAIR_CONTINUOUS_RAMP");
            Assert.IsNotNull(ramp);
            Collider c=ramp.GetComponent<Collider>();
            Assert.IsNotNull(c);
            Assert.IsTrue(c.enabled);
            Assert.Less(c.bounds.max.x,-20.8f,"Primary staircase must stay outside central aisle");
            Assert.IsNull(GameObject.Find("STAIR_EAST_CONTINUOUS_WALKING_RAMP"),
                "Retired east stair must not have invisible collision");
            Assert.IsNotNull(GameObject.Find("2F_EAST_WALL_CONNECTING_ROUTE"));
            Assert.IsNotNull(GameObject.Find("2F_EAST_REAR_TRANSFER_ROUTE"));
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
