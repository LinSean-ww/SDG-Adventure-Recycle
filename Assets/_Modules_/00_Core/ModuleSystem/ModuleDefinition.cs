using System;
using UnityEngine;

namespace SDGAdventure.ModuleSystem
{
    public sealed class ModuleDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string ScenePath { get; }
        public Type AdapterType { get; }

        public ModuleDefinition(string id, string displayName, string scenePath, Type adapterType)
        {
            Id = id;
            DisplayName = displayName;
            ScenePath = scenePath;
            AdapterType = adapterType;
        }

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(Id) || !System.Text.RegularExpressions.Regex.IsMatch(Id, "^[a-z][a-z0-9_]*$"))
                error = "Module ID must use lowercase ASCII letters, digits and underscores, starting with a letter.";
            else if (string.IsNullOrWhiteSpace(DisplayName)) error = "Display name is missing.";
            else if (string.IsNullOrWhiteSpace(ScenePath) || !ScenePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                     !ScenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) error = "Unity scene path is invalid.";
            else if (AdapterType == null || !typeof(MonoBehaviour).IsAssignableFrom(AdapterType) ||
                     !typeof(IModuleAdapter).IsAssignableFrom(AdapterType) || AdapterType.IsAbstract)
                error = "Module adapter is missing or invalid.";
            else { error = null; return true; }
            return false;
        }
    }

    public struct ModuleResult
    {
        public string ModuleId;
        public bool Completed;
        public int CollectedParts;
        public int PuzzleAttempts;
        public float PlaySeconds;
    }

    public interface IModuleAdapter
    {
        string ModuleId { get; }
        bool Initialize(ModuleCoordinator coordinator);
        void Shutdown();
    }
}
