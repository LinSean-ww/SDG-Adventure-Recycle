using System;
using System.Collections.Generic;

namespace SDGAdventure.ModuleSystem
{
    public static class ModuleRegistry
    {
        private static readonly Dictionary<string, ModuleDefinition> modules = new Dictionary<string, ModuleDefinition>(StringComparer.Ordinal);
        public static IEnumerable<ModuleDefinition> All => modules.Values;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode() => modules.Clear();

        public static bool TryRegister(ModuleDefinition definition, out string error)
        {
            if (definition == null) { error = "Module definition is null."; return false; }
            if (!definition.IsValid(out error)) return false;
            if (modules.ContainsKey(definition.Id)) { error = "Duplicate module ID: " + definition.Id; return false; }
            if (ScenePathRegistered(definition.ScenePath)) { error = "Scene already registered: " + definition.ScenePath; return false; }
            modules.Add(definition.Id, definition);
            return true;
        }

        private static bool ScenePathRegistered(string path)
        {
            foreach (ModuleDefinition module in modules.Values)
                if (string.Equals(module.ScenePath, path, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static bool TryGet(string id, out ModuleDefinition definition) =>
            modules.TryGetValue(id ?? string.Empty, out definition);

        public static bool TryGetByScene(string path, out ModuleDefinition definition)
        {
            foreach (ModuleDefinition item in modules.Values)
                if (string.Equals(item.ScenePath, path, StringComparison.OrdinalIgnoreCase))
                { definition = item; return true; }
            definition = null;
            return false;
        }
    }
}
