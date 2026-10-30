using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Additive C3 factory art. Meshes and materials live under ArtFull, separate from the source FBX.
public static class C3FactoryArtConstruction
{
    private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    private const string ArtPath = "Assets/_Modules_/02_Levels/Level03/ArtFull";
    private const string MeshPath = ArtPath + "/Meshes";
    private const string MaterialPath = ArtPath + "/Materials";
    private static readonly string EvidencePath = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_FullArt_Evidence");
    private static Material placeholder;

    public static void StageB_Architecture()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject blockout = scene.GetRootGameObjects().Single(root => root.name == "C_TwoFloor_Blockout");
        if (scene.GetRootGameObjects().Any(root => root.name == "C_Factory_Art"))
            throw new Exception("C_Factory_Art already exists; refusing duplicate construction.");
        PrepareAssetFolders();
        Transform root = Group("C_Factory_Art", null);
        string[] names = { "Architecture_Additions", "Floor1_Industrial", "Floor2_Industrial", "IndustrialMachines",
            "Pipes", "Props", "Materials_Visuals", "Lighting", "Audio", "Decorations" };
        foreach (string name in names) Group(name, root);
        Transform architecture = root.Find("Architecture_Additions");

        // Preserve M2 collision geometry; replace only its solid white guard/support appearance.
        Transform railings = blockout.transform.Find("Railings");
        foreach (MeshRenderer renderer in railings.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = false;
        Transform supports = blockout.transform.Find("Supports");
        foreach (MeshRenderer renderer in supports.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = false;

        foreach (BoxCollider physical in railings.GetComponentsInChildren<BoxCollider>(true))
        {
            if (physical.name.StartsWith("R_Stair_")) continue;
            if (physical.name.StartsWith("Guard")) continue;
            Bounds b = physical.bounds;
            bool alongX = b.size.x > b.size.z;
            float length = alongX ? b.size.x : b.size.z;
            var geo = new Geometry();
            AddRailSegment(geo, length, alongX);
            MeshObject("Steel_Visual_" + physical.name, architecture, b.center, geo);
        }

        foreach (float stairX in new[] { -21.1f, -5.9f })
        {
            string label = stairX < -10f ? "West" : "East";
            var stringers = new Geometry();
            var noses = new Geometry();
            var stairRails = new Geometry();
            for (int side = -1; side <= 1; side += 2)
            {
                float x = stairX + side * 0.87f;
                stringers.AddBeam(new Vector3(x, 0.66f, 6.08f), new Vector3(x, 4.39f, 13.84f), 0.13f, 0.24f);
                float guardX = stairX + side * 0.96f;
                stairRails.AddTube(new Vector3(guardX, 1.80f, 6.18f), new Vector3(guardX, 5.57f, 13.74f), 0.055f, 10);
                stairRails.AddTube(new Vector3(guardX, 1.30f, 6.18f), new Vector3(guardX, 5.07f, 13.74f), 0.036f, 10);
                for (int index = 0; index < 22; index += 4)
                {
                    float z = 6.18f + index * 0.36f;
                    float top = 0.55f + (index + 1) * 3.95f / 22f;
                    stairRails.AddBox(new Vector3(guardX, top + 0.55f, z), new Vector3(0.075f, 1.1f, 0.075f));
                }
            }
            for (int index = 0; index < 22; index++)
            {
                float z = 6.18f + index * 0.36f;
                float top = 0.55f + (index + 1) * 3.95f / 22f;
                noses.AddBox(new Vector3(stairX, top + 0.018f, z - 0.155f), new Vector3(1.8f, 0.035f, 0.035f));
            }
            MeshObject("Steel_" + label + "StairStringers", architecture, Vector3.zero, stringers);
            MeshObject("Steel_" + label + "StairNosing", architecture, Vector3.zero, noses);
            MeshObject("Steel_" + label + "StairOpenRail", architecture, Vector3.zero, stairRails);
        }

        var deckBeams = new Geometry();
        foreach (float z in new[] { 13.95f, 16.85f })
            deckBeams.AddBox(new Vector3(-13.5f, 4.15f, z), new Vector3(17f, 0.28f, 0.22f));
        foreach (float x in new[] { -21.5f, -17.5f, -13.5f, -9.5f, -5.5f })
            deckBeams.AddBox(new Vector3(x, 4.15f, 15.4f), new Vector3(0.22f, 0.28f, 3.2f));
        MeshObject("Steel_MainDeckUnderframe", architecture, Vector3.zero, deckBeams);

        var beamMesh = new Geometry();
        beamMesh.AddBox(new Vector3(-0.15f, 0f, 0f), new Vector3(0.07f, 3.75f, 0.34f));
        beamMesh.AddBox(new Vector3(0.15f, 0f, 0f), new Vector3(0.07f, 3.75f, 0.34f));
        beamMesh.AddBox(Vector3.zero, new Vector3(0.30f, 3.75f, 0.07f));
        Mesh supportMesh = SaveMesh("Steel_IBeamSupport", beamMesh);
        foreach (BoxCollider support in supports.GetComponentsInChildren<BoxCollider>(true))
            MeshObject("Steel_Visual_" + support.name, architecture, support.bounds.center, supportMesh);

        var crossBraces = new Geometry();
        foreach (float x in new[] { -21.5f, -17.5f, -10f, -5.5f })
        {
            crossBraces.AddBeam(new Vector3(x, 0.78f, 14.5f), new Vector3(x, 4.1f, 16.5f), 0.075f, 0.075f);
            crossBraces.AddBeam(new Vector3(x, 0.78f, 16.5f), new Vector3(x, 4.1f, 14.5f), 0.075f, 0.075f);
        }
        MeshObject("Steel_MainDeckCrossBraces", architecture, Vector3.zero, crossBraces);
        Save(scene);
        ValidateStageB();
        Debug.Log("C3_FULLART_STAGE_B_PASS");
    }

    public static void ValidateStageB()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        Transform art = roots.Single(root => root.name == "C_Factory_Art").transform;
        Transform blockout = roots.Single(root => root.name == "C_TwoFloor_Blockout").transform;
        if (art.Find("Architecture_Additions").GetComponentsInChildren<MeshRenderer>(true).Length < 25)
            throw new Exception("Insufficient architectural mesh assemblies.");
        if (blockout.Find("Railings").GetComponentsInChildren<MeshRenderer>(true).Any(renderer => renderer.enabled))
            throw new Exception("Solid M2 guard renderers are still visible.");
        if (blockout.GetComponentsInChildren<Collider>(true).Length != 157 ||
            art.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new Exception("M2 collision geometry was altered during visual architecture pass.");
        Directory.CreateDirectory(EvidencePath);
        File.WriteAllText(EvidencePath + "/StageB_Architecture.txt",
            "artVisualMeshes=" + art.Find("Architecture_Additions").GetComponentsInChildren<MeshRenderer>(true).Length + "\n" +
            "m2PhysicalColliders=" + blockout.GetComponentsInChildren<Collider>(true).Length + "\n" +
            "artColliders=" + art.GetComponentsInChildren<Collider>(true).Length + "\n" +
            "closedWhiteRailRenderers=0\n", new UTF8Encoding(false));
        Debug.Log("C3_FULLART_STAGE_B_VERIFY_PASS");
    }

    public static void StageC_IndustrialEnvironment()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = scene.GetRootGameObjects().Single(root => root.name == "C_Factory_Art").transform;
        if (art.Find("IndustrialMachines/PurificationReactor") != null)
            throw new Exception("Industrial construction already exists; refusing duplicate construction.");
        PrepareAssetFolders();
        Transform machines = art.Find("IndustrialMachines");
        Transform floor1 = art.Find("Floor1_Industrial");
        Transform floor2 = art.Find("Floor2_Industrial");
        Transform pipes = art.Find("Pipes");
        Transform props = art.Find("Props");
        Transform decals = art.Find("Decorations");

        Transform reactor = Machine("PurificationReactor", machines, new Vector3(-12.5f, 0.55f, 9.5f),
            new Vector3(0f, 1.58f, 0f), new Vector3(2.45f, 3.16f, 2.45f));
        var reactorShell = new Geometry();
        reactorShell.AddCylinder(new Vector3(0f, 1.52f, 0f), 1.04f, 2.6f, 24);
        reactorShell.AddCylinder(new Vector3(0f, 0.16f, 0f), 1.20f, 0.25f, 24);
        reactorShell.AddCylinder(new Vector3(0f, 2.87f, 0f), 1.16f, 0.20f, 24);
        Part("Steel_ReactorPressureBody", reactor, reactorShell);
        var reactorRust = new Geometry();
        foreach (float y in new[] { 0.37f, 1.30f, 2.42f }) reactorRust.AddCylinder(new Vector3(0f, y, 0f), 1.08f, 0.12f, 24);
        Part("Rust_ReactorSeams", reactor, reactorRust);
        var reactorPanel = new Geometry();
        reactorPanel.AddBox(new Vector3(0f, 1.57f, -1.05f), new Vector3(0.80f, 1.20f, 0.14f));
        Part("Olive_ReactorServicePanel", reactor, reactorPanel);
        var reactorGlass = new Geometry();
        reactorGlass.AddBox(new Vector3(0f, 1.70f, -1.13f), new Vector3(0.38f, 0.72f, 0.035f));
        Part("GlowCyan_ReactorSightGlass", reactor, reactorGlass);

        Transform sorter = Machine("WasteSortingLine", machines, new Vector3(-16.0f, 0.55f, 7.5f),
            new Vector3(0f, 0.73f, 0f), new Vector3(3.4f, 1.46f, 1.6f));
        var sorterSteel = new Geometry();
        sorterSteel.AddBox(new Vector3(0f, 0.30f, 0f), new Vector3(3.2f, 0.58f, 1.5f));
        sorterSteel.AddBox(new Vector3(0f, 1.24f, 0f), new Vector3(3.3f, 0.13f, 1.3f));
        foreach (float x in new[] { -1.32f, 1.32f })
            foreach (float z in new[] { -0.58f, 0.58f })
                sorterSteel.AddBox(new Vector3(x, 0.82f, z), new Vector3(0.12f, 0.90f, 0.12f));
        Part("Steel_SortingFrame", sorter, sorterSteel);
        var sorterBelt = new Geometry();
        sorterBelt.AddBox(new Vector3(0f, 1.30f, 0f), new Vector3(3.00f, 0.04f, 0.90f));
        Part("Rubber_SortingBelt", sorter, sorterBelt);
        var sorterWarnings = new Geometry();
        sorterWarnings.AddBox(new Vector3(0f, 0.73f, -0.79f), new Vector3(2.8f, 0.10f, 0.025f));
        Part("Warning_SortingEdge", sorter, sorterWarnings);

        Transform pump = Machine("FeedPump", machines, new Vector3(-8.5f, 0.55f, 8.0f),
            new Vector3(0f, 0.93f, 0f), new Vector3(2.0f, 1.86f, 2.0f));
        var pumpGeo = new Geometry();
        pumpGeo.AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(1.9f, 0.24f, 1.9f));
        pumpGeo.AddCylinder(new Vector3(0f, 1.0f, 0f), 0.72f, 1.45f, 20);
        pumpGeo.AddCylinder(new Vector3(0f, 1.72f, 0f), 0.79f, 0.12f, 20);
        Part("Olive_PumpHousing", pump, pumpGeo);
        var pumpRust = new Geometry();
        pumpRust.AddCylinder(new Vector3(0f, 0.49f, 0f), 0.77f, 0.10f, 20);
        pumpRust.AddBox(new Vector3(0.78f, 1.05f, 0f), new Vector3(0.24f, 0.55f, 0.30f));
        Part("Rust_PumpCorrosion", pump, pumpRust);

        Cabinet("WestPowerCabinet_A", floor1, new Vector3(-23.1f, 0.55f, 17.7f), 0.95f, 2.0f, 0.68f);
        Cabinet("WestPowerCabinet_B", floor1, new Vector3(-23.1f, 0.55f, 20.1f), 0.95f, 2.0f, 0.68f);
        Cabinet("EastToolCabinet", floor1, new Vector3(-2.0f, 0.55f, 6.5f), 1.35f, 2.10f, 0.75f);

        Transform filters = Machine("NortheastFilterArray", machines, new Vector3(-5.0f, 0.55f, 21.0f),
            new Vector3(0f, 1.44f, 0f), new Vector3(2.65f, 2.88f, 1.55f));
        var filterBody = new Geometry();
        foreach (float x in new[] { -0.65f, 0.65f })
        {
            filterBody.AddCylinder(new Vector3(x, 1.55f, 0f), 0.54f, 2.38f, 20);
            filterBody.AddCylinder(new Vector3(x, 2.72f, 0f), 0.59f, 0.13f, 20);
        }
        Part("Olive_TwinFilterVessels", filters, filterBody);
        var filterBands = new Geometry();
        foreach (float x in new[] { -0.65f, 0.65f })
            foreach (float y in new[] { 0.54f, 1.58f, 2.50f })
                filterBands.AddCylinder(new Vector3(x, y, 0f), 0.575f, 0.09f, 20);
        Part("Rust_FilterBands", filters, filterBands);
        var filterBase = new Geometry();
        filterBase.AddBox(new Vector3(0f, 0.11f, 0f), new Vector3(2.45f, 0.22f, 1.4f));
        Part("Steel_FilterSkid", filters, filterBase);

        Transform chipBench = Machine("UpperChipMaintenanceBench", floor2, new Vector3(-13.2f, 4.5f, 19.28f),
            new Vector3(0f, 0.43f, 0f), new Vector3(1.35f, 0.86f, 0.58f));
        var benchFrame = new Geometry();
        benchFrame.AddBox(new Vector3(0f, 0.77f, 0f), new Vector3(1.35f, 0.11f, 0.58f));
        foreach (float x in new[] { -0.57f, 0.57f })
            foreach (float z in new[] { -0.23f, 0.23f })
                benchFrame.AddBox(new Vector3(x, 0.38f, z), new Vector3(0.075f, 0.75f, 0.075f));
        Part("Steel_UpperWorkbench", chipBench, benchFrame);
        var benchTop = new Geometry();
        benchTop.AddBox(new Vector3(0f, 0.84f, 0f), new Vector3(1.24f, 0.04f, 0.51f));
        Part("Rust_UpperWorkSurface", chipBench, benchTop);
        var benchNotes = new Geometry();
        benchNotes.AddBox(new Vector3(-0.33f, 0.88f, 0f), new Vector3(0.24f, 0.01f, 0.30f));
        Part("Paper_MaintenanceLog", chipBench, benchNotes);

        Cabinet("UpperDistributionBox", floor2, new Vector3(-7.25f, 4.5f, 19.22f), 1.05f, 1.0f, 0.55f);

        WasteContainer("WasteContainer_A", props, new Vector3(-16.0f, 0.55f, 23.0f));
        WasteContainer("WasteContainer_B", props, new Vector3(-8.0f, 0.55f, 23.0f));
        Barrel("Barrel_West", props, new Vector3(-22.8f, 0.55f, 23.1f));
        Barrel("Barrel_North", props, new Vector3(-10.3f, 0.55f, 23.35f));
        Barrel("Barrel_East", props, new Vector3(-7.0f, 0.55f, 22.8f));

        var westPipe = new Geometry();
        westPipe.AddTube(new Vector3(-24.35f, 3.10f, 3.0f), new Vector3(-24.35f, 3.10f, 22.5f), 0.105f, 12);
        westPipe.AddTube(new Vector3(-24.35f, 3.10f, 17.7f), new Vector3(-23.55f, 2.45f, 17.7f), 0.08f, 12);
        westPipe.AddTube(new Vector3(-24.35f, 3.10f, 20.1f), new Vector3(-23.55f, 2.45f, 20.1f), 0.08f, 12);
        MeshObject("Pipe_WestServiceTrunk", pipes, Vector3.zero, westPipe);
        var northPipe = new Geometry();
        northPipe.AddTube(new Vector3(-22.5f, 3.18f, 24.2f), new Vector3(-3.2f, 3.18f, 24.2f), 0.12f, 14);
        northPipe.AddTube(new Vector3(-5.0f, 3.18f, 24.2f), new Vector3(-5.0f, 2.95f, 21.0f), 0.095f, 12);
        MeshObject("Pipe_NorthFilterFeed", pipes, Vector3.zero, northPipe);
        var reactorPipe = new Geometry();
        reactorPipe.AddTube(new Vector3(-12.5f, 3.55f, 9.5f), new Vector3(-14.55f, 3.55f, 9.5f), 0.12f, 14);
        reactorPipe.AddTube(new Vector3(-14.55f, 3.55f, 9.5f), new Vector3(-14.55f, 3.55f, 21.8f), 0.12f, 14);
        reactorPipe.AddTube(new Vector3(-14.55f, 3.55f, 21.8f), new Vector3(-14.55f, 3.18f, 24.2f), 0.10f, 12);
        MeshObject("Pipe_ReactorReturn", pipes, Vector3.zero, reactorPipe);
        var overhead = new Geometry();
        overhead.AddTube(new Vector3(-21.8f, 7.15f, 14.8f), new Vector3(-5.4f, 7.15f, 14.8f), 0.08f, 10);
        overhead.AddTube(new Vector3(-21.8f, 7.15f, 16.0f), new Vector3(-5.4f, 7.15f, 16.0f), 0.06f, 10);
        MeshObject("Pipe_UpperCableConduit", pipes, Vector3.zero, overhead);

        WarningPanel("EntryFaultWarning", props, new Vector3(-20.9f, 2.65f, 2.9f), new Vector3(2.8f, 0.62f, 0.10f));
        WarningPanel("WestElectricalDanger", props, new Vector3(-24.38f, 2.55f, 17.8f), new Vector3(0.08f, 0.65f, 1.5f));
        WarningPanel("UpperRestrictedMaintenance", floor2, new Vector3(-12.5f, 5.45f, 19.72f), new Vector3(1.5f, 0.42f, 0.07f));
        for (int i = 0; i < 8; i++)
        {
            float x = new[] { -18f, -13.5f, -7.8f, -20.5f, -5.2f, -16.2f, -10.3f, -3.5f }[i];
            float z = new[] { 10.9f, 12.2f, 11.0f, 19.5f, 13.0f, 20.3f, 20.5f, 9.4f }[i];
            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mark.name = "Dirt_FloorStain_" + (i + 1).ToString("00");
            mark.transform.SetParent(decals, false);
            mark.transform.position = new Vector3(x, 0.557f, z);
            mark.transform.rotation = Quaternion.Euler(-90f, i * 29f, 0f);
            mark.transform.localScale = new Vector3(0.9f + (i % 3) * 0.35f, 1.2f + (i % 2) * 0.5f, 1f);
            UnityEngine.Object.DestroyImmediate(mark.GetComponent<Collider>());
            mark.GetComponent<MeshRenderer>().sharedMaterial = placeholder;
        }

        Save(scene);
        ValidateStageC();
        Debug.Log("C3_FULLART_STAGE_C_PASS");
    }

    public static void ValidateStageC()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = scene.GetRootGameObjects().Single(root => root.name == "C_Factory_Art").transform;
        int machineCount = art.Find("IndustrialMachines").childCount + art.Find("Floor1_Industrial").childCount +
            art.Find("Floor2_Industrial").childCount;
        int artColliders = art.GetComponentsInChildren<Collider>(true).Length;
        if (machineCount < 10 || artColliders < 10 || artColliders > 22)
            throw new Exception("Industrial equipment count or collision count is outside the intended range.");
        Physics.SyncTransforms();
        var checks = new[]
        {
            new { Name = "Fuse", Eye = new Vector3(-20f, 2.05f, 20.3f), Target = new Vector3(-20f, 0.98f, 22f), Expected = "M3_Fuse" },
            new { Name = "Chip", Eye = new Vector3(-12.5f, 6f, 16.7f), Target = new Vector3(-12.5f, 4.9f, 18.5f), Expected = "M3_ControlChip" },
            new { Name = "Filter", Eye = new Vector3(-4f, 2.05f, 16.3f), Target = new Vector3(-4f, 1.03f, 18f), Expected = "M3_FilterModule" },
            new { Name = "Console", Eye = new Vector3(-2.5f, 2.05f, 18.95f), Target = new Vector3(-0.9f, 1.3f, 18.95f), Expected = "M4_PuzzleConsole" }
        };
        var evidence = new StringBuilder();
        evidence.AppendLine("machineAssemblies=" + machineCount + " artColliders=" + artColliders);
        foreach (var item in checks)
        {
            Vector3 line = item.Target - item.Eye;
            bool hit = Physics.Raycast(item.Eye, line.normalized, out RaycastHit result, 3f, ~0, QueryTriggerInteraction.Ignore);
            string first = hit ? result.collider.name : "NONE";
            evidence.AppendLine("ray=" + item.Name + " firstHit=" + first);
            if (first != item.Expected) throw new Exception("Art obstructs required interaction: " + item.Name + " first=" + first);
        }
        Directory.CreateDirectory(EvidencePath);
        File.WriteAllText(EvidencePath + "/StageC_Industry.txt", evidence.ToString(), new UTF8Encoding(false));
        Debug.Log("C3_FULLART_STAGE_C_VERIFY_PASS");
    }

    private static Transform Machine(string name, Transform parent, Vector3 position, Vector3 colliderCenter, Vector3 colliderSize)
    {
        Transform root = Group(name, parent);
        root.position = position;
        BoxCollider collider = root.gameObject.AddComponent<BoxCollider>();
        collider.center = colliderCenter;
        collider.size = colliderSize;
        return root;
    }

    private static void Part(string name, Transform parent, Geometry geo) => MeshObject(name, parent, parent.position, geo);

    private static void Cabinet(string name, Transform parent, Vector3 position, float width, float height, float depth)
    {
        Transform root = Machine(name, parent, position, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth));
        var shell = new Geometry();
        shell.AddBox(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth));
        Part("Olive_" + name + "Shell", root, shell);
        var door = new Geometry();
        door.AddBox(new Vector3(0f, height * 0.55f, -depth * 0.51f), new Vector3(width * 0.80f, height * 0.76f, 0.035f));
        Part("Steel_" + name + "Door", root, door);
        var markings = new Geometry();
        markings.AddBox(new Vector3(width * 0.25f, height * 0.67f, -depth * 0.55f), new Vector3(0.12f, 0.25f, 0.028f));
        Part("Warning_" + name + "Tag", root, markings);
    }

    private static void WasteContainer(string name, Transform parent, Vector3 position)
    {
        Transform root = Machine(name, parent, position, new Vector3(0f, 0.55f, 0f), new Vector3(1.45f, 1.1f, 1.2f));
        var body = new Geometry();
        body.AddBox(new Vector3(0f, 0.55f, 0f), new Vector3(1.42f, 1.1f, 1.18f));
        body.AddBox(new Vector3(0f, 1.13f, 0f), new Vector3(1.48f, 0.08f, 1.24f));
        Part("Rust_" + name + "Body", root, body);
        var warning = new Geometry();
        warning.AddBox(new Vector3(0f, 0.70f, -0.60f), new Vector3(0.62f, 0.26f, 0.025f));
        Part("Warning_" + name + "Label", root, warning);
    }

    private static void Barrel(string name, Transform parent, Vector3 position)
    {
        Transform root = Machine(name, parent, position, new Vector3(0f, 0.45f, 0f), new Vector3(0.72f, 0.9f, 0.72f));
        var body = new Geometry();
        body.AddCylinder(new Vector3(0f, 0.46f, 0f), 0.32f, 0.88f, 16);
        Part("Rust_" + name + "Body", root, body);
        var rims = new Geometry();
        foreach (float y in new[] { 0.12f, 0.47f, 0.82f }) rims.AddCylinder(new Vector3(0f, y, 0f), 0.35f, 0.07f, 16);
        Part("Steel_" + name + "Rims", root, rims);
    }

    private static void WarningPanel(string name, Transform parent, Vector3 center, Vector3 size)
    {
        var back = new Geometry();
        back.AddBox(Vector3.zero, size);
        MeshObject("Warning_" + name, parent, center, back);
    }

    private static void AddRailSegment(Geometry geo, float length, bool alongX)
    {
        if (alongX)
        {
            geo.AddBox(new Vector3(0f, 0.49f, 0f), new Vector3(length, 0.09f, 0.09f));
            geo.AddBox(new Vector3(0f, -0.08f, 0f), new Vector3(length, 0.055f, 0.055f));
            int count = Mathf.Max(2, Mathf.CeilToInt(length / 1.6f) + 1);
            for (int i = 0; i < count; i++)
                geo.AddBox(new Vector3(-length * 0.5f + length * i / (count - 1), 0f, 0f), new Vector3(0.075f, 1.1f, 0.075f));
        }
        else
        {
            geo.AddBox(new Vector3(0f, 0.49f, 0f), new Vector3(0.09f, 0.09f, length));
            geo.AddBox(new Vector3(0f, -0.08f, 0f), new Vector3(0.055f, 0.055f, length));
            int count = Mathf.Max(2, Mathf.CeilToInt(length / 1.6f) + 1);
            for (int i = 0; i < count; i++)
                geo.AddBox(new Vector3(0f, 0f, -length * 0.5f + length * i / (count - 1)), new Vector3(0.075f, 1.1f, 0.075f));
        }
    }

    private static void PrepareAssetFolders()
    {
        Directory.CreateDirectory(MeshPath);
        Directory.CreateDirectory(MaterialPath);
        string place = MaterialPath + "/M_BlockoutPlaceholder.mat";
        placeholder = AssetDatabase.LoadAssetAtPath<Material>(place);
        if (placeholder == null)
        {
            placeholder = new Material(Shader.Find("Standard")) { name = "M_BlockoutPlaceholder" };
            placeholder.color = new Color(0.7f, 0.74f, 0.77f);
            AssetDatabase.CreateAsset(placeholder, place);
        }
    }

    private static Transform Group(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject MeshObject(string name, Transform parent, Vector3 position, Geometry geo) =>
        MeshObject(name, parent, position, SaveMesh(name, geo));

    private static GameObject MeshObject(string name, Transform parent, Vector3 position, Mesh mesh)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = placeholder;
        return go;
    }

    private static Mesh SaveMesh(string name, Geometry geo)
    {
        string path = MeshPath + "/" + name + ".asset";
        if (File.Exists(path)) throw new Exception("Mesh asset already exists: " + path);
        Mesh mesh = geo.ToMesh(name);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save C3 art scene.");
        AssetDatabase.SaveAssets();
    }

    private sealed class Geometry
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public void AddBox(Vector3 center, Vector3 size)
        {
            Vector3 half = size * 0.5f;
            AddFace(center, new Vector3(half.x, -half.y, -half.z), new Vector3(half.x, -half.y, half.z),
                new Vector3(half.x, half.y, half.z), new Vector3(half.x, half.y, -half.z), Vector3.right, size.z, size.y);
            AddFace(center, new Vector3(-half.x, -half.y, half.z), new Vector3(-half.x, -half.y, -half.z),
                new Vector3(-half.x, half.y, -half.z), new Vector3(-half.x, half.y, half.z), Vector3.left, size.z, size.y);
            AddFace(center, new Vector3(-half.x, half.y, -half.z), new Vector3(half.x, half.y, -half.z),
                new Vector3(half.x, half.y, half.z), new Vector3(-half.x, half.y, half.z), Vector3.up, size.x, size.z);
            AddFace(center, new Vector3(-half.x, -half.y, half.z), new Vector3(half.x, -half.y, half.z),
                new Vector3(half.x, -half.y, -half.z), new Vector3(-half.x, -half.y, -half.z), Vector3.down, size.x, size.z);
            AddFace(center, new Vector3(half.x, -half.y, half.z), new Vector3(-half.x, -half.y, half.z),
                new Vector3(-half.x, half.y, half.z), new Vector3(half.x, half.y, half.z), Vector3.forward, size.x, size.y);
            AddFace(center, new Vector3(-half.x, -half.y, -half.z), new Vector3(half.x, -half.y, -half.z),
                new Vector3(half.x, half.y, -half.z), new Vector3(-half.x, half.y, -half.z), Vector3.back, size.x, size.y);
        }

        private void AddFace(Vector3 center, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float width, float height)
        {
            int start = vertices.Count;
            vertices.Add(center + a); vertices.Add(center + b); vertices.Add(center + c); vertices.Add(center + d);
            for (int i = 0; i < 4; i++) normals.Add(normal);
            uv.Add(Vector2.zero); uv.Add(new Vector2(width, 0f)); uv.Add(new Vector2(width, height)); uv.Add(new Vector2(0f, height));
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }

        public void AddTube(Vector3 start, Vector3 end, float radius, int sides)
        {
            Vector3 axis = (end - start).normalized;
            Vector3 tangent = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 bitangent = Vector3.Cross(axis, tangent).normalized;
            float length = Vector3.Distance(start, end);
            int first = vertices.Count;
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                Vector3 radial = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                vertices.Add(start + radial * radius);
                vertices.Add(end + radial * radius);
                normals.Add(radial); normals.Add(radial);
                uv.Add(new Vector2((float)i / sides, 0f)); uv.Add(new Vector2((float)i / sides, length));
                if (i == sides) continue;
                int index = first + i * 2;
                triangles.Add(index); triangles.Add(index + 3); triangles.Add(index + 1);
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
            }
        }

        public void AddCylinder(Vector3 center, float radius, float height, int sides = 16)
        {
            AddTube(center - Vector3.up * height * 0.5f, center + Vector3.up * height * 0.5f, radius, sides);
            AddDisc(center + Vector3.up * height * 0.5f, radius, sides, true);
            AddDisc(center - Vector3.up * height * 0.5f, radius, sides, false);
        }

        private void AddDisc(Vector3 center, float radius, int sides, bool top)
        {
            int first = vertices.Count;
            vertices.Add(center);
            normals.Add(top ? Vector3.up : Vector3.down);
            uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);
                vertices.Add(center + new Vector3(cosine * radius, 0f, sine * radius));
                normals.Add(top ? Vector3.up : Vector3.down);
                uv.Add(new Vector2(0.5f + cosine * 0.5f, 0.5f + sine * 0.5f));
                if (i == sides) continue;
                if (top) { triangles.Add(first); triangles.Add(first + i + 2); triangles.Add(first + i + 1); }
                else { triangles.Add(first); triangles.Add(first + i + 1); triangles.Add(first + i + 2); }
            }
        }

        public void AddBeam(Vector3 start, Vector3 end, float width, float depth)
        {
            Vector3 axis = (end - start).normalized;
            Vector3 sideways = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 other = Vector3.Cross(axis, sideways).normalized;
            Vector3 mid = (start + end) * 0.5f;
            float length = Vector3.Distance(start, end);
            int first = vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                Vector3 radial = sideways * (i == 0 || i == 3 ? -width * 0.5f : width * 0.5f) +
                    other * (i < 2 ? -depth * 0.5f : depth * 0.5f);
                vertices.Add(mid - axis * length * 0.5f + radial);
                vertices.Add(mid + axis * length * 0.5f + radial);
                normals.Add(radial.normalized); normals.Add(radial.normalized);
                uv.Add(new Vector2(i * 0.25f, 0f)); uv.Add(new Vector2(i * 0.25f, length));
            }
            for (int i = 0; i < 4; i++)
            {
                int a = first + i * 2;
                int b = first + ((i + 1) % 4) * 2;
                triangles.Add(a); triangles.Add(b + 1); triangles.Add(a + 1);
                triangles.Add(a); triangles.Add(b); triangles.Add(b + 1);
            }
        }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
