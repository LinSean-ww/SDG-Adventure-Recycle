using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M6IntegrationTests
{
    public sealed class C3UpperSupportVisualTests
    {
        private const string Scene = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private static readonly string Output = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_UpperSupport_Evidence");

        [UnityTest]
        public IEnumerator CaptureUpperSupportViews()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            yield return null;
            GameObject player = GameObject.Find("M2_Player");
            Assert.IsNotNull(player);
            Camera camera = player.GetComponentInChildren<Camera>();
            Assert.IsNotNull(camera);
            player.GetComponents<MonoBehaviour>().Single(x => x.GetType().Name == "C3FirstPersonController").enabled = false;
            Directory.CreateDirectory(Output);

            yield return CaptureView(player, camera, new Vector3(-13.5f, 4.5f, 12.6f),
                new Vector3(-13f, 9.6f, 15.4f), "Upper_ConduitBridge.png");
            yield return CaptureView(player, camera, new Vector3(-19f, 4.5f, 13.2f),
                new Vector3(-15f, 9.6f, 15.4f), "Upper_WestBridgeAnchor.png");
            yield return CaptureView(player, camera, new Vector3(-12.5f, 0.55f, 6.6f),
                new Vector3(-12.5f, 9.6f, 9.5f), "Upper_ReactorLampBridge.png");
            yield return CaptureView(player, camera, new Vector3(-13.5f, 4.5f, 16.8f),
                new Vector3(-14f, 9.6f, 15.5f), "Upper_WalkwayLampBridge.png");
        }

        private static IEnumerator CaptureView(GameObject player, Camera camera, Vector3 position,
            Vector3 target, string name)
        {
            player.transform.position = position;
            yield return null;
            camera.transform.LookAt(target);
            RenderTexture texture = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                Texture2D image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                image.Apply();
                File.WriteAllBytes(Output + "/" + name, image.EncodeToPNG());
                Object.Destroy(image);
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = previous;
                texture.Release();
                Object.Destroy(texture);
            }
        }
    }
}
