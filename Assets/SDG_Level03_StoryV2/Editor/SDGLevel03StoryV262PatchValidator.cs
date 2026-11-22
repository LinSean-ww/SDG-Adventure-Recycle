// Compilation + static/resource checks for V2.6.2 candidate.
// Validation of Play Mode visual appearance must still be performed manually.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SDGAdventure.Level03StoryV2.Editor
{
    public static class SDGLevel03StoryV262PatchValidator
    {
        public static void Validate()
        {
            // If scripts failed compilation, Unity cannot execute this method.
            Type controllerType = typeof(SDGAdventure.Level03StoryV2.SDGLevel03StoryV2Controller);
            Type sparksType = typeof(SDGAdventure.Level03StoryV2.SDGLevel03V262EmbeddedSparks);
            if (!typeof(MonoBehaviour).IsAssignableFrom(controllerType) ||
                !typeof(MonoBehaviour).IsAssignableFrom(sparksType))
                throw new InvalidOperationException("V262_REPAIR_INVALID_TYPES");

            string controllerPath = Path.Combine(Application.dataPath,
                "SDG_Level03_StoryV2/SDGLevel03StoryV2Controller.cs");
            if (!File.Exists(controllerPath))
                throw new FileNotFoundException("V262_REPAIR_CONTROLLER_MISSING", controllerPath);

            string source = File.ReadAllText(controllerPath);
            string[] required = {
                "[SDG Story V2.6.2] TRACE_SCREEN_OPEN",
                "class SDGLevel03V262EmbeddedSparks",
                "shortCircuitFx=SDGLevel03V262EmbeddedSparks.Create(traceArea)",
                "if(shortCircuitFx)shortCircuitFx.Emit(at)",
                "photoImage.sprite=photoSprite",
                "tracePulse.anchoredPosition = smoothTrace[pulseIndex]",
                "if(distance>PathTolerance"
            };
            foreach (string fragment in required)
                if (!source.Contains(fragment))
                    throw new InvalidOperationException("V262_REPAIR_SOURCE_HOOK_MISSING: " + fragment);

            // Strict: an image-only fallback is not proof the requested reference photo exists.
            Texture2D reference = Resources.Load<Texture2D>("SDG_Level03_ElectricCable_Reference");
            if (reference == null)
                throw new InvalidOperationException("V262_REPAIR_PHOTO_RESOURCE_MISSING: Resources/SDG_Level03_ElectricCable_Reference must load as Texture2D");

            Debug.Log("V262_REPAIR_VALIDATION_PASS controller=" + controllerType.FullName +
                " sparks=" + sparksType.FullName +
                " photo=" + reference.width + "x" + reference.height +
                " note=EDIT_MODE_ONLY_PLAYMODE_NOT_VERIFIED");
        }
    }
}
