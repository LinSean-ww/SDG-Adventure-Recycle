using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class C3FactoryArtLighting
{
    private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    private const string MatPath = "Assets/_Modules_/02_Levels/Level03/ArtFull/Materials";
    private static readonly string EvidencePath = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_FullArt_Evidence");

    public static void StageE_Atmosphere()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform root = scene.GetRootGameObjects().Single(go => go.name == "C_Factory_Art").transform;
        Transform lighting = root.Find("Lighting");
        if (lighting.childCount != 0) throw new Exception("Lighting already constructed; refusing duplicate stage.");
        Light sun = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Light>(true))
            .Single(light => light.name == "Directional Light");
        sun.intensity = 0.32f;
        sun.shadows = LightShadows.None;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.29f, 0.32f, 0.34f);
        RenderSettings.reflectionIntensity = 0.45f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.fogColor = new Color(0.19f, 0.23f, 0.25f);

        Material warm = Material("M_WarmIndustrialLamp");
        Material cyan = Material("M_CyanInstrument");
        Material red = Material("M_RedFaultIndicator");
        Fixture(lighting, "Warm_EntrySafety", new Vector3(-20.9f, 6.8f, 4.1f),
            new Color(1f, 0.57f, 0.27f), 1.3f, 10.0f, warm);
        Fixture(lighting, "Warm_CentralReactor", new Vector3(-12.5f, 7.1f, 9.5f),
            new Color(1f, 0.57f, 0.29f), 1.55f, 10.5f, warm);
        Fixture(lighting, "Warm_WestService", new Vector3(-22.0f, 7.15f, 19.2f),
            new Color(1f, 0.53f, 0.25f), 1.35f, 11.0f, warm);
        Fixture(lighting, "Warm_UpperWalkway", new Vector3(-14f, 8.05f, 15.5f),
            new Color(1f, 0.63f, 0.34f), 1.35f, 9.5f, warm);
        Fixture(lighting, "Warm_EastService", new Vector3(-4.1f, 7.1f, 13.4f),
            new Color(1f, 0.60f, 0.32f), 1.25f, 10.2f, warm);
        Fixture(lighting, "Cyan_Console", new Vector3(-1.8f, 2.65f, 18.9f),
            new Color(0.17f, 0.76f, 0.86f), 0.85f, 4.3f, cyan);
        Fixture(lighting, "Cyan_ChipStation", new Vector3(-12.6f, 6.35f, 19.2f),
            new Color(0.21f, 0.70f, 0.79f), 0.75f, 4.4f, cyan);
        Fixture(lighting, "Red_EntryFault", new Vector3(-21.0f, 3.4f, 2.85f),
            new Color(1f, 0.09f, 0.045f), 0.55f, 3.0f, red);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Failed to save lighting stage.");
        ValidateStageE();
        Debug.Log("C3_FULLART_STAGE_E_PASS");
    }

    public static void ValidateStageE()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform lighting = scene.GetRootGameObjects().Single(go => go.name == "C_Factory_Art").transform.Find("Lighting");
        Light[] lights = lighting.GetComponentsInChildren<Light>(true);
        if (lights.Length < 8 || lights.Length > 9 || lights.Any(light => light.shadows != LightShadows.None || light.intensity <= 0f))
            throw new Exception("Industrial lighting count, shadow cost, or intensity invalid.");
        if (RenderSettings.ambientLight.grayscale < 0.25f || RenderSettings.ambientLight.grayscale > 0.38f)
            throw new Exception("Ambient lighting outside readability target.");
        Directory.CreateDirectory(EvidencePath);
        Light directional = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Light>(true))
            .Single(light => light.name == "Directional Light");
        File.WriteAllText(EvidencePath + "/StageE_Lighting.txt",
            "localLights=" + lights.Length + "\nrealtimeShadows=0\nambient=" + RenderSettings.ambientLight +
            "\ndirectionalIntensity=" + directional.intensity + "\nfogDensity=" + RenderSettings.fogDensity + "\n", new UTF8Encoding(false));
        Debug.Log("C3_FULLART_STAGE_E_VERIFY_PASS");
    }

    private static Material Material(string name)
    {
        Material asset = AssetDatabase.LoadAssetAtPath<Material>(MatPath + "/" + name + ".mat");
        if (asset == null) throw new Exception("Missing material " + name);
        return asset;
    }

    private static void Fixture(Transform parent, string name, Vector3 position, Color color,
        float intensity, float range, Material indicator)
    {
        GameObject fixture = new GameObject(name);
        fixture.transform.SetParent(parent, false);
        fixture.transform.position = position;
        Light light = fixture.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.Auto;
        GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bulb.name = "IndicatorLens";
        bulb.transform.SetParent(fixture.transform, false);
        bulb.transform.localScale = new Vector3(0.30f, 0.12f, 0.30f);
        UnityEngine.Object.DestroyImmediate(bulb.GetComponent<Collider>());
        bulb.GetComponent<MeshRenderer>().sharedMaterial = indicator;
    }
}
