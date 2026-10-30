using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SDGAdventure.UI;
using UnityEditor;
using UnityEngine;

public static class M6FontValidation
{
    private const string FontPath = "Assets/_Modules_/06_ExternalAssets/Resources/Fonts/NotoSansCJKtc-Regular.otf";
    private const string LicensePath = "Assets/StreamingAssets/FontLicenses/NotoSansCJK-OFL.txt";

    public static void Validate()
    {
        Font imported = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (imported == null) throw new Exception("Noto CJK font did not import as a Unity Font.");
        if (!imported.dynamic) throw new Exception("Noto CJK font must be dynamic for the full UI text set.");
        Font runtime = CjkGuiFont.Asset;
        if (runtime == null || runtime != imported) throw new Exception("Resources font load failed.");
        if (!File.Exists(LicensePath) || !File.ReadAllText(LicensePath).Contains("SIL OPEN FONT LICENSE Version 1.1"))
            throw new Exception("Bundled OFL license is missing.");

        string[] scripts =
        {
            "Assets/_Modules_/00_Core/ModuleSystem/ModuleCoordinator.cs",
            "Assets/_Modules_/02_Levels/Level03/Scripts/C3TreasureHud.cs",
            "Assets/_Modules_/02_Levels/Level03/Scripts/C3PlayerInteractor.cs",
            "Assets/_Modules_/02_Levels/Level03/Scripts/C3PuzzleConsole.cs",
            "Assets/_Modules_/02_Levels/Level03/Scripts/C3CompletionScreen.cs"
        };
        var glyphs = new HashSet<char>();
        foreach (string script in scripts)
        {
            string source = File.ReadAllText(script);
            if (!source.Contains("CjkGuiFont.Apply();"))
                throw new Exception("OnGUI script does not apply the bundled font: " + script);
            foreach (Match literal in Regex.Matches(source, "\"(?:\\\\.|[^\"\\\\])*\""))
                foreach (char character in literal.Value)
                    if (character >= 128 && !char.IsSurrogate(character)) glyphs.Add(character);
        }
        glyphs.Add('A');
        glyphs.Add('0');
        var missing = glyphs.Where(character => !runtime.HasCharacter(character)).ToArray();
        if (missing.Length != 0)
        {
            var detail = new StringBuilder();
            foreach (char character in missing)
                detail.Append(character).Append(" U+").Append(((int)character).ToString("X4")).Append("; ");
            throw new Exception("Font lacks UI characters: " + detail);
        }
        Debug.Log("M6_FONT_VALIDATE_PASS font=" + imported.name + " dynamic=" + imported.dynamic +
            " uniqueUICharacters=" + glyphs.Count + " missing=0 license=OFL-1.1");
    }
}
