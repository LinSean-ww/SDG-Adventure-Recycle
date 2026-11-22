using System;
using System.Reflection;
using UnityEngine;

namespace SDGAdventure.FrontEnd
{
    /// <summary>
    /// Lightweight adapter. Reads CurrentHealth/MaxHealth ONLY from a component on
    /// the local SharedFirstPersonController object. No guessed enemy HP or scene edits.
    /// Other gameplay controllers can call SDGFrontEndPlayerVitals.ReportHealth(...).
    /// </summary>
    public sealed class SDGFrontEndHealthSync : MonoBehaviour
    {
        private float nextPoll;
        private Component source;
        private PropertyInfo currentProperty;
        private PropertyInfo maximumProperty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("SDG FrontEnd - Player Health Sync");
            DontDestroyOnLoad(go);
            go.AddComponent<SDGFrontEndHealthSync>();
        }

        private void Update()
        {
            // During a slot load Level04PlayerHealth.Awake() initially has full HP.
            // Do not overwrite the saved HP before RestoreAfterSpawn applies it.
            if (SDGSaveProgressRuntimeV1.IsRestoringHealth) return;
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.4f;
            if (source == null || !source.gameObject.activeInHierarchy)
                FindPlayerHealth();
            if (source == null || currentProperty == null || maximumProperty == null) return;
            try
            {
                int current = Convert.ToInt32(currentProperty.GetValue(source, null));
                int max = Convert.ToInt32(maximumProperty.GetValue(source, null));
                SDGFrontEndPlayerVitals.ReportHealth(current, max);
            }
            catch (Exception)
            {
                source = null;
                currentProperty = null;
                maximumProperty = null;
            }
        }

        private void FindPlayerHealth()
        {
            source = null;
            currentProperty = null;
            maximumProperty = null;
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour candidate = behaviours[i];
                if (candidate == null || candidate.GetType().Name != "SharedFirstPersonController") continue;
                Component[] components = candidate.GetComponents<Component>();
                for (int j = 0; j < components.Length; j++)
                {
                    Component component = components[j];
                    if (component == null) continue;
                    Type type = component.GetType();
                    PropertyInfo hp = type.GetProperty("CurrentHealth", BindingFlags.Public | BindingFlags.Instance);
                    PropertyInfo max = type.GetProperty("MaxHealth", BindingFlags.Public | BindingFlags.Instance);
                    if (hp == null || max == null || !hp.CanRead || !max.CanRead) continue;
                    source = component;
                    currentProperty = hp;
                    maximumProperty = max;
                    return;
                }
            }
        }
    }
}
