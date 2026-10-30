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
    public sealed class C3FactoryFullArtTests
    {
        private const string Scene = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private static Type TypeOf(string fullName) => Type.GetType(fullName + ", Assembly-CSharp", true);
        private static Component Find(string fullName) => UnityEngine.Object.FindObjectOfType(TypeOf(fullName)) as Component;

        [UnityTest]
        public IEnumerator StageA_UpperDeckChipRemainsCollectibleAndHudHintIsCurrent()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            GameObject player = GameObject.Find("M2_Player");
            Type partType = TypeOf("C3TreasurePuzzle.C3CollectiblePart");
            Component[] parts = UnityEngine.Object.FindObjectsOfType(partType).Cast<Component>().ToArray();
            Assert.AreEqual(3, parts.Length);
            Component chip = parts.Single(part => (string)partType.GetProperty("PartId").GetValue(part) == "control_chip");
            Assert.AreEqual(new Vector3(-13.2f, 5.37f, 19.25f), chip.transform.position);
            Assert.IsNotNull(chip.GetComponent<Collider>());
            Vector3 eye = new Vector3(-13.2f, 6f, 18f);
            Vector3 direction = (chip.transform.position - eye).normalized;
            Assert.IsTrue(Physics.Raycast(eye, direction, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore));
            Assert.AreEqual(chip.gameObject, hit.collider.gameObject, "A normal upper-deck view must hit the chip first.");

            Component progress = Find("C3TreasurePuzzle.C3TreasureProgress");
            Assert.IsTrue((bool)progress.GetType().GetMethod("TryCollect").Invoke(progress, new object[] { "fuse", "保險絲" }));
            Component hud = Find("C3TreasurePuzzle.C3TreasureHud");
            string guidance = (string)hud.GetType().GetProperty("GuidanceText").GetValue(hud);
            StringAssert.Contains("二樓", guidance);
            Assert.IsTrue((bool)partType.GetMethod("CanInteract").Invoke(chip, new object[] { player }));
            partType.GetMethod("Interact").Invoke(chip, new object[] { player });
            Assert.AreEqual(2, progress.GetType().GetProperty("CollectedCount").GetValue(progress));
            Assert.IsFalse(chip.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Final_ConsoleRejectsIncompleteAndWrongOrderThenActivatesAudio()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            GameObject player = GameObject.Find("M2_Player");
            Component progress = Find("C3TreasurePuzzle.C3TreasureProgress");
            Component console = Find("C3TreasurePuzzle.C3PuzzleConsole");
            Component device = Find("C3TreasurePuzzle.C3DeviceState");
            Component hud = Find("C3TreasurePuzzle.C3TreasureHud");
            var p = progress.GetType();
            var c = console.GetType();
            var d = device.GetType();
            c.GetMethod("Interact").Invoke(console, new object[] { player });
            Assert.IsFalse((bool)c.GetProperty("PanelOpen").GetValue(console));
            Assert.IsFalse((bool)d.GetMethod("TryActivate").Invoke(device, null));
            foreach (string id in new[] { "fuse", "control_chip", "filter_module" })
                Assert.IsTrue((bool)p.GetMethod("TryCollect").Invoke(progress, new object[] { id, id }));
            StringAssert.Contains("3/3", (string)hud.GetType().GetProperty("StatusText").GetValue(hud));
            Assert.IsTrue((bool)c.GetMethod("OpenPanel").Invoke(console, null));
            Type stepType = TypeOf("C3TreasurePuzzle.C3ProcessingStep");
            foreach (string step in new[] { "Recycling", "Sorting", "PollutionTreatment" })
                Assert.IsTrue((bool)c.GetMethod("SelectStep").Invoke(console, new[] { Enum.Parse(stepType, step) }));
            Assert.IsFalse((bool)c.GetMethod("Confirm").Invoke(console, null));
            Assert.IsFalse((bool)c.GetMethod("ConfirmActivation").Invoke(console, null));
            c.GetMethod("Retry").Invoke(console, null);
            foreach (string step in new[] { "Sorting", "Recycling", "PollutionTreatment" })
                Assert.IsTrue((bool)c.GetMethod("SelectStep").Invoke(console, new[] { Enum.Parse(stepType, step) }));
            Assert.IsTrue((bool)c.GetMethod("Confirm").Invoke(console, null));
            Assert.IsFalse((bool)d.GetProperty("LevelCompleted").GetValue(device));
            c.GetMethod("ClosePanel").Invoke(console, null);
            yield return null;
            Assert.IsTrue((bool)c.GetMethod("OpenPanel").Invoke(console, null));
            Assert.IsTrue((bool)c.GetMethod("ConfirmActivation").Invoke(console, null));
            Assert.IsFalse((bool)d.GetProperty("LevelCompleted").GetValue(device));
            Assert.AreEqual("Starting", d.GetProperty("FactoryState").GetValue(device).ToString());
            AudioSource source = GameObject.Find("PurifierStartupSound").GetComponent<AudioSource>();
            Assert.IsTrue(source.isPlaying, "Device activation should trigger original startup sound.");
        }

        [UnityTest]
        public IEnumerator CaptureFinalFactoryViews()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            GameObject player = GameObject.Find("M2_Player");
            Camera camera = player.GetComponentInChildren<Camera>();
            Assert.IsNotNull(camera);
            player.GetComponents<MonoBehaviour>().Single(item => item.GetType().Name == "C3FirstPersonController").enabled = false;
            Directory.CreateDirectory(SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_FullArt_Evidence"));
            player.transform.position = new Vector3(-12.5f, 0.55f, 5.6f);
            player.transform.rotation = Quaternion.identity;
            yield return null;
            Capture(camera, SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_FullArt_Evidence/Final_1F_Reactor.png"));
            player.transform.position = new Vector3(-19.5f, 4.5f, 15.4f);
            player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            yield return null;
            Capture(camera, SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_FullArt_Evidence/Final_2F_Deck.png"));
            player.transform.position = new Vector3(-13.0f, 4.5f, 16.5f);
            player.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            yield return null;
            Capture(camera, SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_FullArt_Evidence/Final_2F_Chip.png"));
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
                Texture2D picture = new Texture2D(960, 540, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                picture.Apply();
                File.WriteAllBytes(path, picture.EncodeToPNG());
                UnityEngine.Object.Destroy(picture);
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
