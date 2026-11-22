using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SDGAdventure.SharedPlayer
{
    public enum PlayerMenuState { Playing, Paused, Settings, Tasks }

    // Shared with ModuleCoordinator, which already references the SharedPlayer namespace.
    // Avoids introducing a reverse assembly dependency from SharedPlayer to ModuleSystem.
    public static class OfficialFrontEndSession
    {
        public const string ScenePath =
            "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scenes/SDG_StartAndMainMenu.unity";
        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Active = false;

        public static void Observe(Scene scene, string developmentSelectionPath)
        {
            if (scene.path == ScenePath) Active = true;
            else if (scene.path == developmentSelectionPath) Active = false;
        }
    }

    public sealed class PlayerPauseMenu : MonoBehaviour
    {
        [SerializeField] private SharedFirstPersonController player;
        [SerializeField] private Transform testReturnPoint;
        [SerializeField] private bool standaloneLevelActions;
        [Tooltip("選關場景的完整路徑或名稱；須加入 Build Settings。")]
        [SerializeField] private string selectionScene = "";

        private PlayerInputService input;
        private PlayerMenuCanvas view;
        private PlayerTaskJournal journal;
        private InputActionRebindingExtensions.RebindingOperation activeRebind;
        private int rebindFinishedFrame = -1;
        private float previousTimeScale = 1f;
        private Action restartLevel;
        private Action returnToSelection;
        private Func<bool> canOpen;
        private bool tasksOpenedFromPause;
        private Action openTaskView;
        private Action closeTaskView;
        // Input can be delivered again while the new scene is starting, notably after
        // switching from the official front end. Direct calls to Pause() remain available.
        private float pauseInputBlockedUntilRealtime = -1f;

        public PlayerMenuState State { get; private set; } = PlayerMenuState.Playing;
        public bool HasTestReturnPoint => testReturnPoint != null;
        public bool HasLevelActions => restartLevel != null && returnToSelection != null;
        public bool ShowLevelActions => standaloneLevelActions || HasLevelActions;
        public bool IsRebinding => activeRebind != null;
        public string Feedback { get; private set; } = "";
        public PlayerTaskJournal Journal => journal;
        public PlayerMenuCanvas View => view;
        public bool UsesExternalTaskView => openTaskView != null && closeTaskView != null;

        public void SetPlayer(SharedFirstPersonController value) => player = value;
        public void SetTestReturnPoint(Transform value) => testReturnPoint = value;
        public void SetStandaloneLevelActions(bool enabled) => standaloneLevelActions = enabled;
        public void SetSelectionScene(string scenePathOrName) => selectionScene = scenePathOrName ?? "";
        public void SetLevelActions(Action restart, Action returnAction, Func<bool> canOpenMenu)
        {
            restartLevel = restart;
            returnToSelection = returnAction;
            canOpen = canOpenMenu;
        }
        public void SetTaskView(Action open, Action close)
        {
            openTaskView = open;
            closeTaskView = close;
        }

        private void Awake()
        {
            if (player == null) player = GetComponent<SharedFirstPersonController>();
            journal = GetComponent<PlayerTaskJournal>();
            if (journal == null) journal = gameObject.AddComponent<PlayerTaskJournal>();
            view = GetComponent<PlayerMenuCanvas>();
            if (view == null) view = gameObject.AddComponent<PlayerMenuCanvas>();
            view.Build();
        }

        private void OnEnable()
        {
            input = PlayerInputService.Instance;
            if (input != null)
            {
                input.PausePressed += OnPauseInput;
                input.TaskListPressed += OpenTasks;
                input.UiTaskListPressed += HandleUiTaskList;
                input.UiCancelPressed += HandleCancel;
            }
            if (journal != null) journal.Changed += RefreshTasks;
        }

        private void Start()
        {
            Resume();
            bool officialLevel = OfficialFrontEndSession.Active &&
                gameObject.scene.path.StartsWith("Assets/_Modules_/02_Levels/", StringComparison.Ordinal);
            pauseInputBlockedUntilRealtime = officialLevel ? Time.realtimeSinceStartup + 0.85f : -1f;
        }

        private void OnPauseInput()
        {
            if (pauseInputBlockedUntilRealtime > Time.realtimeSinceStartup)
            {
                Debug.Log("[SDG Official Flow] Ignored pause input carried into level startup.");
                return;
            }
            Pause();
        }

        public void Pause()
        {
            if (State != PlayerMenuState.Playing || input == null || (canOpen != null && !canOpen())) return;
            Feedback = "";
            EnterUi(PlayerMenuState.Paused);
            view.ShowPause(this);
        }

        public void OpenTasks()
        {
            if (State != PlayerMenuState.Playing || input == null || (canOpen != null && !canOpen())) return;
            tasksOpenedFromPause = false;
            EnterUi(PlayerMenuState.Tasks);
            ShowTasks();
        }

        public void OpenTasksFromPause()
        {
            if (State != PlayerMenuState.Paused) return;
            tasksOpenedFromPause = true;
            State = PlayerMenuState.Tasks;
            ShowTasks();
        }

        public void CloseTasks()
        {
            if (State != PlayerMenuState.Tasks) return;
            if (UsesExternalTaskView) closeTaskView();
            if (!tasksOpenedFromPause) { Resume(); return; }
            tasksOpenedFromPause = false;
            State = PlayerMenuState.Paused;
            view.ShowPause(this);
        }

        public void OpenSettings()
        {
            if (State != PlayerMenuState.Paused) return;
            State = PlayerMenuState.Settings;
            Feedback = "";
            view.ShowSettings(this, input);
        }

        public void BackToPause()
        {
            if (State != PlayerMenuState.Settings) return;
            CancelRebind();
            State = PlayerMenuState.Paused;
            view.ShowPause(this);
        }

        public void Resume()
        {
            CancelRebind();
            if (UsesExternalTaskView) closeTaskView();
            tasksOpenedFromPause = false;
            State = PlayerMenuState.Playing;
            view?.Hide();
            Time.timeScale = previousTimeScale;
            input?.SetContext(PlayerInputContext.Gameplay);
            PlayerCursor.LockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void ReturnToTestStart()
        {
            if (testReturnPoint == null || player == null) return;
            player.Teleport(testReturnPoint.position, testReturnPoint.rotation);
            Resume();
        }

        public void RestartLevel()
        {
            if (restartLevel != null) { restartLevel(); return; }
            if (!standaloneLevelActions) return;
            Scene scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path) ||
                !Application.CanStreamedLevelBeLoaded(scene.path))
            {
                ShowPauseFeedback("無法重新開始：目前場景尚未加入 Build Settings，或無法載入。 ");
                return;
            }
            TryLoadScene(scene.path, "無法重新開始本關，請確認場景可載入。");
        }

        public void ReturnToSelection()
        {
            // The official front-end button must not send players to M6_DevelopmentSelection.
            // Preserve the original callbacks and test scene behavior for development sessions.
            if (OfficialFrontEndSession.Active)
            {
                string officialScene = OfficialFrontEndSession.ScenePath;
                if (!Application.CanStreamedLevelBeLoaded(officialScene))
                {
                    ShowPauseFeedback("無法返回主選單：正式前端場景未加入 Build Settings。");
                    return;
                }
                CancelRebind();
                Time.timeScale = 1f;
                input?.SetContext(PlayerInputContext.Inactive);
                TryLoadScene(officialScene, "無法返回正式主選單，請確認場景可載入。");
                return;
            }
            if (returnToSelection != null) { returnToSelection(); return; }
            if (!standaloneLevelActions) return;
            if (string.IsNullOrWhiteSpace(selectionScene))
            {
                ShowPauseFeedback("尚未設定選關場景。");
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(selectionScene))
            {
                ShowPauseFeedback("無法返回選關：場景未加入 Build Settings，或無法載入。");
                return;
            }
            TryLoadScene(selectionScene, "無法返回選關，請確認場景可載入。");
        }

        private void TryLoadScene(string path, string error)
        {
            try { SceneManager.LoadScene(path, LoadSceneMode.Single); }
            catch (Exception) { ShowPauseFeedback(error); }
        }

        private void ShowPauseFeedback(string message)
        {
            Feedback = message;
            if (State == PlayerMenuState.Paused) view.ShowPause(this);
        }

        public void BeginRebind(string map, string action, int bindingIndex)
        {
            if (State != PlayerMenuState.Settings || activeRebind != null) return;
            Feedback = "請按下新按鍵；Esc 取消重綁。";
            activeRebind = input.Keybindings.BeginInteractiveRebind(map, action, bindingIndex,
                (accepted, message) =>
                {
                    activeRebind = null;
                    rebindFinishedFrame = Time.frameCount;
                    Feedback = accepted ? "按鍵已更新並儲存。" : message;
                    if (State == PlayerMenuState.Settings) view.RefreshSettings(this, input);
                });
            view.RefreshSettings(this, input);
        }

        public void CancelRebind()
        {
            if (activeRebind == null) return;
            InputActionRebindingExtensions.RebindingOperation operation = activeRebind;
            activeRebind = null;
            operation.Cancel();
            if (State == PlayerMenuState.Settings) view.RefreshSettings(this, input);
        }

        public void RestoreDefaults()
        {
            CancelRebind();
            input.RestoreDefaults();
            PlayerViewPreference.Mode = PlayerViewMode.FirstPerson;
            PlayerPerspectiveRig rig = GetComponent<PlayerPerspectiveRig>();
            if (rig != null) rig.ApplyMode();
            Feedback = "已恢復預設按鍵、第一人稱視角與設定。";
            view.RefreshSettings(this, input);
        }

        public void TogglePerspectiveMode()
        {
            PlayerViewPreference.Mode = PlayerViewPreference.Mode == PlayerViewMode.FirstPerson
                ? PlayerViewMode.ThirdPerson : PlayerViewMode.FirstPerson;
            PlayerPerspectiveRig rig = GetComponent<PlayerPerspectiveRig>();
            if (rig != null) rig.ApplyMode();
            Feedback = PlayerViewPreference.Mode == PlayerViewMode.FirstPerson
                ? "已切換第一人稱視角。" : "已切換第三人稱視角。";
            view.RefreshSettings(this, input);
        }

        public void ToggleSprintMode()
        {
            ButtonMode current = input.Keybindings.Settings.sprintMode;
            input.SetSprintMode(current == ButtonMode.Hold ? ButtonMode.Toggle : ButtonMode.Hold);
            view.RefreshSettings(this, input);
        }

        public void ToggleCrouchMode()
        {
            ButtonMode current = input.Keybindings.Settings.crouchMode;
            input.SetCrouchMode(current == ButtonMode.Hold ? ButtonMode.Toggle : ButtonMode.Hold);
            view.RefreshSettings(this, input);
        }

        public void SetMouseSensitivity(float value)
        {
            float rounded = Mathf.Round(value * 10f) * 0.1f;
            if (Mathf.Abs(rounded - input.Keybindings.Settings.mouseSensitivity) < 0.05f) return;
            input.SetMouseSensitivity(rounded);
            view.RefreshSettings(this, input);
        }

        private void EnterUi(PlayerMenuState state)
        {
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            State = state;
            Time.timeScale = 0f;
            input.SetContext(PlayerInputContext.UI);
            PlayerCursor.LockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void HandleCancel()
        {
            if (activeRebind != null) { CancelRebind(); return; }
            if (rebindFinishedFrame == Time.frameCount) return;
            if (State == PlayerMenuState.Tasks) CloseTasks();
            else if (State == PlayerMenuState.Settings) BackToPause();
            else if (State == PlayerMenuState.Paused) Resume();
        }

        private void HandleUiTaskList()
        {
            if (State == PlayerMenuState.Tasks) CloseTasks();
            else if (State == PlayerMenuState.Paused && UsesExternalTaskView) OpenTasksFromPause();
        }

        private void ShowTasks()
        {
            if (UsesExternalTaskView)
            {
                view.Hide();
                openTaskView();
            }
            else view.ShowTasks(this, journal);
        }

        private void RefreshTasks()
        {
            if (State == PlayerMenuState.Tasks && !UsesExternalTaskView) view.ShowTasks(this, journal);
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.PausePressed -= OnPauseInput;
                input.TaskListPressed -= OpenTasks;
                input.UiTaskListPressed -= HandleUiTaskList;
                input.UiCancelPressed -= HandleCancel;
                if (input.Context != PlayerInputContext.Inactive)
                    input.SetContext(PlayerInputContext.Inactive);
            }
            if (journal != null) journal.Changed -= RefreshTasks;
            CancelRebind();
            if (UsesExternalTaskView) closeTaskView();
            if (State != PlayerMenuState.Playing) Time.timeScale = previousTimeScale;
            view?.Hide();
            PlayerCursor.LockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
