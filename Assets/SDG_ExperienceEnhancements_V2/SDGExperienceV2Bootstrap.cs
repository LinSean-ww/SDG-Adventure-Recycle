using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.ExperienceV2
{
    // Adds presentation-only effects and guidance. No existing scene or quest logic is replaced.
    public static class SDGExperienceV2Bootstrap
    {
        public const string Level01 = "Assets/_Modules_/02_Levels/Level01/Imported/Scenes/WTF.unity";
        public const string Level02 = "Assets/_Modules_/02_Levels/Level02/Imported/Scenes/SampleScene.unity";
        private const string MarkerName = "SDG_Experience_V2_DigAndStarNavigator";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCallbacks() { SceneManager.sceneLoaded -= OnLoaded; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() { SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Startup() { OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single); }

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            string path = scene.path.Replace('\\', '/');
            if (path != Level01 && path != Level02) return;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == MarkerName) return;
            GameObject host = new GameObject(MarkerName);
            SceneManager.MoveGameObjectToScene(host, scene);
            if (path == Level01) host.AddComponent<SDGLevel01ShovelVFX>();
            else host.AddComponent<SDGLevel02StarNavigator>();
        }
    }
}
