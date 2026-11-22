#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using C3TreasurePuzzle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.Level03.MissionGatesV7.Editor
{
    /// <summary>
    /// Verifies exact Level03 door geometry from the provided source archive.
    /// Never scans arbitrary names containing Door / Gate. Only B/C and D exit.
    /// </summary>
    public static class C3MissionGateBatchV7
    {
        private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private const string CurtainPath = "M3_Treasure/C3_ReferenceRecyclingFactory_Visuals/V11_BC_PVC_CURTAIN";
        private const string FramePath = "C_CollisionProxies/M3_DoorFrameCaps/";
        private const string BId = "SDG_V7_L03_B_MISSION_IRON_SHUTTER";
        private const string DId = "SDG_V7_L03_D_MISSION_IRON_SHUTTER";
        private const string LegacyB = "SDG_V6_L03_BC_ENTRY_SHUTTER";
        private const string LegacyD = "SDG_V6_L03_D_EXIT_SHUTTER";
        private const string Materials = "Assets/_Modules_/02_Levels/Level03/MissionGatesV7/Materials";
        private const string ReportName = "SDG_Gates_V7_Level03_Report.txt";

        [MenuItem("SDG Adventure/Doors/25 Level03 B-D mission-locked shutters (verified + backup)")]
        public static void ApplyFromEditor()
        {
            try { Apply(); EditorUtility.DisplayDialog("SDG Gates v7", "Finished. Check SDG_Gates_V7_Level03_Report.txt and test in PlayMode.", "OK"); }
            catch (Exception e) { Debug.LogError("SDG_GATE_V7_ERROR: " + e); EditorUtility.DisplayDialog("SDG Gates v7 ERROR", e.Message, "OK"); }
        }

        public static void ApplyBatch()
        {
            try { Apply(); Debug.Log("SDG_GATE_V7_SUCCESS"); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("SDG_GATE_V7_ERROR: " + e); EditorApplication.Exit(1); }
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before modifying scenes.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.isLoaded || scene.path != ScenePath)
                throw new InvalidOperationException("Level03 main scene was not loaded.");

            Transform priorB = Optional(scene, BId);
            Transform priorD = Optional(scene, DId);
            if (priorB != null || priorD != null)
            {
                if (priorB == null || priorD == null ||
                    priorB.GetComponent<C3MissionLockedVerticalShutter>() == null ||
                    priorD.GetComponent<C3MissionLockedVerticalShutter>() == null)
                    throw new InvalidOperationException("Partially converted v7 gate detected. Restore v7 backup first; refusing duplicate conversion.");
                WriteReport("v7 already installed. Both verified gates exist; no changes made. Recheck mission gates in PlayMode.");
                return;
            }

            // Validate exact original gate geometry before any scene modification.
            Transform curtain = Required(scene, CurtainPath);
            Transform header = Required(scene, CurtainPath + "/HEADER_RAIL");
            RequireNear(header.position, new Vector3(-.30f, 3.54f, 12.5f), .16f, "B header");
            if (curtain.GetComponentsInChildren<Renderer>(true).Length < 20)
                throw new InvalidOperationException("B/C original PVC curtain geometry is not recognized; no changes made.");
            RequireNear(Required(scene, FramePath + "D_LeftJamb").position,
                new Vector3(-15.4f, 2f, .32f), .14f, "D left jamb");
            RequireNear(Required(scene, FramePath + "D_RightJamb").position,
                new Vector3(-9.6f, 2f, .32f), .14f, "D right jamb");
            RequireNear(Required(scene, FramePath + "D_LintelCap").position,
                new Vector3(-12.5f, 3.675f, .32f), .14f, "D lintel");
            RequireNear(Required(scene, FramePath + "B_NearJamb").position,
                new Vector3(-.3f, 2f, 9.6f), .14f, "B near jamb");
            RequireNear(Required(scene, FramePath + "B_FarJamb").position,
                new Vector3(-.3f, 2f, 15.4f), .14f, "B far jamb");
            Required(scene, "C_CollisionProxies/B_Door_Floor");
            Required(scene, "C_CollisionProxies/D_Door_Floor");

            Transform mission = Required(scene, "M3_Treasure");
            C3TreasureProgress progress = mission.GetComponent<C3TreasureProgress>();
            C3DeviceState device = mission.GetComponent<C3DeviceState>();
            if (progress == null) progress = mission.GetComponentInChildren<C3TreasureProgress>(true);
            if (device == null) device = mission.GetComponentInChildren<C3DeviceState>(true);
            if (progress == null || device == null)
                throw new InvalidOperationException("Missing C3 mission authority. Refusing to create doors that can bypass the quest.");

            Transform oldB = Optional(scene, LegacyB), oldD = Optional(scene, LegacyD);
            ValidateLegacyGate(oldB, LegacyB);
            ValidateLegacyGate(oldD, LegacyD);

            string backup = BackupScene();
            Material iron = EnsureMaterial("SDG_V7_DarkSteel.mat", new Color(.19f,.24f,.29f), .65f, .34f);
            Material rib = EnsureMaterial("SDG_V7_RibSteel.mat", new Color(.36f,.42f,.47f), .7f, .4f);
            Material housing = EnsureMaterial("SDG_V7_Housing.mat", new Color(.13f,.16f,.19f), .78f, .35f);

            // Legacy v6 gates are generated by the v6 tool, not original level design.
            // Remove only the exact verified v6-generated gate objects to avoid duplicates.
            if (oldB != null) UnityEngine.Object.DestroyImmediate(oldB.gameObject);
            if (oldD != null) UnityEngine.Object.DestroyImmediate(oldD.gameObject);
            curtain.gameObject.SetActive(false); // B's original 20 visual PVC strips.

            Transform physicalRoot = Required(scene, "C_CollisionProxies");
            MakeGate(physicalRoot, BId, new Vector3(-.30f, 2.02f, 12.50f),
                4.55f, 2.94f, true, true,
                C3MissionLockedVerticalShutter.UnlockMilestone.AllThreePartsCollected,
                progress, device, iron, rib, housing);
            MakeGate(physicalRoot, DId, new Vector3(-12.50f, 2.02f, .32f),
                4.55f, 2.94f, false, false,
                C3MissionLockedVerticalShutter.UnlockMilestone.FactoryRunning,
                progress, device, iron, rib, housing);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException("Could not save C_Main.unity. Use the included restore command.");
            AssetDatabase.SaveAssets();
            WriteReport("APPLIED: Level03 C_Main.unity\n" +
                "Backup: " + backup + "\n" +
                "B entrance: original 20-strip PVC curtain hidden; one 4.55m x 2.94m iron shutter at (-0.30,2.02,12.50).\n" +
                "B unlock: C3TreasureProgress.AllPartsCollected = true (3 mission parts).\n" +
                "D exit: existing frame and trigger kept; one 4.55m x 2.94m iron shutter at (-12.50,2.02,0.32).\n" +
                "D unlock: C3DeviceState.FactoryState == Running or Completed (after puzzle and factory start).\n" +
                "Once unlocked, player proximity raises shutter from either side. Otherwise the panel and collider stay closed.\n" +
                "Exact v6-generated B/D gate objects removed, if found; no equipment or cabinet doors touched.\n" +
                "Level01 / Level02 / Level04 unchanged.\n" +
                "Unity compile and PlayMode validation must be completed on the user's computer.");
        }

        private static void ValidateLegacyGate(Transform obj, string id)
        {
            if (obj == null) return;
            if (obj.name != id || obj.Find("MOVING_SHUTTER_PANEL") == null)
                throw new InvalidOperationException("Unknown gate layout: " + id + ". Cannot safely replace it.");
            MonoBehaviour[] scripts = obj.GetComponents<MonoBehaviour>();
            if (scripts.Length != 1 || scripts[0] == null || scripts[0].GetType().Name != "SDGVerifiedVerticalGateV6")
                throw new InvalidOperationException("Legacy gate has unexpected or missing scripts: " + id + ". No changes made.");
        }

        private static void MakeGate(Transform root, string id, Vector3 center, float width,
            float height, bool alongZ, bool addGuides,
            C3MissionLockedVerticalShutter.UnlockMilestone milestone,
            C3TreasureProgress progress, C3DeviceState device,
            Material iron, Material rib, Material housing)
        {
            GameObject anchor = new GameObject(id);
            anchor.transform.SetParent(root, false);
            anchor.transform.position = center;
            Transform mover = new GameObject("MOVING_IRON_SHUTTER").transform;
            mover.SetParent(anchor.transform, false);
            Cube(mover, "SOLID_IRON_LEAF", Vector3.zero,
                alongZ ? new Vector3(.17f, height, width) : new Vector3(width, height, .17f),
                iron, true);
            for (int i = 1; i <= 13; i++)
            {
                float y = -height * .5f + i * height / 14f;
                for (int face = -1; face <= 1; face += 2)
                {
                    float offset = face * .112f;
                    Vector3 p = alongZ ? new Vector3(offset,y,0f) : new Vector3(0f,y,offset);
                    Vector3 s = alongZ ? new Vector3(.055f,.055f,width-.06f) :
                        new Vector3(width-.06f,.055f,.055f);
                    Cube(mover, "HORIZONTAL_RIB_" + i + "_" + face, p, s, rib, false);
                }
            }
            if (addGuides)
            {
                float edge = width * .5f + .10f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 p = alongZ ? new Vector3(0,0,side*edge) : new Vector3(side*edge,0,0);
                    Vector3 s = alongZ ? new Vector3(.32f,height+.30f,.14f) : new Vector3(.14f,height+.30f,.32f);
                    Cube(anchor.transform,"GUIDE_"+side,p,s,housing,false);
                }
                Cube(anchor.transform,"UPPER_MOTOR_BOX",new Vector3(0,height*.5f+.14f,0),
                    alongZ ? new Vector3(.40f,.27f,width+.22f) : new Vector3(width+.22f,.27f,.40f),housing,false);
            }
            // Existing D jambs/lintel remain intact; no duplicate structural frame.
            C3MissionLockedVerticalShutter gate = anchor.AddComponent<C3MissionLockedVerticalShutter>();
            gate.Configure(milestone, progress, device, mover, height + .17f, 2.4f, 3.5f);
        }

        private static GameObject Cube(Transform parent, string name, Vector3 localPos,
            Vector3 size, Material material, bool solid)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
            return cube;
        }

        private static Material EnsureMaterial(string file, Color color, float metallic, float gloss)
        {
            if (!AssetDatabase.IsValidFolder(Materials))
            {
                const string baseFolder = "Assets/_Modules_/02_Levels/Level03/MissionGatesV7";
                if (!AssetDatabase.IsValidFolder(baseFolder)) throw new InvalidOperationException("Missing MissionGatesV7 folder.");
                AssetDatabase.CreateFolder(baseFolder, "Materials");
            }
            string path = Materials + "/" + file;
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Cannot find a supported Lit shader.");
            mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", gloss);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static string BackupScene()
        {
            string root = Path.GetDirectoryName(Application.dataPath) ?? "";
            string folder = Path.Combine(root, "SDG_GateBackups", "mission_v7_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(folder);
            File.Copy(Path.Combine(root, ScenePath.Replace('/',Path.DirectorySeparatorChar)),
                Path.Combine(folder,"Level03_C_Main.unity"),false);
            return folder;
        }
        private static void WriteReport(string details)
        {
            string root = Path.GetDirectoryName(Application.dataPath) ?? "";
            string path = Path.Combine(root, ReportName);
            File.WriteAllText(path, "SDG Mission Gates v7 | " + DateTime.Now.ToString("u") + "\n" + details + "\n", new UTF8Encoding(true));
            Debug.Log("SDG_GATE_V7_REPORT " + path + "\n" + details);
        }
        private static Transform Optional(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    if (item.name == name) return item;
            }
            return null;
        }
        private static Transform Required(Scene scene, string path)
        {
            string[] parts = path.Split('/');
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                Transform current = root.transform;
                for (int i = 1; i < parts.Length && current != null; i++) current = current.Find(parts[i]);
                if (current != null) return current;
            }
            throw new InvalidOperationException("Missing verified Level03 scene object: " + path);
        }
        private static void RequireNear(Vector3 actual, Vector3 expected, float tolerance, string label)
        {
            if (Vector3.Distance(actual, expected) > tolerance)
                throw new InvalidOperationException(label + " moved: " + actual + " (expected " + expected + "). No scene changes made.");
        }
    }
}
#endif
