using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.Level03StoryV2
{
    public static class SDGLevel03StoryV2Bridge
    {
        public const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        public static SDGLevel03StoryV2Controller Instance { get; private set; }
        public static bool ExitUnlocked { get { return Instance != null && Instance.ExitUnlocked; } }
        public static bool TryInterceptCraft()
        {
            return Instance != null && Instance.TryInterceptCraft();
        }
        internal static void Bind(SDGLevel03StoryV2Controller value) { Instance = value; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Instance = null;
            C3TreasurePuzzle.C3StoryV2Hooks.ExitCheck = null;
            C3TreasurePuzzle.C3StoryV2Hooks.CraftCheck = null;
            SceneManager.sceneLoaded -= OnLoaded;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterLoad()
        {
            OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                !string.Equals(scene.path.Replace('\\', '/'), ScenePath, StringComparison.OrdinalIgnoreCase)) return;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "SDG_LEVEL03_STORY_V2") return;
            GameObject go = new GameObject("SDG_LEVEL03_STORY_V2");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<SDGLevel03StoryV2Controller>();
        }
    }
}
