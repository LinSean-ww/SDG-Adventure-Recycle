using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using SDGAdventure.ModuleSystem;
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
                    restoreHealthPending = false;
                    Debug.Log("[SDG Progress R2] RESTORED slot=" + record.slot
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
                    doors = CaptureDoors(scene.path)
                };
                string savedPath = SlotPath(slot);
                WriteAtomic(savedPath, JsonUtility.ToJson(record, true));
                if (!TryReadSlot(slot, out SDGProgressRecordV1 readBack) ||
                    readBack.health != record.health || readBack.maxHealth != record.maxHealth ||
                    readBack.position != record.position)
                    throw new InvalidDataException("存檔寫入後回讀驗證不一致，請保留診斷資料。");
                Debug.Log("[SDG Progress R2] SAVED slot=" + slot + " level=" + level
                    + " HP=" + record.health + "/" + record.maxHealth
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
                       !string.IsNullOrEmpty(record.savedUtc);
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
