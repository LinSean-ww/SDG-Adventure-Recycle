using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using C3TreasurePuzzle;

public static class C3SceneBugFixTool
{
    private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    private static readonly string Evidence = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_BugFix_Evidence");
    private const string MatPath = "Assets/_Modules_/02_Levels/Level03/ArtFull/Materials/";

    public static void FixPipesAndSupports()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = Root(scene, "C_Factory_Art");
        Transform pipes = art.Find("Pipes");
        if (pipes.Find("Pipe_MountsAndConnections") != null) throw new Exception("Pipe repair already exists.");
        Material steel = Mat("M_DarkIndustrialSteel");
        Material rusty = Mat("M_RustedSteel");
        Transform mounts = NewGroup("Pipe_MountsAndConnections", pipes);

        Transform west = pipes.Find("Pipe_WestServiceTrunk");
        Capsule(west, "Collision_MainWestTrunk", new Vector3(-24.35f, 3.10f, 3.0f), new Vector3(-24.35f, 3.10f, 22.5f), 0.105f);
        Capsule(west, "Collision_WestCabinetA", new Vector3(-24.35f, 3.10f, 17.7f), new Vector3(-23.55f, 2.45f, 17.7f), 0.08f);
        Capsule(west, "Collision_WestCabinetB", new Vector3(-24.35f, 3.10f, 20.1f), new Vector3(-23.55f, 2.45f, 20.1f), 0.08f);
        foreach (float z in new[] { 4f, 9f, 14f, 18.6f, 22f })
            Beam(mounts, "WestWallHanger_" + z.ToString("0.0", CultureInfo.InvariantCulture),
                new Vector3(-24.93f, 3.10f, z), new Vector3(-24.35f, 3.10f, z), 0.12f, steel);
        Tube(mounts, "WestWallFeed", new Vector3(-24.98f, 3.10f, 3.0f), new Vector3(-24.35f, 3.10f, 3.0f), 0.105f, rusty, false);
        Tube(mounts, "WestNorthElbow", new Vector3(-24.35f, 3.10f, 22.5f), new Vector3(-24.35f, 3.18f, 24.2f), 0.105f, steel, true);
        Tube(mounts, "NorthWestJoin", new Vector3(-24.35f, 3.18f, 24.2f), new Vector3(-22.5f, 3.18f, 24.2f), 0.12f, steel, true);
        Sleeve(mounts, "WestWallFeedSleeve", new Vector3(-24.82f, 3.10f, 3.0f), Quaternion.Euler(0f, 0f, 90f), rusty);

        Transform north = pipes.Find("Pipe_NorthFilterFeed");
        Capsule(north, "Collision_NorthFilterMain", new Vector3(-22.5f, 3.18f, 24.2f), new Vector3(-3.2f, 3.18f, 24.2f), 0.12f);
        Capsule(north, "Collision_NorthFilterBranch", new Vector3(-5.0f, 3.18f, 24.2f), new Vector3(-5.0f, 2.95f, 21.0f), 0.095f);
        foreach (float x in new[] { -21f, -16f, -10.5f, -5f })
            Beam(mounts, "NorthWallHanger_" + x.ToString("0.0", CultureInfo.InvariantCulture),
                new Vector3(x, 3.18f, 24.2f), new Vector3(x, 3.18f, 24.93f), 0.13f, steel);
        Tube(mounts, "NorthWallFeed", new Vector3(-3.2f, 3.18f, 24.2f), new Vector3(-3.2f, 3.18f, 25.0f), 0.12f, steel, false);
        Sleeve(mounts, "NorthWallFeedSleeve", new Vector3(-3.2f, 3.18f, 24.80f), Quaternion.Euler(90f, 0f, 0f), rusty);

        Transform reactor = pipes.Find("Pipe_ReactorReturn");
        Capsule(reactor, "Collision_ReactorOut", new Vector3(-12.5f, 3.55f, 9.5f), new Vector3(-14.55f, 3.55f, 9.5f), 0.12f);
        Capsule(reactor, "Collision_ReactorReturnMain", new Vector3(-14.55f, 3.55f, 9.5f), new Vector3(-14.55f, 3.55f, 21.8f), 0.12f);
        Capsule(reactor, "Collision_ReactorReturnJoin", new Vector3(-14.55f, 3.55f, 21.8f), new Vector3(-14.55f, 3.18f, 24.2f), 0.10f);
        foreach (float z in new[] { 14.4f, 17f, 19.5f })
            Beam(mounts, "ReactorDeckHanger_" + z.ToString("0.0", CultureInfo.InvariantCulture),
                new Vector3(-14.55f, 3.55f, z), new Vector3(-14.55f, 4.30f, z), 0.12f, steel);

        Transform upper = pipes.Find("Pipe_UpperCableConduit");
        Capsule(upper, "Collision_UpperConduitA", new Vector3(-21.8f, 7.15f, 14.8f), new Vector3(-5.4f, 7.15f, 14.8f), 0.08f);
        Capsule(upper, "Collision_UpperConduitB", new Vector3(-21.8f, 7.15f, 16.0f), new Vector3(-5.4f, 7.15f, 16.0f), 0.06f);
        foreach (float x in new[] { -20f, -15f, -10f, -6f })
            foreach (float z in new[] { 14.8f, 16.0f })
                Beam(mounts, "CeilingCableHanger_" + x.ToString("0.0", CultureInfo.InvariantCulture) + "_" + z.ToString("0.0", CultureInfo.InvariantCulture),
                    new Vector3(x, 7.15f, z), new Vector3(x, 9.90f, z), 0.08f, steel);
        foreach (float z in new[] { 14.8f, 16.0f })
        {
            Tube(mounts, "WestUpperFeed_" + z.ToString("0.0", CultureInfo.InvariantCulture),
                new Vector3(-24.93f, 7.15f, z), new Vector3(-21.8f, 7.15f, z), 0.075f, steel, false);
            Tube(mounts, "EastUpperFeed_" + z.ToString("0.0", CultureInfo.InvariantCulture),
                new Vector3(-5.4f, 7.15f, z), new Vector3(-0.42f, 7.15f, z), 0.075f, steel, false);
        }

        Transform braces = art.Find("Architecture_Additions/Steel_MainDeckCrossBraces");
        if (braces == null || braces.childCount != 0) throw new Exception("Unexpected M2 cross brace structure.");
        foreach (float x in new[] { -21.5f, -17.5f, -10f, -5.5f })
        {
            Capsule(braces, "Collision_CrossBraceA_" + x.ToString("0.0", CultureInfo.InvariantCulture),
                new Vector3(x, 0.78f, 14.5f), new Vector3(x, 4.10f, 16.5f), 0.053f);
            Capsule(braces, "Collision_CrossBraceB_" + x.ToString("0.0", CultureInfo.InvariantCulture),
                new Vector3(x, 0.78f, 16.5f), new Vector3(x, 4.10f, 14.5f), 0.053f);
        }
        Save(scene);
        ValidatePipesAndSupports();
        Debug.Log("C3_BUGFIX_PIPES_PASS");
    }

    public static void ValidatePipesAndSupports()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = Root(scene, "C_Factory_Art");
        Transform pipes = art.Find("Pipes");
        int pipeColliders = pipes.GetComponentsInChildren<Collider>(true).Length;
        int braceColliders = art.Find("Architecture_Additions/Steel_MainDeckCrossBraces").GetComponentsInChildren<Collider>(true).Length;
        if (pipeColliders < 32 || braceColliders != 8 || pipes.GetComponentsInChildren<Collider>(true).Any(c => c.isTrigger || !c.enabled))
            throw new Exception("Pipe and support physical collision count invalid: " + pipeColliders + "/" + braceColliders);
        Physics.SyncTransforms();
        Vector3 ray = new Vector3(-23.8f, 3.10f, 11f);
        if (!Physics.Raycast(ray, Vector3.left, out RaycastHit hit, 1f, ~0, QueryTriggerInteraction.Ignore) ||
            hit.collider.name != "Collision_MainWestTrunk")
            throw new Exception("West water pipe remains pass-through; first hit=" + (hit.collider != null ? hit.collider.name : "NONE"));
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/StageA_Pipes.txt", "pipeAndMountColliders=" + pipeColliders +
            "\ncrossBraceColliders=" + braceColliders + "\nwestPipeRayFirst=" + hit.collider.name + "\n", new UTF8Encoding(false));
    }

    public static void FixIndustrialLamps()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform lighting = Root(scene, "C_Factory_Art").Find("Lighting");
        Material steel = Mat("M_DarkIndustrialSteel");
        Material rust = Mat("M_RustedSteel");
        string[] names = { "Warm_EntrySafety", "Warm_WestService", "Warm_CentralReactor", "Warm_EastService", "Warm_UpperWalkway" };
        foreach (string name in names)
        {
            Transform lamp = lighting.Find(name);
            if (lamp == null || lamp.Find("CeilingMount") != null) throw new Exception("Warm lamp missing or already mounted: " + name);
            float y = lamp.position.y;
            float top = 9.90f;
            Transform structure = NewGroup("CeilingMount", lamp);
            VisualBeam(structure, "SuspensionRod", new Vector3(lamp.position.x, y + 0.19f, lamp.position.z),
                new Vector3(lamp.position.x, top - 0.08f, lamp.position.z), 0.085f, steel);
            VisualBeam(structure, "CeilingCrossbar", new Vector3(lamp.position.x - 0.28f, top - 0.11f, lamp.position.z),
                new Vector3(lamp.position.x + 0.28f, top - 0.11f, lamp.position.z), 0.09f, steel);
            VisualCube(structure, "CeilingPlate", new Vector3(lamp.position.x, top - 0.065f, lamp.position.z),
                new Vector3(0.60f, 0.13f, 0.45f), steel);
            VisualCylinder(structure, "BellShade", new Vector3(lamp.position.x, y + 0.18f, lamp.position.z),
                new Vector3(0.57f, 0.13f, 0.57f), rust);
            VisualCylinder(structure, "LampSocket", new Vector3(lamp.position.x, y + 0.07f, lamp.position.z),
                new Vector3(0.36f, 0.09f, 0.36f), steel);
        }
        // The original ceiling proxy spans the chimney opening. Attach the two
        // central lamps and four inner conduit hangers to real roof-anchored beams.
        C3UpperSupportFixTool.ApplyInOpenScene(scene);
        Save(scene);
        ValidateIndustrialLamps();
        Debug.Log("C3_BUGFIX_LAMPS_PASS");
    }

    public static void ValidateIndustrialLamps()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform lighting = Root(scene, "C_Factory_Art").Find("Lighting");
        string[] names = { "Warm_EntrySafety", "Warm_WestService", "Warm_CentralReactor", "Warm_EastService", "Warm_UpperWalkway" };
        var output = new StringBuilder();
        foreach (string name in names)
        {
            Transform lamp = lighting.Find(name);
            Transform mount = lamp.Find("CeilingMount");
            if (mount == null || mount.childCount != 5 || mount.Find("SuspensionRod") == null || mount.Find("CeilingPlate") == null)
                throw new Exception("Warm lamp mount incomplete: " + name);
            Renderer plate = mount.Find("CeilingPlate").GetComponent<Renderer>();
            float expectedTop = name == "Warm_CentralReactor" || name == "Warm_UpperWalkway" ? 9.62f : 10.00f;
            if (Mathf.Abs(plate.bounds.max.y - expectedTop) > 0.015f)
                throw new Exception("Warm lamp not connected to roof or supported bridge: " + name);
            output.AppendLine(name + " bulb=" + V(lamp.position) + " ceilingPlateTop=" + plate.bounds.max.y.ToString("F3", CultureInfo.InvariantCulture));
        }
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/StageB_Lamps.txt", output.ToString(), new UTF8Encoding(false));
    }

    public static void FixWarningBoards()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = Root(scene, "C_Factory_Art");
        Material steel = Mat("M_DarkIndustrialSteel");
        Transform entry = art.Find("Props/Warning_EntryFaultWarning");
        Transform west = art.Find("Props/Warning_WestElectricalDanger");
        Transform upper = art.Find("Floor2_Industrial/Warning_UpperRestrictedMaintenance");
        if (entry.Find("WallFastenerLeft") != null || west.Find("WallFastenerLeft") != null || upper.Find("RailFastenerLeft") != null)
            throw new Exception("Warning boards already mounted.");
        entry.position = new Vector3(-20.9f, 2.65f, 0.50f);
        west.position = new Vector3(-24.82f, 2.55f, 17.8f);
        upper.position = new Vector3(-12.5f, 5.23f, 19.91f);
        foreach (float x in new[] { -22.0f, -19.8f })
            VisualBeam(entry, x < -21f ? "WallFastenerLeft" : "WallFastenerRight",
                new Vector3(x, 2.65f, 0.395f), new Vector3(x, 2.65f, 0.50f), 0.11f, steel);
        foreach (float z in new[] { 17.25f, 18.35f })
            VisualBeam(west, z < 17.8f ? "WallFastenerLeft" : "WallFastenerRight",
                new Vector3(-24.925f, 2.55f, z), new Vector3(-24.82f, 2.55f, z), 0.095f, steel);
        foreach (float x in new[] { -13.1f, -11.9f })
            VisualBeam(upper, x < -12.5f ? "RailFastenerLeft" : "RailFastenerRight",
                new Vector3(x, 5.00f, 19.98f), new Vector3(x, 5.45f, 19.91f), 0.07f, steel);
        Save(scene);
        ValidateWarningBoards();
        Debug.Log("C3_BUGFIX_WARNINGS_PASS");
    }

    public static void ValidateWarningBoards()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = Root(scene, "C_Factory_Art");
        Transform entry = art.Find("Props/Warning_EntryFaultWarning");
        Transform west = art.Find("Props/Warning_WestElectricalDanger");
        Transform upper = art.Find("Floor2_Industrial/Warning_UpperRestrictedMaintenance");
        if (Mathf.Abs(entry.position.z - 0.50f) > 0.01f || Mathf.Abs(west.position.x + 24.82f) > 0.01f ||
            Mathf.Abs(upper.position.z - 19.91f) > 0.01f || upper.position.y > 5.30f)
            throw new Exception("Warning board position invalid.");
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/StageC_Warnings.txt",
            "Entry=" + V(entry.position) + " (south wall)\nWest=" + V(west.position) +
            " (west wall)\nUpper=" + V(upper.position) + " (north upper guard)\n", new UTF8Encoding(false));
    }

    public static void FixConsoleAndGuides()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform level = Root(scene, "C_LevelMarkers");
        Transform area = level.Find("PuzzleConsoleArea");
        Transform console = area.Find("M4_PuzzleConsole");
        Transform guide = area.Find("M3_PlannedConsoleGuide");
        if (console == null || guide == null || Vector3.Distance(console.position, new Vector3(-0.9f, 1f, 18.95f)) > 0.002f ||
            Quaternion.Angle(console.rotation, Quaternion.identity) > 0.1f)
            throw new Exception("Console no longer matches measured baseline; refusing to overwrite.");
        if (console.GetComponent<C3PuzzleConsole>() == null || console.GetComponent<BoxCollider>() == null)
            throw new Exception("Console interaction component or collider missing.");
        if (guide.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 || guide.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new Exception("Guide contains gameplay or collision components; do not hide before review.");

        // Plan view (+Y) clockwise: the existing -Z front turns toward -X, into the C interior.
        console.position = new Vector3(-0.65f, 1.0f, 18.95f);
        console.rotation = Quaternion.Euler(0f, 90f, 0f);
        foreach (MeshRenderer renderer in guide.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = false;

        Transform lighting = Root(scene, "C_Factory_Art").Find("Lighting");
        Transform cyan = lighting.Find("Cyan_Console");
        if (cyan.Find("WallMount") != null) throw new Exception("Cyan console light already mounted.");
        cyan.position = new Vector3(-0.75f, 2.37f, 18.95f);
        Transform mount = NewGroup("WallMount", cyan);
        Material steel = Mat("M_DarkIndustrialSteel");
        VisualCube(mount, "WallPlate", new Vector3(-0.43f, 2.37f, 18.95f), new Vector3(0.11f, 0.35f, 0.48f), steel);
        VisualBeam(mount, "FixtureArm", new Vector3(-0.48f, 2.37f, 18.95f), new Vector3(-0.75f, 2.37f, 18.95f), 0.085f, steel);
        Save(scene);
        ValidateConsoleAndGuides();
        Debug.Log("C3_BUGFIX_CONSOLE_PASS");
    }

    public static void ValidateConsoleAndGuides()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform level = Root(scene, "C_LevelMarkers");
        Transform area = level.Find("PuzzleConsoleArea");
        Transform console = area.Find("M4_PuzzleConsole");
        Transform guide = area.Find("M3_PlannedConsoleGuide");
        Collider collider = console.GetComponent<Collider>();
        Collider wall = Root(scene, "C_CollisionProxies").Find("C_Wall_4_Far").GetComponent<Collider>();
        if (Vector3.Distance(console.position, new Vector3(-0.65f, 1f, 18.95f)) > 0.002f ||
            Vector3.Angle(console.TransformDirection(Vector3.back), Vector3.left) > 0.1f ||
            Mathf.Abs(collider.bounds.max.x - wall.bounds.min.x) > 0.02f ||
            console.GetComponent<C3PuzzleConsole>() == null)
            throw new Exception("Console orientation, wall mounting, or gameplay component invalid.");
        MeshRenderer[] guideRenderers = guide.GetComponentsInChildren<MeshRenderer>(true);
        if (guideRenderers.Length != 3 || guideRenderers.Any(r => r.enabled))
            throw new Exception("Pure visual cyan guide remains visible.");
        Transform cyan = Root(scene, "C_Factory_Art").Find("Lighting/Cyan_Console");
        if (cyan.Find("WallMount/FixtureArm") == null || Mathf.Abs(cyan.position.z - console.position.z) > 0.01f)
            throw new Exception("Cyan console lamp still floats or is misaligned.");
        Physics.SyncTransforms();
        Vector3 eye = new Vector3(-2.25f, 2.05f, 18.95f);
        Vector3 aim = new Vector3(-0.65f, 1.40f, 18.95f) - eye;
        if (!Physics.Raycast(eye, aim.normalized, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore) ||
            hit.collider != collider)
            throw new Exception("Console not first reachable 3-unit ray target; first=" + (hit.collider != null ? hit.collider.name : "NONE"));
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/StageD_Console.txt",
            "beforePos=(-0.900,1.000,18.950) beforeEuler=(0,0,0) beforeFront=-Z\n" +
            "afterPos=" + V(console.position) + " afterEuler=" + V(console.eulerAngles) + " afterFront=" + V(console.TransformDirection(Vector3.back)) +
            "\nconsoleCollider=" + V(collider.bounds.min) + ".." + V(collider.bounds.max) +
            "\neastWallMinX=" + wall.bounds.min.x.ToString("F3", CultureInfo.InvariantCulture) +
            "\nfirstRayHit=" + hit.collider.name + "\ncyanLamp=" + V(cyan.position) + " wallMounted=true\nguideRenderersDisabled=3\n",
            new UTF8Encoding(false));
    }

    public static void FixSpatialDetails()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = Root(scene, "C_Factory_Art");
        Material steel = Mat("M_DarkIndustrialSteel");
        Material rust = Mat("M_RustedSteel");
        Transform barrel = art.Find("Props/Barrel_East");
        if (Vector3.Distance(barrel.position, new Vector3(-7f, 0.55f, 22.8f)) > 0.002f)
            throw new Exception("East barrel was moved after the baseline probe.");
        barrel.position = new Vector3(-6.3f, 0.55f, 22.8f);

        Transform chipLamp = art.Find("Lighting/Cyan_ChipStation");
        if (chipLamp.Find("GuardMount") != null) throw new Exception("Chip station lamp already mounted.");
        Transform chipMount = NewGroup("GuardMount", chipLamp);
        VisualBeam(chipMount, "GuardPost", new Vector3(-12.6f, 5.10f, 19.96f),
            new Vector3(-12.6f, 6.35f, 19.96f), 0.075f, steel);
        VisualBeam(chipMount, "LightBoom", new Vector3(-12.6f, 6.35f, 19.96f),
            new Vector3(-12.6f, 6.35f, 19.20f), 0.075f, steel);
        VisualCube(chipMount, "LampHousing", new Vector3(-12.6f, 6.43f, 19.20f),
            new Vector3(0.40f, 0.16f, 0.37f), steel);

        Transform red = art.Find("Lighting/Red_EntryFault");
        if (red.Find("WallMount") != null) throw new Exception("Red fault lamp already mounted.");
        red.position = new Vector3(-21.0f, 3.40f, 0.63f);
        Transform redMount = NewGroup("WallMount", red);
        VisualCube(redMount, "WallPlate", new Vector3(-21.0f, 3.40f, 0.46f),
            new Vector3(0.35f, 0.35f, 0.12f), rust);
        VisualBeam(redMount, "FixtureArm", new Vector3(-21.0f, 3.40f, 0.51f),
            new Vector3(-21.0f, 3.40f, 0.63f), 0.075f, steel);
        Save(scene);
        ValidateSpatialDetails();
        Debug.Log("C3_BUGFIX_SPATIAL_DETAILS_PASS");
    }

    public static void ValidateSpatialDetails()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = Root(scene, "C_Factory_Art");
        Collider waste = art.Find("Props/WasteContainer_B").GetComponent<Collider>();
        Collider barrel = art.Find("Props/Barrel_East").GetComponent<Collider>();
        if (waste.bounds.Intersects(barrel.bounds)) throw new Exception("East barrel still intersects waste container B.");
        Transform chipLamp = art.Find("Lighting/Cyan_ChipStation");
        Transform red = art.Find("Lighting/Red_EntryFault");
        if (chipLamp.Find("GuardMount/GuardPost") == null || red.Find("WallMount/FixtureArm") == null ||
            Mathf.Abs(red.position.z - 0.63f) > 0.01f)
            throw new Exception("Remaining C3 accent lamps are not physically mounted.");
        var outside = art.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.enabled && (r.bounds.min.x < -25.08f || r.bounds.max.x > 0.08f ||
                r.bounds.min.z < -0.08f || r.bounds.max.z > 25.08f || r.bounds.min.y < 0.45f || r.bounds.max.y > 9.99f))
            .Select(r => PathOf(r.transform)).ToArray();
        if (outside.Length > 0) throw new Exception("Art renderer exceeds C shell envelope: " + string.Join(",", outside));
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/StageE_SpatialDetails.txt",
            "Barrel_East before=(-7.000,0.550,22.800) after=" + V(barrel.transform.position) +
            "\nWasteContainer_B overlap=false\nChipCyanLight guardMounted=true\nEntryRedLight wallMounted=true\nartRendererOutsideCShell=0\n",
            new UTF8Encoding(false));
    }

    public static void FixCollectibleVisuals()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Material ceramic = NewStandardMaterial("M_C3_CeramicFuse", new Color(0.78f, 0.76f, 0.67f), 0.05f, 0.18f);
        Material board = NewStandardMaterial("M_C3_ControlBoard", new Color(0.06f, 0.30f, 0.22f), 0.25f, 0.28f);
        Material brass = NewStandardMaterial("M_C3_BrassContacts", new Color(0.67f, 0.46f, 0.18f), 0.68f, 0.48f);
        Material filterMedia = NewStandardMaterial("M_C3_FilterMedia", new Color(0.42f, 0.59f, 0.61f), 0.04f, 0.10f);
        Material steel = Mat("M_DarkIndustrialSteel");
        Material black = Mat("M_OilyRubber");
        Material cyan = Mat("M_CyanInstrument");
        Material olive = Mat("M_WornOliveMachinePaint");

        C3CollectiblePart[] parts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<C3CollectiblePart>(true)).ToArray();
        if (parts.Length != 3) throw new Exception("Unexpected C3 part count.");
        foreach (C3CollectiblePart part in parts)
        {
            if (part.transform.Find("FactoryPartVisual") != null ||
                PrefabUtility.GetCorrespondingObjectFromSource(part.gameObject) == null)
                throw new Exception("Collectible is not the original prefab instance or was already styled: " + part.name);
            part.GetComponent<MeshRenderer>().enabled = false;
            Transform visual = CounterScaledVisualRoot(part.transform, "FactoryPartVisual");
            Vector3 p = part.transform.position;
            switch (part.PartId)
            {
                case "fuse":
                    VisualCylinder(visual, "CeramicFuseTube", p, new Vector3(0.18f, 0.22f, 0.18f), ceramic);
                    VisualCylinder(visual, "BrassFuseTop", p + Vector3.up * 0.28f, new Vector3(0.26f, 0.052f, 0.26f), brass);
                    VisualCylinder(visual, "BrassFuseBottom", p + Vector3.down * 0.28f, new Vector3(0.26f, 0.052f, 0.26f), brass);
                    VisualCylinder(visual, "SteelFuseNeckTop", p + Vector3.up * 0.20f, new Vector3(0.21f, 0.035f, 0.21f), steel);
                    VisualCylinder(visual, "SteelFuseNeckBottom", p + Vector3.down * 0.20f, new Vector3(0.21f, 0.035f, 0.21f), steel);
                    break;
                case "control_chip":
                    VisualCube(visual, "GreenCircuitBoard", p, new Vector3(0.60f, 0.050f, 0.44f), board);
                    VisualCube(visual, "BlackControllerChip", p + Vector3.up * 0.045f, new Vector3(0.19f, 0.030f, 0.17f), black);
                    for (int i = 0; i < 6; i++)
                    {
                        float x = -0.23f + i * 0.092f;
                        VisualCube(visual, "BrassContact_" + (i + 1).ToString("00"), p + new Vector3(x, 0.032f, -0.183f),
                            new Vector3(0.045f, 0.014f, 0.065f), brass);
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        float x = -0.17f + i * 0.17f;
                        VisualCube(visual, "CircuitTrace_" + (i + 1).ToString("00"), p + new Vector3(x, 0.029f, 0.13f),
                            new Vector3(0.014f, 0.007f, 0.14f), brass);
                    }
                    break;
                case "filter_module":
                    VisualCylinder(visual, "ReplaceableFilterCore", p, new Vector3(0.43f, 0.24f, 0.43f), filterMedia);
                    VisualCylinder(visual, "FilterSteelTop", p + Vector3.up * 0.25f, new Vector3(0.56f, 0.052f, 0.56f), steel);
                    VisualCylinder(visual, "FilterSteelBottom", p + Vector3.down * 0.25f, new Vector3(0.56f, 0.052f, 0.56f), steel);
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i * Mathf.PI * 2f / 10f;
                        GameObject fin = VisualCube(visual, "PleatedFilterRib_" + i.ToString("00"),
                            p + new Vector3(Mathf.Sin(a) * 0.23f, 0f, Mathf.Cos(a) * 0.23f),
                            new Vector3(0.05f, 0.38f, 0.075f), filterMedia);
                        fin.transform.rotation = Quaternion.Euler(0f, i * 36f, 0f);
                    }
                    VisualCylinder(visual, "FilterGrip", p + Vector3.up * 0.31f, new Vector3(0.23f, 0.030f, 0.23f), brass);
                    break;
                default: throw new Exception("Unknown collectible ID: " + part.PartId);
            }
        }

        Transform test = Root(scene, "M2_TestInteractable");
        if (test.GetComponent<C3TestInteractable>() == null || test.GetComponent<BoxCollider>() == null ||
            test.Find("FactoryTestTerminalVisual") != null)
            throw new Exception("Original test interactable structure changed.");
        test.GetComponent<MeshRenderer>().enabled = false;
        Transform terminal = CounterScaledVisualRoot(test, "FactoryTestTerminalVisual");
        Vector3 q = test.position;
        VisualCube(terminal, "DarkServicePillar", q, new Vector3(0.53f, 1.70f, 0.53f), steel);
        VisualCube(terminal, "OliveTerminalHead", q + Vector3.up * 0.53f, new Vector3(0.56f, 0.48f, 0.56f), olive);
        VisualCube(terminal, "CyanStatusScreen", q + new Vector3(-0.282f, 0.55f, 0f), new Vector3(0.016f, 0.25f, 0.30f), cyan);
        VisualCube(terminal, "BasePlate", q + Vector3.down * 0.83f, new Vector3(0.57f, 0.08f, 0.57f), brass);

        AssetDatabase.SaveAssets();
        Save(scene);
        ValidateCollectibleVisuals();
        Debug.Log("C3_BUGFIX_PART_VISUALS_PASS");
    }

    public static void ValidateCollectibleVisuals()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        C3CollectiblePart[] parts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<C3CollectiblePart>(true)).ToArray();
        if (parts.Length != 3 || parts.Select(p => p.PartId).Distinct().Count() != 3) throw new Exception("C3 part IDs changed.");
        var output = new StringBuilder();
        foreach (C3CollectiblePart part in parts)
        {
            Collider c = part.GetComponent<Collider>();
            Transform v = part.transform.Find("FactoryPartVisual");
            if (c == null || !c.enabled || v == null || part.GetComponent<MeshRenderer>().enabled ||
                v.GetComponentsInChildren<Collider>(true).Length != 0 ||
                PrefabUtility.GetCorrespondingObjectFromSource(part.gameObject) == null)
                throw new Exception("Collectible prefab, root collider, or non-colliding visual invalid: " + part.name);
            foreach (Renderer r in v.GetComponentsInChildren<Renderer>(true))
                if (!ContainsBounds(c.bounds, r.bounds, 0.032f))
                    throw new Exception("Collectible visual extends too far beyond interaction collider: " + part.name + "/" + r.name);
            output.AppendLine(part.name + " id=" + part.PartId + " world=" + V(part.transform.position) +
                " originalCollider=" + c.GetType().Name + " newVisualRenderers=" + v.GetComponentsInChildren<Renderer>(true).Length);
        }
        Transform test = Root(scene, "M2_TestInteractable");
        if (test.GetComponent<C3TestInteractable>() == null || test.GetComponent<BoxCollider>() == null ||
            test.Find("FactoryTestTerminalVisual") == null || test.GetComponent<MeshRenderer>().enabled)
            throw new Exception("Test interaction pillar was not preserved and restyled.");
        output.AppendLine("M2_TestInteractable=C3TestInteractable and BoxCollider retained; industrial terminal visible");
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/StageF_PartVisuals.txt", output.ToString(), new UTF8Encoding(false));
    }

    private static bool ContainsBounds(Bounds outer, Bounds inner, float tolerance) =>
        inner.min.x >= outer.min.x - tolerance && inner.max.x <= outer.max.x + tolerance &&
        inner.min.y >= outer.min.y - tolerance && inner.max.y <= outer.max.y + tolerance &&
        inner.min.z >= outer.min.z - tolerance && inner.max.z <= outer.max.z + tolerance;

    private static Transform CounterScaledVisualRoot(Transform parent, string name)
    {
        Transform t = NewGroup(name, parent);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        Vector3 s = parent.lossyScale;
        t.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
        return t;
    }

    private static Material NewStandardMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = MatPath + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Material material = new Material(Shader.Find("Standard")) { name = name };
        material.SetColor("_Color", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject VisualBeam(Transform parent, string name, Vector3 start, Vector3 end, float width, Material mat)
    {
        GameObject beam = Beam(parent, name, start, end, width, mat);
        UnityEngine.Object.DestroyImmediate(beam.GetComponent<Collider>());
        return beam;
    }
    private static GameObject VisualCube(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    private static GameObject VisualCylinder(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static Transform Root(Scene scene, string name) => scene.GetRootGameObjects().Single(r => r.name == name).transform;
    private static Transform NewGroup(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }
    private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(MatPath + name + ".mat") ??
        throw new Exception("Missing material: " + name);
    private static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save scene.");
    }
    private static CapsuleCollider Capsule(Transform parent, string name, Vector3 start, Vector3 end, float radius)
    {
        Transform t = NewGroup(name, parent);
        Vector3 delta = end - start;
        t.position = (start + end) * 0.5f;
        t.rotation = Quaternion.FromToRotation(Vector3.up, delta);
        CapsuleCollider c = t.gameObject.AddComponent<CapsuleCollider>();
        c.direction = 1;
        c.radius = radius;
        c.height = delta.magnitude + 2f * radius;
        return c;
    }
    private static GameObject Beam(Transform parent, string name, Vector3 start, Vector3 end, float width, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        Vector3 delta = end - start;
        go.transform.position = (start + end) * 0.5f;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta);
        go.transform.localScale = new Vector3(width, delta.magnitude, width);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }
    private static GameObject Tube(Transform parent, string name, Vector3 start, Vector3 end, float radius, Material mat, bool collision)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        Vector3 delta = end - start;
        go.transform.position = (start + end) * 0.5f;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta);
        go.transform.localScale = new Vector3(radius * 2f, delta.magnitude * 0.5f, radius * 2f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    private static void Sleeve(Transform parent, string name, Vector3 center, Quaternion rotation, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.rotation = rotation;
        go.transform.localScale = new Vector3(0.35f, 0.06f, 0.35f);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    public static void ProbeBefore()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory(Evidence);
        var rows = new StringBuilder();
        rows.AppendLine("Path\tPositionWorld\tRotationWorldEuler\tLossyScale\tRendererMin\tRendererMax\tColliderType\tColliderMin\tColliderMax\tComponents");
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "ONLYfactory") continue;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (root.name == "C_CollisionProxies" && t != root.transform && !t.name.StartsWith("C_")) continue;
                Renderer renderer = t.GetComponent<Renderer>();
                Collider collider = t.GetComponent<Collider>();
                rows.Append(PathOf(t)).Append('\t').Append(V(t.position)).Append('\t').Append(V(t.eulerAngles))
                    .Append('\t').Append(V(t.lossyScale)).Append('\t')
                    .Append(renderer != null ? V(renderer.bounds.min) : "-").Append('\t')
                    .Append(renderer != null ? V(renderer.bounds.max) : "-").Append('\t')
                    .Append(collider != null ? collider.GetType().Name : "-").Append('\t')
                    .Append(collider != null ? V(collider.bounds.min) : "-").Append('\t')
                    .Append(collider != null ? V(collider.bounds.max) : "-").Append('\t')
                    .Append(string.Join(",", t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)))
                    .AppendLine();
            }
        }
        File.WriteAllText(Evidence + "/Before_Transforms.tsv", rows.ToString(), new UTF8Encoding(false));

        var detail = new StringBuilder();
        Transform factory = scene.GetRootGameObjects().Single(r => r.name == "ONLYfactory").transform;
        Renderer main = factory.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "factory");
        MeshFilter filter = main.GetComponent<MeshFilter>();
        Mesh mesh = filter.sharedMesh;
        detail.AppendLine("Factory renderer world bounds=" + V(main.bounds.min) + ".." + V(main.bounds.max));
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            string mat = i < main.sharedMaterials.Length && main.sharedMaterials[i] != null ? main.sharedMaterials[i].name : "NULL";
            Bounds bounds = TransformBounds(main.localToWorldMatrix, mesh.GetSubMesh(i).bounds);
            detail.AppendLine("Submesh " + i + " material=" + mat + " bounds=" + V(bounds.min) + ".." + V(bounds.max));
        }
        foreach (string name in new[] { "M4_PuzzleConsole", "Cyan_Console", "Warm_EntrySafety", "Warm_WestService", "Warm_CentralReactor", "Warm_EastService", "Warm_UpperWalkway" })
        {
            Transform t = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(x => x.name == name);
            if (t == null) { detail.AppendLine(name + "=MISSING"); continue; }
            detail.AppendLine(name + " path=" + PathOf(t) + " pos=" + V(t.position) + " euler=" + V(t.eulerAngles) +
                " forward=" + V(t.forward) + " scale=" + V(t.lossyScale));
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                detail.AppendLine("  Renderer " + PathOf(r.transform) + " enabled=" + r.enabled + " bounds=" + V(r.bounds.min) + ".." + V(r.bounds.max) +
                    " material=" + string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "NULL")));
            foreach (Collider c in t.GetComponentsInChildren<Collider>(true))
                detail.AppendLine("  Collider " + PathOf(c.transform) + " " + c.GetType().Name + " enabled=" + c.enabled + " trigger=" + c.isTrigger +
                    " bounds=" + V(c.bounds.min) + ".." + V(c.bounds.max));
        }
        File.WriteAllText(Evidence + "/Before_Details.txt", detail.ToString(), new UTF8Encoding(false));
        Debug.Log("C3_BUGFIX_PROBE_BEFORE_PASS");
    }

    public static void ProbeCeilingMaterial()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Renderer renderer = Root(scene, "ONLYfactory").GetComponentsInChildren<Renderer>(true).Single(r => r.name == "factory");
        var output = new StringBuilder();
        foreach (int index in new[] { 0, 12, 13 })
        {
            Material material = renderer.sharedMaterials[index];
            output.Append("slot=").Append(index).Append(" name=").Append(material.name)
                .Append(" shader=").Append(material.shader.name)
                .Append(" color=").Append(material.HasProperty("_Color") ? material.GetColor("_Color").ToString() : "NONE")
                .Append(" emission=").Append(material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor").ToString() : "NONE")
                .Append(" asset=").Append(AssetDatabase.GetAssetPath(material)).AppendLine();
        }
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/Before_CeilingMaterial.txt", output.ToString(), new UTF8Encoding(false));
    }

    public static void FixCeilingChimneyMaterial()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform factory = Root(scene, "ONLYfactory");
        Renderer renderer = factory.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "factory");
        Material[] slots = renderer.sharedMaterials;
        if (slots.Length <= 13 || slots[13] == null || slots[13].name != "siteC_Big_Chimney")
            throw new Exception("C-area circular chimney material slot differs from measured baseline.");
        slots[13] = Mat("M_DarkIndustrialSteel");
        renderer.sharedMaterials = slots;
        Save(scene);
        ValidateCeilingChimneyMaterial();
        Debug.Log("C3_BUGFIX_CEILING_PASS");
    }

    public static void ValidateCeilingChimneyMaterial()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform factory = Root(scene, "ONLYfactory");
        Renderer renderer = factory.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "factory");
        if (factory.position != Vector3.zero || factory.rotation != Quaternion.identity || factory.localScale != Vector3.one ||
            renderer.sharedMaterials[13] != Mat("M_DarkIndustrialSteel") ||
            renderer.sharedMaterials[12] != Mat("M_AgedConcrete_Ceiling"))
            throw new Exception("Original FBX transform or C-area scene-only chimney override invalid.");
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/StageG_Ceiling.txt",
            "C circular chimney submesh=13; imported material=siteC_Big_Chimney; base color white, likely imported texture and warm-light tint\n" +
            "sceneInstanceSlot13=M_DarkIndustrialSteel\nsceneInstanceSlot12=M_AgedConcrete_Ceiling\nFBXRootTransform=identity\nsourceFBXAsset=unchanged\n",
            new UTF8Encoding(false));
    }

    public static void LocalizeFactorySigns()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = Root(scene, "C_Factory_Art");
        Transform decor = art.Find("Decorations");
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Modules_/06_ExternalAssets/Resources/Fonts/NotoSansCJKtc-Regular.otf");
        if (font == null) throw new Exception("Existing M6 Traditional Chinese font unavailable; do not use M7 font.");
        Material olive = Mat("M_WornOliveMachinePaint");
        Material steel = Mat("M_DarkIndustrialSteel");

        Transform electrical = decor.Find("Plate_ElectricalIsolation");
        Transform purifier = decor.Find("Plate_Purifier03");
        Transform log = decor.Find("Log_UpperBayChipTransfer");
        if (electrical.Find("SignBackplate") != null || purifier.Find("SignBackplate") != null || log.Find("SignBackplate") != null)
            throw new Exception("C3 sign mounting already exists.");
        SetSign(electrical, font, "高壓危險\n維修前請切斷電源", new Vector3(-24.70f, 2.55f, 17.8f),
            Quaternion.Euler(0f, -90f, 0f), 0.010f);
        SetSign(purifier, font, "03 號淨化設備\n設備停止運作", new Vector3(-12.5f, 2.85f, 8.28f),
            Quaternion.identity, 0.010f);
        SetSign(log, font, "維修紀錄 07\n控制晶片：二樓維修區\n淨化設備已離線", new Vector3(-12.5f, 5.86f, 19.77f),
            Quaternion.identity, 0.010f);

        VisualCube(purifier, "SignBackplate", new Vector3(-12.5f, 2.85f, 8.39f),
            new Vector3(1.95f, 0.71f, 0.095f), olive);
        VisualCube(log, "SignBackplate", new Vector3(-12.5f, 5.86f, 19.88f),
            new Vector3(2.35f, 0.91f, 0.08f), olive);
        foreach (float x in new[] { -13.45f, -11.55f })
            VisualBeam(log, x < -12.5f ? "GuardSignPostLeft" : "GuardSignPostRight",
                new Vector3(x, 5.05f, 19.97f), new Vector3(x, 5.60f, 19.88f), 0.07f, steel);
        // The existing west-wall warning panel is the backing for the electrical label.
        Save(scene);
        ValidateFactorySigns();
        Debug.Log("C3_BUGFIX_TRADITIONAL_CHINESE_PASS");
    }

    public static void ValidateFactorySigns()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform decor = Root(scene, "C_Factory_Art").Find("Decorations");
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Modules_/06_ExternalAssets/Resources/Fonts/NotoSansCJKtc-Regular.otf");
        string[] names = { "Plate_ElectricalIsolation", "Plate_Purifier03", "Log_UpperBayChipTransfer" };
        string[] lines = { "高壓危險", "03 號淨化設備", "維修紀錄 07" };
        var output = new StringBuilder();
        for (int i = 0; i < names.Length; i++)
        {
            Transform t = decor.Find(names[i]);
            TextMesh text = t.GetComponent<TextMesh>();
            if (text == null || text.font != font || !text.text.Contains(lines[i]) ||
                text.text.Any(c => c >= 'A' && c <= 'Z'))
                throw new Exception("Chinese sign or CJK font missing: " + names[i]);
            output.AppendLine(names[i] + " text=" + text.text.Replace('\n', '|') + " world=" + V(t.position) +
                " euler=" + V(t.eulerAngles) + " font=" + font.name);
        }
        if (decor.Find("Plate_Purifier03/SignBackplate") == null ||
            decor.Find("Log_UpperBayChipTransfer/SignBackplate") == null ||
            Vector3.Angle(decor.Find("Plate_ElectricalIsolation").TransformDirection(Vector3.back), Vector3.right) > 0.1f)
            throw new Exception("Sign backing or orientation invalid.");
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/StageH_ChineseSigns.txt", output.ToString() +
            "fontAsset=Assets/_Modules_/06_ExternalAssets/Resources/Fonts/NotoSansCJKtc-Regular.otf (existing M6 asset; OFL notice in Assets/StreamingAssets/FontLicenses)\n",
            new UTF8Encoding(false));
    }

    private static void SetSign(Transform t, Font font, string words, Vector3 position, Quaternion rotation, float size)
    {
        t.position = position;
        t.rotation = rotation;
        TextMesh text = t.GetComponent<TextMesh>();
        text.text = words;
        text.font = font;
        text.fontSize = 72;
        text.characterSize = size;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        t.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    public static void TuneFinalArtAndLighting()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = Root(scene, "C_Factory_Art");
        Transform decor = art.Find("Decorations");
        decor.Find("Log_UpperBayChipTransfer").GetComponent<TextMesh>().characterSize = 0.015f;
        decor.Find("Log_UpperBayChipTransfer/SignBackplate").localScale = new Vector3(2.05f, 0.78f, 0.08f);
        decor.Find("Plate_Purifier03").GetComponent<TextMesh>().characterSize = 0.015f;
        decor.Find("Plate_Purifier03/SignBackplate").localScale = new Vector3(1.48f, 0.55f, 0.095f);
        decor.Find("Plate_ElectricalIsolation").GetComponent<TextMesh>().characterSize = 0.015f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.34f, 0.36f, 0.38f);
        Light sun = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true))
            .Single(l => l.name == "Directional Light");
        sun.intensity = 0.36f;
        sun.shadows = LightShadows.None;

        Transform lighting = art.Find("Lighting");
        if (lighting.Find("Warm_WestPipeInspection") != null) throw new Exception("Inspection light already exists.");
        Transform lamp = NewGroup("Warm_WestPipeInspection", lighting);
        lamp.position = new Vector3(-24.48f, 4.15f, 11f);
        Light light = lamp.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.66f, 0.38f);
        light.intensity = 0.72f;
        light.range = 6.2f;
        light.shadows = LightShadows.None;
        Material steel = Mat("M_DarkIndustrialSteel");
        VisualCube(lamp, "WallPlate", new Vector3(-24.86f, 4.15f, 11f), new Vector3(0.12f, 0.42f, 0.48f), steel);
        VisualBeam(lamp, "InspectionArm", new Vector3(-24.80f, 4.15f, 11f),
            new Vector3(-24.48f, 4.15f, 11f), 0.09f, steel);
        VisualCylinder(lamp, "LampHousing", new Vector3(-24.48f, 4.25f, 11f),
            new Vector3(0.41f, 0.12f, 0.41f), steel);
        VisualCylinder(lamp, "LampLens", new Vector3(-24.48f, 4.09f, 11f),
            new Vector3(0.29f, 0.035f, 0.29f), Mat("M_WarmIndustrialLamp"));
        Save(scene);
        ValidateFinalArtAndLighting();
        Debug.Log("C3_BUGFIX_FINAL_ART_LIGHTING_PASS");
    }

    public static void ValidateFinalArtAndLighting()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform art = Root(scene, "C_Factory_Art");
        Transform decor = art.Find("Decorations");
        Transform lamp = art.Find("Lighting/Warm_WestPipeInspection");
        Light light = lamp != null ? lamp.GetComponent<Light>() : null;
        if (light == null || light.shadows != LightShadows.None || lamp.Find("InspectionArm") == null ||
            Mathf.Abs(decor.Find("Log_UpperBayChipTransfer").GetComponent<TextMesh>().characterSize - 0.015f) > 0.0001f ||
            RenderSettings.ambientLight.grayscale < 0.32f || RenderSettings.ambientLight.grayscale > 0.39f)
            throw new Exception("Final sign readability or work-light setting invalid.");
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/StageI_ArtLighting.txt",
            "ambient=" + RenderSettings.ambientLight + "\ndirectionalIntensity=0.36\nWestPipeInspection=" + V(lamp.position) +
            " pointLightIntensity=" + light.intensity.ToString("F2", CultureInfo.InvariantCulture) +
            " noRealtimeShadows=true wallMounted=true\nTraditionalChineseSignCharacterSize=0.015\n",
            new UTF8Encoding(false));
    }

    public static void FinalStaticAudit()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory(Evidence);
        Transform factory = Root(scene, "ONLYfactory");
        Transform art = Root(scene, "C_Factory_Art");
        Transform blockout = Root(scene, "C_TwoFloor_Blockout");
        Transform player = Root(scene, "M2_Player");
        if (factory.position != Vector3.zero || factory.rotation != Quaternion.identity || factory.localScale != Vector3.one ||
            player.GetComponent<C3FirstPersonController>() == null || player.GetComponent<C3PlayerInteractor>() == null ||
            scene.GetRootGameObjects().Any(r => r.name == "SharedPlayer") ||
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Any(c => c.GetType().FullName.Contains("SharedPlayer")))
            throw new Exception("Original FBX or C3-only player ownership changed.");
        C3CollectiblePart[] parts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<C3CollectiblePart>(true)).ToArray();
        if (parts.Length != 3 || parts.Select(p => p.PartId).Distinct().Count() != 3 ||
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<C3PuzzleConsole>(true)).Count() != 1 ||
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<C3DeviceState>(true)).Count() != 1 ||
            blockout.GetComponentsInChildren<BoxCollider>(true).Length != 157)
            throw new Exception("Original C3 gameplay, parts, or M2 structure count changed.");
        Collider[] artColliders = art.GetComponentsInChildren<Collider>(true);
        var output = new StringBuilder();
        output.AppendLine("Unity=" + Application.unityVersion + " scene=" + ScenePath);
        output.AppendLine("originalFactoryRoot=identity; M2Player=C3FirstPersonController+C3PlayerInteractor; sharedPlayerComponents=0");
        output.AppendLine("parts=3 puzzleConsoles=1 devices=1 m2BoxColliders=157 artColliders=" + artColliders.Length);
        foreach (C3CollectiblePart p in parts) output.AppendLine("PART " + p.name + " id=" + p.PartId + " pos=" + V(p.transform.position));
        Collider[] equipment = new[] { "Floor1_Industrial", "Floor2_Industrial", "IndustrialMachines", "Props" }
            .SelectMany(group => art.Find(group).Cast<Transform>())
            .Select(t => t.GetComponent<Collider>()).Where(c => c != null).ToArray();
        int equipmentPairs = 0;
        for (int i = 0; i < equipment.Length; i++)
            for (int j = i + 1; j < equipment.Length; j++)
                if (equipment[i].bounds.Intersects(equipment[j].bounds))
                {
                    equipmentPairs++;
                    output.AppendLine("EQUIPMENT_AABB " + equipment[i].name + " vs " + equipment[j].name);
                }
        output.AppendLine("equipmentColliderOverlaps=" + equipmentPairs);
        if (equipmentPairs > 0) throw new Exception("Separate equipment colliders overlap; inspect scene placement.");

        Renderer[] visible = art.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
        string[] outside = visible.Where(r => r.bounds.min.x < -25.08f || r.bounds.max.x > 0.08f ||
            r.bounds.min.z < -0.08f || r.bounds.max.z > 25.08f || r.bounds.min.y < 0.45f ||
            r.bounds.max.y > ((r.name == "RoofAnchor_-20" || r.name == "RoofAnchor_-6" ||
                r.name == "CeilingPlate") ? 10.015f : 9.99f))
            .Select(r => PathOf(r.transform)).ToArray();
        output.AppendLine("VISIBLE_ART_BOUNDS_OUTSIDE_C_SHELL=" + outside.Length);
        foreach (string path in outside) output.AppendLine("  OUTSIDE " + path);
        if (outside.Length > 0) throw new Exception("Art leaves C envelope: " + string.Join(",", outside));

        Collider[] originalWalls = Root(scene, "C_CollisionProxies").GetComponentsInChildren<Collider>(true)
            .Where(c => c.name.StartsWith("C_Wall_")).ToArray();
        int intentionalMountOverlaps = 0, otherWallOverlaps = 0;
        foreach (Collider c in artColliders)
            foreach (Collider wall in originalWalls)
                if (c.bounds.Intersects(wall.bounds))
                {
                    bool anchor = c.name.Contains("WallHanger") || c.name.Contains("WallFeed") ||
                        c.name.Contains("WallPlate") || c.name.Contains("NorthWall") || c.name.Contains("WestWall");
                    if (anchor) intentionalMountOverlaps++;
                    else { otherWallOverlaps++; output.AppendLine("UNEXPECTED_WALL_AABB " + PathOf(c.transform) + " vs " + wall.name); }
                }
        output.AppendLine("wallProxyAABB intentionalMounts=" + intentionalMountOverlaps + " other=" + otherWallOverlaps);
        if (otherWallOverlaps > 0) throw new Exception("Art collider unexpectedly intersects original wall proxy.");

        MeshFilter factoryFilter = factory.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "factory");
        GameObject temporary = new GameObject("C3_BugFix_TemporaryFactoryMeshCollisionProbe");
        int modelHits = 0;
        try
        {
            temporary.transform.SetPositionAndRotation(factoryFilter.transform.position, factoryFilter.transform.rotation);
            temporary.transform.localScale = factoryFilter.transform.lossyScale;
            MeshCollider model = temporary.AddComponent<MeshCollider>();
            model.sharedMesh = factoryFilter.sharedMesh;
            if (model.sharedMesh == null) throw new Exception("FBX collision probe mesh failed to cook.");
            Physics.SyncTransforms();
            foreach (Collider c in artColliders)
            {
                Bounds b = c.bounds;
                if (Physics.OverlapBox(b.center, b.extents * 0.90f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Contains(model))
                {
                    modelHits++;
                    output.AppendLine("FBX_AABB_OVERLAP " + PathOf(c.transform));
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(temporary); }
        output.AppendLine("factoryMeshAABBOverlap=" + modelHits + " (includes intended fixture anchor intersections if any)");
        if (modelHits > 0) throw new Exception("Added collider overlaps source FBX; inspect Final_StaticAudit.txt.");

        CheckTargetRay(output, "Fuse", new Vector3(-20f, 2.05f, 20.3f), new Vector3(-20f, 0.98f, 22f), "M3_Fuse");
        CheckTargetRay(output, "Chip", new Vector3(-12.5f, 6f, 16.7f), new Vector3(-12.5f, 4.9f, 18.5f), "M3_ControlChip");
        CheckTargetRay(output, "Filter", new Vector3(-4f, 2.05f, 16.3f), new Vector3(-4f, 1.03f, 18f), "M3_FilterModule");
        CheckTargetRay(output, "Console", new Vector3(-2.25f, 2.05f, 18.95f), new Vector3(-0.65f, 1.4f, 18.95f), "M4_PuzzleConsole");
        output.AppendLine("RESULT=PASS");
        File.WriteAllText(Evidence + "/Final_StaticAudit.txt", output.ToString(), new UTF8Encoding(false));
        WriteAfterTransformSnapshot(scene);
        Debug.Log("C3_BUGFIX_FINAL_STATIC_AUDIT_PASS");
    }

    private static void CheckTargetRay(StringBuilder output, string label, Vector3 eye, Vector3 target, string expected)
    {
        Vector3 aim = target - eye;
        bool hit = Physics.Raycast(eye, aim.normalized, out RaycastHit result, 3f, ~0, QueryTriggerInteraction.Ignore);
        string first = hit ? result.collider.name : "NONE";
        output.AppendLine("RAY " + label + " first=" + first);
        if (first != expected) throw new Exception("Necessary interaction blocked: " + label + " first=" + first);
    }

    private static void WriteAfterTransformSnapshot(Scene scene)
    {
        var rows = new StringBuilder();
        rows.AppendLine("Path\tPositionWorld\tRotationWorldEuler\tLossyScale\tRendererMin\tRendererMax\tColliderType\tColliderMin\tColliderMax\tComponents");
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "ONLYfactory") continue;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                Renderer r = t.GetComponent<Renderer>();
                Collider c = t.GetComponent<Collider>();
                rows.Append(PathOf(t)).Append('\t').Append(V(t.position)).Append('\t').Append(V(t.eulerAngles))
                    .Append('\t').Append(V(t.lossyScale)).Append('\t')
                    .Append(r != null ? V(r.bounds.min) : "-").Append('\t')
                    .Append(r != null ? V(r.bounds.max) : "-").Append('\t')
                    .Append(c != null ? c.GetType().Name : "-").Append('\t')
                    .Append(c != null ? V(c.bounds.min) : "-").Append('\t')
                    .Append(c != null ? V(c.bounds.max) : "-").Append('\t')
                    .Append(string.Join(",", t.GetComponents<Component>().Where(x => x != null).Select(x => x.GetType().Name)))
                    .AppendLine();
            }
        }
        File.WriteAllText(Evidence + "/After_Transforms.tsv", rows.ToString(), new UTF8Encoding(false));
    }

    private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
    {
        Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
        Vector3 ext = bounds.extents;
        Vector3 axisX = matrix.MultiplyVector(new Vector3(ext.x, 0, 0));
        Vector3 axisY = matrix.MultiplyVector(new Vector3(0, ext.y, 0));
        Vector3 axisZ = matrix.MultiplyVector(new Vector3(0, 0, ext.z));
        Vector3 worldExt = new Vector3(Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
        return new Bounds(center, worldExt * 2f);
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    private static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:F3},{1:F3},{2:F3})", v.x, v.y, v.z);
}
