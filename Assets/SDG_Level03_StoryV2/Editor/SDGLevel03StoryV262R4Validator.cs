// V2.6.2 R4: Unity compilation + structural EditMode checks only.
// Real Play Mode motion/collision and screenshot inspection still require a human.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SDGAdventure.Level03StoryV2.Editor
{
    public static class SDGLevel03StoryV262R4Validator
    {
        public static void Validate()
        {
            Type controller = typeof(SDGAdventure.Level03StoryV2.SDGLevel03StoryV2Controller);
            Type sparks = typeof(SDGAdventure.Level03StoryV2.SDGLevel03V262EmbeddedSparks);
            Type dynamicCurrent = typeof(SDGAdventure.Level03StoryV2.SDGLevel03V4EnergyStream);
            Type renderer = typeof(SDGAdventure.Level03StoryV2.SDGLevel03V4TubeTexture);
            if(!typeof(MonoBehaviour).IsAssignableFrom(controller) ||
                !typeof(MonoBehaviour).IsAssignableFrom(sparks) ||
                !typeof(MonoBehaviour).IsAssignableFrom(dynamicCurrent))
                throw new InvalidOperationException("R4_COMPONENT_TYPE_MISSING");
            if(renderer.GetMethod("Build",BindingFlags.Public|BindingFlags.Static)==null)
                throw new InvalidOperationException("R4_TEXTURE_BUILDER_MISSING");
            if(controller.GetField("PatchVersion",BindingFlags.Public|BindingFlags.Static).GetRawConstantValue().ToString()!="V2.6.2-R4")
                throw new InvalidOperationException("R4_VERSION_MISMATCH");
            string path=Path.Combine(Application.dataPath,"SDG_Level03_StoryV2/SDGLevel03StoryV2Controller.cs");
            if(!File.Exists(path))throw new FileNotFoundException("R4_CONTROLLER_MISSING",path);
            string source=File.ReadAllText(path);
            string[] required={
                "smoothTrace=BuildR4Route()",
                "r4TubeTexture=SDGLevel03V4TubeTexture.Build(smoothTrace,900,675)",
                "SDGLevel03V4EnergyStream.Create(traceArea,smoothTrace,traceArcLengths)",
                "private static Vector2[] BuildR4Route()",
                "ClosestConnectedTracePoint(sample,localIndex",
                "Input.GetKeyDown(KeyCode.F9)",
                "if(shortCircuitFx)shortCircuitFx.Emit(at)",
                "public void SetPlayerState(bool active,float progress)",
                "[SDG Story V2.6.2 R4] TRACE_SCREEN_OPEN"
            };
            foreach(string hook in required)
                if(!source.Contains(hook))throw new InvalidOperationException("R4_HOOK_MISSING: "+hook);
            string[] forbidden={
                "SDG_Level03_ElectricCable_Reference",
                "SDGLevel03PhotoTracePath.Build",
                "V2.6.1 PHOTO SPRITE cable"
            };
            foreach(string obsolete in forbidden)
                if(source.Contains(obsolete))throw new InvalidOperationException("R4_OLD_ARTWORK_REMAINING: "+obsolete);
            MethodInfo build=controller.GetMethod("BuildR4Route",BindingFlags.NonPublic|BindingFlags.Static);
            if(build==null)throw new InvalidOperationException("R4_ROUTE_FACTORY_MISSING");
            Vector2[] pts=build.Invoke(null,null) as Vector2[];
            if(pts==null || pts.Length<100)throw new InvalidOperationException("R4_ROUTE_TOO_SHORT");
            float arcLength=0f;
            for(int i=1;i<pts.Length;i++)
            {
                float len=Vector2.Distance(pts[i],pts[i-1]);
                if(len<.0001f || len>30f)throw new InvalidOperationException("R4_ROUTE_DISCONTINUITY_AT_"+i);
                arcLength+=len;
            }
            Texture2D tube=null;
            try
            {
                tube=SDGAdventure.Level03StoryV2.SDGLevel03V4TubeTexture.Build(pts,900,675);
                Vector2 mid=pts[pts.Length/2];
                int sx=Mathf.RoundToInt(mid.x+449.5f);
                int sy=Mathf.RoundToInt(mid.y+337f);
                if(tube.GetPixel(sx,sy).a<.8f)
                    throw new InvalidOperationException("R4_PIPE_CENTER_PIXEL_TRANSPARENT");
                if(tube.GetPixel(0,0).a>.01f)
                    throw new InvalidOperationException("R4_PIPE_BACKGROUND_NOT_TRANSPARENT");
            }
            finally {if(tube!=null)UnityEngine.Object.DestroyImmediate(tube);}
            Debug.Log("V262_R4_VALIDATION_PASS samples="+pts.Length+" arc="+arcLength.ToString("F1")+
                " pipe=PROCEDURAL_UI_IMAGE no_reference_photo=true"+
                " note=EDIT_MODE_ONLY_NOT_PLAYMODE_NOT_VISUAL_VERIFIED");
        }
    }
}
