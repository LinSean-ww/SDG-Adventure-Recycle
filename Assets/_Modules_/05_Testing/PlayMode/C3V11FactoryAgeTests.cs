using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SDGAdventure.Testing
{
    public sealed class C3V11FactoryAgeTests
    {
        private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private static Component FindRuntime(string name)
        {
            Type type = Type.GetType("C3TreasurePuzzle." + name + ", Assembly-CSharp", true);
            return UnityEngine.Object.FindObjectOfType(type) as Component;
        }

        [UnityTest]
        public IEnumerator AgeingBuildsOnTheCurrentFactoryNotTheRetiredOverlay()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameObject.Find("C3_ReferenceRecyclingFactory_Visuals"));
            Assert.IsNotNull(GameObject.Find("V11_AGE_WALL_W_0"));
            Assert.IsNotNull(GameObject.Find("V11_AGE_WALL_N_0"));
            Assert.IsNotNull(GameObject.Find("V11_AGE_FLOOR_OIL_0"));
            Assert.IsNotNull(GameObject.Find("V11_AGE_PIPE_0"));
            Assert.IsNull(FindRuntime("C3FactoryDecay"),
                "Retired rubble pass must remain disabled; this is an in-place age layer.");
        }

        [UnityTest]
        public IEnumerator AllAgeingGeometryIsVisualOnly()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            Transform root = GameObject.Find("C3_ReferenceRecyclingFactory_Visuals").transform;
            int markers = 0;
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (!tr.name.StartsWith("V11_AGE_", StringComparison.Ordinal)) continue;
                markers++;
                Assert.IsNotNull(tr.GetComponent<Renderer>(), "Missing visual renderer: " + tr.name);
                foreach (Collider collider in tr.GetComponents<Collider>())
                    Assert.IsFalse(collider.enabled, "Age mark must not block the player: " + tr.name);
            }
            Assert.GreaterOrEqual(markers, 85);
        }

        [UnityTest]
        public IEnumerator ExistingConveyorAndDoorOpeningsRemainIntact()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            Component factory = FindRuntime("C3ReferenceFactoryEnvironment");
            Assert.IsNotNull(factory);
            Assert.IsTrue((bool)factory.GetType().GetProperty("ConveyorEndsInSorter").GetValue(factory));
            Component line = FindRuntime("C3RecyclingLineGeometry");
            Assert.IsNotNull(line);
            Material binding = line.GetType().GetProperty("BindingMaterial").GetValue(line) as Material;
            Assert.IsNotNull(binding);
            Assert.Less(binding.color.r, .45f, "Active production machinery should have an aged finish.");
            Assert.IsNotNull(GameObject.Find("V11_BC_PVC_CURTAIN"));
            // No ageing layer is allowed to place visual material in the B/C doorway.
            Transform root = GameObject.Find("C3_ReferenceRecyclingFactory_Visuals").transform;
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (!tr.name.StartsWith("V11_AGE_", StringComparison.Ordinal)) continue;
                Vector3 p = tr.position;
                Assert.IsFalse(p.x > -.65f && p.x < -.2f && p.z > 10.2f && p.z < 14.8f && p.y < 4.0f,
                    "Age geometry in B/C opening: " + tr.name);
            }
        }
    }
}
