using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SDGAdventure.Experience
{
    /// <summary>
    /// Camera-local first-person tool models (not world pickups). F6 swaps grip models.
    /// Existing Level01Input continues to own E interaction and left-click digging.
    /// Animations react to those inputs without consuming or replacing input actions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SDGLevel01HeldTools : MonoBehaviour
    {
        private GameObject clampRoot, shovelRoot;
        private Transform clampFingerA, clampFingerB;
        private Canvas hudCanvas;
        private Material steel, darkSteel, wood, glove, accent;
        private bool shovelSelected = true;
        private float actionAt = -10f;
        private bool acting;
        private Quaternion clampRest, shovelRest;

        private void Start()
        {
            steel = Mat(new Color(.54f, .68f, .68f));
            darkSteel = Mat(new Color(.19f, .27f, .31f));
            wood = Mat(new Color(.42f, .27f, .13f));
            glove = Mat(new Color(.21f, .28f, .27f));
            accent = Mat(new Color(.07f, .75f, .54f));
            clampRoot = BuildClamp();
            shovelRoot = BuildShovel();
            clampRest = clampRoot.transform.localRotation;
            shovelRest = shovelRoot.transform.localRotation;
            ApplySelection();
            MakeHint();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
            {
                shovelSelected = !shovelSelected;
                ApplySelection();
            }
            // Only animate the current tool. The existing project controls ALL interactions and rewards.
            if (shovelSelected && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                actionAt = Time.unscaledTime;
                acting = true;
            }
            if (!shovelSelected && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                actionAt = Time.unscaledTime;
                acting = true;
            }
            float elapsed = Time.unscaledTime - actionAt;
            float strike = acting && elapsed < .32f ? Mathf.Sin(elapsed / .32f * Mathf.PI) : 0f;
            if (elapsed >= .32f) acting = false;
            float breathing = Mathf.Sin(Time.unscaledTime * 2f) * .003f;
            if (shovelRoot != null)
            {
                shovelRoot.transform.localPosition = new Vector3(.37f, -.36f + breathing - strike * .055f, .78f);
                shovelRoot.transform.localRotation = shovelRest * Quaternion.Euler(strike * 21f, 0, 0);
            }
            if (clampRoot != null)
            {
                clampRoot.transform.localPosition = new Vector3(.37f, -.35f + breathing, .76f);
                clampRoot.transform.localRotation = clampRest * Quaternion.Euler(-strike * 8f, 0, 0);
                if (clampFingerA != null) clampFingerA.localRotation = Quaternion.Euler(0, -strike * 14f, 0);
                if (clampFingerB != null) clampFingerB.localRotation = Quaternion.Euler(0, strike * 14f, 0);
            }
        }

        private void ApplySelection()
        {
            if (clampRoot != null) clampRoot.SetActive(!shovelSelected);
            if (shovelRoot != null) shovelRoot.SetActive(shovelSelected);
            if (hudCanvas != null)
            {
                var label = hudCanvas.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = shovelSelected ? "手持：鐵鏟｜F6 切換夾子｜挖掘沿用原本按鍵" :
                        "手持：夾子｜F6 切換鐵鏟｜夾取沿用原本互動鍵";
            }
        }

        private GameObject BuildClamp()
        {
            GameObject root = NewParent("SDG_HandHeld_Clamp");
            root.transform.localPosition = new Vector3(.37f, -.35f, .76f);
            root.transform.localRotation = Quaternion.Euler(-17f, -24f, 27f);
            Piece(root.transform, "Grip", PrimitiveType.Cylinder, new Vector3(0, -.10f, .04f),
                new Vector3(.027f, .14f, .027f), wood, Quaternion.Euler(90, 0, 0));
            Piece(root.transform, "Hinge", PrimitiveType.Sphere, new Vector3(0, .02f, .05f),
                new Vector3(.065f, .065f, .065f), darkSteel);
            var a = NewChild("MovingJawLeft", root.transform);
            var b = NewChild("MovingJawRight", root.transform);
            a.transform.localPosition = new Vector3(-.045f, .03f, .05f);
            b.transform.localPosition = new Vector3(.045f, .03f, .05f);
            Piece(a.transform, "LeftArm", PrimitiveType.Cube, new Vector3(0, .13f, 0),
                new Vector3(.035f, .27f, .034f), steel);
            Piece(b.transform, "RightArm", PrimitiveType.Cube, new Vector3(0, .13f, 0),
                new Vector3(.035f, .27f, .034f), steel);
            Piece(a.transform, "LeftTip", PrimitiveType.Cube, new Vector3(.016f, .266f, 0),
                new Vector3(.054f, .035f, .045f), accent);
            Piece(b.transform, "RightTip", PrimitiveType.Cube, new Vector3(-.016f, .266f, 0),
                new Vector3(.054f, .035f, .045f), accent);
            clampFingerA = a.transform; clampFingerB = b.transform;
            AddGlove(root.transform);
            return root;
        }

        private GameObject BuildShovel()
        {
            GameObject root = NewParent("SDG_HandHeld_Shovel");
            root.transform.localPosition = new Vector3(.37f, -.36f, .78f);
            root.transform.localRotation = Quaternion.Euler(12f, 8f, -28f);
            Piece(root.transform, "Handle", PrimitiveType.Cylinder, new Vector3(0, .025f, 0),
                new Vector3(.024f, .30f, .024f), wood);
            Piece(root.transform, "GripEnd", PrimitiveType.Cylinder, new Vector3(0, -.26f, 0),
                new Vector3(.042f, .055f, .042f), darkSteel);
            Piece(root.transform, "Neck", PrimitiveType.Cylinder, new Vector3(0, .28f, 0),
                new Vector3(.035f, .09f, .035f), steel);
            Piece(root.transform, "Blade", PrimitiveType.Cube, new Vector3(0, .41f, 0),
                new Vector3(.23f, .24f, .045f), steel);
            Piece(root.transform, "BladeEdge", PrimitiveType.Cube, new Vector3(0, .53f, 0),
                new Vector3(.20f, .028f, .055f), darkSteel);
            AddGlove(root.transform);
            return root;
        }

        private void AddGlove(Transform root)
        {
            Piece(root, "RightHandGlove", PrimitiveType.Sphere, new Vector3(.045f, -.16f, -.04f),
                new Vector3(.12f, .17f, .13f), glove);
            Piece(root, "RightSleeve", PrimitiveType.Cylinder, new Vector3(.066f, -.29f, -.05f),
                new Vector3(.073f, .13f, .073f), darkSteel, Quaternion.Euler(0, 0, -15));
        }

        private static GameObject NewChild(string name, Transform parent)
        {
            var result = new GameObject(name);
            result.transform.SetParent(parent, false);
            result.layer = 2; // Ignore Raycast: never interfere with FPS hit detection.
            return result;
        }

        private GameObject NewParent(string name) { return NewChild(name, transform); }

        private static void Piece(Transform parent, string name, PrimitiveType shape, Vector3 position,
            Vector3 scale, Material material, Quaternion? rotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.layer = 2;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation ?? Quaternion.identity;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static Material Mat(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        private void MakeHint()
        {
            var canvasRoot = SDGExperienceUI.Rect("SDG_ToolHint", null);
            SceneManager.MoveGameObjectToScene(canvasRoot.gameObject, gameObject.scene);
            hudCanvas = canvasRoot.gameObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 1250;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            RectTransform panel = SDGExperienceUI.Rect("ToolHintPanel", canvasRoot);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0);
            panel.pivot = new Vector2(.5f, 0);
            panel.anchoredPosition = new Vector2(0, 105);
            panel.sizeDelta = new Vector2(670, 43);
            Image background = panel.gameObject.AddComponent<Image>();
            background.raycastTarget = false;
            background.color = new Color(.02f, .15f, .14f, .77f);
            SDGExperienceUI.Text(panel, "ToolLabel", "", 19, Vector2.zero, new Vector2(650, 40),
                new Color(.75f, 1f, .89f));
            ApplySelection();
        }

        private void OnDestroy()
        {
            if (clampRoot != null) Destroy(clampRoot);
            if (shovelRoot != null) Destroy(shovelRoot);
            if (hudCanvas != null) Destroy(hudCanvas.gameObject);
            if (steel != null) Destroy(steel);
            if (darkSteel != null) Destroy(darkSteel);
            if (wood != null) Destroy(wood);
            if (glove != null) Destroy(glove);
            if (accent != null) Destroy(accent);
        }
    }
}
