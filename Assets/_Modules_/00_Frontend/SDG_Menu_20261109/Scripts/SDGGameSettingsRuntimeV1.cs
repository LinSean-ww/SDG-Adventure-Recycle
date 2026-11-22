using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace SDGAdventure.FrontEnd
{
    [Serializable]
    public sealed class SDGSettingsDataV1
    {
        public float master = 1f;
        public float music = .8f;
        public float effects = 1f;
        public bool fullscreen = true;
        public int width;
        public int height;
        public int quality;
        public float sensitivity = 2f;
        public bool invertY;
        public bool autoSave = true;
    }

    /// <summary>Shared preferences: audio, screen and shared player input across level transitions.</summary>
    public sealed class SDGGameSettingsRuntimeV1 : MonoBehaviour
    {
        const string P = "SDG_Settings_V1_";
        public const string AutoSaveKey = "SDG_SP_AutoEnabled";
        static SDGGameSettingsRuntimeV1 instance;
        static bool inverted;
        static SDGSettingsDataV1 current;
        readonly Dictionary<int, SourceState> sources = new Dictionary<int, SourceState>();
        readonly Dictionary<int, float> legacyMouseBases = new Dictionary<int, float>();
        float nextAudioScan, nextInputScan, nextOwnerScan;
        string activeOwner;
        sealed class SourceState { public AudioSource source; public float baseline, lastApplied, lastFactor; public bool music; }

        public static bool InvertY { get { return inverted; } }
        public static SDGSettingsDataV1 Current { get { return current ?? Load(); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; current = null; inverted = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            GameObject host = new GameObject("SDG Game Settings V1 (persistent)");
            DontDestroyOnLoad(host);
            host.AddComponent<SDGGameSettingsRuntimeV1>();
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            if (!PlayerPrefs.HasKey(P + "Auto_original"))
                PlayerPrefs.SetInt(P + "Auto_original", PlayerPrefs.GetInt(AutoSaveKey, 1));
            activeOwner = GetOwnerId();
            SyncOwnerAutoSave();
            Apply(Load(), false);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            nextAudioScan = 0f;
            nextInputScan = 0f;
        }
        void Update()
        {
            if (Time.unscaledTime >= nextOwnerScan)
            {
                nextOwnerScan = Time.unscaledTime + .75f;
                string owner = GetOwnerId();
                if (owner != activeOwner) { activeOwner = owner; SyncOwnerAutoSave(); }
                else
                {
                    // Keep the existing save-panel AutoSave button and this settings panel in sync.
                    string ownerKey = P + "Auto_" + owner;
                    int observed = PlayerPrefs.GetInt(AutoSaveKey, 1);
                    if (PlayerPrefs.GetInt(ownerKey, observed) != observed)
                    {
                        PlayerPrefs.SetInt(ownerKey, observed);
                        PlayerPrefs.Save();
                    }
                }
            }
            if (Time.unscaledTime >= nextAudioScan)
            {
                nextAudioScan = Time.unscaledTime + 1f;
                ApplySourceVolumes();
            }
            if (Time.unscaledTime >= nextInputScan)
            {
                nextInputScan = Time.unscaledTime + 1.75f;
                ApplyMouseSettings();
            }
        }

        public static SDGSettingsDataV1 Load()
        {
            SDGSettingsDataV1 s = new SDGSettingsDataV1();
            s.master = Mathf.Clamp01(PlayerPrefs.GetFloat(P + "Master", PlayerPrefs.GetFloat("SDG_FE_MasterVolume", 1f)));
            s.music = Mathf.Clamp01(PlayerPrefs.GetFloat(P + "Music", .8f));
            s.effects = Mathf.Clamp01(PlayerPrefs.GetFloat(P + "Effects", 1f));
            s.fullscreen = PlayerPrefs.GetInt(P + "Fullscreen", Screen.fullScreen ? 1 : 0) != 0;
            s.width = PlayerPrefs.GetInt(P + "Width", 0);
            s.height = PlayerPrefs.GetInt(P + "Height", 0);
            s.quality = PlayerPrefs.GetInt(P + "Quality", QualitySettings.GetQualityLevel());
            s.sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(P + "Sensitivity", 2f), .1f, 10f);
            s.invertY = PlayerPrefs.GetInt(P + "InvertY", 0) != 0;
            s.autoSave = ReadOwnerAutoSave();
            return s;
        }
        public static void SaveAndApply(SDGSettingsDataV1 s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            PlayerPrefs.SetFloat(P + "Master", Mathf.Clamp01(s.master));
            PlayerPrefs.SetFloat(P + "Music", Mathf.Clamp01(s.music));
            PlayerPrefs.SetFloat(P + "Effects", Mathf.Clamp01(s.effects));
            PlayerPrefs.SetInt(P + "Fullscreen", s.fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(P + "Width", Mathf.Max(0, s.width));
            PlayerPrefs.SetInt(P + "Height", Mathf.Max(0, s.height));
            PlayerPrefs.SetInt(P + "Quality", s.quality);
            PlayerPrefs.SetFloat(P + "Sensitivity", Mathf.Clamp(s.sensitivity, .1f, 10f));
            PlayerPrefs.SetInt(P + "InvertY", s.invertY ? 1 : 0);
            PlayerPrefs.SetInt(P + "Auto_" + GetOwnerId(), s.autoSave ? 1 : 0);
            PlayerPrefs.SetInt(AutoSaveKey, s.autoSave ? 1 : 0);
            // Keeps the old front-end volume UI consistent if visited again.
            PlayerPrefs.SetFloat("SDG_FE_MasterVolume", Mathf.Clamp01(s.master));
            PlayerPrefs.Save();
            Apply(s, true);
        }
        static void Apply(SDGSettingsDataV1 s, bool changeDisplay)
        {
            current = s;
            inverted = s.invertY;
            AudioListener.volume = s.master;
            if (QualitySettings.names.Length > 0)
                QualitySettings.SetQualityLevel(Mathf.Clamp(s.quality, 0, QualitySettings.names.Length - 1), true);
#if !UNITY_EDITOR
            if (changeDisplay || (s.width > 0 && s.height > 0))
            {
                if (s.width > 0 && s.height > 0) Screen.SetResolution(s.width, s.height, s.fullscreen);
                else Screen.fullScreen = s.fullscreen;
            }
#else
            // Never resize the Unity Editor Game View automatically.
#endif
            if (instance != null)
            {
                instance.ApplySourceVolumes();
                instance.ApplyMouseSettings();
            }
        }
        static string GetOwnerId()
        {
            try
            {
                Type t = typeof(SDGGameSettingsRuntimeV1).Assembly.GetType("SDGAdventure.FrontEnd.SDGSaveProfilesV2");
                PropertyInfo p = t == null ? null : t.GetProperty("ActiveId", BindingFlags.Public | BindingFlags.Static);
                string id = p == null ? null : p.GetValue(null, null) as string;
                return string.IsNullOrEmpty(id) ? "original" : id;
            }
            catch { return "original"; }
        }
        static bool ReadOwnerAutoSave()
        {
            string key = P + "Auto_" + GetOwnerId();
            return PlayerPrefs.GetInt(key, GetOwnerId() == "original" ? PlayerPrefs.GetInt(AutoSaveKey, 1) : 1) != 0;
        }
        void SyncOwnerAutoSave()
        {
            PlayerPrefs.SetInt(AutoSaveKey, ReadOwnerAutoSave() ? 1 : 0);
            PlayerPrefs.Save();
        }

        static bool IsMusic(AudioSource audio)
        {
            if (audio == null) return false;
            SDGAudioRoleV1 role = audio.GetComponent<SDGAudioRoleV1>();
            if (role != null) return role.category == SDGAudioRoleV1.Category.Music;
            string group = audio.outputAudioMixerGroup != null ? audio.outputAudioMixerGroup.name : "";
            string name = (audio.gameObject.name + " " + group).ToLowerInvariant();
            return name.Contains("music") || name.Contains("bgm") || name.Contains("soundtrack") ||
                name.Contains("背景音樂") || name.Contains("配樂");
        }
        void ApplySourceVolumes()
        {
            if (current == null) return;
            AudioSource[] found = FindObjectsOfType<AudioSource>(true);
            foreach (AudioSource audio in found)
            {
                if (audio == null) continue;
                int key = audio.GetInstanceID();
                SourceState track;
                if (!sources.TryGetValue(key, out track) || track.source != audio)
                {
                    track = new SourceState { source = audio, baseline = audio.volume, lastApplied = audio.volume,
                        lastFactor = 1f, music = IsMusic(audio) };
                    sources[key] = track;
                }
                float newFactor = track.music ? current.music : current.effects;
                // Preserve fades or audio adjustments made by gameplay scripts.
                if (Mathf.Abs(audio.volume - track.lastApplied) > .003f)
                {
                    if (track.lastFactor > .001f) track.baseline = Mathf.Clamp01(audio.volume / track.lastFactor);
                    else if (audio.volume > .003f) track.baseline = Mathf.Clamp01(audio.volume);
                }
                float target = Mathf.Clamp01(track.baseline * newFactor);
                if (Mathf.Abs(audio.volume - target) > .003f) audio.volume = target;
                track.lastApplied = audio.volume;
                track.lastFactor = newFactor;
            }
            List<int> dead = new List<int>();
            foreach (var pair in sources) if (pair.Value.source == null) dead.Add(pair.Key);
            foreach (int id in dead) sources.Remove(id);
        }
        void ApplyMouseSettings()
        {
            if (current == null) return;
            // Existing shared FPS input saves sensitivity in its own settings store.
            var input = SDGAdventure.SharedPlayer.PlayerInputService.Instance;
            if (input != null && input.Keybindings != null &&
                Mathf.Abs(input.Keybindings.Settings.mouseSensitivity - current.sensitivity) > .01f)
            {
                input.SetMouseSensitivity(current.sensitivity);
            }
            // Legacy mouse-look scripts in C3/Level04 are still supported.
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            foreach (MonoBehaviour mb in behaviours)
            {
                if (mb == null || !mb.gameObject.scene.IsValid()) continue;
                Type t = mb.GetType();
                if (t.Name != "CameraLook" && t.Name != "C3FirstPersonController") continue;
                // C3 already receives the shared sensitivity multiplier when sharedInput exists.
                // Scaling its private field as well would accidentally square the sensitivity.
                if (t.Name == "C3FirstPersonController")
                {
                    FieldInfo shared = t.GetField("sharedInput", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (shared != null && shared.GetValue(mb) != null) continue;
                }
                FieldInfo f = t.GetField("mouseSensitivity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f == null || f.FieldType != typeof(float)) continue;
                int key = mb.GetInstanceID();
                float original;
                if (!legacyMouseBases.TryGetValue(key, out original))
                {
                    original = (float)f.GetValue(mb);
                    legacyMouseBases[key] = original;
                }
                float desired = original * (current.sensitivity / 2f);
                if (Mathf.Abs((float)f.GetValue(mb) - desired) > .01f) f.SetValue(mb, desired);
            }
        }
    }
}
