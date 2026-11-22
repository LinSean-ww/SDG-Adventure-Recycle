// V2.6.2 Bend R3: compile and source/resource checks only, NOT Play Mode testing.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SDGAdventure.Level03StoryV2.Editor
{
    public static class SDGLevel03StoryV262BendR3Validator
    {
        public static void Validate()
        {
            Type controller = typeof(SDGAdventure.Level03StoryV2.SDGLevel03StoryV2Controller);
            Type sparks = typeof(SDGAdventure.Level03StoryV2.SDGLevel03V262EmbeddedSparks);
            if (!typeof(MonoBehaviour).IsAssignableFrom(controller) ||
                !typeof(MonoBehaviour).IsAssignableFrom(sparks))
                throw new InvalidOperationException("BENDR3_TYPE_CHECK_FAILED");
            string path = Path.Combine(Application.dataPath,
                "SDG_Level03_StoryV2/SDGLevel03StoryV2Controller.cs");
            if(!File.Exists(path))throw new FileNotFoundException("BENDR3_CONTROLLER_MISSING",path);
            string source = File.ReadAllText(path);
            string[] required = {
                "private float[] traceArcLengths;",
                "ClosestConnectedTracePoint(sample,localIndex",
                "traceArcLengths[i]=traceArcLengths[i-1]",
                "Input.GetKeyDown(KeyCode.F9)",
                "traceDebugOverlay.SetActive(false);",
                "[SDG Story V2.6.2] TRACE_SCREEN_OPEN",
                "if(shortCircuitFx)shortCircuitFx.Emit(at)"
            };
            foreach(string hook in required)
                if(!source.Contains(hook))throw new InvalidOperationException("BENDR3_HOOK_MISSING: "+hook);
            Texture2D photo=Resources.Load<Texture2D>("SDG_Level03_ElectricCable_Reference");
            if(!photo)throw new InvalidOperationException("BENDR3_PHOTO_RESOURCE_MISSING");
            Debug.Log("V262_BENDR3_VALIDATION_PASS photo="+photo.width+"x"+photo.height+
                " note=EDIT_MODE_ONLY_NOT_PLAYMODE_NOT_VISUAL_VERIFIED");
        }
    }
}
