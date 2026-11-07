using System.Collections.Generic;
using UnityEngine;
using SDGAdventure.SharedPlayer;

namespace C3TreasurePuzzle
{
    [RequireComponent(typeof(Collider))]
    public sealed class C3PuzzleConsole : C3Interactable
    {
        public enum ConsoleMode { Puzzle,Activation,ProductionControl }
        public const string Clue = "工廠作業線索：先把混合廢棄物依材質分類；可再利用的材料接著送去資源回收；最後妥善處理剩餘污染物。請依作業先後輸入。這是本關的簡化教育謎題，並非所有工業廢棄物的唯一標準流程。";
        [SerializeField] private C3TreasureProgress progress;
        [SerializeField] private C3PuzzleState puzzle;
        [SerializeField] private C3DeviceState device;
        private readonly List<C3ProcessingStep> selection = new List<C3ProcessingStep>(3);
        private bool panelOpen;
        public ConsoleMode Mode { get; private set; }
        private string feedback = "";
        private float noticeUntil;
        private CursorLockMode previousLock;
        private bool previousVisible;
        private static C3PuzzleConsole activePanel;
        private static int closedFrame = -1;

        public override string DisplayName => "資源處理控制台";
        public bool PanelOpen => panelOpen;
        public bool IsActivationPanel => Mode==ConsoleMode.Activation;
        private C3RecyclingLine ProductionLine => device?device.GetComponent<C3RecyclingLine>():null;
        private static bool ProductionInputAvailable => PlayerInputService.Instance==null ||
            PlayerInputService.Instance.Context==PlayerInputContext.Gameplay;
        public string ProductionStatus => ProductionLine?ProductionLine.PresentationProgress:"產線尚未就緒。";
        public bool CanStartPresentation => panelOpen && Mode==ConsoleMode.ProductionControl && ProductionInputAvailable &&
            Time.timeScale>0f && ProductionLine && ProductionLine.CanStartPresentation;
        public string Feedback => feedback;
        public string NoticeText => !panelOpen && Time.unscaledTime < noticeUntil ? feedback : "";
        public IReadOnlyList<C3ProcessingStep> Selection => selection;
        public static bool BlocksGameplayInputThisFrame => activePanel != null || closedFrame == Time.frameCount;

        public void Configure(C3TreasureProgress owner, C3PuzzleState state)
        {
            progress = owner;
            puzzle = state;
        }

        public void SetDevice(C3DeviceState value) => device = value;

        private void Awake()
        {
            if (progress == null) progress = FindObjectOfType<C3TreasureProgress>();
            if (puzzle == null) puzzle = FindObjectOfType<C3PuzzleState>();
            if (device == null) device = FindObjectOfType<C3DeviceState>();
        }

        public override bool CanInteract(GameObject actor) => isActiveAndEnabled && progress != null && puzzle != null &&
            device != null && !device.LevelCompleted && (!device.IsActivated || ProductionInputAvailable) &&
            !panelOpen && !C3CompletionScreen.BlocksGameplayInputThisFrame;

        public override void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            if (!progress.AllPartsCollected)
            {
                feedback = "零件尚未收齊：" + MissingParts();
                noticeUntil = Time.unscaledTime + 4f;
                return;
            }
            OpenPanel();
        }

        public bool OpenPanel()
        {
            if (!isActiveAndEnabled || panelOpen || activePanel != null || progress == null || puzzle == null || device == null ||
                device.LevelCompleted || C3CompletionScreen.BlocksGameplayInputThisFrame || !progress.AllPartsCollected) return false;
            if (device.IsActivated && !ProductionInputAvailable) return false;
            selection.Clear();
            Mode=device.IsActivated?ConsoleMode.ProductionControl:puzzle.PuzzleSolved?ConsoleMode.Activation:ConsoleMode.Puzzle;
            feedback = Mode==ConsoleMode.ProductionControl?ProductionStatus:
                puzzle.PuzzleSolved ? "解謎已完成；設備尚未啟動。請確認啟動。" : "請依線索選擇三個處理步驟。";
            previousLock = PlayerCursor.LockState;
            previousVisible = Cursor.visible;
            activePanel = this;
            panelOpen = true;
            PlayerCursor.LockState = CursorLockMode.None;
            Cursor.visible = true;
            return true;
        }

        public void ClosePanel()
        {
            if (!panelOpen) return;
            panelOpen = false;
            if (activePanel == this) activePanel = null;
            closedFrame = Time.frameCount;
            if (C3CompletionScreen.BlocksGameplayInputThisFrame || (device && device.LevelCompleted))
            { PlayerCursor.LockState=CursorLockMode.None; Cursor.visible=true; }
            else
            {
                PlayerInputService input=PlayerInputService.Instance;
                if (input==null || input.Context==PlayerInputContext.Gameplay)
                { PlayerCursor.LockState=previousLock; Cursor.visible=previousVisible; }
            }
        }

        public bool SelectStep(C3ProcessingStep step)
        {
            if (!panelOpen || Mode!=ConsoleMode.Puzzle || device.IsActivated || puzzle.PuzzleSolved || selection.Count >= 3 ||
                step < C3ProcessingStep.Sorting || step > C3ProcessingStep.PollutionTreatment ||
                selection.Contains(step)) return false;
            selection.Add(step);
            feedback = "已選擇 " + selection.Count + "/3 個步驟。";
            return true;
        }

        public void Retry()
        {
            if (!panelOpen || Mode!=ConsoleMode.Puzzle || device.IsActivated || puzzle.PuzzleSolved) return;
            selection.Clear();
            feedback = "已清除順序，請重新選擇。";
        }

        public bool Confirm()
        {
            if (!panelOpen || Mode!=ConsoleMode.Puzzle || device.IsActivated || puzzle.PuzzleSolved) return false;
            if (selection.Count != 3)
            {
                feedback = "請先選滿三個處理步驟。";
                return false;
            }
            bool correct = puzzle.SubmitAnswer(selection.ToArray());
            if (correct)
            {
                // Stay in the same control-panel session. C3UiView sees this state
                // on its next Update and displays the activation UI immediately.
                // Device activation itself still requires the player's confirmation.
                Mode = ConsoleMode.Activation;
                feedback = "解謎成功！請確認啟動淨化設備。";
            }
            else
            {
                // Preserve the existing puzzle panel and Retry action on a wrong answer.
                feedback = "順序錯誤。請參考工廠作業線索，按「重試」重新挑戰。";
            }
            return correct;
        }

        public bool ConfirmActivation()
        {
            if (!panelOpen || Mode!=ConsoleMode.Activation || device == null || device.Phase != C3DevicePhase.ReadyToActivate) return false;
            ClosePanel();
            return device.TryActivate();
        }
        internal bool AuthorizesPresentation(C3RecyclingLine line,C3DeviceState owner)
            => isActiveAndEnabled && panelOpen && activePanel==this && Mode==ConsoleMode.ProductionControl &&
                device==owner && ProductionLine==line && ProductionInputAvailable && Time.timeScale>0f && !C3CompletionScreen.BlocksGameplayInputThisFrame;
        public bool StartPresentation()
        {
            if (!CanStartPresentation || !ProductionLine.TryStartPresentation(this)) return false;
            ClosePanel(); return true;
        }

        public string MissingParts()
        {
            if (progress == null) return "進度資料未就緒";
            var names = new List<string>(3);
            if (!progress.HasPart(C3TreasureProgress.FuseId)) names.Add("保險絲");
            if (!progress.HasPart(C3TreasureProgress.ChipId)) names.Add("控制晶片");
            if (!progress.HasPart(C3TreasureProgress.FilterId)) names.Add("過濾模組");
            return string.Join("、", names.ToArray());
        }

        private void Update()
        {
            if (panelOpen && (C3CompletionScreen.BlocksGameplayInputThisFrame || (device && device.LevelCompleted)))
            { ClosePanel(); return; }
            if (panelOpen && device && device.IsActivated)
            {
                Mode=ConsoleMode.ProductionControl; selection.Clear();
                if (!ProductionInputAvailable) { ClosePanel(); return; }
            }
            if (panelOpen && Input.GetKeyDown(KeyCode.Escape)) ClosePanel();
        }

        private void OnDisable()
        {
            ClosePanel();
        }


    }
}
