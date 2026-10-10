using PsycheVR.OpsConsole.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PsycheVR.Gameplay.MonitorUi;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Solar Power main panel: the spacecraft drifts along a 0 to 3.4 AU track (Sun, Earth, Mars, the
    /// asteroid's orbit) from 1 AU out to 3.4 AU while the array power readout falls and the thruster bar
    /// empties (<see cref="PowerModel"/>). Advances only through <see cref="Tick"/>, which the tab calls
    /// while shown.
    /// </summary>
    public class PowerDriftView : MonoBehaviour
    {
        private const float ScaleAu = 3.4f;
        private const float StartAu = 1f, EndAu = 3.4f;
        private const float EarthAu = 1f, MarsAu = 1.52f;
        private const float ZoneMinAu = 2.5f, ZoneMaxAu = 3.3f;
        private const float DriftSeconds = 8f, HoldSeconds = 2f;

        // layout, mm unless a share of the view's height
        private const float Pad = 8f;
        private const float SideMargin = 22f;          // keeps the end labels inside the panel
        private const float TrackY = 0.48f;
        private const float TrackThickness = 1.2f;
        private const float TickWidth = 1.2f, TickHeight = 8f;
        private const float BandHeight = 24f, BandOutline = 1f, SunTickHeight = 16f;
        private const float SpacecraftWidth = 39f, SpacecraftGap = 2f;    // above the track line; the cutout has its own margin
        // the cutout is dark (purple-grey panels): a soft Mustard glow behind it keeps it readable on the panel
        private const float GlowScale = 1.5f, GlowAlpha = 0.25f;
        private const float TickLabelWidth = 40f, TickLabelHeight = 10f, TickLabelGap = 2f;
        private const float ReadoutSize = 22f, LabelSize = 7f, LineSize = 9f;
        private const float CaptionYMin = 0.88f, ReadoutYMin = 0.68f, ReadoutYMax = 0.88f;
        private const float BarYMin = 0.15f, BarYMax = 0.22f, BarLabelWidth = 34f, BarFrame = 1f;
        private const float LineYMax = 0.13f;
        private const float FullShare = 1f;

        private const string ReadoutFormat = "{0:1} kW approx.";   // TMP SetText: {0:1} = one decimal
        private const string FullLine = "Plenty: thruster at full power.";
        private const string WeakLine = "Less sunlight, gentler push.";

        [SerializeField] private RectTransform spacecraft;
        [SerializeField] private RectTransform barFill;
        [SerializeField] private TMP_Text readout;
        [SerializeField] private TMP_Text line;
        [SerializeField] private UIPolyline track;

        private readonly Vector2[] _trackPoints = new Vector2[2];
        private float _clock;
        private int _shownTenths = -1;
        private int _shownFull = -1;

        /// <summary>
        /// Creates the view stretched over <paramref name="parent"/>, at 1 AU. Fonts and the spacecraft
        /// sprite come from <paramref name="content"/> (may be null: default font, no icon).
        /// </summary>
        public static PowerDriftView Create(RectTransform parent, MonitorContent content)
        {
            var rt = Child(parent, "PowerDrift");
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.one * Pad; rt.offsetMax = -Vector2.one * Pad;
            var view = rt.gameObject.AddComponent<PowerDriftView>();
            var titleFont = content != null ? content.titleFont : null;
            var bodyFont = content != null ? content.bodyFont : null;

            var caption = MonitorScreen.Text(rt, "Caption", bodyFont, LabelSize, TextAlignmentOptions.TopLeft, MonitorPalette.LightGrey);
            caption.text = "Solar array power";
            Band(caption.rectTransform, CaptionYMin, 1f);

            // the track: a zero-height strip at TrackY, inset so the end labels fit; children sit on it by AU
            var strip = TrackStrip(rt, "Track");
            // the asteroid's orbit: a Mustard frame around a full Purple fill
            var outline = Child(strip, "PsycheZone").gameObject.AddComponent<Image>();
            outline.color = MonitorPalette.Mustard; outline.raycastTarget = false;
            var brt = outline.rectTransform;
            brt.anchorMin = new Vector2(ZoneMinAu / ScaleAu, 0.5f); brt.anchorMax = new Vector2(ZoneMaxAu / ScaleAu, 0.5f);
            brt.sizeDelta = new Vector2(0f, BandHeight);
            var band = Child(brt, "Fill").gameObject.AddComponent<Image>();
            band.color = MonitorPalette.Purple; band.raycastTarget = false;
            var bfr = band.rectTransform;
            bfr.anchorMin = Vector2.zero; bfr.anchorMax = Vector2.one;
            bfr.offsetMin = Vector2.one * BandOutline; bfr.offsetMax = -Vector2.one * BandOutline;
            view.track = Line(strip, "TrackLine", MonitorPalette.Grey, TrackThickness);
            view.LayoutTrack();

            Mark(strip, bodyFont, 0f, "Sun", MonitorPalette.Mustard, SunTickHeight * 0.5f);
            Mark(strip, bodyFont, EarthAu, "Earth", MonitorPalette.Grey, TickHeight * 0.5f);
            Mark(strip, bodyFont, MarsAu, "Mars", MonitorPalette.Grey, TickHeight * 0.5f);
            Mark(strip, bodyFont, (ZoneMinAu + ZoneMaxAu) * 0.5f, "Asteroid Psyche", null, BandHeight * 0.5f);

            var barLabel = MonitorScreen.Text(rt, "ThrusterLabel", bodyFont, LabelSize, TextAlignmentOptions.MidlineLeft, MonitorPalette.LightGrey);
            barLabel.text = "Thruster";
            var blr = barLabel.rectTransform;
            blr.anchorMin = new Vector2(0f, BarYMin); blr.anchorMax = new Vector2(0f, BarYMax);
            blr.pivot = new Vector2(0f, 0.5f); blr.sizeDelta = new Vector2(BarLabelWidth, 0f); blr.anchoredPosition = Vector2.zero;
            var frame = Child(rt, "ThrusterBar").gameObject.AddComponent<Image>();
            frame.color = MonitorPalette.Grey; frame.raycastTarget = false;
            var frt = frame.rectTransform;
            frt.anchorMin = new Vector2(0f, BarYMin); frt.anchorMax = new Vector2(1f, BarYMax);
            frt.offsetMin = new Vector2(BarLabelWidth, 0f); frt.offsetMax = Vector2.zero;
            var inner = Child(frt, "Inside").gameObject.AddComponent<Image>();
            inner.color = MonitorPalette.Black; inner.raycastTarget = false;
            var irt = inner.rectTransform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.one * BarFrame; irt.offsetMax = -Vector2.one * BarFrame;

            // the moving parts: own canvas (inherits sorting, still clipped by the panel's mask), so the
            // per-frame drift does not rebuild the whole monitor; last, so it draws over the static parts
            var animated = Child(rt, "Animated");
            Stretch(animated);
            animated.gameObject.AddComponent<Canvas>().overrideSorting = false;

            view.readout = MonitorScreen.Text(animated, "Readout", titleFont, ReadoutSize, TextAlignmentOptions.TopLeft, MonitorPalette.White);
            view.readout.textWrappingMode = TextWrappingModes.NoWrap;
            Band(view.readout.rectTransform, ReadoutYMin, ReadoutYMax);

            // the spacecraft: a holder moved along a copy of the track strip, with the glow behind the icon
            var sprite = content != null ? content.spacecraftIcon : null;
            float aspect = sprite != null && sprite.rect.width > 0f ? sprite.rect.height / sprite.rect.width : 1f;
            view.spacecraft = Child(TrackStrip(animated, "SpacecraftTrack"), "Spacecraft");
            view.spacecraft.pivot = new Vector2(0.5f, 0f);
            view.spacecraft.sizeDelta = new Vector2(SpacecraftWidth, SpacecraftWidth * aspect);
            view.spacecraft.anchoredPosition = new Vector2(0f, SpacecraftGap);
            var glow = Halo(view.spacecraft, "Glow", WithAlpha(MonitorPalette.Mustard, GlowAlpha), SpacecraftWidth * GlowScale).GetComponent<Image>();
            glow.enabled = sprite != null;
            var icon = Child(view.spacecraft, "Icon").gameObject.AddComponent<Image>();
            icon.raycastTarget = false; icon.preserveAspect = true;
            icon.color = Color.white; icon.material = null;   // untinted, default UI material
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            Stretch(icon.rectTransform);

            // the bar fill, over the bar's inside (same rect as Inside above)
            var fillArea = Child(animated, "ThrusterFillArea");
            fillArea.anchorMin = new Vector2(0f, BarYMin); fillArea.anchorMax = new Vector2(1f, BarYMax);
            fillArea.offsetMin = new Vector2(BarLabelWidth + BarFrame, BarFrame); fillArea.offsetMax = -Vector2.one * BarFrame;
            var fill = Child(fillArea, "Fill").gameObject.AddComponent<Image>();
            fill.color = MonitorPalette.Gold; fill.raycastTarget = false;
            view.barFill = fill.rectTransform;
            Stretch(view.barFill);

            view.line = MonitorScreen.Text(animated, "ThrusterLine", bodyFont, LineSize, TextAlignmentOptions.MidlineLeft, MonitorPalette.White);
            Band(view.line.rectTransform, 0f, LineYMax);

            view.Restart();
            return view;
        }

        /// <summary>Back to the start of the drift (1 AU).</summary>
        public void Restart()
        {
            _clock = 0f;
            SetDistance(StartAu);
        }

        /// <summary>
        /// Advances the drift by <paramref name="dt"/> seconds: 1 to 3.4 AU over 8 s (ease in-out), a 2 s
        /// hold, then back to 1 AU.
        /// </summary>
        public void Tick(float dt)
        {
            _clock = Mathf.Repeat(_clock + dt, DriftSeconds + HoldSeconds);
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_clock / DriftSeconds));
            SetDistance(Mathf.Lerp(StartAu, EndAu, s));
        }

        /// <summary>Places the spacecraft at <paramref name="au"/> and updates the readout, bar and line (also for edit-mode previews).</summary>
        public void SetDistance(float au)
        {
            au = Mathf.Clamp(au, 0f, ScaleAu);
            float x = au / ScaleAu;
            spacecraft.anchorMin = spacecraft.anchorMax = new Vector2(x, 0.5f);

            float kw = PowerModel.ArrayKw(au);
            int tenths = Mathf.RoundToInt(kw * 10f);
            if (tenths != _shownTenths)
            {
                _shownTenths = tenths;
                readout.SetText(ReadoutFormat, tenths / 10f);   // SetText formats without allocating
            }
            float share = PowerModel.ThrusterShare(au);
            barFill.anchorMax = new Vector2(share, 1f);
            int full = share >= FullShare ? 1 : 0;
            if (full != _shownFull)
            {
                _shownFull = full;
                line.text = full == 1 ? FullLine : WeakLine;
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (track != null) LayoutTrack();
        }

        // the line runs the strip's full width at its centre
        private void LayoutTrack()
        {
            var r = track.rectTransform.rect;
            _trackPoints[0] = new Vector2(r.xMin, r.center.y);
            _trackPoints[1] = new Vector2(r.xMax, r.center.y);
            track.SetPoints(_trackPoints);
        }

        // a tick (when tickColour is set) and a label under the track at au
        private static void Mark(RectTransform strip, TMP_FontAsset font, float au, string label, Color? tickColour, float labelDrop)
        {
            var anchor = new Vector2(au / ScaleAu, 0.5f);
            if (tickColour.HasValue)
            {
                var tick = Child(strip, label + "Tick").gameObject.AddComponent<Image>();
                tick.color = tickColour.Value; tick.raycastTarget = false;
                tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = anchor;
                tick.rectTransform.sizeDelta = new Vector2(TickWidth, labelDrop * 2f);
            }
            var text = MonitorScreen.Text(strip, label + "Label", font, LabelSize, TextAlignmentOptions.Top, MonitorPalette.LightGrey);
            text.text = label;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(TickLabelWidth, TickLabelHeight);
            rt.anchoredPosition = new Vector2(0f, -(labelDrop + TickLabelGap));
        }

        // a zero-height strip at TrackY across the view, inset by SideMargin; children sit on it by AU
        private static RectTransform TrackStrip(RectTransform parent, string name)
        {
            var strip = Child(parent, name);
            strip.anchorMin = new Vector2(0f, TrackY); strip.anchorMax = new Vector2(1f, TrackY);
            strip.offsetMin = new Vector2(SideMargin, 0f); strip.offsetMax = new Vector2(-SideMargin, 0f);
            return strip;
        }

        private static void Band(RectTransform rt, float yMin, float yMax)
        {
            rt.anchorMin = new Vector2(0f, yMin); rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

    }
}
