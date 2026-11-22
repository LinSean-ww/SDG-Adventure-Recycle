// Unity 2023.1.22f1 EditMode compile and asset validator for R5.
// This validator deliberately does NOT claim PlayMode drag or visual acceptance.
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SDGAdventure.Level03StoryV2.Editor
{
    public static class SDGLevel03StoryV262R5Validator
    {
        public static void Validate()
        {
            Type controller=typeof(SDGLevel03StoryV2Controller);
            Type sortGame=typeof(SDGLevel03R5SortingEnergyGame);
            Type dragging=typeof(SDGLevel03R5TrashDraggable);
            if(!typeof(MonoBehaviour).IsAssignableFrom(sortGame) ||
               !typeof(UnityEngine.EventSystems.IBeginDragHandler).IsAssignableFrom(dragging) ||
               !typeof(UnityEngine.EventSystems.IDragHandler).IsAssignableFrom(dragging) ||
               !typeof(UnityEngine.EventSystems.IEndDragHandler).IsAssignableFrom(dragging))
                throw new InvalidOperationException("R5_DRAG_COMPONENT_MISSING");
            FieldInfo version=controller.GetField("PatchVersion",BindingFlags.Public|BindingFlags.Static);
            if(version==null || (string)version.GetRawConstantValue()!="V2.6.2-R5.0")
                throw new InvalidOperationException("R5_CONTROLLER_VERSION_MISMATCH");
            FieldInfo gameVersion=sortGame.GetField("GameVersion",BindingFlags.Public|BindingFlags.Static);
            if(gameVersion==null || (string)gameVersion.GetRawConstantValue()!="V2.6.2-R5.0")
                throw new InvalidOperationException("R5_GAME_VERSION_MISMATCH");
            if(SDGLevel03R5SortingEnergyGame.TotalItems!=20 ||
               SDGLevel03R5SortingEnergyGame.ItemsPerWave!=4 ||
               SDGLevel03R5SortingEnergyGame.WaveCount!=5 ||
               SDGLevel03R5SortingEnergyGame.PowerLimit!=100 ||
               SDGLevel03R5SortingEnergyGame.RequiredPower!=90)
                throw new InvalidOperationException("R5_RULES_OUT_OF_SYNC");
            string project=Path.Combine(Application.dataPath,"SDG_Level03_StoryV2");
            string source=File.ReadAllText(Path.Combine(project,"SDGLevel03StoryV2Controller.cs"));
            string mini=File.ReadAllText(Path.Combine(project,"SDGLevel03R5SortingEnergyGame.cs"));
            string[] hooks={
                "sortingEnergy.Setup(panelRect,CompleteSortingEnergy)",
                "sortingEnergy.Begin()",
                "private void CompleteSortingEnergy()",
                "gridComplete=true;",
                "screen=Screen.Allocation;ResetElements();ShowAllocation()",
                "case Quest.Production:",
                "r4TubeTexture=SDGLevel03V4TubeTexture.Build(smoothTrace,900,675)",
                "SDGLevel03V4EnergyStream.Create(traceArea,smoothTrace,traceArcLengths)"
            };
            foreach(string hook in hooks)
                if(!source.Contains(hook))throw new InvalidOperationException("R5_CONTROLLER_HOOK_MISSING: "+hook);
            string[] gameplay={
                "IBeginDragHandler,IDragHandler,IEndDragHandler",
                "RectTransformUtility.RectangleContainsScreenPoint(bins[i],screenPosition,null)",
                "found==ItemBins[shuffledItems[processed]]",
                "power=Mathf.Min(PowerLimit,power+6+extra)",
                "timeRemaining=Mathf.Max(0f,timeRemaining-dt)",
                "if(!loadSelected[0]||!loadSelected[1]||!loadSelected[2])",
                "if(used>PowerLimit || used>power)",
                "completed();",
                "phase=Phase.Won",
                "private void ToggleLoad(int row)"
            };
            foreach(string hook in gameplay)
                if(!mini.Contains(hook))throw new InvalidOperationException("R5_GAME_HOOK_MISSING: "+hook);
            if(source.Contains("SetAction(\"確認供電配置\",ConfirmAllocation)") ||
               source.Contains("MakeButton(\"Load selector \"+i"))
                throw new InvalidOperationException("R5_OLD_TEXT_BUTTON_GAME_STILL_ACTIVE");
            BindingFlags binding=BindingFlags.NonPublic|BindingFlags.Static;
            string[] artwork=sortGame.GetField("ItemArt",binding).GetValue(null) as string[];
            string[] names=sortGame.GetField("ItemNames",binding).GetValue(null) as string[];
            int[] ids=sortGame.GetField("ItemBins",binding).GetValue(null) as int[];
            int[] watts=sortGame.GetField("Loads",binding).GetValue(null) as int[];
            if(artwork==null || names==null || ids==null || watts==null ||
                artwork.Length!=20 || names.Length!=20 || ids.Length!=20 || watts.Length!=4 ||
                watts[0]+watts[1]+watts[2]!=90 || watts[0]+watts[1]+watts[2]+watts[3]<=100)
                throw new InvalidOperationException("R5_DATA_TABLE_INVALID");
            int[] counts=new int[4];
            for(int i=0;i<ids.Length;i++)
            {
                if(ids[i]<0 || ids[i]>=4)throw new InvalidOperationException("R5_CATEGORY_OOB_AT_"+i);
                counts[ids[i]]++;
            }
            for(int i=0;i<4;i++)if(counts[i]<3)throw new InvalidOperationException("R5_CATEGORY_TOO_RARE_"+i);
            string[] required={
                "SDG_R5_Panel","SDG_R5_Belt",
                "SDG_R5_Bin_Plastic","SDG_R5_Bin_Metal",
                "SDG_R5_Bin_Electronics","SDG_R5_Bin_General",
                "SDG_R5_Item_Bottle","SDG_R5_Item_Detergent","SDG_R5_Item_Cup",
                "SDG_R5_Item_Can","SDG_R5_Item_Tin","SDG_R5_Item_Scrap",
                "SDG_R5_Item_PCB","SDG_R5_Item_Battery","SDG_R5_Item_Phone",
                "SDG_R5_Item_Bag","SDG_R5_Item_Tissue","SDG_R5_Item_Wrapper"
            };
            foreach(string asset in required)
            {
                string file=Path.Combine(project,"Resources/"+asset+".png");
                if(!File.Exists(file))throw new FileNotFoundException("R5_ART_FILE_MISSING",file);
                Texture2D loaded=Resources.Load<Texture2D>(asset);
                if(!loaded || loaded.width<128 || loaded.height<128)
                    throw new InvalidOperationException("R5_UNITY_TEXTURE_IMPORT_FAILED: "+asset);
                Sprite sprite=Sprite.Create(loaded,new Rect(0,0,loaded.width,loaded.height),
                    new Vector2(.5f,.5f),100f,0,SpriteMeshType.FullRect);
                if(!sprite)throw new InvalidOperationException("R5_SPRITE_FAILED: "+asset);
                UnityEngine.Object.DestroyImmediate(sprite);
            }
            Debug.Log("V262_R5_VALIDATION_PASS game=real_sorting items=20 waves=5 bins=4"+
                " drag=UnityEventSystem energy=live loads=35,30,25,20"+
                " supply_requires=90 shared_R4_tube=preserved EDITMODE_ONLY");
        }
    }
}
