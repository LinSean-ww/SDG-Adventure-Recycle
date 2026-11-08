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
            Assert.IsNotNull(GameObject.Find("P2_B_HIGH_INFEED_INTERFACE"));
            Assert.IsNotNull(GameObject.Find("P2_MAIN_ENTRY"));
            for (int i=0;i<5;i++)
            {
                GameObject section=GameObject.Find("P2_INFEED_"+i);
                Assert.IsNotNull(section,"Missing current B infeed section "+i);
                Transform surface=section.transform.Find("BELT_SURFACE");
                Assert.IsNotNull(surface);
                Assert.IsTrue(surface.GetComponent<Renderer>().enabled);
                Collider collider=surface.GetComponent<Collider>();
                Assert.IsTrue(collider.enabled && !collider.isTrigger);
                Transform roller=section.transform.Find("ROLLER");
                Assert.IsNotNull(roller,"Each section needs rollers.");
                Assert.IsTrue(roller.GetComponent<Renderer>().enabled,"Rollers must remain visible.");
            }
            Assert.Greater(GameObject.Find("P2_B_HIGH_INFEED_INTERFACE").transform.position.y-
                GameObject.Find("P2_MAIN_ENTRY").transform.position.y,1f,"Infeed must descend to the main belt.");
            var type=System.Type.GetType("C3TreasurePuzzle.C3RecyclingLineGeometry, Assembly-CSharp",true);
            var geometry=Object.FindObjectOfType(type);
            Assert.IsNotNull(geometry);
            Assert.IsTrue((bool)type.GetMethod("ConnectionsMatch").Invoke(geometry,new object[]{.01f}));
        }

        [UnityTest]
        public IEnumerator RaisedCeilingAndRealAccessibleStairEndpointsExist()
        {
            yield return Load();
            GameObject roof = GameObject.Find("C_Ceiling");
            Assert.IsNotNull(roof);
            Assert.AreEqual(12.5f, roof.transform.position.y, .06f);
            Assert.IsNotNull(GameObject.Find("V4_HIGH_ROOF_VISIBLE_DARK_STEEL"));
            var root=GameObject.Find("C3_V3_VerifiedCirculation").transform;
            Assert.IsNotNull(root.Find("STAIR_A_WEST/TREAD_22"));
            Collider deck=root.Find("2F_WEST_SERVICE_ROUTE").GetComponent<Collider>();
            Assert.IsTrue(deck.enabled && !deck.isTrigger);
            Vector3 above=deck.bounds.center;above.y=5.4f;
            Assert.IsTrue(Physics.Raycast(above,
                Vector3.down,out RaycastHit hit,1.8f),"Main stair upper landing missing");
            Assert.AreSame(deck,hit.collider);
            Assert.AreEqual(4.5f,hit.point.y,.08f);
        }

        [UnityTest]
        public IEnumerator WestStairRampCanCarryARealCharacterController()
        {
            yield return Load();
            Transform root=GameObject.Find("C3_V3_VerifiedCirculation").transform;
            Collider entry=root.Find("STAIR_A_WEST/BOTTOM_LANDING").GetComponent<Collider>();
            Collider deck=root.Find("2F_WEST_SERVICE_ROUTE").GetComponent<Collider>();
            GameObject walker = new GameObject("V4_TEST_TEMP_STAIR_WALKER");
            try
            {
                CharacterController body = walker.AddComponent<CharacterController>();
                body.height=1.8f;body.radius=.29f;body.center=Vector3.up*.9f;
                body.slopeLimit=48f;body.stepOffset=.24f;
                walker.transform.position=new Vector3(entry.bounds.center.x,entry.bounds.max.y+.08f,entry.bounds.center.z);
                Physics.SyncTransforms();
                // Physical Move calls, not a teleport between floors.
                for (int i=0;i<500 && walker.transform.position.z<deck.bounds.center.z-.1f;i++)
                {
                    body.Move(new Vector3(0f,-.04f,.042f));
                    yield return null;
                }
                Assert.Greater(walker.transform.position.z,15.0f,"Walker got blocked before the 2F deck");
                Assert.Greater(walker.transform.position.y,4.30f,"Walker's capsule could not ascend the stair");
                Assert.IsTrue(body.isGrounded,"Walker must finish on a real supporting collider.");
            }
            finally { Object.Destroy(walker); }
        }
    }
}
