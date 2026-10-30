using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    /// <summary>V8 physical regression tests; MUST run in the Unity Editor, not static-checked.</summary>
    [Ignore("V8-only geometry replaced in V9; run C3V9VisibleIndustrialRouteTests instead")]
    public sealed class C3V8FactoryAccessAndFeedTests
    {
        const string Scene = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameObject.Find("C3_V3_VerifiedCirculation"));
        }
        [UnityTest]
        public IEnumerator BRoomIsHiddenAndOneVisibleConveyorReachesThreeSorters()
        {
            yield return Open();
            var shutter=GameObject.Find("V7_OPAQUE_C_TO_B_RECEIVING_SHUTTER");
            Assert.IsNotNull(shutter,"B interior must be opaque when viewed from C");
            Assert.IsTrue(shutter.GetComponent<BoxCollider>().enabled);
            var belt=GameObject.Find("V8_B_To_C_ContinuousRubber");
            Assert.IsNotNull(belt, "The authored continuous B-to-C feed did not load");
            var render=belt.GetComponentInChildren<MeshRenderer>();
            Assert.IsNotNull(render);
            Bounds bounds=render.bounds;
            Assert.Greater(bounds.max.x,-.88f,"Feed end must meet the C side of B door");
            Assert.Less(bounds.max.x,-.55f,"Feed entered the B room");
            Assert.LessOrEqual(bounds.min.x,-6.18f,"Feed does not meet the main sorter");
            Assert.GreaterOrEqual(bounds.max.z,16.84f,"Feed is not attached to the high line");
            Assert.AreEqual(5.62f,bounds.max.y,.10f,"Feed top does not align with high main conveyor");
            for(int lane=0;lane<3;lane++)
            {
                Assert.IsNotNull(GameObject.Find("V3_CONNECTED_DIVERT_TO_HOPPER_"+lane));
                Assert.IsNotNull(GameObject.Find("CONNECTED_LOWER_OUTFEED_"+lane));
            }
        }
        [UnityTest]
        public IEnumerator VisibleStairIsRealAndConnectsToOriginalMaintenanceWorkstation()
        {
            yield return Open();
            Assert.IsNotNull(GameObject.Find("V8_VisibleStairTreadsAndLanding"));
            Assert.IsNotNull(GameObject.Find("V8_VisibleStairRails"));
            Assert.IsNotNull(GameObject.Find("V8_MAIN_STAIR_CONTINUOUS_RAMP"));
            Assert.IsNotNull(GameObject.Find("V8_MAIN_STAIR_TO_WEST_GALLERY"));
            Assert.IsNotNull(GameObject.Find("UpperChipMaintenanceBench"),
                "Original 2F maintenance workbench is missing");
            Assert.IsNotNull(GameObject.Find("UpperDistributionBox"));
            GameObject walker=new GameObject("V8_WEST_STAIR_TEST_CONTROLLER");
            var cc=walker.AddComponent<CharacterController>();
            cc.height=1.8f;cc.radius=.28f;cc.center=Vector3.up*.9f;
            cc.stepOffset=.30f;cc.slopeLimit=53f;
            walker.transform.position=new Vector3(-21.75f,.66f,6.85f);
            for(int i=0;i<220;i++)
            {
                cc.Move(new Vector3(0f,-.05f,.041f));
                yield return null;
            }
            Assert.Greater(walker.transform.position.z,15.0f,"Unable to ascend primary stair");
            Assert.Greater(walker.transform.position.y,4.24f,"Stair fails to meet 2F height");
            // Move across the intentional inner yellow rail opening to the west gallery.
            for(int i=0;i<46;i++) { cc.Move(new Vector3(-.036f,-.04f,0f));yield return null; }
            Assert.Less(walker.transform.position.x,-22.85f,"Gallery guard blocked stair connection");
            // Then follow the west aisle and turn into the rear maintenance corridor.
            for(int i=0;i<104;i++) { cc.Move(new Vector3(0f,-.04f,.040f));yield return null; }
            Assert.Greater(walker.transform.position.z,18.6f,"West gallery doesn't reach rear route");
            for(int i=0;i<205;i++) { cc.Move(new Vector3(.045f,-.04f,0f));yield return null; }
            Assert.Greater(walker.transform.position.x,-14.0f,"Cannot approach upper workbench");
            Assert.Greater(walker.transform.position.y,4.20f,"Fell off second-floor walkway");
            Object.Destroy(walker);
        }
        [UnityTest]
        public IEnumerator FormerYellowStripeRemainsTraversableAtReportedPosition()
        {
            yield return Open();
            GameObject walker=new GameObject("V8_TEST_OLD_FLOOR_BLOCKER");
            var cc=walker.AddComponent<CharacterController>();
            cc.height=1.8f;cc.radius=.29f;cc.center=Vector3.up*.9f;
            cc.stepOffset=.30f;cc.slopeLimit=53f;
            walker.transform.position=new Vector3(-5.14f,.66f,9.82f);
            for(int i=0;i<120;i++){ cc.Move(new Vector3(0f,-.045f,-.04f));yield return null; }
            Assert.Less(walker.transform.position.z,5.65f,"Still blocked near reported stripe");
            Object.Destroy(walker);
        }
    }
}
