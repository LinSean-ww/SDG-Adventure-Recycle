using UnityEngine;
using SDGAdventure.ModuleSystem;
using SDGAdventure.SharedPlayer;

namespace C3TreasurePuzzle
{
    public sealed class C3CompletionScreen : MonoBehaviour
    {
        public const string Knowledge = "SDG 12 責任消費與生產：正確分類有助於後續資源回收。可再利用的材料應優先回收或再利用；剩餘污染物需要適當處理。減少浪費、改善資源利用效率，有助於更負責任的生產與消費。本關流程是簡化教育謎題，並非所有工業廢棄物的通用處理順序。";
        [SerializeField] private C3TreasureProgress progress;
        [SerializeField] private C3PuzzleState puzzle;
        [SerializeField] private C3DeviceState device;
        private bool visible;
        private bool restarting;
        private static C3CompletionScreen active;
        public bool Visible => visible;
        public static bool BlocksGameplayInputThisFrame => active != null && active.visible;
        public string PartsText => progress == null ? "0/3" : progress.CollectedCount + "/" + C3TreasureProgress.RequiredCount;
        public int PuzzleAttempts => puzzle == null ? 0 : puzzle.SubmissionCount;
        public float PlaySeconds => device == null ? 0f : device.ElapsedSeconds;
        public string CompletionText => device != null && device.LevelCompleted ? "已完成" : "未完成";

        public void Configure(C3TreasureProgress owner, C3PuzzleState puzzleState, C3DeviceState deviceState)
        {
            progress = owner;
            puzzle = puzzleState;
            device = deviceState;
        }

        private void Awake()
        {
            if (progress == null) progress = GetComponent<C3TreasureProgress>();
            if (puzzle == null) puzzle = GetComponent<C3PuzzleState>();
            if (device == null) device = GetComponent<C3DeviceState>();
        }

        private void OnEnable()
        {
            if (device != null) device.Completed += Show;
        }

        private void OnDisable()
        {
            if (device != null) device.Completed -= Show;
            if (active == this) active = null;
        }

        public void Show()
        {
            if (visible || device == null || !device.LevelCompleted) return;
            visible = true;
            active = this;
            PlayerCursor.LockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void RestartLevel()
        {
            if (!visible || restarting) return;
            restarting = true;
            active = null;
            string error = "Module coordinator is unavailable.";
            if (ModuleCoordinator.Instance == null ||
                !ModuleCoordinator.Instance.RestartCurrent(out error))
            {
                restarting = false;
                Debug.LogError("C3 restart failed: " + error);
            }
        }

        public void ContinueToNext()
        {
            if (!visible || restarting || ModuleCoordinator.Instance == null) return;
            if (!ModuleCoordinator.Instance.TryLaunchNext(out string error))
                Debug.LogWarning("C3 next level unavailable: " + error);
        }

        public void ReturnToSelection()
        {
            if (!visible || ModuleCoordinator.Instance == null) return;
            if (!ModuleCoordinator.Instance.ReturnToSelection(out string error))
                Debug.LogError("C3 return failed: " + error);
        }

        public static Rect CalculatePanelRect(int width, int height)
        {
            float margin = Mathf.Min(20f, Mathf.Min(width, height) * 0.04f);
            return new Rect(margin, margin, width - 2f * margin, height - 2f * margin);
        }



        public static string FormatTime(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }
    }
}
