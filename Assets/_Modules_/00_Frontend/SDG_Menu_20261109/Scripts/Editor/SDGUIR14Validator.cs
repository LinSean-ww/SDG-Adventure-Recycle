#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SDGAdventure.FrontEnd.Editor
{
    public static class SDGUIR14Validator
    {
        public static void Validate()
        {
            string root = "Assets/_Modules_/00_Frontend/SDG_Menu_20261109/Scripts/";
            string menu = Path.Combine(root, "SDGMenuReworkR1.cs");
            string level2 = Path.Combine(root, "SDGLevel02HelpSequencerR14.cs");
            if (AssetDatabase.LoadAssetAtPath<MonoScript>(menu) == null ||
                AssetDatabase.LoadAssetAtPath<MonoScript>(level2) == null)
                throw new InvalidOperationException("R1.4 source scripts were not imported");
            string source = File.ReadAllText(menu);
            if (source.IndexOf("HideClonedTemplateIcons", StringComparison.Ordinal) < 0 ||
                source.IndexOf("Header rail", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("R1.4 menu source modifications missing");
            if (typeof(SDGLevel02HelpSequencerR14) == null)
                throw new InvalidOperationException("R1.4 Level02 helper was not compiled");
            Debug.Log("SDG_UI_R14_VALIDATION_PASS");
        }
    }
}
#endif
