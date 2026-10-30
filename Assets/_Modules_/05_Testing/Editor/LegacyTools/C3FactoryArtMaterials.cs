using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Original, deterministic procedural textures. No downloaded or third-party art assets.
public static class C3FactoryArtMaterials
{
    private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    private const string ArtPath = "Assets/_Modules_/02_Levels/Level03/ArtFull";
    private const string TexPath = ArtPath + "/Textures";
    private const string MatPath = ArtPath + "/Materials";
    private static readonly string EvidencePath = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_FullArt_Evidence");

    public static void StageD_IndustrialMaterials()
    {
        Directory.CreateDirectory(TexPath);
        Directory.CreateDirectory(MatPath);
        MakeTexture("T_AgedConcrete", PixelConcrete);
        MakeTexture("T_DarkSteel", PixelSteel);
        MakeTexture("T_RustedSteel", PixelRust);
        MakeTexture("T_Grating", PixelGrate);
        MakeTexture("T_OlivePaint", PixelOlive);
        MakeTexture("T_WarningStripes", PixelWarning);
        MakeTexture("T_PipeWear", PixelPipe);
        MakeTexture("T_OilDust", PixelDirt, true);
        MakeTexture("T_MaintenancePaper", PixelPaper);

        Material concreteFloor = MakeMaterial("M_AgedConcrete_Floor", "T_AgedConcrete", new Color(0.67f, 0.65f, 0.61f), 0f, 0.08f);
        Material concreteWall = MakeMaterial("M_AgedConcrete_Wall", "T_AgedConcrete", new Color(0.55f, 0.59f, 0.60f), 0f, 0.07f);
        Material concreteCeiling = MakeMaterial("M_AgedConcrete_Ceiling", "T_AgedConcrete", new Color(0.45f, 0.47f, 0.49f), 0f, 0.04f);
        Material steel = MakeMaterial("M_DarkIndustrialSteel", "T_DarkSteel", new Color(0.64f, 0.69f, 0.72f), 0.72f, 0.28f);
        Material rust = MakeMaterial("M_RustedSteel", "T_RustedSteel", new Color(0.88f, 0.79f, 0.68f), 0.50f, 0.17f);
        Material grating = MakeMaterial("M_PaintedMetalGrating", "T_Grating", new Color(0.75f, 0.79f, 0.80f), 0.73f, 0.23f);
        Material olive = MakeMaterial("M_WornOliveMachinePaint", "T_OlivePaint", new Color(0.65f, 0.74f, 0.66f), 0.43f, 0.22f);
        Material warning = MakeMaterial("M_WornWarningYellow", "T_WarningStripes", Color.white, 0.15f, 0.12f);
        Material pipe = MakeMaterial("M_OxidizedPipe", "T_PipeWear", new Color(0.67f, 0.71f, 0.69f), 0.56f, 0.26f);
        Material rubber = MakeMaterial("M_OilyRubber", "T_DarkSteel", new Color(0.21f, 0.23f, 0.23f), 0f, 0.05f);
        Material paper = MakeMaterial("M_FadedMaintenancePaper", "T_MaintenancePaper", new Color(0.93f, 0.86f, 0.70f), 0f, 0.04f);
        Material dirt = MakeTransparent("M_OilDustOverlay", "T_OilDust", new Color(0.18f, 0.15f, 0.13f, 0.56f));
        Material cyan = MakeEmissive("M_CyanInstrument", new Color(0.04f, 0.45f, 0.49f), new Color(0.02f, 0.75f, 0.86f));
        MakeEmissive("M_RedFaultIndicator", new Color(0.46f, 0.045f, 0.03f), new Color(1f, 0.05f, 0.015f));
        MakeEmissive("M_WarmIndustrialLamp", new Color(0.85f, 0.41f, 0.12f), new Color(1.25f, 0.55f, 0.14f));

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        Transform art = roots.Single(root => root.name == "C_Factory_Art").transform;
        int assigned = 0;
        foreach (MeshRenderer renderer in art.GetComponentsInChildren<MeshRenderer>(true))
        {
            string name = renderer.name;
            Material material = name.StartsWith("Steel_") ? steel :
                name.StartsWith("Rust_") ? rust :
                name.StartsWith("Olive_") ? olive :
                name.StartsWith("Warning_") ? warning :
                name.StartsWith("Pipe_") ? pipe :
                name.StartsWith("Rubber_") ? rubber :
                name.StartsWith("Paper_") ? paper :
                name.StartsWith("Dirt_") ? dirt :
                name.StartsWith("GlowCyan_") ? cyan : null;
            if (material == null) throw new Exception("No industrial material role for art renderer: " + name);
            renderer.sharedMaterial = material;
            assigned++;
        }

        Transform blockout = roots.Single(root => root.name == "C_TwoFloor_Blockout").transform;
        foreach (MeshRenderer renderer in blockout.Find("Floor2_Platforms").GetComponentsInChildren<MeshRenderer>(true)) renderer.sharedMaterial = grating;
        foreach (MeshRenderer renderer in blockout.Find("Stairs").GetComponentsInChildren<MeshRenderer>(true)) renderer.sharedMaterial = grating;

        Renderer factory = roots.Single(root => root.name == "ONLYfactory").GetComponentsInChildren<Renderer>(true)
            .Single(renderer => renderer.name == "factory");
        Material[] factoryMaterials = factory.sharedMaterials;
        int factoryOverrides = 0;
        for (int i = 0; i < factoryMaterials.Length; i++)
        {
            string name = factoryMaterials[i] != null ? factoryMaterials[i].name : "";
            Material replacement = name == "siteC_floor" ? concreteFloor :
                name == "siteC_ceiling" ? concreteCeiling :
                name.StartsWith("siteC_wall_") ? concreteWall : null;
            if (replacement == null) continue;
            factoryMaterials[i] = replacement;
            factoryOverrides++;
        }
        if (factoryOverrides != 6) throw new Exception("Expected 6 C-area FBX material overrides, got " + factoryOverrides);
        factory.sharedMaterials = factoryMaterials;

        Transform console = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Single(transform => transform.name == "M4_PuzzleConsole");
        console.Find("Housing").GetComponent<MeshRenderer>().sharedMaterial = olive;
        console.Find("ControlStrip").GetComponent<MeshRenderer>().sharedMaterial = steel;

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save material assignments.");
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(EvidencePath);
        File.WriteAllText(EvidencePath + "/StageD_Materials.txt",
            "generatedTextures=9\ncreatedMaterials=15\nartRenderersAssigned=" + assigned + "\nfactoryCSubmeshOverrides=" + factoryOverrides +
            "\nM2Floor2=grating\nM2Stairs=grating\noriginalFBXAsset=unchanged\n", new UTF8Encoding(false));
        ValidateStageD();
        Debug.Log("C3_FULLART_STAGE_D_PASS");
    }

    public static void ValidateStageD()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        Transform art = roots.Single(root => root.name == "C_Factory_Art").transform;
        MeshRenderer[] renderers = art.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length < 80 || renderers.Any(renderer => renderer.sharedMaterial == null ||
            renderer.sharedMaterial.name == "M_BlockoutPlaceholder"))
            throw new Exception("Industrial art contains missing or placeholder materials.");
        string[] expected = { "M_AgedConcrete_Floor", "M_DarkIndustrialSteel", "M_RustedSteel", "M_PaintedMetalGrating",
            "M_WornOliveMachinePaint", "M_WornWarningYellow", "M_OxidizedPipe", "M_OilDustOverlay" };
        foreach (string name in expected)
            if (AssetDatabase.LoadAssetAtPath<Material>(MatPath + "/" + name + ".mat") == null)
                throw new Exception("Missing required material asset: " + name);
        Renderer factory = roots.Single(root => root.name == "ONLYfactory").GetComponentsInChildren<Renderer>(true)
            .Single(renderer => renderer.name == "factory");
        if (!factory.sharedMaterials.Any(material => material != null && material.name == "M_AgedConcrete_Floor"))
            throw new Exception("Original C floor visual was not assigned aged concrete.");
        Debug.Log("C3_FULLART_STAGE_D_VERIFY_PASS renderers=" + renderers.Length);
    }

    private static void MakeTexture(string name, Func<int, int, Color32> pixel, bool alpha = false)
    {
        string path = TexPath + "/" + name + ".png";
        if (File.Exists(path)) throw new Exception("Refusing to overwrite texture asset: " + path);
        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        Color32[] colors = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) colors[y * size + x] = pixel(x, y);
        texture.SetPixels32(colors);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = alpha;
        importer.SaveAndReimport();
    }

    private static Material MakeMaterial(string name, string textureName, Color tint, float metallic, float gloss)
    {
        Material material = NewMaterial(name, "Standard");
        material.SetColor("_Color", tint);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", gloss);
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath + "/" + textureName + ".png"));
        return material;
    }

    private static Material MakeTransparent(string name, string textureName, Color tint)
    {
        Material material = NewMaterial(name, "Unlit/Transparent");
        material.SetColor("_Color", tint);
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath + "/" + textureName + ".png"));
        return material;
    }

    private static Material MakeEmissive(string name, Color baseColor, Color emission)
    {
        Material material = NewMaterial(name, "Standard");
        material.SetColor("_Color", baseColor);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return material;
    }

    private static Material NewMaterial(string name, string shaderName)
    {
        string path = MatPath + "/" + name + ".mat";
        if (File.Exists(path)) throw new Exception("Refusing to overwrite material asset: " + path);
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new Exception("Required Built-in shader missing: " + shaderName);
        Material material = new Material(shader) { name = name };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static float Noise(int x, int y, int grid)
    {
        int cell = 256 / grid;
        float gx = (float)x / cell;
        float gy = (float)y / cell;
        int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
        float tx = gx - ix, ty = gy - iy;
        tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
        float a = Hash(ix, iy, grid), b = Hash(ix + 1, iy, grid);
        float c = Hash(ix, iy + 1, grid), d = Hash(ix + 1, iy + 1, grid);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private static float Hash(int x, int y, int grid)
    {
        unchecked
        {
            uint value = (uint)((x % grid + grid) % grid) * 374761393u +
                (uint)((y % grid + grid) % grid) * 668265263u + 0x9e3779b9u;
            value = (value ^ (value >> 13)) * 1274126177u;
            return (value ^ (value >> 16)) / (float)uint.MaxValue;
        }
    }

    private static float Grain(int x, int y) => Noise(x, y, 4) * 0.47f + Noise(x, y, 16) * 0.36f + Noise(x, y, 64) * 0.17f;
    private static Color32 PixelColor(float r, float g, float b, float a = 1f) => new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), Mathf.Clamp01(a));

    private static Color32 PixelConcrete(int x, int y)
    {
        float n = Grain(x, y);
        float crack = Noise(x, y, 16) < 0.30f && Noise(x + 15, y - 9, 64) < 0.28f ? 0.11f : 0f;
        float v = 0.61f + (n - 0.5f) * 0.24f - crack;
        return PixelColor(v, v * 0.98f, v * 0.94f);
    }

    private static Color32 PixelSteel(int x, int y)
    {
        float n = Grain(x, y), scratch = Noise(x * 2, y / 4, 64);
        float v = 0.48f + (n - 0.5f) * 0.20f + (scratch > 0.79f ? 0.08f : 0f);
        return PixelColor(v * 0.79f, v * 0.88f, v);
    }

    private static Color32 PixelRust(int x, int y)
    {
        float n = Grain(x, y), m = Noise(x + 29, y - 41, 16);
        float spread = Mathf.SmoothStep(0f, 1f, (n - 0.35f) * 3f);
        return PixelColor(0.26f + spread * 0.50f, 0.24f + spread * 0.24f + m * 0.04f,
            0.22f + spread * 0.09f);
    }

    private static Color32 PixelGrate(int x, int y)
    {
        bool bar = x % 32 < 5 || y % 32 < 5 || (x + y) % 64 < 3;
        float n = Grain(x, y);
        float v = bar ? 0.42f + (n - 0.5f) * 0.12f : 0.10f + n * 0.06f;
        return PixelColor(v * 0.75f, v * 0.85f, v);
    }

    private static Color32 PixelOlive(int x, int y)
    {
        float n = Grain(x, y), chip = Noise(x + 17, y + 49, 64);
        if (chip > 0.83f && n < 0.55f) return PixelColor(0.45f, 0.24f, 0.14f);
        return PixelColor(0.23f + n * 0.13f, 0.31f + n * 0.14f, 0.25f + n * 0.09f);
    }

    private static Color32 PixelWarning(int x, int y)
    {
        bool black = ((x + y) / 32) % 2 == 0;
        float wear = Noise(x, y, 64) > 0.82f ? 0.22f : 0f;
        float n = Grain(x, y);
        return black ? PixelColor(0.10f + n * 0.05f, 0.10f + n * 0.05f, 0.08f) :
            PixelColor(0.80f - wear, 0.54f - wear * 0.6f, 0.09f);
    }

    private static Color32 PixelPipe(int x, int y)
    {
        float n = Grain(x, y), rust = Noise(x + 13, y - 17, 16);
        return rust > 0.70f ? PixelColor(0.47f, 0.29f, 0.20f) :
            PixelColor(0.38f + n * 0.18f, 0.45f + n * 0.16f, 0.46f + n * 0.15f);
    }

    private static Color32 PixelDirt(int x, int y)
    {
        float dx = (x - 128f) / 128f, dy = (y - 128f) / 128f;
        float edge = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
        float a = Mathf.Clamp01(edge * 1.6f - 0.28f + (Grain(x, y) - 0.5f) * 0.45f);
        return PixelColor(0.25f, 0.20f, 0.16f, a);
    }

    private static Color32 PixelPaper(int x, int y)
    {
        float n = Grain(x, y);
        float line = y % 23 == 0 && x > 20 && x < 230 ? 0.22f : 0f;
        return PixelColor(0.82f + n * 0.12f - line, 0.75f + n * 0.11f - line,
            0.59f + n * 0.10f - line);
    }
}


