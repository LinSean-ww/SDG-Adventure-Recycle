using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using SDGAdventure.SharedPlayer;

namespace C3TreasurePuzzle
{
    // Scene-owned UGUI: avoids IMGUI's TextCore handle/atlas cache during scene reloads.
    public sealed class C3UiView : MonoBehaviour
    {
        private C3PuzzleConsole console;
        private C3PuzzleState puzzle;
        private C3TreasureHud hud;
        private C3CompletionScreen completion;
        private C3PlayerInteractor interactor;
        private Font font;
        private GameObject puzzlePanel, taskPanel, completionPanel, crosshair, compactPanel, puzzleBackdrop, completionBackdrop;
        private GameObject promptBackdrop;
        private Text taskText, prompt, compactTask, notice, puzzleTitle, puzzleClue, puzzleOrder, puzzleFeedback, completeText;
        private Text taskProgress, taskHintTitle, taskHint;
        private readonly Text[] taskRows = new Text[5];
        private float lastTaskTextWidth = -1f;
        private RectTransform puzzleControlsRow;
        private readonly Button[] choices = new Button[3];
        private Button confirm, retry, activate, cancelActivation, presentation;
        private static readonly string[] names = { "廢棄物分類", "資源回收", "污染處理" };
        // The same correct process applies, but the three displayed buttons are shuffled.
        // Persist the last layout to guarantee a different ordering on the next visit.
        private const string PreviousOrderKey = "SDG_C3_LastPuzzleOptionOrder";
        private readonly C3ProcessingStep[] optionOrder = new C3ProcessingStep[3];

        public C3ProcessingStep GetOptionAt(int slot) => optionOrder[slot];

        private void ShuffleOptionsOnLevelEntry()
        {
            // Six permutations of three distinct choices; choose one that differs
            // from the previous visit. Never shuffle again on Retry/OpenPanel.
            const string possible = "012 021 102 120 201 210";
            string[] orders = possible.Split(' ');
            string previous = PlayerPrefs.GetString(PreviousOrderKey, "012");
            int start = UnityEngine.Random.Range(0, orders.Length - 1);
            int chosen = 0;
            for (int i = 0; i < orders.Length; i++)
            {
                if (orders[i] == previous) continue;
                if (start-- == 0) { chosen = i; break; }
            }
            string selected = orders[chosen];
            PlayerPrefs.SetString(PreviousOrderKey, selected);
            PlayerPrefs.Save();
            for (int i = 0; i < optionOrder.Length; i++)
                optionOrder[i] = (C3ProcessingStep)(selected[i] - '0');
        }


        public void Initialize(C3PuzzleConsole puzzleConsole, C3PuzzleState puzzleState, C3TreasureHud treasureHud,
            C3CompletionScreen completionScreen, C3PlayerInteractor playerInteractor)
        {
            console = puzzleConsole; puzzle = puzzleState; hud = treasureHud; completion = completionScreen; interactor = playerInteractor;
            ShuffleOptionsOnLevelEntry();
            font = PlayerGuiFont.Load();
            GameObject canvasObject = new GameObject("C3UiCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 600;
            canvas.pixelPerfect = true;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            RectTransform root = canvasObject.GetComponent<RectTransform>();

            Text cross = TextItem("Crosshair", root, "+", 26, TextAnchor.MiddleCenter);
            SetRect(cross.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(40, 40));
            crosshair = cross.gameObject;
            promptBackdrop = Panel("InteractionPromptBacking", root,
                new Vector2(0.5f, 0.5f), new Vector2(0, -52),
                new Vector2(480, 44), new Color(0.04f, 0.08f, 0.10f, 0.83f));
            promptBackdrop.GetComponent<Image>().raycastTarget = false;
            promptBackdrop.SetActive(false);
            prompt = TextItem("InteractionPrompt", root, "", 18, TextAnchor.MiddleCenter);
            SetRect(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -52), new Vector2(480, 44));
            compactPanel = Panel("CompactPanel", root, new Vector2(0, 1), new Vector2(16, -16), new Vector2(82, 40), new Color(0.035f, 0.065f, 0.09f, 0.72f));
            compactPanel.GetComponent<Image>().raycastTarget = false;
            compactTask = TextItem("CompactTask", compactPanel.transform, "任務", 18, TextAnchor.MiddleLeft);
            Stretch(compactTask.rectTransform, 10);
            notice = TextItem("ConsoleNotice", root, "", 18, TextAnchor.MiddleCenter);
            SetRect(notice.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(600, 65));

            taskPanel = Panel("TaskSheet", root, new Vector2(0, 1), new Vector2(16, -16), new Vector2(540, 600), new Color(0.035f, 0.065f, 0.09f, 0.96f));
            GameObject accent = Panel("Accent", taskPanel.transform, new Vector2(0, 1), Vector2.zero, new Vector2(4, 600), new Color(0.31f, 0.91f, 0.94f));
            accent.GetComponent<Image>().raycastTarget = false;
            RectTransform accentRect = (RectTransform)accent.transform;
            accentRect.anchorMin = Vector2.zero; accentRect.anchorMax = new Vector2(0, 1); accentRect.pivot = new Vector2(0, 0.5f);
            accentRect.offsetMin = Vector2.zero; accentRect.offsetMax = new Vector2(4, 0);
            RectTransform taskContent = ScrollContent(taskPanel.transform, 6);
            taskText = TextItem("TaskText", taskContent, "任務", 26, TextAnchor.MiddleLeft); taskText.fontStyle = FontStyle.Bold; Height(taskText.gameObject, 38);
            Text mission = TextItem("Mission", taskContent, "尋找淨化設備零件", 20, TextAnchor.MiddleLeft); mission.fontStyle = FontStyle.Bold; Height(mission.gameObject, 32);
            taskProgress = TextItem("Progress", taskContent, "", 22, TextAnchor.MiddleLeft); taskProgress.fontStyle = FontStyle.Bold; taskProgress.color = new Color(0.31f, 0.91f, 0.94f); Height(taskProgress.gameObject, 34);
            for (int i = 0; i < taskRows.Length; i++)
            {
                GameObject row = new GameObject("TaskRow" + i, typeof(RectTransform), typeof(Image)); row.transform.SetParent(taskContent, false);
                row.GetComponent<Image>().color = new Color(0.11f, 0.17f, 0.21f, 0.98f); row.GetComponent<Image>().raycastTarget = false; Height(row, 60);
                taskRows[i] = TextItem("Label", row.transform, "", 19, TextAnchor.MiddleLeft); Stretch(taskRows[i].rectTransform, 10);
            }
            taskHintTitle = TextItem("HintTitle", taskContent, "目前線索", 18, TextAnchor.MiddleLeft); Height(taskHintTitle.gameObject, 28);
            GameObject hintBox = new GameObject("HintBox", typeof(RectTransform), typeof(Image)); hintBox.transform.SetParent(taskContent, false);
            hintBox.GetComponent<Image>().color = new Color(0.08f, 0.23f, 0.27f, 0.98f); hintBox.GetComponent<Image>().raycastTarget = false; Height(hintBox, 85);
            taskHint = TextItem("Hint", hintBox.transform, "", 18, TextAnchor.UpperLeft); Stretch(taskHint.rectTransform, 10);
            Text closeHint = TextItem("CloseHint", taskContent, "再按 P 關閉任務表", 18, TextAnchor.MiddleLeft); Height(closeHint.gameObject, 28);

            puzzleBackdrop = FullscreenBackdrop("PuzzleBackdrop", root, 0.75f);
            puzzlePanel = Panel("PuzzlePanel", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 640), new Color(0.22f, 0.22f, 0.22f, 0.95f));
            RectTransform content = ScrollContent(puzzlePanel.transform, 18);
            puzzleTitle = TextItem("Title", content, "資源處理控制台", 25, TextAnchor.MiddleLeft); puzzleTitle.fontStyle = FontStyle.Bold; Height(puzzleTitle.gameObject, 42);
            puzzleClue = TextItem("Clue", content, C3PuzzleConsole.Clue, 18, TextAnchor.UpperLeft); Height(puzzleClue.gameObject, 105);
            Text instruction = TextItem("Instruction", content, "選擇處理步驟（按作業先後）：", 18, TextAnchor.MiddleLeft); Height(instruction.gameObject, 28);
            for (int i = 0; i < choices.Length; i++)
            {
                C3ProcessingStep step = optionOrder[i];
                choices[i] = ButtonItem("Choice" + i, content, names[(int)step], () => console.SelectStep(step));
            }
            puzzleOrder = TextItem("Order", content, "", 18, TextAnchor.MiddleLeft); Height(puzzleOrder.gameObject, 48);
            puzzleFeedback = TextItem("Feedback", content, "", 18, TextAnchor.UpperLeft); Height(puzzleFeedback.gameObject, 70);
            GameObject controlRow = new GameObject("ControlsRow", typeof(RectTransform), typeof(LayoutElement)); controlRow.transform.SetParent(content, false);
            controlRow.GetComponent<LayoutElement>().preferredHeight = 48; puzzleControlsRow = (RectTransform)controlRow.transform;
            confirm = ButtonItem("Confirm", content, "確認", () => console.Confirm());
            retry = ButtonItem("Retry", content, "重試", () => console.Retry());
            activate = ButtonItem("Activate", content, "確認啟動設備", () => console.ConfirmActivation());
            presentation = ButtonItem("Presentation", content, "產線展示", () => console.StartPresentation());
            presentation.GetComponent<LayoutElement>().preferredHeight=48;
            cancelActivation = ButtonItem("Close", content, "關閉", () => console.ClosePanel());
            confirm.GetComponent<LayoutElement>().ignoreLayout = true; retry.GetComponent<LayoutElement>().ignoreLayout = true; cancelActivation.GetComponent<LayoutElement>().ignoreLayout = true;
            activate.GetComponent<LayoutElement>().preferredHeight = 48;
            Text escapeHint = TextItem("EscapeHint", content, "ESC 可關閉面板", 18, TextAnchor.MiddleLeft); Height(escapeHint.gameObject, 28);

            completionBackdrop = FullscreenBackdrop("CompletionBackdrop", root, 0.82f);
            completionPanel = Panel("CompletionPanel", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 650), new Color(0.22f, 0.22f, 0.22f, 0.95f));
            RectTransform completeContent = ScrollContent(completionPanel.transform, 12);
            Text completionTitle = TextItem("CompletionTitle", completeContent, "淨化設備啟動成功", 26, TextAnchor.UpperLeft); completionTitle.fontStyle = FontStyle.Bold; Height(completionTitle.gameObject, 46);
            completeText = TextItem("CompletionText", completeContent, "", 18, TextAnchor.UpperLeft); Height(completeText.gameObject, 275);
            ButtonItem("Restart", completeContent, "重新開始本關卡", () => completion.RestartLevel()).GetComponent<LayoutElement>().preferredHeight = 48;
            ButtonItem("Selection", completeContent, "返回 M6 開發驗證選關入口", () => completion.ReturnToSelection()).GetComponent<LayoutElement>().preferredHeight = 48;
            puzzlePanel.SetActive(false); puzzleBackdrop.SetActive(false); taskPanel.SetActive(false); completionPanel.SetActive(false); completionBackdrop.SetActive(false);
        }

        // The task sheet may occupy more than 44% of a small Game view; keep its
        // actual edges on screen and let the scroll viewport handle extra height.
        private static Vector2 CalculateTaskPanelSize(int width, int height)
        {
            float scale = Mathf.Clamp(Mathf.Min(width / 1920f, height / 1080f), 0.8f, 1.2f);
            return new Vector2(Mathf.Max(0f, Mathf.Min(540f * scale, width - 32f * scale)),
                Mathf.Max(0f, Mathf.Min(600f * scale, height - 32f * scale)));
        }

        private void Update()
        {
            if (!console || !hud || !completion || !interactor || !puzzle) return;
            bool showCompletion = completion.Visible;
            bool showPuzzle = console.PanelOpen && !showCompletion;
            bool showTask = hud.TaskSheetOpen && !showCompletion && !showPuzzle;
            puzzlePanel.SetActive(showPuzzle); puzzleBackdrop.SetActive(showPuzzle); completionPanel.SetActive(showCompletion); completionBackdrop.SetActive(showCompletion); taskPanel.SetActive(showTask);
            RectTransform puzzleRect = (RectTransform)puzzlePanel.transform;
            puzzleRect.sizeDelta = new Vector2(Mathf.Min(720, Screen.width - 24), Mathf.Min(640, Screen.height - 24));
            float margin = Mathf.Min(20f, Mathf.Min(Screen.width, Screen.height) * 0.04f);
            ((RectTransform)completionPanel.transform).sizeDelta = new Vector2(Screen.width - 2 * margin, Screen.height - 2 * margin);
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f), 0.8f, 1.2f);
            RectTransform taskRect = (RectTransform)taskPanel.transform;
            taskRect.anchoredPosition = new Vector2(16 * scale, -16 * scale);
            taskRect.sizeDelta = CalculateTaskPanelSize(Screen.width, Screen.height);
            RectTransform compactRect = (RectTransform)compactPanel.transform;
            compactRect.anchoredPosition = new Vector2(16 * scale, -16 * scale); compactRect.sizeDelta = new Vector2(82 * scale, 40 * scale);
            compactTask.fontSize = Mathf.RoundToInt(18 * scale);
            compactPanel.GetComponent<Image>().color = hud.CollectionFeedbackActive ? new Color(0.10f, 0.32f, 0.20f, 0.86f) : new Color(0.035f, 0.065f, 0.09f, 0.72f);
            var input = PlayerInputService.Instance;
            bool gameplay = input == null || input.Context == PlayerInputContext.Gameplay;
            bool showAim = gameplay && !showPuzzle && !showCompletion && !showTask && PlayerCursor.LockState == CursorLockMode.Locked;
            crosshair.SetActive(showAim);
            compactPanel.SetActive(gameplay && !showPuzzle && !showCompletion && !showTask);
            notice.text = showPuzzle || showCompletion ? "" : console.NoticeText;
            C3Interactable target = showAim ? interactor.CurrentTarget : null;
            promptBackdrop.SetActive(target != null);
            float promptWidth = Mathf.Min(480f, Screen.width - 32f);
            ((RectTransform)promptBackdrop.transform).sizeDelta = new Vector2(promptWidth, 44f);
            prompt.rectTransform.sizeDelta = new Vector2(promptWidth, 44f);
            string key = input == null ? "F" : input.Keybindings.BindingDisplay("Gameplay", "Interact", 0);
            prompt.text = target == null ? "" :
                target is EcoEnergyPickup ? target.DisplayName + "  [裝備武器後靠近]" :
                target.DisplayName + "  [" + key + "] " +
                (target is C3CollectiblePart ? "拾取" : "互動");
            if (showTask)
            {
                PlayerPauseMenu menu = interactor.GetComponent<PlayerPauseMenu>();
                var journal = menu != null ? menu.Journal : null;
                taskProgress.text = journal == null ? "" : "任務進度  " + journal.CompletedCount + "/" + journal.TotalCount;
                float textWidth = Mathf.Max(40f, taskRect.sizeDelta.x - 32f); // viewport 6px, row 10px each side
                bool widthChanged = !Mathf.Approximately(lastTaskTextWidth, textWidth);
                int rowFont = Mathf.Clamp(Mathf.RoundToInt(19f * scale), 13, 19);
                int hintFont = Mathf.Clamp(Mathf.RoundToInt(18f * scale), 13, 18);
                taskProgress.fontSize = Mathf.Clamp(Mathf.RoundToInt(22f * scale), 16, 22);
                for (int i = 0; i < taskRows.Length; i++)
                {
                    bool hasTask = journal != null && i < journal.Tasks.Count;
                    taskRows[i].transform.parent.gameObject.SetActive(hasTask);
                    if (!hasTask) continue;
                    PlayerTaskItem task = journal.Tasks[i];
                    string rowText = task.title + "：" + (task.completed ? "已完成" : "進行中") + "\n" + task.description;
                    if (widthChanged || taskRows[i].text != rowText || taskRows[i].fontSize != rowFont)
                    {
                        taskRows[i].text = rowText;
                        taskRows[i].fontSize = rowFont;
                        FitTaskBlock(taskRows[i], textWidth, 20f * scale, 52f * scale);
                    }
                    taskRows[i].color = task.completed ? new Color(0.67f, 1f, 0.77f) : new Color(1f, 0.84f, 0.52f);
                }
                taskHintTitle.text = journal != null && journal.CompletedCount >= 3 ? "下一步" : "目前線索";
                string hintText = hud.GuidanceText;
                if (widthChanged || taskHint.text != hintText || taskHint.fontSize != hintFont)
                {
                    taskHint.text = hintText;
                    taskHint.fontSize = hintFont;
                    FitTaskBlock(taskHint, textWidth, 20f * scale, 72f * scale);
                }
                lastTaskTextWidth = textWidth;
            }
            if (showCompletion)
                completeText.text = "零件收集結果：" + completion.PartsText +
                    "\n解謎嘗試次數：" + completion.PuzzleAttempts + "\n本次關卡遊玩時間：" +
                    C3CompletionScreen.FormatTime(completion.PlaySeconds) + "\n完成狀態：" + completion.CompletionText + "\n\n" + C3CompletionScreen.Knowledge;
            if (!showPuzzle) return;
            bool production=console.Mode==C3PuzzleConsole.ConsoleMode.ProductionControl;
            bool activation = !production && puzzle.PuzzleSolved && console.IsActivationPanel;
            bool puzzleMode=!activation && !production;
            puzzleControlsRow.gameObject.SetActive(puzzleMode);
            cancelActivation.GetComponent<LayoutElement>().ignoreLayout = puzzleMode;
            cancelActivation.GetComponentInChildren<Text>().text = activation ? "暫不啟動／關閉" : "關閉";
            puzzleClue.transform.parent.Find("Instruction").gameObject.SetActive(puzzleMode);
            puzzleTitle.text = production?"回收產線控制":activation ? "淨化設備待啟動" : "資源處理控制台";
            puzzleClue.text = production?"觀看九件回收物完成分類、壓縮與出料。展示不增加材料獎勵；開始後會關閉面板，方便觀看。":
                activation ? "三項零件已收齊，確認後啟動設備，待工廠恢復運作再前往出口。" : C3PuzzleConsole.Clue;
            for (int i = 0; i < choices.Length; i++)
            {
                choices[i].gameObject.SetActive(puzzleMode);
                choices[i].interactable = !puzzle.PuzzleSolved && !console.Selection.Contains(optionOrder[i]) && console.Selection.Count < 3;
            }
            string order = "";
            foreach (C3ProcessingStep step in console.Selection) order += (order.Length == 0 ? "" : " → ") + names[(int)step];
            puzzleOrder.text = puzzleMode?"目前順序："+(order.Length==0?"尚未輸入":order):"";
            puzzleFeedback.text = production?console.ProductionStatus:console.Feedback;
            confirm.gameObject.SetActive(puzzleMode); retry.gameObject.SetActive(puzzleMode); activate.gameObject.SetActive(activation);
            presentation.gameObject.SetActive(production); presentation.interactable=console.CanStartPresentation;
            confirm.interactable = !puzzle.PuzzleSolved; retry.interactable = !puzzle.PuzzleSolved;
            cancelActivation.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (!puzzlePanel || !puzzlePanel.activeSelf || !puzzleControlsRow.gameObject.activeSelf) return;
            float width = puzzleControlsRow.rect.width;
            float buttonWidth = (width - 20f) / 3f;
            RectTransform content = (RectTransform)puzzleControlsRow.parent;
            Vector3 topLeft = content.InverseTransformPoint(puzzleControlsRow.TransformPoint(new Vector3(puzzleControlsRow.rect.xMin, puzzleControlsRow.rect.yMax)));
            Button[] buttons = { confirm, retry, cancelActivation };
            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform rect = (RectTransform)buttons[i].transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(topLeft.x - content.rect.xMin + i * (buttonWidth + 10), topLeft.y - content.rect.yMax);
                rect.sizeDelta = new Vector2(buttonWidth, 48);
            }
        }

        private static GameObject FullscreenBackdrop(string name, Transform parent, float alpha)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false); Stretch((RectTransform)go.transform, 0);
            go.GetComponent<Image>().color = new Color(0, 0, 0, alpha); return go;
        }

        private GameObject Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false); SetRect((RectTransform)go.transform, anchor, position, size);
            go.GetComponent<Image>().color = color; return go;
        }
        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
        { rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = pos; rect.sizeDelta = size; }
        private static void Stretch(RectTransform rect, float margin)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(margin, margin); rect.offsetMax = new Vector2(-margin, -margin); }
        private Text TextItem(string name, Transform parent, string value, int size, TextAnchor alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size;
            text.color = Color.white; text.alignment = alignment; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow; text.resizeTextForBestFit = false;
            text.raycastTarget = false; return text;
        }

        // Measure at the current panel width instead of relying on the previous
        // frame's stretched RectTransform. VerticalLayoutGroup then sizes each row.
        private static void FitTaskBlock(Text label, float width, float padding, float minimumHeight)
        {
            if (!label || !label.font) return;
            TextGenerationSettings settings = label.GetGenerationSettings(new Vector2(Mathf.Max(40f, width), 0f));
            float preferred = label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
            LayoutElement layout = label.transform.parent.GetComponent<LayoutElement>();
            if (layout) layout.preferredHeight = Mathf.Max(minimumHeight, Mathf.Ceil(preferred + padding));
        }

        private static void Height(GameObject go, float height) { go.AddComponent<LayoutElement>().preferredHeight = height; }
        private Button ButtonItem(string name, Transform parent, string label, Action action)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.42f, 0.42f, 0.42f, 1f); Height(go, 43);
            Button button = go.GetComponent<Button>(); button.onClick.AddListener(() => action());
            Text text = TextItem("Label", go.transform, label, 18, TextAnchor.MiddleCenter); Stretch(text.rectTransform, 2); return button;
        }
        private static RectTransform ScrollContent(Transform parent, float margin)
        {
            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(parent, false); Stretch((RectTransform)viewport.transform, margin);
            // Text is deliberately not a raycast target. Give the viewport its
            // own transparent hit surface so wheel/drag events over text or blank
            // space reach ScrollRect even when all buttons are below the mask.
            viewport.GetComponent<Image>().color = Color.clear;
            viewport.GetComponent<Image>().raycastTarget = true;
            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false); RectTransform rect = (RectTransform)content.transform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(0.5f, 1); rect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup group = content.GetComponent<VerticalLayoutGroup>(); group.spacing = 8; group.childControlHeight = true;
            group.childControlWidth = true; group.childForceExpandHeight = false; group.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.GetComponent<ScrollRect>(); scroll.viewport = (RectTransform)viewport.transform; scroll.content = rect;
            scroll.horizontal = false; scroll.scrollSensitivity = 28; return rect;
        }
    }
}
