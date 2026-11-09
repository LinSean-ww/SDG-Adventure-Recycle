using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class StarPlatformBuilder : MonoBehaviour
{
    // 在元件右上角選單執行，不需要按播放。
    [ContextMenu("建立星星檢查平台")]
    void BuildPlatform()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogWarning("請先停止播放，再建立平台。");
            return;
        }

        if (transform.Find("PlatformModel") != null)
        {
            Debug.LogWarning("這個物件已經建立過平台。");
            return;
        }

        string folder = "Assets/StarPlatformAssets";

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "StarPlatformAssets");

        Shader shader = UnityEngine.Rendering.GraphicsSettings
            .currentRenderPipeline == null
            ? Shader.Find("Standard")
            : Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            Debug.LogError("目前支援 Built-in 或 URP 專案。");
            return;
        }

        Material gray = MakeMaterial(
            folder, "PlatformGray", shader,
            new Color(0.18f, 0.20f, 0.22f));

        Material green = MakeMaterial(
            folder, "PlatformGreen", shader,
            new Color(0.20f, 0.38f, 0.25f));

        Material yellow = MakeMaterial(
            folder, "PlatformYellow", shader,
            new Color(1f, 0.75f, 0.08f));

        Material black = MakeMaterial(
            folder, "PlatformBlack", shader,
            new Color(0.06f, 0.07f, 0.08f));

        Material light = MakeMaterial(
            folder, "PlatformLight", shader,
            new Color(0.1f, 1f, 0.7f));

        light.EnableKeyword("_EMISSION");
        light.SetColor("_EmissionColor",
            new Color(0.1f, 1f, 0.7f) * 1.5f);
        EditorUtility.SetDirty(light);

        GameObject model = new GameObject("PlatformModel");
        model.transform.SetParent(transform, false);
        Undo.RegisterCreatedObjectUndo(model, "建立檢查平台");

        Transform parent = model.transform;

        // 平台底部原點為 Y = 0，頂面為 Y = 0.6。
        // 只有底座保留實體碰撞器。
        Part(parent, "Base",
            new Vector3(0, 0.33f, 0),
            new Vector3(2f, 0.54f, 2f),
            gray, true);

        Part(parent, "GreenSurface",
            new Vector3(0, 0.59f, 0),
            new Vector3(1.8f, 0.02f, 1.8f),
            green);

        // 台面黃色邊框。
        for (int i = -1; i <= 1; i += 2)
        {
            Part(parent, "YellowBorder",
                new Vector3(0, 0.603f, i * 0.86f),
                new Vector3(1.75f, 0.006f, 0.025f),
                yellow);

            Part(parent, "YellowBorder",
                new Vector3(i * 0.86f, 0.603f, 0),
                new Vector3(0.025f, 0.006f, 1.75f),
                yellow);

            // 底座側面的深色面板。
            Part(parent, "SidePanel",
                new Vector3(0, 0.32f, i * 1.001f),
                new Vector3(1.6f, 0.32f, 0.012f),
                black);

            Part(parent, "SidePanel",
                new Vector3(i * 1.001f, 0.32f, 0),
                new Vector3(0.012f, 0.32f, 1.6f),
                black);
        }

        // 四隻腳與四角指示燈。
        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                Part(parent, "Foot",
                    new Vector3(x * 0.82f, 0.03f, z * 0.82f),
                    new Vector3(0.25f, 0.06f, 0.25f),
                    black);

                Part(parent, "LampHousing",
                    new Vector3(x * 0.88f, 0.615f, z * 0.88f),
                    new Vector3(0.20f, 0.03f, 0.20f),
                    black);

                Part(parent, "Indicator",
                    new Vector3(x * 0.88f, 0.634f, z * 0.88f),
                    new Vector3(0.11f, 0.008f, 0.11f),
                    light);
            }
        }

        // 建立真正的五角星網格，不需要星星圖片。
        string meshPath = folder + "/StarMesh.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

        if (mesh == null)
        {
            mesh = CreateStarMesh();
            AssetDatabase.CreateAsset(mesh, meshPath);
        }

        GameObject star = new GameObject("StarMark");
        star.transform.SetParent(parent, false);
        star.transform.localPosition = new Vector3(0, 0.605f, 0);
        star.AddComponent<MeshFilter>().sharedMesh = mesh;
        star.AddComponent<MeshRenderer>().sharedMaterial = yellow;

        // 台面上方的隱形偵測區。
        GameObject zone = new GameObject("DetectionZone");
        zone.transform.SetParent(parent, false);
        zone.transform.localPosition = new Vector3(0, 1.2f, 0);

        BoxCollider trigger = zone.AddComponent<BoxCollider>();
        trigger.size = new Vector3(1.6f, 1.2f, 1.6f);
        trigger.isTrigger = true;

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager
            .MarkSceneDirty(gameObject.scene);

        Debug.Log("平台建立完成！請將主物件移到房間地板上。");
#endif
    }

#if UNITY_EDITOR
    Material MakeMaterial(
        string folder, string name, Shader shader, Color color)
    {
        string assetPath = folder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, assetPath);
        }

        mat.shader = shader;
        mat.color = color;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.15f);

        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.15f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    void Part(
        Transform parent, string name,
        Vector3 position, Vector3 size,
        Material material, bool solid = false)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = material;

        if (!solid)
            DestroyImmediate(obj.GetComponent<BoxCollider>());
    }

    Mesh CreateStarMesh()
    {
        Vector3[] vertices = new Vector3[11];
        int[] triangles = new int[30];

        vertices[0] = Vector3.zero;

        for (int i = 0; i < 10; i++)
        {
            float angle = i * Mathf.PI / 5f;
            float radius = i % 2 == 0 ? 0.58f : 0.25f;

            vertices[i + 1] = new Vector3(
                Mathf.Sin(angle) * radius,
                0,
                Mathf.Cos(angle) * radius);

            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % 10 + 1;
        }

        Mesh mesh = new Mesh();
        mesh.name = "PlatformStar";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
#endif
}