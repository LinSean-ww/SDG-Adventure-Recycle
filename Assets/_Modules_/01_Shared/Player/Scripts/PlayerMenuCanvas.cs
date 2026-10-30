using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SDGAdventure.SharedPlayer
{
    // Screen-space Canvas keeps menu layout independent of IMGUI event order and Game View zoom.
    public sealed class PlayerMenuCanvas : MonoBehaviour
    {
        private static readonly (string label, string map, string action, int index)[] BindingRows =
        {
            ("前進", "Gameplay", "Move", 1), ("後退", "Gameplay", "Move", 2),
            ("左移", "Gameplay", "Move", 3), ("右移", "Gameplay", "Move", 4),
            ("跳躍", "Gameplay", "Jump", 0), ("奔跑", "Gameplay", "Sprint", 0),
            ("蹲下", "Gameplay", "Crouch", 0), ("互動", "Gameplay", "Interact", 0),
            ("任務清單", "Gameplay", "TaskList", 0), ("暫停", "Global", "Pause", 0)
        };

        private readonly List<(Text text, Button button, string map, string action, int index)> bindingViews =
            new List<(Text, Button, string, string, int)>();
        private GameObject overlay;
        private RectTransform panel;
        private RectTransform content;
        private ScrollRect scroll;
        private Text heading;
        private Button footerButton;
        private Text footerLabel;
        private Text sensitivityLabel;
        private Text sprintLabel;
        private Text crouchLabel;
        private Text perspectiveLabel;
        private Text feedbackLabel;
        private Text pauseFeedbackLabel;
        private Button cancelRebindButton;
        private Slider sensitivitySlider;
        private Action footerAction;
        private Font font;
        private int lastWidth = -1;
        private int lastHeight = -1;
        private float preferredWidth;
        private float preferredHeight;
        private PlayerPauseMenu activePauseMenu;

        public bool IsVisible => overlay != null && overlay.activeSelf;
        public RectTransform Panel => panel;

        public void Build()
        {
            if (overlay != null) return;
            font = PlayerGuiFont.Load();
            GameObject canvasObject = new GameObject("PlayerMenuCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            RectTransform overlayRect = CreateRect("ModalOverlay", canvasObject.transform);
            Stretch(overlayRect, 0f, 0f, 0f, 0f);
            overlay = overlayRect.gameObject;
            Image dim = overlay.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.78f);

            panel = CreateRect("MenuPanel", overlayRect);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.11f, 0.15f, 0.21f, 1f);
            heading = CreateText("Heading", panel, 24, TextAnchor.MiddleCenter);
            Stretch(heading.rectTransform, 14f, 14f, 10f, 0f);
            heading.rectTransform.anchorMin = new Vector2(0f, 1f);
            heading.rectTransform.anchorMax = new Vector2(1f, 1f);
            heading.rectTransform.sizeDelta = new Vector2(-28f, 42f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -24f);

            RectTransform viewport = CreateRect("ScrollViewport", panel);
            Stretch(viewport, 20f, 38f, 58f, 60f);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scroll.viewport = viewport;

            content = CreateRect("ScrollContent", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            RectTransform scrollbarRect = CreateRect("ScrollBar", panel);
            Stretch(scrollbarRect, 0f, 12f, 58f, 60f);
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.sizeDelta = new Vector2(12f, -118f);
            scrollbarRect.anchoredPosition = new Vector2(-18f, -1f);
            Image track = scrollbarRect.gameObject.AddComponent<Image>();
            track.color = new Color(0.22f, 0.28f, 0.36f, 1f);
            Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            RectTransform handle = CreateRect("Handle", scrollbarRect);
            Stretch(handle, 0f, 0f, 0f, 0f);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.65f, 0.75f, 0.88f, 1f);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            Button footer = CreateButton("Footer", panel, "返回", null);
            footerButton = footer;
            RectTransform footerRect = footer.GetComponent<RectTransform>();
            footerRect.anchorMin = new Vector2(0f, 0f);
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.pivot = new Vector2(0.5f, 0f);
            footerRect.anchoredPosition = new Vector2(0f, 10f);
            footerRect.sizeDelta = new Vector2(-40f, 42f);
            footerLabel = footer.GetComponentInChildren<Text>();
            footer.onClick.AddListener(() => footerAction?.Invoke());

            pauseFeedbackLabel = CreateText("PauseFeedback", panel, 16, TextAnchor.MiddleCenter);
            RectTransform feedbackRect = pauseFeedbackLabel.rectTransform;
            feedbackRect.anchorMin = new Vector2(0f, 0f);
            feedbackRect.anchorMax = new Vector2(1f, 0f);
            feedbackRect.pivot = new Vector2(0.5f, 0f);
            feedbackRect.anchoredPosition = new Vector2(0f, 8f);
            feedbackRect.sizeDelta = new Vector2(-40f, 48f);
            pauseFeedbackLabel.gameObject.SetActive(false);

            EnsureEventSystem();
            overlay.SetActive(false);
        }

        public void Hide()
        {
            activePauseMenu = null;
            if (overlay != null) overlay.SetActive(false);
        }

        public void ShowPause(PlayerPauseMenu menu)
        {
            BeginPanel("暫停", 480f, menu.ShowLevelActions ? 510f : menu.HasTestReturnPoint ? 350f : 300f);
            activePauseMenu = menu;
            // Keep all primary actions visible in shorter Game views when possible.
            content.GetComponent<VerticalLayoutGroup>().spacing = 4f;
            int buttonCount = 2 + (menu.ShowLevelActions ? 3 : 0) + (menu.HasTestReturnPoint ? 1 : 0);
            bool hasFeedback = !string.IsNullOrEmpty(menu.Feedback);
            float available = panel.sizeDelta.y - 118f - 8f - 4f * buttonCount;
            float buttonHeight = Mathf.Clamp((available - 24f) / buttonCount, 22f, 46f);
            AddButton("繼續遊戲", menu.Resume, buttonHeight);
            AddButton("操作設定", menu.OpenSettings, buttonHeight);
            if (menu.ShowLevelActions)
            {
                AddButton("任務清單", menu.OpenTasksFromPause, buttonHeight);
                AddButton("重新開始本關", menu.RestartLevel, buttonHeight);
                AddButton("返回選關", menu.ReturnToSelection, buttonHeight);
            }
            if (menu.HasTestReturnPoint) AddButton("返回測試起點", menu.ReturnToTestStart, buttonHeight);
            pauseFeedbackLabel.text = menu.Feedback;
            pauseFeedbackLabel.gameObject.SetActive(hasFeedback);
            AddText("Esc：繼續／返回上一層", 24f, 17, TextAnchor.MiddleCenter);
            EndPanel(null, null);
        }

        public void ShowSettings(PlayerPauseMenu menu, PlayerInputService input)
        {
            BeginPanel("操作設定", 680f, 650f);
            AddText("點擊按鍵欄位後，直接按下新按鍵。Esc 可取消。", 52f, 17);
            AddSensitivity(menu, input);
            sprintLabel = AddButton("", menu.ToggleSprintMode).GetComponentInChildren<Text>();
            crouchLabel = AddButton("", menu.ToggleCrouchMode).GetComponentInChildren<Text>();
            perspectiveLabel = AddButton("", menu.TogglePerspectiveMode).GetComponentInChildren<Text>();
            for (int i = 0; i < BindingRows.Length; i++)
            {
                var binding = BindingRows[i];
                RectTransform row = CreateRect("Binding_" + binding.action + "_" + binding.index, content);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
                HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                Text label = CreateText("Action", row, 18, TextAnchor.MiddleLeft);
                label.text = binding.label;
                label.gameObject.AddComponent<LayoutElement>().preferredWidth = 118f;
                Button button = CreateButton("Key", row, "", () =>
                    menu.BeginRebind(binding.map, binding.action, binding.index));
                LayoutElement buttonLayout = button.gameObject.AddComponent<LayoutElement>();
                buttonLayout.flexibleWidth = 1f;
                bindingViews.Add((button.GetComponentInChildren<Text>(), button,
                    binding.map, binding.action, binding.index));
            }
            feedbackLabel = AddText("", 48f, 17);
            cancelRebindButton = AddButton("取消重綁", menu.CancelRebind);
            AddButton("恢復預設", menu.RestoreDefaults);
            EndPanel("返回暫停選單", menu.BackToPause);
            RefreshSettings(menu, input);
        }

        public void ShowTasks(PlayerPauseMenu menu, PlayerTaskJournal journal)
        {
            BeginPanel("任務清單", 680f, 590f);
            AddText("完成進度：" + journal.CompletedCount + " / " + journal.TotalCount, 40f, 20);
            if (journal.TotalCount == 0) AddText("目前沒有任務。", 60f, 18);
            foreach (PlayerTaskItem task in journal.Tasks)
            {
                string marker = task.completed ? "✓ 已完成" : "○ 進行中";
                AddText(marker + "　" + task.title + "\n" + task.description,
                    112f, 18, TextAnchor.UpperLeft);
            }
            EndPanel("關閉任務清單（Esc）", menu.CloseTasks);
        }

        public void RefreshSettings(PlayerPauseMenu menu, PlayerInputService input)
        {
            if (sensitivityLabel == null || input == null) return;
            PlayerSettingsData settings = input.Keybindings.Settings;
            sensitivityLabel.text = "滑鼠靈敏度：" + settings.mouseSensitivity.ToString("0.0");
            sensitivitySlider.SetValueWithoutNotify(settings.mouseSensitivity);
            sprintLabel.text = "奔跑模式：" + (settings.sprintMode == ButtonMode.Hold ? "按住" : "切換");
            crouchLabel.text = "蹲下模式：" + (settings.crouchMode == ButtonMode.Hold ? "按住" : "切換");
            perspectiveLabel.text = "視角模式：" + (PlayerViewPreference.Mode == PlayerViewMode.FirstPerson ? "第一人稱" : "第三人稱");
            foreach (var binding in bindingViews)
            {
                binding.text.text = menu.IsRebinding ? "按下新按鍵…" :
                    input.Keybindings.BindingDisplay(binding.map, binding.action, binding.index);
                binding.button.interactable = !menu.IsRebinding;
            }
            feedbackLabel.text = menu.Feedback;
            cancelRebindButton.gameObject.SetActive(menu.IsRebinding);
        }

        private void Update()
        {
            if (!IsVisible || (lastWidth == Screen.width && lastHeight == Screen.height)) return;
            if (activePauseMenu != null && activePauseMenu.State == PlayerMenuState.Paused)
                ShowPause(activePauseMenu);
            else ResizePanel();
        }

        private void BeginPanel(string title, float width, float height)
        {
            Build();
            activePauseMenu = null;
            preferredWidth = width;
            preferredHeight = height;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                GameObject old = content.GetChild(i).gameObject;
                old.SetActive(false);
                Destroy(old);
            }
            bindingViews.Clear();
            content.GetComponent<VerticalLayoutGroup>().spacing = 8f;
            pauseFeedbackLabel.gameObject.SetActive(false);
            sensitivityLabel = sprintLabel = crouchLabel = perspectiveLabel = feedbackLabel = null;
            cancelRebindButton = null;
            heading.text = title;
            overlay.SetActive(true);
            ResizePanel();
        }

        private void EndPanel(string footerText, Action action)
        {
            footerButton.gameObject.SetActive(!string.IsNullOrEmpty(footerText));
            footerLabel.text = footerText ?? "";
            footerAction = action;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = 1f;
        }

        private void ResizePanel()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            panel.sizeDelta = new Vector2(Mathf.Min(preferredWidth, Mathf.Max(0f, lastWidth - 24f)),
                Mathf.Min(preferredHeight, Mathf.Max(0f, lastHeight - 24f)));
        }

        private void AddSensitivity(PlayerPauseMenu menu, PlayerInputService input)
        {
            RectTransform row = CreateRect("Sensitivity", content);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            sensitivityLabel = CreateText("Value", row, 17, TextAnchor.MiddleLeft);
            sensitivityLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 160f;
            RectTransform sliderRect = CreateRect("Slider", row);
            sliderRect.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Image sliderBackground = sliderRect.gameObject.AddComponent<Image>();
            sliderBackground.color = new Color(0.28f, 0.37f, 0.48f, 1f);
            sensitivitySlider = sliderRect.gameObject.AddComponent<Slider>();
            sensitivitySlider.minValue = 0.1f;
            sensitivitySlider.maxValue = 10f;
            RectTransform handle = CreateRect("Handle", sliderRect);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.sizeDelta = new Vector2(14f, 0f);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Color.white;
            sensitivitySlider.handleRect = handle;
            sensitivitySlider.targetGraphic = handleImage;
            sensitivitySlider.onValueChanged.AddListener(value => menu.SetMouseSensitivity(value));
            sensitivitySlider.SetValueWithoutNotify(input.Keybindings.Settings.mouseSensitivity);
        }

        private Text AddText(string value, float height, int size, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            Text label = CreateText("Label", content, size, alignment);
            label.text = value;
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return label;
        }

        private Button AddButton(string value, Action action, float height = 46f)
        {
            Button button = CreateButton("Button", content, value, action);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return button;
        }

        private Button CreateButton(string name, Transform parent, string value, Action action)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.32f, 0.43f, 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(() => action());
            Text label = CreateText("Text", rect, 19, TextAnchor.MiddleCenter);
            label.text = value;
            Stretch(label.rectTransform, 8f, 8f, 2f, 2f);
            return button;
        }

        private Text CreateText(string name, Transform parent, int size, TextAnchor alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            GameObject host = new GameObject("PlayerMenuEventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            host.transform.SetParent(transform, false);
            host.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
