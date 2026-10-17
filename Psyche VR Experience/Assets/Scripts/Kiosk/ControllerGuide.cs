using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using PsycheVR.Gameplay;

namespace PsycheVR.Kiosk
{
    /// <summary>
    /// Event mode's first screen (TG-324): how the controllers work, shown before the visitor starts. A card
    /// floats at eye height in front of the start spot with two pictures: grab (a Touch Plus controller with
    /// the grip and the trigger lit) and slap (the game's glove coming down on a dome button in still frames);
    /// no other button is used in Event mode. Pictures: docs/superpowers/scripts/guide/README.md.
    /// Its call to action, "Squeeze both triggers to start", is the press that starts the session clock and
    /// unlocks the room (<see cref="KioskSession"/> leaves Armed), so nothing else dismisses it: a visitor
    /// reads it, or the staff tell them to squeeze both triggers. Everything is
    /// on the card, nothing is audio-only, and the scene reload between visitors brings it back.
    ///
    /// Built in code at Start like the puzzle board, in the same palette and fonts. Lives on the Kiosk
    /// prefab, which Story mode removes, so it never shows there.
    /// </summary>
    public sealed class ControllerGuide : MonoBehaviour
    {
        private const float MillimetresPerMetre = 1000f;
        private const float Width = 500f, Height = 380f;  // mm
        private const float GrabShare = 0.62f;            // the grab picture is wide (callouts both sides), the slap one tall
        private const float Pad = 14f;
        private const float KickerSize = 12f, TitleSize = 32f, HeadSize = 24f, LineSize = 15f, CtaSize = 18f;
        private const float FadeSeconds = 0.5f;
        private const float FallbackEyeHeight = 1.5f;     // m, when the head is not tracked yet

        [Tooltip("The session whose first input dismisses the card.")]
        [SerializeField] private KioskSession session;

        [SerializeField] private TMP_FontAsset titleFont;
        [SerializeField] private TMP_FontAsset bodyFont;
        [SerializeField] private Sprite grabPicture;
        [SerializeField] private Sprite slapPicture;

        [Tooltip("How far (m) in front of the start spot the card floats.")]
        [SerializeField] private float distance = 0.55f;

        private RectTransform _root;
        private CanvasGroup _group;
        private float _fadeStart = -1f;

        private void Start()
        {
            if (session == null) session = GetComponent<KioskSession>();
            if (session == null || session.CurrentPhase != KioskSession.Phase.Armed) { enabled = false; return; }
            Build();
            Place();
        }

        /// <summary>At eye height, <see cref="distance"/> ahead of the rig's start facing, turned to face it.</summary>
        private void Place()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            Transform start = origin != null ? origin.transform : transform;
            var cam = origin != null && origin.Camera != null ? origin.Camera.transform : (Camera.main != null ? Camera.main.transform : null);
            float eye = cam != null && cam.position.y - start.position.y > 0.8f ? cam.position.y : start.position.y + FallbackEyeHeight;
            Vector3 forward = Vector3.ProjectOnPlane(start.forward, Vector3.up).normalized;
            Vector3 at = start.position + forward * distance;
            _root.position = new Vector3(at.x, eye, at.z);
            _root.rotation = Quaternion.LookRotation(forward, Vector3.up);   // a canvas faces its -Z: readable from the start spot
        }

        private void Update()
        {
            if (_root == null) return;
            if (_fadeStart < 0f && session.CurrentPhase != KioskSession.Phase.Armed) _fadeStart = Time.time;
            if (_fadeStart < 0f) return;
            float k = Mathf.Clamp01((Time.time - _fadeStart) / FadeSeconds);
            _group.alpha = 1f - k;
            if (k >= 1f)
            {
                Destroy(_root.gameObject);
                _root = null;
                enabled = false;
            }
        }

        private void Build()
        {
            var go = new GameObject("ControllerGuideCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            _group = go.GetComponent<CanvasGroup>();
            _group.interactable = false; _group.blocksRaycasts = false;
            _root = (RectTransform)go.transform;
            _root.SetParent(null, false);
            _root.localScale = Vector3.one / MillimetresPerMetre;
            _root.sizeDelta = new Vector2(Width, Height);

            Fill(_root, "Background", MonitorPalette.Black);
            var header = Region(_root, "Header", 0f, 1f, 0.79f, 1f);
            var band = Fill(header, "Band", MonitorPalette.DarkPurple);
            band.rectTransform.offsetMin = new Vector2(-Pad, 0f); band.rectTransform.offsetMax = new Vector2(Pad, Pad);
            var kicker = Text(header, "Kicker", bodyFont, KickerSize, TextAlignmentOptions.TopLeft, MonitorPalette.Mustard, "BEFORE YOU START");
            kicker.characterSpacing = 6f;
            Stretch(kicker.rectTransform, 0f, 0.64f, 1f, 1f);
            var title = Text(header, "Title", titleFont, TitleSize, TextAlignmentOptions.BottomLeft, MonitorPalette.White, "HOW YOUR HANDS WORK");
            Stretch(title.rectTransform, 0f, 0f, 1f, 0.64f);
            title.enableAutoSizing = true; title.fontSizeMin = TitleSize * 0.7f; title.fontSizeMax = TitleSize;
            var rule = Fill(header, "Rule", MonitorPalette.Mustard);
            rule.rectTransform.anchorMin = Vector2.zero; rule.rectTransform.anchorMax = new Vector2(1f, 0f);
            rule.rectTransform.offsetMin = new Vector2(Pad * 0.5f, -1.5f); rule.rectTransform.offsetMax = new Vector2(-Pad * 0.5f, 1.5f);

            Panel(Region(_root, "Grab", 0f, GrabShare, 0.17f, 0.79f), grabPicture, "GRAB", "Squeeze the grip or the trigger");
            Panel(Region(_root, "Slap", GrabShare, 1f, 0.17f, 0.79f), slapPicture, "SLAP", "Hit objects with an open hand");

            var cta = Region(_root, "Start", 0.12f, 0.88f, 0.03f, 0.15f);
            var pill = cta.gameObject.AddComponent<Image>(); pill.color = MonitorPalette.Mustard; pill.raycastTarget = false;
            var ctaText = Text(cta, "Text", titleFont, CtaSize, TextAlignmentOptions.Center, MonitorPalette.Black, "SQUEEZE BOTH TRIGGERS TO START");
            MonitorUi.Stretch(ctaText.rectTransform);
        }

        private void Panel(RectTransform area, Sprite picture, string head, string line)
        {
            var card = Fill(area, "Card", MonitorPalette.DarkPurple);
            card.rectTransform.offsetMin = new Vector2(Pad * 0.5f, Pad * 0.5f); card.rectTransform.offsetMax = new Vector2(-Pad * 0.5f, -Pad * 0.5f);
            var pic = MonitorUi.Child(area, "Picture").gameObject.AddComponent<Image>();
            pic.sprite = picture; pic.preserveAspect = true; pic.raycastTarget = false;
            Stretch(pic.rectTransform, 0.04f, 0.36f, 0.96f, 1f);
            var h = Text(area, "Head", titleFont, HeadSize, TextAlignmentOptions.Bottom, MonitorPalette.Mustard, head);
            Stretch(h.rectTransform, 0f, 0.22f, 1f, 0.38f);
            var l = Text(area, "Line", bodyFont, LineSize, TextAlignmentOptions.Top, MonitorPalette.White, line);
            Stretch(l.rectTransform, 0.06f, 0.03f, 0.94f, 0.25f);
            l.enableAutoSizing = true; l.fontSizeMin = LineSize * 0.7f; l.fontSizeMax = LineSize;
        }

        private static void Stretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = new Vector2(Pad, Pad) * 0.5f; rt.offsetMax = -new Vector2(Pad, Pad) * 0.5f;
        }

        private static RectTransform Region(RectTransform parent, string name, float xMin, float xMax, float yMin, float yMax)
        {
            var rt = MonitorUi.Child(parent, name);
            Stretch(rt, xMin, yMin, xMax, yMax);
            return rt;
        }

        private static Image Fill(RectTransform parent, string name, Color c)
        {
            var rt = MonitorUi.Child(parent, name);
            MonitorUi.Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            return img;
        }

        private static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions align, Color colour, string text)
        {
            var rt = MonitorUi.Child(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.alignment = align; t.color = colour; t.raycastTarget = false; t.text = text;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }
    }
}
