using System.Linq;
using System;
using SDGAdventure.ModuleSystem;
using SDGAdventure.SharedPlayer;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Level01ModuleAdapter : MonoBehaviour, IModuleAdapter
{
    public const string ScenePath = "Assets/_Modules_/02_Levels/Level01/Imported/Scenes/WTF.unity";
    public string ModuleId => "level01";
    private TreasureDetector detector;
    private PlayerTaskJournal journal;
    private TextMeshProUGUI tips;
    private ModuleCoordinator coordinator;
    private PlayerInputService input;
    private Func<bool> blockWeaponInput;
    private Level01ExitTrigger exit;
    private Level01CompletionView completionView;
    private DoorInteraction powerDoor;
    private bool completed;
    private float startTime;
    private float previousTimeScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void Register()
    {
        if (!ModuleRegistry.TryRegister(new ModuleDefinition("level01", "第一關", ScenePath,
                typeof(Level01ModuleAdapter)), out string error)) Debug.LogError(error);
    }

    public bool Initialize(ModuleCoordinator owner)
    {
        coordinator = owner;
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (detector == null) detector = root.GetComponentInChildren<TreasureDetector>(true);
            if (journal == null) journal = root.GetComponentInChildren<PlayerTaskJournal>(true);
            foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.gameObject.name == "tips") tips = text;
        }
        if (detector == null || journal == null || tips == null) return false;
        var roots = scene.GetRootGameObjects();
        var scanner = detector.GetComponent<scanball>();
        var door = roots.SelectMany(x => x.GetComponentsInChildren<DoorInteraction>(true)).FirstOrDefault();
        var gates = roots.SelectMany(x => x.GetComponentsInChildren<NewDoor>(true)).ToArray();
        exit = roots.SelectMany(x => x.GetComponentsInChildren<Level01ExitTrigger>(true)).FirstOrDefault();
        var player = journal.GetComponent<SharedFirstPersonController>();
        if (scanner == null || door == null || exit == null || player == null) return false;
        powerDoor = door;
        startTime = Time.time;
        completed = false;
        exit.Initialize(detector, door, player, Complete);
        Level01Input.Configure(scanner, detector, door, gates);
        input = PlayerInputService.Instance;
        if (input == null) return false;
        blockWeaponInput = () => Level01Input.IsBusy;
        input.SetGameplayActionBlocker(blockWeaponInput);
        var menu = journal.GetComponent<PlayerPauseMenu>();
        if (menu != null) menu.SetLevelActions(Restart, ReturnToSelection, () => true);
        detector.ProgressChanged += UpdateTasks;
        UpdateTasks();
        return true;
    }

    private void Complete()
    {
        if (completed || coordinator == null || detector == null || powerDoor == null ||
            detector.TotalCount != 10 || detector.CollectedCount != 10 || !powerDoor.IsOpen) return;
        var result = new ModuleResult { ModuleId = ModuleId, Completed = true,
            CollectedParts = detector.CollectedCount, PlaySeconds = Time.time - startTime };
        if (!coordinator.ReportCompletion(this, result)) return;
        completed = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        input.SetContext(PlayerInputContext.UI);
        PlayerCursor.LockState = CursorLockMode.None;
        Cursor.visible = true;
        completionView = gameObject.AddComponent<Level01CompletionView>();
        completionView.Show(ReturnToSelection, Restart);
    }

    private void UpdateTasks()
    {
        if (journal == null || detector == null) return;
        journal.SetTasks(new[] { new PlayerTaskItem("level01_recycle", "回收垃圾",
            $"依序回收 {detector.CollectedCount}/{detector.TotalCount} 個目標。",
            detector.IsAllTreasuresFound()) });
    }

    private void Update()
    {
        if (tips != null && PlayerInputService.Instance != null)
        {
            tips.rectTransform.sizeDelta = new Vector2(Mathf.Min(700f, Mathf.Max(0f, Screen.width - 32f)), 50f);
            tips.text = $"掃描：{Level01Input.Binding("ScanAim")}　互動：{Level01Input.Binding("Interact")}　挖掘：{Level01Input.Binding("Fire")}";
        }
    }

    private void Restart() { if (coordinator != null && !coordinator.RestartCurrent(out string error)) Debug.LogError(error); }
    private void ReturnToSelection() { if (coordinator != null && !coordinator.ReturnToSelection(out string error)) Debug.LogError(error); }
    public void Shutdown()
    {
        if (exit != null) exit.Clear();
        if (completed) Time.timeScale = previousTimeScale;
        exit = null; powerDoor = null; completionView = null; completed = false;
        if (input != null) input.ClearGameplayActionBlocker(blockWeaponInput);
        input = null; blockWeaponInput = null;
        Level01Input.Clear();
        if (detector != null) detector.ProgressChanged -= UpdateTasks;
        detector = null; journal = null; tips = null; coordinator = null;
    }
    private void OnDestroy() => Shutdown();
}
