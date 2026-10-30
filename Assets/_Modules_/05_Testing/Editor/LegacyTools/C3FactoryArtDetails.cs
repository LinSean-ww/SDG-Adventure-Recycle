using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class C3FactoryArtDetails
{
    private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    private const string MatPath = "Assets/_Modules_/02_Levels/Level03/ArtFull/Materials";
    private static readonly string EvidencePath = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_FullArt_Evidence");

    public static void StageF_EnvironmentalNarrative()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = scene.GetRootGameObjects().Single(go => go.name == "C_Factory_Art").transform;
        Transform decoration = art.Find("Decorations");
        Transform props = art.Find("Props");
        if (decoration.Find("Log_UpperBayChipTransfer") != null)
            throw new Exception("Environmental notes already built; refusing duplicate stage.");
        Material rust = AssetDatabase.LoadAssetAtPath<Material>(MatPath + "/M_RustedSteel.mat");
        Material steel = AssetDatabase.LoadAssetAtPath<Material>(MatPath + "/M_DarkIndustrialSteel.mat");
        if (rust == null || steel == null) throw new Exception("Factory detail materials missing.");

        Label(decoration, "Log_UpperBayChipTransfer", "維修紀錄 07\n控制晶片：二樓維修區\n淨化設備已離線",
            new Vector3(-12.5f, 5.92f, 19.61f), 0.013f, new Color(0.82f, 0.79f, 0.66f));
        Label(decoration, "Plate_Purifier03", "03 號淨化設備\n設備停止運作",
            new Vector3(-12.5f, 2.85f, 8.29f), 0.013f, new Color(0.85f, 0.77f, 0.55f));
        Label(decoration, "Plate_ElectricalIsolation", "高壓危險\n維修前請切斷電源",
            new Vector3(-23.0f, 2.78f, 17.72f), 0.011f, new Color(0.95f, 0.73f, 0.39f));

        Toolbox(props, "ToolCase_WestService", new Vector3(-22.0f, 0.56f, 22.4f), rust, steel);
        Toolbox(props, "ToolCase_UpperBay", new Vector3(-13.5f, 4.51f, 18.6f), rust, steel);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Failed to save narrative details.");
        ValidateNarrative();
        Debug.Log("C3_FULLART_NARRATIVE_PASS");
    }

    public static void ValidateNarrative()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = scene.GetRootGameObjects().Single(go => go.name == "C_Factory_Art").transform;
        TextMesh[] texts = art.Find("Decorations").GetComponentsInChildren<TextMesh>(true);
        if (texts.Length != 3 || texts.Any(text => string.IsNullOrWhiteSpace(text.text)))
            throw new Exception("Maintenance and safety narrative signs missing.");
        if (art.Find("Props/ToolCase_WestService") == null || art.Find("Props/ToolCase_UpperBay") == null)
            throw new Exception("Service tool cases missing.");
        Directory.CreateDirectory(EvidencePath);
        File.WriteAllText(EvidencePath + "/StageF_Narrative.txt",
            "readableLabels=3\nserviceToolCases=2\naddedColliders=0\nsource=original in-project text and Unity primitives\n",
            new UTF8Encoding(false));
    }

    public static void TuneNarrativeLabels()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = scene.GetRootGameObjects().Single(go => go.name == "C_Factory_Art").transform;
        art.Find("Decorations/Log_UpperBayChipTransfer").GetComponent<TextMesh>().characterSize = 0.015f;
        art.Find("Decorations/Plate_Purifier03").GetComponent<TextMesh>().characterSize = 0.015f;
        art.Find("Decorations/Plate_ElectricalIsolation").GetComponent<TextMesh>().characterSize = 0.015f;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Failed to save tuned narrative labels.");
        ValidateNarrative();
        Debug.Log("C3_FULLART_LABEL_SCALE_PASS");
    }

    private static void Label(Transform parent, string name, string words, Vector3 position, float size, Color color)
    {
        GameObject item = new GameObject(name);
        item.transform.SetParent(parent, false);
        item.transform.position = position;
        TextMesh text = item.AddComponent<TextMesh>();
        text.text = words;
        text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Modules_/06_ExternalAssets/Resources/Fonts/NotoSansCJKtc-Regular.otf");
        if (text.font == null) throw new Exception("M6 Traditional Chinese font missing.");
        text.fontSize = 72;
        text.characterSize = size;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.lineSpacing = 1.25f;
        text.color = color;
        item.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
    }

    private static void Toolbox(Transform parent, string name, Vector3 position, Material body, Material hardware)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        Cube(root.transform, "CaseBody", new Vector3(0f, 0.12f, 0f), new Vector3(0.50f, 0.22f, 0.30f), body);
        Cube(root.transform, "Lid", new Vector3(0f, 0.25f, 0f), new Vector3(0.54f, 0.06f, 0.33f), hardware);
        Cube(root.transform, "Handle", new Vector3(0f, 0.31f, 0f), new Vector3(0.16f, 0.055f, 0.045f), hardware);
    }

    private static void Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = size;
        UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}

