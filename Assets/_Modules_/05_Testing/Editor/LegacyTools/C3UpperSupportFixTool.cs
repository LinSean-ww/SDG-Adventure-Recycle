using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using C3TreasurePuzzle;

// Inspects the completed C3 scene before applying any upper-support changes.
public static class C3UpperSupportFixTool
{
    private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    private static readonly string EvidencePath = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_UpperSupport_Evidence");
    private static readonly string[] WarmNames = {
        "Warm_EntrySafety", "Warm_WestService", "Warm_CentralReactor",
        "Warm_EastService", "Warm_UpperWalkway"
    };
    private static readonly Vector3[] WarmPositions = {
        new Vector3(-20.9f, 6.8f, 4.1f), new Vector3(-22f, 7.15f, 19.2f),
        new Vector3(-12.5f, 7.1f, 9.5f), new Vector3(-4.1f, 7.1f, 13.4f),
        new Vector3(-14f, 8.05f, 15.5f)
    };
    private const string MaterialPath = "Assets/_Modules_/02_Levels/Level03/ArtFull/Materials/";

    // Called both by the one-time repair and by the original lamp construction stage.
    // Existing lamp, pipe and platform roots retain their world positions.
    public static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyInOpenScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save upper support repair.");
        WriteInventory(scene, "After");
        Validate();
        Debug.Log("C3_UPPER_SUPPORT_APPLY_PASS");
    }

    // Completes an already placed bridge without regenerating any existing support.
    public static void CompleteWalkwayBridge()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = scene.GetRootGameObjects().Single(go => go.name == "C_Factory_Art").transform;
        Transform frame = art.Find("Architecture_Additions/UpperServiceSupportFrame");
        if (frame == null) throw new Exception("Upper service frame is missing.");
        if (frame.Find("Warm_UpperWalkway_BridgeTie") != null)
            throw new Exception("Upper walkway bridge tie already exists; refusing duplicate.");
        Material steel = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "M_DarkIndustrialSteel.mat");
        if (steel == null) throw new Exception("Existing steel material is missing.");
        VisualBox(frame, "Warm_UpperWalkway_BridgeTie", new Vector3(-14f, 9.655f, 15.4f),
            new Vector3(0.34f, 0.07f, 1.2f), steel);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save upper walkway bridge tie.");
        WriteInventory(scene, "After");
        Validate();
        Debug.Log("C3_UPPER_WALKWAY_BRIDGE_TIE_PASS");
    }

    public static void ApplyInOpenScene(Scene scene)
    {
        Transform art = scene.GetRootGameObjects().Single(go => go.name == "C_Factory_Art").transform;
        Transform architecture = art.Find("Architecture_Additions");
        Transform pipes = art.Find("Pipes/Pipe_MountsAndConnections");
        Transform lighting = art.Find("Lighting");
        if (architecture == null || pipes == null || lighting == null ||
            architecture.Find("UpperServiceSupportFrame") != null)
            throw new Exception("Upper support input missing or already repaired; refusing duplicate construction.");
        Material steel = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "M_DarkIndustrialSteel.mat");
        Material rust = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath + "M_RustedSteel.mat");
        if (steel == null || rust == null) throw new Exception("Existing C3 structural materials missing.");

        // Verified flat FBX roof at all six endpoints by Before_RoofRaycasts.tsv.
        Transform frame = NewGroup("UpperServiceSupportFrame", architecture);
        foreach (float z in new[] { 9.5f, 14.8f, 16.0f })
        {
            string key = z.ToString("0.0", CultureInfo.InvariantCulture);
            Transform bridge = NewGroup("RoofBridge_" + key, frame);
            VisualBox(bridge, "LowerFlange", new Vector3(-13f, 9.655f, z), new Vector3(14f, 0.07f, 0.34f), steel);
            VisualBox(bridge, "Web", new Vector3(-13f, 9.76f, z), new Vector3(14f, 0.20f, 0.12f), steel);
            VisualBox(bridge, "UpperFlange", new Vector3(-13f, 9.865f, z), new Vector3(14f, 0.07f, 0.34f), steel);
            foreach (float x in new[] { -20f, -6f })
                VisualBox(bridge, "RoofAnchor_" + x.ToString("0", CultureInfo.InvariantCulture),
                    new Vector3(x, 9.94f, z), new Vector3(0.8f, 0.12f, 0.46f), rust);
        }
        foreach (float x in new[] { -20f, -15f, -10f, -6f })
            VisualBox(frame, "ConduitBridgeTie_" + x.ToString("0", CultureInfo.InvariantCulture),
                new Vector3(x, 9.73f, 15.4f), new Vector3(0.16f, 0.12f, 1.2f), steel);
        VisualBox(frame, "Warm_UpperWalkway_BridgeTie", new Vector3(-14f, 9.655f, 15.4f),
            new Vector3(0.34f, 0.07f, 1.2f), steel);

        // The four central rods previously ended at an unroofed circular opening.
        // They now terminate at the underside of the anchored transverse I-beams.
        foreach (float x in new[] { -20f, -15f, -10f, -6f })
        foreach (float z in new[] { 14.8f, 16.0f })
        {
            string name = "CeilingCableHanger_" + x.ToString("0.0", CultureInfo.InvariantCulture) +
                          "_" + z.ToString("0.0", CultureInfo.InvariantCulture);
            Transform hanger = pipes.Find(name);
            if (hanger == null || hanger.GetComponent<BoxCollider>() == null)
                throw new Exception("Missing existing conduit hanger/collider: " + name);
            if (Mathf.Abs(hanger.position.x - x) > 0.002f || Mathf.Abs(hanger.position.z - z) > 0.002f)
                throw new Exception("Existing conduit hanger has moved: " + name);
            if (x == -15f || x == -10f) SetVertical(hanger, 7.15f, 9.62f);
            // The pipe center is y=7.15; this small saddle wraps the tube at the rod end.
            VisualBox(frame, "ConduitSaddle_" + x.ToString("0", CultureInfo.InvariantCulture) + "_" +
                z.ToString("0.0", CultureInfo.InvariantCulture), new Vector3(x, 7.055f, z),
                new Vector3(0.25f, 0.10f, 0.25f), rust);
        }

        foreach (string lampName in new[] { "Warm_EntrySafety", "Warm_WestService", "Warm_CentralReactor",
                     "Warm_EastService", "Warm_UpperWalkway" })
        {
            Transform lamp = lighting.Find(lampName);
            Transform mount = lamp == null ? null : lamp.Find("CeilingMount");
            if (mount == null || mount.Find("SuspensionRod") == null || mount.Find("CeilingPlate") == null)
                throw new Exception("Existing warm lamp mount missing: " + lampName);
            bool overOpening = lampName == "Warm_CentralReactor" || lampName == "Warm_UpperWalkway";
            float plateTop = overOpening ? 9.62f : 10.00f;
            Transform plate = mount.Find("CeilingPlate");
            plate.position = new Vector3(lamp.position.x, plateTop - 0.065f, lamp.position.z);
            Transform bar = mount.Find("CeilingCrossbar");
            bar.position = new Vector3(lamp.position.x, plateTop - 0.11f, lamp.position.z);
            SetVertical(mount.Find("SuspensionRod"), lamp.position.y + 0.19f,
                overOpening ? 9.49f : 9.89f);
        }
    }

    private static Transform NewGroup(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void VisualBox(Transform parent, string name, Vector3 center, Vector3 size, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    private static void SetVertical(Transform t, float bottom, float top)
    {
        t.position = new Vector3(t.position.x, (bottom + top) * 0.5f, t.position.z);
        Vector3 size = t.localScale;
        t.localScale = new Vector3(size.x, top - bottom, size.z);
    }

    public static void Validate()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = scene.GetRootGameObjects().Single(go => go.name == "C_Factory_Art").transform;
        Transform frame = art.Find("Architecture_Additions/UpperServiceSupportFrame");
        Transform pipes = art.Find("Pipes/Pipe_MountsAndConnections");
        Transform lighting = art.Find("Lighting");
        if (frame == null || pipes == null || lighting == null) throw new Exception("Upper support repair missing.");
        var output = new StringBuilder("Path\tRole\tWorldPosition\tWorldEuler\tLossyScale\tRendererBounds\tColliderBounds\n");
        foreach (Transform t in frame.GetComponentsInChildren<Transform>(true))
            AppendRow(output, t, "added overhead bridge or pipe saddle");
        foreach (float x in new[] { -20f, -15f, -10f, -6f })
        foreach (float z in new[] { 14.8f, 16.0f })
        {
            Transform hanger = pipes.Find("CeilingCableHanger_" + x.ToString("0.0", CultureInfo.InvariantCulture) +
                "_" + z.ToString("0.0", CultureInfo.InvariantCulture));
            if (hanger == null || hanger.GetComponent<BoxCollider>() == null)
                throw new Exception("Upper conduit hanger/collider lost.");
            float expectedTop = x == -15f || x == -10f ? 9.62f : 9.90f;
            if (Mathf.Abs(hanger.GetComponent<Renderer>().bounds.max.y - expectedTop) > 0.002f ||
                Mathf.Abs(hanger.GetComponent<Renderer>().bounds.min.y - 7.15f) > 0.002f)
                throw new Exception("Upper conduit hanger endpoint mismatch: " + hanger.name);
        }
        Transform blockoutSupports = scene.GetRootGameObjects().Single(go => go.name == "C_TwoFloor_Blockout")
            .transform.Find("Supports");
        foreach (Transform t in blockoutSupports.GetComponentsInChildren<Transform>(true))
            AppendRow(output, t, "retained floor-to-deck support");
        foreach (Transform t in art.Find("Architecture_Additions/Steel_MainDeckCrossBraces")
                     .GetComponentsInChildren<Transform>(true))
            AppendRow(output, t, "retained deck diagonal brace");
        foreach (Transform t in pipes.GetComponentsInChildren<Transform>(true))
            AppendRow(output, t, "retained wall/deck/ceiling pipe connector");
        foreach (Transform t in art.Find("Pipes/Pipe_UpperCableConduit").GetComponentsInChildren<Transform>(true))
            AppendRow(output, t, "retained upper conduit and collision");
        foreach (string name in new[] { "Warm_EntrySafety", "Warm_WestService", "Warm_CentralReactor",
                     "Warm_EastService", "Warm_UpperWalkway" })
        {
            Transform lamp = lighting.Find(name);
            int lampIndex = Array.IndexOf(WarmNames, name);
            if (lampIndex < 0 || Vector3.Distance(lamp.position, WarmPositions[lampIndex]) > 0.001f)
                throw new Exception("Warm lamp root moved: " + name);
            Transform mount = lamp.Find("CeilingMount");
            bool opening = name == "Warm_CentralReactor" || name == "Warm_UpperWalkway";
            float top = opening ? 9.62f : 10.00f;
            if (Mathf.Abs(mount.Find("CeilingPlate").GetComponent<Renderer>().bounds.max.y - top) > 0.002f ||
                Mathf.Abs(mount.Find("SuspensionRod").GetComponent<Renderer>().bounds.max.y -
                    (opening ? 9.49f : 9.89f)) > 0.002f)
                throw new Exception("Lamp mount endpoint mismatch: " + name);
            AppendRow(output, lamp, "retained warm lamp root");
            foreach (Transform child in mount.GetComponentsInChildren<Transform>(true))
                AppendRow(output, child, "lamp mount");
        }
        foreach (float z in new[] { 9.5f, 14.8f, 16.0f })
        {
            string key = z.ToString("0.0", CultureInfo.InvariantCulture);
            Transform bridge = frame.Find("RoofBridge_" + key);
            if (bridge == null || bridge.Find("RoofAnchor_-20") == null || bridge.Find("RoofAnchor_-6") == null)
                throw new Exception("Unanchored service bridge: " + key);
            if (Mathf.Abs(bridge.Find("LowerFlange").GetComponent<Renderer>().bounds.min.y - 9.62f) > 0.002f)
                throw new Exception("Service beam lower edge mismatch: " + key);
        }
        Renderer walkwayTie = frame.Find("Warm_UpperWalkway_BridgeTie").GetComponent<Renderer>();
        Renderer walkwayPlate = lighting.Find("Warm_UpperWalkway/CeilingMount/CeilingPlate").GetComponent<Renderer>();
        if (walkwayTie.bounds.min.y > walkwayPlate.bounds.max.y + 0.002f ||
            walkwayTie.bounds.min.z > walkwayPlate.bounds.max.z ||
            walkwayTie.bounds.max.z < walkwayPlate.bounds.min.z)
            throw new Exception("Upper walkway lamp plate does not meet bridge tie.");
        // Unlike the broad collision proxy, these six endpoints must touch
        // actual FBX ceiling triangles. The middle samples must remain open.
        MeshFilter factoryMesh = scene.GetRootGameObjects().Single(go => go.name == "ONLYfactory")
            .GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "factory");
        GameObject roofProbe = new GameObject("TEMP_C3_UpperSupportRoofProbe");
        roofProbe.transform.position = factoryMesh.transform.position;
        roofProbe.transform.rotation = factoryMesh.transform.rotation;
        roofProbe.transform.localScale = factoryMesh.transform.lossyScale;
        bool oldBackfaces = Physics.queriesHitBackfaces;
        try
        {
            MeshCollider roof = roofProbe.AddComponent<MeshCollider>();
            roof.sharedMesh = factoryMesh.sharedMesh;
            Physics.queriesHitBackfaces = true;
            Physics.SyncTransforms();
            foreach (float z in new[] { 9.5f, 14.8f, 16.0f })
            {
                foreach (float x in new[] { -20f, -6f })
                    if (!roof.Raycast(new Ray(new Vector3(x, 10.3f, z), Vector3.down),
                            out RaycastHit hit, 0.5f) || Mathf.Abs(hit.point.y - 10f) > 0.01f)
                        throw new Exception("Bridge anchor misses real FBX roof: " + x + "," + z);
                if (roof.Raycast(new Ray(new Vector3(-12.5f, 10.3f, z), Vector3.down),
                        out RaycastHit middle, 0.5f))
                    throw new Exception("Expected original roof opening was altered: z=" + z + " y=" + middle.point.y);
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = oldBackfaces;
            UnityEngine.Object.DestroyImmediate(roofProbe);
        }
        if (art.GetComponentsInChildren<Collider>(true).Length != 54)
            throw new Exception("Art collider count changed unexpectedly.");
        if (scene.GetRootGameObjects().Single(go => go.name == "C_TwoFloor_Blockout")
                .GetComponentsInChildren<BoxCollider>(true).Length != 157)
            throw new Exception("M2 platform collider count changed.");
        C3CollectiblePart[] parts = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<C3CollectiblePart>(true)).ToArray();
        if (parts.Length != 3 || parts.Select(p => p.PartId).OrderBy(x => x).SequenceEqual(new[] { "control_chip", "filter_module", "fuse" }) == false)
            throw new Exception("Core collectible references changed.");
        Transform player = scene.GetRootGameObjects().Single(go => go.name == "M2_Player").transform;
        if (player.GetComponent<C3FirstPersonController>() == null || player.GetComponent<C3PlayerInteractor>() == null ||
            scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true))
                .Any(c => c.GetType().FullName.Contains("SharedPlayer")))
            throw new Exception("Original C3 player references changed or M7 was connected.");
        Physics.SyncTransforms();
        CheckRay("Fuse", new Vector3(-20f, 2.05f, 20.3f), new Vector3(-20f, 0.98f, 22f), "M3_Fuse");
        CheckRay("Chip", new Vector3(-12.5f, 6f, 16.7f), new Vector3(-12.5f, 4.9f, 18.5f), "M3_ControlChip");
        CheckRay("Filter", new Vector3(-4f, 2.05f, 16.3f), new Vector3(-4f, 1.03f, 18f), "M3_FilterModule");
        CheckRay("Console", new Vector3(-2.25f, 2.05f, 18.95f), new Vector3(-0.65f, 1.4f, 18.95f), "M4_PuzzleConsole");
        Directory.CreateDirectory(EvidencePath);
        File.WriteAllText(EvidencePath + "/After_SupportInventory.tsv", output.ToString(), new UTF8Encoding(false));
        File.WriteAllText(EvidencePath + "/After_Validation.txt",
            "bridges=3 anchoredEndpoints=6 upperHangers=8 centralHangersShortened=4 warmLampRootsUnmoved=5\n" +
            "artColliders=54 m2BoxColliders=157 collectibleParts=3\n" +
            "fbxRoofAnchorRays=6/6 roofOpeningPreserved=3/3 gameplayTargetRays=4/4 M7Components=0\nRESULT=PASS\n",
            new UTF8Encoding(false));
        Debug.Log("C3_UPPER_SUPPORT_VALIDATE_PASS");
    }

    private static void CheckRay(string label, Vector3 origin, Vector3 target, string expected)
    {
        Vector3 direction = (target - origin).normalized;
        if (!Physics.Raycast(origin, direction, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore) ||
            hit.collider.name != expected)
            throw new Exception("Gameplay target obstructed: " + label + " first=" +
                (hit.collider == null ? "NONE" : hit.collider.name));
    }

    private static void AppendRow(StringBuilder output, Transform t, string role)
    {
        Renderer renderer = t.GetComponent<Renderer>();
        Collider collider = t.GetComponent<Collider>();
        output.Append(PathOf(t)).Append('\t').Append(role).Append('\t')
            .Append(V(t.position)).Append('\t').Append(V(t.eulerAngles)).Append('\t')
            .Append(V(t.lossyScale)).Append('\t')
            .Append(renderer == null ? "-" : B(renderer.bounds)).Append('\t')
            .Append(collider == null ? "-" : B(collider.bounds)).Append('\n');
    }

    public static void Probe()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = scene.GetRootGameObjects().Single(go => go.name == "C_Factory_Art").transform;
        if (art.Find("Architecture_Additions/UpperServiceSupportFrame") != null)
            throw new Exception("Scene already repaired; use ProbeBaselineBackup for before evidence.");
        Directory.CreateDirectory(EvidencePath);
        WriteInventory(scene, "Before");
        ProbeRoofCoverage(scene);
        ProbeRoofRaycasts(scene);
        Debug.Log("C3_UPPER_SUPPORT_PROBE_PASS");
    }

    public static void ProbeBaselineBackup()
    {
        string backup = EvidencePath + "/C_Main_PreUpperSupport_20261027.unity.bak";
        const string temporary = "Assets/_Modules_/05_Testing/Scenes/C3_UpperSupport_BaselineProbe.unity";
        if (!File.Exists(backup) || File.Exists(temporary))
            throw new Exception("Baseline scene backup missing or temporary scene path occupied.");
        try
        {
            File.Copy(backup, temporary, false);
            AssetDatabase.ImportAsset(temporary);
            Scene baseline = EditorSceneManager.OpenScene(temporary, OpenSceneMode.Single);
            WriteInventory(baseline, "Before");
            Debug.Log("C3_UPPER_SUPPORT_BASELINE_PROBE_PASS");
        }
        finally
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            AssetDatabase.DeleteAsset(temporary);
        }
    }

    private static void WriteInventory(Scene scene, string label)
    {
        var output = new StringBuilder("Path\tWorldPosition\tWorldEuler\tLossyScale\tRendererBounds\tColliderBounds\tSource\n");
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "C_Factory_Art" && root.name != "C_TwoFloor_Blockout" &&
                root.name != "C_CollisionProxies" && root.name != "ONLYfactory") continue;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (root.name == "ONLYfactory" && t != root.transform && t.name != "factory") continue;
                if (root.name == "C_CollisionProxies" && t.name != "C_Ceiling") continue;
                Renderer renderer = t.GetComponent<Renderer>();
                Collider collider = t.GetComponent<Collider>();
                output.Append(PathOf(t)).Append('\t').Append(V(t.position)).Append('\t')
                    .Append(V(t.eulerAngles)).Append('\t').Append(V(t.lossyScale)).Append('\t')
                    .Append(renderer != null ? B(renderer.bounds) : "-").Append('\t')
                    .Append(collider != null ? B(collider.bounds) : "-").Append('\t')
                    .Append(root.name).Append('\n');
            }
        }
        File.WriteAllText(EvidencePath + "/" + label + "_Inventory.tsv", output.ToString(), new UTF8Encoding(false));
        if (label != "Before") return;
        var meshOutput = new StringBuilder();
        foreach (MeshFilter filter in scene.GetRootGameObjects().Single(go => go.name == "ONLYfactory")
                     .GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Mesh mesh = filter.sharedMesh;
            meshOutput.AppendLine(PathOf(filter.transform) + " mesh=" + mesh.name + " subMeshes=" + mesh.subMeshCount +
                                  " worldRenderer=" + B(filter.GetComponent<Renderer>().bounds));
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                Bounds local = mesh.GetSubMesh(i).bounds;
                Vector3[] corners = new Vector3[8];
                for (int c = 0; c < 8; c++) corners[c] = filter.transform.TransformPoint(local.center +
                    Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1,
                        (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                Bounds world = new Bounds(corners[0], Vector3.zero);
                foreach (Vector3 corner in corners) world.Encapsulate(corner);
                meshOutput.AppendLine("  submesh=" + i + " worldAABB=" + B(world));
            }
        }
        File.WriteAllText(EvidencePath + "/Before_FBX_SubmeshBounds.txt", meshOutput.ToString(), new UTF8Encoding(false));
    }

    private static void ProbeRoofRaycasts(Scene scene)
    {
        MeshFilter filter = scene.GetRootGameObjects().Single(go => go.name == "ONLYfactory")
            .GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "factory");
        GameObject probe = new GameObject("TEMP_C3_RoofRayProbe");
        probe.transform.position = filter.transform.position;
        probe.transform.rotation = filter.transform.rotation;
        probe.transform.localScale = filter.transform.lossyScale;
        var output = new StringBuilder("X\tZ\tNearCeilingDown\tNearCeilingUp\tFromAbove\n");
        bool previousBackfaces = Physics.queriesHitBackfaces;
        try
        {
            MeshCollider collider = probe.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            Physics.queriesHitBackfaces = true;
            Physics.SyncTransforms();
            foreach (float x in new[] { -22f, -20.9f, -20f, -15f, -14f, -12.5f, -10f, -6f, -4.1f })
            foreach (float z in new[] { 4.1f, 9.5f, 13.4f, 14.8f, 15.5f, 16f, 19.2f })
            {
                output.Append(x.ToString("F2", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(z.ToString("F2", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(Ray(collider, new Vector3(x, 10.3f, z), Vector3.down, 0.7f)).Append('\t')
                    .Append(Ray(collider, new Vector3(x, 9.6f, z), Vector3.up, 0.7f)).Append('\t')
                    .Append(Ray(collider, new Vector3(x, 30f, z), Vector3.down, 22f)).Append('\n');
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = previousBackfaces;
            UnityEngine.Object.DestroyImmediate(probe);
        }
        File.WriteAllText(EvidencePath + "/Before_RoofRaycasts.tsv", output.ToString(), new UTF8Encoding(false));
    }

    private static string Ray(Collider collider, Vector3 origin, Vector3 direction, float distance)
    {
        return collider.Raycast(new Ray(origin, direction), out RaycastHit hit, distance)
            ? hit.point.y.ToString("F3", CultureInfo.InvariantCulture) + "@triangle" + hit.triangleIndex
            : "-";
    }

    private static void ProbeRoofCoverage(Scene scene)
    {
        MeshFilter filter = scene.GetRootGameObjects().Single(go => go.name == "ONLYfactory")
            .GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "factory");
        Mesh mesh = filter.sharedMesh;
        var output = new StringBuilder("Point\tSubmesh12HitsY\tSubmesh13HitsY\n");
        output.AppendLine("meshReadable=" + mesh.isReadable);
        using (var dataArray = Mesh.AcquireReadOnlyMeshData(mesh))
        {
            Mesh.MeshData data = dataArray[0];
            using (var vertexData = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
            using (var roofData = new NativeArray<int>((int)mesh.GetIndexCount(12), Allocator.Temp))
            using (var circleData = new NativeArray<int>((int)mesh.GetIndexCount(13), Allocator.Temp))
            {
                data.GetVertices(vertexData);
                data.GetIndices(roofData, 12);
                data.GetIndices(circleData, 13);
                Vector3[] vertices = vertexData.ToArray().Select(filter.transform.TransformPoint).ToArray();
                Vector2[] points = {
                    new Vector2(-20.9f, 4.1f), new Vector2(-22f, 19.2f),
                    new Vector2(-12.5f, 9.5f), new Vector2(-4.1f, 13.4f), new Vector2(-14f, 15.5f),
                    new Vector2(-20f, 14.8f), new Vector2(-20f, 16f), new Vector2(-15f, 14.8f),
                    new Vector2(-15f, 16f), new Vector2(-10f, 14.8f), new Vector2(-10f, 16f),
                    new Vector2(-6f, 14.8f), new Vector2(-6f, 16f)
                };
                int[] roof = roofData.ToArray();
                int[] circle = circleData.ToArray();
                foreach (Vector2 point in points)
                {
                    output.Append(string.Format(CultureInfo.InvariantCulture, "({0:F2},{1:F2})", point.x, point.y))
                        .Append('\t').Append(Hits(vertices, roof, point)).Append('\t')
                        .Append(Hits(vertices, circle, point)).Append('\n');
                }
            }
        }
        File.WriteAllText(EvidencePath + "/Before_RoofCoverage.tsv", output.ToString(), new UTF8Encoding(false));
    }

    private static string Hits(Vector3[] vertices, int[] indices, Vector2 point)
    {
        var hits = new System.Collections.Generic.List<float>();
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
            float det = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(det) < 0.000001f) continue;
            float u = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.y - c.z)) / det;
            float v = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.y - c.z)) / det;
            if (u < -0.0001f || v < -0.0001f || u + v > 1.0001f) continue;
            float y = u * a.y + v * b.y + (1f - u - v) * c.y;
            if (!hits.Any(h => Mathf.Abs(h - y) < 0.02f)) hits.Add(y);
        }
        hits.Sort();
        return hits.Count == 0 ? "-" : string.Join(",", hits.Select(h => h.ToString("F3", CultureInfo.InvariantCulture)).ToArray());
    }

    private static string PathOf(Transform t)
    {
        return t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    }

    private static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture,
        "({0:F3},{1:F3},{2:F3})", v.x, v.y, v.z);

    private static string B(Bounds b) => V(b.min) + ".." + V(b.max);
}
