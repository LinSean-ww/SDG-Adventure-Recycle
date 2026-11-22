// V2.6.2 R4.1: compilation + structural EditMode validation. Not Play Mode.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SDGAdventure.Level03StoryV2.Editor
{
    public static class SDGLevel03StoryV262R41Validator
    {
        public static void Validate()
        {
            Type controller=typeof(SDGAdventure.Level03StoryV2.SDGLevel03StoryV2Controller);
            Type tube=typeof(SDGAdventure.Level03StoryV2.SDGLevel03V4TubeTexture);
            Type current=typeof(SDGAdventure.Level03StoryV2.SDGLevel03V4EnergyStream);
            Type sparks=typeof(SDGAdventure.Level03StoryV2.SDGLevel03V262EmbeddedSparks);
            if(!typeof(MonoBehaviour).IsAssignableFrom(controller) ||
               !typeof(MonoBehaviour).IsAssignableFrom(current) ||
               !typeof(MonoBehaviour).IsAssignableFrom(sparks) ||
               tube.GetMethod("Build",BindingFlags.Public|BindingFlags.Static)==null)
                throw new InvalidOperationException("R41_REQUIRED_COMPONENT_MISSING");
            var ver=controller.GetField("PatchVersion",BindingFlags.Public|BindingFlags.Static);
            if(ver==null || (string)ver.GetRawConstantValue()!="V2.6.2-R4.1")
                throw new InvalidOperationException("R41_VERSION_MISMATCH");
            string path=Path.Combine(Application.dataPath,"SDG_Level03_StoryV2/SDGLevel03StoryV2Controller.cs");
            if(!File.Exists(path))throw new FileNotFoundException("R41_CONTROLLER_MISSING",path);
            string src=File.ReadAllText(path);
            string[] required={
                "CalibrationWidths = {54f, 42f, 30f}",
                "CalibrationSpeeds = {.50f, .60f, .70f}",
                "CalibrationRoundCount = 3",
                "CalibrationMaxMisses = 3",
                "BuildCalibrationUI()",
                "calibrationRoot.SetActive(true)",
                "bool hit=Mathf.Abs(x-targetZone.anchoredPosition.x)<=targetZone.sizeDelta.x*.5f",
                "Input.GetMouseButtonDown(0)",
                "calibrationRound>=CalibrationRoundCount",
                "calibrationMisses>=CalibrationMaxMisses",
                "r4TubeTexture=SDGLevel03V4TubeTexture.Build(smoothTrace,900,675)",
                "SDGLevel03V4EnergyStream.Create(traceArea,smoothTrace,traceArcLengths)",
                "if(shortCircuitFx)shortCircuitFx.Emit(at)",
                "[SDG Story V2.6.2 R4] TRACE_SCREEN_OPEN"
            };
            foreach(string hook in required)
                if(!src.Contains(hook))throw new InvalidOperationException("R41_HOOK_MISSING: "+hook);
            if(src.Contains("SDG_Level03_ElectricCable_Reference"))
                throw new InvalidOperationException("R41_OLD_PHOTO_REFERENCE_PRESENT");
            BindingFlags nonPublicStatic=BindingFlags.NonPublic|BindingFlags.Static;
            float[] widths=controller.GetField("CalibrationWidths",nonPublicStatic).GetValue(null) as float[];
            float[] speeds=controller.GetField("CalibrationSpeeds",nonPublicStatic).GetValue(null) as float[];
            float[] centers=controller.GetField("CalibrationCenters",nonPublicStatic).GetValue(null) as float[];
            if(widths==null || speeds==null || centers==null || widths.Length!=3 ||
               speeds.Length!=3 || centers.Length!=3)
                throw new InvalidOperationException("R41_TIMING_TABLE_INVALID");
            const float rail=570f;
            for(int i=0;i<3;i++)
            {
                if(widths[i]<=0f || widths[i]>=rail*.12f || speeds[i]<=0f ||
                   Math.Abs(centers[i])+widths[i]*.5f>=rail*.5f)
                    throw new InvalidOperationException("R41_GOAL_OUT_OF_TRACK_AT_ROUND_"+i);
                if(i>0 && !(widths[i]<widths[i-1] && speeds[i]>speeds[i-1]))
                    throw new InvalidOperationException("R41_DIFFICULTY_NOT_INCREASING");
            }
            MethodInfo build=controller.GetMethod("BuildR4Route",nonPublicStatic);
            Vector2[] points=build.Invoke(null,null) as Vector2[];
            if(points==null || points.Length<100)throw new InvalidOperationException("R41_R4_ROUTE_BROKEN");
            Texture2D tex=null;
            try
            {
                tex=SDGAdventure.Level03StoryV2.SDGLevel03V4TubeTexture.Build(points,900,675);
                if(tex==null)throw new InvalidOperationException("R41_TUBE_MISSING");
                Vector2 mid=points[points.Length/2];
                int sx=Mathf.RoundToInt(mid.x+449.5f), sy=Mathf.RoundToInt(mid.y+337f);
                if(tex.GetPixel(sx,sy).a<.8f)
                    throw new InvalidOperationException("R41_TUBE_CENTER_TRANSPARENT");
            }
            finally{if(tex!=null)UnityEngine.Object.DestroyImmediate(tex);}
            Debug.Log("V262_R41_VALIDATION_PASS calibration=3_rounds targets=54,42,30px"+
                " speeds=.50,.60,.70 input=mouse_click_or_keyboard"+
                " tube=shared_curve note=EDIT_MODE_ONLY_NOT_PLAYMODE_VERIFIED");
        }
    }
}
