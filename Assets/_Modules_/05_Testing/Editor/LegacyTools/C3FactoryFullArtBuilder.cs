using System;
using System.IO;
using System.Linq;
using System.Text;
using C3TreasurePuzzle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only, one-stage-at-a-time construction for the C3 factory art pass.
public static class C3FactoryFullArtBuilder
{
    private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
    private static readonly string BackupPath = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_FullArt_Backups/C_Main_PreFullArt_20261026.unity.bak");
    private static readonly string EvidenceDir = SDGAdventure.Testing.ReportOutputPaths.Resolve("C3_FullArt_Evidence");

    public static void StageA_MoveControlChip()
    {
        RequireBackup();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        Transform marker = roots.Single(root => root.name == "C_TwoFloor_Blockout").transform
            .Find("FutureGameplayMarkers/Future_M3_ControlChip_2F_Only");
        if (marker == null) throw new Exception("M2 future chip marker missing.");
        C3CollectiblePart[] parts = roots.SelectMany(root => root.GetComponentsInChildren<C3CollectiblePart>(true)).ToArray();
        if (parts.Length != 3 || parts.Count(part => part.PartId == "control_chip") != 1)
            throw new Exception("Expected exactly three original parts and one control chip.");
        C3CollectiblePart chip = parts.Single(part => part.PartId == "control_chip");
        if (Vector3.Distance(chip.transform.position, new Vector3(-12.5f, 0.95f, 14f)) > 0.001f)
            throw new Exception("Control chip was already moved by another task; refusing to overwrite it.");
        if (Vector3.Distance(marker.position, new Vector3(-12.5f, 4.9f, 18.5f)) > 0.001f)
            throw new Exception("M2 future chip marker has changed.");
        Collider deck = roots.Single(root => root.name == "C_TwoFloor_Blockout").transform
            .Find("Floor2_Platforms/F2_ControlChipBay").GetComponent<Collider>();
        if (!deck.bounds.Contains(new Vector3(marker.position.x, 4.5f, marker.position.z)))
            throw new Exception("Control chip marker is not on the approved upper deck.");
        Vector3 newPosition = marker.position;
        chip.transform.position = newPosition;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save scene after moving the control chip.");
        VerifyStageA();
        Debug.Log("C3_FULLART_STAGE_A_PASS chip=" + Vec(newPosition));
    }

    public static void VerifyStageA()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        C3CollectiblePart[] parts = roots.SelectMany(root => root.GetComponentsInChildren<C3CollectiblePart>(true)).ToArray();
        if (parts.Length != 3 || parts.Count(part => part.PartId == "control_chip") != 1 ||
            parts.Count(part => part.PartId == "fuse") != 1 ||
            parts.Count(part => part.PartId == "filter_module") != 1)
            throw new Exception("C3 part IDs or counts changed.");
        C3CollectiblePart chip = parts.Single(part => part.PartId == "control_chip");
        if (Vector3.Distance(chip.transform.position, new Vector3(-12.5f, 4.9f, 18.5f)) > 0.001f ||
            Vector3.Distance(parts.Single(part => part.PartId == "fuse").transform.position, new Vector3(-20f, 0.98f, 22f)) > 0.001f ||
            Vector3.Distance(parts.Single(part => part.PartId == "filter_module").transform.position, new Vector3(-4f, 1.03f, 18f)) > 0.001f)
            throw new Exception("C3 part position mismatch.");
        Vector3 eye = new Vector3(-12.5f, 6f, 16.7f);
        Vector3 aim = chip.transform.position - eye;
        Physics.SyncTransforms();
        bool rayHit = Physics.Raycast(eye, aim.normalized, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore);
        if (!rayHit || hit.collider.GetComponentInParent<C3CollectiblePart>() != chip)
            throw new Exception("Upper-deck control chip cannot be targeted within 3 units; first hit=" +
                (rayHit ? hit.collider.name : "NONE"));
        Directory.CreateDirectory(EvidenceDir);
        File.WriteAllText(EvidenceDir + "/StageA_Chip.txt",
            "Unity=" + Application.unityVersion + "\n" +
            "partCount=" + parts.Length + "\n" +
            "fuse=" + Vec(parts.Single(part => part.PartId == "fuse").transform.position) + "\n" +
            "control_chip=" + Vec(chip.transform.position) + "\n" +
            "filter_module=" + Vec(parts.Single(part => part.PartId == "filter_module").transform.position) + "\n" +
            "chipRayWithin3m=true firstHit=" + hit.collider.name + "\n" +
            "objective=" + roots.SelectMany(root => root.GetComponentsInChildren<C3TreasureProgress>(true)).Single().Objective + "\n",
            new UTF8Encoding(false));
        Debug.Log("C3_FULLART_STAGE_A_VERIFY_PASS");
    }

    public static void FinalAudit() => C3SceneBugFixTool.FinalStaticAudit();

    // Historical baseline validator for the pre-bug-fix art pass.
    public static void FullArtBaselineAudit()
    {
        RequireBackup();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject[] roots = scene.GetRootGameObjects();
        Transform factory = roots.Single(root => root.name == "ONLYfactory").transform;
        if (factory.position != Vector3.zero || factory.rotation != Quaternion.identity || factory.localScale != Vector3.one)
            throw new Exception("Original FBX root transform changed.");
        if (factory.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.name != "factory"))
            throw new Exception("Original FBX visual is missing.");
        Transform blockout = roots.Single(root => root.name == "C_TwoFloor_Blockout").transform;
        Transform art = roots.Single(root => root.name == "C_Factory_Art").transform;
        string[] groups = { "Architecture_Additions", "Floor1_Industrial", "Floor2_Industrial", "IndustrialMachines",
            "Pipes", "Props", "Materials_Visuals", "Lighting", "Audio", "Decorations" };
        foreach (string group in groups) if (art.Find(group) == null) throw new Exception("Missing art group: " + group);
        C3CollectiblePart[] parts = roots.SelectMany(root => root.GetComponentsInChildren<C3CollectiblePart>(true)).ToArray();
        if (parts.Length != 3 || parts.Select(part => part.PartId).Distinct().Count() != 3 ||
            parts.Any(part => part.GetComponent<Collider>() == null)) throw new Exception("Original parts or colliders changed.");
        if (roots.SelectMany(root => root.GetComponentsInChildren<C3PuzzleConsole>(true)).Count() != 1 ||
            roots.SelectMany(root => root.GetComponentsInChildren<C3DeviceState>(true)).Count() != 1 ||
            roots.SelectMany(root => root.GetComponentsInChildren<C3TreasureProgress>(true)).Count() != 1 ||
            roots.SelectMany(root => root.GetComponentsInChildren<C3FirstPersonController>(true)).Count() != 1 ||
            roots.SelectMany(root => root.GetComponentsInChildren<C3TreasureHud>(true)).Count() != 1)
            throw new Exception("Original C3 gameplay components or module adapter missing.");
        Collider[] addedColliders = art.GetComponentsInChildren<Collider>(true);
        Collider[] walls = roots.SelectMany(root => root.GetComponentsInChildren<Collider>(true))
            .Where(collider => collider.name.StartsWith("C_Wall_")).ToArray();
        foreach (Collider added in addedColliders)
            foreach (Collider wall in walls)
                if (added.bounds.Intersects(wall.bounds))
                    throw new Exception("Added art collider intersects original wall proxy: " + added.name + " / " + wall.name);
        if (blockout.GetComponentsInChildren<BoxCollider>(true).Length != 157 || addedColliders.Length != 14)
            throw new Exception("M2 or added equipment collider count changed.");
        Directory.CreateDirectory(EvidenceDir);
        var report = new StringBuilder();
        report.AppendLine("Unity=" + Application.unityVersion);
        report.AppendLine("scene=" + ScenePath);
        report.AppendLine("factoryRoot=identity; originalRenderer=present");
        report.AppendLine("coreParts=3; console=1; device=1; progress=1; playerController=1; hud=1");
        report.AppendLine("adapter=runtime-created by ModuleCoordinator; verified in Final_MotherFlow.xml");
        report.AppendLine("m2Colliders=157; addedEquipmentColliders=14; addedColliderWallProxyIntersections=0");
        report.AppendLine("playerSpawn=" + Vec(roots.SelectMany(root => root.GetComponentsInChildren<C3FirstPersonController>(true)).Single().transform.position));
        report.AppendLine("console=" + Vec(roots.SelectMany(root => root.GetComponentsInChildren<C3PuzzleConsole>(true)).Single().transform.position));
        report.AppendLine("artHierarchy:");
        foreach (string group in groups)
        {
            Transform child = art.Find(group);
            report.AppendLine("  " + group + " children=" + child.childCount);
            foreach (Transform item in child)
                report.AppendLine("    " + item.name + " world=" + Vec(item.position));
        }
        File.WriteAllText(EvidenceDir + "/Final_StaticAudit.txt", report.ToString(), new UTF8Encoding(false));
        VerifyStageA();
        C3FactoryArtConstruction.ValidateStageC();
        C3FactoryArtMaterials.ValidateStageD();
        C3FactoryArtLighting.ValidateStageE();
        C3FactoryArtAudio.ValidateStageF();
        C3FactoryArtDetails.ValidateNarrative();
        Debug.Log("C3_FULLART_FINAL_STATIC_AUDIT_PASS");
    }

    private static void RequireBackup()
    {
        if (!File.Exists(BackupPath)) throw new Exception("Full-art scene backup missing: " + BackupPath);
    }

    private static string Vec(Vector3 value) => string.Format(System.Globalization.CultureInfo.InvariantCulture,
        "({0:F3},{1:F3},{2:F3})", value.x, value.y, value.z);
}
