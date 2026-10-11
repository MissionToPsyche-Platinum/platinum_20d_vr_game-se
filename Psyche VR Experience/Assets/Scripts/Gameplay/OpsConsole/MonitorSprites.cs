using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Small round sprites made once at runtime (white, tinted per Image) for the monitor's animated views:
    /// a soft glow, a solid disc and a thin ring.
    /// </summary>
    public static class MonitorSprites
    {
        private const int Size = 64;
        private const float EdgeSoftness = 2f;       // texels of anti-aliasing on the disc and ring edges
        private const float RingWidth = 0.14f;       // share of the radius

        private static Sprite _glow, _disc, _ring;

        /// <summary>White at the centre fading to clear at the edge.</summary>
        public static Sprite Glow() => _glow != null ? _glow : _glow = Make("MonitorGlow", d => { float a = Mathf.Clamp01(1f - d); return a * a; });

        /// <summary>A solid round dot with a soft edge.</summary>
        public static Sprite Disc() => _disc != null ? _disc : _disc = Make("MonitorDisc", d => Edge(1f - d));

        /// <summary>A thin circle outline with soft edges.</summary>
        public static Sprite Ring() => _ring != null ? _ring : _ring = Make("MonitorRing", d => Mathf.Min(Edge(1f - d), Edge(d - (1f - RingWidth))));

        // 0 outside, 1 inside, ramped over EdgeSoftness texels (inside = positive distance, in radius shares)
        private static float Edge(float inside) => Mathf.Clamp01(inside * Size * 0.5f / EdgeSoftness);

        private static Sprite Make(string name, System.Func<float, float> alphaAtDistance)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, name = name };
            float half = Size * 0.5f;
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * half) / half;
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alphaAtDistance(d)) * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), Vector2.one * 0.5f);
            sprite.hideFlags = HideFlags.DontSave;
            sprite.name = name;
            return sprite;
        }
    }
}
