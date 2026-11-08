using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace M7SharedPlayerTests
{
    public class Level01FontTests
    {
        [UnityTest]
        public IEnumerator HudTextUsesNotoAndCanAddNewCharacters()
        {
            var missingGlyphWarnings = new List<string>();
            void Capture(string message, string stack, LogType type)
            {
                if (message.Contains("was not found in the") && message.Contains("font asset"))
                    missingGlyphWarnings.Add(message);
            }
            Application.logMessageReceived += Capture;
            try
            {
                yield return SceneManager.LoadSceneAsync("Assets/_Modules_/02_Levels/Level01/Imported/Scenes/WTF.unity");
                yield return null;
                yield return null;
                var tmpType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", true);
                var fontType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro", true);
                var texts = Resources.FindObjectsOfTypeAll(tmpType).OfType<Component>()
                    .Where(x => x.gameObject.scene == SceneManager.GetActiveScene()).ToArray();
                Assert.AreEqual(3, texts.Length);
                var fontProperty = tmpType.GetProperty("font");
                var textProperty = tmpType.GetProperty("text");
                var primary = fontProperty.GetValue(texts[0]);
                foreach (var item in texts) Assert.AreSame(primary, fontProperty.GetValue(item));
                var fontName = ((UnityEngine.Object)primary).name;
                Assert.AreEqual("Level01 NotoSansCJKTC Dynamic", fontName);
                var fallbacks = (IList)fontType.GetProperty("fallbackFontAssetTable").GetValue(primary);
                Assert.AreEqual(0, fallbacks.Count, "Level01 must not fall back to an unlicensed font");
                var hasCharacter = fontType.GetMethod("HasCharacter", new[] { typeof(char), typeof(bool), typeof(bool) });
                const string samples = "掃描：滑鼠左鍵互動：F挖掘：滑鼠右鍵掃描冷卻完畢回收進度：0/10距離紀錄尚未進行掃描";
                foreach (char character in samples.Distinct())
                    Assert.IsTrue((bool)hasCharacter.Invoke(primary, new object[] { character, true, false }), "Missing " + character);
                Assert.IsTrue((bool)hasCharacter.Invoke(primary, new object[] { '繁', true, true }), "Dynamic Noto must add a new glyph");
                var tips = texts.Single(x => x.name == "tips");
                var rect = tips.GetComponent<RectTransform>();
                Assert.AreEqual(new Vector2(0.5f, 1f), rect.anchorMin);
                Assert.AreEqual(new Vector2(0.5f, 1f), rect.anchorMax);
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Assert.Greater(RectTransformUtility.WorldToScreenPoint(null, corners[0]).y, 70f,
                    "Top tips must not overlap the bottom quickbar at the current resolution");
                var originals = texts.Select(item => (string)textProperty.GetValue(item)).ToArray();
                var sampleTexts = new[] { "掃描：滑鼠右鍵　互動：F　挖掘：滑鼠左鍵繁", "掃描冷卻完畢", "回收進度：0/10\n距離紀錄\n尚未進行掃描" };
                for (int i = 0; i < texts.Length; i++)
                {
                    textProperty.SetValue(texts[i], sampleTexts[i]);
                    tmpType.GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) })
                        .Invoke(texts[i], new object[] { true, true });
                }
                yield return null;
                for (int i = 0; i < texts.Length; i++) textProperty.SetValue(texts[i], originals[i]);
                Assert.IsEmpty(missingGlyphWarnings, string.Join("\n", missingGlyphWarnings));
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }
        }
    }
}
