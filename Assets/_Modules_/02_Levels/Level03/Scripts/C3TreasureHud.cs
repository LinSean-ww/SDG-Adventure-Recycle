using UnityEngine;
using SDGAdventure.SharedPlayer;

namespace C3TreasurePuzzle
{
    public sealed class C3TreasureHud : MonoBehaviour
    {
        [SerializeField] private C3TreasureProgress progress;
        private C3DeviceState device;
        private bool taskSheetOpen;
        private bool sharedTaskControl;
        private PlayerTaskJournal taskJournal;
        private float feedbackUntil;

        public const string CollapsedText = "任務";
        public bool TaskSheetOpen => taskSheetOpen;
        public bool CollectionFeedbackActive => Time.unscaledTime < feedbackUntil;
        public void SetProgress(C3TreasureProgress value) => progress = value;
        public void UseSharedTaskMenu(PlayerTaskJournal journal)
        {
            taskJournal = journal;
            sharedTaskControl = journal != null;
        }

        private void Awake()
        {
            if (progress == null) progress = FindObjectOfType<C3TreasureProgress>();
            device = FindObjectOfType<C3DeviceState>();
        }

        private void OnEnable()
        {
            if (progress != null) progress.Changed += OnChanged;
        }

        private void OnDisable()
        {
            if (progress != null) progress.Changed -= OnChanged;
            CloseTaskSheet();
        }

        private void OnChanged() => feedbackUntil = Time.unscaledTime + 3f;

        private void Update()
        {
            // Legacy Input key edge: holding P does not repeatedly toggle the sheet.
            if (!sharedTaskControl && !C3PuzzleConsole.BlocksGameplayInputThisFrame &&
                !C3CompletionScreen.BlocksGameplayInputThisFrame &&
                Input.GetKeyDown(KeyCode.P)) ToggleTaskSheet();
        }

        public void OpenTaskSheet() => taskSheetOpen = true;

        public void CloseTaskSheet()
        {
            taskSheetOpen = false;
        }

        public void ToggleTaskSheet()
        {
            if (taskSheetOpen) CloseTaskSheet();
            else OpenTaskSheet();
        }

        private string InteractionKey => sharedTaskControl && PlayerInputService.Instance != null
            ? PlayerInputService.Instance.Keybindings.BindingDisplay("Gameplay", "Interact", 0) : "F";

        public string GuidanceText => progress == null ? "" : device != null && device.FactoryState == C3FactoryState.Completed
            ? "本關卡已完成"
            : device != null && device.FactoryState == C3FactoryState.Running
                ? "工廠已恢復運作！輸送帶正在分類廢料。請步行前往南側 D 區出口"
            : device != null && device.FactoryState == C3FactoryState.Starting
                ? "工廠正在重新啟動，請等待設備恢復運作"
            : device != null && device.Phase == C3DevicePhase.ReadyToActivate
                ? "解謎完成，請在目前面板按「確認啟動設備」"
                : progress.AllPartsCollected
                    ? "零件收集完成，請前往東北側控制台\n按 " + InteractionKey + " 開啟解謎面板"
                    : progress.Objective;

        public string StatusText => progress == null ? "" :
            "尋找淨化設備零件\n" + progress.CollectedCount + "/3\n" +
            "保險絲：" + State(C3TreasureProgress.FuseId) + "\n" +
            "控制晶片：" + State(C3TreasureProgress.ChipId) + "\n" +
            "過濾模組：" + State(C3TreasureProgress.FilterId) + "\n" + GuidanceText;

        private string State(string id) => progress.HasPart(id) ? "已取得" : "未取得";

        private static float Scale(int width, int height) =>
            Mathf.Clamp(Mathf.Min(width / 1920f, height / 1080f), 0.8f, 1.2f);

        public static Rect CalculateCompactRect(int width, int height)
        {
            float scale = Scale(width, height);
            return new Rect(16f * scale, 16f * scale, 82f * scale, 40f * scale);
        }

        public static Rect CalculateSheetRect(int width, int height, float desiredHeight)
        {
            float scale = Scale(width, height);
            float margin = 16f * scale;
            return new Rect(margin, margin,
                Mathf.Min(540f * scale, width * 0.44f),
                Mathf.Min(desiredHeight, height - margin * 2f));
        }

        // Order: heading, mission, progress, task rows, hint heading, guidance, close hint.
        // OnGUI supplies the height measured from each actual string and its drawing width.
        public static Rect[] CalculateContentRects(float scale, float contentWidth, float[] heights)
        {
            if (heights == null || heights.Length < 7)
                throw new System.ArgumentException("Measured task section heights are required.", nameof(heights));
            Rect[] rects = new Rect[heights.Length];
            float x = 8f * scale;
            float width = contentWidth - 16f * scale;
            float y = 10f * scale;
            for (int i = 0; i < rects.Length; i++)
            {
                rects[i] = new Rect(x, y, width, heights[i]);
                y += heights[i] + (i == rects.Length - 4 || i == rects.Length - 2 ? 13f : 8f) * scale;
            }
            return rects;
        }












    }
}
