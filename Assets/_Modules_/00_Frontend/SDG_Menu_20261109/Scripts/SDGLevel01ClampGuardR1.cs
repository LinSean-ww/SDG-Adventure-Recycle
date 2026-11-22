using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.FrontEnd
{
    /// <summary>
    /// Visual-only guard. Never changes Level01 interaction/pickup controllers.
    /// Disables legacy generated held tools (clamp/shovel) as requested previously,
    /// and removes any late-spawned SDG_HandHeld_Clamp mesh.
    /// </summary>
    public sealed class SDGLevel01ClampGuardR1 : MonoBehaviour
    {
        private static readonly string Level01 = "Assets/_Modules_/02_Levels/Level01/Imported/Scenes/WTF.unity";
        private float nextScan;
        private float stopAt;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { SceneManager.sceneLoaded -= SceneLoaded; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() { SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initial() { SceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single); }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.path.Replace('\\', '/') != Level01) return;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "SDG_Level01_NoClamp_R1") return;
            GameObject host = new GameObject("SDG_Level01_NoClamp_R1");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<SDGLevel01ClampGuardR1>();
        }
        private void Start() { stopAt = Time.unscaledTime + 12f; Scan(); }
        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 1.5f;
            Scan();
            if (Time.unscaledTime > stopAt) enabled = false;
        }
        private void Scan()
        {
            Scene scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != null && behaviour.GetType().Name == "SDGLevel01HeldTools" && behaviour.enabled)
                        behaviour.enabled = false;
                foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
                {
                    foreach (Transform node in cam.GetComponentsInChildren<Transform>(true))
                        if (node.name == "SDG_HandHeld_Clamp") Destroy(node.gameObject);
                }
            }
        }
    }
}
