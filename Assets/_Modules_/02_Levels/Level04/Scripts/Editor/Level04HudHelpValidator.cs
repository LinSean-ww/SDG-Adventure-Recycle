using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SDGAdventure.Level04.Editor
{
    public static class Level04HudHelpValidator
    {
        public static void Validate()
        {
            Type hud = typeof(SDGAdventure.Level04.Level04CombatHud);
            if (!typeof(MonoBehaviour).IsAssignableFrom(hud) ||
                hud.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance) == null)
                throw new InvalidOperationException("LEVEL04_HELP_HUD_TYPE_INVALID");

            string path = Path.Combine(Application.dataPath,
                "_Modules_/02_Levels/Level04/Scripts/Level04CombatHud.cs");
            if (!File.Exists(path))
                throw new FileNotFoundException("LEVEL04_HELP_HUD_SOURCE_MISSING", path);
            string src = File.ReadAllText(path);
            if (!src.Contains("LEVEL04_GAMEPLAY_HELP_V1") ||
                !src.Contains("Level04GameplayHelp") ||
                !src.Contains("階段 1｜機械 Boss 戰") ||
                !src.Contains("階段 2｜資源回收") ||
                !src.Contains("player.Damaged += OnDamaged"))
                throw new InvalidOperationException("LEVEL04_HELP_HUD_REQUIRED_HOOK_MISSING");
            if (src.Contains("CreateBar(root,\"PlayerHealth\"") ||
                src.Contains("CreateBar(root,\"BossHealth\""))
                throw new InvalidOperationException("LEVEL04_DUPLICATE_HEALTH_BAR_STILL_PRESENT");
            Debug.Log("LEVEL04_HELP_HUD_PATCH_PASS EDITMODE_ONLY; PLAYMODE_UNVERIFIED");
        }
    }
}
