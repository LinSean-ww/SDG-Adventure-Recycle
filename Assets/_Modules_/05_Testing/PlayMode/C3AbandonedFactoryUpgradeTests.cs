using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SDGAdventure.Testing
{
    // PlayMode regression checks. Run from Unity Test Runner (cannot be run by static file inspection).
    public sealed class C3AbandonedFactoryUpgradeTests
    {
        private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private static Type TypeOf(string typeName) => Type.GetType(typeName + ", Assembly-CSharp", true);
        private static Component Find(string name) => UnityEngine.Object.FindObjectOfType(TypeOf(name)) as Component;
        private static object Get(object component, string property) => component.GetType().GetProperty(property).GetValue(component);
        private static object Call(object component, string method, params object[] args) =>
            component.GetType().GetMethod(method).Invoke(component, args);

        [UnityTest]
        public IEnumerator RuntimeArtHasBFeedAndNonBlockingDecay()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            Component layout = Find("C3TreasurePuzzle.C3ReferenceFactoryEnvironment");
            Assert.IsNotNull(layout, "The new connected factory must be installed");
            Assert.Greater((int)Get(layout, "DecorativeObjects"), 300);
            Assert.IsTrue((bool)Get(layout, "ConveyorEndsInSorter"));
            GameObject conveyor = GameObject.Find("B區進料帶面");
            GameObject endpoint = GameObject.Find("B區入口_輸送帶連接端");
            Assert.IsNotNull(conveyor);
            Assert.IsNotNull(endpoint);
            Assert.That(endpoint.transform.position.x, Is.EqualTo(-0.7f).Within(.1f));
            Assert.That(endpoint.transform.position.z, Is.EqualTo(12.5f).Within(.1f));
            Assert.IsNotNull(GameObject.Find("V8_B_To_C_ContinuousRubber"));
            Assert.IsNull(Find("C3TreasurePuzzle.C3FactoryDecay"),
                "Obsolete visual rubble overlay would obstruct the new production floor");
        }

        [UnityTest]
        public IEnumerator ThreePartsGlowOnlyWhenPlayerIsNear()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            Component part = UnityEngine.Object.FindObjectsOfType(TypeOf("C3TreasurePuzzle.C3CollectiblePart"))
                .Cast<Component>().Single(c => (string)Get(c, "PartId") == "fuse");
            GameObject player = GameObject.Find("M2_Player");
            player.GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == "C3FirstPersonController").enabled = false;
            player.transform.position = new Vector3(-12f, 0.98f, 22f);
            yield return null;
            Assert.IsFalse((bool)Get(part, "IsGlowing"));
            player.transform.position = part.transform.position + Vector3.back * 0.8f;
            yield return null;
            Assert.IsTrue((bool)Get(part, "IsGlowing"));
            Assert.IsTrue(part.GetComponentInChildren<Light>().enabled);
            player.transform.position = new Vector3(-12f, 0.98f, 22f);
            yield return null;
            Assert.IsFalse((bool)Get(part, "IsGlowing"));
        }

        [UnityTest]
        public IEnumerator ActivateThenReachDExitToComplete()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            Component progress = Find("C3TreasurePuzzle.C3TreasureProgress");
            Component puzzle = Find("C3TreasurePuzzle.C3PuzzleState");
            Component device = Find("C3TreasurePuzzle.C3DeviceState");
            Component presentation = Find("C3TreasurePuzzle.C3FactoryPresentation");
            Component exit = Find("C3TreasurePuzzle.C3ExitTrigger");
            Assert.IsNotNull(exit);
            Collider player = GameObject.Find("M2_Player").GetComponent<CharacterController>();
            Assert.IsFalse((bool)Call(exit, "TryEnter", player));
            foreach (string id in new[] { "fuse", "control_chip", "filter_module" })
                Assert.IsTrue((bool)Call(progress, "TryCollect", id, id));
            Type step = TypeOf("C3TreasurePuzzle.C3ProcessingStep");
            Array order = Array.CreateInstance(step, 3);
            order.SetValue(Enum.Parse(step, "Sorting"), 0);
            order.SetValue(Enum.Parse(step, "Recycling"), 1);
            order.SetValue(Enum.Parse(step, "PollutionTreatment"), 2);
            Assert.IsTrue((bool)Call(puzzle, "TrySolve", order));
            Assert.IsTrue((bool)Call(device, "TryActivate"));
            Assert.IsFalse((bool)Call(exit, "TryEnter", player));
            yield return new WaitForSeconds(3.4f);
            Assert.IsTrue((bool)Get(presentation, "MotorActive"));
            Assert.AreEqual("Running", Get(device, "FactoryState").ToString());
            Assert.IsNotNull(GameObject.Find("C3_IndustrialSign_DZone"));
            Assert.IsTrue((bool)Get(Find("C3TreasurePuzzle.C3ReferenceFactoryEnvironment"), "ConveyorMoving"));
            Assert.IsFalse((bool)Get(device, "LevelCompleted"));
            // When there is no D_Main scene, the trigger completes and shows the result UI.
            Assert.IsTrue((bool)Call(exit, "TryEnter", player));
            Assert.IsTrue((bool)Get(device, "LevelCompleted"));
        }
    }
}
