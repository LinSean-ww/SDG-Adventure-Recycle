using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.Experience
{
    /// <summary>
    /// Isolated additive enhancements. The original level logic and original scenes are not modified.
    /// The callback is registered before scenes load, so BossHealth.maxHealth is set before Start().
    /// </summary>
    public static class SDGExperienceBootstrap
    {
        public const string Level01Path = "Assets/_Modules_/02_Levels/Level01/Imported/Scenes/WTF.unity";
        public const string Level02Path = "Assets/_Modules_/02_Levels/Level02/Imported/Scenes/SampleScene.unity";
        public const string Level04Path = "Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity";
        public const int BossTargetHP = 30;
        private const string HostName = "SDG_ExperienceEnhancements_v1";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SceneManager.sceneLoaded -= OnLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HandleInitialScene()
        {
            OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            string path = scene.path.Replace('\\', '/');
            int level = path == Level01Path ? 1 : path == Level02Path ? 2 : path == Level04Path ? 4 : 0;
            if (level == 0) return;

            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == HostName) return;

            // SceneManager.sceneLoaded is sent after Awake/OnEnable but BEFORE Start.
            // BossHealth.Start() copies maxHealth into currentHealth.
            if (level == 4)
            {
                int changed = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (BossHealth boss in root.GetComponentsInChildren<BossHealth>(true))
                    {
                        if (boss == null || boss.IsDead) continue;
                        int oldMax = boss.maxHealth;
                        boss.maxHealth = Mathf.Max(oldMax, BossTargetHP);
                        if (boss.maxHealth != oldMax) changed++;
                    }
                Debug.Log("[SDG Experience] Level04 boss max health pre-Start updated: " + changed);
            }

            GameObject host = new GameObject(HostName);
            SceneManager.MoveGameObjectToScene(host, scene);
            var manager = host.AddComponent<SDGExperienceLevelRuntime>();
            manager.Configure(level);
        }
    }
}
