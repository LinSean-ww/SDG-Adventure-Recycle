using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.FrontEnd
{
    /// <summary>Small geometric UI icons, rendered as Unity meshes (no icon font / extra image needed).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SDGFrontEndIconGraphic : MaskableGraphic
    {
        public enum IconKind { Play, Continue, Flag, Settings, Credits, Power, Back, Level }
        [SerializeField] private IconKind kind;
        public IconKind Kind { get { return kind; } set { kind = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float r = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.42f;
            switch (kind)
            {
                case IconKind.Play:
                    Triangle(vh, new Vector2(-r * .50f, -r * .82f),
                        new Vector2(-r * .50f, r * .82f), new Vector2(r * .94f, 0));
                    break;
                case IconKind.Continue:
                    Line(vh, new Vector2(0, r), new Vector2(0, -r * .35f), r * .20f);
                    Triangle(vh, new Vector2(-r * .65f, -r * .21f),
                        new Vector2(r * .65f, -r * .21f), new Vector2(0, -r));
                    Line(vh, new Vector2(-r, -r), new Vector2(r, -r), r * .16f);
                    break;
                case IconKind.Flag:
                case IconKind.Level:
                    Line(vh, new Vector2(-r * .7f, -r), new Vector2(-r * .7f, r), r * .16f);
                    Triangle(vh, new Vector2(-r * .63f, r * .80f),
                        new Vector2(r * .82f, r * .34f), new Vector2(-r * .63f, -r * .08f));
                    break;
                case IconKind.Settings:
                    Ring(vh, Vector2.zero, r * .6f, r * .32f, 0, 360, 36);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Line(vh, dir * r * .62f, dir * r, r * .23f);
                    }
                    break;
                case IconKind.Credits:
                    Circle(vh, new Vector2(0, r * .36f), r * .34f, 20);
                    Circle(vh, new Vector2(-r * .75f, r * .04f), r * .25f, 18);
                    Circle(vh, new Vector2(r * .75f, r * .04f), r * .25f, 18);
                    Quad(vh, new Vector2(-r * .52f, -r * .88f), new Vector2(r * .52f, -r * .88f),
                        new Vector2(r * .43f, -r * .20f), new Vector2(-r * .43f, -r * .20f));
                    Quad(vh, new Vector2(-r, -r * .65f), new Vector2(-r * .59f, -r * .65f),
                        new Vector2(-r * .56f, -r * .23f), new Vector2(-r * .93f, -r * .23f));
                    Quad(vh, new Vector2(r * .59f, -r * .65f), new Vector2(r, -r * .65f),
                        new Vector2(r * .93f, -r * .23f), new Vector2(r * .56f, -r * .23f));
                    break;
                case IconKind.Power:
                    Ring(vh, Vector2.zero, r, r * .82f, 50, 310, 40);
                    Line(vh, new Vector2(0, r * 1.05f), new Vector2(0, -r * .06f), r * .2f);
                    break;
                case IconKind.Back:
                    Line(vh, new Vector2(r * .55f, 0), new Vector2(-r * .55f, 0), r * .19f);
                    Line(vh, new Vector2(-r * .55f, 0), new Vector2(0, r * .55f), r * .19f);
                    Line(vh, new Vector2(-r * .55f, 0), new Vector2(0, -r * .55f), r * .19f);
                    break;
            }
        }

        private int Vertex(VertexHelper vh, Vector2 p)
        {
            int index = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.position = p;
            v.color = color;
            vh.AddVert(v);
            return index;
        }
        private void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c)
        {
            int i = Vertex(vh, a); Vertex(vh, b); Vertex(vh, c);
            vh.AddTriangle(i, i + 1, i + 2);
        }
        private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int i = Vertex(vh, a); Vertex(vh, b); Vertex(vh, c); Vertex(vh, d);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
        private void Line(VertexHelper vh, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 v = (b - a).normalized;
            Vector2 n = new Vector2(-v.y, v.x) * thickness * .5f;
            Quad(vh, a - n, b - n, b + n, a + n);
        }
        private void Circle(VertexHelper vh, Vector2 center, float radius, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a1 = 2f * Mathf.PI * i / segments;
                float a2 = 2f * Mathf.PI * (i + 1) / segments;
                Triangle(vh, center,
                    center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius,
                    center + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * radius);
            }
        }
        private void Ring(VertexHelper vh, Vector2 center, float outer, float inner,
            float startDegrees, float endDegrees, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a1 = Mathf.Deg2Rad * Mathf.Lerp(startDegrees, endDegrees, (float)i / segments);
                float a2 = Mathf.Deg2Rad * Mathf.Lerp(startDegrees, endDegrees, (float)(i + 1) / segments);
                Vector2 p1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * outer;
                Vector2 p2 = center + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * outer;
                Vector2 p3 = center + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * inner;
                Vector2 p4 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * inner;
                Quad(vh, p1, p2, p3, p4);
            }
        }
    }
}
