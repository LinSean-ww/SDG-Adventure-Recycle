using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using SDGAdventure.ModuleSystem;
using SDGAdventure.Level04;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SDGAdventure.FrontEnd
{
    // Independent v2 save format: does not overwrite the earlier SDG_Adventure_LastSave_v1.json.
    [Serializable]
    public sealed class SDGProgressDoorV1
    {
        public string hierarchyPath;
        public bool isOpen;
    }

    // Optional v2 extension. Legacy save slots without this block remain readable,
    // but cannot restore a Boss defeat that was never recorded.
    [Serializable]
    public sealed class SDGProgressLevel04ItemV1
    {
        public int itemType;
        public bool activeSelf;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
    }

    [Serializable]
    public sealed class SDGProgressLevel04BossBattleV2
    {
        public int schema = 2;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public int phase;
        public int animatorState;
        public float animatorTime;
        public float attackCooldownRemaining;
        public float attackElapsed;
        public bool attackStateStarted;
        public bool hitAttempted;
    }

    [Serializable]
    public sealed class SDGProgressInventoryItemV2
    {
        public string id;
        public string name;
        public int quantity;
    }

    [Serializable]
    public sealed class SDGProgressLevel04InventoryV2
    {
        public int schema = 2;
        public List<SDGProgressInventoryItemV2> items = new List<SDGProgressInventoryItemV2>();
        public string[] quickSlots = new string[5];
        public int selectedSlot;
        public int ammoInMagazine;
        public int reserveAmmo;
        public bool factoryPowered;
        public bool weaponCrafted;
    }

    [Serializable]
    public sealed class SDGProgressLevel04WorldV1
    {
        public int schema = 1;
        public int bossHealth;
        public int bossMaxHealth;
        public bool bossDead;
        public bool recyclePartsVisible;
        public int completedQuestions;
        public int successfullyRecycledMask;
        public string feedback;
        public int carriedItemType = -1;
        public List<SDGProgressLevel04ItemV1> items = new List<SDGProgressLevel04ItemV1>();
        // R4: exact battle-state snapshot, player viewpoint and weapon/inventory.
        public SDGProgressLevel04BossBattleV2 battle;
        public SDGProgressLevel04InventoryV2 inventory;
        public float viewYaw;
        public float viewPitch;
        public bool thirdPerson;
        public bool worldPlasmaCollected;
        public bool droppedPlasma;
        public Vector3 droppedPlasmaPosition;
        public Quaternion droppedPlasmaRotation = Quaternion.identity;
    }

    [Serializable]
    public sealed class SDGProgressRecordV1
    {
        public int version = 2;
        public int slot;
        public string ownerId;
        public string scenePath;
        public int levelNumber;
        public string playerName;
        public string avatarKey;
        public string savedUtc;
        public int totalPlaySeconds;
        public int completedMask;
        public int experience;
        public int experienceMax = 100;
        public int health = 100;
        public int maxHealth = 100;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public bool hasPlayerPosition;
        public List<SDGProgressDoorV1> doors = new List<SDGProgressDoorV1>();
        public SDGProgressLevel04WorldV1 level04World;
        // All-level v5 world snapshot; old slots remain readable but cannot claim full restore.
        public SDGSceneWorldV5 sceneStateV5;
    }

    /// <summary>
    /// Level and checkpoint persistence. F5 manual save, F9 load selected existing slot.
    /// One auto slot (0) and three manual slots (1-3). Four-level progress is based
    /// on actual ModuleCoordinator.Completed events, not on level visits.
    /// Inventory, per-mission object flags and puzzle states need dedicated adapters;
    /// they are deliberately NOT guessed or pretended to be fully serialized.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SDGSaveProgressRuntimeV1 : MonoBehaviour
    {
        public const string AutoPref = "SDG_SP_AutoEnabled";
        public const string SlotPref = "SDG_SP_SelectedSlot";
        public const string SecondsPref = "SDG_SP_AccumulatedSeconds";
        public const string CompletedPref = "SDG_SP_CompletedLevelMask";
        private const string FilePrefix = "SDG_Progress_v2_";
        private const float AutoInterval = 150f;
        private static SDGSaveProgressRuntimeV1 instance;
        private static SDGProgressRecordV1 pendingRestore;
        private static bool restoreHealthPending;
        public static bool IsRestoringHealth { get { return restoreHealthPending; } }
        public static bool IsRestoringWorld { get { return restoreHealthPending; } }
        private ModuleCoordinator subscribedCoordinator;
        private float playSeconds;
        private float nextAutoSave;
        private float nextPrefsSave;
        private float nextCoordinatorSearch;
        private Coroutine restoreRoutine;
        private int completedMask;

        public static bool AutoSaveEnabled
        {
            get { return PlayerPrefs.GetInt(AutoPref, 1) != 0; }
            set { PlayerPrefs.SetInt(AutoPref, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int SelectedSlot
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(SlotPref, -1), -1, 12); }
            set { PlayerPrefs.SetInt(SlotPref, Mathf.Clamp(value, 0, 12)); PlayerPrefs.Save(); }
        }

        public static int CompletedCount
        {
            get
            {
                int mask = instance != null ? instance.completedMask : PlayerPrefs.GetInt(CompletedPref, 0);
                int count = 0;
                for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) count++;
                return count;
            }
        }

        public static int CurrentPlaySeconds
        {
            get { return Mathf.RoundToInt(instance != null ? instance.playSeconds : PlayerPrefs.GetInt(SecondsPref, 0)); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            pendingRestore = null;
            restoreHealthPending = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;
            var host = new GameObject("SDG Save Progress v2 Service");
            DontDestroyOnLoad(host);
            host.AddComponent<SDGSaveProgressRuntimeV1>();
            host.AddComponent<SDGSavePauseButtonsV1>();
            host.AddComponent<SDGSaveSlotsOverlayV5>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            SDGSaveProfilesV2.Ensure();
            playSeconds = Mathf.Max(0, SDGSaveProfilesV2.Active.totalPlaySeconds);
            completedMask = SDGSaveProfilesV2.Active.completedMask & 15;
            nextAutoSave = Time.unscaledTime + 15f;
            nextPrefsSave = Time.unscaledTime + 10f;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= SceneLoaded;
            if (subscribedCoordinator != null)
                subscribedCoordinator.Completed -= OnModuleCompleted;
            instance = null;
        }

        private void Update()
        {
            Scene scene = SceneManager.GetActiveScene();
            bool inLevel = LevelOf(scene.path) != 0;
            if (Time.unscaledTime >= nextCoordinatorSearch)
            {
                nextCoordinatorSearch = Time.unscaledTime + 1f;
                ConnectCompletionEvent();
            }
            if (inLevel && Time.timeScale > 0f)
                playSeconds += Time.unscaledDeltaTime;

            // Never auto-overwrite manual slot 1 just because F5 was pressed.
            if (inLevel && !SDGSaveSlotsOverlayV5.IsOpen && ManualSavePressed())
                SDGSaveSlotsOverlayV5.OpenSave();
            if (inLevel && !SDGSaveSlotsOverlayV5.IsOpen && ManualLoadPressed())
                SDGSaveSlotsOverlayV5.OpenLoad();
            if (inLevel && !SDGSaveSlotsOverlayV5.IsOpen && AutoSaveEnabled && Time.timeScale > 0f && Time.unscaledTime >= nextAutoSave)
            {
                nextAutoSave = Time.unscaledTime + AutoInterval;
                if (TrySave(0, out string message)) Debug.Log("[SDG Progress] " + message);
            }
            if (Time.unscaledTime >= nextPrefsSave)
            {
                nextPrefsSave = Time.unscaledTime + 10f;
                PersistSummary();
            }
        }

        private static bool ManualSavePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F5)) return true;
#endif
            return false;
        }

        private static bool ManualLoadPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F9)) return true;
#endif
            return false;
        }

        private void ConnectCompletionEvent()
        {
            var coordinator = ModuleCoordinator.Instance;
            if (coordinator == subscribedCoordinator) return;
            if (subscribedCoordinator != null)
                subscribedCoordinator.Completed -= OnModuleCompleted;
            subscribedCoordinator = coordinator;
            if (subscribedCoordinator != null)
                subscribedCoordinator.Completed += OnModuleCompleted;
        }

        private void OnModuleCompleted(ModuleResult result)
        {
            if (!result.Completed) return;
            int number = LevelFromModule(result.ModuleId);
            if (number == 0) number = LevelOf(SceneManager.GetActiveScene().path);
            if (number == 0) return;
            completedMask |= 1 << (number - 1);
            PersistSummary();
            if (AutoSaveEnabled) TrySave(0, out _);
            Debug.Log("[SDG Progress] Level0" + number + " completed; " + (CompletedCount * 25) + "% overall.");
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            // Register authored pickups before gameplay can destroy/consume them.
            SDGSceneWorldSaveV5.NoteSceneLoaded(scene);
            nextAutoSave = Time.unscaledTime + 15f;
            if (restoreRoutine != null) StopCoroutine(restoreRoutine);
            if (pendingRestore != null)
            {
                var snapshot = pendingRestore;
                pendingRestore = null;
                if (snapshot.scenePath == scene.path)
                    restoreRoutine = StartCoroutine(RestoreAfterSpawn(scene.path, snapshot));
                else
                {
                    restoreHealthPending = false;
                    Time.timeScale = 1f;
                    Debug.LogError("[SDG FullScene R4] RESTORE ABORTED: loaded different scene");
                }
            }
        }

        private IEnumerator RestoreAfterSpawn(string scenePath, SDGProgressRecordV1 record)
        {
            for (int i = 0; i < 180; i++)
            {
                Component player = FindLocalPlayer(scenePath);
                if (player != null)
                {
                    yield return null; // Scene Awake/Start and spawn-point initialization must finish.
                    if (!TryTeleport(player, record.position, record.rotation))
                        Debug.LogWarning("[SDG Progress] Could not restore player transform.");
                    if (!TryRestorePlayerHealth(player, record.health))
                    {
                        // Levels without an actual HP component have only a UI health mirror.
                        SDGFrontEndPlayerVitals.ReportHealth(record.health, record.maxHealth);
                        Debug.LogWarning("[SDG Progress R2] No restorable HP component on the player in Level0"
                            + record.levelNumber + "; showing saved HP in UI only.");
                    }
                    RestoreDoors(record, scenePath);
                    if (record.levelNumber <= 3)
                    {
                        try
                        {
                            if (!SDGSceneWorldSaveV5.Valid(record.sceneStateV5, record.levelNumber))
                                throw new InvalidDataException("舊存檔沒有本關完整世界快照。");
                            RestoreLevel04Inventory(record.sceneStateV5.inventory);
                            SDGSceneWorldSaveV5.Restore(SceneManager.GetActiveScene(), record.sceneStateV5);
                        }
                        catch (Exception ex)
                        {
                            restoreHealthPending = false;
                            Time.timeScale = 0f;
                            Debug.LogError("[SDG Save V5] WORLD RESTORE FAILED; gameplay remains paused: " + ex);
                            restoreRoutine = null;
                            yield break;
                        }
                    }
                    if (record.levelNumber == 4 && record.level04World != null)
                    {
                        // ModuleCoordinator/Level04ModuleAdapter configure the recycle
                        // system after scene load. Never restore Boss during its Start().
                        bool worldReady = false;
                        for (int wait = 0; wait < 300; wait++)
                        {
                            var controller = Level04RecycleController.Instance;
                            if (controller != null && controller.gameObject.scene.path == scenePath &&
                                FindSceneComponent<BossHealth>(scenePath) != null &&
                                FindSceneComponent<RecycleHouse>(scenePath) != null)
                            { worldReady = true; break; }
                            yield return null;
                        }
                        if (!worldReady)
                        {
                            restoreHealthPending = false;
                            Time.timeScale = 0f;
                            Debug.LogError("[SDG Progress R3] WORLD RESTORE FAILED: Level04 module not initialized; saved slot remains unchanged.");
                            restoreRoutine = null;
                            yield break;
                        }
                        try
                        {
                            RestoreLevel04Inventory(record.level04World.inventory);
                            RestoreLevel04World(scenePath, record.level04World);
                            RestoreLevel04View(player, record.level04World, record.rotation);
                        }
                        catch (Exception error)
                        {
                            restoreHealthPending = false;
                            Time.timeScale = 0f; // Stay paused on failed reconstruction; do not resume a corrupted encounter.
                            Debug.LogError("[SDG FullScene R4] WORLD RESTORE FAILED; gameplay left paused: " + error);
                            restoreRoutine = null;
                            yield break;
                        }
                        Debug.Log("[SDG Progress R3] WORLD RESTORED slot=" + record.slot
                            + " BossDead=" + record.level04World.bossDead
                            + " BossHP=" + record.level04World.bossHealth
                            + " Recycle=" + record.level04World.completedQuestions + "/5"
                            + " Mask=" + record.level04World.successfullyRecycledMask);
                    }
                    else if (record.levelNumber == 4)
                    {
                        Debug.LogWarning("[SDG Progress R3] LEGACY LEVEL04 SLOT: saved HP and position restored, but this old slot has NO Boss/recycle world state. Re-save with R3 to include it.");
                    }
                    restoreHealthPending = false;
                    // Completed or failed level may show a paused UI instead of gameplay.
                    bool needsPausedState = record.levelNumber == 4 &&
                        (record.health <= 0 || (record.level04World != null && record.level04World.completedQuestions >= 5));
                    Time.timeScale = needsPausedState ? 0f : 1f;
                    var service = PlayerInputService.Instance;
                    if (!needsPausedState)
                    {
                        if (service != null) service.SetContext(PlayerInputContext.Gameplay);
                        PlayerCursor.LockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                    Debug.Log("[SDG FullScene R4] RESTORED slot=" + record.slot
                        + " level=" + record.levelNumber + " HP=" + record.health + "/" + record.maxHealth
                        + " position=" + record.position + " scene=" + record.scenePath);
                    restoreRoutine = null;
                    yield break;
                }
                yield return null;
            }
            restoreHealthPending = false;
            Time.timeScale = 0f; // Refuse to resume a scene with no controlled player.
            Debug.LogWarning("[SDG Progress] Loaded level but player controller was not found. " +
                             "Inventory/mission flags are not supported by this patch.");
            restoreRoutine = null;
        }

        // Called only from main menu, before switching save-owner identity.
        public static bool SwitchOwner(string ownerId)
        {
            if (instance == null) return false;
            instance.PersistSummary();
            if (!SDGSaveProfilesV2.SetActive(ownerId)) return false;
            instance.playSeconds = Mathf.Max(0, SDGSaveProfilesV2.Active.totalPlaySeconds);
            instance.completedMask = SDGSaveProfilesV2.Active.completedMask & 15;
            SelectedSlot = 1;
            return true;
        }

        public static bool TrySave(int slot, out string message)
        {
            message = "";
            if (slot < 0 || slot > 12) { message = "不支援的存檔欄位"; return false; }
            if (instance == null) { message = "存檔服務尚未啟動"; return false; }
            if (restoreHealthPending) { message = "正在還原存檔，禁止覆寫欄位。"; return false; }
            Scene scene = SceneManager.GetActiveScene();
            int level = LevelOf(scene.path);
            if (level == 0 || !Application.CanStreamedLevelBeLoaded(scene.path))
            {
                message = "請在已加入 Build Settings 的正式關卡中儲存。";
                return false;
            }
            Component player = FindLocalPlayer(scene.path);
            if (player == null)
            {
                message = "找不到本關玩家控制器，拒絕建立無法恢復位置的存檔。";
                return false;
            }
            try
            {
                int liveHealth = Mathf.Clamp(PlayerPrefs.GetInt(SDGFrontEndPlayerVitals.HealthKey, 100), 0,
                    Mathf.Max(1, PlayerPrefs.GetInt(SDGFrontEndPlayerVitals.MaxHealthKey, 100)));
                int liveMaxHealth = Mathf.Max(1, PlayerPrefs.GetInt(SDGFrontEndPlayerVitals.MaxHealthKey, 100));
                TryCapturePlayerHealth(player, ref liveHealth, ref liveMaxHealth);
                SDGProgressRecordV1 record = new SDGProgressRecordV1
                {
                    slot = slot,
                    ownerId = SDGSaveProfilesV2.ActiveId,
                    scenePath = scene.path,
                    levelNumber = level,
                    playerName = PlayerPrefs.GetString("SDG_FE_PlayerName", "回收探險者"),
                    avatarKey = PlayerPrefs.GetString("SDG_FE_SelectedAvatar", ""),
                    savedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    totalPlaySeconds = CurrentPlaySeconds,
                    completedMask = instance.completedMask,
                    experience = PlayerPrefs.GetInt("SDG_FE_Experience", 0),
                    experienceMax = Mathf.Max(1, PlayerPrefs.GetInt("SDG_FE_ExperienceMax", 100)),
                    health = liveHealth,
                    maxHealth = liveMaxHealth,
                    position = player.transform.position,
                    rotation = player.transform.rotation,
                    hasPlayerPosition = true,
                    doors = CaptureDoors(scene.path),
                    level04World = level == 4 ? CaptureLevel04World(scene.path, player) : null,
                    sceneStateV5 = level <= 3 ? SDGSceneWorldSaveV5.Capture(scene, level, CaptureLevel04Inventory()) : null
                };
                string savedPath = SlotPath(slot);
                WriteAtomic(savedPath, JsonUtility.ToJson(record, true));
                if (!TryReadSlot(slot, out SDGProgressRecordV1 readBack) ||
                    readBack.health != record.health || readBack.maxHealth != record.maxHealth ||
                    readBack.position != record.position ||
                    (level <= 3 && (readBack.sceneStateV5 == null ||
                        !SDGSceneWorldSaveV5.Valid(readBack.sceneStateV5, level) ||
                        readBack.sceneStateV5.components.Count != record.sceneStateV5.components.Count ||
                        readBack.sceneStateV5.objects.Count != record.sceneStateV5.objects.Count ||
                        (level == 3 && (readBack.sceneStateV5.level03Production == null ||
                            readBack.sceneStateV5.level03Ledger == null ||
                            readBack.sceneStateV5.level03Production.materials.Count != record.sceneStateV5.level03Production.materials.Count ||
                            readBack.sceneStateV5.level03Production.batches.Count != record.sceneStateV5.level03Production.batches.Count ||
                            readBack.sceneStateV5.level03Ledger.materialIds.Count != record.sceneStateV5.level03Ledger.materialIds.Count)))) ||
                    (level == 4 && (readBack.level04World == null ||
                        readBack.level04World.bossHealth != record.level04World.bossHealth ||
                        readBack.level04World.bossDead != record.level04World.bossDead ||
                        readBack.level04World.completedQuestions != record.level04World.completedQuestions ||
                        readBack.level04World.successfullyRecycledMask != record.level04World.successfullyRecycledMask ||
                        readBack.level04World.battle == null ||
                        readBack.level04World.inventory == null ||
                        readBack.level04World.battle.phase != record.level04World.battle.phase ||
                        readBack.level04World.battle.position != record.level04World.battle.position ||
                        readBack.level04World.inventory.items.Count != record.level04World.inventory.items.Count)))
                    throw new InvalidDataException("存檔寫入後回讀驗證不一致，請保留診斷資料。");
                Debug.Log("[SDG Progress R3] SAVED slot=" + slot + " level=" + level
                    + " HP=" + record.health + "/" + record.maxHealth
                    + (record.level04World != null ? (" BossDead=" + record.level04World.bossDead
                        + " BossHP=" + record.level04World.bossHealth
                        + " Recycle=" + record.level04World.completedQuestions + "/5") : "")
                    + " position=" + record.position + " path=" + savedPath);
                // Update the profile and menu from the exact HP committed to the save slot.
                SDGFrontEndPlayerVitals.ReportHealth(record.health, record.maxHealth);
                instance.PersistSummary();
                UpdateFrontEndBridge(record);
                message = (slot == 0 ? "自動存檔" : "手動存檔 " + slot) + " 已完成（第 " + level + " 關）。";
                return true;
            }
            catch (Exception ex)
            {
                message = "儲存失敗：" + ex.Message;
                Debug.LogError("[SDG Progress] " + ex);
                return false;
            }
        }

        public static bool TryReadSlot(int slot, out SDGProgressRecordV1 record)
        {
            record = null;
            if (slot < 0 || slot > 12) return false;
            string path = SlotPath(slot);
            if (!File.Exists(path)) return false;
            try
            {
                var info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > 4 * 1024 * 1024) return false;
                record = JsonUtility.FromJson<SDGProgressRecordV1>(File.ReadAllText(path));
                return record != null && record.version == 2 && record.slot == slot &&
                       (string.IsNullOrEmpty(record.ownerId) ? SDGSaveProfilesV2.ActiveId == "original" : record.ownerId == SDGSaveProfilesV2.ActiveId) &&
                       record.hasPlayerPosition &&
                       record.levelNumber >= 1 && record.levelNumber <= 4 &&
                       LevelOf(record.scenePath) == record.levelNumber &&
                       !string.IsNullOrEmpty(record.savedUtc) &&
                       (record.levelNumber != 4 || record.level04World == null || IsValidLevel04World(record.level04World));
            }
            catch (Exception) { record = null; return false; }
        }

        public static int NewestSlot()
        {
            int best = -1;
            DateTime bestDate = DateTime.MinValue;
            for (int i = 0; i <= 12; i++)
            {
                if (!TryReadSlot(i, out SDGProgressRecordV1 record)) continue;
                // A legacy HP-only slot must not be advertised as a working Continue checkpoint.
                if (record.levelNumber <= 3 && !SDGSceneWorldSaveV5.Valid(record.sceneStateV5, record.levelNumber)) continue;
                if (record.levelNumber == 4 &&
                    (record.level04World == null || record.level04World.battle == null || record.level04World.inventory == null)) continue;
                if (!DateTime.TryParse(record.savedUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTime parsed)) continue;
                if (best == -1 || parsed.ToUniversalTime() > bestDate)
                {
                    best = i;
                    bestDate = parsed.ToUniversalTime();
                }
            }
            return best;
        }

        public static bool TryLoad(int slot, out string message)
        {
            if (!TryReadSlot(slot, out SDGProgressRecordV1 record))
            {
                message = "這個欄位沒有有效存檔。";
                return false;
            }
            if (record.levelNumber <= 3 &&
                !SDGSceneWorldSaveV5.Valid(record.sceneStateV5, record.levelNumber))
            {
                message = "此舊存檔沒有第 " + record.levelNumber + " 關完整場景狀態，拒絕假恢復。請使用新版重新建立存檔。";
                return false;
            }
            if (record.levelNumber == 4 &&
                (record.level04World == null || record.level04World.battle == null ||
                 record.level04World.inventory == null))
            {
                message = "舊存檔沒有 Boss 戰鬥位置／AI／武器快照，不能完整恢復。請先備份舊欄，再用 R4 建立新存檔。";
                Debug.LogWarning("[SDG FullScene R4] LEGACY SLOT REJECTED: slot=" + slot);
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(record.scenePath))
            {
                message = "存檔關卡未加入 Build Settings：" + record.scenePath;
                return false;
            }
            if (instance == null) { message = "存檔服務尚未啟動。"; return false; }
            try
            {
                // Apply meta only after file+scene validation, before scene activation.
                PlayerPrefs.SetString("SDG_FE_PlayerName", record.playerName ?? "回收探險者");
                if (!string.IsNullOrEmpty(record.avatarKey))
                    PlayerPrefs.SetString("SDG_FE_SelectedAvatar", record.avatarKey);
                PlayerPrefs.SetInt("SDG_FE_Experience", Mathf.Max(0, record.experience));
                PlayerPrefs.SetInt("SDG_FE_ExperienceMax", Mathf.Max(1, record.experienceMax));
                PlayerPrefs.SetInt("SDG_FE_PlayerHealth", Mathf.Clamp(record.health, 0, Mathf.Max(1, record.maxHealth)));
                PlayerPrefs.SetInt("SDG_FE_PlayerHealthMax", Mathf.Max(1, record.maxHealth));
                instance.completedMask = record.completedMask & 15;
                // Loading the slot restores its recorded summary, not the later in-memory value.
                instance.playSeconds = Mathf.Max(0, record.totalPlaySeconds);
                instance.PersistSummary();
                UpdateFrontEndBridge(record);
                pendingRestore = record;
                restoreHealthPending = true;
                Debug.Log("[SDG Progress R2] LOADING slot=" + slot + " level=" + record.levelNumber
                    + " HP=" + record.health + "/" + record.maxHealth
                    + " position=" + record.position + " scene=" + record.scenePath);
                Time.timeScale = 0f; // Awake/Start still run; gameplay timers and physics are suspended.
                SceneManager.LoadScene(record.scenePath, LoadSceneMode.Single);
                message = "正在讀取第 " + record.levelNumber + " 關。";
                return true;
            }
            catch (Exception ex)
            {
                pendingRestore = null;
                restoreHealthPending = false;
                Time.timeScale = 1f;
                message = "讀取失敗：" + ex.Message;
                Debug.LogError("[SDG Progress] " + ex);
                return false;
            }
        }

        public static void SaveFromPauseMenu()
        {
            SDGSaveSlotsOverlayV5.OpenSave();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) FlushAndAutoSave();
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused) FlushAndAutoSave();
        }
        private void OnApplicationQuit() { FlushAndAutoSave(); }

        private void FlushAndAutoSave()
        {
            PersistSummary();
            if (AutoSaveEnabled && LevelOf(SceneManager.GetActiveScene().path) != 0)
                TrySave(0, out _);
        }

        private void PersistSummary()
        {
            PlayerPrefs.SetInt(SecondsPref, CurrentPlaySeconds);
            PlayerPrefs.SetInt(CompletedPref, completedMask & 15);
            PlayerPrefs.Save();
            SDGSaveProfilesV2.CaptureActive(CurrentPlaySeconds, completedMask);
        }

        private static void UpdateFrontEndBridge(SDGProgressRecordV1 record)
        {
            string scene = record.scenePath;
            if (scene.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                scene = scene.Substring(0, scene.Length - 6);
            PlayerPrefs.SetString("SDG_FE_LastSaveScene", scene);
            PlayerPrefs.SetString("SDG_FE_LastSaveTitle", "第 " + record.levelNumber + " 關");
            PlayerPrefs.SetFloat("SDG_FE_LastSaveProgress01", CountBits(record.completedMask) / 4f);
            PlayerPrefs.SetInt("SDG_FE_LastSaveSeconds", record.totalPlaySeconds);
            PlayerPrefs.Save();
        }

        private static int CountBits(int mask)
        {
            int count = 0;
            for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) count++;
            return count;
        }

        private static int LevelOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            path = path.Replace('\\', '/');
            for (int i = 1; i <= 4; i++)
                if (path.IndexOf("/_Modules_/02_Levels/Level0" + i + "/", StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            return 0;
        }

        private static int LevelFromModule(string id)
        {
            switch (id)
            {
                case "level01": return 1;
                case "level02": return 2;
                case "c3_treasure_puzzle": return 3;
                case "level04": return 4;
                default: return 0;
            }
        }

        private static string SlotPath(int slot)
        {
            return SDGSaveProfilesV2.SlotPath(slot);
        }

        private static void WriteAtomic(string target, string body)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string temp = target + ".tmp";
            File.WriteAllText(temp, body, new System.Text.UTF8Encoding(false));
            if (!File.Exists(target)) { File.Move(temp, target); return; }
            string backup = target + ".bak";
            try { File.Replace(temp, target, backup); }
            catch (PlatformNotSupportedException)
            {
                File.Copy(target, backup, true);
                File.Delete(target);
                File.Move(temp, target);
            }
        }

        private static T FindSceneComponent<T>(string scenePath) where T : Component
        {
            foreach (T candidate in FindObjectsOfType<T>(true))
                if (candidate != null && candidate.gameObject.scene.path == scenePath) return candidate;
            return null;
        }

        private static bool IsValidLevel04World(SDGProgressLevel04WorldV1 value)
        {
            if (value == null || value.schema != 1 || value.bossMaxHealth < 1 ||
                value.bossHealth < 0 || value.bossHealth > value.bossMaxHealth ||
                value.bossDead != (value.bossHealth == 0) ||
                value.completedQuestions < 0 || value.completedQuestions > 5 ||
                value.successfullyRecycledMask < 0 || value.successfullyRecycledMask > 31 ||
                value.carriedItemType < -1 || value.carriedItemType > 4 ||
                value.items == null || value.items.Count != 5) return false;
            int found = 0;
            foreach (var item in value.items)
            {
                if (item == null || item.itemType < 0 || item.itemType > 4) return false;
                int bit = 1 << item.itemType;
                if ((found & bit) != 0) return false;
                found |= bit;
            }
            if (found != 31 || (value.carriedItemType >= 0 &&
                (value.successfullyRecycledMask & (1 << value.carriedItemType)) != 0)) return false;
            int successCount = 0;
            for (int i = 0; i < 5; i++) if ((value.successfullyRecycledMask & (1 << i)) != 0) successCount++;
            return successCount <= value.completedQuestions &&
                (!value.bossDead ? (value.completedQuestions == 0 && value.successfullyRecycledMask == 0 && value.carriedItemType == -1) : true);
        }

        private static SDGProgressLevel04WorldV1 CaptureLevel04World(string scenePath, Component player)
        {
            var boss = FindSceneComponent<BossHealth>(scenePath);
            var house = FindSceneComponent<RecycleHouse>(scenePath);
            var recycler = Level04RecycleController.Instance;
            if (!boss || !house || recycler == null || recycler.gameObject.scene.path != scenePath)
                throw new InvalidOperationException("Level04 state not initialized. Refusing a partial world save.");
            var saved = new SDGProgressLevel04WorldV1
            {
                bossHealth = boss.CurrentHealth,
                bossMaxHealth = boss.maxHealth,
                bossDead = boss.IsDead,
                recyclePartsVisible = boss.recycleParts && boss.recycleParts.activeSelf,
                completedQuestions = house.CompletedQuestions,
                successfullyRecycledMask = house.CorrectlyRecycledMask,
                feedback = house.Feedback,
                carriedItemType = recycler.Carried ? (int)recycler.Carried.itemType : -1,
                battle = boss.bossAI ? boss.bossAI.CaptureBattleState() : null,
                inventory = CaptureLevel04Inventory(),
                worldPlasmaCollected = Level04PlasmaPickup.WorldPickupCollected,
                droppedPlasma = Level04PlasmaPickup.HasDroppedWeapon,
                droppedPlasmaPosition = Level04PlasmaPickup.DroppedPosition,
                droppedPlasmaRotation = Level04PlasmaPickup.DroppedRotation
            };
            var motor = player as SharedFirstPersonController;
            if (!motor || saved.battle == null || saved.inventory == null)
                throw new InvalidOperationException("Level04 combat, player or inventory unavailable: save refused.");
            saved.viewPitch = motor.SnapshotViewPitch;
            saved.viewYaw = motor.SnapshotViewYaw;
            saved.thirdPerson = PlayerViewPreference.Mode == PlayerViewMode.ThirdPerson;
            if (saved.droppedPlasma && saved.inventory.items.Exists(x => x.id == Level04PlasmaPickup.WeaponId))
                throw new InvalidOperationException("Dropped plasma cannot also be in inventory.");
            for (int i = 0; i < 5; i++)
            {
                var item = recycler.FindItemByType(i);
                bool consumed = (saved.successfullyRecycledMask & (1 << i)) != 0;
                if (!item && !consumed)
                    throw new InvalidOperationException("Unconsumed Level04 item " + i + " missing. Save refused.");
                saved.items.Add(new SDGProgressLevel04ItemV1
                {
                    itemType = i,
                    activeSelf = !consumed && item && item.gameObject.activeSelf,
                    position = item ? item.transform.position : Vector3.zero,
                    rotation = item ? item.transform.rotation : Quaternion.identity
                });
            }
            if (!IsValidLevel04World(saved))
                throw new InvalidDataException("Level04 Boss/recycle state is inconsistent. Save refused.");
            return saved;
        }

        private static void RestoreLevel04World(string scenePath, SDGProgressLevel04WorldV1 state)
        {
            if (!IsValidLevel04World(state))
                throw new InvalidDataException("Level04 world state in slot is invalid.");
            var boss = FindSceneComponent<BossHealth>(scenePath);
            var house = FindSceneComponent<RecycleHouse>(scenePath);
            var recycler = Level04RecycleController.Instance;
            if (!boss || !house || recycler == null || recycler.gameObject.scene.path != scenePath)
                throw new InvalidOperationException("Level04 module is not ready for world restore.");
            if (boss.maxHealth != state.bossMaxHealth)
                throw new InvalidDataException("Level04 Boss maximum health changed since the slot was saved.");
            if (state.battle == null || boss.bossAI == null)
                throw new InvalidOperationException("R4 combat snapshot or Boss AI missing.");
            boss.RestoreFromSave(state.bossHealth, state.bossDead, state.recyclePartsVisible);
            boss.bossAI.RestoreBattleState(state.battle, state.bossDead);
            house.RestoreFromSave(state.completedQuestions, state.successfullyRecycledMask, state.feedback);
            recycler.RestoreItemsFromSave(state.items, state.successfullyRecycledMask, state.carriedItemType);
            Level04PlasmaPickup.RestorePickupFromSave(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),
                state.worldPlasmaCollected, state.droppedPlasma,
                state.droppedPlasmaPosition, state.droppedPlasmaRotation);
            // Last: only now allow Level04ModuleAdapter to update its task list
            // (and, for 5/5, show the normal completion screen).
            restoreHealthPending = false;
            house.NotifyRestored();
        }

        private static SDGProgressLevel04InventoryV2 CaptureLevel04Inventory()
        {
            EcoAdventureRuntime bag = EcoAdventureRuntime.Instance;
            if (!bag || bag.quickSlots == null || bag.quickSlots.Length != 5)
                throw new InvalidOperationException("Persistent inventory not ready. Save refused.");
            var state = new SDGProgressLevel04InventoryV2
            {
                selectedSlot = bag.selectedSlot,
                ammoInMagazine = bag.ammoInMagazine,
                reserveAmmo = bag.reserveAmmo,
                factoryPowered = bag.factoryPowered,
                weaponCrafted = bag.weaponCrafted,
                quickSlots = (string[])bag.quickSlots.Clone()
            };
            foreach (var entry in bag.inventory)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id) || entry.quantity <= 0)
                    throw new InvalidOperationException("Invalid inventory item. Save refused.");
                state.items.Add(new SDGProgressInventoryItemV2
                { id = entry.id, name = entry.name, quantity = entry.quantity });
            }
            return state;
        }

        private static void RestoreLevel04Inventory(SDGProgressLevel04InventoryV2 state)
        {
            if (state == null || state.schema != 2 || state.items == null ||
                state.quickSlots == null || state.quickSlots.Length != 5 ||
                state.selectedSlot < 0 || state.selectedSlot >= 5 ||
                state.ammoInMagazine < 0 || state.ammoInMagazine > 24 ||
                state.reserveAmmo < 0 || state.reserveAmmo > 120)
                throw new InvalidDataException("Invalid R4 inventory snapshot.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in state.items)
                if (item == null || string.IsNullOrEmpty(item.id) || item.quantity < 1 ||
                    item.quantity > 100000 || !ids.Add(item.id))
                    throw new InvalidDataException("Corrupt or duplicate inventory item.");
            var slotIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in state.quickSlots)
                if (!string.IsNullOrEmpty(id) && (!ids.Contains(id) || !slotIds.Add(id)))
                    throw new InvalidDataException("Corrupt quick-slot assignment.");
            EcoAdventureRuntime bag = EcoAdventureRuntime.Instance;
            if (!bag || bag.quickSlots == null || bag.quickSlots.Length != 5)
                throw new InvalidOperationException("Persistent inventory unavailable for restoration.");
            // This list is the live owner of inventory quantities; reconstruct it
            // atomically from validated slot data instead of calling AddItem (which
            // could auto-equip or refuse world-unique items).
            bag.inventory.Clear();
            foreach (var item in state.items)
                bag.inventory.Add(new EcoAdventureRuntime.InventoryItem
                { id = item.id, name = item.name, quantity = item.quantity });
            Array.Copy(state.quickSlots, bag.quickSlots, 5);
            bag.selectedSlot = state.selectedSlot;
            bag.ammoInMagazine = state.ammoInMagazine;
            bag.reserveAmmo = state.reserveAmmo;
            bag.factoryPowered = state.factoryPowered;
            bag.weaponCrafted = state.weaponCrafted;
            bag.RepairQuickSlots();
            bag.RefreshInventoryPresentation();
            Debug.Log("[SDG FullScene R4] INVENTORY RESTORED items=" + state.items.Count
                + " slot=" + state.selectedSlot + " ammo=" + state.ammoInMagazine
                + "/" + state.reserveAmmo);
        }

        private static void RestoreLevel04View(Component player, SDGProgressLevel04WorldV1 state, Quaternion playerRotation)
        {
            var motor = player as SharedFirstPersonController;
            if (!motor) throw new InvalidOperationException("Player view controller missing.");
            PlayerViewPreference.Mode = state.thirdPerson ? PlayerViewMode.ThirdPerson : PlayerViewMode.FirstPerson;
            var rig = motor.GetComponent<PlayerPerspectiveRig>();
            if (rig) rig.ApplyMode();
            motor.RestoreViewFromSave(state.viewPitch, state.viewYaw);
            motor.transform.rotation = playerRotation;
            Debug.Log("[SDG FullScene R4] VIEW RESTORED pitch=" + state.viewPitch
                + " yaw=" + state.viewYaw);
        }

        private static bool TryCapturePlayerHealth(Component player, ref int current, ref int maximum)
        {
            // Read the live player health, rather than a possibly delayed HUD/PlayerPrefs mirror.
            foreach (Component component in player.GetComponents<Component>())
            {
                if (component == null) continue;
                Type type = component.GetType();
                PropertyInfo hp = type.GetProperty("CurrentHealth", BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo max = type.GetProperty("MaxHealth", BindingFlags.Public | BindingFlags.Instance);
                if (hp == null || max == null || !hp.CanRead || !max.CanRead) continue;
                try
                {
                    int valueMax = Mathf.Max(1, Convert.ToInt32(max.GetValue(component, null)));
                    int valueCurrent = Mathf.Clamp(Convert.ToInt32(hp.GetValue(component, null)), 0, valueMax);
                    maximum = valueMax;
                    current = valueCurrent;
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[SDG Progress] Live HP capture failed: " + ex.Message);
                }
            }
            return false;
        }

        private static bool TryRestorePlayerHealth(Component player, int savedHealth)
        {
            foreach (Component component in player.GetComponents<Component>())
            {
                if (component == null) continue;
                Type type = component.GetType();
                MethodInfo method = type.GetMethod("RestoreFromSave", BindingFlags.Public | BindingFlags.Instance,
                    null, new Type[] { typeof(int) }, null);
                PropertyInfo hp = type.GetProperty("CurrentHealth", BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo max = type.GetProperty("MaxHealth", BindingFlags.Public | BindingFlags.Instance);
                if (method == null || hp == null || max == null || !hp.CanRead || !max.CanRead) continue;
                try
                {
                    method.Invoke(component, new object[] { savedHealth });
                    int actualMax = Mathf.Max(1, Convert.ToInt32(max.GetValue(component, null)));
                    int actualHP = Mathf.Clamp(Convert.ToInt32(hp.GetValue(component, null)), 0, actualMax);
                    SDGFrontEndPlayerVitals.ReportHealth(actualHP, actualMax);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[SDG Progress] Player HP restoration failed: " + ex.Message);
                }
            }
            return false;
        }

        private static Component FindLocalPlayer(string scenePath)
        {
            var behaviours = FindObjectsOfType<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var candidate = behaviours[i];
                if (candidate != null && candidate.gameObject.scene.path == scenePath &&
                    (candidate.GetType().Name == "SharedFirstPersonController" ||
                     candidate.GetType().Name == "C3FirstPersonController"))
                    return candidate;
            }
            return null;
        }

        private static bool TryTeleport(Component player, Vector3 position, Quaternion rotation)
        {
            try
            {
                var method = player.GetType().GetMethod("Teleport", BindingFlags.Instance | BindingFlags.Public,
                    null, new[] { typeof(Vector3), typeof(Quaternion) }, null);
                if (method != null)
                    method.Invoke(player, new object[] { position, rotation });
                else
                {
                    CharacterController controller = player.GetComponent<CharacterController>();
                    if (controller != null) controller.enabled = false;
                    player.transform.SetPositionAndRotation(position, rotation);
                    if (controller != null) controller.enabled = true;
                }
                return true;
            }
            catch (Exception ex) { Debug.LogWarning("[SDG Progress] Teleport: " + ex.Message); return false; }
        }

        private static List<SDGProgressDoorV1> CaptureDoors(string scenePath)
        {
            var saved = new List<SDGProgressDoorV1>();
            foreach (var behaviour in FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.gameObject.scene.path != scenePath ||
                    behaviour.GetType().Name != "SDGVerticalShutterDoor") continue;
                PropertyInfo p = behaviour.GetType().GetProperty("IsOpen", BindingFlags.Instance | BindingFlags.Public);
                if (p == null || p.PropertyType != typeof(bool)) continue;
                try
                {
                    saved.Add(new SDGProgressDoorV1
                    {
                        hierarchyPath = HierarchyPath(behaviour.transform),
                        isOpen = (bool)p.GetValue(behaviour, null)
                    });
                }
                catch (Exception ex) { Debug.LogWarning("[SDG Progress] Door capture: " + ex.Message); }
            }
            return saved;
        }

        private static void RestoreDoors(SDGProgressRecordV1 record, string scenePath)
        {
            if (record.doors == null || record.doors.Count == 0) return;
            foreach (var behaviour in FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.gameObject.scene.path != scenePath ||
                    behaviour.GetType().Name != "SDGVerticalShutterDoor") continue;
                string path = HierarchyPath(behaviour.transform);
                for (int i = 0; i < record.doors.Count; i++)
                {
                    SDGProgressDoorV1 state = record.doors[i];
                    if (state == null || state.hierarchyPath != path) continue;
                    try
                    {
                        MethodInfo m = behaviour.GetType().GetMethod("SetInstantOpen", BindingFlags.Instance | BindingFlags.Public,
                            null, new[] { typeof(bool) }, null);
                        if (m != null) m.Invoke(behaviour, new object[] { state.isOpen });
                    }
                    catch (Exception ex) { Debug.LogWarning("[SDG Progress] Door restore: " + ex.Message); }
                    break;
                }
            }
        }

        private static string HierarchyPath(Transform t)
        {
            string path = t.name + "[" + t.GetSiblingIndex() + "]";
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "[" + t.GetSiblingIndex() + "]/" + path;
            }
            return path;
        }
    }

    // R5.1: world save bridge kept in this compilation unit to avoid missing-type CS0246.

    // Opt-in, narrowly scoped state bridge.  This is NOT a generic Unity object serializer.
    // The supported field list is deliberately explicit: engine references and event handlers
    // cannot safely be round-tripped through JsonUtility.
    [Serializable]
    public sealed class SDGWorldFieldV5
    {
        public string name;
        public string kind;
        public string value;
    }
    [Serializable]
    public sealed class SDGWorldComponentV5
    {
        public string path;
        public string type;
        public int ordinal;
        public List<SDGWorldFieldV5> values = new List<SDGWorldFieldV5>();
    }
    [Serializable]
    public sealed class SDGWorldObjectV5
    {
        public string path;
        public bool active;
        public Vector3 position;
        public Quaternion rotation;
        public bool body;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public bool isKinematic;
        public bool useGravity;
    }
    [Serializable]
    public sealed class SDGWorldLevel02HeldBoxV5
    {
        // Same ordinal-safe transform key as the V5 Rigidbody snapshot.
        public string path;
        public bool previousKinematic;
        public bool previousGravity;
        public bool previousDetectCollisions;
        public int previousInterpolation;
        public int previousCollisionMode;
        public bool[] previousPlayerIgnores;
    }

    [Serializable]
    public sealed class SDGSceneWorldV5
    {
        public int schema = 5;
        public int level;
        public List<SDGWorldComponentV5> components = new List<SDGWorldComponentV5>();
        public List<SDGWorldObjectV5> objects = new List<SDGWorldObjectV5>();
        public SDGProgressLevel04InventoryV2 inventory;
        public List<string> removedAuthoredObjects = new List<string>();
        // Optional: older V5 slots omit this field and remain loadable.
        public SDGWorldLevel02HeldBoxV5 level02HeldBox;
        // Optional R1 safe-completed level-3 production snapshot and its receipt ledger.
        public C3TreasurePuzzle.SDGC3ProductionCheckpointR1 level03Production;
        public SDGC3ReceiptLedgerR1 level03Ledger;
    }

    public static class SDGSceneWorldSaveV5
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static string authoredScene;
        private static HashSet<string> authoredObjects = new HashSet<string>(StringComparer.Ordinal);
        public static void NoteSceneLoaded(Scene scene)
        {
            authoredScene = scene.path;
            authoredObjects.Clear();
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (MonoBehaviour c in AllBehaviours(scene))
                if (c && PhysicalTypes.Contains(Name(c))) authoredObjects.Add(Key(c.transform));
        }
        private static readonly Dictionary<string, string[]> Fields = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "TreasureDetector", new[] { "currentIndex", "isAnimating" } },
            { "DoorInteraction", new[] { "isOpenTriggered", "<IsOpen>k__BackingField", "<IsBusy>k__BackingField" } },
            { "SlidingStarGate", new[] { "state", "progress", "approachSide" } },
            { "StarGoalPlate", new[] { "complete", "settledFor", "<TotalStars>k__BackingField", "<PlacedStars>k__BackingField" } },
            { "C3FirstPersonController", new[] { "lookPitch", "verticalSpeed", "isCrouching", "isProne" } },
            { "C3CollectiblePart", new[] { "collected", "dropped" } },
            { "EcoEnergyPickup", new[] { "collected" } },
            { "C3TreasureProgress", new[] { "collected", "<PartsSubmitted>k__BackingField", "<LastFeedback>k__BackingField" } },
            { "C3PuzzleState", new[] { "<PuzzleSolved>k__BackingField", "<SubmissionCount>k__BackingField" } },
            { "C3DeviceState", new[] { "<FactoryState>k__BackingField", "startupElapsed", "<CompletionSeconds>k__BackingField" } },
            { "SDGLevel03StoryV2Controller", new[] { "quest", "screen", "gridComplete", "assemblyAuthorized", "assemblyStage",
                "tracing", "segmentProgress", "runningNotified", "waitingForScreen", "completedTargets", "rangePlaced",
                "targetCompleted", "targetHomes", "targetDirections", "calibrationRound", "calibrationMisses", "traceStepIndex" } },
            { "SDGLevel03R5SortingEnergyGame", new[] { "phase", "timeRemaining", "processed", "correct", "wrong",
                "combo", "bestCombo", "power", "loadSelected", "shuffledItems", "finishedCallback" } }
        };
        private static readonly HashSet<string> PhysicalTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "C3CollectiblePart", "EcoLootPickup", "EcoEnergyPickup", "Level04PlasmaPickup",
            "WeaponPickup", "RecycleItem"
        };
        private static List<MonoBehaviour> AllBehaviours(Scene scene)
        {
            var result = new List<MonoBehaviour>();
            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<MonoBehaviour>(true));
            return result;
        }
        private static List<Transform> AllTransforms(Scene scene)
        {
            var result = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<Transform>(true));
            return result;
        }
        private static string Key(Transform obj)
        {
            var part = obj.GetComponent<C3TreasurePuzzle.C3CollectiblePart>();
            if (part && !string.IsNullOrEmpty(part.PartId)) return "@C3Part:" + part.PartId;
            // Same-name ordinal rather than absolute sibling index: destroying an unrelated
            // pickup cannot change the keys of all later scene objects.
            string path = Segment(obj);
            while (obj.parent != null)
            { obj = obj.parent; path = Segment(obj) + "/" + path; }
            return path;
        }
        private static string Segment(Transform obj)
        {
            int ordinal = 0;
            if (obj.parent != null)
            {
                for (int i = 0; i < obj.GetSiblingIndex(); i++)
                    if (obj.parent.GetChild(i).name == obj.name) ordinal++;
            }
            else
            {
                foreach (GameObject root in obj.gameObject.scene.GetRootGameObjects())
                {
                    if (root.transform == obj) break;
                    if (root.name == obj.name) ordinal++;
                }
            }
            return obj.name + "[" + ordinal + "]";
        }
        private static string Name(MonoBehaviour c) { return c.GetType().Name; }
        private static string PrimitiveKind(Type t)
        {
            if (t == typeof(bool)) return "bool";
            if (t == typeof(int)) return "int";
            if (t == typeof(float)) return "float";
            if (t == typeof(string)) return "string";
            if (t == typeof(bool[])) return "bool[]";
            if (t == typeof(int[])) return "int[]";
            if (t == typeof(Vector3[])) return "vector3[]";
            if (t == typeof(HashSet<string>)) return "string-set";
            if (t.IsEnum) return "enum";
            return null;
        }
        private static string Encode(object value, string kind)
        {
            if (value == null) return "";
            if (kind == "bool[]")
            {
                var array = (bool[])value;
                var parts = new string[array.Length];
                for (int i = 0; i < parts.Length; i++) parts[i] = array[i] ? "1" : "0";
                return string.Join(",", parts);
            }
            if (kind == "int[]") return string.Join(",", (int[])value);
            if (kind == "vector3[]")
            {
                var vectors = (Vector3[])value;
                var parts = new string[vectors.Length];
                for (int i = 0; i < parts.Length; i++)
                    parts[i] = vectors[i].x.ToString("R", CultureInfo.InvariantCulture) + "," +
                        vectors[i].y.ToString("R", CultureInfo.InvariantCulture) + "," +
                        vectors[i].z.ToString("R", CultureInfo.InvariantCulture);
                return string.Join("|", parts);
            }
            if (kind == "string-set")
            {
                var values = new List<string>((HashSet<string>)value);
                values.Sort(StringComparer.Ordinal);
                return string.Join("|", values);
            }
            if (kind == "bool") return (bool)value ? "1" : "0";
            if (kind == "enum") return Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        private static object Decode(string value, string kind, Type type)
        {
            switch (kind)
            {
                case "bool": return value == "1";
                case "int": return int.Parse(value, CultureInfo.InvariantCulture);
                case "float": return float.Parse(value, CultureInfo.InvariantCulture);
                case "string": return value ?? "";
                case "enum": return Enum.ToObject(type, int.Parse(value, CultureInfo.InvariantCulture));
                case "bool[]":
                    if (string.IsNullOrEmpty(value)) return new bool[0];
                    var b = value.Split(','); var result = new bool[b.Length];
                    for (int i = 0; i < result.Length; i++) result[i] = b[i] == "1";
                    return result;
                case "int[]":
                    if (string.IsNullOrEmpty(value)) return new int[0];
                    var parts = value.Split(','); var ints = new int[parts.Length];
                    for (int i = 0; i < ints.Length; i++) ints[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
                    return ints;
                case "vector3[]":
                    if (string.IsNullOrEmpty(value)) return new Vector3[0];
                    var entries = value.Split('|'); var vectors = new Vector3[entries.Length];
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var coordinates = entries[i].Split(',');
                        if (coordinates.Length != 3) throw new FormatException("Invalid saved vector.");
                        vectors[i] = new Vector3(
                            float.Parse(coordinates[0], CultureInfo.InvariantCulture),
                            float.Parse(coordinates[1], CultureInfo.InvariantCulture),
                            float.Parse(coordinates[2], CultureInfo.InvariantCulture));
                    }
                    return vectors;
                case "string-set": return new HashSet<string>(string.IsNullOrEmpty(value)
                    ? new string[0] : value.Split('|'), StringComparer.Ordinal);
                default: throw new InvalidOperationException("Unsupported field kind: " + kind);
            }
        }
        private static int Ordinal(MonoBehaviour behaviour)
        {
            int index = 0;
            foreach (var component in behaviour.GetComponents<MonoBehaviour>())
                if (component && component.GetType() == behaviour.GetType())
                { if (component == behaviour) return index; index++; }
            return -1;
        }
        private static void RequireStable(Scene scene, int level, List<MonoBehaviour> behaviours)
        {
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in behaviours)
            {
                if (!c || c.gameObject.scene != scene) continue;
                string name = Name(c);
                present.Add(name);
                if (level == 1 && name == "TreasureDetector" && ReadBool(c, "isAnimating"))
                    throw new InvalidOperationException("第一關挖掘動畫進行中，請等待物品完成回收後再存檔。此動畫尚不支援逐影格還原。");
                if (level == 1 && name == "DoorInteraction" && ReadBool(c, "<IsBusy>k__BackingField"))
                    throw new InvalidOperationException("第一關開門動畫進行中，請等待動畫完成後再存檔。");
                if (level == 2 && name == "Level02BoxCarryController")
                {
                    var carrier = c as Level02BoxCarryController;
                    if (carrier == null || (carrier.HeldBox != null &&
                        carrier.HeldBox.gameObject.scene != scene))
                        throw new InvalidOperationException("第二關箱子控制器或場景不一致；拒絕建立不完整存檔。");
                }
                if (level == 3 && name == "SDGLevel03StoryV2Controller")
                {
                    FieldInfo screen = c.GetType().GetField("screen", Flags);
                    if (screen != null && Convert.ToInt32(screen.GetValue(c)) != 0)
                        throw new InvalidOperationException("第三關小遊戲畫面仍開啟，這個畫面尚無安全的逐步還原器；請先完成或關閉該操作再存檔。");
                    FieldInfo quest = c.GetType().GetField("quest", Flags);
                    int stage = quest == null ? -1 : Convert.ToInt32(quest.GetValue(c));
                    if (stage >= 1 && stage <= 3)
                        throw new InvalidOperationException("第三關電網互動進度尚無完整還原器；請完成該電網階段後再存檔。");
                }
                if (level == 3 && name == "C3RecyclingLine")
                {
                    var line = c as C3TreasurePuzzle.C3RecyclingLine;
                    if (!line) throw new InvalidOperationException("第三關產線控制器無效。");
                    line.CaptureCompletedCheckpointR1(); // refuses in-flight and presentation states
                }
            }
            string[] required = level == 1 ? new[] { "TreasureDetector", "DoorInteraction" } :
                level == 2 ? new[] { "StarGoalPlate", "SlidingStarGate" } :
                level == 3 ? new[] { "C3TreasureProgress", "C3PuzzleState", "C3DeviceState", "SDGLevel03StoryV2Controller" } :
                new string[0];
            foreach (string name in required)
                if (!present.Contains(name))
                    throw new InvalidOperationException("缺少本關世界狀態控制器：" + name + "；拒絕建立不完整存檔。");
        }
        private static bool ReadBool(MonoBehaviour c, string field)
        {
            FieldInfo f = c.GetType().GetField(field, Flags);
            return f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(c);
        }
        public static SDGSceneWorldV5 Capture(Scene scene, int level, SDGProgressLevel04InventoryV2 inventory)
        {
            if (!scene.IsValid() || !scene.isLoaded || level < 1 || level > 4)
                throw new InvalidOperationException("World capture invoked for invalid scene.");
            var behaviours = AllBehaviours(scene);
            RequireStable(scene, level, behaviours);
            if (authoredScene != scene.path)
                throw new InvalidOperationException("Scene baseline was not registered: refusing partial save.");
            var state = new SDGSceneWorldV5 { level = level, inventory = inventory };
            if (level == 3)
            {
                C3TreasurePuzzle.C3RecyclingLine line = null;
                foreach (MonoBehaviour b in behaviours)
                    if (b is C3TreasurePuzzle.C3RecyclingLine candidate)
                    {
                        if (line != null) throw new InvalidOperationException("第三關有多個產線控制器，拒絕存檔。");
                        line = candidate;
                    }
                EcoAdventureRuntime runtime = EcoAdventureRuntime.Instance;
                if (!line || !runtime)
                    throw new InvalidOperationException("第三關產線或背包尚未準備完成，拒絕存檔。");
                state.level03Production = line.CaptureCompletedCheckpointR1();
                state.level03Ledger = runtime.CaptureRecyclingLedgerForSaveR1();
                if (!C3TreasurePuzzle.C3RecyclingLine.CoveredByLedgerR1(state.level03Production, state.level03Ledger))
                    throw new InvalidOperationException("第三關加工紀錄與玩家收據不一致，拒絕存檔。");
            }
            if (level == 2)
            {
                foreach (MonoBehaviour item in behaviours)
                {
                    var carrier = item as Level02BoxCarryController;
                    if (!carrier || carrier.HeldBox == null) continue;
                    if (state.level02HeldBox != null)
                        throw new InvalidOperationException("第二關發現多個搬箱控制器，拒絕儲存含糊的持有狀態。");
                    bool[] collisionFlags = carrier.SavePreviousPlayerIgnores;
                    if (collisionFlags == null || collisionFlags.Length == 0 ||
                        carrier.HeldBox.GetComponentsInChildren<Collider>().Length != collisionFlags.Length)
                        throw new InvalidOperationException("第二關持有箱子的碰撞設定不完整，拒絕儲存。");
                    state.level02HeldBox = new SDGWorldLevel02HeldBoxV5
                    {
                        path = Key(carrier.HeldBox.transform),
                        previousKinematic = carrier.SavePreviousKinematic,
                        previousGravity = carrier.SavePreviousGravity,
                        previousDetectCollisions = carrier.SavePreviousDetectCollisions,
                        previousInterpolation = carrier.SavePreviousInterpolation,
                        previousCollisionMode = carrier.SavePreviousCollisionMode,
                        previousPlayerIgnores = collisionFlags
                    };
                }
            }
            var stillPresent = new HashSet<string>(StringComparer.Ordinal);
            foreach (MonoBehaviour c in behaviours)
                if (c && PhysicalTypes.Contains(Name(c))) stillPresent.Add(Key(c.transform));
            foreach (string id in authoredObjects)
                if (!stillPresent.Contains(id)) state.removedAuthoredObjects.Add(id);
            var capturePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var behaviour in behaviours)
            {
                if (!behaviour || !Fields.TryGetValue(Name(behaviour), out string[] selected)) continue;
                var entry = new SDGWorldComponentV5
                { path = Key(behaviour.transform), type = behaviour.GetType().FullName, ordinal = Ordinal(behaviour) };
                foreach (string name in selected)
                {
                    FieldInfo field = behaviour.GetType().GetField(name, Flags);
                    if (field == null) throw new MissingFieldException(entry.type, name);
                    string kind = PrimitiveKind(field.FieldType);
                    if (kind == null) throw new InvalidOperationException("Unsupported field: " + entry.type + "." + name);
                    entry.values.Add(new SDGWorldFieldV5 { name = name, kind = kind, value = Encode(field.GetValue(behaviour), kind) });
                }
                state.components.Add(entry);
                if (PhysicalTypes.Contains(Name(behaviour))) capturePaths.Add(Key(behaviour.transform));
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
                    if (body) capturePaths.Add(Key(body.transform));
                if (level == 1)
                {
                    foreach (TreasureDetector detector in root.GetComponentsInChildren<TreasureDetector>(true))
                        foreach (Transform treasure in detector.targetBottles)
                            if (treasure) capturePaths.Add(Key(treasure));
                }
            }
            var transforms = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform t in AllTransforms(scene))
            {
                string key = Key(t);
                if (capturePaths.Contains(key)) transforms[key] = t;
            }
            if (transforms.Count != capturePaths.Count || transforms.Count > 2048 || state.components.Count > 512)
                throw new InvalidOperationException("Scene identity incomplete or snapshot exceeds safety limits.");
            foreach (var t in transforms)
            {
                Rigidbody b = t.Value.GetComponent<Rigidbody>();
                state.objects.Add(new SDGWorldObjectV5
                {
                    path = t.Key, active = t.Value.gameObject.activeSelf,
                    position = t.Value.position, rotation = t.Value.rotation,
                    body = b != null, velocity = b ? b.velocity : Vector3.zero,
                    angularVelocity = b ? b.angularVelocity : Vector3.zero,
                    isKinematic = b && b.isKinematic, useGravity = b && b.useGravity
                });
            }
            return state;
        }
        public static bool Valid(SDGSceneWorldV5 state, int level)
        {
            return state != null && state.schema == 5 && state.level == level &&
                state.components != null && state.objects != null &&
                state.components.Count <= 512 && state.objects.Count <= 2048 &&
                state.removedAuthoredObjects != null && state.removedAuthoredObjects.Count <= 2048 &&
                state.inventory != null && state.inventory.schema == 2 &&
                (state.level02HeldBox == null || (level == 2 &&
                    !string.IsNullOrEmpty(state.level02HeldBox.path) &&
                    state.level02HeldBox.previousPlayerIgnores != null &&
                    state.level02HeldBox.previousPlayerIgnores.Length > 0 &&
                    state.level02HeldBox.previousPlayerIgnores.Length <= 64)) &&
                (state.level03Production == null || (level == 3 &&
                    C3TreasurePuzzle.C3RecyclingLine.ValidCompletedCheckpointR1(state.level03Production) &&
                    EcoAdventureRuntime.ValidRecyclingLedgerForSaveR1(state.level03Ledger) &&
                    C3TreasurePuzzle.C3RecyclingLine.CoveredByLedgerR1(state.level03Production, state.level03Ledger))) &&
                (state.level03Ledger == null || state.level03Production != null);
        }
        public static void Restore(Scene scene, SDGSceneWorldV5 state)
        {
            if (!Valid(state, state == null ? 0 : state.level)) throw new InvalidOperationException("Invalid world snapshot version.");
            var objects = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform t in AllTransforms(scene)) objects[Key(t)] = t;
            var behaviours = AllBehaviours(scene);
            var behaviourMap = new Dictionary<string, MonoBehaviour>(StringComparer.Ordinal);
            foreach (MonoBehaviour b in behaviours)
            {
                if (!b || !Fields.ContainsKey(Name(b))) continue;
                behaviourMap[Key(b.transform) + "|" + b.GetType().FullName + "|" + Ordinal(b)] = b;
            }
            // Validate level-3 production and ledger BEFORE any field mutation.
            C3TreasurePuzzle.C3RecyclingLine level03Line = null;
            EcoAdventureRuntime level03Runtime = null;
            if (state.level == 3 && state.level03Production != null)
            {
                foreach (MonoBehaviour c in behaviours)
                    if (c is C3TreasurePuzzle.C3RecyclingLine line)
                    {
                        if (level03Line != null) throw new InvalidOperationException("第三關有重複產線控制器。");
                        level03Line = line;
                    }
                level03Runtime = EcoAdventureRuntime.Instance;
                if (!level03Line || !level03Runtime ||
                    !C3TreasurePuzzle.C3RecyclingLine.CoveredByLedgerR1(state.level03Production, state.level03Ledger))
                    throw new InvalidOperationException("第三關產線還原所需的場景或收據資料不完整。");
            }
            // Fail before modifying any state if a required authored object is missing.
            Level02BoxCarryController level02Carrier = null;
            Rigidbody carriedBody = null;
            if (state.level02HeldBox != null)
            {
                Transform heldTransform;
                if (!objects.TryGetValue(state.level02HeldBox.path, out heldTransform) ||
                    !heldTransform || !(carriedBody = heldTransform.GetComponent<Rigidbody>()))
                    throw new InvalidOperationException("第二關存檔的持有箱子不在原場景中。");
                foreach (MonoBehaviour candidate in behaviours)
                {
                    var matching = candidate as Level02BoxCarryController;
                    if (matching == null) continue;
                    if (level02Carrier != null)
                        throw new InvalidOperationException("第二關有多個搬箱控制器，無法確定存檔持有者。");
                    level02Carrier = matching;
                }
                if (!level02Carrier || !level02Carrier.isActiveAndEnabled ||
                    carriedBody.GetComponentsInChildren<Collider>().Length !=
                    state.level02HeldBox.previousPlayerIgnores.Length)
                    throw new InvalidOperationException("第二關搬箱控制器或碰撞器與存檔不相容。");
            }
            foreach (string gone in state.removedAuthoredObjects)
                if (string.IsNullOrEmpty(gone) || !objects.ContainsKey(gone))
                    throw new InvalidOperationException("Authored item removal could not be replayed: " + gone);
            foreach (var o in state.objects)
                if (o == null || string.IsNullOrEmpty(o.path) || !objects.ContainsKey(o.path))
                    throw new InvalidOperationException("World restore object missing: " + (o == null ? "null" : o.path));
            foreach (var c in state.components)
            {
                if (c == null || string.IsNullOrEmpty(c.path) || string.IsNullOrEmpty(c.type) || c.values == null ||
                    !behaviourMap.ContainsKey(c.path + "|" + c.type + "|" + c.ordinal))
                    throw new InvalidOperationException("World restore component missing: " + (c == null ? "null" : c.type));
                MonoBehaviour target = behaviourMap[c.path + "|" + c.type + "|" + c.ordinal];
                if (!Fields.TryGetValue(Name(target), out string[] allowed) || c.values.Count != allowed.Length)
                    throw new InvalidOperationException("World component schema mismatch: " + c.type);
                foreach (var item in c.values)
                {
                    if (item == null || Array.IndexOf(allowed, item.name) < 0) throw new InvalidOperationException("Unknown saved field.");
                    FieldInfo f = target.GetType().GetField(item.name, Flags);
                    if (f == null || PrimitiveKind(f.FieldType) != item.kind) throw new InvalidOperationException("Saved field type mismatch.");
                    Decode(item.value, item.kind, f.FieldType); // validation pass
                }
            }
            foreach (var c in state.components)
            {
                MonoBehaviour target = behaviourMap[c.path + "|" + c.type + "|" + c.ordinal];
                foreach (var item in c.values)
                {
                    FieldInfo f = target.GetType().GetField(item.name, Flags);
                    object decoded = Decode(item.value, item.kind, f.FieldType);
                    if (item.kind == "string-set")
                    {
                        var original = (HashSet<string>)f.GetValue(target);
                        if (original == null) throw new InvalidOperationException("Readonly quest collection unavailable.");
                        original.Clear(); original.UnionWith((HashSet<string>)decoded);
                    }
                    else if (f.IsInitOnly && (item.kind == "int[]" || item.kind == "bool[]" || item.kind == "vector3[]"))
                    {
                        Array original = f.GetValue(target) as Array;
                        Array clone = decoded as Array;
                        if (original == null || clone == null || original.Length != clone.Length)
                            throw new InvalidOperationException("Readonly mission array dimensions changed.");
                        Array.Copy(clone, original, original.Length);
                    }
                    else f.SetValue(target, decoded);
                }
            }
            foreach (MonoBehaviour target in behaviours)
            {
                if (!target || Name(target) != "SDGLevel03StoryV2Controller") continue;
                MethodInfo rebuild = target.GetType().GetMethod("RebuildRangeAfterSaveV5", Flags, null, Type.EmptyTypes, null);
                if (rebuild == null) throw new MissingMethodException(target.GetType().FullName, "RebuildRangeAfterSaveV5");
                rebuild.Invoke(target, null);
            }
            foreach (string gone in state.removedAuthoredObjects)
                objects[gone].gameObject.SetActive(false);
            foreach (var s in state.objects)
            {
                Transform t = objects[s.path];
                Rigidbody b = s.body ? t.GetComponent<Rigidbody>() : null;
                if (s.body && !b)
                {
                    if (t.GetComponent<C3TreasurePuzzle.C3CollectiblePart>() != null)
                        b = t.gameObject.AddComponent<Rigidbody>();
                    else throw new InvalidOperationException("Missing physics component: " + s.path);
                }
                if (b) { b.isKinematic = true; b.velocity = Vector3.zero; b.angularVelocity = Vector3.zero; }
                t.SetPositionAndRotation(s.position, s.rotation);
                if (b)
                { b.isKinematic = s.isKinematic; b.useGravity = s.useGravity; b.velocity = s.velocity; b.angularVelocity = s.angularVelocity; }
                if (t.gameObject.activeSelf != s.active) t.gameObject.SetActive(s.active);
            }
            // Rebuild ignore-collision relationships only AFTER Rigidbody poses have
            // been reconstructed. This avoids loading a kinematic held box with no owner.
            if (state.level02HeldBox != null)
            {
                var held = state.level02HeldBox;
                if (!level02Carrier.RestoreCarryFromWorldSave(carriedBody,
                    held.previousKinematic, held.previousGravity, held.previousDetectCollisions,
                    held.previousInterpolation, held.previousCollisionMode, held.previousPlayerIgnores))
                    throw new InvalidOperationException("第二關持有箱子的物理狀態恢復失敗；已停止遊戲以保護存檔。");
            }
            // Recreate official finished bales without replaying transactions or achievements.
            if (level03Line != null)
            {
                level03Runtime.RestoreRecyclingLedgerFromSaveR1(state.level03Ledger);
                level03Line.RestoreCompletedCheckpointR1(state.level03Production);
            }
            // Refresh only proven non-mutating display / visuals routines after field reconstruction.
            foreach (MonoBehaviour target in behaviours)
            {
                if (!target) continue;
                string name = Name(target);
                string method = name == "TreasureDetector" ? "UpdateDistanceUI" :
                    name == "SlidingStarGate" ? "MoveLeaves" :
                    name == "SDGLevel03StoryV2Controller" ? "RefreshTask" : null;
                if (method == null) continue;
                MethodInfo refresh = target.GetType().GetMethod(method, Flags, null, Type.EmptyTypes, null);
                if (refresh != null) refresh.Invoke(target, null);
            }
        }
    }
}
