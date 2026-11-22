using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SDGAdventure.ExperienceV2
{
    /// <summary>
    /// In-game star-box finder: identifies real Level02 objects, points toward them, then guides
    /// players to the star platform and exit gate. No teleportation, reward, or quest mutation.
    /// Unknown "sobox" containers are explicitly labelled as an unverified box area.
    /// </summary>
    public sealed class SDGLevel02StarNavigator : MonoBehaviour
    {
        private sealed class Candidate { public Transform target; public int confidence; public string reason; }
        private readonly List<Candidate> boxCandidates = new List<Candidate>();
        private Camera cam;
        private Component plate, carry, gate;
        private Canvas canvas;
        private RectTransform markerRoot;
        private Text markerLabel, instructions, progress;
        private GameObject detailPanel;
        private bool navigatorVisible = true;
        private bool detailsVisible = true;
        private bool oldHelpHidden;
        private float rescanAt, cameraAt, helpAt, nextUiAt;
        private Vector3 currentPoint;
        private bool hasPoint;
        private string currentTargetTitle;

        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Color Mint = new Color(.47f, 1f, .81f);
        private static readonly Color White = new Color(.94f, 1f, .98f);

        private void Start()
        {
            BuildUI();
            rescanAt = 0f;
            helpAt = Time.unscaledTime + 2f;
        }

        private void Update()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.f2Key.wasPressedThisFrame)
                {
                    navigatorVisible = !navigatorVisible;
                    if (canvas != null) canvas.gameObject.SetActive(navigatorVisible);
                }
                if (Keyboard.current.f3Key.wasPressedThisFrame)
                {
                    detailsVisible = !detailsVisible;
                    if (detailPanel != null) detailPanel.SetActive(detailsVisible);
                }
            }
            if (!oldHelpHidden && Time.unscaledTime >= helpAt)
            {
                helpAt = Time.unscaledTime + .6f;
                HideOnlyTheV1Level02Help();
            }
            if (Time.unscaledTime >= cameraAt && (cam == null || !cam.enabled))
            {
                cameraAt = Time.unscaledTime + .75f;
                cam = FindPlayerCamera();
            }
            if (Time.unscaledTime >= rescanAt)
            {
                rescanAt = Time.unscaledTime + 1.75f;
                ScanTargets();
            }
            if (!navigatorVisible) return;
            if (Time.unscaledTime >= nextUiAt)
            {
                nextUiAt = Time.unscaledTime + .20f;
                UpdateGuide();
            }
            UpdateWorldMarker();
        }

        private Camera FindPlayerCamera()
        {
            foreach (Camera c in Camera.allCameras)
            {
                if (c == null || !c.enabled || c.gameObject.scene != gameObject.scene) continue;
                for (Transform parent = c.transform; parent != null; parent = parent.parent)
                {
                    var behaviours = parent.GetComponents<MonoBehaviour>();
                    foreach (var behaviour in behaviours)
                        if (behaviour != null && behaviour.GetType().Name == "SharedFirstPersonController") return c;
                }
            }
            return null;
        }

        private void HideOnlyTheV1Level02Help()
        {
            // v1 help is too vague; suppress ONLY this specific tutorial, not the actual quest HUD.
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name != "SDG_Help_Overlay") continue;
                root.SetActive(false);
                oldHelpHidden = true;
                return;
            }
        }

        private void ScanTargets()
        {
            boxCandidates.Clear();
            plate = null;
            carry = null;
            gate = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (var b in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (b == null || b.gameObject.scene != gameObject.scene) continue;
                string type = b.GetType().Name;
                if (type == "StarGoalPlate") plate = b;
                else if (type == "Level02BoxCarryController") carry = b;
                else if (type == "SlidingStarGate") gate = b;
                if (!b.gameObject.activeInHierarchy) continue;
                if (type == "StarGoalPlate" || type == "SlidingStarGate") continue;
                if ((type.IndexOf("Star", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (type.IndexOf("Box", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Carry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Pickup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Crate", StringComparison.OrdinalIgnoreCase) >= 0)))
                    AddCandidate(b.transform, 100, "星星箱互動物件");
                else if (type.IndexOf("Star", StringComparison.OrdinalIgnoreCase) >= 0 &&
                         ImplementsInteractable(b.GetType()))
                    AddCandidate(b.transform, 95, "可互動的星星物件");
            }
            // Known scene-object name from the user's readonly scan: 'sobox'.
            // Because the original script is not available, this is a *possible* storage area, not a certified box.
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                ScanNames(root.transform);
        }

        private void ScanNames(Transform transform)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy) return;
            string name = transform.name.ToLowerInvariant();
            bool exactArea = name == "sobox" || name.StartsWith("sobox (");
            bool labeledBox = (name.Contains("star") && (name.Contains("box") || name.Contains("crate"))) || name.Contains("星星箱");
            if (labeledBox || exactArea) AddCandidate(transform, exactArea ? 45 : 80,
                exactArea ? "疑似箱子區（sobox）" : "星星箱命名物件");
            foreach (Transform child in transform) ScanNames(child);
        }

        private static bool ImplementsInteractable(Type type)
        {
            foreach (var iface in type.GetInterfaces())
                if (iface.Name == "IPlayerInteractable") return true;
            return false;
        }

        private void AddCandidate(Transform target, int confidence, string reason)
        {
            if (target == null || target.GetComponentInChildren<Renderer>() == null) return;
            for (int i = 0; i < boxCandidates.Count; i++)
            {
                if (boxCandidates[i].target != target) continue;
                if (boxCandidates[i].confidence < confidence) { boxCandidates[i].confidence = confidence; boxCandidates[i].reason = reason; }
                return;
            }
            boxCandidates.Add(new Candidate { target = target, confidence = confidence, reason = reason });
        }

        private void UpdateGuide()
        {
            if (instructions == null || progress == null) return;
            hasPoint = false;
            Vector3 from = cam != null ? cam.transform.position : Vector3.zero;
            int done = ReadInt(plate, "PlacedStars", 0);
            int total = ReadInt(plate, "TotalStars", 0);
            bool allPlaced = ReadBool(plate, "IsComplete", false);
            bool carrying = HasHeldBox(carry);
            progress.text = "星星箱已放置：" + done + "/" + (total > 0 ? total.ToString() : "?");

            if (cam == null)
            {
                instructions.text = "正在尋找玩家攝影機…\nF2 切換導航｜F3 顯示完整說明";
                return;
            }
            if (allPlaced)
            {
                if (gate != null)
                {
                    SetTarget(gate.transform, "出口閘門");
                    instructions.text = "星星已全部放妥！前往出口閘門。" + DescribeDirection(from) + "\nF2 導航開關｜F3 詳細說明";
                }
                else instructions.text = "星星已全部放妥！尋找已開啟的閘門通關。";
                return;
            }
            if (carrying)
            {
                if (plate != null)
                {
                    SetTarget(plate.transform, "星星平台");
                    instructions.text = "你正在搬運箱子。跟隨◇前往星星平台。" + DescribeDirection(from) +
                                        "\n靠近平台，依遊戲中的操作提示放置。";
                }
                else instructions.text = "正在搬運箱子，請尋找星星平台並放置。";
                return;
            }

            Candidate best = null;
            float bestScore = float.PositiveInfinity;
            foreach (Candidate c in boxCandidates)
            {
                if (c.target == null || !c.target.gameObject.activeInHierarchy) continue;
                Vector3 pos = TargetPoint(c.target);
                float dist = Vector3.Distance(from, pos);
                // Prefer genuinely interactable star cargo over the unverified 'sobox' container.
                float score = dist + (100 - c.confidence) * .45f;
                if (score < bestScore) { bestScore = score; best = c; }
            }
            if (best == null)
            {
                instructions.text = "沒有偵測到箱子目標。請移動到另一區域，系統每兩秒自動掃描。" +
                                    "\n找到星星箱後，準星對準並依畫面中央的互動鍵提示拾取。";
                return;
            }
            SetTarget(best.target, best.confidence >= 80 ? "星星箱" : "可能的箱子區");
            instructions.text = (best.confidence >= 80 ? "跟隨◇尋找星星箱。" : "◇指向疑似箱子區，請查看周圍有無星星箱。") +
                                DescribeDirection(from) + "\n靠近後將準星對準箱子，依畫面中央互動提示拾起。";
        }

        private void SetTarget(Transform transform, string label)
        {
            if (transform == null) return;
            hasPoint = true;
            currentPoint = TargetPoint(transform);
            currentTargetTitle = label;
        }

        private static Vector3 TargetPoint(Transform transform)
        {
            Renderer own = transform.GetComponent<Renderer>();
            if (own != null) return own.bounds.center + Vector3.up * .35f;
            Renderer[] children = transform.GetComponentsInChildren<Renderer>(false);
            if (children.Length == 0) return transform.position + Vector3.up;
            Bounds combined = children[0].bounds;
            for (int i = 1; i < children.Length; i++) combined.Encapsulate(children[i].bounds);
            return combined.center + Vector3.up * .4f;
        }

        private string DescribeDirection(Vector3 from)
        {
            if (!hasPoint || cam == null) return "";
            Vector3 delta = currentPoint - from;
            float distance = delta.magnitude;
            delta.y = 0f;
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (delta.sqrMagnitude < .001f || forward.sqrMagnitude < .001f) return " 距離約 " + distance.ToString("F0") + "m。";
            float angle = Vector3.SignedAngle(forward, delta, Vector3.up);
            string side = Mathf.Abs(angle) < 26f ? "前方" :
                angle >= 26f && angle < 75f ? "右前方" :
                angle <= -26f && angle > -75f ? "左前方" :
                angle >= 75f && angle < 120f ? "右方" :
                angle <= -75f && angle > -120f ? "左方" :
                angle >= 120f && angle < 160f ? "右後方" :
                angle <= -120f && angle > -160f ? "左後方" : "後方";
            return " 位於" + side + "，約 " + distance.ToString("F0") + "m。";
        }

        private void UpdateWorldMarker()
        {
            if (markerRoot == null || markerLabel == null || cam == null || !hasPoint)
            {
                if (markerRoot != null) markerRoot.gameObject.SetActive(false);
                return;
            }
            markerRoot.gameObject.SetActive(true);
            Vector3 screen = cam.WorldToScreenPoint(currentPoint);
            if (screen.z <= 0f)
            {
                screen.x = Screen.width - screen.x;
                screen.y = Screen.height - screen.y;
            }
            float px = Mathf.Clamp(screen.x, 150f, Mathf.Max(151f, Screen.width - 150f));
            float py = Mathf.Clamp(screen.y, 165f, Mathf.Max(166f, Screen.height - 150f));
            RectTransform canvasRect = (RectTransform)canvas.transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(px, py), null, out Vector2 local))
                markerRoot.anchoredPosition = local;
            markerLabel.text = "◇ " + currentTargetTitle + "  " + Vector3.Distance(cam.transform.position, currentPoint).ToString("F0") + "m";
        }

        private static object Member(Component owner, string name)
        {
            if (owner == null) return null;
            Type t = owner.GetType();
            try
            {
                PropertyInfo p = t.GetProperty(name, Flags);
                if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(owner, null);
                FieldInfo f = t.GetField(name, Flags);
                return f == null ? null : f.GetValue(owner);
            }
            catch (Exception) { return null; }
        }
        private static bool ReadBool(Component owner, string name, bool fallback) => Member(owner, name) is bool value ? value : fallback;
        private static int ReadInt(Component owner, string name, int fallback)
        {
            object value = Member(owner, name);
            return value is int n ? n : fallback;
        }
        private static bool HasHeldBox(Component owner)
        {
            object held = Member(owner, "HeldBox");
            if (held == null) return false;
            if (held is bool state) return state;
            if (held is UnityEngine.Object obj) return obj != null;
            return true;
        }

        private void BuildUI()
        {
            canvas = SDGExperienceV2UI.Canvas("SDG_StarBoxNavigatorV2_NoGameplayRaycast", 1380);
            SceneManager.MoveGameObjectToScene(canvas.gameObject, gameObject.scene);
            RectTransform panel = SDGExperienceV2UI.Rect("RightTopNavigator", canvas.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(690f, 235f), new Vector2(-28f, -130f));
            detailPanel = panel.gameObject;
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(.015f, .12f, .14f, .9f);
            background.raycastTarget = false;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.15f, .89f, .68f, .95f);
            outline.effectDistance = new Vector2(2f, -2f);
            SDGExperienceV2UI.Text("Title", panel, "第二關｜尋找星星箱", 29, Mint, TextAnchor.MiddleLeft,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(640f, 44f), new Vector2(0f, 84f));
            instructions = SDGExperienceV2UI.Text("Instructions", panel,
                "準備搜尋實際星星箱…", 19, White, TextAnchor.UpperLeft,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(638f, 113f), new Vector2(0f, 1f));
            progress = SDGExperienceV2UI.Text("Progress", panel, "星星箱已放置：0/?", 20, Mint, TextAnchor.MiddleLeft,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(638f, 32f), new Vector2(0f, -69f));
            SDGExperienceV2UI.Text("Shortcuts", panel, "F2：開關導航　F3：展開／收合說明", 17, Mint,
                TextAnchor.MiddleLeft, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(638f, 26f), new Vector2(0f, -100f));

            markerRoot = SDGExperienceV2UI.Rect("WorldTargetMarker", canvas.transform,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(340f, 64f), Vector2.zero);
            markerLabel = SDGExperienceV2UI.Text("MarkerLabel", markerRoot, "◇ 星星箱", 24, Mint, TextAnchor.MiddleCenter,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(340f, 60f), Vector2.zero);
            markerRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }
    }
}
