using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using SDGAdventure.ModuleSystem;
using SDGAdventure.Level04;
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
            get { return Mathf.Clamp(PlayerPrefs.GetInt(SlotPref, -1), -1, 3); }
            set { PlayerPrefs.SetInt(SlotPref, Mathf.Clamp(value, 0, 3)); PlayerPrefs.Save(); }
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

            if (inLevel && ManualSavePressed())
            {
                int slot = SelectedSlot;
                if (slot == 0 || slot == -1) { slot = 1; SelectedSlot = 1; } // F5 never overwrites Auto.
                if (!TrySave(slot, out string msg)) Debug.LogWarning("[SDG Progress] " + msg);
                else Debug.Log("[SDG Progress] " + msg);
            }
            if (inLevel && ManualLoadPressed())
            {
                int slot = SelectedSlot < 0 ? NewestSlot() : SelectedSlot;
                if (!TryLoad(slot, out string msg)) Debug.LogWarning("[SDG Progress] " + msg);
            }
            if (inLevel && AutoSaveEnabled && Time.timeScale > 0f && Time.unscaledTime >= nextAutoSave)
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
            nextAutoSave = Time.unscaledTime + 15f;
            if (restoreRoutine != null) StopCoroutine(restoreRoutine);
            if (pendingRestore != null)
            {
                var snapshot = pendingRestore;
                pendingRestore = null;
                if (snapshot.scenePath == scene.path)
                    restoreRoutine = StartCoroutine(RestoreAfterSpawn(scene.path, snapshot));
                else
                    restoreHealthPending = false;
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
                            Debug.LogError("[SDG Progress R3] WORLD RESTORE FAILED: Level04 module not initialized; saved slot remains unchanged.");
                            restoreRoutine = null;
                            yield break;
                        }
                        try
                        {
                            RestoreLevel04World(scenePath, record.level04World);
                        }
                        catch (Exception error)
                        {
                            restoreHealthPending = false;
                            Debug.LogError("[SDG Progress R3] WORLD RESTORE FAILED: " + error);
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
                    Debug.Log("[SDG Progress R3] RESTORED slot=" + record.slot
                        + " level=" + record.levelNumber + " HP=" + record.health + "/" + record.maxHealth
                        + " position=" + record.position + " scene=" + record.scenePath);
                    restoreRoutine = null;
                    yield break;
                }
                yield return null;
            }
            restoreHealthPending = false;
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
            if (slot < 0 || slot > 3) { message = "不支援的存檔欄位"; return false; }
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
                message = "找不到本關 SharedFirstPersonController，拒絕建立無法恢復位置的存檔。";
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
                    level04World = level == 4 ? CaptureLevel04World(scene.path) : null
                };
                string savedPath = SlotPath(slot);
                WriteAtomic(savedPath, JsonUtility.ToJson(record, true));
                if (!TryReadSlot(slot, out SDGProgressRecordV1 readBack) ||
                    readBack.health != record.health || readBack.maxHealth != record.maxHealth ||
                    readBack.position != record.position ||
                    (level == 4 && (readBack.level04World == null ||
                        readBack.level04World.bossHealth != record.level04World.bossHealth ||
                        readBack.level04World.bossDead != record.level04World.bossDead ||
                        readBack.level04World.completedQuestions != record.level04World.completedQuestions ||
                        readBack.level04World.successfullyRecycledMask != record.level04World.successfullyRecycledMask)))
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
            if (slot < 0 || slot > 3) return false;
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
            for (int i = 0; i <= 3; i++)
            {
                if (!TryReadSlot(i, out SDGProgressRecordV1 record)) continue;
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
                Time.timeScale = 1f;
                SceneManager.LoadScene(record.scenePath, LoadSceneMode.Single);
                message = "正在讀取第 " + record.levelNumber + " 關。";
                return true;
            }
            catch (Exception ex)
            {
                pendingRestore = null;
                restoreHealthPending = false;
                message = "讀取失敗：" + ex.Message;
                Debug.LogError("[SDG Progress] " + ex);
                return false;
            }
        }

        public static void SaveFromPauseMenu()
        {
            int slot = SelectedSlot;
            if (slot < 1) { slot = 1; SelectedSlot = 1; }
            if (TrySave(slot, out string msg)) Debug.Log("[SDG Progress] " + msg);
            else Debug.LogWarning("[SDG Progress] " + msg);
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

        private static SDGProgressLevel04WorldV1 CaptureLevel04World(string scenePath)
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
                carriedItemType = recycler.Carried ? (int)recycler.Carried.itemType : -1
            };
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
            boss.RestoreFromSave(state.bossHealth, state.bossDead, state.recyclePartsVisible);
            house.RestoreFromSave(state.completedQuestions, state.successfullyRecycledMask, state.feedback);
            recycler.RestoreItemsFromSave(state.items, state.successfullyRecycledMask, state.carriedItemType);
            // Last: only now allow Level04ModuleAdapter to update its task list
            // (and, for 5/5, show the normal completion screen).
            restoreHealthPending = false;
            house.NotifyRestored();
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
                    candidate.GetType().Name == "SharedFirstPersonController")
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
}
