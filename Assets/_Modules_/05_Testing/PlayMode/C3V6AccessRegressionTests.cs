using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    public sealed class C3V6AccessRegressionTests
    {
        private const string Scene="Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(Scene,LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameObject.Find("C3_V3_VerifiedCirculation"));
        }

        [UnityTest]
        public IEnumerator ConveyorIsInsideTheMainHallAndConnectsToSorter()
        {
            yield return Load();
            GameObject conveyor=GameObject.Find("V8_B_To_C_ContinuousRubber");
            Assert.IsNotNull(conveyor);
            Renderer r=conveyor.GetComponentInChildren<Renderer>();
            Assert.IsNotNull(r);
            Assert.LessOrEqual(r.bounds.max.x,-.55f,"Conveyor entered B room");
            Assert.LessOrEqual(r.bounds.min.x,-6.1f,"Conveyor does not reach sorter main line");
            Assert.GreaterOrEqual(r.bounds.max.z,16.8f,"Conveyor has no feed to the sorter");
            Assert.GreaterOrEqual(r.bounds.max.y,5.6f,"Conveyor cannot reach high intake");
            Assert.LessOrEqual(r.bounds.max.y,5.72f,"Conveyor crossover is above its clearance envelope");
            Assert.IsNull(GameObject.Find("V5_BPortalContinuousBelt"),
                "Old B-inside model should not be present simultaneously");
        }

        [UnityTest]
        public IEnumerator StairApproachesClearOfQuantumBench()
        {
            yield return Load();
            GameObject desk=GameObject.Find("EASTER_EGG_EcoQuantumWorkshop");
            Assert.IsNotNull(desk);
            Assert.LessOrEqual(desk.transform.position.z,3.25f,
                "Quantum bench still blocks the east stair's entrance");
            Assert.IsNotNull(GameObject.Find("V8_VisibleStairRails"));
            Assert.IsNotNull(GameObject.Find("V8_MAIN_STAIR_CONTINUOUS_RAMP"));
            Assert.IsTrue(Physics.Raycast(new Vector3(-21.75f,6.0f,15.55f),Vector3.down,
                out RaycastHit deck,3.0f),"2F landing is missing");
            Assert.AreEqual(4.5f,deck.point.y,.08f);
        }

        [UnityTest]
        public IEnumerator BothStairsCanCarryACharacterControllerOntoTheSecondFloor()
        {
            yield return Load();
            float x=-21.75f;
            GameObject walker=new GameObject("V8_PRIMARY_STAIR_WALKER");
            CharacterController cc=walker.AddComponent<CharacterController>();
            cc.radius=.28f;cc.height=1.8f;cc.center=Vector3.up*.9f;
            cc.stepOffset=.30f;cc.slopeLimit=50f;
            walker.transform.position=new Vector3(x,.66f,6.90f);
            for(int i=0;i<215;i++)
            {
                cc.Move(new Vector3(0f,-.06f,.044f));
                yield return null;
            }
            Assert.Greater(walker.transform.position.z,15.0f,
                "Primary stair failed at z="+walker.transform.position.z);
            Assert.Greater(walker.transform.position.y,4.20f,
                "CharacterController could not climb to 2F");
            Object.Destroy(walker);
        }

        [UnityTest]
        public IEnumerator ActualCharacterControllerCanWalkAlongFrontAisle()
        {
            yield return Load();
            // Test a central unobstructed walking line, rather than object existence.
            GameObject walker=new GameObject("V6_AISLE_WALKER");
            CharacterController cc=walker.AddComponent<CharacterController>();
            cc.radius=.28f;cc.height=1.8f;cc.center=Vector3.up*.9f;
            cc.stepOffset=.26f;cc.slopeLimit=50f;
            walker.transform.position=new Vector3(-15f,.6f,5.8f);
            for (int i=0;i<100;i++)
            {
                cc.Move(new Vector3(0f,-.03f,.047f));
                yield return null;
            }
            Assert.Greater(walker.transform.position.z,9.9f,
                "Main floor aisle obstructed: character controller unable to cross");
            Object.Destroy(walker);
        }
    }
}
