using System.Collections.Generic;
using UnityEngine;

namespace C3TreasurePuzzle
{
    [RequireComponent(typeof(Collider))]
    public sealed class C3PuzzleConsole : C3Interactable
    {
        public const string Clue = "工廠作業線索：先把混合廢棄物依材質分類；可再利用的材料接著送去資源回收；最後妥善處理剩餘污染物。請依作業先後輸入。這是本關的簡化教育謎題，並非所有工業廢棄物的唯一標準流程。";
        [SerializeField] private C3TreasureProgress progress;
        [SerializeField] private C3PuzzleState puzzle;
        [SerializeField] private C3DeviceState device;
        private readonly List<C3ProcessingStep> selection = new List<C3ProcessingStep>(3);
        private bool panelOpen;
        private bool activationPanel;
        private string feedback = "";
        private float noticeUntil;
        private CursorLockMode previousLock;
        private bool previousVisible;
        private static C3PuzzleConsole activePanel;
        private static int closedFrame = -1;

        public override string DisplayName => "資源處理控制台";
        public bool PanelOpen => panelOpen;
        public bool IsActivationPanel => activationPanel;
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
            device != null && !device.IsActivated && !panelOpen && !C3CompletionScreen.BlocksGameplayInputThisFrame;

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
            if (panelOpen || activePanel != null || progress == null || puzzle == null || device == null ||
                device.IsActivated || C3CompletionScreen.BlocksGameplayInputThisFrame || !progress.AllPartsCollected) return false;
            selection.Clear();
            activationPanel = puzzle.PuzzleSolved;
            feedback = puzzle.PuzzleSolved ? "解謎已完成；設備尚未啟動。請確認啟動。" : "請依線索選擇三個處理步驟。";
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            activePanel = this;
            panelOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return true;
        }

        public void ClosePanel()
        {
            if (!panelOpen) return;
            panelOpen = false;
            if (activePanel == this) activePanel = null;
            closedFrame = Time.frameCount;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }

        public bool SelectStep(C3ProcessingStep step)
        {
            if (!panelOpen || puzzle.PuzzleSolved || selection.Count >= 3 ||
                step < C3ProcessingStep.Sorting || step > C3ProcessingStep.PollutionTreatment ||
                selection.Contains(step)) return false;
            selection.Add(step);
            feedback = "已選擇 " + selection.Count + "/3 個步驟。";
            return true;
        }

        public void Retry()
        {
            if (!panelOpen || puzzle.PuzzleSolved) return;
            selection.Clear();
            feedback = "已清除順序，請重新選擇。";
        }

        public bool Confirm()
        {
            if (!panelOpen || puzzle.PuzzleSolved) return false;
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
                activationPanel = true;
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
            if (!panelOpen || !activationPanel || device == null || device.Phase != C3DevicePhase.ReadyToActivate) return false;
            ClosePanel();
            return device.TryActivate();
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
            if (panelOpen && Input.GetKeyDown(KeyCode.Escape)) ClosePanel();
        }

        private void OnDisable()
        {
            ClosePanel();
        }


    }
}
