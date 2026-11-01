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
            yield return SceneManager.LoadSceneAsync(C3Scene,LoadSceneMode.Single);
            yield return null;
            Component visual=Find("C3TreasurePuzzle.C3ReferenceFactoryEnvironment");
            Component geometry=Find("C3TreasurePuzzle.C3RecyclingLineGeometry");
            Assert.IsNotNull(visual); Assert.IsNotNull(geometry);
            Physics.SyncTransforms();
            M6IntegrationTests.C3V11Phase2RecyclingLineTests.AssertEquipmentConnections(geometry);
            foreach (string label in new[] {"PaperHall","MetalHall","PlasticHall","Assembly"})
                Assert.IsNotNull(GameObject.Find("C3_IndustrialSign_"+label));
            foreach (string name in new[] {"Hidden_WeaponAssemblyBench_F","Secondary_RecyclingInspectionWorkbench_F",
                "WasteSortingLine","M4_PuzzleConsole","V11_BC_PVC_CURTAIN"}) Assert.IsNotNull(GameObject.Find(name));
            Assert.AreEqual(0,(int)visual.GetType().GetProperty("ConveyorPackageCount").GetValue(visual),"Dormant has no pre-spawned fake products");
            Assert.IsTrue((bool)visual.GetType().GetProperty("ConveyorEndsInSorter").GetValue(visual));
            for (int lane=0;lane<3;lane++)
            {
                GameObject machine=GameObject.Find("SORTER_MAIN_BODY_"+lane);
                Assert.IsNotNull(machine); Assert.IsTrue(machine.GetComponent<BoxCollider>().enabled);
            }
            Assert.That(Vector3.Distance(GameObject.Find("Hidden_WeaponAssemblyBench_F").transform.position,
                GameObject.Find("Secondary_RecyclingInspectionWorkbench_F").transform.position),Is.GreaterThan(6f));
            Assert.That(GameObject.Find("WasteSortingLine").transform.position.x,Is.EqualTo(-13f).Within(.01f));
            Assert.IsNull(Find("C3TreasurePuzzle.C3EnvironmentUpgrade"));
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
