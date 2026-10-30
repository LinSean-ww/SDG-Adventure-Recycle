using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    public sealed class M2CharacterIntegrationTests
    {
        private const string C3Scene = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";

        [UnityTest]
        public IEnumerator C3PlayerVisualUsesActualMovementAndStopsAtWall()
        {
            yield return SceneManager.LoadSceneAsync(C3Scene, LoadSceneMode.Single);
            GameObject player = GameObject.Find("M2_Player");
            Assert.IsNotNull(player);
            CharacterController motor = player.GetComponent<CharacterController>();
            Assert.IsNotNull(motor);
            Type boneType = Type.GetType("C3TreasurePuzzle.C3BoneController, Assembly-CSharp", true);
            MonoBehaviour bones = player.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(component => component != null && component.GetType() == boneType);
            Assert.IsTrue((bool)boneType.GetProperty("HasRequiredBones").GetValue(bones));
            Assert.AreEqual(1, player.GetComponentsInChildren<Camera>(true).Length);
            Assert.AreEqual(1, player.GetComponentsInChildren<AudioListener>(true).Length);

            MonoBehaviour controller = player.GetComponents<MonoBehaviour>()
                .Single(component => component.GetType().Name == "C3FirstPersonController");
            controller.enabled = false;
            motor.enabled = false;
            player.transform.SetPositionAndRotation(new Vector3(-21f, 0.55f, 5.5f), Quaternion.identity);
            motor.enabled = true;
            Transform leg = bones.transform.Find("BODYBONE/Ltopleg");
            Assert.IsNotNull(leg);
            Quaternion rest = leg.localRotation;
            Transform arm = bones.transform.Find("BODYBONE/downbone/topbone/LtopHand");
            Assert.IsNotNull(arm);
            Quaternion armRest = arm.localRotation;

            float maxLegAngle = 0f;
            for (int frame = 0; frame < 30; frame++)
            {
                motor.Move(new Vector3(0f, -0.05f, 0.04f));
                yield return new WaitForSeconds(0.02f);
                maxLegAngle = Mathf.Max(maxLegAngle, Quaternion.Angle(rest, leg.localRotation));
            }
            Assert.Greater(maxLegAngle, 2f, "Walking did not animate the leg.");

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.name = "M2 Test Wall";
                wall.transform.position = player.transform.position + Vector3.forward * 0.7f + Vector3.up;
                wall.transform.localScale = new Vector3(2f, 2f, 0.2f);
                yield return null;
                for (int frame = 0; frame < 35; frame++)
                {
                    motor.Move(new Vector3(0f, -0.05f, 0.04f));
                    yield return new WaitForSeconds(0.02f);
                }
                Assert.Less(Mathf.Abs(motor.velocity.z), 0.2f, "Wall did not stop horizontal motion.");
                Assert.Less(Quaternion.Angle(rest, leg.localRotation), 1f,
                    "Leg kept walking after actual horizontal movement stopped.");
                float maxIdleArmAngle = 0f;
                for (int frame = 0; frame < 25; frame++)
                {
                    motor.Move(new Vector3(0f, -0.05f, 0f));
                    yield return new WaitForSeconds(0.02f);
                    maxIdleArmAngle = Mathf.Max(maxIdleArmAngle,
                        Quaternion.Angle(armRest, arm.localRotation));
                }
                Assert.Greater(maxIdleArmAngle, 1f, "Idle arm motion was not observed.");
            }
            finally { UnityEngine.Object.Destroy(wall); }
        }

        [UnityTest]
        public IEnumerator C3CameraLookTurnsBodyClampsPitchAndMovesHeadOnce()
        {
            yield return SceneManager.LoadSceneAsync(C3Scene, LoadSceneMode.Single);
            GameObject player = GameObject.Find("M2_Player");
            Assert.IsNotNull(player);
            Camera camera = player.GetComponentInChildren<Camera>();
            Transform head = player.transform.Find("fk/BODYBONE/downbone/topbone/head");
            Assert.IsNotNull(head);
            Assert.AreEqual(1, player.GetComponentsInChildren<Camera>(true).Length);
            Assert.AreEqual(1, player.GetComponentsInChildren<AudioListener>(true).Length);
            Assert.IsFalse(player.GetComponentsInChildren<MonoBehaviour>(true)
                .Any(component => component != null && component.GetType().Name == "CameraLook"));

            MonoBehaviour controller = player.GetComponents<MonoBehaviour>()
                .Single(component => component.GetType().Name == "C3FirstPersonController");
            MethodInfo applyLook = controller.GetType().GetMethod("ApplyLook", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo tickMovement = controller.GetType().GetMethod("TickMovement", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(applyLook);
            Assert.IsNotNull(tickMovement);
            Quaternion headRest = head.localRotation;
            float yawBefore = player.transform.eulerAngles.y;

            applyLook.Invoke(controller, new object[] { new Vector2(5f, 0f) });
            Assert.AreEqual(10f, Mathf.DeltaAngle(yawBefore, player.transform.eulerAngles.y), 0.1f);

            applyLook.Invoke(controller, new object[] { new Vector2(0f, 100f) });
            yield return new WaitForSeconds(0.02f);
            Assert.AreEqual(-60f, Mathf.DeltaAngle(0f, camera.transform.localEulerAngles.x), 0.1f);
            Quaternion upExpected = Quaternion.AngleAxis(-60f, player.transform.right) *
                (head.parent.rotation * headRest);
            Assert.Less(Quaternion.Angle(upExpected, head.rotation), 0.5f);

            applyLook.Invoke(controller, new object[] { new Vector2(0f, -100f) });
            yield return new WaitForSeconds(0.02f);
            Assert.AreEqual(48f, Mathf.DeltaAngle(0f, camera.transform.localEulerAngles.x), 0.1f);
            Quaternion downExpected = Quaternion.AngleAxis(48f, player.transform.right) *
                (head.parent.rotation * headRest);
            Assert.Less(Quaternion.Angle(downExpected, head.rotation), 0.5f);

            CharacterController motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            player.transform.position = new Vector3(0f, 10f, 0f);
            motor.enabled = true;
            Vector3 before = player.transform.position;
            tickMovement.Invoke(controller, new object[] { Vector2.up, false, false, false, 0.1f });
            Vector3 displacement = player.transform.position - before;
            displacement.y = 0f;
            Assert.Greater(Vector3.Dot(displacement, player.transform.forward), 0.2f,
                "Movement did not follow the newly turned player root.");
        }

        [UnityTest]
        public IEnumerator C3ControllerStillJumpsFallsAndCollidesWithGround()
        {
            yield return SceneManager.LoadSceneAsync(C3Scene, LoadSceneMode.Single);
            GameObject player = GameObject.Find("M2_Player");
            CharacterController motor = player.GetComponent<CharacterController>();
            MonoBehaviour controller = player.GetComponents<MonoBehaviour>()
                .Single(component => component.GetType().Name == "C3FirstPersonController");
            MethodInfo tickMovement = controller.GetType().GetMethod("TickMovement", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(tickMovement);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                floor.name = "M2H Jump Test Floor";
                floor.transform.position = new Vector3(100f, 19.75f, 0f);
                floor.transform.localScale = new Vector3(10f, 0.5f, 10f);
                Physics.SyncTransforms();
                motor.enabled = false;
                player.transform.position = new Vector3(100f, 20.2f, 0f);
                motor.enabled = true;

                for (int frame = 0; frame < 50 && !motor.isGrounded; frame++)
                {
                    tickMovement.Invoke(controller, new object[] { Vector2.zero, false, false, false, 0.02f });
                    yield return null;
                }
                if (!motor.isGrounded)
                    motor.Move(Vector3.down * 0.15f);
                Assert.IsTrue(motor.isGrounded,
                    $"Gravity did not land the capsule on the floor: y={player.transform.position.y} velocity={motor.velocity} enabled={motor.enabled}");
                float groundedY = player.transform.position.y;
                tickMovement.Invoke(controller, new object[] { Vector2.zero, false, false, true, 0.02f });
                Assert.Greater(player.transform.position.y, groundedY + 0.05f,
                    "Jump did not raise the grounded player.");
                for (int frame = 0; frame < 110 && !motor.isGrounded; frame++)
                {
                    tickMovement.Invoke(controller, new object[] { Vector2.zero, false, false, false, 0.02f });
                    yield return null;
                }
                if (!motor.isGrounded)
                    motor.Move(Vector3.down * 0.15f);
                Assert.IsTrue(motor.isGrounded, "Player did not land after jumping.");
                Assert.AreEqual(groundedY, player.transform.position.y, 0.08f);
            }
            finally { UnityEngine.Object.Destroy(floor); }
        }
    }
}
