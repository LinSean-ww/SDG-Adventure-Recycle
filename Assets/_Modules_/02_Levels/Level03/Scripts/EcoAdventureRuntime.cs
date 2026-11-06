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
    public bool CanAutoRecharge => IsQuantumWeaponEquipped &&
        !inventoryOpen && !reloading && ammoInMagazine < MagazineCapacity;

    private Camera activeCamera;
    private GameObject weaponModel;
    private string presentedId;
    private bool inventoryOpen;
    private bool reloading;
    private float nextShotTime;
    private float lastShotFeedbackUntil;
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
        if (string.IsNullOrEmpty(id) || quantity <= 0) return;
        InventoryItem entry = inventory.Find(x => x.id == id);
        if (entry == null)
        {
            entry = new InventoryItem { id = id, name = displayName, quantity = 0 };
            inventory.Add(entry);
        }
        entry.quantity += quantity;
        Notify("已取得 " + displayName + " × " + quantity);
    }

    public bool Consume(string id, int count)
    {
        InventoryItem entry = inventory.Find(x => x.id == id);
        if (entry == null || entry.quantity < count) return false;
        entry.quantity -= count;
        if (entry.quantity == 0)
        {
            inventory.Remove(entry);
            for (int i = 0; i < quickSlots.Length; i++)
                if (quickSlots[i] == id) quickSlots[i] = null;
        }
        return true;
    }

    public void EquipFirstAvailable(string id)
    {
        for (int i = 0; i < quickSlots.Length; i++)
            if (string.IsNullOrEmpty(quickSlots[i]))
            {
                quickSlots[i] = id;
                return;
            }
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
        if (Has(id) || weaponCrafted) return false;
        AddItem(id, kind == 0 ? "再生塑膠" : "再生金屬");
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
        if (Has(CircuitId) || weaponCrafted)
        {
            Notify("已取回電路板");
            return false;
        }
        AddItem(CircuitId, "廢棄電路板");
        return true;
    }

    public bool CraftWeapon()
    {
        if (!factoryPowered)
        {
            Notify("需要先啟動控制器");
            return false;
        }
        if (weaponCrafted)
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
        Notify("彩蛋解鎖！按 Tab 查看背包，使用 1～5 裝備");
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
        return !inventoryOpen && Cursor.lockState == CursorLockMode.Locked &&
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

    private void Update()
    {
        BindCamera();
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
            Cursor.lockState != CursorLockMode.Locked &&
            mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (keyboard.tabKey.wasPressedThisFrame &&
            (inventoryOpen || GameplayEnabled()))
            ToggleInventory();

        if (!GameplayEnabled()) return;

        if (keyboard.digit1Key.wasPressedThisFrame) selectedSlot = 0;
        if (keyboard.digit2Key.wasPressedThisFrame) selectedSlot = 1;
        if (keyboard.digit3Key.wasPressedThisFrame) selectedSlot = 2;
        if (keyboard.digit4Key.wasPressedThisFrame) selectedSlot = 3;
        if (keyboard.digit5Key.wasPressedThisFrame) selectedSlot = 4;

        // In C3 the existing action-service interactor dispatches quest OR
        // workshop interaction. Retain raw-F fallback in other scenes or when
        // that interactor is disabled; do not poll the same input twice.
        C3PlayerInteractor c3Interactor = activeCamera != null
            ? activeCamera.GetComponentInParent<C3PlayerInteractor>() : null;
        if (keyboard.fKey.wasPressedThisFrame &&
            (c3Interactor == null || !c3Interactor.HandlesInteractionInput))
            InteractWorkshop();

        if (WeaponEquipped())
        {
            if (FirePressed(mouse)) Shoot();
            if (keyboard.rKey.wasPressedThisFrame && !reloading)
                reloadRoutine = StartCoroutine(Reload());
        }

        if (presentedId != CurrentSlotId())
            BuildHeldModel();
    }

    private void ToggleInventory()
    {
        inventoryOpen = !inventoryOpen;
        PlayerInputService service = PlayerInputService.Instance;
        if (service != null)
            service.SetContext(inventoryOpen ? PlayerInputContext.UI : PlayerInputContext.Gameplay);
        Cursor.lockState = inventoryOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = inventoryOpen;
        if (!inventoryOpen) presentedId = null;
    }

    private string CurrentSlotId()
    {
        return selectedSlot >= 0 && selectedSlot < quickSlots.Length
            ? quickSlots[selectedSlot] : null;
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
        bool aiming = GameplayEnabled() && WeaponEquipped() &&
            Mouse.current != null && Mouse.current.rightButton.isPressed;
        activeCamera.fieldOfView = Mathf.Lerp(activeCamera.fieldOfView,
            aiming ? initialFov * 0.74f : initialFov, Time.deltaTime * 11f);

        if (presentedId != CurrentSlotId() && !inventoryOpen)
            BuildHeldModel();
        if (weaponModel != null)
        {
            PlayerPerspectiveRig rig = activeCamera.GetComponentInParent<PlayerPerspectiveRig>();
            bool thirdPerson = rig != null && rig.IsThirdPerson;
            EcoQuantumWeaponView visual = weaponModel.GetComponent<EcoQuantumWeaponView>();
            if (visual != null)
            {
                visual.SetThirdPersonPresentation(rig, thirdPerson, aiming);
                if (thirdPerson) return; // Final visual pose is owned by the view after the camera rig.
            }
            Transform parent = thirdPerson ? rig.transform : activeCamera.transform;
            if (weaponModel.transform.parent != parent)
            {
                weaponModel.transform.SetParent(parent, false);
                weaponModel.transform.localPosition = thirdPerson
                    ? new Vector3(0.23f, 1.03f, 0.23f) : new Vector3(0.32f, -0.29f, 0.55f);
            }
            Vector3 localTarget = thirdPerson ? new Vector3(0.23f, 1.03f, 0.23f) :
                aiming ? new Vector3(0f, -0.19f, 0.52f) :
                         new Vector3(0.32f, -0.29f, 0.55f);
            weaponModel.transform.localPosition = Vector3.Lerp(
                weaponModel.transform.localPosition, localTarget, Time.deltaTime * 10f);
            weaponModel.transform.localRotation = thirdPerson
                ? Quaternion.Euler(0f, 0f, -7f) : Quaternion.identity;
        }
    }

    private void Shoot()
    {
        if (reloading || Time.time < nextShotTime) return;
        if (ammoInMagazine <= 0)
        {
            Notify("能量彈匣已用盡，按 R 更換");
            nextShotTime = Time.time + 0.3f;
            return;
        }
        if (activeCamera == null) return;
        ammoInMagazine--;
        totalShots++;
        lastShotFeedbackUntil = Time.unscaledTime + 0.22f;
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
            hit.collider.SendMessageUpwards("TakeDamage", 15f,
                SendMessageOptions.DontRequireReceiver);
            break;
        }
        // One presentation path, driven by the unchanged hitscan result above.
        EcoQuantumWeaponView presentation = weaponModel != null
            ? weaponModel.GetComponent<EcoQuantumWeaponView>() : null;
        if (presentation != null) presentation.OnShot(destination, surfaceHit);
    }

    private IEnumerator Reload()
    {
        if (ammoInMagazine >= MagazineCapacity || reserveAmmo <= 0) yield break;
        reloading = true;
        Notify("正在更換能源匣");
        yield return new WaitForSeconds(1.35f);
        int amount = Mathf.Min(MagazineCapacity - ammoInMagazine, reserveAmmo);
        ammoInMagazine += amount;
        reserveAmmo -= amount;
        reloading = false;
        reloadRoutine = null;
    }

    private void BuildHeldModel()
    {
        presentedId = CurrentSlotId();
        if (weaponModel != null) Destroy(weaponModel);
        weaponModel = null;
        string selected = CurrentSlotId();
        if (activeCamera == null || string.IsNullOrEmpty(selected) || !Has(selected)) return;
        weaponModel = new GameObject(selected == WeaponId ?
            "EcoQuantumProjector_ViewModel" : "HeldInventoryItem");
        weaponModel.transform.SetParent(activeCamera.transform, false);
        weaponModel.transform.localPosition = new Vector3(0.32f, -0.29f, 0.55f);
        if (selected != WeaponId)
        {
            bool fuse = selected == C3TreasureProgress.FuseId;
            bool chip = selected == C3TreasureProgress.ChipId;
            bool filter = selected == C3TreasureProgress.FilterId;
            Color color = fuse ? new Color(1f, 0.58f, 0.11f) :
                chip ? new Color(0.15f, 0.8f, 1f) :
                filter ? new Color(0.23f, 0.83f, 0.40f) :
                new Color(0.5f, 0.72f, 0.65f);
            AddGunPart(fuse || filter ? PrimitiveType.Cylinder : PrimitiveType.Cube,
                "Held_" + selected, Vector3.zero,
                new Vector3(0.13f, 0.16f, 0.13f), color);
            return;
        }
        EcoQuantumWeaponView appearance = weaponModel.AddComponent<EcoQuantumWeaponView>();
        appearance.AttachTo(activeCamera.transform);
        appearance.SetVisible(true);
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

    private void OnGUI()
    {
        if (Instance != this || activeCamera == null) return;
        Font font = PlayerGuiFont.Load();
        if (GUI.skin != null && font != null) GUI.skin.font = font;
        if (C3CompletionScreen.BlocksGameplayInputThisFrame && !inventoryOpen)
            return;
        int width = Screen.width, height = Screen.height;
        for (int i = 0; i < 5; i++)
        {
            string id = quickSlots[i];
            InventoryItem item = inventory.Find(x => x.id == id);
            string name = item != null ? item.name : "空手";
            float x = width * 0.5f - 250f + i * 100f;
            GUI.Box(new Rect(x, height - 70f, 98f, 54f),
                (selectedSlot == i ? "▼ " : "") + (i + 1) + "  " + name);
        }
        if (WeaponEquipped())
        {
            GUI.Box(new Rect(12, height - 80, 200, 50),
                "槍內 " + ammoInMagazine + "/" + MagazineCapacity +
                "  |  備用 " + reserveAmmo);
            if (Time.unscaledTime < lastShotFeedbackUntil)
                GUI.Box(new Rect(width * 0.5f - 60f, height * 0.5f + 44f,
                    120f, 28f), "發射 #" + totalShots);
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
        GUI.Box(new Rect(px, py, 540, 520), "背包 INVENTORY");
        GUI.Label(new Rect(px + 24, py + 36, 490, 24),
            "先按 1～5 選擇快捷欄，背包開啟後點選物品裝備。");
        for (int i = 0; i < inventory.Count && i < 12; i++)
        {
            InventoryItem entry = inventory[i];
            if (GUI.Button(new Rect(px + 24, py + 66 + i * 32f, 488, 29),
                entry.name + "  ×" + entry.quantity))
            {
                quickSlots[selectedSlot] = entry.id;
                presentedId = null;
            }
        }
        if (GUI.Button(new Rect(px + 24, py + 452, 231, 36),
            "清空目前快捷欄（空手）"))
        {
            quickSlots[selectedSlot] = null;
            presentedId = null;
        }
        if (GUI.Button(new Rect(px + 283, py + 452, 229, 36), "關閉背包 [Tab]"))
            ToggleInventory();
    }
}
