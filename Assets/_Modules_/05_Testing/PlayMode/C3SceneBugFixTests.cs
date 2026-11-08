using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    public sealed class C3SceneBugFixTests
    {
        private const string Scene = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private static readonly string Output = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_BugFix_Evidence");

        [UnityTest]
        public IEnumerator CaptureBugFixViews()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            GameObject player = GameObject.Find("M2_Player");
            Assert.IsNotNull(player);
            Camera camera = player.GetComponentInChildren<Camera>();
            Assert.IsNotNull(camera);
            MonoBehaviour controller = player.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "C3FirstPersonController");
            controller.enabled = false;
            Directory.CreateDirectory(Output);

            player.transform.position = new Vector3(-12.5f, 0.55f, 5.6f);
            player.transform.rotation = Quaternion.identity;
            camera.transform.localRotation = Quaternion.identity;
            yield return null;
            Capture(camera, "After_Reactor.png");

            player.transform.position = new Vector3(-13f, 4.5f, 16.5f);
            player.transform.rotation = Quaternion.identity;
            camera.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            yield return null;
            Capture(camera, "After_ChipAndChineseSign.png");

            player.transform.position = new Vector3(-2.5f, 0.55f, 18.95f);
            player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            camera.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            yield return null;
            Capture(camera, "After_EastWallConsole.png");

            player.transform.position = new Vector3(-21f, 0.55f, 4f);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            camera.transform.localRotation = Quaternion.Euler(-9f, 0f, 0f);
            yield return null;
            Capture(camera, "After_EntryWarning.png");

            player.transform.position = new Vector3(-22.7f, 0.55f, 11f);
            player.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
            camera.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            yield return null;
            Capture(camera, "After_WestPipeAndHangers.png");

            player.transform.position = new Vector3(-13.5f, 4.5f, 15.4f);
            player.transform.rotation = Quaternion.identity;
            camera.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
            yield return null;
            Capture(camera, "After_UpperCeiling.png");

            player.transform.position = new Vector3(-22.6f, 0.55f, 17.8f);
            player.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
            camera.transform.localRotation = Quaternion.Euler(-13f, 0f, 0f);
            yield return null;
            Capture(camera, "After_WestElectricalChineseSign.png");

            player.transform.position = new Vector3(-20f, 0.55f, 19.5f);
            player.transform.rotation = Quaternion.identity;
            camera.transform.localRotation = Quaternion.Euler(23f, 0f, 0f);
            yield return null;
            Capture(camera, "After_FuseModel.png");

            player.transform.position = new Vector3(-4f, 0.55f, 15.7f);
            player.transform.rotation = Quaternion.identity;
            camera.transform.localRotation = Quaternion.Euler(23f, 0f, 0f);
            yield return null;
            Capture(camera, "After_FilterModel.png");
        }

        [UnityTest]
        public IEnumerator OriginalControllerMovesJumpsAndPhysicalSupportsBlock()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            GameObject player = GameObject.Find("M2_Player");
            Assert.IsNotNull(player);
            Camera camera = player.GetComponentInChildren<Camera>();
            CharacterController capsule = player.GetComponent<CharacterController>();
            MonoBehaviour controller = player.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "C3FirstPersonController");
            Assert.IsNotNull(camera);
            Assert.IsNotNull(capsule);
            Assert.IsTrue(player.GetComponents<MonoBehaviour>().Any(x => x.GetType().Name == "C3PlayerInteractor"));
            Assert.IsFalse(SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Any(x => x.GetType().Name == "SharedFirstPersonController"),
                "C3 must keep its original controller instead of adding a second shared player.");
            controller.enabled = false;
            MethodInfo tick = controller.GetType().GetMethod("TickMovement", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(tick);
            capsule.Move(Vector3.down * 0.1f);
            var contactEvidence=player.AddComponent<C3V11CollisionRecorder>();
            contactEvidence.CaptureDetails=true;
            Vector3 beforeMove=player.transform.position;
            tick.Invoke(controller, new object[] { new Vector2(0f, 1f), false, false, false, 0.1f });
            string evidence="Start="+beforeMove+" end="+player.transform.position+
                " forward="+player.transform.forward+" speed="+controller.GetType().GetField("currentMoveSpeed",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(controller)+
                " contacts="+string.Join(", ",contactEvidence.DetailedContacts.Select(c=>c.Collider.name+" normal="+c.Normal));
            UnityEngine.Object.Destroy(contactEvidence);
            Assert.Greater(Vector3.Dot(player.transform.position - beforeMove, player.transform.forward), 0.25f, "C3 player must move forward from the spawn. "+evidence);
            capsule.Move(Vector3.down * 0.1f);
            float beforeJump = player.transform.position.y;
            tick.Invoke(controller, new object[] { Vector2.zero, false, false, true, 0.02f });
            Assert.Greater(player.transform.position.y, beforeJump + 0.03f, "Original C3 jump step failed.");

            capsule.enabled = false;
            player.transform.position = new Vector3(-23f, 2.2f, 11f);
            capsule.enabled = true;
            yield return null;
            for (int i = 0; i < 40; i++) capsule.Move(new Vector3(-0.05f, 0f, 0f));
            Assert.Greater(player.transform.position.x, -24.08f, "Main west pipe collision did not stop the player capsule.");

            capsule.enabled = false;
            player.transform.position = new Vector3(-9f, 1f, 15.5f);
            capsule.enabled = true;
            yield return null;
            for (int i = 0; i < 40; i++) capsule.Move(new Vector3(-0.05f, 0f, 0f));
            Assert.Greater(player.transform.position.x, -9.82f, "Main deck brace collision did not stop the player capsule.");
        }

        [UnityTest]
        public IEnumerator BDoorSpawnIsCenteredClearStableAndCanApproachDoor()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            GameObject player = GameObject.Find("M2_Player");
            var body = player.GetComponent<CharacterController>();
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None)
                .Count(c => c.gameObject.name == "M2_Player"));
            var nearWall = GameObject.Find("C_Wall_4_Near").GetComponent<Collider>();
            var farWall = GameObject.Find("C_Wall_4_Far").GetComponent<Collider>();
            float centerZ = (nearWall.bounds.max.z + farWall.bounds.min.z) * .5f;
            Assert.That(player.transform.position.z, Is.EqualTo(centerZ).Within(.01f));
            Assert.That(player.transform.position.x, Is.EqualTo(-1.35f).Within(.02f));
            Assert.Greater(Vector3.Dot(player.transform.forward, Vector3.left), .99f);
            Assert.That(player.transform.position.y, Is.InRange(.53f, .62f));
            Physics.SyncTransforms();
            foreach (var other in Physics.OverlapCapsule(player.transform.position + Vector3.up * body.radius,
                player.transform.position + Vector3.up * (body.height - body.radius), body.radius,
                ~0, QueryTriggerInteraction.Ignore))
            {
                if (other == body || other.transform.IsChildOf(player.transform)) continue;
                Assert.IsFalse(Physics.ComputePenetration(body, player.transform.position, player.transform.rotation,
                    other, other.transform.position, other.transform.rotation, out _, out float distance) && distance > .001f,
                    "Spawn overlaps " + other.name);
            }
            var position = player.transform.position;
            yield return null;
            yield return null;
            Assert.That(player.transform.position.x, Is.EqualTo(position.x).Within(.02f), "Scene initialization moved the spawn.");
            Assert.That(player.transform.position.z, Is.EqualTo(position.z).Within(.02f));
            Assert.Greater(Vector3.Dot(player.transform.forward, Vector3.left), .99f, "Scene initialization changed the spawn rotation.");
            var controller = player.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "C3FirstPersonController");
            controller.enabled = false;
            for (int step = 0; step < 20; step++) body.Move(Vector3.right * .05f + Vector3.down * .01f);
            Assert.Greater(player.transform.position.x, -.5f, "The player could not reach the B doorway.");
            Assert.That(player.transform.position.y, Is.InRange(.50f, .65f), "Player fell through the floor.");
            var endBarrier = GameObject.Find("B_Safety_End").GetComponent<Collider>();
            for (int step = 0; step < 60; step++) body.Move(Vector3.right * .05f + Vector3.down * .01f);
            Assert.Less(player.transform.position.x, endBarrier.bounds.min.x, "The existing B safety barrier no longer blocks the route.");
            Debug.Log("B doorway route stops at x=" + player.transform.position.x + " before " + endBarrier.name + " at x=" + endBarrier.bounds.min.x);
        }

        private static void Capture(Camera camera, string file)
        {
            RenderTexture target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                Texture2D image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                image.Apply();
                File.WriteAllBytes(Output + "/" + file, image.EncodeToPNG());
                UnityEngine.Object.Destroy(image);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
        }
    }
}
