using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace SDGAdventure.FrontEnd
{
    /// <summary>
    /// Read-only copy of the real v3.3 player panel. All visual elements, the
    /// avatar image, name field, XP text and live HP fill originate from the
    /// existing front-end panel, not from newly drawn artwork.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SDGSharedLevelPlayerHUD : MonoBehaviour
    {
        public TextMeshProUGUI experienceSource;
        public Image originalExperienceFill;

        private float nextRefresh;
        private int lastExperience = int.MinValue;
        private int lastExperienceMax = int.MinValue;

        private void OnEnable()
        {
            lastExperience = int.MinValue;
            lastExperienceMax = int.MinValue;
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.2f;
            Refresh();
        }

        private void Refresh()
        {
            int max = Mathf.Max(1, PlayerPrefs.GetInt(SDGFrontEndSaveBridge.KeyXPMax, 100));
            int current = Mathf.Max(0, PlayerPrefs.GetInt(SDGFrontEndSaveBridge.KeyXP, 0));
            if (current == lastExperience && max == lastExperienceMax) return;
            lastExperience = current;
            lastExperienceMax = max;

            // The original v3.3 CJK legacy mirror reads this TMP field.
            // Updating it preserves the currently verified mini-slot layout.
            if (experienceSource != null)
                experienceSource.text = current.ToString("N0") + " / " + max.ToString("N0");

            if (originalExperienceFill != null)
                originalExperienceFill.fillAmount = Mathf.Clamp01((float)current / max);
        }
    }

    /// <summary>
    /// Automatically displays one shared player panel when any Level01-04 scene
    /// is loaded, including direct Play of a level inside Unity Editor.
    /// Hides it on the front-end/other scenes. Never edits gameplay scenes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SDGSharedLevelHUDRuntime : MonoBehaviour
    {
        private const string PrefabResourceName = "SDG_LevelPlayerHUD";
        private static SDGSharedLevelHUDRuntime instance;
        private GameObject holder;
        private bool triedToLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            GameObject canvasObject = new GameObject("SDG Shared Level HUD Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(CanvasGroup));
            canvasObject.AddComponent<SDGSharedLevelHUDRuntime>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Gameplay movement/shooting must not be blocked by the display.
            CanvasGroup group = GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.ignoreParentGroups = true;
        }

        private void OnEnable()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void Start()
        {
            UpdateVisibility(SceneManager.GetActiveScene());
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateVisibility(scene);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            UpdateVisibility(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void UpdateVisibility(Scene scene)
        {
            bool isLevel = IsPlayableLevel(scene);
            // An auxiliary, non-level scene may be loaded additively while a
            // gameplay scene is still active. Do not hide its player panel.
            if (!isLevel)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (IsPlayableLevel(SceneManager.GetSceneAt(i)))
                    {
                        isLevel = true;
                        break;
                    }
            if (!isLevel)
            {
                if (holder != null) holder.SetActive(false);
                return;
            }
            EnsureHUD();
            if (holder != null) holder.SetActive(true);
        }

        private static bool IsPlayableLevel(Scene scene)
        {
            if (!scene.IsValid()) return false;
            string p = (scene.path ?? string.Empty).Replace('\\', '/');
            for (int i = 1; i <= 4; i++)
            {
                string token = "/_Modules_/02_Levels/Level0" + i + "/";
                if (p.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private void EnsureHUD()
        {
            if (holder != null || triedToLoad) return;
            triedToLoad = true;
            GameObject template = Resources.Load<GameObject>(PrefabResourceName);
            if (template == null)
            {
                Debug.LogError("[SDG Shared HUD] Resources/" + PrefabResourceName +
                    ".prefab was not found. Run the shared HUD installer.");
                return;
            }

            GameObject root = new GameObject("Top-left Player Profile Anchor", typeof(RectTransform));
            holder = root;
            RectTransform anchor = root.GetComponent<RectTransform>();
            anchor.SetParent(transform, false);
            anchor.anchorMin = new Vector2(0f, 1f);
            anchor.anchorMax = new Vector2(0f, 1f);
            anchor.pivot = new Vector2(0f, 1f);
            anchor.anchoredPosition = new Vector2(24f, -24f);
            anchor.sizeDelta = new Vector2(480f, 191f);
            anchor.localScale = Vector3.one;

            GameObject panelRoot = Instantiate(template, anchor, false);
            panelRoot.name = "SDG Level Player Profile (v3.3 shared)";
            RectTransform rect = panelRoot.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }
            // No new GraphicRaycaster: this is a read-only FPS overlay.
            // Avatar/name selection remains available in the original menu.
        }
    }
}
