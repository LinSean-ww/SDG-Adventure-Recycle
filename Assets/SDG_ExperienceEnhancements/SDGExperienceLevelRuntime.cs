using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SDGAdventure.SharedPlayer;

namespace SDGAdventure.Experience
{
    /// <summary>Lightweight per-scene owner, destroyed automatically at unload.</summary>
    public sealed class SDGExperienceLevelRuntime : MonoBehaviour
    {
        private int level;
        private SDGLevel01HeldTools tools;
        private SDGLevel04BossOverlay bossUI;
        private CanvasGroup guide;
        private GameObject guideCanvasObject;
        private float guideEnd;
        private float nextSearch;

        public void Configure(int targetLevel) { level = targetLevel; }

        private void Start()
        {
            if (level == 2 || level == 4) CreateGuide();
            nextSearch = 0f;
        }

        private void Update()
        {
            if (level == 2 || level == 4)
            {
                if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame && guide != null)
                {
                    bool turnOn = guide.alpha < .05f;
                    guide.alpha = turnOn ? 1f : 0f;
                    if (turnOn) guideEnd = Time.unscaledTime + 18f;
                }
                if (guide != null && guide.alpha > 0 && Time.unscaledTime > guideEnd)
                    guide.alpha = 0f;
            }

            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + .5f;

            if (level == 1 && tools == null)
            {
                Camera playerCamera = FindGameplayCamera();
                if (playerCamera != null)
                {
                    tools = playerCamera.GetComponent<SDGLevel01HeldTools>();
                    if (tools == null) tools = playerCamera.gameObject.AddComponent<SDGLevel01HeldTools>();
                }
            }
            else if (level == 4 && bossUI == null)
            {
                BossHealth found = null;
                foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                {
                    found = root.GetComponentInChildren<BossHealth>(true);
                    if (found != null) break;
                }
                if (found != null)
                {
                    bossUI = gameObject.AddComponent<SDGLevel04BossOverlay>();
                    bossUI.Bind(found);
                }
            }
        }

        private Camera FindGameplayCamera()
        {
            // Only adopt the camera attached to the Level01 player; never a UI preview camera.
            var all = FindObjectsOfType<Camera>(true);
            foreach (Camera cam in all)
                if (cam != null && cam.enabled && cam.gameObject.scene == gameObject.scene &&
                    cam.GetComponentInParent<SharedFirstPersonController>() != null)
                    return cam;
            return null;
        }

        private void OnDestroy()
        {
            if (guideCanvasObject != null) Destroy(guideCanvasObject);
        }

        private void CreateGuide()
        {
            GameObject canvasGo = new GameObject("SDG_Help_Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(canvasGo, gameObject.scene);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1300;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            guideCanvasObject = canvasGo;
            guide = canvasGo.AddComponent<CanvasGroup>();
            guide.blocksRaycasts = false;
            guide.interactable = false;
            guideEnd = Time.unscaledTime + 18f;

            RectTransform panel = SDGExperienceUI.Rect("HelpPanel", canvasGo.transform);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 1f);
            panel.pivot = new Vector2(.5f, 1f);
            panel.anchoredPosition = new Vector2(0, -118f);
            panel.sizeDelta = new Vector2(760f, 215f);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(.025f, .145f, .155f, .92f);
            bg.raycastTarget = false;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.1f, .83f, .67f, .85f);
            outline.effectDistance = new Vector2(2f, -2f);

            SDGExperienceUI.Text(panel, "Title", level == 2 ? "第二關｜星星箱搬運任務" : "第四關｜機械 Boss 與資源回收", 30,
                new Vector2(0, 66f), new Vector2(704f, 46f), new Color(.57f, 1f, .82f));
            string text = level == 2
                ? "① 找到星星箱，依畫面按鍵提示拾取與搬運。\n② 將箱子放到星星平台，直到所有星星就位。\n③ 通過已開啟的閘門，即可完成第二關。"
                : "① 取得／裝備電漿武器，瞄準 Boss 射擊。\n② 觀察畫面上方 Boss 血條，閃避攻擊並注意階段變化。\n③ 擊倒 Boss 後收集回收物，完成 5 道資源循環題目。";
            SDGExperienceUI.Text(panel, "Instructions", text, 22,
                new Vector2(0, -7f), new Vector2(700f, 112f), new Color(.92f, 1f, .97f));
            SDGExperienceUI.Text(panel, "HelpHint", "F1：再次顯示／隱藏玩法說明（不會暫停遊戲）", 16,
                new Vector2(0, -82f), new Vector2(700f, 28f), new Color(.52f, .91f, .83f));
        }
    }
}
