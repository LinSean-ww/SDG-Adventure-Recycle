using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    /// <summary>Run from Unity Test Runner/PlayMode; these checks were NOT run in the
    /// file-authoring environment, which does not have Unity Editor installed.</summary>
    public sealed class C3V3CirculationTests
    {
        private const string Scene = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private IEnumerator OpenFactory()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameObject.Find("C3_V3_VerifiedCirculation"),
                "V3 circulation assembly must exist after loading C_Main.");
        }
        [UnityTest]
        public IEnumerator AllFivePedestrianSectionsHavePhysicalFloor()
        {
            yield return OpenFactory();
            Vector3[] testPoints =
            {
                new Vector3(-23.20f,5.4f,14.15f),
                new Vector3(-23.20f,5.4f,17.50f),
                new Vector3(-13.2f,5.4f,19.20f),
                new Vector3(-4.65f,5.4f,17.50f),
                new Vector3(-1.55f,5.4f,14.15f)
            };
            foreach (Vector3 start in testPoints)
            {
                Assert.IsTrue(Physics.Raycast(start, Vector3.down, out RaycastHit hit, 2.0f),
                    "Gap in 2F route at " + start);
                StringAssert.Contains("2F_", hit.collider.name,
                    "Expected one of the new solid 2F deck colliders: " + start);
                Assert.AreEqual(4.50f,hit.point.y,.06f,"Deck top height is discontinuous");
            }
        }
        [UnityTest]
        public IEnumerator StairRampsReachTheirDecksWithoutBarrierAcrossExit()
        {
            yield return OpenFactory();
            foreach (float z in new[] { 7.8f,9.5f,11.5f,13.5f,15.0f })
            {
                Assert.IsTrue(Physics.Raycast(new Vector3(-21.75f,6f,z), Vector3.down,
                    out RaycastHit hit,6f),"Main staircase missing at z="+z);
                StringAssert.Contains("V8_MAIN_STAIR",hit.collider.name);
            }
            Assert.IsNotNull(GameObject.Find("V8_MAIN_STAIR_TO_WEST_GALLERY"));
            Assert.IsNotNull(GameObject.Find("2F_CONNECTING_REAR_ROUTE"));
        }
        [UnityTest]
        public IEnumerator ConveyorHasOneFeedAndThreePhysicalIntakeOutputPairs()
        {
            yield return OpenFactory();
            Assert.IsNotNull(GameObject.Find("V3_LIVE_SINGLE_INFEED_RUBBER_SURFACE"));
            Assert.IsNotNull(GameObject.Find("V8_B_To_C_ContinuousRubber"));
            for (int lane=0;lane<3;lane++)
            {
                Assert.IsNotNull(GameObject.Find("V3_CONNECTED_DIVERT_TO_HOPPER_"+lane));
                Assert.IsNotNull(GameObject.Find("CONNECTED_LOWER_OUTFEED_"+lane));
                Assert.IsNotNull(GameObject.Find("SORTER_MAIN_BODY_"+lane));
            }
        }
        [UnityTest]
        public IEnumerator OriginalGameplayObjectsAndPlayerRemain()
        {
            yield return OpenFactory();
            Assert.IsNotNull(GameObject.Find("WasteSortingLine"));
            Assert.IsNotNull(GameObject.Find("M4_PuzzleConsole"));
            Assert.IsNotNull(GameObject.Find("Future_M3_ControlChip_2F_Only"));
            Type controller = Type.GetType("C3TreasurePuzzle.C3FirstPersonController, Assembly-CSharp");
            Type workshop = Type.GetType("C3TreasurePuzzle.EcoFactoryWorkshop, Assembly-CSharp");
            Assert.IsNotNull(controller);
            Assert.IsNotNull(workshop);
            Assert.IsNotNull(UnityEngine.Object.FindObjectOfType(controller));
            Assert.IsNotNull(UnityEngine.Object.FindObjectOfType(workshop));
        }
    }
}
