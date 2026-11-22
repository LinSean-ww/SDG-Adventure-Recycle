using UnityEngine;

namespace SDGAdventure.Level03StoryV2
{
    // V2.4: CPU-bake exactly the same smooth centreline used by cursor collision.
    // RawImage renders using Unity's standard UI shader, like the visible start/end images.
    // One 720x260 texture is created once; NO per-frame pixel writes or line GameObjects.
    public static class SDGLevel03TraceBakedTexture
    {
        public static Texture2D Build(Vector2[] points, int width, int height)
        {
            if (points == null || points.Length < 2)
                throw new System.ArgumentException("Cable path must have at least 2 points.");
            if (width < 64 || height < 64)
                throw new System.ArgumentOutOfRangeException("width", "Cable texture is too small.");

            int count = width * height;
            float[] distanceSq = new float[count];
            float[] along = new float[count];
            for (int i = 0; i < count; i++) distanceSq[i] = float.MaxValue;
            float xOffset = width * .5f - .5f;
            float yOffset = height * .5f - .5f;
            float travelled = 0f;
            const float influence = 47f;

            for (int segment = 0; segment < points.Length - 1; segment++)
            {
                Vector2 a = points[segment] + new Vector2(xOffset, yOffset);
                Vector2 b = points[segment + 1] + new Vector2(xOffset, yOffset);
                Vector2 delta = b - a;
                float segmentSq = Mathf.Max(.0001f, delta.sqrMagnitude);
                float segmentLength = Mathf.Sqrt(segmentSq);
                int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x) - influence), 0, width - 1);
                int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x) + influence), 0, width - 1);
                int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y) - influence), 0, height - 1);
                int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y) + influence), 0, height - 1);

                for (int y = minY; y <= maxY; y++)
                {
                    int row = y * width;
                    for (int x = minX; x <= maxX; x++)
                    {
                        float t = Mathf.Clamp01(((x - a.x) * delta.x + (y - a.y) * delta.y) / segmentSq);
                        float dx = x - a.x - delta.x * t;
                        float dy = y - a.y - delta.y * t;
                        float ds = dx * dx + dy * dy;
                        int index = row + x;
                        if (ds >= distanceSq[index]) continue;
                        distanceSq[index] = ds;
                        along[index] = travelled + t * segmentLength;
                    }
                }
                travelled += segmentLength;
            }

            Color32[] colors = new Color32[count];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    // Solid background makes missing texture or alpha-mask issues obvious.
                    Color32 c = ((x % 32 == 0) || (y % 32 == 0))
                        ? new Color32(12, 48, 55, 255)
                        : new Color32(5, 28, 38, 255);
                    float d2 = distanceSq[i];
                    if (d2 <= 47f * 47f)
                    {
                        float d = Mathf.Sqrt(d2);
                        if (d < .9f && Mathf.Repeat(along[i], 29f) < 16f)
                            c = new Color32(174, 255, 235, 255); // segmented guide wire
                        else if (d < 1.8f)
                            c = new Color32(60, 185, 199, 255);
                        else if (d < 12.5f)
                            c = new Color32(16, 72, 81, 255); // clearly navigable interior
                        else if (d < 15.9f)
                            c = new Color32(35, 180, 182, 255); // inner safe rail
                        else if (d < 17f)
                            c = new Color32(143, 229, 221, 255);
                        else if (d < 21f)
                            c = new Color32(246, 78, 80, 255); // red collision wall
                        else if (d < 24f)
                            c = new Color32(182, 39, 56, 255);
                        else if (d < 29f)
                            c = new Color32(105, 30, 48, 255);
                        else if (d < 35f)
                            c = new Color32(43, 29, 43, 255); // low-cost rim glow
                    }
                    colors[i] = c;
                }
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "SDG_Level03_V24_VisibleCable_720x260";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(colors);
            texture.Apply(false, false); // Leave readable for editor pixel-level visibility validation (~0.7 MiB).
            return texture;
        }
    }
}
