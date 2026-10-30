using System.Collections;
using System.Linq;
using System;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    public sealed class C3M2BlockoutPlayModeTests
    {
        private const string Scene = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";

        [UnityTest]
        public IEnumerator PlayerCapsuleCanUseBothStairsAndMainWalkway()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            GameObject player = GameObject.Find("M2_Player");
            Assert.IsNotNull(player);
            CharacterController capsule = player.GetComponent<CharacterController>();
            Assert.IsNotNull(capsule);
            MonoBehaviour controller = player.GetComponents<MonoBehaviour>()
                .Single(item => item.GetType().Name == "C3FirstPersonController");
            controller.enabled = false;
            capsule.enabled = false;
            player.transform.position = new Vector3(-21.1f, 0.55f, 5.5f);
            capsule.enabled = true;
            yield return null;

            // Deliberately move through the real CharacterController collision path, not by teleporting between floors.
            for (int i = 0; i < 174; i++)
            {
                capsule.Move(new Vector3(0f, -0.05f, 0.05f));
                yield return null;
            }
            Assert.Greater(player.transform.position.y, 4.25f, "West stair did not reach the upper level.");
            Assert.Greater(player.transform.position.z, 13.9f);

            for (int i = 0; i < 24; i++)
            {
                capsule.Move(new Vector3(0f, -0.05f, 0.05f));
                yield return null;
            }
            for (int i = 0; i < 152; i++)
            {
                capsule.Move(new Vector3(0.1f, -0.05f, 0f));
                yield return null;
            }
            Assert.AreEqual(-5.9f, player.transform.position.x, 0.25f, "Main walkway is blocked.");
            Assert.AreEqual(4.5f, player.transform.position.y, 0.25f, "Player left the upper walking surface.");

            for (int i = 0; i < 198; i++)
            {
                capsule.Move(new Vector3(0f, -0.08f, -0.05f));
                yield return null;
            }
            Assert.Less(player.transform.position.y, 0.85f, "East stair did not return to ground level.");
            Assert.Less(player.transform.position.z, 6f);

            // Midspan south railing must stop a standing capsule from leaving the deck.
            capsule.enabled = false;
            player.transform.position = new Vector3(-13.5f, 4.5f, 15f);
            capsule.enabled = true;
            yield return null;
            for (int i = 0; i < 40; i++)
            {
                capsule.Move(new Vector3(0f, -0.05f, -0.05f));
                yield return null;
            }
            Assert.Greater(player.transform.position.z, 13.9f, "South railing did not block the player capsule.");
            Assert.Greater(player.transform.position.y, 4.2f, "Player fell through the main walkway.");
        }

        [UnityTest]
        public IEnumerator OriginalPartComponentsAndConsoleStillCompleteTheLevel()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            GameObject player = GameObject.Find("M2_Player");
            Assert.IsNotNull(player);
            Type partType = Type.GetType("C3TreasurePuzzle.C3CollectiblePart, Assembly-CSharp", true);
            Component[] parts = UnityEngine.Object.FindObjectsOfType(partType).Cast<Component>().ToArray();
            Assert.AreEqual(3, parts.Length);
            foreach (Component part in parts)
            {
                MethodInfo canInteract = partType.GetMethod("CanInteract");
                MethodInfo interact = partType.GetMethod("Interact");
                Assert.IsTrue((bool)canInteract.Invoke(part, new object[] { player }), part.name);
                interact.Invoke(part, new object[] { player });
                Assert.IsFalse(part.gameObject.activeSelf, part.name + " should be collected and disabled.");
            }
            Type progressType = Type.GetType("C3TreasurePuzzle.C3TreasureProgress, Assembly-CSharp", true);
            Component progress = UnityEngine.Object.FindObjectOfType(progressType) as Component;
            Assert.AreEqual(3, progressType.GetProperty("CollectedCount").GetValue(progress));

            Type consoleType = Type.GetType("C3TreasurePuzzle.C3PuzzleConsole, Assembly-CSharp", true);
            Component console = GameObject.Find("M4_PuzzleConsole").GetComponent(consoleType);
            Assert.IsTrue((bool)consoleType.GetMethod("CanInteract").Invoke(console, new object[] { player }));
            consoleType.GetMethod("Interact").Invoke(console, new object[] { player });
            Assert.IsTrue((bool)consoleType.GetProperty("PanelOpen").GetValue(console));
            Type stepType = Type.GetType("C3TreasurePuzzle.C3ProcessingStep, Assembly-CSharp", true);
            foreach (string name in new[] { "Sorting", "Recycling", "PollutionTreatment" })
                Assert.IsTrue((bool)consoleType.GetMethod("SelectStep").Invoke(console, new[] { Enum.Parse(stepType, name) }));
            Assert.IsTrue((bool)consoleType.GetMethod("Confirm").Invoke(console, null));
            consoleType.GetMethod("ClosePanel").Invoke(console, null);
            Assert.IsTrue((bool)consoleType.GetMethod("OpenPanel").Invoke(console, null));
            Assert.IsTrue((bool)consoleType.GetMethod("ConfirmActivation").Invoke(console, null));
            Type deviceType = Type.GetType("C3TreasurePuzzle.C3DeviceState, Assembly-CSharp", true);
            Component device = UnityEngine.Object.FindObjectOfType(deviceType) as Component;
            Assert.IsFalse((bool)deviceType.GetProperty("LevelCompleted").GetValue(device));
            deviceType.GetMethod("NotifyStartupFinished").Invoke(device, null);
            Component exit = UnityEngine.Object.FindObjectOfType(Type.GetType("C3TreasurePuzzle.C3ExitTrigger, Assembly-CSharp", true)) as Component;
            Assert.IsTrue((bool)exit.GetType().GetMethod("TryEnter").Invoke(exit, new object[] { player.GetComponent<CharacterController>() }));
            Assert.IsTrue((bool)deviceType.GetProperty("LevelCompleted").GetValue(device));
        }

        [UnityTest]
        public IEnumerator CaptureBlockoutViewsForManualReview()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            GameObject player = GameObject.Find("M2_Player");
            Camera camera = player.GetComponentInChildren<Camera>();
            Assert.IsNotNull(camera);
            MonoBehaviour controller = player.GetComponents<MonoBehaviour>()
                .Single(item => item.GetType().Name == "C3FirstPersonController");
            controller.enabled = false;
            Directory.CreateDirectory(SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_M2_VisualReview"));

            player.transform.position = new Vector3(-12.5f, 0.55f, 7f);
            player.transform.rotation = Quaternion.identity;
            yield return null;
            Capture(camera, SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_M2_VisualReview/From1F.png"));

            player.transform.position = new Vector3(-19.5f, 4.5f, 15.4f);
            player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            yield return null;
            Capture(camera, SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_M2_VisualReview/From2F.png"));
        }

        private static void Capture(Camera camera, string path)
        {
            RenderTexture target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            RenderTexture oldActive = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                Texture2D image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.Destroy(image);
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
        }
    }
}
