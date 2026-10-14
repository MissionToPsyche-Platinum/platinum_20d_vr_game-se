using UnityEngine;
using UnityEngine.UI;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Small uGUI building helpers shared by the ops monitor's code-built views: child rects, stretching,
    /// round dots and glows (<see cref="MonitorSprites"/>), polylines and alpha tweaks. Nothing made here
    /// is a raycast target.
    /// </summary>
    internal static class MonitorUi
    {
        /// <summary>A new empty rect under <paramref name="parent"/>, on the parent's layer.</summary>
        public static RectTransform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Fills the parent exactly: anchors at the corners, no offsets.</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>A round dot of <paramref name="size"/> centred on its parent's centre.</summary>
        public static RectTransform Dot(RectTransform parent, string name, Color colour, float size)
        {
            var img = Child(parent, name).gameObject.AddComponent<Image>();
            img.sprite = MonitorSprites.Disc(); img.color = colour; img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * size;
            return rt;
        }

        /// <summary>A soft glow of <paramref name="colour"/> (alpha included) and <paramref name="size"/>, centred on its parent's centre.</summary>
        public static RectTransform Halo(RectTransform parent, string name, Color colour, float size)
        {
            var img = Child(parent, name).gameObject.AddComponent<Image>();
            img.sprite = MonitorSprites.Glow(); img.raycastTarget = false;
            img.color = colour;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * size;
            return rt;
        }

        /// <summary>An empty <see cref="UIPolyline"/> stretched over <paramref name="parent"/>.</summary>
        public static UIPolyline Line(RectTransform parent, string name, Color colour, float thickness)
        {
            var line = Child(parent, name).gameObject.AddComponent<UIPolyline>();
            line.color = colour; line.raycastTarget = false;
            line.Thickness = thickness;
            Stretch(line.rectTransform);
            return line;
        }

        /// <summary><paramref name="c"/> with alpha <paramref name="a"/>.</summary>
        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        /// <summary>Sets the graphic's alpha, skipping the write when it already matches.</summary>
        public static void SetAlpha(Graphic g, float alpha)
        {
            var c = g.color;
            if (Mathf.Approximately(c.a, alpha)) return;
            c.a = alpha;
            g.color = c;
        }
    }
}
