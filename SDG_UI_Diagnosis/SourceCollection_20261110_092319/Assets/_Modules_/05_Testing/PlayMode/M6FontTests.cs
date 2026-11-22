using NUnit.Framework;
using UnityEngine;

namespace M6IntegrationTests
{
    public sealed class M6FontTests
    {
        [Test]
        public void BundledFontHasRepresentativeEnglishNumbersAndTraditionalChinese()
        {
            Font font = Resources.Load<Font>("Fonts/NotoSansCJKtc-Regular");
            Assert.IsNotNull(font, "Player must load the font from Resources, independent of system fonts.");
            foreach (char character in "SDG Adventure M6 2026 任務尋寶互動控制台解謎淨化設備通關返回重新開始責任消費與生產｜→／：，。")
                if (character != ' ') Assert.IsTrue(font.HasCharacter(character), "Missing glyph: " + character);
        }
    }
}
