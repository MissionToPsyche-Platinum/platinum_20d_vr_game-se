using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// The puzzle info board's display (TG-295), built in code on the plaque face at Awake like the ops
    /// monitor (<see cref="MonitorScreen"/>), in the same palette and fonts: a header with the kicker
    /// ("PSYCHE SPACECRAFT", "PIECE 2 OF 3") and the piece's name, then a picture of the piece beside its
    /// description, and a row of progress dots, one per kind of piece: found pieces fill in, the page on
    /// show is Mustard. A description's first line reads as a sentence; each later "Label: value" line is
    /// a stat, its label in small Mustard capitals (<see cref="Format"/>), so the page scans at a glance.
    /// When the model is finished, <see cref="ShowComplete"/> shows the completion page (TG-297): the
    /// completion title alone, at title size, centred where the description sits; no one reads more then. <see cref="InstructionTextManager"/> decides what to show and calls
    /// <see cref="ShowPage"/>; before the first piece is placed the board shows the start page.
    ///
    /// The board's plaque sits under a heavily non-uniformly scaled transform, so the canvas is parented
    /// to the board's parent and placed in world space on the face: the face is the side of the plaque's
    /// bounds that its up axis points out of, and its own up runs against the plaque's forward (the
    /// plaque leans back, forward pointing down the slope). The face plane is <see cref="faceMarker"/>'s.
    /// </summary>
    public class PuzzleBoardScreen : MonoBehaviour
    {
        [Serializable]
        public class Piece
        {
            [Tooltip("Tag of the SnappableObject this entry describes. Pieces that share a page (the two solar arrays) share one entry.")]
            public string[] tags;
            [Tooltip("Picture shown beside the description (Tools > Puzzle > Render Piece Pictures).")]
            public Sprite picture;
        }

        private const float MillimetresPerMetre = 1000f;
        private const float FaceOffset = 0.002f;       // m in front of the plaque face
        private const float FrameInsetX = 0.022f;      // m of plaque frame left showing at the sides
        private const float FrameInsetTop = 0.045f;    // m at the top: the leaning plaque's bounds overstate its height
        private const float FrameInsetBottom = 0.037f; // m at the bottom
        private const float Pad = 10f;                 // mm
        private const float HeaderShare = 0.27f, FooterShare = 0.12f, PictureShare = 0.42f;
        private const float KickerSize = 12f, TitleSize = 40f, BodyMax = 19f, BodyMin = 10f;
        private const string StatLabelColour = "#F9A000";   // Mustard
        private const float DotSize = 12f, DotGap = 9f;

        [Tooltip("The plaque (MC_MessageBoard_With_Text) whose face the screen covers.")]
        [SerializeField] private Renderer plaque;

        [Tooltip("Anything lying on the plaque face (the old text): the screen sits in its plane. The plaque mesh reports empty bounds, so the face cannot be found from it.")]
        [SerializeField] private Transform faceMarker;

        [SerializeField] private TMP_FontAsset titleFont;
        [SerializeField] private TMP_FontAsset bodyFont;

        [Tooltip("One entry per kind of piece, in building order. Sets the number of progress dots.")]
        [SerializeField] private Piece[] pieces = Array.Empty<Piece>();

        [Header("Start page (no piece placed yet)")]
        [SerializeField] private string startTitle = "Start with the bus";
        [TextArea] [SerializeField] private string startText = "Place the Psyche bus (the large black box) on top of the pedestal to begin. Each piece you add shows up here.";
        [Tooltip("Picture on the start page; the bus.")]
        [SerializeField] private Sprite startPicture;
        [SerializeField] private string kicker = "PSYCHE SPACECRAFT";

        [Header("Completion page")]
        [Tooltip("Shown as written, not in capitals: \"Psyche\" is never all caps (publishing rules).")]
        [SerializeField] private string completeTitle = "Psyche Satellite Complete";

        private RectTransform _root;
        private TMP_Text _kicker, _count, _title, _body;
        private Image _picture, _glow;
        private RectTransform _card;
        private TMP_Text _centreTitle;
        private Image[] _dots;

        /// <summary>Number of kinds of piece (progress dots).</summary>
        public int PieceCount => pieces.Length;

        private void Awake() => Build();

        /// <summary>The entry index for a piece tag, or -1.</summary>
        public int IndexOf(string tag)
        {
            for (int i = 0; i < pieces.Length; i++)
                if (pieces[i].tags != null && Array.IndexOf(pieces[i].tags, tag) >= 0) return i;
            return -1;
        }

        /// <summary>The picture for a piece tag, or null.</summary>
        public Sprite PictureFor(string tag)
        {
            int i = IndexOf(tag);
            return i >= 0 ? pieces[i].picture : null;
        }

        /// <summary>The completion page: the completion title alone in the middle, every dot lit.</summary>
        public void ShowComplete()
        {
            if (_root == null) return;
            _kicker.text = kicker;
            _count.text = pieces.Length + " OF " + pieces.Length + " PLACED";
            Fill("", "", null);
            _centreTitle.text = completeTitle;
            SetCompleteLayout(true);
            foreach (var d in _dots) d.color = MonitorPalette.Mustard;
        }

        /// <summary>The completion page's layout (the centred title only) or the piece pages' (picture and text).</summary>
        private void SetCompleteLayout(bool complete)
        {
            _card.gameObject.SetActive(!complete);
            _body.gameObject.SetActive(!complete);
            _centreTitle.gameObject.SetActive(complete);
        }

        /// <summary>The start page, shown until the first piece is placed.</summary>
        public void ShowStart()
        {
            if (_root == null) return;
            SetCompleteLayout(false);
            _kicker.text = kicker;
            _count.text = "0 OF " + pieces.Length + " PLACED";
            Fill(startTitle, startText, startPicture);
            SetDots(-1, null);
        }

        /// <summary>
        /// Shows one piece's page: <paramref name="page"/> of <paramref name="pageCount"/> found so far.
        /// <paramref name="found"/> holds the entry index of every page found (for the dots).
        /// </summary>
        public void ShowPage(string title, string text, Sprite picture, int page, int pageCount, int currentEntry, int[] found)
        {
            if (_root == null) return;
            SetCompleteLayout(false);
            _kicker.text = kicker;
            _count.text = "PIECE " + (page + 1) + " OF " + pageCount;
            Fill(title, text, picture);
            SetDots(currentEntry, found);
        }

        /// <summary>
        /// Rich text for a description: the first line as written, then every "Label: value" line as a stat
        /// row with the label in small Mustard capitals. Lines without a colon pass through unchanged.
        /// </summary>
        public static string Format(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var lines = text.Replace("\r", "").Split('\n');
            var sb = new System.Text.StringBuilder(lines[0].Trim());
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                sb.Append(i == 1 ? "\n<size=40%>\n</size>" : "\n<size=20%>\n</size>");
                int colon = line.IndexOf(':');
                if (colon <= 0) { sb.Append(line); continue; }
                sb.Append("<size=72%><cspace=0.08em><color=").Append(StatLabelColour).Append(">")
                  .Append(line.Substring(0, colon).Trim().ToUpperInvariant())
                  .Append("</color></cspace></size>  ")
                  .Append(line.Substring(colon + 1).Trim());
            }
            return sb.ToString();
        }

        private void Fill(string title, string text, Sprite picture)
        {
            _title.text = title;
            _body.text = Format(text);
            _picture.sprite = picture;
            _picture.enabled = picture != null;
            _glow.enabled = picture != null;
        }

        private void SetDots(int current, int[] found)
        {
            for (int i = 0; i < _dots.Length; i++)
            {
                bool isFound = found != null && Array.IndexOf(found, i) >= 0;
                _dots[i].color = i == current ? MonitorPalette.Mustard
                    : isFound ? MonitorPalette.LightGrey
                    : MonitorUi.WithAlpha(MonitorPalette.Grey, 0.45f);
            }
        }

        private void Build()
        {
            if (_root != null || plaque == null) return;
            Transform t = plaque.transform;
            Vector3 n = t.up.normalized, up = -t.forward.normalized, right = Vector3.Cross(up, n);
            Bounds b = plaque.bounds;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                Vector3 d = c - b.center;
                float x = Vector3.Dot(d, right), y = Vector3.Dot(d, up);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            float width = (maxX - minX) - 2f * FrameInsetX, height = (maxY - minY) - FrameInsetTop - FrameInsetBottom;
            Vector3 centre = b.center + right * (minX + maxX) * 0.5f
                + up * ((minY + FrameInsetBottom) + (maxY - FrameInsetTop)) * 0.5f;

            var go = new GameObject("PuzzleBoardCanvas", typeof(RectTransform), typeof(Canvas));
            go.layer = plaque.gameObject.layer;
            go.transform.SetParent(t.parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _root = (RectTransform)go.transform;
            _root.rotation = Quaternion.LookRotation(-n, up);   // a canvas faces its -Z
            Vector3 face = faceMarker != null ? centre + n * Vector3.Dot(faceMarker.position - centre, n) : centre;
            _root.position = face + n * FaceOffset;
            Vector3 ps = t.parent != null ? t.parent.lossyScale : Vector3.one;
            _root.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z) / MillimetresPerMetre;
            _root.sizeDelta = new Vector2(width, height) * MillimetresPerMetre;

            Fill(_root, "Background", MonitorPalette.Black);
            var header = Region(_root, "Header", 1f - HeaderShare, 1f);
            var footer = Region(_root, "Footer", 0f, FooterShare);
            var body = Region(_root, "Body", FooterShare, 1f - HeaderShare);

            var band = Fill(header, "Band", MonitorPalette.DarkPurple);
            band.rectTransform.offsetMin = new Vector2(-Pad, 0f); band.rectTransform.offsetMax = new Vector2(Pad, Pad);
            _kicker = Text(header, "Kicker", bodyFont, KickerSize, TextAlignmentOptions.TopLeft, MonitorPalette.Mustard);
            Stretch(_kicker.rectTransform, 0f, 0.62f, 0.6f, 1f, Pad);
            _kicker.characterSpacing = 6f;
            _count = Text(header, "Count", bodyFont, KickerSize, TextAlignmentOptions.TopRight, MonitorPalette.LightGrey);
            Stretch(_count.rectTransform, 0.5f, 0.62f, 1f, 1f, Pad);
            _count.characterSpacing = 6f;
            _title = Text(header, "Title", titleFont, TitleSize, TextAlignmentOptions.BottomLeft, MonitorPalette.White);
            _title.fontStyle = FontStyles.UpperCase;
            Stretch(_title.rectTransform, 0f, 0f, 1f, 0.66f, Pad);
            AutoSize(_title, TitleSize * 0.6f, TitleSize);
            var rule = Fill(header, "Rule", MonitorPalette.Mustard);
            rule.rectTransform.anchorMin = new Vector2(0f, 0f); rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.offsetMin = new Vector2(Pad, -1.5f); rule.rectTransform.offsetMax = new Vector2(-Pad, 1.5f);

            var card = Region(body, "PictureCard", 0f, 1f, 0f, PictureShare);
            _card = card;
            Fill(card, "Card", MonitorPalette.DarkPurple);
            _glow = MonitorUi.Halo(card, "Glow", MonitorUi.WithAlpha(MonitorPalette.Purple, 0.9f), 1f).GetComponent<Image>();
            MonitorUi.Stretch(_glow.rectTransform);
            _picture = MonitorUi.Child(card, "Picture").gameObject.AddComponent<Image>();
            _picture.preserveAspect = true; _picture.raycastTarget = false;
            Stretch(_picture.rectTransform, 0f, 0f, 1f, 1f, 0f);

            // the completion title: the header title's font and size, shown as written, centred in the body
            _centreTitle = Text(body, "CompleteTitle", titleFont, TitleSize, TextAlignmentOptions.Center, MonitorPalette.White);
            Stretch(_centreTitle.rectTransform, 0f, 0f, 1f, 1f, Pad);
            _centreTitle.gameObject.SetActive(false);

            _body = Text(body, "Description", bodyFont, BodyMax, TextAlignmentOptions.MidlineLeft, MonitorPalette.White);
            Stretch(_body.rectTransform, PictureShare, 0f, 1f, 1f, Pad);
            _body.lineSpacing = 8f;
            AutoSize(_body, BodyMin, BodyMax);

            var dots = MonitorUi.Child(footer, "Dots");
            Stretch(dots, 0f, 0f, 1f, 1f, Pad);
            var row = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleLeft; row.spacing = DotGap;
            row.childControlWidth = row.childControlHeight = false; row.childForceExpandWidth = row.childForceExpandHeight = false;
            _dots = new Image[pieces.Length];
            for (int i = 0; i < _dots.Length; i++)
            {
                _dots[i] = MonitorUi.Child(dots, "Dot" + i).gameObject.AddComponent<Image>();
                _dots[i].sprite = MonitorSprites.Disc(); _dots[i].raycastTarget = false;
                _dots[i].rectTransform.sizeDelta = Vector2.one * DotSize;
            }

            ShowStart();
        }

        private static void Stretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax, float pad)
        {
            rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = new Vector2(pad, pad) * 0.5f; rt.offsetMax = -new Vector2(pad, pad) * 0.5f;
        }

        private static RectTransform Region(RectTransform parent, string name, float yMin, float yMax, float xMin = 0f, float xMax = 1f)
        {
            var rt = MonitorUi.Child(parent, name);
            Stretch(rt, xMin, yMin, xMax, yMax, Pad);
            return rt;
        }

        private static Image Fill(RectTransform parent, string name, Color c)
        {
            var rt = MonitorUi.Child(parent, name);
            MonitorUi.Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            return img;
        }

        private static void AutoSize(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true; t.fontSizeMin = min; t.fontSizeMax = max;
        }

        private static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions align, Color colour)
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
