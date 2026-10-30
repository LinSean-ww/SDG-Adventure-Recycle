using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    /// <summary>
    /// V9 *Unity PlayMode* checks. None of these results can be claimed to pass
    /// until actually run in Unity 2023.1.22f1. Renderer bounds test the imported
    /// OBJ mesh, not the coordinates from the offline mesh generator.
    /// </summary>
    public sealed class C3V9VisibleIndustrialRouteTests
    {
        const string Scene="Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync(Scene,LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameObject.Find("C3_V3_VerifiedCirculation"));
        }
        [UnityTest]
        public IEnumerator VisibleRightStairExistsAndItsWorldBoundsMatchCWorkAisle()
        {
            yield return Open();
            GameObject go=GameObject.Find("V9_RightVisibleStairTreads");
            Assert.IsNotNull(go,"RIGHT-HAND staircase not instantiated; check Console errors / Resources importer");
            var rend=go.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(rend,"Stair imported but has no visible mesh");
            Assert.IsTrue(rend.enabled);
            Bounds b=rend.bounds;
            Assert.That(b.min.x, Is.InRange(-4.4f,-3.7f), "Unity OBJ X-axis mirrored: stair outside C hall");
            Assert.That(b.max.x, Is.InRange(-1.6f,-.8f));
            Assert.That(b.min.z, Is.InRange(2.0f,2.8f));
            Assert.That(b.max.y, Is.InRange(4.4f,4.7f));
            Assert.IsNotNull(GameObject.Find("V9_RIGHT_STAIR_WALKABLE_RAMP"));
            Assert.IsNotNull(GameObject.Find("V9_RIGHT_STAIR_EAST_GALLERY_JOIN"));
            Assert.IsNotNull(GameObject.Find("UpperChipMaintenanceBench"));
        }
        [UnityTest]
        public IEnumerator BReceivingBeltActuallyExistsOnCFrontFaceAndMeetsMainHighBelt()
        {
            yield return Open();
            GameObject g=GameObject.Find("V9_BDoorToSorterVisibleBelt");
            Assert.IsNotNull(g,"V9 B-port feed mesh is missing");
            MeshRenderer r=g.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(r);
            Bounds b=r.bounds;
            Assert.Greater(b.min.z,11.1f,"B belt extends into front walkway");
            Assert.Less(b.min.z,12.0f,"Raised B feed still hidden entirely behind sorter");
            Assert.Greater(b.max.z,16.85f,"B conveyor does not reach C sorting line");
            Assert.Less(b.min.x,-6.15f, "Unity OBJ X-axis mirrored: B belt outside C hall");
            Assert.Greater(b.max.x,-.90f);
            Assert.Less(b.max.x,-.44f,"V9 feed intrudes through B-room wall");
            Assert.Greater(b.max.y,6.48f,"B feeder upper section should be visible over sorter");
            Assert.IsNotNull(GameObject.Find("V7_OPAQUE_C_TO_B_RECEIVING_SHUTTER"));
        }
        [UnityTest]
        public IEnumerator ObjWorldHandednessMatchesCFactoryWalkwayAndColliderLayout()
        {
            yield return Open();
            var deck = GameObject.Find("V3_WalkwayDeck");
            Assert.IsNotNull(deck);
            Renderer d = deck.GetComponentInChildren<Renderer>();
            Assert.IsNotNull(d);
            Assert.That(d.bounds.min.x, Is.InRange(-24.4f, -23.8f),
                "Walkway x should be negative in C hall, not positive across boundary wall");
            var east = GameObject.Find("V7_EastWallSkywalkDeck");
            Assert.IsNotNull(east);
            Renderer er = east.GetComponentInChildren<Renderer>();
            Assert.That(er.bounds.min.x, Is.InRange(-4.9f,-4.4f));
            Assert.That(er.bounds.max.x, Is.InRange(-.9f,-.4f));
        }
        [UnityTest]
        public IEnumerator PlayerCanClimbRightStairAndReachRearMaintenanceBridge()
        {
            yield return Open();
            GameObject player=new GameObject("V9_TEST_WALKER");
            CharacterController cc=player.AddComponent<CharacterController>();
            cc.radius=.25f; cc.height=1.80f; cc.center=Vector3.up*.9f;
            cc.stepOffset=.30f;cc.slopeLimit=54f;
            player.transform.position=new Vector3(-2.95f,.69f,2.65f);
            for(int i=0;i<240;i++)
            {
                cc.Move(new Vector3(0f,-.08f,.036f));
                yield return null;
            }
            Assert.Greater(player.transform.position.z,10.25f,"Cannot climb visible staircase");
            Assert.Greater(player.transform.position.y,4.10f,"Not elevated to 2F");
            for(int i=0;i<36;i++){cc.Move(new Vector3(.040f,-.05f,0f));yield return null;}
            Assert.Greater(player.transform.position.x,-1.80f,"Stair landing did not join east gallery");
            for(int i=0;i<145;i++){cc.Move(new Vector3(0f,-.05f,.040f));yield return null;}
            Assert.Greater(player.transform.position.z,16.95f,"East gallery is blocked");
            for(int i=0;i<64;i++){cc.Move(new Vector3(-.040f,-.05f,0f));yield return null;}
            for(int i=0;i<53;i++){cc.Move(new Vector3(0f,-.05f,.040f));yield return null;}
            Assert.Greater(player.transform.position.z,18.70f,"Rear maintenance bridge cannot be reached");
            for(int i=0;i<210;i++){cc.Move(new Vector3(-.043f,-.05f,0f));yield return null;}
            Assert.Less(player.transform.position.x,-10f,"Bench on upper platform still unreachable");
            Assert.Greater(player.transform.position.y,4.12f,"Player fell through upper walkway");
            Object.Destroy(player);
        }
        [UnityTest]
        public IEnumerator PriorFloorStripeIsPassable()
        {
            yield return Open();
            GameObject walker=new GameObject("V9_TEST_STRIPE_PASSAGE");
            CharacterController cc=walker.AddComponent<CharacterController>();
            cc.height=1.80f;cc.center=Vector3.up*.90f;cc.radius=.27f;
            cc.stepOffset=.30f;cc.slopeLimit=54f;
            walker.transform.position=new Vector3(-5.14f,.70f,9.82f);
            for(int i=0;i<120;i++){cc.Move(new Vector3(0f,-.055f,-.043f));yield return null;}
            Assert.Less(walker.transform.position.z,5.6f);
            Object.Destroy(walker);
        }
    }
}
