using System.Collections;
using NUnit.Framework;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.TestTools;

namespace M7SharedPlayerTests
{
    public sealed class PlayerPerspectivePlayModeTests
    {
        private GameObject root;
        private GameObject wall;
        private Camera camera;
        private PlayerPerspectiveRig rig;
        private PlayerViewMode previousMode;

        [SetUp]
        public void Setup()
        {
            previousMode = PlayerViewPreference.Mode;
            PlayerViewPreference.Mode = PlayerViewMode.FirstPerson;
            root = new GameObject("CameraRigTestPlayer", typeof(CharacterController));
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            camera = cameraObject.GetComponent<Camera>();
            rig = root.AddComponent<PlayerPerspectiveRig>();
            rig.Bind(camera.transform);
        }

        [TearDown]
        public void Teardown()
        {
            PlayerViewPreference.Mode = previousMode;
            if (wall != null) Object.DestroyImmediate(wall);
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void DefaultIsFirstPersonAndDiagonalTravelIsNormalized()
        {
            Assert.IsFalse(rig.IsThirdPerson);
            PlayerViewPreference.Mode = PlayerViewMode.ThirdPerson;
            Vector3 forward = rig.GetThirdPersonTravel(new Vector2(0f, 1f));
            Vector3 diagonal = rig.GetThirdPersonTravel(new Vector2(1f, 1f));
            Assert.That(forward.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(diagonal.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(Vector3.Dot(diagonal.normalized, forward), Is.GreaterThan(0.6f));
        }

        [UnityTest]
        public IEnumerator FreeLookDoesNotTurnPlayerAndReturnKeepsPlayerPosition()
        {
            Vector3 start = root.transform.position;
            PlayerViewPreference.Mode = PlayerViewMode.ThirdPerson;
            rig.ApplyMode();
            rig.Orbit(60f, 12f, -60f, 60f);
            yield return null;
            Assert.That(root.transform.eulerAngles.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(camera.transform.position.z, Is.LessThan(-0.1f));
            PlayerViewPreference.Mode = PlayerViewMode.FirstPerson;
            rig.ApplyMode();
            Assert.That(camera.transform.localPosition.y, Is.EqualTo(1.5f).Within(0.01f));
            Assert.That(root.transform.position, Is.EqualTo(start));
        }

        [UnityTest]
        public IEnumerator ThirdPersonCameraStopsBeforeWall()
        {
            PlayerViewPreference.Mode = PlayerViewMode.ThirdPerson;
            rig.ApplyMode();
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.5f, -1.4f);
            wall.transform.localScale = new Vector3(8f, 5f, 0.2f);
            Physics.SyncTransforms();
            yield return null;
            Assert.That(camera.transform.position.z, Is.GreaterThan(-1.31f));
        }
    }
}
