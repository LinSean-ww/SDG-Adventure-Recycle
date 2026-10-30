using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    /// <summary>
    /// V4 regression checks. Run in Unity 2023.1.22f1 Test Runner PlayMode.
    /// NOTE: This file was created without a local Unity Editor; these tests
    /// have NOT been executed by the authoring environment.
    /// </summary>
    public sealed class C3V4BFeedAndAccessTests
    {
        private const string Scene =
            "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameObject.Find("C3_V3_VerifiedCirculation"));
        }

        [UnityTest]
        public IEnumerator BZoneHasVisibleInputBeltRollersAndConveyorLift()
        {
            yield return Load();
            foreach (string name in new []
            {
                "V8_B_To_C_ContinuousRubber", "V8_B_To_C_YellowGuidesAndCleats",
                "V8_B_To_C_DrumRollers", "V3_LIVE_SINGLE_INFEED_RUBBER_SURFACE"
            })
            {
                GameObject go = GameObject.Find(name);
                Assert.IsNotNull(go, "B to classification path missing: " + name);
                Assert.Greater(go.GetComponentsInChildren<MeshRenderer>(true).Length, 0,
                    name + " imported prefab has no visible mesh");
            }
        }

        [UnityTest]
        public IEnumerator RaisedCeilingAndRealAccessibleStairEndpointsExist()
        {
            yield return Load();
            GameObject roof = GameObject.Find("C_Ceiling");
            Assert.IsNotNull(roof);
            Assert.AreEqual(12.5f, roof.transform.position.y, .06f);
            Assert.IsNotNull(GameObject.Find("V4_HIGH_ROOF_VISIBLE_DARK_STEEL"));
            Assert.IsNotNull(GameObject.Find("V8_MAIN_STAIR_CONTINUOUS_RAMP"));
            Assert.IsNotNull(GameObject.Find("V8_MAIN_STAIR_UPPER_LANDING"));
            Assert.IsTrue(Physics.Raycast(new Vector3(-21.75f,5.4f,15.55f),
                Vector3.down,out RaycastHit hit,1.8f),"Main stair upper landing missing");
            Assert.AreEqual(4.5f,hit.point.y,.08f);
        }

        [UnityTest]
        public IEnumerator WestStairRampCanCarryARealCharacterController()
        {
            yield return Load();
            GameObject walker = new GameObject("V4_TEST_TEMP_STAIR_WALKER");
            CharacterController body = walker.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = .29f;
            body.center = Vector3.up*.9f;
            body.slopeLimit = 48f;
            body.stepOffset = .24f;
            walker.transform.position = new Vector3(-21.75f,.64f,6.90f);
            // Physical Move calls, NOT NavMesh reachability assumptions.
            for (int i=0;i<215;i++)
            {
                body.Move(new Vector3(0f,-.06f,.042f));
                yield return null;
            }
            Assert.Greater(walker.transform.position.z,15.0f,
                "Walker's forward route got blocked before the 2F deck");
            Assert.Greater(walker.transform.position.y,4.30f,
                "Walker's capsule could not ascend the stair's sloping collider");
            Object.Destroy(walker);
        }
    }
}
