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
            var belt = GameObject.Find("V8_B_To_C_ContinuousRubber");
            Assert.IsNotNull(belt, "V5 B-door conveyor prefab was not loaded");
            var renderers = belt.GetComponentsInChildren<MeshRenderer>(true);
            Assert.Greater(renderers.Length, 0, "B conveyor has no renderable mesh");
            Bounds all = renderers[0].bounds;
            foreach (var renderer in renderers) all.Encapsulate(renderer.bounds);
            Assert.LessOrEqual(all.min.x, -2.9f, "B-floor belt does not reach the lift");
            Assert.GreaterOrEqual(all.max.x, -.88f, "C-side receiving end does not reach the B door");
            Assert.LessOrEqual(all.max.x, -0.55f, "B belt protrudes through the B doorway into Zone B");
            Assert.LessOrEqual(all.min.y, 1.25f, "B-side receiving belt is still mounted too high");
            Assert.GreaterOrEqual(all.max.y, 5.6f, "B stair-clearance crossover is missing");
            Assert.LessOrEqual(all.max.y, 5.72f, "Conveyor crossover rises above the designed clearance");
            Assert.GreaterOrEqual(all.max.z, 16.8f, "B line does not reach the main conveyor");
            Assert.IsNotNull(GameObject.Find("V8_B_To_C_YellowGuidesAndCleats"));
            Assert.IsNotNull(GameObject.Find("V8_B_To_C_DrumRollers"));
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
