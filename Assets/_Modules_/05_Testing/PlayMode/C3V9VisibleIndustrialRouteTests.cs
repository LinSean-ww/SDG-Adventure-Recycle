using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    /// <summary>
    /// Current-layout *Unity PlayMode* checks (original file/GUID retained). None of these results can be claimed to pass
    /// until actually run in Unity 2023.1.22f1. Renderer bounds test the imported
    /// OBJ mesh, not the coordinates from the offline mesh generator.
    /// </summary>
    public sealed class C3V9VisibleIndustrialRouteTests
    {
        const string Scene="Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync(Scene,LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameObject.Find("C3_V3_VerifiedCirculation"));
        }
        [UnityTest]
        public IEnumerator ReplacementStairsHaveMatchingPhysicalTreadsOnBothSides()
        {
            yield return Open();
            foreach (string side in new[] { "A_WEST", "B_EAST" })
            {
                Transform stair = GameObject.Find("STAIR_" + side).transform;
                Assert.IsNotNull(stair);
                for (int i = 1; i <= 22; i++)
                {
                    Transform tread = stair.Find("TREAD_" + i.ToString("D2"));
                    Assert.IsNotNull(tread);
                    Renderer renderer = tread.GetComponent<Renderer>();
                    Collider collider = tread.GetComponent<Collider>();
                    Assert.IsTrue(renderer.enabled && collider.enabled);
                    Assert.Less(Vector3.Distance(renderer.bounds.center, collider.bounds.center), .01f);
                    Assert.That(collider.bounds.max.y, Is.EqualTo(.56f + i * (3.94f / 22f)).Within(.01f));
                }
            }
            Assert.IsNull(GameObject.Find("V9_RIGHT_STAIR_WALKABLE_RAMP"));
            Assert.IsNotNull(GameObject.Find("UpperChipMaintenanceBench"));
        }
        [UnityTest]
        public IEnumerator BReceivingBeltActuallyExistsOnCFrontFaceAndMeetsMainHighBelt()
        {
            yield return Open();
            GameObject g=GameObject.Find("V9_BDoorToSorterVisibleBelt");
            Assert.IsNotNull(g,"V9 B-port feed mesh is missing");
            MeshRenderer r=g.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(r);
            Bounds b=r.bounds;
            Assert.Greater(b.min.z,11.1f,"B belt extends into front walkway");
            Assert.Less(b.min.z,12.0f,"Raised B feed still hidden entirely behind sorter");
            Assert.Greater(b.max.z,16.85f,"B conveyor does not reach C sorting line");
            Assert.Less(b.min.x,-6.15f, "Unity OBJ X-axis mirrored: B belt outside C hall");
            Assert.Greater(b.max.x,-.90f);
            Assert.Less(b.max.x,-.44f,"V9 feed intrudes through B-room wall");
            Assert.Greater(b.max.y,6.48f,"B feeder upper section should be visible over sorter");
            Assert.IsNotNull(GameObject.Find("V7_OPAQUE_C_TO_B_RECEIVING_SHUTTER"));
        }
        [UnityTest]
        public IEnumerator ReplacementDeckWorldBoundsMatchBothStairExitsAndRearBay()
        {
            yield return Open();
            var west = GameObject.Find("2F_WEST_SERVICE_ROUTE").GetComponent<Collider>();
            var east = GameObject.Find("2F_EAST_SERVICE_ROUTE").GetComponent<Collider>();
            var rear = GameObject.Find("2F_CONNECTING_REAR_ROUTE").GetComponent<Collider>();
            Assert.That(west.bounds.min.x, Is.EqualTo(-24.1f).Within(.01f));
            Assert.That(west.bounds.max.x, Is.EqualTo(-22.3f).Within(.01f));
            Assert.That(east.bounds.min.x, Is.EqualTo(-2.45f).Within(.01f));
            Assert.That(east.bounds.max.x, Is.EqualTo(-.65f).Within(.01f));
            foreach (Collider side in new[] { west, east })
            {
                Assert.That(side.bounds.min.z, Is.EqualTo(13.7f).Within(.01f));
                Assert.That(side.bounds.max.z, Is.EqualTo(rear.bounds.min.z).Within(.01f));
                Assert.That(side.bounds.max.y, Is.EqualTo(4.5f).Within(.01f));
            }
            Assert.That(rear.bounds.min.z, Is.EqualTo(18.1f).Within(.01f));
            Assert.That(rear.bounds.max.z, Is.EqualTo(21.3f).Within(.01f));
        }
        [UnityTest]
        public IEnumerator PlayerCanClimbRightStairAndReachRearMaintenanceBridge()
        {
            yield return Open();
            GameObject player = GameObject.Find("M2_Player");
            CharacterController cc = player.GetComponent<CharacterController>();
            foreach (MonoBehaviour behaviour in player.GetComponents<MonoBehaviour>())
                if (behaviour && behaviour.GetType().Name == "C3FirstPersonController") behaviour.enabled = false;
            cc.enabled = false;
            player.transform.position = new Vector3(-1.55f, .60f, 3.1f);
            cc.enabled = true;
            Physics.SyncTransforms();
            // Production capsule dimensions, not a smaller/slope-relaxed test walker.
            Vector3[] route = { new Vector3(-1.55f,4.5f,14.2f),
                new Vector3(-1.55f,4.5f,19.2f), new Vector3(-13.2f,4.5f,19.2f) };
            foreach (Vector3 goal in route)
            {
                for (int i = 0; i < 600; i++)
                {
                    Vector3 delta = goal - player.transform.position;
                    delta.y = 0f;
                    if (delta.magnitude < .08f) break;
                    cc.Move(delta.normalized * .04f + Vector3.down * .025f);
                    yield return null;
                }
                Assert.Less(Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                    new Vector2(goal.x, goal.z)), .15f, "Blocked before " + goal);
                Assert.Greater(player.transform.position.y, 4.2f, "Fell on upper route");
            }
        }
        [UnityTest]
        public IEnumerator PriorFloorStripeIsPassable()
        {
            yield return Open();
            GameObject walker=new GameObject("V9_TEST_STRIPE_PASSAGE");
            CharacterController cc=walker.AddComponent<CharacterController>();
            cc.height=1.80f;cc.center=Vector3.up*.90f;cc.radius=.27f;
            cc.stepOffset=.30f;cc.slopeLimit=54f;
            walker.transform.position=new Vector3(-5.14f,.70f,9.82f);
            for(int i=0;i<120;i++){cc.Move(new Vector3(0f,-.055f,-.043f));yield return null;}
            Assert.Less(walker.transform.position.z,5.6f);
            Object.Destroy(walker);
        }
    }
}
