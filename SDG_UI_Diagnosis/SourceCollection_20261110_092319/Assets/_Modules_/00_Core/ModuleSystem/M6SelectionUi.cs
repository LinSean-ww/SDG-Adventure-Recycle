using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SDGAdventure.SharedPlayer;

namespace SDGAdventure.ModuleSystem
{
    // Development selection UI uses UGUI to avoid IMGUI's TextCore cache across scene reloads.
    public sealed class M6SelectionUi : MonoBehaviour
    {
        private GameObject canvasObject;
        private GameObject ownedEventSystem;
        private RectTransform panelRect, launchRect;
        private Text result, error;
        private ModuleCoordinator coordinator;

        private void OnEnable() => SceneManager.sceneUnloaded += HandleSceneUnloaded;
        private void OnDisable() => SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.path == ModuleCoordinator.SelectionScenePath) Hide();
        }

        public void Show(ModuleCoordinator owner)
        {
            coordinator = owner;
            if (canvasObject == null) Build();
            if (ownedEventSystem != null) ownedEventSystem.SetActive(true);
            canvasObject.SetActive(true);
        }

        public void Hide()
        {
            if (canvasObject != null) canvasObject.SetActive(false);
            if (ownedEventSystem != null) ownedEventSystem.SetActive(false);
        }

        private void Update()
        {
            if (!canvasObject || !canvasObject.activeSelf || !coordinator) return;
            float width = Mathf.Min(600, Screen.width - 32);
            panelRect.sizeDelta = new Vector2(width, 480);
            launchRect.sizeDelta = new Vector2(width - 40, 50);
            ModuleResult? last = coordinator.LastResult;
            result.text = last.HasValue ? "最近結果：" + last.Value.ModuleId + (last.Value.Completed ? " 通關" : " 未完成") : "";
        }

        private void Build()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject events = new GameObject("M6EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem = events;
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            canvasObject = new GameObject("M6SelectionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(canvasObject.transform, false);
            panelRect = (RectTransform)panel.transform; panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0, -40); panelRect.sizeDelta = new Vector2(600, 480); panel.GetComponent<Image>().color = new Color(0.22f, 0.22f, 0.22f, 0.95f);
            Font font = PlayerGuiFont.Load();
            AddText(panel.transform, font, "SDG Adventure｜M6 開發驗證選關入口", 14, 130, 28);
            AddText(panel.transform, font, "僅供整合驗證；不是正式遊戲 UI。", 14, 81, 44);
            GameObject buttonObject = new GameObject("Launch", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panel.transform, false); launchRect = (RectTransform)buttonObject.transform;
            launchRect.anchorMin = launchRect.anchorMax = launchRect.pivot = new Vector2(0.5f, 0.5f);
            launchRect.anchoredPosition = new Vector2(0, 26); launchRect.sizeDelta = new Vector2(560, 50);
            buttonObject.GetComponent<Image>().color = new Color(0.42f, 0.42f, 0.42f);
            buttonObject.GetComponent<Button>().onClick.AddListener(() =>
            { if (!coordinator.TryLaunch("c3_treasure_puzzle", out string message)) error.text = message; });
            Text buttonText = AddText(buttonObject.transform, font, "進入 C3_TreasurePuzzle", 14, 0, 50);
            buttonText.rectTransform.sizeDelta = new Vector2(-20, 50);
            GameObject level01 = Instantiate(buttonObject, panel.transform);
            level01.name = "LaunchLevel01";
            ((RectTransform)level01.transform).anchoredPosition = new Vector2(0, -35);
            Button level01Button = level01.GetComponent<Button>();
            level01Button.onClick.RemoveAllListeners();
            level01Button.onClick.AddListener(() =>
            { if (!coordinator.TryLaunch("level01", out string message)) error.text = message; });
            level01.GetComponentInChildren<Text>().text = "進入第一關";
            GameObject level02 = Instantiate(buttonObject, panel.transform);
            level02.name = "LaunchLevel02";
            ((RectTransform)level02.transform).anchoredPosition = new Vector2(0, -96);
            Button level02Button = level02.GetComponent<Button>();
            level02Button.onClick.RemoveAllListeners();
            level02Button.onClick.AddListener(() =>
            { if (!coordinator.TryLaunch("level02", out string message)) error.text = message; });
            level02.GetComponentInChildren<Text>().text = "進入第二關";
            GameObject level04 = Instantiate(buttonObject, panel.transform);
            level04.name = "LaunchLevel04";
            ((RectTransform)level04.transform).anchoredPosition = new Vector2(0, -157);
            Button level04Button = level04.GetComponent<Button>();
            level04Button.onClick.RemoveAllListeners();
            level04Button.onClick.AddListener(() =>
            { if (!coordinator.TryLaunch("level04", out string message)) error.text = message; });
            level04.GetComponentInChildren<Text>().text = "進入第四關";
            result = AddText(panel.transform, font, "", 14, -210, 44);
            error = AddText(panel.transform, font, "", 14, -245, 50);
        }

        private static Text AddText(Transform parent, Font font, string value, int size, float y, float height)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform; rect.anchorMin = new Vector2(0, 0.5f); rect.anchorMax = new Vector2(1, 0.5f); rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, y); rect.sizeDelta = new Vector2(-40, height);
            Text text = go.GetComponent<Text>(); text.font = font; text.fontSize = size; text.text = value;
            text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false; return text;
        }
    }
}
