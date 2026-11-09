using System;
using System.Linq;
using SDGAdventure.ModuleSystem;
using SDGAdventure.SharedPlayer;
using UnityEngine;

public sealed class Level02ModuleAdapter : MonoBehaviour, IModuleAdapter
{
    public const string Id = "level02";
    public const string ScenePath = "Assets/_Modules_/02_Levels/Level02/Imported/Scenes/SampleScene.unity";
    public string ModuleId => Id;
    private ModuleCoordinator coordinator;
    private SlidingStarGate gate;
    private StarGoalPlate plate;
    private Level02BoxCarryController carry;
    private Level02HeadlampController headlamp;
    private PlayerInputService input;
    private PlayerPauseMenu menu;
    private PlayerTaskJournal journal;
    private Func<bool> blockWeapons;
    private bool completed;
    private float startTime, previousTimeScale;
    private int lastPlaced = -1, lastTotal = -1;
    private bool lastPlateComplete;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void Register()
    {
        if (!ModuleRegistry.TryRegister(new ModuleDefinition(Id, "第二關", ScenePath, typeof(Level02ModuleAdapter)), out string error))
            Debug.LogError(error);
    }

    public bool Initialize(ModuleCoordinator owner)
    {
        coordinator = owner;
        var roots = gameObject.scene.GetRootGameObjects();
        var players = roots.SelectMany(r => r.GetComponentsInChildren<SharedFirstPersonController>(false)).ToArray();
        var gates = roots.SelectMany(r => r.GetComponentsInChildren<SlidingStarGate>(false)).ToArray();
        var plates = roots.SelectMany(r => r.GetComponentsInChildren<StarGoalPlate>(false)).ToArray();
        input = PlayerInputService.Instance;
        if (players.Length != 1 || gates.Length != 1 || plates.Length != 1 || !input) return false;
        var player = players[0];
        var motor = player.GetComponent<CharacterController>();
        var interactor = player.GetComponent<PlayerInteractor>();
        menu = player.GetComponent<PlayerPauseMenu>();
        journal = player.GetComponent<PlayerTaskJournal>();
        if (!motor || !interactor || !menu || !journal || !player.GetComponentInChildren<Camera>()) return false;
        headlamp = gameObject.AddComponent<Level02HeadlampController>();
        headlamp.Initialize(player.transform, input);
        gate = gates[0]; plate = plates[0];
        carry = player.gameObject.AddComponent<Level02BoxCarryController>();
        gate.SetPlayer(motor);
        gate.PlayerPassedAndGateLocked += Complete;
        blockWeapons = () => carry && carry.HeldBox;
        input.SetGameplayActionBlocker(blockWeapons);
        menu.SetLevelActions(Restart, Return, () => !completed);
        startTime = Time.time;
        return true;
    }

    private void Update()
    {
        if (!journal || !plate || (lastPlaced == plate.PlacedStars && lastTotal == plate.TotalStars && lastPlateComplete == plate.IsComplete)) return;
        lastPlaced = plate.PlacedStars; lastTotal = plate.TotalStars;
        lastPlateComplete = plate.IsComplete;
        journal.SetTasks(new[] { new PlayerTaskItem("level02_stars", "尋找星星箱", $"將星星箱放上平台：{lastPlaced}/{lastTotal}", plate.IsComplete) });
    }

    private void Complete()
    {
        if (completed || !coordinator || !plate || !plate.IsComplete || !gate || !gate.IsLocked) return;
        if (!coordinator.ReportCompletion(this, new ModuleResult { ModuleId = Id, Completed = true, PuzzleAttempts = plate.PlacedStars, PlaySeconds = Time.time - startTime })) return;
        completed = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        input.SetContext(PlayerInputContext.UI);
        PlayerCursor.LockState = CursorLockMode.None;
        Cursor.visible = true;
        gameObject.AddComponent<Level02CompletionView>().Show(Return, Restart);
    }

    private void Restart() { if (coordinator && !coordinator.RestartCurrent(out string error)) Debug.LogError(error); }
    private void Return() { if (coordinator && !coordinator.ReturnToSelection(out string error)) Debug.LogError(error); }

    public void Shutdown()
    {
        if (gate) gate.PlayerPassedAndGateLocked -= Complete;
        if (input && blockWeapons != null) input.ClearGameplayActionBlocker(blockWeapons);
        if (headlamp) { Destroy(headlamp); headlamp = null; }
        if (carry) { carry.enabled = false; Destroy(carry); }
        if (completed) Time.timeScale = previousTimeScale;
        coordinator = null; gate = null; plate = null; carry = null;
    }
    private void OnDestroy() => Shutdown();
}
