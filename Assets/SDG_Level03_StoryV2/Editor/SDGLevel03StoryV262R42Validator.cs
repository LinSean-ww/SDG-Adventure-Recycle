// V2.6.2 R4.2 EditMode structural validator. It does not certify Play Mode fidelity.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SDGAdventure.Level03StoryV2.Editor
{
    public static class SDGLevel03StoryV262R42Validator
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
                throw new InvalidOperationException("R42_REQUIRED_COMPONENT_MISSING");
            FieldInfo ver=controller.GetField("PatchVersion",BindingFlags.Public|BindingFlags.Static);
            if(ver==null || (string)ver.GetRawConstantValue()!="V2.6.2-R4.2")
                throw new InvalidOperationException("R42_VERSION_MISMATCH");
            string root=Path.Combine(Application.dataPath,"SDG_Level03_StoryV2");
            string source=Path.Combine(root,"SDGLevel03StoryV2Controller.cs");
            if(!File.Exists(source)) throw new FileNotFoundException("R42_CONTROLLER_MISSING",source);
            string src=File.ReadAllText(source);
            string[] required={
                "private const float RailWidth = 1070f",
                "CalibrationWidths = {74f, 55f, 38f}",
                "CalibrationSpeeds = {.34f, .42f, .49f}",
                "CalibrationRoundCount = 3",
                "CalibrationMaxMisses = 3",
                "new Vector2(1480f,834f)",
                "SDG_R42_ReactionPanel",
                "SDG_R42_MovingPointer",
                "R4.2 Live target: visible rect equals hit rect",
                "R4.2 real moving cyan arrow",
                "bool hit=Mathf.Abs(x-targetZone.anchoredPosition.x)<=targetZone.sizeDelta.x*.5f",
                "Input.GetMouseButtonDown(0)",
                "calibrationRound>=CalibrationRoundCount",
                "calibrationMisses>=CalibrationMaxMisses",
                "r4TubeTexture=SDGLevel03V4TubeTexture.Build(smoothTrace,900,675)",
                "SDGLevel03V4EnergyStream.Create(traceArea,smoothTrace,traceArcLengths)",
                "if(shortCircuitFx)shortCircuitFx.Emit(at)"
            };
            foreach(string hook in required)
                if(!src.Contains(hook))throw new InvalidOperationException("R42_HOOK_MISSING: "+hook);
            if(src.Contains("SDG_Level03_ElectricCable_Reference"))
                throw new InvalidOperationException("R42_OLD_PHOTO_REFERENCE_PRESENT");
            BindingFlags nonPublicStatic=BindingFlags.NonPublic|BindingFlags.Static;
            float[] widths=controller.GetField("CalibrationWidths",nonPublicStatic).GetValue(null) as float[];
            float[] speeds=controller.GetField("CalibrationSpeeds",nonPublicStatic).GetValue(null) as float[];
            float[] centers=controller.GetField("CalibrationCenters",nonPublicStatic).GetValue(null) as float[];
            if(widths==null || speeds==null || centers==null || widths.Length!=3 ||
               speeds.Length!=3 || centers.Length!=3)
                throw new InvalidOperationException("R42_TIMING_TABLE_INVALID");
            const float rail=1070f;
            for(int i=0;i<3;i++)
            {
                if(widths[i]<=0f || widths[i]>=rail*.09f || speeds[i]<=0f ||
                   Math.Abs(centers[i])+widths[i]*.5f>=rail*.5f)
                    throw new InvalidOperationException("R42_GOAL_OUT_OF_TRACK_AT_ROUND_"+i);
                if(i>0 && !(widths[i]<widths[i-1] && speeds[i]>speeds[i-1]))
                    throw new InvalidOperationException("R42_DIFFICULTY_NOT_INCREASING");
            }
            CheckTexture(Path.Combine(root,"Resources/SDG_R42_ReactionPanel.png"),1480,834);
            CheckTexture(Path.Combine(root,"Resources/SDG_R42_MovingPointer.png"),216,112);
            if(!Resources.Load<Texture2D>("SDG_R42_ReactionPanel") ||
               !Resources.Load<Texture2D>("SDG_R42_MovingPointer"))
                throw new InvalidOperationException("R42_UNITY_RESOURCE_IMPORT_FAILED");
            MethodInfo route=controller.GetMethod("BuildR4Route",nonPublicStatic);
            Vector2[] points=route.Invoke(null,null) as Vector2[];
            if(points==null || points.Length<100)
                throw new InvalidOperationException("R42_R4_ROUTE_BROKEN");
            Texture2D tex=null;
            try
            {
                tex=SDGAdventure.Level03StoryV2.SDGLevel03V4TubeTexture.Build(points,900,675);
                if(tex==null)throw new InvalidOperationException("R42_TUBE_MISSING");
                Vector2 mid=points[points.Length/2];
                int sx=Mathf.RoundToInt(mid.x+449.5f), sy=Mathf.RoundToInt(mid.y+337f);
                if(tex.GetPixel(sx,sy).a<.8f)
                    throw new InvalidOperationException("R42_TUBE_CENTER_TRANSPARENT");
            }
            finally{if(tex!=null)UnityEngine.Object.DestroyImmediate(tex);}
            Debug.Log("V262_R42_VALIDATION_PASS hud=1480x834 art=decorative_only"+
                " dynamic_pointer=true dynamic_goal=true goal_widths=74,55,38"+
                " rail=1070 live_hit_area=true rounds=3 source=R4_or_R41"+
                " tube=shared_curve EDIT_MODE_ONLY_NOT_PLAYMODE_VERIFIED");
        }
        static void CheckTexture(string absolutePath,int expectedWidth,int expectedHeight)
        {
            if(!File.Exists(absolutePath))throw new FileNotFoundException("R42_IMAGE_MISSING",absolutePath);
            Texture2D t=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!t.LoadImage(File.ReadAllBytes(absolutePath)))
                    throw new InvalidOperationException("R42_IMAGE_DECODE_FAILED: "+absolutePath);
                if(t.width!=expectedWidth || t.height!=expectedHeight)
                    throw new InvalidOperationException("R42_IMAGE_DIMENSIONS_INVALID: "+absolutePath);
            }
            finally{UnityEngine.Object.DestroyImmediate(t);}
        }
    }
}
