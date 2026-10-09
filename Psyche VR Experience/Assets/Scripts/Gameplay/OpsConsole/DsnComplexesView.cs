using System;
using PsycheVR.OpsConsole.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PsycheVR.Gameplay.MonitorUi;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Deep Space Network stack panel: Earth seen from above the North Pole turns counter-clockwise once
    /// every 20 s, carrying Goldstone, Madrid and Canberra on its rim at their real longitudes. The complex
    /// that faces Psyche (<see cref="DsnGeometry.Facing"/>, the fixed arrow to the right) lights up and
    /// sends a Gold beam with moving pulses to the arrow; when the next complex takes over, the beam
    /// switches and the handoff callback fires once per handoff. Labels stay upright and inside the rim (clear of the
    /// beam): the markers are placed on the rim each frame rather than turned with the disc. The panel gets
    /// a Black backdrop so Earth's DarkPurple rim shows against it. Advances only through <see cref="Tick"/>, which
    /// the tab calls while shown; the moving parts sit under their own nested canvas.
    /// </summary>
    public class DsnComplexesView : MonoBehaviour
    {
        private const float SecondsPerTurn = 20f;
        private const float FullTurnDeg = 360f;
        private const float PsycheDirectionDeg = 0f;     // the arrow points along +x
        private const int PulseCount = 3;
        private const float PulseSeconds = 1.5f;         // one pulse from the complex to the arrow

        // layout, mm
        private const float Pad = 6f;
        private const float CaptionHeight = 9f, CaptionSize = 7f;
        private const float LabelSize = 7f, LabelHeight = 9f, LabelGap = 2f;
        private const float NoteSize = 6f;
        private const float EarthMargin = 2f;            // between Earth and the plot's edges
        private const float MaxEarthWidthShare = 0.27f;  // Earth's radius at most this share of the plot width
        private const float DiagonalInset = 6f;          // extra inset at 45 degrees, so a label's corner stays inside the rim
        private const float MarkerSize = 4f, ActiveMarkerSize = 5.5f, ActiveGlowSize = 16f, GlowAlpha = 0.45f;
        private const float ArrowLength = 22f, ArrowHead = 4f, ArrowThickness = 1.2f;
        private const float ArrowLabelWidth = 36f, ArrowLabelGap = 2f;
        private const float BeamThickness = 1.2f, BeamAlpha = 0.85f;
        private const float PulseSize = 3f, PulseGlowSize = 8f;

        private const string CaptionText = "How handoffs work";
        private const string ArrowText = "To Psyche";
        private const string NoteText = "Seen from above the North Pole";

        [SerializeField] private RectTransform plot;
        [SerializeField] private RectTransform earth, arrowLabel;
        [SerializeField] private UIPolyline arrow, beam;
        [SerializeField] private RectTransform[] markers = new RectTransform[DsnGeometry.Count];
        [SerializeField] private RectTransform[] labelRects = new RectTransform[DsnGeometry.Count];
        [SerializeField] private TMP_Text[] labels = new TMP_Text[DsnGeometry.Count];
        [SerializeField] private RectTransform activeGlow;
        [SerializeField] private RectTransform[] pulses = new RectTransform[PulseCount];

        private readonly Vector2[] _beamPoints = new Vector2[2];
        private readonly Vector2[] _arrowPoints = new Vector2[5];
        private Action _onHandoff;
        private Vector2 _earthCentre, _arrowTail;
        private float _radius;
        private float _rotation;
        private float _pulseClock;
        private int _facing = -1;
        private bool _built;    // every part exists: layout may run

        /// <summary>Earth's current rotation, degrees counter-clockwise from the longitudes' rest position.</summary>
        public float Rotation => _rotation;

        /// <summary>Index (<see cref="DsnGeometry"/>) of the complex now facing Psyche.</summary>
        public int FacingIndex => _facing;

        /// <summary>
        /// Creates the view stretched over <paramref name="parent"/>, at rotation 0. Fonts come from
        /// <paramref name="content"/> (may be null: default font). <paramref name="onHandoff"/> (may be null)
        /// runs each time a new complex takes over while the view advances through <see cref="Tick"/>.
        /// </summary>
        public static DsnComplexesView Create(RectTransform parent, MonitorContent content, Action onHandoff)
        {
            var rt = Child(parent, "DsnComplexes");
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.one * Pad; rt.offsetMax = -Vector2.one * Pad;
            var view = rt.gameObject.AddComponent<DsnComplexesView>();
            view._onHandoff = onHandoff;
            // space behind Earth: the whole panel, under everything else
            var backdrop = Child(rt, "Backdrop").gameObject.AddComponent<Image>();
            backdrop.color = MonitorPalette.Black; backdrop.raycastTarget = false;
            var brt = backdrop.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = -Vector2.one * Pad; brt.offsetMax = Vector2.one * Pad;
            var titleFont = content != null ? content.titleFont : null;
            var bodyFont = content != null ? content.bodyFont : null;

            var caption = MonitorScreen.Text(rt, "Caption", bodyFont, CaptionSize, TextAlignmentOptions.BottomLeft, MonitorPalette.LightGrey);
            caption.text = CaptionText;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            var crt = caption.rectTransform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.sizeDelta = new Vector2(0f, CaptionHeight); crt.anchoredPosition = Vector2.zero;
            var note = MonitorScreen.Text(rt, "Note", bodyFont, NoteSize, TextAlignmentOptions.BottomRight, MonitorPalette.LightGrey);
            note.text = NoteText;
            note.textWrappingMode = TextWrappingModes.NoWrap;
            var nrt = note.rectTransform;
            nrt.anchorMin = Vector2.zero; nrt.anchorMax = new Vector2(1f, 0f);
            nrt.pivot = new Vector2(0.5f, 0f);
            nrt.sizeDelta = new Vector2(0f, CaptionHeight); nrt.anchoredPosition = Vector2.zero;

            view.plot = Child(rt, "Plot");
            view.plot.anchorMin = Vector2.zero; view.plot.anchorMax = Vector2.one;
            view.plot.offsetMin = new Vector2(0f, CaptionHeight); view.plot.offsetMax = Vector2.zero;

            // Earth: a Purple disc in a DarkPurple rim
            view.earth = Dot(view.plot, "Earth", MonitorPalette.Purple, 1f);
            var rim = Child(view.earth, "Rim").gameObject.AddComponent<Image>();
            rim.sprite = MonitorSprites.Ring(); rim.color = MonitorPalette.DarkPurple; rim.raycastTarget = false;
            Stretch(rim.rectTransform);

            // the fixed arrow towards Psyche
            view.arrow = Line(view.plot, "Arrow", MonitorPalette.White, ArrowThickness);
            var arrowText = MonitorScreen.Text(view.plot, "ArrowLabel", titleFont, LabelSize, TextAlignmentOptions.BottomRight, MonitorPalette.White);
            arrowText.text = ArrowText;
            arrowText.textWrappingMode = TextWrappingModes.NoWrap;
            view.arrowLabel = arrowText.rectTransform;
            view.arrowLabel.anchorMin = view.arrowLabel.anchorMax = new Vector2(0.5f, 0.5f);
            view.arrowLabel.pivot = new Vector2(1f, 0f);
            view.arrowLabel.sizeDelta = new Vector2(ArrowLabelWidth, LabelHeight);

            // the moving parts: own canvas (inherits sorting, still clipped by the panel's mask)
            var animated = Child(view.plot, "Animated");
            Stretch(animated);
            animated.gameObject.AddComponent<Canvas>().overrideSorting = false;
            view.beam = Line(animated, "Beam", WithAlpha(MonitorPalette.Gold, BeamAlpha), BeamThickness);
            for (int i = 0; i < PulseCount; i++)
            {
                view.pulses[i] = Halo(animated, $"Pulse{i}", WithAlpha(MonitorPalette.Gold, GlowAlpha), PulseGlowSize);
                Dot(view.pulses[i], "Core", MonitorPalette.Gold, PulseSize);
            }
            view.activeGlow = Halo(animated, "ActiveGlow", WithAlpha(MonitorPalette.Mustard, GlowAlpha), ActiveGlowSize);
            for (int i = 0; i < DsnGeometry.Count; i++)
            {
                view.markers[i] = Dot(animated, DsnGeometry.Names[i], MonitorPalette.Mustard, MarkerSize);
                var label = MonitorScreen.Text(animated, DsnGeometry.Names[i] + "Label", bodyFont, LabelSize, TextAlignmentOptions.Center, MonitorPalette.LightGrey);
                label.text = DsnGeometry.Names[i];
                label.textWrappingMode = TextWrappingModes.NoWrap;
                // sized to the word, so the pivot can hug the inside of the rim from any side
                float width = label.GetPreferredValues(DsnGeometry.Names[i]).x;
                var lrt = label.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(width, LabelHeight);
                view.labels[i] = label;
                view.labelRects[i] = lrt;
            }

            view._built = true;
            view.Layout();
            view.Restart();
            return view;
        }

        /// <summary>Back to rotation 0 with the beams' pulses at their start (no handoff callback).</summary>
        public void Restart()
        {
            _pulseClock = 0f;
            SetRotation(0f);
        }

        /// <summary>
        /// Advances Earth's turn by <paramref name="dt"/> seconds (a full turn every 20 s) and the beam's
        /// pulses; fires the handoff callback when a new complex faces Psyche.
        /// </summary>
        public void Tick(float dt)
        {
            _pulseClock = Mathf.Repeat(_pulseClock + dt, PulseSeconds);
            Apply(Mathf.Repeat(_rotation + dt * FullTurnDeg / SecondsPerTurn, FullTurnDeg), true);
        }

        /// <summary>Shows Earth turned by <paramref name="degrees"/> counter-clockwise, without the handoff callback (edit-mode previews).</summary>
        public void SetRotation(float degrees) => Apply(Mathf.Repeat(degrees, FullTurnDeg), false);

        private void Apply(float rotation, bool announce)
        {
            _rotation = rotation;
            if (!_built) return;
            for (int i = 0; i < DsnGeometry.Count; i++)
            {
                Vector2 dir = Direction(DsnGeometry.AngleOf(i, rotation));
                // just inside the rim, so the complexes read as on Earth
                markers[i].anchoredPosition = _earthCentre + dir * (_radius - ActiveMarkerSize * 0.5f);
                // the label's outer edge sits just inside the rim, whichever side of Earth it is on
                float inset = ActiveMarkerSize + LabelGap + DiagonalInset * 2f * Mathf.Abs(dir.x * dir.y);
                labelRects[i].pivot = new Vector2(0.5f + dir.x * 0.5f, 0.5f + dir.y * 0.5f);
                labelRects[i].anchoredPosition = _earthCentre + dir * (_radius - inset);
            }

            int facing = DsnGeometry.Facing(rotation, PsycheDirectionDeg);
            if (facing != _facing)
            {
                if (_facing >= 0)
                {
                    labels[_facing].color = MonitorPalette.LightGrey;
                    markers[_facing].sizeDelta = Vector2.one * MarkerSize;
                    if (announce) _onHandoff?.Invoke();
                }
                _facing = facing;
                labels[facing].color = MonitorPalette.White;
                markers[facing].sizeDelta = Vector2.one * ActiveMarkerSize;
            }

            Vector2 from = markers[facing].anchoredPosition;
            activeGlow.anchoredPosition = from;
            _beamPoints[0] = from;
            _beamPoints[1] = _arrowTail;
            beam.SetPoints(_beamPoints);
            for (int i = 0; i < PulseCount; i++)
            {
                float along = Mathf.Repeat(_pulseClock / PulseSeconds + (float)i / PulseCount, 1f);
                pulses[i].anchoredPosition = Vector2.Lerp(from, _arrowTail, along);
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_built) Layout();
        }

        // Earth on the left (labels inside it), the arrow at the right edge at Earth's height
        private void Layout()
        {
            Rect r = plot.rect;
            float edge = EarthMargin + ActiveMarkerSize * 0.5f;   // the active marker stays in the plot
            _radius = Mathf.Max(Mathf.Min(r.height * 0.5f - edge, r.width * MaxEarthWidthShare), 0f);
            // positions are offsets from the plot's centre (every child is anchored there)
            _earthCentre = new Vector2(r.xMin + edge + _radius, r.center.y) - r.center;
            earth.anchoredPosition = _earthCentre;
            earth.sizeDelta = Vector2.one * _radius * 2f;

            float tip = r.xMax - r.center.x;
            _arrowTail = new Vector2(tip - ArrowLength, _earthCentre.y);
            Vector2 head = new Vector2(tip, _earthCentre.y);
            _arrowPoints[0] = _arrowTail;
            _arrowPoints[1] = head;
            _arrowPoints[2] = head + new Vector2(-ArrowHead, ArrowHead);
            _arrowPoints[3] = head;
            _arrowPoints[4] = head + new Vector2(-ArrowHead, -ArrowHead);
            arrow.SetPoints(_arrowPoints);
            arrowLabel.anchoredPosition = new Vector2(tip, _earthCentre.y + ArrowHead + ArrowLabelGap);

            int facing = _facing;
            _facing = -1;   // re-applies the active look
            if (facing >= 0) { labels[facing].color = MonitorPalette.LightGrey; markers[facing].sizeDelta = Vector2.one * MarkerSize; }
            Apply(_rotation, false);
        }

        // the unit vector at angleDeg, counter-clockwise from +x
        private static Vector2 Direction(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

    }
}
