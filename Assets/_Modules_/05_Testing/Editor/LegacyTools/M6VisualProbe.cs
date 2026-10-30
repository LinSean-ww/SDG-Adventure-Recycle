using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class M6VisualProbe
{
    private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    private const string ModelPath = "Assets/_Modules_/02_Levels/Level03/Models/ONLYfactory.fbx";

    public static void Inspect()
    {
        var lines = new StringBuilder();
        void Add(string value) => lines.AppendLine(value);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Add("scene=" + scene.path);
        Add("pipeline=" + (GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.name));
        Add("quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "; shadows=" + QualitySettings.shadows);
        Add("ambientMode=" + RenderSettings.ambientMode + "; intensity=" + RenderSettings.ambientIntensity +
            "; skybox=" + (RenderSettings.skybox == null ? "NULL" : RenderSettings.skybox.name));
        Add("ambientSky=" + RenderSettings.ambientSkyColor + "; ambientEquator=" + RenderSettings.ambientEquatorColor +
            "; ambientGround=" + RenderSettings.ambientGroundColor);
        Add("ambientProbeL00=" + RenderSettings.ambientProbe[0, 0] + "," + RenderSettings.ambientProbe[1, 0] +
            "," + RenderSettings.ambientProbe[2, 0]);
        Add("lightmaps=" + LightmapSettings.lightmaps.Length + "; lightingData=" +
            (Lightmapping.lightingDataAsset == null ? "NULL" : Lightmapping.lightingDataAsset.name));
        Add("sun=" + (RenderSettings.sun == null ? "NULL" : RenderSettings.sun.name));
        foreach (Light light in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true)))
            Add("LIGHT " + light.name + " type=" + light.type + " enabled=" + light.enabled + " intensity=" +
                light.intensity + " color=" + light.color + " shadows=" + light.shadows + " bake=" + light.lightmapBakeType);

        var materials = new Dictionary<string, int>(StringComparer.Ordinal);
        int renderers = 0, nullSlots = 0, missingShaders = 0, blackColors = 0;
        foreach (Renderer renderer in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true)))
        {
            renderers++;
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null) { nullSlots++; Add("NULL_MATERIAL " + renderer.name); continue; }
                string shader = material.shader == null ? "NULL" : material.shader.name;
                if (shader == "NULL" || !material.shader.isSupported) missingShaders++;
                Color color = material.HasProperty("_Color") ? material.color : Color.white;
                if (color.maxColorComponent < 0.02f) blackColors++;
                string key = material.name + " | " + shader + " | " + color;
                if (!materials.ContainsKey(key)) materials[key] = 0;
                materials[key]++;
                if (shader == "NULL" || !material.shader.isSupported || color.maxColorComponent < 0.02f)
                    Add("SUSPECT_MATERIAL " + renderer.name + " " + key);
            }
        }
        Add("rendererCount=" + renderers + "; nullSlots=" + nullSlots + "; unsupportedShaders=" +
            missingShaders + "; blackColors=" + blackColors);
        foreach (var pair in materials.OrderByDescending(item => item.Value)) Add("SCENE_MATERIAL " + pair.Value + " " + pair.Key);
        UnityEngine.Object[] subassets = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
        Add("MODEL_SUBASSETS=" + subassets.Length);
        foreach (Material material in subassets.OfType<Material>())
            Add("MODEL_MATERIAL " + material.name + " shader=" + (material.shader == null ? "NULL" : material.shader.name) +
                " color=" + (material.HasProperty("_Color") ? material.color.ToString() : "NO_COLOR") +
                " path=" + AssetDatabase.GetAssetPath(material));
        string output = SDGAdventure.Testing.ReportOutputPaths.Resolve("M6/VisualProbe.txt");
        File.WriteAllText(output, lines.ToString(), Encoding.UTF8);
        Debug.Log("M6_VISUAL_PROBE_COMPLETE " + output);
    }
}
