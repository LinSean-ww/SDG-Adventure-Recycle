using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SDGAdventure.FrontEnd
{
    /// <summary>
    /// Read-only scene ownership audit for official Level01..Level08.
    /// DOES NOT destroy, hide, rename or reparent gameplay objects. This is
    /// important because player-held weapons can intentionally be shared.
    /// </summary>
    public sealed class SDGLevelIsolationAuditR13 : MonoBehaviour
    {
        private static SDGLevelIsolationAuditR13 instance;
        private static string LogFile => Path.Combine(Application.persistentDataPath, "SDG_LevelIsolation_R13.log");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() { instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;
            var obj = new GameObject("SDG Level Isolation Audit R1.3 (read-only)");
            DontDestroyOnLoad(obj);
            instance = obj.AddComponent<SDGLevelIsolationAuditR13>();
        }

        private void OnEnable() { SceneManager.sceneLoaded += Loaded; }
        private void OnDisable() { SceneManager.sceneLoaded -= Loaded; }
        private void Start() { StartCoroutine(Inspect(SceneManager.GetActiveScene())); }
        private void Loaded(Scene scene, LoadSceneMode _) { StartCoroutine(Inspect(scene)); }

        private static bool IsLevel(Scene scene, out int n)
        {
            n = 0;
            if (!scene.IsValid() || !scene.isLoaded) return false;
            string path = scene.path.Replace('\\', '/');
            for (int i = 1; i <= 8; i++)
            {
                if (path.IndexOf("/02_Levels/Level0" + i + "/", StringComparison.OrdinalIgnoreCase) < 0) continue;
                n = i;
                return true;
            }
            return false;
        }

        private static bool Suspect(Transform t, int level, out string why)
        {
            why = null;
            string name = t.name;
            MonoBehaviour[] behaviours = t.GetComponents<MonoBehaviour>();
            for (int i = 1; i <= 8; i++)
            {
                if (i == level) continue;
                string foreign = "Level0" + i;
                if (name.IndexOf(foreign, StringComparison.OrdinalIgnoreCase) >= 0)
                    why = "foreign-object-name=" + foreign;
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour == null) continue;
                    Type type = behaviour.GetType();
                    string n = type.FullName ?? type.Name;
                    if (type.Name.IndexOf(foreign, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("." + foreign + ".", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        why = "foreign-component=" + n;
                        break;
                    }
                }
                if (why != null) return true;
            }
            // Extra signatures from the Level04 imported boss scene.
            if (level != 4 &&
                (name.IndexOf("ToiletMech", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("BossHealth", StringComparison.OrdinalIgnoreCase) >= 0))
                why = "possible-Level04-object";
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null) continue;
                string n = behaviour.GetType().Name;
                if (level != 4 && (n == "BossHealth" || n == "ToiletMechBossAI" ||
                                   n == "PlayerPlasmaGun" || n == "WeaponPickup"))
                {
                    why = "possible-Level04-component=" + n;
                    break;
                }
            }
            return why != null;
        }

        private static void Walk(Transform root, int level, List<string> problems, int depth)
        {
            if (root == null || depth > 45 || problems.Count >= 80) return;
            if (Suspect(root, level, out string why))
                problems.Add(why + " | " + root.name + " | scene=" + root.gameObject.scene.path +
                             " | active=" + root.gameObject.activeInHierarchy);
            foreach (Transform child in root) Walk(child, level, problems, depth + 1);
        }

        private IEnumerator Inspect(Scene scene)
        {
            if (!IsLevel(scene, out int level)) yield break;
            yield return null; // after first scene initialization frame
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            List<string> lines = new List<string>();
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects()) Walk(root.transform, level, lines, 0);
                // Check DontDestroyOnLoad for gameplay objects that escaped their level.
                Scene persistent = gameObject.scene;
                if (persistent.IsValid() && persistent.name == "DontDestroyOnLoad")
                    foreach (GameObject root in persistent.GetRootGameObjects())
                        Walk(root.transform, level, lines, 0);
                string report = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") +
                    " | Level0" + level + " | " + scene.path + " | suspects=" + lines.Count + "\n";
                foreach (string line in lines) report += "  " + line + "\n";
                if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 2 * 1024 * 1024)
                {
                    if (File.Exists(LogFile + ".previous")) File.Delete(LogFile + ".previous");
                    File.Move(LogFile, LogFile + ".previous");
                }
                File.AppendAllText(LogFile, report, new System.Text.UTF8Encoding(false));
                if (lines.Count > 0)
                    Debug.LogWarning("[SDG Level Isolation R1.3] Found " + lines.Count +
                                     " potentially foreign objects; diagnostic only: " + LogFile);
                else Debug.Log("[SDG Level Isolation R1.3] Level0" + level + " checked; no foreign foreign level or Boss component names found.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SDG Level Isolation R1.3] Audit failed: " + ex.Message);
            }
        }
    }
}
