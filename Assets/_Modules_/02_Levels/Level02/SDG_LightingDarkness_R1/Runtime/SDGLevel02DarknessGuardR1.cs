using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Standalone Level02 lighting correction. No existing scene, save, menu or Cinematics source edits.
namespace SDG.Level02.DarknessSafeFixR1
{
    [Serializable]
    public sealed class DarknessOptions
    {
        public bool enabled = true;
        public string targetScenePath = "Assets/_Modules_/02_Levels/Level02/Imported/Scenes/SampleScene.unity";
        public float ambientBrightness = 0.012f;
        public bool removeBakedLightmaps = true;
        public bool disableDirectionalLights = true;
        public bool disablePointLights = true;
        public bool preserveSpotLights = true;
        public bool blackSkybox = true;
        public bool disableReflections = true;
        public bool blackCameraBackground = true;
    }

    public sealed class SDGLevel02DarknessGuardR1 : MonoBehaviour
    {
        private const string OptionsResource = "SDG_Level02_Darkness_R1";
        private static SDGLevel02DarknessGuardR1 instance;
        private DarknessOptions options;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartGuard()
        {
            if (instance != null) return;
            TextAsset file = Resources.Load<TextAsset>(OptionsResource);
            if (file == null)
            {
                Debug.LogWarning("[SDG L02 DARK R1] Settings missing; no lighting changes performed.");
                return;
            }
            DarknessOptions loaded;
            try { loaded = JsonUtility.FromJson<DarknessOptions>(file.text); }
            catch (Exception ex)
            {
                Debug.LogError("[SDG L02 DARK R1] Invalid settings: " + ex.Message);
                return;
            }
            if (loaded == null || !loaded.enabled || string.IsNullOrEmpty(loaded.targetScenePath)) return;
            GameObject host = new GameObject("SDG Level02 Darkness Guard R1");
            DontDestroyOnLoad(host);
            SDGLevel02DarknessGuardR1 guard = host.AddComponent<SDGLevel02DarknessGuardR1>();
            guard.options = loaded;
            instance = guard;
            SceneManager.sceneLoaded += guard.OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (options == null || !options.enabled) return;
            if (!string.Equals(scene.path.Replace('\\', '/'), options.targetScenePath.Replace('\\', '/'),
                StringComparison.OrdinalIgnoreCase)) return;
            StartCoroutine(ApplyAfterLoad(scene));
        }

        private IEnumerator ApplyAfterLoad(Scene levelScene)
        {
            // Even when the level-intro video sets Time.timeScale=0, yield return null advances.
            yield return null;
            if (!levelScene.IsValid() || !levelScene.isLoaded) yield break;
            if (!string.Equals(SceneManager.GetActiveScene().path, levelScene.path, StringComparison.OrdinalIgnoreCase))
            {
                // Avoid changing lighting for unrelated additive/active scenes.
                yield break;
            }
            ApplyDarkness(levelScene);
        }

        private void ApplyDarkness(Scene levelScene)
        {
            try
            {
                // Unity loads scene RenderSettings on scene load. Change only while Level02 is active.
                float dim = Mathf.Clamp(options.ambientBrightness, 0f, 0.25f);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(dim, dim, dim * 1.2f, 1f);
                RenderSettings.ambientIntensity = dim;
                RenderSettings.ambientProbe = new SphericalHarmonicsL2();
                if (options.blackSkybox) RenderSettings.skybox = null;
                if (options.disableReflections) RenderSettings.reflectionIntensity = 0f;
                int bakedCount = LightmapSettings.lightmaps == null ? 0 : LightmapSettings.lightmaps.Length;
                if (options.removeBakedLightmaps) LightmapSettings.lightmaps = new LightmapData[0];

                int disabled = 0;
                int preserved = 0;
                Light[] lights = Resources.FindObjectsOfTypeAll<Light>();
                foreach (Light light in lights)
                {
                    if (light == null || !light.gameObject.scene.IsValid() ||
                        !string.Equals(light.gameObject.scene.path, levelScene.path, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!light.enabled) continue;
                    if (IsFlashlight(light) || (options.preserveSpotLights && light.type == LightType.Spot))
                    {
                        preserved++;
                        continue;
                    }
                    bool switchOff =
                        (light.type == LightType.Directional && options.disableDirectionalLights) ||
                        (light.type == LightType.Point && options.disablePointLights);
                    if (switchOff)
                    {
                        // Only changes the in-memory scene object; the .unity asset is untouched.
                        light.enabled = false;
                        disabled++;
                    }
                }

                int camerasChanged = 0;
                if (options.blackCameraBackground)
                {
                    Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
                    foreach (Camera camera in cameras)
                    {
                        if (camera == null || !camera.gameObject.scene.IsValid() ||
                            !string.Equals(camera.gameObject.scene.path, levelScene.path, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (camera.clearFlags != CameraClearFlags.Skybox) continue;
                        camera.clearFlags = CameraClearFlags.SolidColor;
                        camera.backgroundColor = Color.black;
                        camerasChanged++;
                    }
                }
                Debug.Log("[SDG L02 DARK R1] APPLIED to Level02 only. ambient=" + dim.ToString("F3") +
                          ", baked lightmaps=" + bakedCount + (options.removeBakedLightmaps ? " cleared" : " retained") +
                          ", non-flashlight lights off=" + disabled +
                          ", spot/flashlights kept=" + preserved +
                          ", skybox cameras changed=" + camerasChanged +
                          ". Other levels and scene files were not edited.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SDG L02 DARK R1] Correction failed: " + ex);
            }
        }

        private static bool IsFlashlight(Light light)
        {
            Transform node = light.transform;
            for (int depth = 0; node != null && depth < 12; depth++, node = node.parent)
            {
                string n = node.name.ToLowerInvariant();
                if (n.Contains("flashlight") || n.Contains("flash_light") ||
                    n.Contains("headlamp") || n.Contains("handtorch") ||
                    n.Contains("torchlight") || n.Contains("手電") || n.Contains("頭燈"))
                    return true;
                // All light components parented to a live camera are preserved.
                if (node.GetComponent<Camera>() != null) return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this) instance = null;
        }
    }
}
