using C3TreasurePuzzle;
using SDGAdventure.ModuleSystem;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.C3
{
    public sealed class C3ModuleAdapter : MonoBehaviour, IModuleAdapter
    {
        public const string Id = "c3_treasure_puzzle";
        public const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        public string ModuleId => Id;
        private ModuleCoordinator coordinator;
        private C3DeviceState device;
        private C3TreasureProgress progress;
        private C3PuzzleState puzzle;
        private bool reported;
        private C3FirstPersonController player;
        private C3PlayerInteractor interactor;
        private PlayerTaskJournal journal;
        private C3TreasureHud hud;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            if (!ModuleRegistry.TryRegister(new ModuleDefinition(Id, "C3 Treasure Puzzle", ScenePath,
                    typeof(C3ModuleAdapter)), out string error))
                Debug.LogError("C3 registration failed: " + error);
        }

        public bool Initialize(ModuleCoordinator owner)
        {
            coordinator = owner;
            Scene scene = gameObject.scene;
            int deviceCount = 0;
            int playerCount = 0;
            int cameraCount = 0;
            int listenerCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (C3DeviceState item in root.GetComponentsInChildren<C3DeviceState>(true))
                { device = item; deviceCount++; }
                foreach (C3TreasureProgress item in root.GetComponentsInChildren<C3TreasureProgress>(true)) progress = item;
                foreach (C3PuzzleState item in root.GetComponentsInChildren<C3PuzzleState>(true)) puzzle = item;
                foreach (C3TreasureHud item in root.GetComponentsInChildren<C3TreasureHud>(true)) hud = item;
                foreach (C3FirstPersonController item in root.GetComponentsInChildren<C3FirstPersonController>(true))
                { player = item; playerCount++; }
                foreach (C3PlayerInteractor item in root.GetComponentsInChildren<C3PlayerInteractor>(true))
                    interactor = item;
                cameraCount += root.GetComponentsInChildren<Camera>(true).Length;
                listenerCount += root.GetComponentsInChildren<AudioListener>(true).Length;
            }
            if (deviceCount != 1 || progress == null || puzzle == null || playerCount != 1 ||
                interactor == null || hud == null || cameraCount != 1 || listenerCount != 1 ||
                PlayerInputService.Instance == null)
            {
                Debug.LogError($"C3 scene ownership invalid: devices={deviceCount}, players={playerCount}, cameras={cameraCount}, listeners={listenerCount}");
                return false;
            }
            PlayerInputService input = PlayerInputService.Instance;
            player.UseSharedInput(input);
            interactor.UseSharedInput(input);
            PlayerPauseMenu menu = player.GetComponent<PlayerPauseMenu>();
            if (menu == null) menu = player.gameObject.AddComponent<PlayerPauseMenu>();
            menu.SetLevelActions(RestartLevel, ReturnToSelection,
                () => !C3PuzzleConsole.BlocksGameplayInputThisFrame &&
                      !C3CompletionScreen.BlocksGameplayInputThisFrame);
            journal = menu.Journal;
            hud.UseSharedTaskMenu(journal);
            menu.SetTaskView(hud.OpenTaskSheet, hud.CloseTaskSheet);
            C3PuzzleConsole puzzleConsole = FindObjectOfType<C3PuzzleConsole>();
            C3CompletionScreen completionScreen = FindObjectOfType<C3CompletionScreen>();
            if (puzzleConsole == null || completionScreen == null) return false;
            C3UiView view = gameObject.AddComponent<C3UiView>();
            view.Initialize(puzzleConsole, puzzle, hud, completionScreen, interactor);
            RefreshTasks();
            progress.Changed += RefreshTasks;
            puzzle.Solved += RefreshTasks;
            device.Activated += RefreshTasks;
            device.Running += RefreshTasks;
            device.Completed += RefreshTasks;
            // C_Main uses skybox ambient light and has no Lighting Data asset. A fresh
            // project import can start with a zero ambient probe, leaving unlit factory
            // walls black until Unity refreshes the environment. Refresh once per load.
            DynamicGI.UpdateEnvironment();
            device.Completed += OnCompleted;
            if (device.LevelCompleted) OnCompleted();
            return true;
        }

        private void OnCompleted()
        {
            if (reported || coordinator == null || device == null || progress == null || puzzle == null ||
                !device.LevelCompleted || !device.IsActivated || !progress.AllPartsCollected || !puzzle.PuzzleSolved)
                return;
            ModuleResult result = new ModuleResult
            {
                ModuleId = Id,
                Completed = true,
                CollectedParts = progress.CollectedCount,
                PuzzleAttempts = puzzle.SubmissionCount,
                PlaySeconds = device.CompletionSeconds
            };
            reported = coordinator.ReportCompletion(this, result);
        }

        private void RefreshTasks()
        {
            if (journal == null || progress == null || puzzle == null || device == null) return;
            journal.SetTasks(new[]
            {
                new PlayerTaskItem(C3TreasureProgress.FuseId, "尋找保險絲", "在 C 區西側尋寶標記附近。",
                    progress.HasPart(C3TreasureProgress.FuseId)),
                new PlayerTaskItem(C3TreasureProgress.ChipId, "尋找控制晶片", "在二樓高架維修平台。",
                    progress.HasPart(C3TreasureProgress.ChipId)),
                new PlayerTaskItem(C3TreasureProgress.FilterId, "尋找過濾模組", "位於工廠東北側過濾設備附近。",
                    progress.HasPart(C3TreasureProgress.FilterId)),
                new PlayerTaskItem("c3_puzzle", "完成處理順序解謎", "在控制台依作業先後輸入。",
                    puzzle.PuzzleSolved),
                new PlayerTaskItem("c3_activation", device.IsActivated ? "前往 D 區出口" : "啟動淨化設備",
                    device.FactoryState == C3FactoryState.Starting ? "工廠正在重新啟動。" :
                    device.IsActivated ? "輸送帶已開始分類。請步行前往南側 D 區出口。" : "完成解謎後於控制台確認啟動。",
                    device.LevelCompleted)
            });
        }

        private void RestartLevel()
        {
            if (coordinator != null && !coordinator.RestartCurrent(out string error))
                Debug.LogError("C3 restart failed: " + error);
        }

        private void ReturnToSelection()
        {
            if (coordinator != null && !coordinator.ReturnToSelection(out string error))
                Debug.LogError("C3 return failed: " + error);
        }

        public void Shutdown()
        {
            if (hud != null) hud.CloseTaskSheet();
            if (device != null)
            {
                device.Completed -= OnCompleted;
                device.Activated -= RefreshTasks;
                device.Running -= RefreshTasks;
                device.Completed -= RefreshTasks;
            }
            if (progress != null) progress.Changed -= RefreshTasks;
            if (puzzle != null) puzzle.Solved -= RefreshTasks;
            device = null;
            progress = null;
            puzzle = null;
            player = null;
            interactor = null;
            journal = null;
            hud = null;
            coordinator = null;
        }

        private void OnDestroy() => Shutdown();
    }
}
