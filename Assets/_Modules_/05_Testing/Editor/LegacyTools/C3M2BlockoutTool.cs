using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using C3TreasurePuzzle;

// Editor-only survey and construction tool. It does not alter C3 gameplay scripts.
public static class C3M2BlockoutTool
{
    private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    private static readonly string ProbePath = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_M2_Bounds_Probe.txt");
    private static readonly string CandidatePath = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_M2_Candidate_Space_Check.txt");
    private static readonly string BackupPath = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_M2_Backups/C_Main_PreBlockout_20261026.unity.bak");
    private static readonly string ValidationPath = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_M2_PostBuild_Validation.txt");
    private static readonly string ObjectTablePath = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_M2_NewObjects.csv");
    private const float FloorTop = 0.55f;
    private const float DeckTop = 4.5f;
    private const int StairCount = 22;
    private const float StairTread = 0.36f;

    public static void Build()
    {
        if (!File.Exists(BackupPath)) throw new Exception("Scene backup is missing: " + BackupPath);
        CheckCandidateSpace();
        string check = File.ReadAllText(CandidatePath);
        if (!check.Contains("PositiveControl_FBXFloor") || !check.Contains("PositiveControl_FBXWestWall") ||
            check.Split('\n').Where(line => line.Contains("PositiveControl_")).Any(line => !line.Contains("factoryMeshHit=True")) ||
            check.Split('\n').Where(line => line.StartsWith("CANDIDATE ") && !line.Contains("PositiveControl_")).Any(line => !line.Contains("factoryMeshHit=False") || !line.EndsWith("otherHits=\r")))
            throw new Exception("Candidate space check did not pass both positive controls and all five empty volumes.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (scene.GetRootGameObjects().Any(root => root.name == "C_TwoFloor_Blockout"))
            throw new Exception("Blockout root already exists; refusing to duplicate it.");

        Transform root = Group("C_TwoFloor_Blockout", null);
        Transform floor1 = Group("Floor1_Additions", root);
        Transform floor2 = Group("Floor2_Platforms", root);
        Transform stairs = Group("Stairs", root);
        Transform railings = Group("Railings", root);
        Transform supports = Group("Supports", root);
        Transform future = Group("FutureGameplayMarkers", root);

        Marker("F1_EntryStart_Zone", floor1, -21f, FloorTop, 4f);
        Marker("F1_WestMaintenance_Zone", floor1, -20f, FloorTop, 19f);
        Marker("F1_CentralOperations_Zone", floor1, -12.5f, FloorTop, 10f);
        Marker("F1_NortheastFilter_Zone", floor1, -4f, FloorTop, 18f);
        Marker("F1_Console_Zone", floor1, -0.9f, FloorTop, 18.95f);
        Marker("F1_WestStairAccess", floor1, -21.1f, FloorTop, 5.5f);
        Marker("F1_EastStairLanding", floor1, -5.9f, FloorTop, 5.5f);

        Block("F2_MainWalkway", floor2, -13.5f, 4.4f, 15.4f, 17f, 0.2f, 3.2f);
        Block("F2_ControlChipBay", floor2, -12.5f, 4.4f, 18.5f, 3f, 0.2f, 3f);
        Block("F2_ObservationDeck", floor2, -7f, 4.4f, 18.5f, 4f, 0.2f, 3f);

        BuildStair(stairs, railings, "West_Up", -21.1f);
        BuildStair(stairs, railings, "East_Down", -5.9f);

        // Deck edge barriers, with two 1.8 m openings aligned to the stairs.
        RailX(railings, "R_MainSouth", -13.5f, 13.8f, 13.4f);
        RailX(railings, "R_NorthWest", -18f, 17f, 8f);
        RailX(railings, "R_NorthBetweenBays", -10f, 17f, 2f);
        RailZ(railings, "R_WestEnd", -22f, 15.5f, 3f);
        RailZ(railings, "R_EastEnd", -5f, 17f, 6f);
        RailZ(railings, "R_ChipBayWest", -14f, 18.5f, 3f);
        RailZ(railings, "R_ChipBayEast", -11f, 18.5f, 3f);
        RailZ(railings, "R_ObservationWest", -9f, 18.5f, 3f);
        RailX(railings, "R_ChipBayNorth", -12.5f, 20f, 3f);
        RailX(railings, "R_ObservationNorth", -7f, 20f, 4f);

        foreach (float x in new[] { -21.5f, -17.5f, -10f, -5.5f })
            foreach (float z in new[] { 14.5f, 16.5f })
                Block("S_Main_" + Coord(x) + "_" + Coord(z), supports, x, 2.425f, z, 0.25f, 3.75f, 0.25f);
        foreach (float x in new[] { -13.5f, -11.5f, -8.5f, -5.5f })
            Block("S_Bay_" + Coord(x), supports, x, 2.425f, 19.5f, 0.25f, 3.75f, 0.25f);

        Marker("Future_M3_ControlChip_2F_Only", future, -12.5f, 4.9f, 18.5f);
        Marker("F2_ObservationPoint", future, -7f, DeckTop, 18.5f);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Failed to save C_Main blockout scene.");
        Debug.Log("C3_M2_BUILD_PASS scene=" + ScenePath + " addedRoot=" + root.name);
    }

    public static void Validate()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        GameObject blockout = roots.Single(item => item.name == "C_TwoFloor_Blockout");
        GameObject factory = roots.Single(item => item.name == "ONLYfactory");
        var output = new StringBuilder();
        var failures = new System.Collections.Generic.List<string>();
        output.AppendLine("C3_M2_POSTBUILD_VALIDATION Unity=" + Application.unityVersion + " Scene=" + scene.path);
        var table = new StringBuilder("HierarchyPath,Kind,WorldX,WorldY,WorldZ,WorldSizeX,WorldSizeY,WorldSizeZ\r\n");
        foreach (Transform item in blockout.GetComponentsInChildren<Transform>(true))
        {
            Collider collider = item.GetComponent<Collider>();
            Vector3 position = item.position;
            Vector3 size = collider != null ? collider.bounds.size : Vector3.zero;
            table.Append(PathOf(item)).Append(',').Append(collider == null ? "GroupOrMarker" : "SolidBox");
            foreach (float value in new[] { position.x, position.y, position.z, size.x, size.y, size.z })
                table.Append(',').Append(value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
            table.Append("\r\n");
        }
        File.WriteAllText(ObjectTablePath, table.ToString(), new UTF8Encoding(false));
        string[] groups = { "Floor1_Additions", "Floor2_Platforms", "Stairs", "Railings", "Supports", "FutureGameplayMarkers" };
        foreach (string group in groups)
            if (blockout.transform.Find(group) == null) failures.Add("Missing hierarchy group: " + group);

        Collider[] additions = blockout.GetComponentsInChildren<Collider>(true);
        output.AppendLine("ADDED_COLLLIDERS=" + additions.Length + " BOX=" + additions.OfType<BoxCollider>().Count());
        if (additions.Length == 0 || additions.Any(item => !(item is BoxCollider) || !item.enabled || item.isTrigger))
            failures.Add("Blockout lacks enabled, solid BoxColliders.");

        C3CollectiblePart[] parts = roots.SelectMany(item => item.GetComponentsInChildren<C3CollectiblePart>(true)).ToArray();
        C3PuzzleConsole[] consoles = roots.SelectMany(item => item.GetComponentsInChildren<C3PuzzleConsole>(true)).ToArray();
        C3TreasureProgress[] progress = roots.SelectMany(item => item.GetComponentsInChildren<C3TreasureProgress>(true)).ToArray();
        C3PuzzleState[] puzzles = roots.SelectMany(item => item.GetComponentsInChildren<C3PuzzleState>(true)).ToArray();
        C3DeviceState[] devices = roots.SelectMany(item => item.GetComponentsInChildren<C3DeviceState>(true)).ToArray();
        CharacterController[] players = roots.SelectMany(item => item.GetComponentsInChildren<CharacterController>(true)).ToArray();
        output.AppendLine("GAMEPLAY_COUNTS parts=" + parts.Length + " consoles=" + consoles.Length + " progress=" + progress.Length +
            " puzzles=" + puzzles.Length + " devices=" + devices.Length + " players=" + players.Length);
        if (parts.Length != 3 || consoles.Length != 1 || progress.Length != 1 || puzzles.Length != 1 || devices.Length != 1 || players.Length != 1)
            failures.Add("Gameplay object count changed.");
        if (parts.Length == 3)
        {
            var expected = new System.Collections.Generic.Dictionary<string, Vector3>
            {
                { "fuse", new Vector3(-20f, 0.98f, 22f) },
                { "control_chip", new Vector3(-12.5f, 0.95f, 14f) },
                { "filter_module", new Vector3(-4f, 1.03f, 18f) }
            };
            foreach (C3CollectiblePart part in parts)
            {
                output.AppendLine("PART " + part.PartId + " position=" + Vec(part.transform.position));
                if (!expected.ContainsKey(part.PartId) || Vector3.Distance(expected[part.PartId], part.transform.position) > 0.001f)
                    failures.Add("Part ID or position changed: " + part.name);
            }
        }
        if (consoles.Length == 1)
        {
            output.AppendLine("CONSOLE position=" + Vec(consoles[0].transform.position));
            if (Vector3.Distance(consoles[0].transform.position, new Vector3(-0.9f, 1f, 18.95f)) > 0.001f)
                failures.Add("Console moved.");
            SerializedObject serialized = new SerializedObject(consoles[0]);
            foreach (string property in new[] { "progress", "puzzle", "device" })
                if (serialized.FindProperty(property).objectReferenceValue == null) failures.Add("Console reference missing: " + property);
        }
        if (players.Length == 1)
        {
            output.AppendLine("PLAYER position=" + Vec(players[0].transform.position) + " height=" + players[0].height +
                " radius=" + players[0].radius + " stepOffset=" + players[0].stepOffset);
            if (Vector3.Distance(players[0].transform.position, new Vector3(-21f, 0.58f, 4f)) > 0.001f)
                failures.Add("Player spawn moved.");
        }

        Transform main = blockout.transform.Find("Floor2_Platforms/F2_MainWalkway");
        Transform westFirst = blockout.transform.Find("Stairs/Stair_West_Up/Tread_01");
        Transform westLast = blockout.transform.Find("Stairs/Stair_West_Up/Tread_22");
        Transform eastFirst = blockout.transform.Find("Stairs/Stair_East_Down/Tread_01");
        Transform eastLast = blockout.transform.Find("Stairs/Stair_East_Down/Tread_22");
        if (main == null || westFirst == null || westLast == null || eastFirst == null || eastLast == null)
            failures.Add("Main deck or stair endpoint missing.");
        else
        {
            Bounds deck = main.GetComponent<Collider>().bounds;
            output.AppendLine("CONNECTIVITY deck=" + BoundsOf(deck) + " risePerTread=" +
                ((DeckTop - FloorTop) / StairCount).ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                " floorToDeck=" + (DeckTop - FloorTop) + " ceilingStandingClearance=" + (9.9f - DeckTop));
            foreach (Transform end in new[] { westFirst, westLast, eastFirst, eastLast })
                output.AppendLine("STAIR_ENDPOINT " + PathOf(end) + " " + BoundsOf(end.GetComponent<Collider>().bounds));
            if ((DeckTop - FloorTop) / StairCount > players[0].stepOffset ||
                Mathf.Abs(westFirst.GetComponent<Collider>().bounds.min.y - FloorTop) > 0.02f ||
                Mathf.Abs(westLast.GetComponent<Collider>().bounds.max.y - DeckTop) > 0.02f ||
                westLast.GetComponent<Collider>().bounds.max.z < deck.min.z)
                failures.Add("Stair and deck numeric connection check failed.");
        }

        MeshFilter filter = factory.GetComponentsInChildren<MeshFilter>(true).Single(item => item.name == "factory");
        GameObject temporary = new GameObject("C3_M2_TemporaryFactoryCollisionProbe");
        try
        {
            temporary.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
            temporary.transform.localScale = filter.transform.lossyScale;
            MeshCollider modelCollider = temporary.AddComponent<MeshCollider>();
            modelCollider.sharedMesh = filter.sharedMesh;
            Physics.SyncTransforms();
            int modelHits = 0;
            int structureHits = 0;
            Collider[] originalStructure = roots.Where(item => item != blockout && item != factory)
                .SelectMany(item => item.GetComponentsInChildren<Collider>(true))
                .Where(item => item.name.StartsWith("C_"))
                .ToArray();
            foreach (Collider added in additions)
            {
                Bounds box = added.bounds;
                Collider[] hits = Physics.OverlapBox(box.center, box.extents * 0.98f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (hits.Contains(modelCollider))
                {
                    modelHits++;
                    output.AppendLine("MODEL_INTERSECTION " + PathOf(added.transform));
                }
                foreach (Collider structure in originalStructure)
                    if (hits.Contains(structure) && structure.name != "C_Floor")
                    {
                        structureHits++;
                        output.AppendLine("STRUCTURE_INTERSECTION " + PathOf(added.transform) + " with " + PathOf(structure.transform));
                    }
            }
            output.AppendLine("INTERSECTIONS factoryMesh=" + modelHits + " originalWallsCeiling=" + structureHits);
            if (modelHits != 0 || structureHits != 0) failures.Add("New colliders intersect original FBX or boundary colliders.");
        }
        finally { UnityEngine.Object.DestroyImmediate(temporary); }

        CheckRay(output, failures, "Fuse", new Vector3(-20f, 2.05f, 20.3f), new Vector3(-20f, 0.98f, 22f), "M3_Fuse");
        CheckRay(output, failures, "ControlChip", new Vector3(-12.5f, 2.05f, 12.2f), new Vector3(-12.5f, 0.95f, 14f), "M3_ControlChip");
        CheckRay(output, failures, "FilterModule", new Vector3(-4f, 2.05f, 16.3f), new Vector3(-4f, 1.03f, 18f), "M3_FilterModule");
        CheckRay(output, failures, "Console", new Vector3(-2.5f, 2.05f, 18.95f), new Vector3(-0.9f, 1.3f, 18.95f), "M4_PuzzleConsole");

        output.AppendLine("RESULT=" + (failures.Count == 0 ? "STATIC_PASS" : "STATIC_FAIL"));
        foreach (string failure in failures) output.AppendLine("FAILURE " + failure);
        File.WriteAllText(ValidationPath, output.ToString(), new UTF8Encoding(false));
        if (failures.Count != 0) throw new Exception("C3 M2 static validation failed. See " + ValidationPath);
        Debug.Log("C3_M2_STATIC_VALIDATION_PASS output=" + ValidationPath);
    }

    private static void CheckRay(StringBuilder output, System.Collections.Generic.List<string> failures,
        string label, Vector3 eye, Vector3 target, string expected)
    {
        Vector3 line = target - eye;
        bool hitSomething = Physics.Raycast(eye, line.normalized, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore);
        string actual = hitSomething ? hit.collider.name : "NONE";
        output.AppendLine("INTERACTION_RAY " + label + " from=" + Vec(eye) + " to=" + Vec(target) + " firstHit=" + actual);
        if (actual != expected) failures.Add("Interaction ray blocked: " + label + " firstHit=" + actual);
    }

    private static void BuildStair(Transform stairParent, Transform railingParent, string prefix, float x)
    {
        Transform stair = Group("Stair_" + prefix, stairParent);
        Transform guards = Group("R_Stair_" + prefix, railingParent);
        float rise = (DeckTop - FloorTop) / StairCount;
        for (int index = 0; index < StairCount; index++)
        {
            float z = 6f + (index + 0.5f) * StairTread;
            float top = FloorTop + (index + 1) * rise;
            Block("Tread_" + (index + 1).ToString("00"), stair, x, top - 0.09f, z, 1.8f, 0.18f, StairTread);
            Block("GuardLeft_" + (index + 1).ToString("00"), guards, x - 0.96f, top + 0.55f, z, 0.12f, 1.1f, StairTread);
            Block("GuardRight_" + (index + 1).ToString("00"), guards, x + 0.96f, top + 0.55f, z, 0.12f, 1.1f, StairTread);
        }
    }

    private static void RailX(Transform parent, string name, float x, float z, float length) =>
        Block(name, parent, x, 5.05f, z, length, 1.1f, 0.12f);
    private static void RailZ(Transform parent, string name, float x, float z, float length) =>
        Block(name, parent, x, 5.05f, z, 0.12f, 1.1f, length);

    private static Transform Group(string name, Transform parent)
    {
        GameObject item = new GameObject(name);
        item.transform.SetParent(parent, false);
        return item.transform;
    }

    private static void Marker(string name, Transform parent, float x, float y, float z)
    {
        Transform item = Group(name, parent);
        item.position = new Vector3(x, y, z);
    }

    private static void Block(string name, Transform parent, float x, float y, float z, float width, float height, float depth)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = name;
        item.transform.SetParent(parent, false);
        item.transform.position = new Vector3(x, y, z);
        item.transform.localScale = new Vector3(width, height, depth);
    }

    private static string Coord(float value) => value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('-', 'm').Replace('.', 'p');

    public static void Probe()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var output = new StringBuilder();
        output.AppendLine("C3_M2_EDITOR_PROBE Unity=" + Application.unityVersion + " Scene=" + scene.path);
        GameObject factory = scene.GetRootGameObjects().Single(root => root.name == "ONLYfactory");
        output.AppendLine("FACTORY_ROOT " + Describe(factory.transform));
        foreach (Renderer renderer in factory.GetComponentsInChildren<Renderer>(true))
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            output.AppendLine("RENDERER " + PathOf(renderer.transform) + " enabled=" + renderer.enabled +
                " worldBounds=" + BoundsOf(renderer.bounds) +
                " mesh=" + (filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : "none") +
                " localMeshBounds=" + (filter != null && filter.sharedMesh != null ? BoundsOf(filter.sharedMesh.bounds) : "none"));
            if (filter != null && filter.sharedMesh != null)
            {
                Mesh mesh = filter.sharedMesh;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    output.AppendLine("SUBMESH " + PathOf(renderer.transform) + " index=" + i +
                        " material=" + (i < materials.Length && materials[i] != null ? materials[i].name : "none") +
                        " worldBounds=" + BoundsOf(TransformBounds(renderer.transform, mesh.GetSubMesh(i).bounds)));
            }
        }
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                output.AppendLine("COLLIDER " + PathOf(collider.transform) + " type=" + collider.GetType().Name +
                    " enabled=" + collider.enabled + " trigger=" + collider.isTrigger + " worldBounds=" + BoundsOf(collider.bounds));
        File.WriteAllText(ProbePath, output.ToString(), new UTF8Encoding(false));
        Debug.Log("C3_M2_PROBE_PASS renderers=" + factory.GetComponentsInChildren<Renderer>(true).Length + " output=" + ProbePath);
    }

    public static void CheckCandidateSpace()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject factory = scene.GetRootGameObjects().Single(root => root.name == "ONLYfactory");
        MeshFilter filter = factory.GetComponentsInChildren<MeshFilter>(true).Single(item => item.name == "factory");
        var output = new StringBuilder();
        output.AppendLine("C3_M2_CANDIDATE_CHECK Unity=" + Application.unityVersion + " mesh=" + filter.sharedMesh.name);
        GameObject temporary = new GameObject("C3_M2_TemporaryFactoryCollisionProbe");
        try
        {
            temporary.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
            temporary.transform.localScale = filter.transform.lossyScale;
            MeshCollider modelCollider = temporary.AddComponent<MeshCollider>();
            modelCollider.sharedMesh = filter.sharedMesh;
            if (modelCollider.sharedMesh == null) throw new Exception("Temporary MeshCollider could not cook factory mesh.");
            Physics.SyncTransforms();
            CheckBox(output, modelCollider, "PositiveControl_FBXFloor", new Vector3(-12.5f, 0.5f, 12.5f), new Vector3(1f, 0.3f, 1f));
            CheckBox(output, modelCollider, "PositiveControl_FBXWestWall", new Vector3(-25f, 5f, 12f), new Vector3(0.5f, 1f, 1f));
            CheckBox(output, modelCollider, "MainDeck_and_Standing", new Vector3(-13.5f, 5.3f, 15.4f), new Vector3(17f, 2f, 3.2f));
            CheckBox(output, modelCollider, "ChipBay_and_Standing", new Vector3(-12.5f, 5.3f, 18.5f), new Vector3(3f, 2f, 3f));
            CheckBox(output, modelCollider, "ObservationDeck_and_Standing", new Vector3(-7f, 5.3f, 18.5f), new Vector3(4f, 2f, 3f));
            CheckBox(output, modelCollider, "WestStair_and_Standing", new Vector3(-21.1f, 3.45f, 9.96f), new Vector3(1.8f, 5.7f, 7.92f));
            CheckBox(output, modelCollider, "EastStair_and_Standing", new Vector3(-5.9f, 3.45f, 9.96f), new Vector3(1.8f, 5.7f, 7.92f));
            File.WriteAllText(CandidatePath, output.ToString(), new UTF8Encoding(false));
            Debug.Log("C3_M2_CANDIDATE_CHECK_DONE output=" + CandidatePath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temporary);
        }
    }

    private static void CheckBox(StringBuilder output, Collider modelCollider, string name, Vector3 center, Vector3 size)
    {
        Collider[] hits = Physics.OverlapBox(center, size * 0.5f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        output.AppendLine("CANDIDATE " + name + " center=" + Vec(center) + " size=" + Vec(size) +
            " factoryMeshHit=" + hits.Contains(modelCollider) + " otherHits=" +
            string.Join(",", hits.Where(hit => hit != modelCollider).Select(hit => PathOf(hit.transform)).ToArray()));
    }

    private static string Describe(Transform transform) =>
        "position=" + Vec(transform.position) + " rotation=" + Vec(transform.eulerAngles) + " scale=" + Vec(transform.lossyScale);

    private static string BoundsOf(Bounds bounds) => "min=" + Vec(bounds.min) + " max=" + Vec(bounds.max);
    private static Bounds TransformBounds(Transform transform, Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Bounds world = new Bounds(transform.TransformPoint(min), Vector3.zero);
        for (int xi = 0; xi < 2; xi++)
            for (int yi = 0; yi < 2; yi++)
                for (int zi = 0; zi < 2; zi++)
                    world.Encapsulate(transform.TransformPoint(new Vector3(
                        xi == 0 ? min.x : max.x, yi == 0 ? min.y : max.y, zi == 0 ? min.z : max.z)));
        return world;
    }
    private static string Vec(Vector3 value) => string.Format(System.Globalization.CultureInfo.InvariantCulture,
        "({0:F4},{1:F4},{2:F4})", value.x, value.y, value.z);

    private static string PathOf(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
