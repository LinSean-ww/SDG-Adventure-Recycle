#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SDGAdventure.Level03StoryV2.Editor
{
    /// <summary>Only verifies installation and source wiring. Not a Game View visual test.</summary>
    public static class SDGLevel03StoryV261Validator
    {
        [MenuItem("SDG Adventure/Level03/Validate V2.6.1 Photo UI (Static)")]
        public static void Validate()
        {
            string root=Directory.GetParent(Application.dataPath).FullName;
            string report=Path.Combine(root,"SDG_Level03_StoryV2_6_1_Validation.txt");
            if(File.Exists(report))File.Delete(report);
            if(SDGLevel03StoryV2Controller.PatchVersion!="V2.6.1")
                throw new InvalidOperationException("V2.6.1 controller is not the compiled version.");
            string path="Assets/SDG_Level03_StoryV2/Resources/SDG_Level03_ElectricCable_Reference.png";
            TextureImporter importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Cable PNG TextureImporter is missing.");
            bool changed=false;
            if(importer.npotScale!=TextureImporterNPOTScale.None)
            {importer.npotScale=TextureImporterNPOTScale.None;changed=true;}
            if(importer.textureCompression!=TextureImporterCompression.Uncompressed)
            {importer.textureCompression=TextureImporterCompression.Uncompressed;changed=true;}
            if(importer.mipmapEnabled){importer.mipmapEnabled=false;changed=true;}
            if(importer.maxTextureSize<2048){importer.maxTextureSize=2048;changed=true;}
            if(changed)importer.SaveAndReimport();
            Texture2D asset=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Texture2D resource=Resources.Load<Texture2D>("SDG_Level03_ElectricCable_Reference");
            if(!asset || !resource || asset.width!=1448 || asset.height!=1086)
                throw new InvalidOperationException("Photo is missing, not loadable, or has incorrect imported dimensions.");
            string controller=File.ReadAllText(Path.Combine(Application.dataPath,
                "SDG_Level03_StoryV2/SDGLevel03StoryV2Controller.cs"));
            string[] required={
                "photoCanvas.overrideSorting=true;",
                "photoCanvas.sortingOrder=1100;",
                "typeof(Image)",
                "Sprite.Create(reference",
                "[SDG Story V2.6.1] TRACE_SCREEN_OPEN",
                "shortCircuitFx.Emit(at)"
            };
            foreach(string token in required)
                if(!controller.Contains(token))throw new InvalidOperationException("Controller render hook missing: "+token);
            Vector2[] pathPoints=SDGLevel03PhotoTracePath.Build(900,675);
            if(pathPoints.Length<200)throw new InvalidOperationException("Cable mouse path is incomplete.");
            string result="SDG LEVEL03 STORY V2.6.1 STATIC SUCCESS\n"+
                "PatchVersion=V2.6.1\n"+
                "Texture=1448x1086\n"+
                "Renderer=UnityEngine.UI.Image + Sprite.Create\n"+
                "NestedCanvasSortingOrder=1100\n"+
                "CursorPathPoints="+pathPoints.Length+"\n"+
                "CollisionSparks=retained\n"+
                "LIMITATION: NO PIXEL OR PLAYMODE RENDER VERIFICATION.\n"+
                "Check Unity Console for [SDG Story V2.6.1] TRACE_SCREEN_OPEN and send a Game View screenshot.\n";
            File.WriteAllText(report,result);
            Debug.Log(result);
        }
    }
}
#endif
