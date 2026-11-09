using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SDGAdventure.SharedPlayer;
using C3TreasurePuzzle;

// One persistent inventory/equipment owner. The camera and controller stay scene-owned.
public sealed class EcoAdventureRuntime : MonoBehaviour
{
    public static EcoAdventureRuntime Instance { get; private set; }

    [Serializable]
    public sealed class InventoryItem
    {
        public string id;
        public string name;
        public int quantity;
    }

    public const string WeaponId = "eco_quantum_projector";
    public const string MetalId = "eco_recycled_metal";
    public const string PlasticId = "eco_recycled_plastic";
    public const string CircuitId = "eco_recovered_circuit";

    public readonly List<InventoryItem> inventory = new List<InventoryItem>();
    public string[] quickSlots = new string[5];
    public int selectedSlot;
    public bool factoryPowered;
    public bool weaponCrafted;
    public int ammoInMagazine = 24;
    public int reserveAmmo = 72;

    // The left number in the weapon HUD is the loaded magazine, not the reserve.
    public const int MagazineCapacity = 24;
    public const int MaxStoredEnergy = 120;
    private int completedPlasticSorts;
    private int completedMetalSorts;
    private int completedPaperSorts;
    public const int RecyclingReceiptCapacity=4095;
    private readonly HashSet<string> recyclingReceipts=new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> recyclingBatches=new HashSet<string>(StringComparer.Ordinal);
    public int RecyclingReceiptCount => recyclingReceipts.Count;
    public bool HasRecyclingReceipt(string materialId) => recyclingReceipts.Contains(materialId);
    // One atomic processing receipt. SceneLoaded resets local unlock counters,
    // but retains these IDs so a completed upstream item cannot be replayed.
    public bool TryCommitRecyclingBatch(string batchId,EcoRecycleCargo.Kind materialType,string[] materialIds)
    {
        int kind=C3RecyclingLine.LegacyKind(materialType);
        if (!factoryPowered || kind<0 || string.IsNullOrWhiteSpace(batchId) || batchId.Length>128 || recyclingBatches.Contains(batchId) ||
            materialIds==null || materialIds.Length!=C3RecyclingLine.UnitsPerBale ||
            recyclingReceipts.Count+materialIds.Length>RecyclingReceiptCapacity) return false;
        HashSet<string> unique=new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in materialIds)
            if (string.IsNullOrWhiteSpace(id) || id.Length>128 || !unique.Add(id) || recyclingReceipts.Contains(id)) return false;
        recyclingBatches.Add(batchId);
        foreach (string id in materialIds) { recyclingReceipts.Add(id); ReceiveSortedMaterial(kind); }
        return true;
    }

    // A weapon carried in inventory does not recharge unless it is held.
    public bool IsQuantumWeaponEquipped => WeaponEquipped();
    public bool IsGameplayInputAllowed => GameplayEnabled();
    public bool CanAutoRecharge => IsQuantumWeaponEquipped &&
        !inventoryOpen && !reloading && ammoInMagazine < MagazineCapacity;

    private Camera activeCamera;
    private GameObject weaponModel;
    private EcoQuantumWorldPickup droppedWeapon;
    private readonly HashSet<string> collectedWorkshopMaterials = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<EcoMaterialWorldPickup> droppedMaterials = new HashSet<EcoMaterialWorldPickup>();
    private string presentedId;
    private bool inventoryOpen;
    private readonly HashSet<object> quickbarSuppressors = new HashSet<object>();
    public void SuppressQuickbar(object owner) { if (owner != null) quickbarSuppressors.Add(owner); }
    public void RestoreQuickbar(object owner) { if (owner != null) quickbarSuppressors.Remove(owner); }
    public bool QuickbarSuppressed => quickbarSuppressors.Count > 0;
    private bool reloading;
    private float nextShotTime;
    private float nextPlasmaShotTime;
    private int totalShots;
    private string message = "";
    private float messageEnd;
    private GUIStyle centeredStyle;
    private GUIStyle inventoryStyle;
    private float initialFov = 60f;
    private Coroutine reloadRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearStatic()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject host = new GameObject("EcoAdventureRuntime_PersistentInventory");
        DontDestroyOnLoad(host);
        host.AddComponent<EcoAdventureRuntime>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += SceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= SceneLoaded;
        Instance = null;
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Replaying C begins a fresh sorting cycle. Inventory and loaded energy persist.
        if (scene.name == "C_Main")
        {
            factoryPowered = false;
            completedPlasticSorts = 0;
            completedMetalSorts = 0;
            completedPaperSorts = 0;
        }
        // Inventory and ammo survive scene changes, camera models do not.
        activeCamera = null;
        weaponModel = null;
        presentedId = null;
        inventoryOpen = false;
        reloading = false;
        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        reloadRoutine = null;
        BindCamera();
    }

    private void BindCamera()
    {
        Camera main = Camera.main;
        if (main == null)
        {
            activeCamera = null;
            return;
        }
        bool playerCamera =
            main.GetComponentInParent<C3FirstPersonController>() != null ||
            main.GetComponentInParent<SharedFirstPersonController>() != null;
        if (!playerCamera)
        {
            activeCamera = null; // Do not show an inventory overlay in the level menu.
            return;
        }
        if (main == activeCamera) return;
        activeCamera = main;
        initialFov = main.fieldOfView;
        presentedId = null;
    }

    public bool Has(string id, int count = 1)
    {
        InventoryItem entry = inventory.Find(x => x.id == id);
        return entry != null && entry.quantity >= count;
    }

    public void AddItem(string id, string displayName, int quantity = 1)
    {
        if (string.IsNullOrEmpty(id) || quantity <= 0 || (id == WeaponId && (droppedWeapon || Has(WeaponId))) ||
            (id == SDGAdventure.Level04.Level04PlasmaPickup.WeaponId && (quantity != 1 || Has(id) || SDGAdventure.Level04.Level04PlasmaPickup.HasDroppedWeapon))) return;
        InventoryItem entry = inventory.Find(x => x.id == id);
        if (entry == null)
        {
            entry = new InventoryItem { id = id, name = displayName, quantity = 0 };
            inventory.Add(entry);
        }
        entry.quantity += quantity;
        EquipFirstAvailable(id);
        Notify("已取得 " + displayName + " × " + quantity);
    }

    // All slot mutations pass through these helpers. Inventory quantities remain
    // independent from slot assignments, including stacks of the same ID.
    public bool AssignQuickSlot(int target, string id)
    {
        RepairQuickSlots();
        if (target < 0 || target >= quickSlots.Length ||
            (!string.IsNullOrEmpty(id) && !Has(id))) return false;
        string previous = quickSlots[target];
        int source = string.IsNullOrEmpty(id) ? -1 : Array.IndexOf(quickSlots, id);
        if (source == target) return true;
        if (source >= 0) quickSlots[source] = previous;
        quickSlots[target] = string.IsNullOrEmpty(id) ? null : id;
        RefreshHeldAfterSlotsChanged();
        return true;
    }

    public void RepairQuickSlots()
    {
        // Preserve the selected copy of an old duplicate so it stays held.
        string selected = CurrentSlotId();
        bool changed = false;
        for (int i = 0; i < quickSlots.Length; i++)
        {
            string id = quickSlots[i];
            if (string.IsNullOrEmpty(id)) continue;
            bool duplicate = i != selectedSlot && id == selected;
            for (int j = 0; !duplicate && j < i; j++)
                duplicate = quickSlots[j] == id;
            if (!Has(id) || duplicate)
            { quickSlots[i] = null; changed = true; }
        }
        if (changed) RefreshHeldAfterSlotsChanged();
    }

    private void RefreshHeldAfterSlotsChanged()
    {
        if (reloading && !WeaponEquipped()) CancelReload();
        if (activeCamera != null && presentedId != CurrentSlotId()) BuildHeldModel();
    }

    private void RemoveSlotsFor(string id)
    {
        for (int i = 0; i < quickSlots.Length; i++)
            if (quickSlots[i] == id) quickSlots[i] = null;
    }

    public bool Consume(string id, int count)
    {
        if (count <= 0) return false;
        InventoryItem entry = inventory.Find(x => x.id == id);
        if (entry == null || entry.quantity < count) return false;
        entry.quantity -= count;
        if (entry.quantity == 0)
        {
            inventory.Remove(entry);
            RemoveSlotsFor(id);
            RefreshHeldAfterSlotsChanged();
        }
        return true;
    }

    // Validates all prerequisites before removing any of the three stacks.
    public bool ConsumeSubmittedParts()
    {
        string[] ids = { C3TreasureProgress.FuseId, C3TreasureProgress.ChipId, C3TreasureProgress.FilterId };
        foreach (string id in ids) if (!Has(id)) return false;
        foreach (string id in ids)
        {
            inventory.RemoveAll(item => item.id == id);
            RemoveSlotsFor(id);
        }
        return true;
    }

    public void RefreshInventoryPresentation() => RefreshHeldAfterSlotsChanged();

    public void EquipFirstAvailable(string id)
    {
        RepairQuickSlots();
        if (string.IsNullOrEmpty(id) || !Has(id) || Array.IndexOf(quickSlots, id) >= 0) return;
        if (selectedSlot >= 0 && selectedSlot < quickSlots.Length && string.IsNullOrEmpty(quickSlots[selectedSlot]))
        { AssignQuickSlot(selectedSlot, id); return; }
        for (int i = 0; i < quickSlots.Length; i++)
            if (string.IsNullOrEmpty(quickSlots[i]))
            { AssignQuickSlot(i, id); return; }
    }

    public void FactoryPoweredOn()
    {
        if (factoryPowered) return;
        factoryPowered = true;
        Notify("回收分類系統已啟動：發現隱藏組裝程序");
    }

    // 0 = plastic, 1 = metal, 2 = paper. Sorting unlocks the scattered material
    // depots, it must NOT hand materials directly to the player inventory.
    public void ReceiveSortedMaterial(int kind)
    {
        if (!factoryPowered) return;
        if (kind == 0) completedPlasticSorts++;
        else if (kind == 1) completedMetalSorts++;
        else if (kind == 2) completedPaperSorts++;
    }

    public bool HasSortedMaterial(int kind)
    {
        return kind == 0 ? completedPlasticSorts > 0 :
            kind == 1 ? completedMetalSorts > 0 : kind == 2 && completedPaperSorts > 0;
    }

    public bool RecoverSortedMaterial(int kind)
    {
        if (!factoryPowered)
        {
            Notify("請先啟動工廠分類線");
            return false;
        }
        if (kind != 0 && kind != 1) return false;
        if (!HasSortedMaterial(kind))
        {
            Notify(kind == 0 ? "等待塑膠完成分類" : "等待金屬完成分類");
            return false;
        }
        string id = kind == 0 ? PlasticId : MetalId;
        if (Has(id) || weaponCrafted || WasWorkshopMaterialCollected(id)) return false;
        AddItem(id, kind == 0 ? "再生塑膠" : "再生金屬");
        collectedWorkshopMaterials.Add(id);
        return true;
    }

    // Salvage refills the loaded weapon (left HUD value), NEVER the spare bank.
    // e.g. 0/93 + plastic bottle +5 -> 5/93.
    public int AddEnergyFromSalvage(string salvageName, int requestedAmount)
    {
        if (!CanAutoRecharge || requestedAmount <= 0) return 0;
        int loaded = Mathf.Clamp(ammoInMagazine, 0, MagazineCapacity);
        int added = Mathf.Min(requestedAmount, MagazineCapacity - loaded);
        if (added <= 0) return 0;
        ammoInMagazine = loaded + added;
        Notify("已回收" + salvageName + "，槍內能量 +" + added +
            "（" + ammoInMagazine + "/" + MagazineCapacity +
            "），備用 " + reserveAmmo + " 不變");
        return added;
    }

    // Return a result so the material cache can disappear only on a real pickup.
    public bool RecoverCircuit()
    {
        if (!factoryPowered)
        {
            Notify("請先恢復工廠運作");
            return false;
        }
        if (Has(CircuitId) || weaponCrafted || WasWorkshopMaterialCollected(CircuitId))
        {
            Notify("已取回電路板");
            return false;
        }
        AddItem(CircuitId, "廢棄電路板");
        collectedWorkshopMaterials.Add(CircuitId);
        return true;
    }

    public bool CraftWeapon()
    {
        if (!factoryPowered)
        {
            Notify("需要先啟動控制器");
            return false;
        }
        if (weaponCrafted || Has(WeaponId) || droppedWeapon)
        {
            Notify("循環量子投射器已組裝完成");
            return false;
        }
        if (!Has(MetalId) || !Has(PlasticId) || !Has(CircuitId))
        {
            Notify("缺少材料：再生金屬、再生塑膠、廢棄電路板各一");
            return false;
        }

        Consume(MetalId, 1);
        Consume(PlasticId, 1);
        Consume(CircuitId, 1);
        weaponCrafted = true;
        AddItem(WeaponId, "循環量子投射器");
        EquipFirstAvailable(WeaponId);
        Notify("彩蛋解鎖！開啟背包並使用快捷欄裝備");
        return true;
    }

    public void Notify(string text, float seconds = 4.5f)
    {
        message = text;
        messageEnd = Time.unscaledTime + Mathf.Max(0.1f, seconds);
    }

    private bool GameplayEnabled()
    {
        PlayerInputService service = PlayerInputService.Instance;
        return !inventoryOpen && PlayerCursor.LockState == CursorLockMode.Locked &&
            (service == null || service.Context == PlayerInputContext.Gameplay) &&
            !C3PuzzleConsole.BlocksGameplayInputThisFrame &&
            !C3CompletionScreen.BlocksGameplayInputThisFrame;
    }

    // This action is independent of Movement / W, A, S or D.
    private bool FirePressed(Mouse mouse)
    {
        PlayerInputService input = PlayerInputService.Instance;
        if (input != null && input.Actions != null)
        {
            InputAction action = input.Actions.FindAction("Gameplay/Fire", false);
            if (action != null) return action.IsPressed();
        }
        return mouse != null && mouse.leftButton.isPressed;
    }

    private static bool CarryingLevel04TaskItem => SDGAdventure.Level04.Level04RecycleController.Instance && SDGAdventure.Level04.Level04RecycleController.Instance.Carried;
    public string CarriedTaskItemText => CarryingLevel04TaskItem ? "任務攜帶：" + SDGAdventure.Level04.Level04RecycleController.ItemName(SDGAdventure.Level04.Level04RecycleController.Instance.Carried.itemType) + " ×1" : "";

    private void Update()
    {
        BindCamera();
        RepairQuickSlots();
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null) return;

        // A click inside the Game view can restore gameplay cursor capture.
        // Never steal the cursor from an open puzzle, backpack or pause menu.
        PlayerInputService input = PlayerInputService.Instance;
        if (activeCamera != null && !inventoryOpen &&
            input != null && input.Context == PlayerInputContext.Gameplay &&
            !C3PuzzleConsole.BlocksGameplayInputThisFrame &&
            !C3CompletionScreen.BlocksGameplayInputThisFrame &&
            PlayerCursor.LockState != CursorLockMode.Locked &&
            mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            PlayerCursor.LockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        PlayerPauseMenu pauseMenu = activeCamera != null ? activeCamera.GetComponentInParent<PlayerPauseMenu>() : null;
        bool menuOpen = pauseMenu != null && pauseMenu.State != PlayerMenuState.Playing;
        if (inventoryOpen && !menuOpen && keyboard.escapeKey.wasPressedThisFrame && pauseMenu != null)
        {
            ToggleInventory();
            pauseMenu.Pause();
            return;
        }
        if (menuOpen) { inventoryOpen = false; return; }
        if ((input != null ? input.InventoryWasPressed : keyboard.tabKey.wasPressedThisFrame) &&
            (inventoryOpen || GameplayEnabled()))
            ToggleInventory();

        if (!GameplayEnabled()) return;

        if (input != null && !CarryingLevel04TaskItem)
        {
            for (int i = 1; i <= 5; i++)
                if (input.SlotWasPressed(i)) selectedSlot = i - 1;
        }
        else if (input == null && !CarryingLevel04TaskItem)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) selectedSlot = 0;
            if (keyboard.digit2Key.wasPressedThisFrame) selectedSlot = 1;
            if (keyboard.digit3Key.wasPressedThisFrame) selectedSlot = 2;
            if (keyboard.digit4Key.wasPressedThisFrame) selectedSlot = 3;
            if (keyboard.digit5Key.wasPressedThisFrame) selectedSlot = 4;
        }

        // In C3 the existing action-service interactor dispatches quest OR
        // workshop interaction. Retain raw-F fallback in other scenes or when
        // that interactor is disabled; do not poll the same input twice.
        C3PlayerInteractor c3Interactor = activeCamera != null
            ? activeCamera.GetComponentInParent<C3PlayerInteractor>() : null;
        if (keyboard.fKey.wasPressedThisFrame &&
            (c3Interactor == null || !c3Interactor.HandlesInteractionInput))
            InteractWorkshop();

        if (input != null && input.DropWasPressed &&
            !SDGAdventure.Level04.Level04RecycleController.TryHandleDrop()) TryDropCurrent();

        if (CarryingLevel04TaskItem)
        {
            if (reloading) CancelReload();
            return;
        }

        if (reloading && !WeaponEquipped()) CancelReload();
        if (presentedId != CurrentSlotId()) BuildHeldModel();
        if (WeaponEquipped())
        {
            if ((input != null ? input.ReloadWasPressed : keyboard.rKey.wasPressedThisFrame) &&
                !reloading && ammoInMagazine < MagazineCapacity && reserveAmmo > 0)
                reloadRoutine = StartCoroutine(Reload());
            if ((input == null || !input.GameplayActionsBlocked) && FirePressed(mouse)) Shoot();
        }

        if (CurrentSlotId() == SDGAdventure.Level04.Level04PlasmaPickup.WeaponId && Has(CurrentSlotId()) &&
            input != null && !input.GameplayActionsBlocked && input.PrimaryPressed) ShootPlasma();
        if (presentedId != CurrentSlotId())
            BuildHeldModel();
    }

    private void ToggleInventory()
    {
        inventoryOpen = !inventoryOpen;
        PlayerInputService service = PlayerInputService.Instance;
        if (service != null)
            service.SetContext(inventoryOpen ? PlayerInputContext.UI : PlayerInputContext.Gameplay);
        PlayerCursor.LockState = inventoryOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = inventoryOpen;
        if (!inventoryOpen && !CarryingLevel04TaskItem) presentedId = null;
    }

    private string CurrentSlotId()
    {
        return selectedSlot >= 0 && selectedSlot < quickSlots.Length
            ? quickSlots[selectedSlot] : null;
    }

    // The original scene-owned quest object is the world representation. Its
    // quest receipt is separate and remains completed during drop and pickup.
    private C3CollectiblePart FindPart(string id)
    {
        if (!activeCamera || activeCamera.gameObject.scene.name != "C_Main") return null;
        foreach (GameObject root in activeCamera.gameObject.scene.GetRootGameObjects())
            foreach (C3CollectiblePart part in root.GetComponentsInChildren<C3CollectiblePart>(true))
                if (part.PartId == id) return part;
        return null;
    }

    private bool TryDropCurrent()
    {
        string id = CurrentSlotId();
        if (string.IsNullOrEmpty(id) || !Has(id) || !activeCamera) return false;
        if (id == WeaponId) return TryDropQuantumWeapon();
        if (id == SDGAdventure.Level04.Level04PlasmaPickup.WeaponId) return SDGAdventure.Level04.Level04PlasmaPickup.TryDrop(this, activeCamera);
        if (IsWorkshopMaterial(id)) return TryDropWorkshopMaterial(id);
        C3CollectiblePart part = FindPart(id);
        CharacterController player = activeCamera.GetComponentInParent<CharacterController>();
        if (!part || !part.CanDropFromInventory || !player)
        {
            Notify("此物品目前無法安全丟棄");
            return false;
        }
        Vector3 forward = Vector3.ProjectOnPlane(activeCamera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .01f) forward = player.transform.forward;
        Vector3 origin = player.transform.TransformPoint(player.center);
        float distance = player.radius + .8f;
        const float clearance = .52f;
        // Ignore only the player's own colliders; walls and floors block placement.
        foreach (RaycastHit hit in Physics.SphereCastAll(origin, clearance, forward, distance, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider && !hit.collider.transform.IsChildOf(player.transform))
            { Notify("前方空間不足，無法丟棄"); return false; }
        Vector3 position = origin + forward * distance;
        foreach (Collider obstacle in Physics.OverlapSphere(position, clearance, ~0, QueryTriggerInteraction.Ignore))
            if (obstacle && !obstacle.transform.IsChildOf(player.transform))
            { Notify("前方空間不足，無法丟棄"); return false; }
        if (!part.PrepareDrop(position, forward * 1.2f + Vector3.up * .35f, player)) return false;
        // No asynchronous work intervenes between the checked inventory and debit.
        if (!Consume(id, 1)) { part.RollbackDrop(); return false; }
        presentedId = null;
        BuildHeldModel();
        return true;
    }

    private static bool IsWorkshopMaterial(string id) => id == PlasticId || id == MetalId || id == CircuitId;
    public bool WasWorkshopMaterialCollected(string id) => !string.IsNullOrEmpty(id) && collectedWorkshopMaterials.Contains(id);
    private static string MaterialName(string id) => id == PlasticId ? "再生塑膠" : id == MetalId ? "再生金屬" : "廢棄電路板";

    private bool TryDropWorkshopMaterial(string id)
    {
        if (!activeCamera || activeCamera.gameObject.scene.name != "C_Main" || !Has(id)) return false;
        Transform source = FindMaterialVisual(id);
        CharacterController player = activeCamera.GetComponentInParent<CharacterController>();
        if (!source || !player) { Notify("找不到材料原模型，無法安全丟棄"); return false; }
        Vector3 forward = Vector3.ProjectOnPlane(activeCamera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .01f) forward = player.transform.forward;
        Vector3 origin = player.transform.TransformPoint(player.center);
        float distance = player.radius + .8f;
        const float clearance = .52f;
        foreach (RaycastHit hit in Physics.SphereCastAll(origin, clearance, forward, distance, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider && !hit.collider.transform.IsChildOf(player.transform))
            { Notify("前方空間不足，無法丟棄"); return false; }
        Vector3 position = origin + forward * distance;
        foreach (Collider obstacle in Physics.OverlapSphere(position, clearance, ~0, QueryTriggerInteraction.Ignore))
            if (obstacle && !obstacle.transform.IsChildOf(player.transform))
            { Notify("前方空間不足，無法丟棄"); return false; }

        InventoryItem before = inventory.Find(x => x.id == id);
        int quantityBefore = before.quantity;
        GameObject world = null;
        EcoMaterialWorldPickup pickup = null;
        try
        {
            world = new GameObject("DroppedWorkshopMaterial_" + id);
            world.SetActive(false);
            SceneManager.MoveGameObjectToScene(world, activeCamera.gameObject.scene);
            pickup = world.AddComponent<EcoMaterialWorldPickup>();
            if (!pickup.Prepare(this, id, MaterialName(id), source, position, Quaternion.LookRotation(forward)))
            { Destroy(world); return false; }
            world.SetActive(true);
            pickup.Launch(forward * 1.0f + Vector3.up * .25f, player);
            if (!Consume(id, 1)) { world.SetActive(false); Destroy(world); return false; }
            droppedMaterials.Add(pickup);
            collectedWorkshopMaterials.Add(id);
            pickup.Commit();
            return true;
        }
        catch (Exception exception)
        {
            if (pickup) droppedMaterials.Remove(pickup);
            if (world) { world.SetActive(false); Destroy(world); }
            InventoryItem after = inventory.Find(x => x.id == id);
            int remaining = after != null ? after.quantity : 0;
            if (remaining < quantityBefore) AddItem(id, MaterialName(id), quantityBefore - remaining);
            Debug.LogException(exception);
            Notify("材料掉落失敗");
            return false;
        }
    }

    public bool IsDroppedMaterial(EcoMaterialWorldPickup pickup) => pickup && droppedMaterials.Contains(pickup);
    public bool RecoverDroppedMaterial(EcoMaterialWorldPickup pickup, string id, string name)
    {
        if (!IsWorkshopMaterial(id) || !IsDroppedMaterial(pickup)) return false;
        droppedMaterials.Remove(pickup);
        pickup.gameObject.SetActive(false);
        AddItem(id, name, 1);
        Destroy(pickup.gameObject);
        return true;
    }
    public void DroppedMaterialDestroyed(EcoMaterialWorldPickup pickup, string id, string name)
    {
        if (!droppedMaterials.Remove(pickup) || !IsWorkshopMaterial(id)) return;
        AddItem(id, name, 1);
    }

    private bool TryDropQuantumWeapon()
    {
        if (!activeCamera || (activeCamera.gameObject.scene.name != "C_Main" &&
            activeCamera.gameObject.scene.path != "Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity") || droppedWeapon || !WeaponEquipped()) return false;
        InventoryItem entry = inventory.Find(x => x.id == WeaponId);
        CharacterController player = activeCamera.GetComponentInParent<CharacterController>();
        if (entry == null || entry.quantity != 1 || !player)
        { Notify("武器狀態不明，無法安全丟棄"); return false; }
        Vector3 forward = Vector3.ProjectOnPlane(activeCamera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .01f) forward = player.transform.forward;
        Vector3 origin = player.transform.TransformPoint(player.center);
        float distance = player.radius + 1.0f;
        const float clearance = .65f; // Encloses the scaled, asymmetric rifle collider.
        foreach (RaycastHit hit in Physics.SphereCastAll(origin, clearance, forward, distance, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider && !hit.collider.transform.IsChildOf(player.transform))
            { Notify("前方空間不足，無法丟棄"); return false; }
        Vector3 position = origin + forward * distance;
        foreach (Collider obstacle in Physics.OverlapSphere(position, clearance, ~0, QueryTriggerInteraction.Ignore))
            if (obstacle && !obstacle.transform.IsChildOf(player.transform))
            { Notify("前方空間不足，無法丟棄"); return false; }

        GameObject world = null;
        try
        {
            world = new GameObject("DroppedQuantumProjector");
            world.SetActive(false);
            SceneManager.MoveGameObjectToScene(world, activeCamera.gameObject.scene);
            EcoQuantumWorldPickup pickup = world.AddComponent<EcoQuantumWorldPickup>();
            pickup.Prepare(this, position, Quaternion.LookRotation(forward), player);
            if (activeCamera.gameObject.scene.path == "Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity")
                world.AddComponent<SDGAdventure.Level04.Level04ProximityHint>();
            world.SetActive(true);
            pickup.Launch(forward * 1.0f + Vector3.up * .25f, player);
            droppedWeapon = pickup;
            if (!Consume(WeaponId, 1))
            {
                droppedWeapon = null;
                world.SetActive(false);
                Destroy(world);
                return false;
            }
            pickup.Commit();
            return true;
        }
        catch (Exception ex)
        {
            if (world) { world.SetActive(false); Destroy(world); }
            droppedWeapon = null;
            if (!Has(WeaponId)) AddItem(WeaponId, "循環量子投射器");
            Debug.LogException(ex);
            Notify("武器掉落失敗");
            return false;
        }
    }

    // The persistent runtime remains the only owner of magazine/reserve values;
    // the world representation carries no independent copy of ammunition.
    public bool IsWorldQuantumWeapon(EcoQuantumWorldPickup pickup) => droppedWeapon == pickup && !Has(WeaponId);

    public bool RecoverQuantumWeapon(EcoQuantumWorldPickup pickup)
    {
        if (!pickup || droppedWeapon != pickup || Has(WeaponId)) return false;
        droppedWeapon = null;
        pickup.gameObject.SetActive(false);
        AddItem(WeaponId, "循環量子投射器");
        Destroy(pickup.gameObject);
        return true;
    }

    // A scene unload or accidental external destruction must not delete the
    // only weapon. The same runtime retains both ammunition values.
    public void WorldQuantumWeaponDestroyed(EcoQuantumWorldPickup pickup)
    {
        if (droppedWeapon != pickup) return;
        droppedWeapon = null;
        if (!Has(WeaponId)) AddItem(WeaponId, "循環量子投射器");
    }

    private Transform FindMaterialVisual(string id)
    {
        if (!activeCamera || activeCamera.gameObject.scene.name != "C_Main") return null;
        foreach (GameObject root in activeCamera.gameObject.scene.GetRootGameObjects())
            foreach (EcoWorkshopInteractable cache in root.GetComponentsInChildren<EcoWorkshopInteractable>(true))
                if (cache.MaterialItemId == id)
                    return cache.transform.Find("V11_CacheContents_Visual");
        return null;
    }

    // Copy only the existing meshes, transforms and shared materials; no gameplay,
    // physics, hint overlays or source data are copied to the held presentation.
    internal static void CopyPartVisual(Transform source, Transform target)
    {
        foreach (Transform child in source)
        {
            if (child.name.StartsWith("V11_HintOverlay_", StringComparison.Ordinal) || child.name == "NearbyPartGlow_DoNotCollect") continue;
            GameObject copy = new GameObject(child.name);
            copy.transform.SetParent(target, false);
            copy.transform.localPosition = child.localPosition;
            copy.transform.localRotation = child.localRotation;
            copy.transform.localScale = child.localScale;
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            MeshFilter filter = child.GetComponent<MeshFilter>();
            if (renderer && renderer.enabled && filter && filter.sharedMesh)
            {
                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                copy.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            CopyPartVisual(child, copy.transform);
        }
    }

    private bool WeaponEquipped()
    {
        return CurrentSlotId() == WeaponId && Has(WeaponId);
    }

    private EcoWorkshopInteractable NearbyMaterialCache()
    {
        if (activeCamera == null) return null;
        CharacterController character = activeCamera.GetComponentInParent<CharacterController>();
        if (character == null) return null;
        // Use the character position, not the orbiting third-person camera.
        EcoWorkshopInteractable nearest = EcoWorkshopInteractable.FindNearestMaterial(character.transform.position, 2.25f);
        PlayerPerspectiveRig rig = character.GetComponent<PlayerPerspectiveRig>();
        C3PlayerInteractor interactor = character.GetComponent<C3PlayerInteractor>();
        if (nearest != null && rig != null && rig.IsThirdPerson && interactor != null)
        {
            Collider volume = nearest.GetComponent<Collider>();
            if (!volume || !interactor.CanReachPoint(volume, volume.ClosestPoint(interactor.ReachOrigin), 2.25f)) return null;
        }
        return nearest;
    }

    public void InteractWorkshopFromPlayer(C3PlayerInteractor interactor)
    {
        BindCamera();
        if (!GameplayEnabled() || interactor == null || !interactor.HandlesInteractionInput ||
            activeCamera == null || activeCamera.GetComponentInParent<C3PlayerInteractor>() != interactor) return;
        InteractWorkshop();
    }

    private void InteractWorkshop()
    {
        if (activeCamera == null) return;
        Ray ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
        PlayerPerspectiveRig rig = activeCamera.GetComponentInParent<PlayerPerspectiveRig>();
        bool third = rig != null && rig.IsThirdPerson;
        RaycastHit[] hits = Physics.RaycastAll(ray, third ? 8f : 3.5f,
            ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        CharacterController player = activeCamera.GetComponentInParent<CharacterController>();
        C3PlayerInteractor interactor = player != null ? player.GetComponent<C3PlayerInteractor>() : null;
        foreach (RaycastHit hit in hits)
        {
            if (player != null && hit.collider.transform.IsChildOf(player.transform)) continue;
            if (third && player != null && Vector3.Distance(
                player.transform.position + Vector3.up, hit.point) > 3.5f) break;
            EcoWorkshopInteractable target =
                hit.collider.GetComponentInParent<EcoWorkshopInteractable>();
            if (target != null)
            {
                if (third && interactor != null && !interactor.CanReachPoint(hit.collider, hit.point, 3.5f)) return;
                target.Interact();
                return;
            }
            // Leave standard quest items and the puzzle console to their own
            // interaction system; do not accidentally pick up a nearby material.
            if (hit.collider.GetComponentInParent<C3Interactable>() != null) return;
            break;
        }
        // Third-person actions require the first camera hit AND body reach.
        // Do not fall through a wall/missed aim to an off-ray nearby cache.
        // Keep the accepted first-person proximity fallback below unchanged.
        if (third) return;
        // Nearby materials can be picked up without requiring a tiny exact ray hit.
        // This respects the same material sorting/activation checks as normal F use.
        EcoWorkshopInteractable nearest = NearbyMaterialCache();
        if (nearest != null) nearest.Interact();
    }

    private void LateUpdate()
    {
        if (activeCamera == null) return;
        bool aiming = !reloading && GameplayEnabled() && WeaponEquipped() &&
            (PlayerInputService.Instance == null || !PlayerInputService.Instance.GameplayActionsBlocked) &&
            (PlayerInputService.Instance != null ? PlayerInputService.Instance.SecondaryHeld :
             Mouse.current != null && Mouse.current.rightButton.isPressed);
        activeCamera.fieldOfView = Mathf.Lerp(activeCamera.fieldOfView,
            aiming ? initialFov * 0.74f : initialFov, Time.deltaTime * 11f);

        if (presentedId != CurrentSlotId() && !inventoryOpen)
            BuildHeldModel();
        if (weaponModel != null)
        {
            bool showHeldWeapon = !CarryingLevel04TaskItem;
            if (weaponModel.activeSelf != showHeldWeapon) weaponModel.SetActive(showHeldWeapon);
            if (!showHeldWeapon)
            {
                if (reloading) CancelReload();
                return;
            }
            PlayerPerspectiveRig rig = activeCamera.GetComponentInParent<PlayerPerspectiveRig>();
            bool thirdPerson = rig != null && rig.IsThirdPerson;
            EcoQuantumWeaponView visual = weaponModel.GetComponent<EcoQuantumWeaponView>();
            if (visual != null)
            {
                visual.SetThirdPersonPresentation(rig, thirdPerson, aiming);
                if (thirdPerson) return; // Final visual pose is owned by the view after the camera rig.
            }
            C3WorkerAppearance worker = thirdPerson ? rig.GetComponent<C3WorkerAppearance>() : null;
            Transform parent = thirdPerson && worker && worker.ItemHand ? worker.ItemHand :
                thirdPerson ? rig.transform : activeCamera.transform;
            bool handAttached = thirdPerson && worker && parent == worker.ItemHand;
            if (weaponModel.transform.parent != parent)
            {
                weaponModel.transform.SetParent(parent, false);
                weaponModel.transform.localPosition = handAttached ? Vector3.zero : thirdPerson
                    ? new Vector3(0.23f, 1.03f, 0.23f) : new Vector3(0.32f, -0.29f, 0.55f);
            }
            Vector3 localTarget = handAttached ? Vector3.zero : thirdPerson ? new Vector3(0.23f, 1.03f, 0.23f) :
                aiming ? new Vector3(0f, -0.20f, 0.52f) :
                         new Vector3(0.32f, -0.29f, 0.55f);
            weaponModel.transform.localPosition = Vector3.Lerp(
                weaponModel.transform.localPosition, localTarget, Time.deltaTime * 10f);
            weaponModel.transform.localRotation = thirdPerson
                ? Quaternion.Euler(0f, 0f, -7f) : Quaternion.identity;
        }
    }

    private void Shoot()
    {
        if (!WeaponEquipped() || reloading || Time.time < nextShotTime) return;
        if (ammoInMagazine <= 0)
        {
            PlayerInputService input = PlayerInputService.Instance;
            string reloadKey = input != null ? input.Keybindings.BindingDisplay("Gameplay", "Reload", 0) : "R";
            Notify("能量彈匣已用盡，按 " + reloadKey + " 更換");
            nextShotTime = Time.time + 0.3f;
            return;
        }
        if (activeCamera == null) return;
        ammoInMagazine--;
        totalShots++;
        nextShotTime = Time.time + 0.18f;
        Ray ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
        Vector3 destination = ray.GetPoint(85f);
        bool surfaceHit = false;
        RaycastHit[] hits = Physics.RaycastAll(ray, 85f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        CharacterController player = activeCamera.GetComponentInParent<CharacterController>();
        foreach (RaycastHit hit in hits)
        {
            if (player != null && hit.collider.transform.IsChildOf(player.transform)) continue;
            destination = hit.point;
            surfaceHit = true;
            // Enemy damage uses the aim ray; the player's third-person body is excluded.
            BossHealth level04Boss = hit.collider.GetComponentInParent<BossHealth>();
            if (level04Boss != null && activeCamera.gameObject.scene.path == "Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity")
                level04Boss.TakeDamage(1);
            else hit.collider.SendMessageUpwards("TakeDamage", 15f, SendMessageOptions.DontRequireReceiver);
            break;
        }
        // One presentation path, driven by the unchanged hitscan result above.
        EcoQuantumWeaponView presentation = weaponModel != null
            ? weaponModel.GetComponent<EcoQuantumWeaponView>() : null;
        if (presentation != null) presentation.OnShot(destination, surfaceHit);
    }

    private void ShootPlasma()
    {
        if (activeCamera == null || Time.time < nextPlasmaShotTime) return;
        nextPlasmaShotTime = Time.time + .4f;
        Ray ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
        Vector3 target = ray.GetPoint(100f);
        var hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
        var player = activeCamera.GetComponentInParent<CharacterController>();
        foreach (var hit in hits) { if (player && hit.collider.transform.IsChildOf(player.transform)) continue; target=hit.point; var boss=hit.collider.GetComponentInParent<BossHealth>(); if(boss) boss.TakeDamage(1); break; }
        SDGAdventure.Level04.Level04PlasmaPickup.ShowShot(weaponModel ? weaponModel.transform : null, activeCamera.transform.position + activeCamera.transform.forward * .6f, target);
    }

    private IEnumerator Reload()
    {
        if (!WeaponEquipped() || reloading || ammoInMagazine >= MagazineCapacity || reserveAmmo <= 0) yield break;
        reloading = true;
        EcoQuantumWeaponView visual = weaponModel != null ? weaponModel.GetComponent<EcoQuantumWeaponView>() : null;
        if (visual != null) visual.BeginReload();
        float elapsed = 0f;
        while (elapsed < 1.35f)
        {
            if (!WeaponEquipped() || visual == null) { if (visual != null) visual.EndReload(); reloading = false; reloadRoutine = null; yield break; }
            elapsed += Time.deltaTime;
            visual.SetReloadProgress(Mathf.Clamp01(elapsed / 1.35f));
            yield return null;
        }
        int amount = Mathf.Min(MagazineCapacity - ammoInMagazine, reserveAmmo);
        ammoInMagazine += amount;
        reserveAmmo -= amount;
        visual.EndReload();
        reloading = false;
        reloadRoutine = null;
    }

    private void CancelReload()
    {
        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        reloadRoutine = null;
        reloading = false;
        EcoQuantumWeaponView visual = weaponModel != null ? weaponModel.GetComponent<EcoQuantumWeaponView>() : null;
        if (visual != null) visual.EndReload();
    }

    private void BuildHeldModel()
    {
        presentedId = CurrentSlotId();
        if (weaponModel != null) { weaponModel.SetActive(false); Destroy(weaponModel); }
        weaponModel = null;
        string selected = CurrentSlotId();
        if (activeCamera == null || string.IsNullOrEmpty(selected) || !Has(selected)) return;
        weaponModel = new GameObject(selected == WeaponId ?
            "EcoQuantumProjector_ViewModel" : "HeldInventoryItem");
        weaponModel.transform.SetParent(activeCamera.transform, false);
        weaponModel.transform.localPosition = new Vector3(0.32f, -0.29f, 0.55f);
        if (selected == SDGAdventure.Level04.Level04PlasmaPickup.WeaponId)
        {
            var model = SDGAdventure.Level04.Level04PlasmaPickup.CreateModel(weaponModel.transform);
            if (!model) { Destroy(weaponModel); weaponModel = null; }
            else weaponModel.AddComponent<SDGAdventure.Level04.Level04WeaponPose>().Bind(activeCamera);
            return;
        }
        if (selected != WeaponId)
        {
            C3CollectiblePart part = FindPart(selected);
            Transform source = part ? part.transform : FindMaterialVisual(selected);
            if (!source)
            {
                Destroy(weaponModel);
                weaponModel = null;
                return;
            }
            GameObject visual = new GameObject(part ? "OriginalPartVisual" : "OriginalWorkshopMaterialVisual");
            visual.transform.SetParent(weaponModel.transform, false);
            visual.transform.localScale = part ? part.transform.lossyScale * .5f : Vector3.one * .6f;
            CopyPartVisual(source, visual.transform);
            return;
        }
        EcoQuantumWeaponView appearance = weaponModel.AddComponent<EcoQuantumWeaponView>();
        appearance.AttachTo(activeCamera.transform);
        appearance.SetVisible(true);
        if (activeCamera.gameObject.scene.path == "Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity" &&
            activeCamera.GetComponentInParent<SDGAdventure.CompletePlayer.CompletePlayerWorkerAppearance>())
            weaponModel.AddComponent<SDGAdventure.Level04.Level04WeaponPose>().Bind(activeCamera);
        weaponModel.transform.localPosition = new Vector3(0.32f, -0.29f, 0.55f);
    }

    private void AddGunPart(PrimitiveType type, string name, Vector3 pos,
        Vector3 scale, Color color, Quaternion? rotation = null, bool emissive = false)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(weaponModel.transform, false);
        part.transform.localPosition = pos;
        part.transform.localRotation = rotation ?? Quaternion.identity;
        part.transform.localScale = scale;
        Collider collider = part.GetComponent<Collider>();
        if (collider) Destroy(collider);
        Renderer renderer = part.GetComponent<Renderer>();
        Shader shader = Shader.Find("Standard");
        if (renderer != null && shader != null)
        {
            Material material = new Material(shader);
            material.color = color;
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
            }
            C3OwnedVisualMaterial.Assign(renderer, material);
        }
    }

    public string QuickSlotDisplay(int index)
    {
        if (index < 0 || index >= quickSlots.Length) return "";
        InventoryItem item = inventory.Find(x => x.id == quickSlots[index]);
        string name = item != null ? item.name : "空手";
        if (CarryingLevel04TaskItem && selectedSlot == index)
            return "任務：" + SDGAdventure.Level04.Level04RecycleController.ItemName(SDGAdventure.Level04.Level04RecycleController.Instance.Carried.itemType) + " ×1\n原：" + name;
        string binding = PlayerInputService.Instance != null
            ? PlayerInputService.Instance.Keybindings.BindingDisplay("Gameplay", "Slot" + (index + 1), 0)
            : (index + 1).ToString();
        return (selectedSlot == index && !CarryingLevel04TaskItem ? "▼ " : "") + binding + "  " + name;
    }

    private void OnGUI()
    {
        if (Instance != this || activeCamera == null) return;
        PlayerPauseMenu menu = activeCamera.GetComponentInParent<PlayerPauseMenu>();
        if (menu != null && menu.State != PlayerMenuState.Playing) return;
        Font font = PlayerGuiFont.Load();
        if (GUI.skin != null && font != null) GUI.skin.font = font;
        if (C3CompletionScreen.BlocksGameplayInputThisFrame && !inventoryOpen)
            return;
        int width = Screen.width, height = Screen.height;
        if (!QuickbarSuppressed) for (int i = 0; i < 5; i++)
        {
            float x = width * 0.5f - 250f + i * 100f;
            GUI.Box(new Rect(x, height - 70f, 98f, 54f), QuickSlotDisplay(i));
        }
        if (WeaponEquipped())
        {
            GUI.Box(new Rect(12, height - 80, 200, 50),
                "槍內 " + ammoInMagazine + "/" + MagazineCapacity +
                "  |  備用 " + reserveAmmo);
        }

        if (Time.unscaledTime < messageEnd)
            GUI.Box(new Rect(width * 0.5f - 250, 55, 500, 40), message);

        if (GameplayEnabled() && activeCamera != null)
        {
            // Material names are deliberately hidden at long range. Only a nearby,
            // uncollected cache produces an on-screen prompt. The cache disappears
            // on successful pickup, so this prompt disappears in the same frame.
            EcoWorkshopInteractable nearby = NearbyMaterialCache();
            if (nearby != null)
            {
                GUI.Box(new Rect(width * 0.5f - 230, height - 165, 460, 42),
                    "附近發現：" + nearby.MaterialDisplayName + "｜" + nearby.Prompt);
            }
            else if (Physics.Raycast(activeCamera.transform.position,
                activeCamera.transform.forward, out RaycastHit target, 3.5f,
                ~0, QueryTriggerInteraction.Ignore))
            {
                EcoWorkshopInteractable bench =
                    target.collider.GetComponentInParent<EcoWorkshopInteractable>();
                if (bench != null && bench.isActiveAndEnabled && !bench.IsMaterialCache)
                    GUI.Box(new Rect(width * 0.5f - 190, height - 165, 380, 42),
                        bench.Prompt);
            }
        }
        if (!inventoryOpen) return;

        float px = width * 0.5f - 270, py = Mathf.Max(30, height * 0.5f - 260);
        string carriedTask = CarriedTaskItemText;
        float taskOffset = string.IsNullOrEmpty(carriedTask) ? 0f : 24f;
        GUI.Box(new Rect(px, py, 540, 520 + taskOffset), "背包 INVENTORY");
        GUI.Label(new Rect(px + 24, py + 36, 490, 24),
            "先選擇快捷欄，背包開啟後點選物品裝備。");
        if (taskOffset > 0f) GUI.Label(new Rect(px + 24, py + 62, 490, 24), carriedTask);
        for (int i = 0; i < inventory.Count && i < 12; i++)
        {
            InventoryItem entry = inventory[i];
            if (GUI.Button(new Rect(px + 24, py + 66 + taskOffset + i * 32f, 488, 29),
                entry.name + "  ×" + entry.quantity))
            {
                AssignQuickSlot(selectedSlot, entry.id);
            }
        }
        if (GUI.Button(new Rect(px + 24, py + 452 + taskOffset, 231, 36),
            "清空目前快捷欄（空手）"))
        {
            AssignQuickSlot(selectedSlot, null);
        }
        if (GUI.Button(new Rect(px + 283, py + 452 + taskOffset, 229, 36), "關閉背包"))
            ToggleInventory();
    }
}

// Scene representation only. Inventory and ammunition remain owned by the
// persistent EcoAdventureRuntime, never by this visual/interaction component.
public sealed class EcoQuantumWorldPickup : C3Interactable, IPlayerInteractable
{
    private EcoAdventureRuntime owner;
    private C3VisualBuilder art;
    private Rigidbody body;
    private BoxCollider volume;
    private bool committed;
    public override string DisplayName => "循環量子投射器";
    public string Prompt => "拾取循環量子投射器";

    public void Prepare(EcoAdventureRuntime runtime, Vector3 position, Quaternion rotation, Collider player)
    {
        owner = runtime;
        transform.SetPositionAndRotation(position, rotation);
        art = new C3VisualBuilder();
        // The exact same geometry and palette builder as EcoQuantumWeaponView.
        art.QuantumRifle(transform, .60f);
        volume = gameObject.AddComponent<BoxCollider>();
        volume.center = new Vector3(0f, -.025f, .105f);
        volume.size = new Vector3(.24f, .36f, 1.02f);
        body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = true;
        body.mass = 1.5f;
        body.drag = .4f;
        body.angularDrag = 1.5f;
        gameObject.AddComponent<C3ProximityHint>().Configure(new Color(.2f, .85f, 1f));
    }

    public void Launch(Vector3 velocity, Collider player)
    {
        if (player && volume)
        {
            Physics.IgnoreCollision(volume, player, true);
            StartCoroutine(RestoreCollisionWhenClear(player));
        }
        body.isKinematic = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.velocity = velocity;
    }

    public void Commit() => committed = true;
    public override bool CanInteract(GameObject actor) => isActiveAndEnabled && committed && owner && owner.IsWorldQuantumWeapon(this);
    public override void Interact(GameObject actor)
    {
        if (CanInteract(actor)) owner.RecoverQuantumWeapon(this);
    }

    private IEnumerator RestoreCollisionWhenClear(Collider player)
    {
        do
        {
            yield return new WaitForFixedUpdate();
            if (!volume || !player) yield break;
        }
        while (Physics.ComputePenetration(volume, transform.position, transform.rotation,
            player, player.transform.position, player.transform.rotation, out _, out _));
        Physics.IgnoreCollision(volume, player, false);
    }

    private void OnDestroy()
    {
        if (committed && owner) owner.WorldQuantumWeaponDestroyed(this);
        C3ProximityHint hint = GetComponent<C3ProximityHint>();
        if (hint) hint.ReleaseVisuals();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            if (renderer) renderer.sharedMaterials = Array.Empty<Material>();
        if (art != null) art.Dispose();
    }
}

// One scene object represents exactly one unit. The persistent inventory owner
// tracks committed objects and recovers them if the scene unloads unexpectedly.
public sealed class EcoMaterialWorldPickup : C3Interactable
{
    private EcoAdventureRuntime owner;
    private string itemId, itemName;
    private Rigidbody body;
    private BoxCollider volume;
    private bool committed;
    public override string DisplayName => itemName;

    public bool Prepare(EcoAdventureRuntime runtime, string id, string name, Transform source,
        Vector3 position, Quaternion rotation)
    {
        if (!runtime || !source) return false;
        owner = runtime; itemId = id; itemName = name;
        transform.SetPositionAndRotation(position, rotation);
        var visual = new GameObject("OriginalWorkshopMaterialVisual");
        visual.transform.SetParent(transform, false);
        visual.transform.localScale = Vector3.one * .6f; // same scale and source as the held model
        EcoAdventureRuntime.CopyPartVisual(source, visual.transform);
        Bounds bounds = default(Bounds);
        bool any = false;
        foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.sharedMesh) continue;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = mesh.center + Vector3.Scale(mesh.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                point = transform.InverseTransformPoint(filter.transform.TransformPoint(point));
                if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                else bounds.Encapsulate(point);
            }
        }
        if (!any) return false;
        volume = gameObject.AddComponent<BoxCollider>();
        volume.center = bounds.center;
        volume.size = Vector3.Max(bounds.size + Vector3.one * .02f, Vector3.one * .04f);
        body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true; body.useGravity = true;
        body.mass = .5f; body.drag = .4f; body.angularDrag = 1.5f;
        Color color = id == EcoAdventureRuntime.CircuitId ? new Color(.2f, .8f, 1f) :
            id == EcoAdventureRuntime.MetalId ? new Color(1f, .65f, .2f) : new Color(.2f, 1f, .4f);
        gameObject.AddComponent<C3ProximityHint>().Configure(color);
        return true;
    }

    public void Launch(Vector3 velocity, Collider player)
    {
        if (player && volume)
        {
            Physics.IgnoreCollision(volume, player, true);
            StartCoroutine(RestoreCollisionWhenClear(player));
        }
        body.isKinematic = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.velocity = velocity;
    }
    public void Commit() => committed = true;
    public override bool CanInteract(GameObject actor) => isActiveAndEnabled && committed && owner && owner.IsDroppedMaterial(this);
    public override void Interact(GameObject actor)
    {
        if (CanInteract(actor)) owner.RecoverDroppedMaterial(this, itemId, itemName);
    }
    private IEnumerator RestoreCollisionWhenClear(Collider player)
    {
        do
        {
            yield return new WaitForFixedUpdate();
            if (!volume || !player) yield break;
        }
        while (Physics.ComputePenetration(volume, transform.position, transform.rotation,
            player, player.transform.position, player.transform.rotation, out _, out _));
        Physics.IgnoreCollision(volume, player, false);
    }
    private void OnDestroy()
    {
        if (committed && owner) owner.DroppedMaterialDestroyed(this, itemId, itemName);
        C3ProximityHint hint = GetComponent<C3ProximityHint>();
        if (hint) hint.ReleaseVisuals();
    }
}
