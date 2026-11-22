#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SDGAdventure.SharedPlayer;

namespace SDGAdventure.FrontEnd.EditorTools
{
    public sealed class SDGCleanBaselineBDGateWindow : EditorWindow
    {
        private GameObject bDoor;
        private GameObject dDoor;
        private bool autoOpen = true;
        private bool acknowledgeScriptedDoors;
        private Vector2 scroll;
        private readonly List<GameObject> bSigns = new List<GameObject>();
        private readonly List<GameObject> dSigns = new List<GameObject>();
        private string status = "請在 Edit Mode 開啟 Level03 的 C_Main.unity，選取 B、D 的原始門板。";
        private const string Materials = "Assets/_Modules_/01_Shared/Player/TargetedGateMaterialsV3";

        private struct DoorInfo
        {
            public GameObject target;
            public Renderer[] renderers;
            public Collider[] colliders;
            public Bounds bounds;
            public bool widthAlongZ;
            public float width;
            public float height;
            public float thickness;
            public string[] scripts;
        }

        [MenuItem("SDG Adventure/前端 UI/21 原始版本 B-D 入口升降鐵閘門（指定兩道）")]
        private static void OpenWindow()
        {
            GetWindow<SDGCleanBaselineBDGateWindow>("SDG B-D 原始門修正").minSize = new Vector2(740, 630);
        }

        private void OnGUI()
        {
            Scene scene = SceneManager.GetActiveScene();
            bool ready = scene.IsValid() && scene.isLoaded && !EditorApplication.isPlayingOrWillChangePlaymode &&
                scene.path.Replace('\\', '/').Contains("/_Modules_/02_Levels/Level03/");
            EditorGUILayout.LabelField("SDG Adventure | 原始場景 B / D 升降鐵閘門 v3", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("給已還原到『改門前』的乾淨第三關使用。舊版 19 的名稱批次轉換不要執行；本工具不會碰樓梯、輸送帶護欄、工具櫃、配電箱或其他未指定物件。", MessageType.Info);
            EditorGUILayout.LabelField("目前場景", scene.IsValid() ? scene.path : "(無)", EditorStyles.wordWrappedMiniLabel);
            if (!ready) EditorGUILayout.HelpBox("請先停止 Play，開啟 Level03/Scenes/C_Main.unity，再執行工具。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button("掃描 B、D 區入口招牌（只定位，不改任何物件）", GUILayout.Height(30))) FindSigns(scene);
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(140));
                ShowSignList("B 區招牌", bSigns);
                ShowSignList("D 區招牌", dSigns);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("1. 從 Hierarchy 選取實際門板，再指定 B / D", EditorStyles.boldLabel);
                DrawDoorField("B 區原門板", ref bDoor);
                DrawDoorField("D 區原門板", ref dDoor);
                EditorGUILayout.Space(5);
                DrawPreview("B", bDoor, scene);
                DrawPreview("D", dDoor, scene);
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("2. 開門方式", EditorStyles.boldLabel);
                autoOpen = EditorGUILayout.ToggleLeft("靠近自動向上升起、離開向下降落（僅適合無門禁／無解謎限制的入口）", autoOpen);
                acknowledgeScriptedDoors = EditorGUILayout.ToggleLeft("我已檢查原始開門腳本，知道需要重新連接 OpenDoor / CloseDoor", acknowledgeScriptedDoors);
                EditorGUILayout.HelpBox("若入口原本有門禁或任務限制，請取消『靠近自動開門』；轉換後必須讓原控制腳本呼叫新元件的 OpenDoor/CloseDoor，否則門不會自行開啟。", MessageType.Warning);
                EditorGUILayout.Space(5);
                if (GUILayout.Button("確認 B、D 兩道原門，備份後同時轉換", GUILayout.Height(40))) ConvertBoth(scene);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("3. 個別還原（僅本工具剛轉換的門）", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("還原 B 門", GUILayout.Height(26))) RevertOne(scene, bDoor);
                if (GUILayout.Button("還原 D 門", GUILayout.Height(26))) RevertOne(scene, dDoor);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox(status, MessageType.None);
        }

        private static void DrawDoorField(string label, ref GameObject value)
        {
            EditorGUILayout.BeginHorizontal();
            value = (GameObject)EditorGUILayout.ObjectField(label, value, typeof(GameObject), true);
            if (GUILayout.Button("使用目前選取", GUILayout.Width(106))) value = Selection.activeGameObject;
            if (GUILayout.Button("定位", GUILayout.Width(48)) && value != null) Focus(value);
            EditorGUILayout.EndHorizontal();
        }

        private static void Focus(GameObject target)
        {
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        }

        private static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        private static IEnumerable<GameObject> SceneObjects(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    yield return t.gameObject;
        }

        private void FindSigns(Scene scene)
        {
            bSigns.Clear(); dSigns.Clear();
            foreach (GameObject go in SceneObjects(scene))
            {
                string text = go.name;
                TextMesh tm = go.GetComponent<TextMesh>();
                if (tm != null) text += " " + tm.text;
                foreach (Component c in go.GetComponents<Component>())
                {
                    if (c == null) continue;
                    string typeName = c.GetType().Name;
                    if (!typeName.Contains("TextMeshPro") && !typeName.Contains("TMP_Text")) continue;
                    PropertyInfo prop = c.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                        try { text += " " + (prop.GetValue(c, null) as string); } catch { }
                }
                string compact = text.Replace(" ", "").Replace("　", "").ToUpperInvariant();
                if (compact.Contains("B區入口") || compact.Contains("ZONEB") || compact.Contains("ENTRANCEB"))
                { if (bSigns.Count < 12) bSigns.Add(go); }
                if (compact.Contains("D區入口") || compact.Contains("ZONED") || compact.Contains("ENTRANCED"))
                { if (dSigns.Count < 12) dSigns.Add(go); }
            }
            status = "只讀搜尋完成。B 區招牌：" + bSigns.Count + "；D 區招牌：" + dSigns.Count +
                "。按『定位招牌』，到 Scene 視窗找到入口，再選門板本身；招牌不能當作門板。";
        }

        private static void ShowSignList(string title, List<GameObject> signs)
        {
            EditorGUILayout.LabelField(title + "：" + signs.Count, EditorStyles.miniBoldLabel);
            foreach (GameObject sign in signs)
            {
                if (sign == null) continue;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(PathOf(sign.transform), EditorStyles.miniLabel);
                if (GUILayout.Button("定位招牌", GUILayout.Width(76))) Focus(sign);
                EditorGUILayout.EndHorizontal();
            }
        }

        private static Bounds LocalBounds(Transform origin, Renderer[] rs)
        {
            bool any = false;
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (Renderer r in rs)
            {
                if (r == null) continue;
                Bounds wr = r.bounds;
                Vector3 c = wr.center, e = wr.extents;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 point = origin.InverseTransformPoint(c + Vector3.Scale(e, new Vector3(x, y, z)));
                    if (!any) { b = new Bounds(point, Vector3.zero); any = true; }
                    else b.Encapsulate(point);
                }
            }
            return b;
        }

        private static bool TryDoor(GameObject target, Scene scene, bool permitConverted,
            out DoorInfo info, out string error)
        {
            info = new DoorInfo();
            error = null;
            if (target == null || target.scene != scene) { error = "未指定本關的原始門板。"; return false; }
            if (target.GetComponent<RectTransform>() != null || target.GetComponentInParent<Canvas>() != null)
            { error = "選到 UI，不是 3D 門板。"; return false; }
            if (!permitConverted && (target.GetComponent<SDGTargetedGateRecordV3>() != null ||
                target.GetComponent<SDGTargetedLiftGateV3>() != null || target.GetComponents<MonoBehaviour>().Any(m => m != null && m.GetType().Name == "SDGVerticalShutterDoor") ||
                target.transform.Find("SDG_UpwardIronShutter") != null))
            { error = "此門已由舊版或新版轉換過，不能再次轉換。"; return false; }
            Renderer[] rs = target.GetComponentsInChildren<Renderer>(true)
                .Where(r => r != null && !(r is ParticleSystemRenderer) && !(r is TrailRenderer)).ToArray();
            if (rs.Length == 0 || rs.Length > 16) { error = "門板 Renderer 數量不合理（" + rs.Length + "）。"; return false; }
            Bounds b = LocalBounds(target.transform, rs);
            float w = Mathf.Max(b.size.x, b.size.z);
            float depth = Mathf.Min(b.size.x, b.size.z);
            if (w < 0.85f || w > 9.0f || b.size.y < 1.6f || b.size.y > 7f || depth > 1.2f || w < depth * 1.3f)
            { error = "尺寸不像一片門（" + b.size.ToString("F2") + "），請選真正門板 Mesh，非門框／牆／樓梯。"; return false; }
            info.target = target;
            info.renderers = rs;
            info.colliders = target.GetComponentsInChildren<Collider>(true).Where(c => c != null && !c.isTrigger).ToArray();
            info.bounds = b;
            info.widthAlongZ = b.size.z > b.size.x;
            info.width = w;
            info.height = b.size.y;
            info.thickness = depth;
            info.scripts = target.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(m => m != null && !(m is SDGTargetedGateRecordV3) && !(m is SDGTargetedLiftGateV3))
                .Select(m => m.GetType().Name).Distinct().ToArray();
            return true;
        }

        private static void DrawPreview(string id, GameObject target, Scene scene)
        {
            DoorInfo info;
            string err;
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(id + " 區門板", target == null ? "尚未指定" : PathOf(target.transform), EditorStyles.wordWrappedMiniLabel);
            if (target != null)
            {
                if (TryDoor(target, scene, true, out info, out err))
                    EditorGUILayout.LabelField("大小：寬 " + info.width.ToString("F2") + " m / 高 " + info.height.ToString("F2") +
                        " m；渲染器 " + info.renderers.Length + "；腳本：" + (info.scripts.Length == 0 ? "無" : string.Join(",", info.scripts)), EditorStyles.wordWrappedMiniLabel);
                else EditorGUILayout.HelpBox(err, MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
        }

        private static string Backup(Scene scene)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            string project = Directory.GetParent(Application.dataPath).FullName;
            string from = Path.Combine(project, scene.path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(from)) throw new IOException("正式場景檔不存在：" + from);
            string folder = Path.Combine(project, "SDG_FrontEnd_Backups", "CleanBDGateV3_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            string dest = Path.Combine(folder, scene.path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(from, dest, false);
            File.WriteAllText(Path.Combine(folder, "RESTORE.txt"),
                "Scene=" + scene.path + Environment.NewLine + "Backup=" + dest + Environment.NewLine +
                "Close Unity before copying backup .unity over project .unity. Do not replace any .meta file.");
            return dest;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Material Material(string file, Color color)
        {
            string path = Materials + "/" + file + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            EnsureFolder(Materials);
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("找不到 Standard 或 URP Lit shader。");
            Material m = new Material(shader);
            m.color = color;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.85f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static void AddCube(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(go, "Create targeted B/D gate geometry");
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Undo.DestroyObjectImmediate(collider);
        }

        private static void MakeGate(DoorInfo info, string id, bool proximity, Material dark, Material trim)
        {
            GameObject root = info.target;
            GameObject host = new GameObject("SDG_" + id + "_LiftGate_V3");
            Undo.RegisterCreatedObjectUndo(host, "Create exact " + id + " gate");
            host.transform.SetParent(root.transform, false);
            host.transform.localPosition = info.bounds.center;
            host.transform.localRotation = info.widthAlongZ ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            host.transform.localScale = Vector3.one;
            GameObject panel = new GameObject("SlidingIronPanel");
            Undo.RegisterCreatedObjectUndo(panel, "Create lifting leaf");
            panel.transform.SetParent(host.transform, false);
            panel.transform.localPosition = Vector3.zero;
            float w = info.width, h = info.height, d = Mathf.Clamp(info.thickness, 0.12f, 0.35f);
            AddCube("CorrugatedIronPanel", panel.transform, Vector3.zero, new Vector3(w, h, d), dark);
            int count = Mathf.Clamp(Mathf.CeilToInt(h / 0.27f), 6, 36);
            for (int i = 0; i <= count; i++)
                AddCube("HorizontalRib_" + i, panel.transform,
                    new Vector3(0f, -h * 0.5f + i * h / count, -d * 0.58f),
                    new Vector3(w + 0.025f, 0.05f, 0.04f), trim);
            BoxCollider blocker = Undo.AddComponent<BoxCollider>(panel);
            blocker.center = Vector3.zero;
            blocker.size = new Vector3(w, h, d);
            AddCube("LeftGuideRail", host.transform, new Vector3(-w * 0.5f - 0.075f, 0, 0), new Vector3(0.15f, h + 0.12f, d + 0.08f), trim);
            AddCube("RightGuideRail", host.transform, new Vector3(w * 0.5f + 0.075f, 0, 0), new Vector3(0.15f, h + 0.12f, d + 0.08f), trim);
            AddCube("UpperRollerHousing", host.transform, new Vector3(0f, h * 0.5f + 0.14f, 0), new Vector3(w + 0.30f, 0.28f, d + 0.16f), dark);
            SDGTargetedGateRecordV3 record = Undo.AddComponent<SDGTargetedGateRecordV3>(root);
            record.Capture(id, info.renderers, info.colliders, host);
            SDGTargetedLiftGateV3 controller = Undo.AddComponent<SDGTargetedLiftGateV3>(root);
            controller.Configure(panel.transform, h + 0.1f, proximity, info.renderers, info.colliders);
            foreach (Renderer r in info.renderers)
                if (r != null) { Undo.RecordObject(r, "Hide selected original door leaf"); r.enabled = false; }
            foreach (Collider c in info.colliders)
                if (c != null) { Undo.RecordObject(c, "Disable original door solid collision"); c.enabled = false; }
            EditorUtility.SetDirty(record);
            EditorUtility.SetDirty(controller);
        }

        private void ConvertBoth(Scene scene)
        {
            if (bDoor == null || dDoor == null || bDoor == dDoor)
            { status = "請指定兩個不同的 B / D 原始門板，不能留空或選同一個物件。"; return; }
            DoorInfo b, d;
            string be, de;
            if (!TryDoor(bDoor, scene, false, out b, out be)) { status = "B 門：" + be; return; }
            if (!TryDoor(dDoor, scene, false, out d, out de)) { status = "D 門：" + de; return; }
            if (bDoor.transform.IsChildOf(dDoor.transform) || dDoor.transform.IsChildOf(bDoor.transform))
            { status = "兩個目標有父子關係，可能選到整個建築；請重新選取兩片獨立門板。"; return; }
            if ((b.scripts.Length > 0 || d.scripts.Length > 0) && !acknowledgeScriptedDoors)
            { status = "選取門板包含控制腳本，請先核對原門禁流程，再勾選已檢查腳本。"; return; }
            if ((b.scripts.Length > 0 || d.scripts.Length > 0) && autoOpen)
            { status = "門板含遊戲腳本，為避免繞過原門禁，請取消『靠近自動開門』再轉換。"; return; }
            string question = "將只修改 C_Main.unity 中兩個指定的原門板：\n\nB：" + PathOf(bDoor.transform) +
                "\nD：" + PathOf(dDoor.transform) + "\n\nB：" + b.bounds.size.ToString("F2") +
                "\nD：" + d.bounds.size.ToString("F2") +
                "\n\n靠近開門：" + (autoOpen ? "是" : "否（需要外部 OpenDoor/CloseDoor）") +
                "\n其他任何門、樓梯、箱門、護欄不修改。\n\n確定？";
            if (!EditorUtility.DisplayDialog("核對 B / D 入口後才套用", question, "備份後只轉換這兩道", "取消")) return;
            try
            {
                string backup = Backup(scene);
                if (backup == null) { status = "已取消，未修改。"; return; }
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("SDG convert exact original B and D entrances");
                try
                {
                    Material dark = Material("BD_IronGraphite_V3", new Color(0.18f, 0.22f, 0.25f));
                    Material trim = Material("BD_BrushedSteel_V3", new Color(0.41f, 0.46f, 0.49f));
                    MakeGate(b, "B", autoOpen, dark, trim);
                    MakeGate(d, "D", autoOpen, dark, trim);
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    status = "SUCCESS：只轉換指定的 B / D 入口。場景備份：" + backup + "。請 Play 驗收門升降與碰撞。";
                    Debug.Log("[SDG B/D Gate V3] " + status);
                }
                catch
                {
                    Undo.RevertAllDownToGroup(group);
                    throw;
                }
            }
            catch (Exception e) { status = "轉換失敗，請檢查 Console 與備份：" + e.Message; Debug.LogException(e); }
        }

        private void RevertOne(Scene scene, GameObject target)
        {
            if (target == null || target.scene != scene)
            { status = "請先指定本關要還原的 B 或 D 門板。"; return; }
            SDGTargetedGateRecordV3 record = target.GetComponent<SDGTargetedGateRecordV3>();
            SDGTargetedLiftGateV3 driver = target.GetComponent<SDGTargetedLiftGateV3>();
            if (record == null || driver == null || record.GeneratedHost == null)
            { status = "此物件不是 v3 新轉換的門，未進行還原。"; return; }
            if (!EditorUtility.DisplayDialog("確認還原單一道門", "只還原：" + PathOf(target.transform), "備份並還原", "取消")) return;
            try
            {
                string backup = Backup(scene);
                if (backup == null) return;
                Renderer[] rs = record.Renderers; bool[] rf = record.RendererStates;
                Collider[] cs = record.Colliders; bool[] cf = record.ColliderStates;
                for (int i = 0; i < rs.Length && i < rf.Length; i++)
                    if (rs[i] != null) { Undo.RecordObject(rs[i], "Restore original door renderer"); rs[i].enabled = rf[i]; }
                for (int i = 0; i < cs.Length && i < cf.Length; i++)
                    if (cs[i] != null) { Undo.RecordObject(cs[i], "Restore original door collision"); cs[i].enabled = cf[i]; }
                Undo.DestroyObjectImmediate(record.GeneratedHost);
                Undo.DestroyObjectImmediate(driver);
                Undo.DestroyObjectImmediate(record);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                status = "已還原單一道門，備份：" + backup;
            }
            catch (Exception e) { status = "還原失敗：" + e.Message; Debug.LogException(e); }
        }
    }
}
#endif
