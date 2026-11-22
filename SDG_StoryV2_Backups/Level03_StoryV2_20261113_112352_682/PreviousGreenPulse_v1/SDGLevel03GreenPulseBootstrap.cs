using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.Level03GreenPulse
{
    // Additive Level03-only feature. Does not change any existing scene or quest scripts.
    public static class SDGLevel03GreenPulseBootstrap
    {
        public const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private const string Marker = "SDG_LEVEL03_GREEN_PULSE_V1";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterLoad()
        {
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                !string.Equals(scene.path.Replace('\\', '/'), ScenePath, System.StringComparison.OrdinalIgnoreCase))
                return;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == Marker) return;
            var host = new GameObject(Marker);
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<SDGLevel03GreenPulseChallenge>();
        }
    }
}
