using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace M6IntegrationTests
{
    public sealed class C3V11TaskLayoutTests
    {
        private const BindingFlags StaticPrivate = BindingFlags.NonPublic | BindingFlags.Static;
        private static Type ViewType => Type.GetType("C3TreasurePuzzle.C3UiView, Assembly-CSharp", true);

        [TestCase(640, 360)]
        [TestCase(866, 838)]
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        public void TaskSheetFitsTheGameView(int width, int height)
        {
            MethodInfo method = ViewType.GetMethod("CalculateTaskPanelSize", StaticPrivate);
            Assert.IsNotNull(method);
            Vector2 size = (Vector2)method.Invoke(null, new object[] { width, height });
            float scale = Mathf.Clamp(Mathf.Min(width / 1920f, height / 1080f), 0.8f, 1.2f);
            Assert.Greater(size.x, 0f);
            Assert.Greater(size.y, 0f);
            Assert.LessOrEqual(size.x + 32f * scale, width + 0.1f);
            Assert.LessOrEqual(size.y + 32f * scale, height + 0.1f);
            if (width <= 866) Assert.Greater(size.x, width * 0.44f, "Small views need more than the old 44% width.");
        }

        [Test]
        public void LongWrappedTraditionalChineseTaskExpandsItsLayoutRow()
        {
            Font font = Resources.Load<Font>("Fonts/NotoSansCJKtc-Regular");
            Assert.IsNotNull(font);
            GameObject row = new GameObject("TaskRowProbe", typeof(RectTransform), typeof(LayoutElement));
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(row.transform, false);
            try
            {
                Text label = labelObject.GetComponent<Text>();
                label.font = font;
                label.fontSize = 15;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.text = "前往 D 區出口：進行中\n輸送帶已開始分類，請步行前往南側 D 區出口。";
                MethodInfo method = ViewType.GetMethod("FitTaskBlock", StaticPrivate);
                Assert.IsNotNull(method);
                method.Invoke(null, new object[] { label, 140f, 20f, 48f });
                Assert.Greater(row.GetComponent<LayoutElement>().preferredHeight, 60f,
                    "Wrapped Chinese text must expand instead of overflowing into the next row.");
            }
            finally { UnityEngine.Object.DestroyImmediate(row); }
        }
    }
}
