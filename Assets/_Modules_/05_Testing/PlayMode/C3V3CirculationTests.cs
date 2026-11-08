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
                new Vector3(-1.55f,5.4f,17.50f),
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
            Transform root=GameObject.Find("C3_V3_VerifiedCirculation").transform;
            foreach (var side in new[] { (stair:"STAIR_A_WEST",deck:"2F_WEST_SERVICE_ROUTE"),
                                         (stair:"STAIR_B_EAST",deck:"2F_EAST_SERVICE_ROUTE") })
            {
                Transform stair=root.Find(side.stair);
                Assert.IsNotNull(stair,side.stair);
                Collider deck=GameObject.Find(side.deck).GetComponent<Collider>();
                Assert.IsTrue(deck.enabled && !deck.isTrigger,side.deck);
                float previousTop=.56f;
                Collider last=null;
                for(int step=1;step<=22;step++)
                {
                    Transform tread=stair.Find("TREAD_"+step.ToString("D2"));
                    Assert.IsNotNull(tread,side.stair+" tread "+step);
                    last=tread.GetComponent<Collider>();
                    Assert.IsNotNull(last);Assert.IsTrue(last.enabled && !last.isTrigger);
                    Assert.IsTrue(tread.GetComponent<Renderer>().enabled);
                    Assert.That(last.bounds.max.y-previousTop,Is.InRange(.17f,.30f));
                    previousTop=last.bounds.max.y;
                }
                Assert.That(last.bounds.max.y,Is.EqualTo(deck.bounds.max.y).Within(.01f));
                if(side.stair=="STAIR_B_EAST")
                {
                    Collider landing=root.Find("B_EAST_LEVEL_LANDING").GetComponent<Collider>();
                    Assert.IsTrue(landing.enabled && !landing.isTrigger);
                    Assert.That(last.bounds.max.z,Is.EqualTo(landing.bounds.min.z).Within(.01f));
                    Assert.That(landing.bounds.max.z,Is.EqualTo(deck.bounds.min.z).Within(.01f));
                    Assert.That(landing.bounds.max.y,Is.EqualTo(deck.bounds.max.y).Within(.01f));
                }
                else Assert.That(last.bounds.max.z,Is.EqualTo(deck.bounds.min.z).Within(.01f));
            }
            Assert.IsNotNull(GameObject.Find("2F_CONNECTING_REAR_ROUTE"));
        }
        [UnityTest]
        public IEnumerator ConveyorHasOneFeedAndThreePhysicalIntakeOutputPairs()
        {
            yield return OpenFactory();
            Assert.IsNotNull(GameObject.Find("P2_B_HIGH_INFEED_INTERFACE"));
            Assert.IsNotNull(GameObject.Find("P2_MAIN_BELT"));
            Type geometryType=Type.GetType("C3TreasurePuzzle.C3RecyclingLineGeometry, Assembly-CSharp",true);
            var geometry=UnityEngine.Object.FindObjectOfType(geometryType);
            Assert.IsNotNull(geometry);
            Assert.IsTrue((bool)geometryType.GetMethod("ConnectionsMatch").Invoke(geometry,new object[] { .01f }),
                "Current infeed and all three machine paths must have connected, visible solid belt surfaces.");
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
