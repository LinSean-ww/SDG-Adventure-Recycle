using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SDGAdventure.Testing
{
    // Test assemblies don't reference Assembly-CSharp. Reflect runtime classes instead.
    // Run in Unity Editor > Window > General > Test Runner > PlayMode.
    public sealed class C3VisualFPSIntegrationTests
    {
        private const string C3Scene = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private static Component Find(string className)
        {
            Type type=Type.GetType(className + ", Assembly-CSharp", true);
            return UnityEngine.Object.FindObjectOfType(type) as Component;
        }
        private static object Call(Component item,string method, params object[] args) =>
            item.GetType().GetMethod(method).Invoke(item,args);
        private static object Get(Component item,string property) =>
            item.GetType().GetField(property).GetValue(item);

        [UnityTest]
        public IEnumerator IndustrialSignsAndWorkshopAreInstantiated()
        {
            yield return SceneManager.LoadSceneAsync(C3Scene,LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(Find("C3TreasurePuzzle.C3ReferenceFactoryEnvironment"));
            Assert.IsNotNull(Find("C3TreasurePuzzle.EcoFactoryWorkshop"));
            Assert.IsNotNull(Find("EcoAdventureRuntime"));
            GameObject d = GameObject.Find("C3_IndustrialSign_DZone");
            GameObject b = GameObject.Find("C3_IndustrialSign_BZone");
            Assert.IsNotNull(d);
            Assert.IsNotNull(b);
            Assert.IsNotNull(d.transform.Find("ReadableFront"));
            Assert.IsNotNull(d.transform.Find("ReadableRear"));
            Assert.IsNotNull(b.transform.Find("ReadableFront"));
            Assert.IsNotNull(b.transform.Find("ReadableRear"));
            Assert.IsNotNull(GameObject.Find("EASTER_EGG_EcoQuantumWorkshop"));
            Assert.IsNotNull(GameObject.Find("Hidden_WeaponAssemblyBench_F"));
            Assert.IsNotNull(GameObject.Find("Secondary_RecyclingInspectionWorkbench_F"));
            Assert.IsNotNull(Find("EcoWorkshopInteractable"));
            Assert.IsNotNull(GameObject.Find("Hidden_ElectronicScrapCache_F"));
            Assert.IsNotNull(Find("C3TreasurePuzzle.C3FactoryPresentation"));
        }

        [UnityTest]
        public IEnumerator ReferenceFactoryBuildsThreeSortersWithoutChangingPuzzle()
        {
            yield return SceneManager.LoadSceneAsync(C3Scene, LoadSceneMode.Single);
            yield return null;
            Component visual = Find("C3TreasurePuzzle.C3ReferenceFactoryEnvironment");
            Assert.IsNotNull(visual, "The reference recycling-plant art must attach to C_Main.");
            Assert.IsNotNull(GameObject.Find("C3_ReferenceRecyclingFactory_Visuals"));
            Assert.IsNotNull(GameObject.Find("C3_IndustrialSign_PaperHall"));
            Assert.IsNotNull(GameObject.Find("C3_IndustrialSign_MetalHall"));
            Assert.IsNotNull(GameObject.Find("C3_IndustrialSign_PlasticHall"));
            Assert.IsNotNull(GameObject.Find("C3_IndustrialSign_Assembly"));
            Assert.IsNotNull(GameObject.Find("Hidden_WeaponAssemblyBench_F"));
            Assert.IsNotNull(GameObject.Find("Secondary_RecyclingInspectionWorkbench_F"));
            Assert.IsNotNull(GameObject.Find("WasteSortingLine"));
            Assert.IsNotNull(GameObject.Find("M4_PuzzleConsole"));
            int rendered=(int)visual.GetType().GetProperty("DecorativeObjects").GetValue(visual);
            int packages=(int)visual.GetType().GetProperty("ConveyorPackageCount").GetValue(visual);
            Assert.Greater(rendered, 300);
            Assert.AreEqual(18, packages);
            Assert.IsTrue((bool)visual.GetType().GetProperty("ConveyorEndsInSorter").GetValue(visual));
            Assert.IsNotNull(GameObject.Find("V3_LIVE_SINGLE_INFEED_RUBBER_SURFACE"));
            GameObject machine = GameObject.Find("SORTER_MAIN_BODY_0");
            Assert.IsNotNull(machine, "The actual feed machine must exist below the shared belt.");
            Assert.IsNotNull(machine.GetComponent<BoxCollider>(), "The visible machine must be a physical obstacle.");
            for (int lane=0;lane<3;lane++)
            {
                GameObject input=GameObject.Find("V3_CONNECTED_DIVERT_TO_HOPPER_"+lane);
                GameObject output=GameObject.Find("CONNECTED_LOWER_OUTFEED_"+lane);
                GameObject body=GameObject.Find("SORTER_MAIN_BODY_"+lane);
                Assert.IsNotNull(input, "Each machine must connect to the upper belt by an inlet chute.");
                Assert.IsNotNull(output, "Each machine must connect to a lower discharge belt.");
                Assert.IsNotNull(body);
                Assert.That(Mathf.Abs(input.transform.position.x-body.transform.position.x), Is.LessThan(.01f));
                Assert.That(Mathf.Abs(output.transform.position.x-body.transform.position.x), Is.LessThan(.01f));
                Assert.That(Mathf.Abs(output.transform.position.z-11.48f), Is.LessThan(.01f));
            }
            Assert.IsNotNull(GameObject.Find("V8_B_To_C_ContinuousRubber"));
            Assert.That(Vector3.Distance(GameObject.Find("Hidden_WeaponAssemblyBench_F").transform.position,
                GameObject.Find("Secondary_RecyclingInspectionWorkbench_F").transform.position), Is.GreaterThan(6f));
            Assert.That(GameObject.Find("WasteSortingLine").transform.position.x, Is.EqualTo(-13f).Within(.01f));
            Assert.IsNull(Find("C3TreasurePuzzle.C3EnvironmentUpgrade"), "Old disconnected overlay must not be added.");
        }

        [UnityTest]
        public IEnumerator RecyclingCraftCreatesHotbarWeapon()
        {
            yield return SceneManager.LoadSceneAsync(C3Scene,LoadSceneMode.Single);
            yield return null;
            Component eco=Find("EcoAdventureRuntime");
            Assert.IsNotNull(eco);
            if (!(bool)Get(eco,"weaponCrafted"))
            {
                Call(eco,"FactoryPoweredOn");
                Call(eco,"ReceiveSortedMaterial",0);
                Call(eco,"ReceiveSortedMaterial",1);
                Assert.IsTrue((bool)Call(eco,"RecoverSortedMaterial",0));
                Assert.IsTrue((bool)Call(eco,"RecoverSortedMaterial",1));
                Assert.IsTrue((bool)Call(eco,"RecoverCircuit"));
                Assert.IsTrue((bool)Call(eco,"CraftWeapon"));
            }
            Assert.IsTrue((bool)Call(eco,"Has","eco_quantum_projector",1));
        }
    }
}
