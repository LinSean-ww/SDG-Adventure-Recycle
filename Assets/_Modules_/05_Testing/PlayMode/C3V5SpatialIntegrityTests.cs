using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    // Static geometry bounds and runtime collider checks for the V5 repair.
    // Not a replacement for walking both stairs in the Unity Game window.
    public class C3V5SpatialIntegrityTests
    {
        const string Scene = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameObject.Find("C3_V3_VerifiedCirculation"),
                "Runtime generated facility did not start");
        }

        [UnityTest]
        public IEnumerator BDoorHasVisibleFloorHeightConveyorAndOverheadConnection()
        {
            yield return Open();
            // The V11 route replaces the V8 floor belt with an elevated infeed.
            // Exercise its actual section joints, surfaces and doorway clearance.
            Component geometry=C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3RecyclingLineGeometry");
            Assert.IsNotNull(geometry);
            Transform entry=(Transform)C3V11Phase2RecyclingLineTests.Member(geometry,"BMaterialInterface");
            Assert.Greater(entry.position.y,6.8f);
            Assert.Greater(entry.position.x,0f);
            C3V11Phase2RecyclingLineTests.AssertEquipmentConnections(geometry);
            Transform proxies=GameObject.Find("C_CollisionProxies").transform;
            foreach(RaycastHit hit in Physics.SphereCastAll(new Vector3(.7f,7.12f,12.5f),.12f,Vector3.left,2f,~0,QueryTriggerInteraction.Ignore))
                Assert.IsFalse(hit.collider.transform.IsChildOf(proxies),"Infeed hits wall proxy "+hit.collider.name);
            foreach(string name in new[]{"C_Wall_4_Lintel","C_Wall_4_Near","C_Wall_4_Far","B_Door_Floor"})
                Assert.IsTrue(GameObject.Find(name).GetComponent<Collider>().enabled,name);
        }

        [UnityTest]
        public IEnumerator StairAndWalkwayMidlinesHaveNoSupportPosts()
        {
            yield return Open();
            var corridor = GameObject.Find("C3_V3_VerifiedCirculation");
            foreach(float x in new[] { -23.20f, -4.65f })
            foreach(float z in new[] { 14.55f, 17.70f })
            {
                foreach(var c in corridor.GetComponentsInChildren<Collider>(true))
                {
                    if (!c.enabled || c.isTrigger || !c.name.StartsWith("BRIDGE_GROUNDED_COLUMN_"))
                        continue;
                    Assert.Greater(Mathf.Abs(c.bounds.center.x - x), .55f,
                        "A post still blocks the pedestrian centerline at " + x + ", " + z);
                }
            }
        }
    }
}
