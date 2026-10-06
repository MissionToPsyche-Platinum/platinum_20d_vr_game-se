using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// The ops monitor's screen, built in code on the monitor's screen face (like HandoverMessage):
    /// a world-space canvas lying on the face at the face's own tilt, with the same frame on every tab:
    /// header (title, banner, dots), body (one main panel and a stack of two), footer (line, as-of,
    /// credits). Tabs fill <see cref="Main"/>, <see cref="Top"/> and <see cref="Bottom"/>.
    /// </summary>
    public class MonitorScreen : MonoBehaviour
    {
        private const float MillimetresPerMetre = 1000f;
        private const float FaceOffset = 0.002f;
        private const float HeaderShare = 0.12f, FooterShare = 0.12f;
        private const float MainShare = 0.60f, Gutter = 0.02f;
        private const float Pad = 6f;                 // mm
        private const float TitleSize = 18f, BodySize = 9f, CreditSize = 6f, DotSize = 5f;
        private const float BannerSpeed = 40f;        // mm per second
        private const float SlideSeconds = 0.1f, SlideDistanceShare = 0.3f;
        private const float DynamicPixelsPerUnit = 10f;
        private const int DotCount = 4;

        /// <summary>The large left panel of the body.</summary>
        public RectTransform Main { get; private set; }
        /// <summary>The upper panel of the right-hand stack.</summary>
        public RectTransform Top { get; private set; }
        /// <summary>The lower panel of the right-hand stack.</summary>
        public RectTransform Bottom { get; private set; }
        /// <summary>The tab title, top left of the header.</summary>
        public TMP_Text Title { get; private set; }
        /// <summary>The banner line under the title; scrolls when it does not fit.</summary>
        public TMP_Text Banner { get; private set; }
        /// <summary>The footer line, bottom left.</summary>
        public TMP_Text Footer { get; private set; }
        /// <summary>The "as of" date and image credits, bottom right.</summary>
        public TMP_Text Credits { get; private set; }
        /// <summary>The content asset this screen was built from.</summary>
        public MonitorContent Content { get; private set; }

        private RectTransform _root, _body, _bannerMask, _attract;
        private Image[] _dots;
        private Image _black;
        private TMP_Text _attractText;
        private Coroutine _slide;

        private bool Built => _root != null;

        /// <summary>
        /// Builds the canvas on <paramref name="face"/>. Works in edit mode too (for renders); a second call
        /// does nothing. Needs the face mesh to be readable (the builder sets this on OpsMonitor.fbx).
        /// </summary>
        public void Build(Renderer face, MonitorContent content)
        {
            if (Built) return;
            var mesh = face.GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null || !mesh.isReadable || mesh.normals.Length == 0)
            {
                Debug.LogError($"[MonitorScreen] {face.name}: mesh has no readable normals; enable Read/Write on its model import.", this);
                return;
            }
            Content = content;
            Vector3 n = face.transform.TransformDirection(mesh.normals.Aggregate(Vector3.zero, (s, v) => s + v)).normalized;
            if (Vector3.Dot(n, transform.forward) < 0f) n = -n;
            var b = mesh.bounds;
            Vector3 centre = face.transform.TransformPoint(b.center);
            // the face's own up: project world up onto the face plane
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
            Vector3 right = Vector3.Cross(n, up);   // the viewer's right, looking at the face along -n
            Vector2 extent = FaceExtent(face.transform, b, right, up);

            var go = new GameObject("MonitorCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            go.layer = face.gameObject.layer;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = DynamicPixelsPerUnit;
            _root = (RectTransform)go.transform;
            _root.position = centre + n * FaceOffset;
            _root.rotation = Quaternion.LookRotation(-n, up);   // a canvas faces its -Z
            // 1 unit = 1 mm in world space, whatever the parent's scale
            Vector3 parentScale = transform.lossyScale;
            _root.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z) / MillimetresPerMetre;
            _root.sizeDelta = extent * MillimetresPerMetre;

            Fill(_root, "Background", MonitorPalette.Black);
            var header = Region(_root, "Header", 1f - HeaderShare, 1f);
            var footer = Region(_root, "Footer", 0f, FooterShare);
            _body = Region(_root, "Body", FooterShare, 1f - HeaderShare);

            Title = Text(header, "Title", content.titleFont, TitleSize, TextAlignmentOptions.TopLeft, MonitorPalette.White);
            Title.fontStyle = FontStyles.UpperCase;
            Stretch(Title.rectTransform, 0f, 0.45f, 0.7f, 1f);
            _bannerMask = Child(header, "BannerMask");
            _bannerMask.gameObject.AddComponent<RectMask2D>();
            // own canvas, so the scrolling banner re-batches without rebuilding the whole screen
            _bannerMask.gameObject.AddComponent<Canvas>().overrideSorting = false;
            Stretch(_bannerMask, 0f, 0f, 1f, 0.45f);
            Banner = Text(_bannerMask, "Banner", content.bodyFont, BodySize, TextAlignmentOptions.MidlineLeft, MonitorPalette.Mustard);
            Banner.textWrappingMode = TextWrappingModes.NoWrap;
            // pinned to the mask's left middle; Update sizes it to the text and scrolls it
            var bannerRect = Banner.rectTransform;
            bannerRect.anchorMin = bannerRect.anchorMax = bannerRect.pivot = new Vector2(0f, 0.5f);
            bannerRect.anchoredPosition = Vector2.zero;

            var dots = Child(header, "Dots");
            Stretch(dots, 0.75f, 0.55f, 1f, 1f);
            var row = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleRight; row.spacing = DotSize; row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            _dots = new Image[DotCount];
            for (int i = 0; i < _dots.Length; i++)
            {
                _dots[i] = Child(dots, $"Dot{i}").gameObject.AddComponent<Image>();
                _dots[i].rectTransform.sizeDelta = Vector2.one * DotSize;
            }

            Main = Panel(_body, "Main", 0f, MainShare);
            float stackLeft = MainShare + Gutter;
            Top = Panel(_body, "Top", stackLeft, 1f, 0.5f + Gutter * 0.5f, 1f);
            Bottom = Panel(_body, "Bottom", stackLeft, 1f, 0f, 0.5f - Gutter * 0.5f);

            Footer = Text(footer, "FooterLine", content.bodyFont, BodySize, TextAlignmentOptions.MidlineLeft, MonitorPalette.LightGrey);
            Stretch(Footer.rectTransform, 0f, 0f, 0.62f, 1f);
            Credits = Text(footer, "Credits", content.bodyFont, CreditSize, TextAlignmentOptions.MidlineRight, MonitorPalette.LightGrey);
            Stretch(Credits.rectTransform, 0.62f, 0f, 1f, 1f);

            _attract = Child(header, "Attract");
            Stretch(_attract, 0f, 0f, 1f, 1f);
            _attract.gameObject.AddComponent<Image>().color = MonitorPalette.Mustard;
            _attractText = Text(_attract, "Prompt", content.titleFont, TitleSize, TextAlignmentOptions.Center, MonitorPalette.Black);
            Stretch(_attractText.rectTransform, 0f, 0f, 1f, 1f);
            _attract.gameObject.SetActive(false);

            _black = Fill(_root, "PowerOff", Color.black);
            _black.gameObject.SetActive(false);
        }

        /// <summary>
        /// World-space width and height of the face: the 8 corners of its mesh bounds, in world space,
        /// measured along the face's <paramref name="right"/> and <paramref name="up"/> axes.
        /// </summary>
        private static Vector2 FaceExtent(Transform face, Bounds b, Vector3 right, Vector3 up)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                Vector3 w = face.TransformPoint(corner);
                float x = Vector3.Dot(w, right), y = Vector3.Dot(w, up);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            return new Vector2(maxX - minX, maxY - minY);
        }

        /// <summary>Fills the dots: <paramref name="current"/> Mustard, the rest grey.</summary>
        public void SetDots(int current, int count)
        {
            if (!Built) return;
            for (int i = 0; i < _dots.Length; i++)
            {
                _dots[i].gameObject.SetActive(i < count);
                _dots[i].color = i == current ? MonitorPalette.Mustard : MonitorPalette.Grey;
            }
        }

        /// <summary>Power off: everything black.</summary>
        public void SetBlack(bool black) { if (Built) _black.gameObject.SetActive(black); }

        /// <summary>Shows the Mustard kiosk banner over the header with <paramref name="text"/>.</summary>
        public void ShowAttract(string text) { if (!Built) return; _attractText.text = text; _attract.gameObject.SetActive(true); }

        /// <summary>Hides the kiosk banner.</summary>
        public void HideAttract() { if (Built) _attract.gameObject.SetActive(false); }

        /// <summary>Slides the body out in <paramref name="direction"/> (+1 forward), runs <paramref name="swap"/>, slides back in.</summary>
        public void Slide(int direction, Action swap)
        {
            if (_slide != null) StopCoroutine(_slide);
            if (!Built || !Application.isPlaying) { swap(); return; }
            _slide = StartCoroutine(SlideRoutine(direction, swap));
        }

        private IEnumerator SlideRoutine(int direction, Action swap)
        {
            float d = _body.rect.width * SlideDistanceShare * direction;
            for (float t = 0f; t < SlideSeconds; t += Time.unscaledDeltaTime) { _body.anchoredPosition = new Vector2(-d * t / SlideSeconds, 0f); yield return null; }
            swap();
            for (float t = 0f; t < SlideSeconds; t += Time.unscaledDeltaTime) { _body.anchoredPosition = new Vector2(d * (1f - t / SlideSeconds), 0f); yield return null; }
            _body.anchoredPosition = Vector2.zero;
            _slide = null;
        }

        private void Update()
        {
            if (Banner == null || _bannerMask == null) return;
            var rt = Banner.rectTransform;
            float textWidth = Banner.preferredWidth, maskWidth = _bannerMask.rect.width;
            rt.sizeDelta = new Vector2(textWidth, _bannerMask.rect.height);
            if (textWidth <= maskWidth) { rt.anchoredPosition = Vector2.zero; return; }
            // enters from the right edge, leaves fully past the left edge, then repeats
            float x = maskWidth - Mathf.Repeat(Time.unscaledTime * BannerSpeed, textWidth + maskWidth);
            rt.anchoredPosition = new Vector2(x, 0f);
        }

        // ---- layout helpers (anchors are fractions of the parent) ----
        private static RectTransform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = new Vector2(Pad, Pad) * 0.5f; rt.offsetMax = -new Vector2(Pad, Pad) * 0.5f;
        }

        private static RectTransform Region(RectTransform parent, string name, float yMin, float yMax)
        { var rt = Child(parent, name); Stretch(rt, 0f, yMin, 1f, yMax); return rt; }

        private static RectTransform Panel(RectTransform parent, string name, float xMin, float xMax, float yMin = 0f, float yMax = 1f)
        {
            var rt = Child(parent, name);
            Stretch(rt, xMin, yMin, xMax, yMax);
            rt.gameObject.AddComponent<Image>().color = MonitorPalette.DarkPurple;
            rt.gameObject.AddComponent<RectMask2D>();
            return rt;
        }

        private static Image Fill(RectTransform parent, string name, Color c)
        {
            var rt = Child(parent, name);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            return img;
        }

        /// <summary>A TMP label in the given font; public so tabs share the look.</summary>
        public static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions align, Color colour)
        {
            var rt = Child(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.alignment = align; t.color = colour; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }
    }
}
