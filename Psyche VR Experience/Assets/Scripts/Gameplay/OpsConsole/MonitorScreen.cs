using System;
using System.Collections;
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
        private const float DesignWidth = 598f;       // mm: the screen face the layout sizes were tuned on
        private const float HeaderShare = 0.12f, FooterShare = 0.12f;
        private const float MainShare = 0.60f, Gutter = 0.02f;
        private const float Pad = 6f;                 // mm
        private const float TitleSize = 18f, BodySize = 9f, CreditSize = 6f, DotSize = 5f;
        // the footer band (FooterShare of the screen, about 44 mm) had room to spare: its line grows to fill
        // it on one line while that line can stay at FooterOneLineMin or larger; a longer line wraps to two
        // instead of shrinking below it (FitFooter). The credits stay on one line, shrinking as needed.
        private const float FooterMinSize = 9f, FooterOneLineMin = 10f, FooterMaxSize = 14f, CreditMaxSize = 9f;
        // a Mustard bar for prompts that must not be missed (the Thruster push): the header's full height,
        // centred, with the title's purple on its left and a purple strip as wide on its right (the dots)
        private const float PopupSize = 20f, PopupMinSize = 8f;
        private const float PopupTitleGap = Pad, PopupTextPad = 1.5f;   // mm: clear of the title; inside the bar
        private const float BannerSpeed = 40f;        // mm per second
        private const float SlideSeconds = 0.1f;
        private const float SlideDrift = Pad * 0.5f;   // mm: inside the panel margin, never past the screen edge
        private const int DotCount = 4;
        private const float CrossCheckDegrees = 1f;   // tolerated gap between the bounds normal and the mesh normals

        /// <summary>Name of the canvas <see cref="Build"/> creates under this object.</summary>
        public const string CanvasName = "MonitorCanvas";

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

        private RectTransform _root, _body, _bannerMask, _attract, _popup;
        private CanvasGroup _bodyGroup;
        private Image[] _dots;
        private Image _black;
        private TMP_Text _attractText, _popupText;
        private Coroutine _slide;
        private string _fittedFooter;

        private bool Built => _root != null;

        /// <summary>
        /// Builds the canvas on <paramref name="face"/>, a flat rectangular screen mesh. Works in edit mode
        /// too (for renders); a second call does nothing. Only the mesh bounds are used (see
        /// <see cref="FaceNormal"/>), so the mesh need not be readable.
        /// </summary>
        public void Build(Renderer face, MonitorContent content)
        {
            if (Built) return;
            var filter = face.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                Debug.LogError($"[MonitorScreen] {face.name}: no mesh to build the screen on.", this);
                return;
            }
            Content = content;
            var b = mesh.bounds;
            Vector3 n = FaceNormal(face.transform, b, transform.forward);
            CrossCheckNormal(face, mesh, n);
            Vector3 centre = face.transform.TransformPoint(b.center);
            // the face's own up: project world up onto the face plane
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
            Vector3 right = Vector3.Cross(n, up);   // the viewer's right, looking at the face along -n
            Vector2 extent = FaceExtent(face.transform, b, right, up);

            var go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            go.layer = face.gameObject.layer;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _root = (RectTransform)go.transform;
            _root.position = centre + n * FaceOffset;
            _root.rotation = Quaternion.LookRotation(-n, up);   // a canvas faces its -Z
            // The layout is designed for a DesignWidth-mm screen; a bigger screen scales everything
            // (text included) up with it, so 1 unit = 1 mm only at the design size.
            float grow = extent.x * MillimetresPerMetre / DesignWidth;
            Vector3 parentScale = transform.lossyScale;
            _root.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z) * grow / MillimetresPerMetre;
            _root.sizeDelta = extent * MillimetresPerMetre / grow;

            Fill(_root, "Background", MonitorPalette.Black);
            var header = Region(_root, "Header", 1f - HeaderShare, 1f);
            var footer = Region(_root, "Footer", 0f, FooterShare);
            _body = Region(_root, "Body", FooterShare, 1f - HeaderShare);
            _bodyGroup = _body.gameObject.AddComponent<CanvasGroup>();   // fades nested canvases too
            _bodyGroup.interactable = false;
            _bodyGroup.blocksRaycasts = false;

            Title = Text(header, "Title", content.titleFont, TitleSize, TextAlignmentOptions.TopLeft, MonitorPalette.White);
            Title.fontStyle = FontStyles.UpperCase;
            Stretch(Title.rectTransform, 0f, 0.45f, 0.7f, 1f);
            _bannerMask = MonitorUi.Child(header, "BannerMask");
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

            var dots = MonitorUi.Child(header, "Dots");
            Stretch(dots, 0.75f, 0.55f, 1f, 1f);
            var row = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleRight; row.spacing = DotSize; row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            _dots = new Image[DotCount];
            for (int i = 0; i < _dots.Length; i++)
            {
                _dots[i] = MonitorUi.Child(dots, $"Dot{i}").gameObject.AddComponent<Image>();
                _dots[i].rectTransform.sizeDelta = Vector2.one * DotSize;
            }

            Main = Panel(_body, "Main", 0f, MainShare);
            float stackLeft = MainShare + Gutter;
            Top = Panel(_body, "Top", stackLeft, 1f, 0.5f + Gutter * 0.5f, 1f);
            Bottom = Panel(_body, "Bottom", stackLeft, 1f, 0f, 0.5f - Gutter * 0.5f);

            Footer = Text(footer, "FooterLine", content.bodyFont, BodySize, TextAlignmentOptions.MidlineLeft, MonitorPalette.LightGrey);
            Stretch(Footer.rectTransform, 0f, 0f, 0.62f, 1f);
            AutoSize(Footer, FooterMinSize, FooterMaxSize);   // FitFooter picks one line or two per text
            Credits = Text(footer, "Credits", content.bodyFont, CreditSize, TextAlignmentOptions.MidlineRight, MonitorPalette.LightGrey);
            Stretch(Credits.rectTransform, 0.62f, 0f, 1f, 1f);
            Credits.textWrappingMode = TextWrappingModes.NoWrap;   // one line: it shrinks rather than split "as of"
            AutoSize(Credits, CreditSize, CreditMaxSize);

            _attract = MonitorUi.Child(header, "Attract");
            Stretch(_attract, 0f, 0f, 1f, 1f);
            _attract.gameObject.AddComponent<Image>().color = MonitorPalette.Mustard;
            _attractText = Text(_attract, "Prompt", content.titleFont, TitleSize, TextAlignmentOptions.Center, MonitorPalette.Black);
            Stretch(_attractText.rectTransform, 0f, 0f, 1f, 1f);
            _attract.gameObject.SetActive(false);

            // over the banner row (the Thruster tab has no banner), above the attract bar, under the power-off
            // black; outside the body, so a page turn does not fade it
            _popup = MonitorUi.Child(header, "Popup");
            _popup.anchorMin = Vector2.zero; _popup.anchorMax = Vector2.one;   // sides set per show (ShowPopup)
            _popup.offsetMin = _popup.offsetMax = Vector2.zero;
            var card = _popup.gameObject.AddComponent<Image>(); card.color = MonitorPalette.Mustard; card.raycastTarget = false;
            _popupText = Text(_popup, "Text", content.titleFont, PopupSize, TextAlignmentOptions.Center, MonitorPalette.Black);
            MonitorUi.Stretch(_popupText.rectTransform);
            _popupText.rectTransform.offsetMin = Vector2.one * PopupTextPad;
            _popupText.rectTransform.offsetMax = -Vector2.one * PopupTextPad;
            AutoSize(_popupText, PopupMinSize, PopupSize);
            _popup.gameObject.SetActive(false);

            _black = Fill(_root, "PowerOff", Color.black);
            _black.gameObject.SetActive(false);
        }

        /// <summary>Shows <paramref name="text"/> on the header's pop-up bar (sized around the current title); replaces any text already there.</summary>
        public void ShowPopup(string text)
        {
            if (_popup == null || string.IsNullOrEmpty(text)) return;
            // equal margins: the title's left inset, its width and a gap, so the bar sits centred between
            // the title and a matching purple strip on the right
            float side = Pad * 0.5f + Title.GetPreferredValues(Title.text).x + PopupTitleGap;
            _popup.offsetMin = new Vector2(side, 0f);
            _popup.offsetMax = new Vector2(-side, 0f);
            _popupText.text = text;
            _popup.gameObject.SetActive(true);
        }

        /// <summary>Hides the header's pop-up bar.</summary>
        public void HidePopup()
        {
            if (_popup != null) _popup.gameObject.SetActive(false);
        }

        /// <summary>
        /// World-space outward normal of a flat rectangular face from its local mesh bounds alone, pointing
        /// along <paramref name="towardViewer"/>. The screen's tilt is baked into the mesh (the face's
        /// transform is untilted), so the thinnest bounds axis alone would give an upright normal. A flat
        /// face tilted about one bounds axis (the hinge) runs corner to corner across the other two, so its
        /// plane holds the hinge and one diagonal of that bounds side; the bounds' proportions give the tilt
        /// exactly. The hinge is the in-plane axis nearest to horizontal; of the two diagonals, the one whose
        /// normal faces more upward wins (a screen leans back, never forward). Valid for any tilt about the
        /// hinge, not for a face also rolled about its normal.
        /// </summary>
        private static Vector3 FaceNormal(Transform face, Bounds b, Vector3 towardViewer)
        {
            Vector3 size = b.size;
            int thin = 0;
            for (int i = 1; i < 3; i++) if (size[i] < size[thin]) thin = i;
            int p = (thin + 1) % 3, q = (thin + 2) % 3;
            Vector3 pWorld = face.TransformVector(Axis(p, 1f)), qWorld = face.TransformVector(Axis(q, 1f));
            bool pIsHinge = Mathf.Abs(Vector3.Dot(pWorld.normalized, Vector3.up)) <= Mathf.Abs(Vector3.Dot(qWorld.normalized, Vector3.up));
            Vector3 hinge = pIsHinge ? pWorld : qWorld;
            int span = pIsHinge ? q : p;

            Vector3 best = Vector3.zero;
            float bestUp = float.MinValue;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                // a diagonal of the span x thin side, in world space (TransformVector keeps any non-uniform scale)
                Vector3 diagonal = face.TransformVector(Axis(span, size[span]) + Axis(thin, sign * size[thin]));
                Vector3 n = Vector3.Cross(hinge, diagonal).normalized;
                if (Vector3.Dot(n, towardViewer) < 0f) n = -n;
                if (n.y > bestUp) { bestUp = n.y; best = n; }
            }
            return best;
        }

        private static Vector3 Axis(int index, float length)
        {
            var v = Vector3.zero;
            v[index] = length;
            return v;
        }

        /// <summary>
        /// Optional check of <see cref="FaceNormal"/> against the mesh's own normals, only when the mesh is
        /// readable: logs a warning if they disagree (a face that is not a flat tilted rectangle).
        /// </summary>
        private void CrossCheckNormal(Renderer face, Mesh mesh, Vector3 n)
        {
            if (!mesh.isReadable) return;
            var normals = mesh.normals;
            if (normals.Length == 0) return;
            Vector3 sum = Vector3.zero;
            foreach (var v in normals) sum += v;
            Vector3 fromMesh = face.transform.TransformDirection(sum).normalized;
            if (Vector3.Dot(fromMesh, n) < 0f) fromMesh = -fromMesh;
            float gap = Vector3.Angle(fromMesh, n);
            if (gap > CrossCheckDegrees)
                Debug.LogWarning($"[MonitorScreen] {face.name}: face normal from bounds is {gap:F1} deg off the mesh normals; the canvas may not lie flush.", this);
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

        /// <summary>True while a page slide is in progress (its swap may still be pending).</summary>
        public bool IsSliding => _slide != null;

        /// <summary>
        /// Page turn: the body fades out drifting a few millimetres in <paramref name="direction"/>
        /// (+1 forward), <paramref name="swap"/> runs, and the new page fades in from the other side.
        /// The drift stays inside the panel margins: a world-space canvas does not clip at its edge,
        /// so a full slide showed the panels floating beside the monitor (2026-10-03).
        /// </summary>
        public void Slide(int direction, Action swap)
        {
            if (_slide != null) StopCoroutine(_slide);
            if (!Built || !Application.isPlaying) { swap(); return; }
            _slide = StartCoroutine(SlideRoutine(direction, swap));
        }

        private IEnumerator SlideRoutine(int direction, Action swap)
        {
            float d = SlideDrift * direction;
            float startAlpha = _bodyGroup.alpha;
            for (float t = 0f; t < SlideSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / SlideSeconds;
                _bodyGroup.alpha = Mathf.Lerp(startAlpha, 0f, k);
                _body.anchoredPosition = new Vector2(-d * k, 0f);
                yield return null;
            }
            _bodyGroup.alpha = 0f;
            swap();
            for (float t = 0f; t < SlideSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / SlideSeconds;
                _bodyGroup.alpha = k;
                _body.anchoredPosition = new Vector2(d * (1f - k), 0f);
                yield return null;
            }
            _bodyGroup.alpha = 1f;
            _body.anchoredPosition = Vector2.zero;
            _slide = null;
        }

        /// <summary>A page turn cut short by disabling must not leave the body faded out.</summary>
        private void OnDisable()
        {
            _slide = null;
            if (_bodyGroup == null) return;
            _bodyGroup.alpha = 1f;
            _body.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// Fits the footer line to its text: one line when it fits at <see cref="FooterOneLineMin"/> or larger
        /// (then as large as fits, up to <see cref="FooterMaxSize"/>), otherwise wrapped to two lines. Runs on
        /// its own whenever the footer text changes; public for edit-mode previews, where Update does not run.
        /// </summary>
        public void FitFooter()
        {
            if (Footer == null) return;
            _fittedFooter = Footer.text;
            Footer.enableAutoSizing = false;
            Footer.fontSize = FooterOneLineMin;
            Footer.textWrappingMode = TextWrappingModes.NoWrap;
            bool oneLine = Footer.GetPreferredValues(Footer.text).x <= Footer.rectTransform.rect.width;
            Footer.textWrappingMode = oneLine ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
            AutoSize(Footer, oneLine ? FooterOneLineMin : FooterMinSize, FooterMaxSize);
        }

        private void Update()
        {
            if (Footer != null && !ReferenceEquals(Footer.text, _fittedFooter) && Footer.text != _fittedFooter) FitFooter();
            if (Banner == null || _bannerMask == null) return;
            var rt = Banner.rectTransform;
            float textWidth = Banner.preferredWidth, maskWidth = _bannerMask.rect.width;
            rt.sizeDelta = new Vector2(textWidth, _bannerMask.rect.height);
            if (textWidth <= maskWidth) { rt.anchoredPosition = Vector2.zero; return; }
            // enters from the right edge, leaves fully past the left edge, then repeats
            float x = maskWidth - Mathf.Repeat(Time.unscaledTime * BannerSpeed, textWidth + maskWidth);
            rt.anchoredPosition = new Vector2(x, 0f);
        }

        private static void Stretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = new Vector2(Pad, Pad) * 0.5f; rt.offsetMax = -new Vector2(Pad, Pad) * 0.5f;
        }

        private static void AutoSize(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
        }

        private static RectTransform Region(RectTransform parent, string name, float yMin, float yMax)
        { var rt = MonitorUi.Child(parent, name); Stretch(rt, 0f, yMin, 1f, yMax); return rt; }

        private static RectTransform Panel(RectTransform parent, string name, float xMin, float xMax, float yMin = 0f, float yMax = 1f)
        {
            var rt = MonitorUi.Child(parent, name);
            Stretch(rt, xMin, yMin, xMax, yMax);
            rt.gameObject.AddComponent<Image>().color = MonitorPalette.DarkPurple;
            rt.gameObject.AddComponent<RectMask2D>();
            return rt;
        }

        private static Image Fill(RectTransform parent, string name, Color c)
        {
            var rt = MonitorUi.Child(parent, name);
            MonitorUi.Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            return img;
        }

        /// <summary>A TMP label in the given font; public so tabs share the look.</summary>
        public static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions align, Color colour)
        {
            var rt = MonitorUi.Child(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.alignment = align; t.color = colour; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }
    }
}
