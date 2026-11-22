using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.Level03StoryV2
{
    // UI sparks are intentionally built from standard Image/RawImage objects.
    // They remain visible in ScreenSpaceOverlay even when the factory is completely dark.
    // All 18 rays are pooled: failed attempts cause no GameObject allocation.
    public sealed class SDGLevel03ShortCircuitFX : MonoBehaviour
    {
        const int Count = 18;
        readonly RectTransform[] rays = new RectTransform[Count];
        readonly Image[] rayImages = new Image[Count];
        readonly Vector2[] velocities = new Vector2[Count];
        readonly Vector2[] locations = new Vector2[Count];
        readonly float[] spans = new float[Count];
        RawImage glow;
        Texture2D glowTexture;
        RectTransform glowRect;
        float startedAt = -10f;
        Vector2 origin;
        bool playing;

        public static SDGLevel03ShortCircuitFX Create(RectTransform parent)
        {
            GameObject root = new GameObject("V2.4 SHORT CIRCUIT - pooled electric sparks", typeof(RectTransform), typeof(SDGLevel03ShortCircuitFX));
            root.transform.SetParent(parent, false);
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = parent.rect.size;
            SDGLevel03ShortCircuitFX fx = root.GetComponent<SDGLevel03ShortCircuitFX>();
            fx.Build();
            return fx;
        }

        void Build()
        {
            GameObject flashObject = new GameObject("ShortCircuitRadialFlash", typeof(RectTransform), typeof(RawImage));
            flashObject.transform.SetParent(transform, false);
            glowRect = flashObject.GetComponent<RectTransform>();
            glowRect.anchorMin = glowRect.anchorMax = new Vector2(.5f, .5f);
            glowRect.sizeDelta = new Vector2(112f, 112f);
            glowRect.pivot = new Vector2(.5f, .5f);
            glow = flashObject.GetComponent<RawImage>();
            glow.raycastTarget = false;
            glowTexture = MakeGlow();
            glow.texture = glowTexture;
            glow.gameObject.SetActive(false);

            for (int i = 0; i < Count; i++)
            {
                GameObject child = new GameObject("Spark streak " + i, typeof(RectTransform), typeof(Image));
                child.transform.SetParent(transform, false);
                RectTransform rect = child.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(16f, 2.5f);
                rays[i] = rect;
                rayImages[i] = child.GetComponent<Image>();
                rayImages[i].raycastTarget = false;
                child.SetActive(false);
            }
        }

        static Texture2D MakeGlow()
        {
            const int n = 64;
            Color32[] data = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - 31.5f) / 31.5f;
                    float dy = (y - 31.5f) / 31.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 2f);
                    data[y * n + x] = new Color32(255, 113, 28, (byte)Mathf.RoundToInt(alpha * 235f));
                }
            Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.name = "SDG_Level03_V24_ShortCircuitGlow";
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            t.SetPixels32(data);
            t.Apply(false, true);
            return t;
        }

        public void ResetEffect()
        {
            playing = false;
            if (glow) glow.gameObject.SetActive(false);
            for (int i = 0; i < Count; i++) if (rays[i]) rays[i].gameObject.SetActive(false);
        }

        public void Emit(Vector2 at)
        {
            origin = at;
            startedAt = Time.unscaledTime;
            playing = true;
            if (glow)
            {
                glowRect.anchoredPosition = origin;
                glowRect.localScale = Vector3.one;
                glow.color = Color.white;
                glow.gameObject.SetActive(true);
            }
            for (int i = 0; i < Count; i++)
            {
                // Stable fan-like burst with small deterministic variation.
                float angle = i * Mathf.PI * 2f / Count + Mathf.Sin(i * 3.73f) * .14f;
                float speed = 155f + (i * 29 % 121);
                velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                locations[i] = origin;
                spans[i] = 11f + (i % 5) * 3f;
                rays[i].anchoredPosition = origin;
                rays[i].sizeDelta = new Vector2(spans[i], i % 3 == 0 ? 3.8f : 2.3f);
                rays[i].localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
                rayImages[i].color = i % 4 == 0 ? Color.white : (i % 2 == 0 ? new Color(1f, .93f, .24f) : new Color(1f, .42f, .12f));
                rays[i].gameObject.SetActive(true);
            }
        }

        void Update()
        {
            if (!playing) return;
            float age = Time.unscaledTime - startedAt;
            if (age >= .62f) { ResetEffect(); return; }
            float fade = 1f - age / .62f;
            if (glow)
            {
                glowRect.localScale = Vector3.one * (1f + age * 1.9f);
                glow.color = new Color(1f, 1f, 1f, fade);
            }
            for (int i = 0; i < Count; i++)
            {
                Vector2 p = origin + velocities[i] * age * (.94f - age * .25f);
                p.y -= age * age * 105f;
                locations[i] = p;
                rays[i].anchoredPosition = p;
                rays[i].sizeDelta = new Vector2(Mathf.Max(1f, spans[i] * fade), i % 3 == 0 ? 3f : 2f);
                Color c = rayImages[i].color;
                c.a = fade;
                rayImages[i].color = c;
            }
        }

        void OnDestroy()
        {
            if (glowTexture) Destroy(glowTexture);
        }
    }
}
