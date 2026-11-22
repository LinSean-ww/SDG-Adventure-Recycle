#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SDGAdventure.SharedBoundaryGates;

namespace SDGAdventure.SharedBoundaryGates.EditorTools
{
    // No name-based bulk conversion. Each physical boundary has one selected original door leaf.
    public sealed class SDGSharedBoundaryGateWindowV5 : EditorWindow
    {
        private const string Id12 = "ZONE1_TO_ZONE2";
        private const string Id2B = "ZONE2_TO_ZONEB";
        private const string MaterialFolder = "Assets/_Modules_/01_Shared/Player/SharedBoundaryGateMaterialsV5";
        private GameObject door12;
        private GameObject door2B;
        private bool autoProximity = true;
        private bool acknowledgeScripts;
        private bool acknowledgeComplexMesh;
        private string status = "請在第三關或第四關的 Scene 視窗選取通道中原本存在的實際門板。";
        private Vector2 scroll;

        private struct DoorInfo
        {
            public GameObject target;
            public Renderer[] renderers;
            public Collider[] colliders;
            public Bounds bounds;
            public float width;
            public float height;
            public float thickness;
            public bool widthOnZ;
            public string[] attachedScripts;
        }

        [MenuItem("SDG Adventure/前端 UI/23 第三四關共用邊界門（1-2、2-B）")]
        public static void Open()
        {
            GetWindow<SDGSharedBoundaryGateWindowV5>("SDG 共用邊界鐵閘門").minSize = new Vector2(740, 700);
        }

        private void OnGUI()
        {
            Scene scene = SceneManager.GetActiveScene();
            bool isScene = scene.IsValid() && scene.isLoaded && IsTargetScene(scene.path);
            bool ready = isScene && !EditorApplication.isPlayingOrWillChangePlaymode;
            EditorGUILayout.LabelField("SDG Adventure | 第三、四關共用邊界門 v5", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("一個物理通道只使用一扇門：1 區 ↔ 2 區一扇；2 區 ↔ B 區一扇。第三關、第四關各自在自己的正式場景中操作。不依名稱批次改門，也不新增第二道門。", MessageType.Info);
            EditorGUILayout.LabelField("目前場景", scene.IsValid() ? scene.path : "(未開啟)", EditorStyles.wordWrappedMiniLabel);
            if (!ready)
                EditorGUILayout.HelpBox("請停止 Play，開啟 Level03 或 Level04 的正式主場景。工具不允許修改 Level01、Level02 或其他場景。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(!ready))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("1. 選取兩個通道各自的原始門板（不是門框、牆、櫃門）", EditorStyles.boldLabel);
                DrawTarget("1 區 ↔ 2 區（共用一扇）", ref door12);
                DrawTarget("2 區 ↔ B 區（共用一扇）", ref door2B);
                EditorGUILayout.Space(6);
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(170));
                ShowPreview("1 ↔ 2", door12, scene, Id12);
                ShowPreview("2 ↔ B", door2B, scene, Id2B);
                EditorGUILayout.EndScrollView();
                if (door12 != null && door12 == door2B)
                    EditorGUILayout.HelpBox("兩個邊界選到了相同物件。不同通道不能共用同一片實體門板。", MessageType.Error);

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("2. 開啟機制", EditorStyles.boldLabel);
                autoProximity = EditorGUILayout.ToggleLeft("無門禁時：從任一側靠近即自動升起，離開後下降", autoProximity);
                acknowledgeScripts = EditorGUILayout.ToggleLeft("我已檢查門板上原有的控制腳本，必要時會接回 OpenDoor / CloseDoor", acknowledgeScripts);
                acknowledgeComplexMesh = EditorGUILayout.ToggleLeft("我已核對多個 Renderer 都屬於原門板，不包含門框與周邊設備", acknowledgeComplexMesh);
                EditorGUILayout.HelpBox("如果原門有鑰匙、解謎或關卡鎖定：取消自動開門，並把原腳本的開門事件連到新元件 OpenDoor()。不會自動繞過門禁。", MessageType.Warning);
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("3. 備份並改造所選的共用通道門", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("只改 1 ↔ 2", GUILayout.Height(32))) Convert(scene, Id12, door12, null);
                if (GUILayout.Button("只改 2 ↔ B", GUILayout.Height(32))) Convert(scene, Id2B, door2B, null);
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("兩道都核對好了：備份後一起轉換", GUILayout.Height(38))) Convert(scene, Id12, door12, door2B);

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("4. 個別還原（只還原 v5 工具產生的門）", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("還原 1 ↔ 2", GUILayout.Height(28))) Revert(scene, Id12, door12);
                if (GUILayout.Button("還原 2 ↔ B", GUILayout.Height(28))) Revert(scene, Id2B, door2B);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox(status, MessageType.None);
        }

        private static bool IsTargetScene(string path)
        {
            string p = (path ?? "").Replace('\\', '/');
            return p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) &&
                (p.Contains("/_Modules_/02_Levels/Level03/") || p.Contains("/_Modules_/02_Levels/Level04/"));
        }

        private static void DrawTarget(string title, ref GameObject selection)
        {
            EditorGUILayout.BeginHorizontal();
            selection = (GameObject)EditorGUILayout.ObjectField(title, selection, typeof(GameObject), true);
            if (GUILayout.Button("使用目前選取", GUILayout.Width(100))) selection = Selection.activeGameObject;
            if (GUILayout.Button("定位", GUILayout.Width(50)) && selection != null)
            {
                Selection.activeGameObject = selection;
                EditorGUIUtility.PingObject(selection);
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static string ScenePath(Transform t)
        {
            string result = t.name;
            while (t.parent != null) { t = t.parent; result = t.name + "/" + result; }
            return result;
        }

        // Get local-space mesh bounds instead of using world-axis-aligned bounds for rotated doors.
        private static Bounds ComputeLocalBounds(Transform root, Renderer[] renderers)
        {
            Bounds result = new Bounds();
            bool first = true;
            Matrix4x4 rootInverse = root.worldToLocalMatrix;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                Bounds source;
                Matrix4x4 mat;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                SkinnedMeshRenderer skin = r as SkinnedMeshRenderer;
                if (mf != null && mf.sharedMesh != null)
                {
                    source = mf.sharedMesh.bounds;
                    mat = rootInverse * r.transform.localToWorldMatrix;
                }
                else if (skin != null)
                {
                    source = skin.localBounds;
                    mat = rootInverse * r.transform.localToWorldMatrix;
                }
                else
                {
                    source = r.bounds;
                    mat = rootInverse;
                }
                Vector3 c = source.center, e = source.extents;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 point = mat.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3(x, y, z)));
                    if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                    else result.Encapsulate(point);
                }
            }
            return result;
        }

        private static bool HasOldGateMarkers(GameObject target)
        {
            if (target.GetComponent<SDGSharedBoundaryGateRecordV5>() != null) return true;
            foreach (MonoBehaviour mb in target.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string name = mb.GetType().Name;
                if (name == "SDGTargetedGateRecordV3" || name == "SDGTargetedLiftGateV3" ||
                    name == "SDGVerticalShutterDoor" || name == "SDGSharedBoundaryLiftGateV5") return true;
            }
            foreach (Transform tr in target.GetComponentsInChildren<Transform>(true))
                if (tr.name.StartsWith("SDG_UpwardIronShutter", StringComparison.Ordinal) ||
                    tr.name.StartsWith("SDG_B_LiftGate_", StringComparison.Ordinal) ||
                    tr.name.StartsWith("SDG_D_LiftGate_", StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool Inspect(GameObject target, Scene scene, out DoorInfo info, out string error)
        {
            info = new DoorInfo(); error = null;
            if (target == null || target.scene != scene) { error = "尚未選取此場景中的門板"; return false; }
            if (target.GetComponentInParent<Canvas>() != null || target.GetComponent<RectTransform>() != null)
            { error = "選取的是 UI，不是門板"; return false; }
            if (target.GetComponentInChildren<Camera>(true) != null)
            { error = "選取的物件包含攝影機。請改選門板的 Mesh 物件，而非整個場景群組。"; return false; }
            if (HasOldGateMarkers(target)) { error = "這個物件已被其他工具改造過；請先回復原門，避免重複產生門"; return false; }
            var renderers = target.GetComponentsInChildren<Renderer>(true)
                .Where(r => r != null && !(r is ParticleSystemRenderer) && !(r is TrailRenderer)).ToArray();
            if (renderers.Length == 0 || renderers.Length > 16)
            { error = "需要選擇具備少量 Mesh Renderer 的單片門板（目前 " + renderers.Length + " 個）"; return false; }
            Bounds b = ComputeLocalBounds(target.transform, renderers);
            float width = Mathf.Max(b.size.x, b.size.z);
            float thickness = Mathf.Min(b.size.x, b.size.z);
            float worldHeight = b.size.y * Mathf.Abs(target.transform.lossyScale.y);
            float worldWidth = width * Mathf.Max(Mathf.Abs(target.transform.lossyScale.x), Mathf.Abs(target.transform.lossyScale.z));
            if (worldHeight < 1.4f || worldHeight > 8f || worldWidth < 0.75f || worldWidth > 11f ||
                thickness > width * 0.50f)
            {
                error = "選取物件尺寸不像單片直立門板（局部大小 " + b.size.ToString("F2") +
                        "）。不要選整個建築、門框、櫃門或牆。";
                return false;
            }
            info.target = target; info.bounds = b;
            info.renderers = renderers;
            info.colliders = target.GetComponentsInChildren<Collider>(true).Where(c => c != null && !c.isTrigger).ToArray();
            info.width = width; info.height = b.size.y;
            info.thickness = thickness; info.widthOnZ = b.size.z > b.size.x;
            info.attachedScripts = target.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(s => s != null).Select(s => s.GetType().Name).Distinct().ToArray();
            return true;
        }

        private static void ShowPreview(string label, GameObject target, Scene scene, string id)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(label + " | " + (target == null ? "尚未指定" : ScenePath(target.transform)), EditorStyles.wordWrappedMiniLabel);
            DoorInfo info;
            string error;
            if (Inspect(target, scene, out info, out error))
            {
                EditorGUILayout.LabelField("門板尺寸（局部）：寬 " + info.width.ToString("F2") +
                    " / 高 " + info.height.ToString("F2") + " | Renderers " + info.renderers.Length +
                    " | 控制腳本 " + (info.attachedScripts.Length == 0 ? "無" : string.Join(",", info.attachedScripts)), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("包含的 Mesh：" + string.Join(" | ", info.renderers.Select(r => r.name).ToArray()), EditorStyles.wordWrappedMiniLabel);
                if (FindExisting(scene, id) != null)
                    EditorGUILayout.HelpBox("此場景已存在該通道的 v5 門，不可再次轉換。", MessageType.Warning);
            }
            else EditorGUILayout.HelpBox(error, MessageType.Warning);
            EditorGUILayout.EndVertical();
        }

        private static SDGSharedBoundaryGateRecordV5 FindExisting(Scene scene, string id)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (SDGSharedBoundaryGateRecordV5 record in root.GetComponentsInChildren<SDGSharedBoundaryGateRecordV5>(true))
                    if (record != null && record.BoundaryId == id) return record;
            return null;
        }

        private static string Backup(Scene scene)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            string root = Directory.GetParent(Application.dataPath).FullName;
            string source = Path.Combine(root, scene.path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source)) throw new IOException("找不到場景文件：" + source);
            string folder = Path.Combine(root, "SDG_FrontEnd_Backups", "SharedBoundariesV5_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            string dst = Path.Combine(folder, scene.path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(source, dst, false);
            File.WriteAllText(Path.Combine(folder, "RESTORE.txt"),
                "Close Unity before restoring this scene .unity file. Do not copy a .meta file.\r\nScene=" + scene.path + "\r\nBackup=" + dst);
            return dst;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static Material GetMaterial(string assetName, Color color)
        {
            string path = MaterialFolder + "/" + assetName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            EnsureFolder(MaterialFolder);
            bool isSRP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            Shader shader = Shader.Find(isSRP ? "Universal Render Pipeline/Lit" : "Standard");
            if (shader == null) shader = Shader.Find(isSRP ? "Standard" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new Exception("找不到 URP Lit 或 Standard Shader");
            material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.78f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void VisualCube(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(go, "Shared boundary gate visual");
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Undo.DestroyObjectImmediate(c);
        }

        private static void CreateGate(DoorInfo info, string id, bool automatic, Material iron, Material rib)
        {
            GameObject host = new GameObject("SDG_SharedGate_" + id + "_V5");
            Undo.RegisterCreatedObjectUndo(host, "One shared shutter gate");
            host.transform.SetParent(info.target.transform, false);
            host.transform.localPosition = info.bounds.center;
            host.transform.localRotation = info.widthOnZ ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            host.transform.localScale = Vector3.one;

            GameObject panel = new GameObject("RisingIronShutterPanel");
            Undo.RegisterCreatedObjectUndo(panel, "Moving shutter leaf");
            panel.transform.SetParent(host.transform, false);
            panel.transform.localPosition = Vector3.zero;
            float width = info.width, height = info.height;
            float depth = Mathf.Clamp(info.thickness, 0.09f, 0.32f);
            VisualCube("IronSheet", panel.transform, Vector3.zero, new Vector3(width, height, depth), iron);
            int strips = Mathf.Clamp(Mathf.CeilToInt(height / 0.24f), 6, 32);
            for (int i = 0; i <= strips; i++)
                VisualCube("Rib_" + i, panel.transform,
                    new Vector3(0f, -height * 0.5f + height * i / strips, -(depth * 0.5f + 0.025f)),
                    new Vector3(width + 0.02f, 0.05f, 0.045f), rib);
            BoxCollider blocking = Undo.AddComponent<BoxCollider>(panel);
            blocking.center = Vector3.zero;
            blocking.size = new Vector3(width, height, depth);
            VisualCube("LeftGuide", host.transform, new Vector3(-width * 0.5f - 0.075f, 0f, 0f),
                new Vector3(0.15f, height + 0.1f, depth + 0.08f), rib);
            VisualCube("RightGuide", host.transform, new Vector3(width * 0.5f + 0.075f, 0f, 0f),
                new Vector3(0.15f, height + 0.1f, depth + 0.08f), rib);
            VisualCube("TopHousing", host.transform, new Vector3(0f, height * 0.5f + 0.12f, 0f),
                new Vector3(width + 0.30f, 0.24f, depth + 0.1f), iron);

            SDGSharedBoundaryGateRecordV5 record = Undo.AddComponent<SDGSharedBoundaryGateRecordV5>(info.target);
            record.Capture(id, host, info.renderers, info.colliders);
            SDGSharedBoundaryLiftGateV5 controller = Undo.AddComponent<SDGSharedBoundaryLiftGateV5>(info.target);
            controller.Configure(id, panel.transform, height + 0.12f, automatic, info.renderers, info.colliders);
            foreach (Renderer r in info.renderers)
            {
                if (r == null) continue;
                Undo.RecordObject(r, "Hide original shared-boundary door panel");
                r.enabled = false;
            }
            foreach (Collider c in info.colliders)
            {
                if (c == null) continue;
                Undo.RecordObject(c, "Disable original shared-boundary door solid collider");
                c.enabled = false;
            }
            EditorUtility.SetDirty(record);
            EditorUtility.SetDirty(controller);
        }

        private void Convert(Scene scene, string id, GameObject chosen, GameObject second)
        {
            bool both = second != null;
            if (chosen == null) { status = "尚未選擇此通道的實際原始門板。"; return; }
            if (both && chosen == second) { status = "兩個邊界不可選取同一片門。"; return; }
            if (both && (chosen.transform.IsChildOf(second.transform) || second.transform.IsChildOf(chosen.transform)))
            { status = "兩個物件有父子關係。請選各通道真正獨立的門板。"; return; }

            DoorInfo one, two;
            string error;
            if (!Inspect(chosen, scene, out one, out error)) { status = "第一道門：" + error; return; }
            if (both)
            {
                if (!Inspect(second, scene, out two, out error)) { status = "第二道門：" + error; return; }
            }
            else two = new DoorInfo();
            string id2 = Id2B;
            if (FindExisting(scene, id) != null || (both && FindExisting(scene, id2) != null))
            { status = "該邊界的鐵閘門已存在，請避免重複新增。"; return; }
            if (one.renderers.Length > 4 || (both && two.renderers.Length > 4))
                if (!acknowledgeComplexMesh) { status = "包含多片 Mesh。請確認沒有選到門框，核對後勾選多 Renderer 確認。"; return; }
            if (one.attachedScripts.Length > 0 || (both && two.attachedScripts.Length > 0))
            {
                if (!acknowledgeScripts) { status = "原門有控制腳本，先核對門禁機制後勾選確認。"; return; }
                if (autoProximity) { status = "原門有控制腳本，禁止自動感應以免繞過原門禁。請取消『靠近自動開門』。"; return; }
            }
            string prompt = "只替換當前場景指定的實際原始門板（不新增第二扇門）。\n\n場景：" + scene.path +
                "\n通道 " + id + "：" + ScenePath(chosen.transform) +
                (both ? "\n通道 " + id2 + "：" + ScenePath(second.transform) : "") +
                "\n\n開門模式：" + (autoProximity ? "雙側接近自動開啟" : "手動事件 OpenDoor / CloseDoor") +
                "\n\n其他門與所有關卡內容不更動，套用前將備份 .unity。";
            if (!EditorUtility.DisplayDialog("最終確認共用通道門", prompt, "備份後僅修改所選門", "取消")) return;
            try
            {
                string backup = Backup(scene);
                if (backup == null) { status = "取消，未修改。"; return; }
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Convert exact shared boundary door(s)");
                try
                {
                    Material iron = GetMaterial("DarkIron_V5", new Color(0.18f, 0.21f, 0.24f));
                    Material rib = GetMaterial("SteelRib_V5", new Color(0.42f, 0.46f, 0.49f));
                    CreateGate(one, id, autoProximity, iron, rib);
                    if (both) CreateGate(two, id2, autoProximity, iron, rib);
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    status = "完成本場景的指定共用通道門轉換。備份：" + backup + "。請在 Play 模式驗收升降與碰撞。";
                    Debug.Log("[SDG Shared Gate V5] " + status);
                }
                catch { Undo.RevertAllDownToGroup(group); throw; }
            }
            catch (Exception ex) { status = "修改失敗：" + ex.Message; Debug.LogException(ex); }
        }

        private void Revert(Scene scene, string id, GameObject target)
        {
            if (target == null || target.scene != scene) { status = "請先指定目前場景的門板。"; return; }
            SDGSharedBoundaryGateRecordV5 record = target.GetComponent<SDGSharedBoundaryGateRecordV5>();
            SDGSharedBoundaryLiftGateV5 lift = target.GetComponent<SDGSharedBoundaryLiftGateV5>();
            if (record == null || lift == null || record.CreatedGate == null || record.BoundaryId != id)
            { status = "不是該邊界的 v5 門，未修改。"; return; }
            if (!EditorUtility.DisplayDialog("還原原始門板", "只還原：" + ScenePath(target.transform), "備份後還原", "取消")) return;
            try
            {
                string backup = Backup(scene);
                if (backup == null) return;
                Renderer[] rs = record.OriginalRenderers; bool[] rb = record.RendererEnabledBefore;
                Collider[] cs = record.OriginalColliders; bool[] cb = record.ColliderEnabledBefore;
                for (int i = 0; i < rs.Length && i < rb.Length; i++)
                    if (rs[i] != null) { Undo.RecordObject(rs[i], "Restore original renderer"); rs[i].enabled = rb[i]; }
                for (int i = 0; i < cs.Length && i < cb.Length; i++)
                    if (cs[i] != null) { Undo.RecordObject(cs[i], "Restore original collider"); cs[i].enabled = cb[i]; }
                Undo.DestroyObjectImmediate(record.CreatedGate);
                Undo.DestroyObjectImmediate(lift);
                Undo.DestroyObjectImmediate(record);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                status = "已還原原門板。備份：" + backup;
            }
            catch (Exception ex) { status = "還原失敗：" + ex.Message; Debug.LogException(ex); }
        }
    }
}
#endif
