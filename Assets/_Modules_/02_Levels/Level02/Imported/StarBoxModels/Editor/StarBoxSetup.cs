using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public static class StarBoxSetup
{
    [MenuItem("Tools/Star Box/Create Box Prefabs")]
    public static void Create()
    {
        string[] ids = AssetDatabase.FindAssets("StarBoxSetup t:MonoScript");
        if (ids.Length == 0) return;
        string script = AssetDatabase.GUIDToAssetPath(ids[0]);
        string root = Path.GetDirectoryName(Path.GetDirectoryName(script)).Replace('\\', '/');
        string matDir = root + "/Materials";
        string prefabDir = root + "/Prefabs";
        if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(root, "Materials");
        if (!AssetDatabase.IsValidFolder(prefabDir)) AssetDatabase.CreateFolder(root, "Prefabs");

        bool srp = GraphicsSettings.currentRenderPipeline != null;
        Shader shader = Shader.Find(srp ? "Universal Render Pipeline/Lit" : "Standard");
        if (shader == null) {
            EditorUtility.DisplayDialog("Box Setup", "Use the Built-in or Universal Render Pipeline.", "OK");
            return;
        }
        string[] names = { "Cardboard", "TapeTop", "TapeSide", "StarSide" };
        foreach (string name in names)
        {
            string p = matDir + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, p); }
            mat.shader = shader;
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(root + "/" + name + ".png");
            mat.mainTexture = tex;
            mat.color = Color.white;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.08f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(mat);
        }
        foreach (string boxName in new[] { "NormalBox", "StarBox" })
        {
            string modelPath = root + "/" + boxName + ".obj";
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) { Debug.LogError("Missing model: " + modelPath); continue; }
            importer.globalScale = 1f;
            importer.SaveAndReimport();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject rootGO = new GameObject(boxName);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.transform.SetParent(rootGO.transform, false);
            visual.name = "Visual";
            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
            {
                Material[] slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null) continue;
                    foreach (string n in names)
                        if (slots[i].name.StartsWith(n))
                            slots[i] = AssetDatabase.LoadAssetAtPath<Material>(matDir + "/" + n + ".mat");
                }
                r.sharedMaterials = slots;
            }
            BoxCollider collider = rootGO.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.5f, 0f);
            collider.size = Vector3.one;
            Rigidbody rb = rootGO.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            PrefabUtility.SaveAsPrefabAsset(rootGO, prefabDir + "/" + boxName + ".prefab");
            Object.DestroyImmediate(rootGO);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Box Setup", "NormalBox and StarBox are ready in the Prefabs folder.", "OK");
    }
}
