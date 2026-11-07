using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace M6IntegrationTests
{
    public sealed class C3V13BakedFactoryTests
    {
        C3V11SafeCirculationTests scope;
        static Type TypeOf(string name) => Type.GetType("C3TreasurePuzzle." + name + ", Assembly-CSharp", true);
        static Component Find(string name) => Object.FindObjectOfType(TypeOf(name)) as Component;
        static object Get(object o, string n) => C3V11Phase2RecyclingLineTests.Member(o, n);
        [UnitySetUp] public IEnumerator Open() { scope = new C3V11SafeCirculationTests(); yield return scope.OpenFactory(); }
        [UnityTearDown] public IEnumerator Close() { if (scope != null) yield return scope.Cleanup(); }

        [UnityTest] public IEnumerator SavedGeometryRebindsAndReloadsWithoutDuplicateEquipment()
        {
            for (int round = 0; round < 2; round++)
            {
                var manifest = Find("C3BakedFactory"); Assert.IsNotNull(manifest);
                GameObject[] authored = (GameObject[])Get(manifest, "generatedObjects");
                Assert.IsTrue(authored.All(g => g));
                var geo = Find("C3RecyclingLineGeometry");
                Assert.AreEqual(30, ((IList)Get(geo, "Sections")).Count);
                Assert.AreEqual(3, ((IList)Get(geo, "Machines")).Count);
                Assert.AreEqual(true, geo.GetType().GetMethod("ConnectionsMatch").Invoke(geo, new object[] { .01f }));
                foreach (string name in new[] { "C3_ReferenceRecyclingFactory_Visuals", "C3_P2_RecyclingLine", "C3_V3_VerifiedCirculation", "EASTER_EGG_EcoQuantumWorkshop", "P2_TRACKED_MATERIALS" })
                    Assert.AreEqual(1, Object.FindObjectsOfType<Transform>().Count(t => t.name == name), name);
                // The nonserialized property block must exist after loading a saved scene.
                geo.GetType().GetMethod("SetPress").Invoke(geo, new object[] { 0, .5f });
                geo.GetType().GetMethod("SetPress").Invoke(geo, new object[] { 0, 0f });
                Assert.IsNotNull(Find("C3RecyclingLine"));
                Assert.AreEqual(0, Get(Find("C3TreasureProgress"), "CollectedCount"));
                if (round == 0)
                {
                    yield return SceneManager.LoadSceneAsync("Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity");
                    yield return null; yield return null;
                }
            }
        }
    }
}
