using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    public sealed class M6VisualRuntimeTests
    {
        private const string C3 = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private const string Selection = "Assets/_Modules_/05_Testing/Scenes/M6_DevelopmentSelection.unity";
        private static readonly StringBuilder report = new StringBuilder();
        // Compare rendered pixels against a capture from the unchanged original project.

        private static IEnumerator Settle()
        {
            for (int i = 0; i < 12; i++) yield return null;
        }

        private static double Capture(string label)
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "C3 camera");
            var probe = RenderSettings.ambientProbe;
            report.AppendLine(label + " ambientL00=" + probe[0, 0] + "," + probe[1, 0] + "," + probe[2, 0] +
                " skybox=" + (RenderSettings.skybox == null ? "NULL" : RenderSettings.skybox.name) +
                " ambientIntensity=" + RenderSettings.ambientIntensity +
                " lightmaps=" + LightmapSettings.lightmaps.Length +
                " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                " cameraClear=" + camera.clearFlags + " cameraPos=" + camera.transform.position +
                " cameraRot=" + camera.transform.rotation.eulerAngles);

            RenderTexture texture = new RenderTexture(800, 450, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                Texture2D image = new Texture2D(800, 450, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 800, 450), 0, 0);
                image.Apply();
                Color32[] pixels = image.GetPixels32();
                long sum = 0;
                int dark = 0;
                foreach (Color32 pixel in pixels)
                {
                    int brightness = (pixel.r + pixel.g + pixel.b) / 3;
                    sum += brightness;
                    if (brightness < 20) dark++;
                }
                double darkFraction = dark / (double)pixels.Length;
                CompareToOriginalBaseline(pixels, label, darkFraction);
                report.AppendLine(label + " meanBrightness=" + (sum / (double)pixels.Length).ToString("F2") +
                    " darkFraction=" + darkFraction.ToString("F4"));
                Directory.CreateDirectory(SDGAdventure.Testing.ReportOutputPaths.Resolve("M6/VisualRuntime"));
                File.WriteAllBytes(SDGAdventure.Testing.ReportOutputPaths.Resolve("M6/VisualRuntime/") + label + ".png", image.EncodeToPNG());
                UnityEngine.Object.Destroy(image);
                return darkFraction;
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = previous;
                texture.Release();
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static void CompareToOriginalBaseline(Color32[] actual, string label, double darkFraction)
        {
            string path = Path.Combine(Application.dataPath, "_Modules_", "05_Testing",
                "ReferenceImages", "M6_OriginalDirectC3.png");
            Assert.IsTrue(File.Exists(path), "Original-project image baseline is missing: " + path);
            Texture2D reference = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                Assert.IsTrue(reference.LoadImage(File.ReadAllBytes(path)), "Original-project image baseline cannot be decoded.");
                Assert.AreEqual(800, reference.width);
                Assert.AreEqual(450, reference.height);
                Color32[] expected = reference.GetPixels32();
                Assert.AreEqual(expected.Length, actual.Length);
                int changedPixels = 0;
                long absoluteChannelDifference = 0;
                int baselineDarkPixels = 0;
                for (int i = 0; i < actual.Length; i++)
                {
                    Color32 a = actual[i];
                    Color32 b = expected[i];
                    if (a.r != b.r || a.g != b.g || a.b != b.b) changedPixels++;
                    absoluteChannelDifference += Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
                    if ((b.r + b.g + b.b) / 3 < 20) baselineDarkPixels++;
                }
                double meanAbsoluteChannelDifference = absoluteChannelDifference / (3.0 * actual.Length);
                double baselineDarkFraction = baselineDarkPixels / (double)actual.Length;
                report.AppendLine(label + " changedPixels=" + changedPixels +
                    " meanAbsoluteChannelDifference=" + meanAbsoluteChannelDifference.ToString("F6") +
                    " baselineDarkFraction=" + baselineDarkFraction.ToString("F6"));
                // Existing original-versus-isolated render differs at only 20 of 360,000 pixels.
                // A larger difference is a migration regression even if both images are similarly dark.
                Assert.LessOrEqual(changedPixels, 36, label + " differs from the original-project C3 image.");
                Assert.LessOrEqual(meanAbsoluteChannelDifference, 0.01,
                    label + " pixel colors differ from the original-project C3 image.");
                Assert.LessOrEqual(Math.Abs(darkFraction - baselineDarkFraction), 0.001,
                    label + " dark-pixel share differs from the original-project C3 image.");
            }
            finally
            {
                UnityEngine.Object.Destroy(reference);
            }
        }
        [UnityTest]
        public IEnumerator DirectVersusSelectionSceneLighting()
        {
            report.Clear();
            yield return SceneManager.LoadSceneAsync(C3, LoadSceneMode.Single);
            yield return Settle();
            double directDark = Capture("DirectC3");
            Assert.Greater(RenderSettings.ambientProbe[0, 0], 0.05f, "Skybox ambient probe was not generated.");

            if (Application.CanStreamedLevelBeLoaded(Selection))
            {
                yield return SceneManager.LoadSceneAsync(Selection, LoadSceneMode.Single);
                Type coordinatorType = Type.GetType("SDGAdventure.ModuleSystem.ModuleCoordinator, Assembly-CSharp", true);
                object coordinator = coordinatorType.GetProperty("Instance").GetValue(null);
                Assert.IsNotNull(coordinator);
                object[] args = { "c3_treasure_puzzle", null };
                Assert.IsTrue((bool)coordinatorType.GetMethod("TryLaunch").Invoke(coordinator, args), args[1]?.ToString());
                float deadline = Time.realtimeSinceStartup + 15f;
                while (SceneManager.GetActiveScene().path != C3 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual(C3, SceneManager.GetActiveScene().path);
                yield return Settle();
                double selectionDark = Capture("FromSelection");
                Assert.Less(Math.Abs(selectionDark - directDark), 0.01, "Selection loading changed C3 lighting relative to direct loading.");
                Assert.Greater(RenderSettings.ambientProbe[0, 0], 0.05f, "Skybox ambient probe was not generated after transition.");
            }
            DynamicGI.UpdateEnvironment();
            yield return Settle();
            Capture("AfterGIUpdate");
            File.WriteAllText(SDGAdventure.Testing.ReportOutputPaths.Resolve("M6/VisualRuntime/RuntimeProbe.txt"), report.ToString());
        }
    }
}

