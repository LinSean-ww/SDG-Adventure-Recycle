using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Level01FontSetup
{
    private const string FontPath = "Assets/_Modules_/02_Levels/Level01/Fonts/Level01 NotoSansCJKTC Dynamic.asset";
    private const string SourcePath = "Assets/_Modules_/01_Shared/Player/Resources/SharedPlayer/Fonts/NotoSansCJKtc-Regular.otf";
    private const string Samples = "掃描：滑鼠右鍵　互動：F　挖掘：滑鼠左鍵掃描冷卻完畢回收進度：0/10距離紀錄尚未進行掃描" +
        "冷卻中秒掃描中已經找完去門口旁的機器互動距離小於可按挖掘ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789:./+-_()";

    public static void Verify()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
        if (font == null || source == null || font.sourceFontFile != source ||
            font.atlasPopulationMode != AtlasPopulationMode.Dynamic || !font.isMultiAtlasTexturesEnabled ||
            (font.fallbackFontAssetTable != null && font.fallbackFontAssetTable.Count != 0))
            throw new InvalidOperationException("Level01 Noto font configuration is incomplete.");
        if (!font.HasCharacters(Samples, out uint[] missing, false, true))
            throw new InvalidOperationException("Level01 sample glyphs are missing: " + string.Join(", ", missing.Select(x => "U+" + x.ToString("X4"))));
        var scene = EditorSceneManager.OpenScene(Level01ModuleAdapter.ScenePath, OpenSceneMode.Single);
        var texts = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<TMP_Text>(true)).ToArray();
        if (texts.Length == 0) throw new InvalidOperationException("No TMP text found in the Level01 scene.");
        foreach (var text in texts)
        {
            if (text.font != font || text.fontSharedMaterial != font.material)
                throw new InvalidOperationException("Unexpected font or material on " + text.name);
            Debug.Log("LEVEL01_TMP_TEXT " + text.name + " font=" + text.font.name);
        }
        Debug.Log("LEVEL01_FONT_VERIFY_OK texts=" + texts.Length + " dynamic=true samples=ok");
    }
}
