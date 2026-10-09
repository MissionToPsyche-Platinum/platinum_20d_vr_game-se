using System;
using System.Collections.Generic;
using System.Globalization;
using PsycheVR.OpsConsole.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PsycheVR.Gameplay.MonitorUi;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Mars Flyby main panel: Psyche's route to scale from the build-date JPL Horizons snapshot. Faint
    /// orbits of Earth, Mars and the asteroid around the Sun; the spacecraft's path drawn over 10 s from
    /// launch (2023-10-14) to arrival (2029-08-01; after the snapshot's last spacecraft sample it follows
    /// the asteroid); a Mustard ring flash and "+1,000 mph" as the timeline passes the Mars flyby; the
    /// trail solid up to the build date and dashed after; "Psyche is here today" at the build-date position
    /// during a 3 s hold, then it starts again. Advances only through <see cref="Tick"/>, which the tab
    /// calls while shown. Everything that moves sits under its own nested canvas, so the per-frame redraw
    /// does not rebuild the rest of the monitor.
    /// </summary>
    public class TrajectoryView : MonoBehaviour
    {
        private const string SpacecraftBody = "Spacecraft", AsteroidBody = "Asteroid", EarthBody = "Earth", MarsBody = "Mars";
        private static readonly DateTime LaunchDate = new DateTime(2023, 10, 14);
        private static readonly DateTime ArrivalDate = new DateTime(2029, 8, 1);
        private static readonly DateTime FlybyDate = new DateTime(2026, 5, 15);
        private const float AnimSeconds = 10f, HoldSeconds = 3f, FlashSeconds = 1.5f;
        private const float FullTurnDeg = 360f;

        // layout, mm
        private const float Pad = 6f;
        private const float CaptionHeight = 9f, CaptionSize = 6f;
        private const float PlotMargin = 3f;
        private const float DateSize = 14f, DateWidth = 60f, DateHeight = 16f;
        private const float LegendSize = 6f, LegendRow = 8f, LegendWidth = 62f, LegendDot = 3.5f, LegendGap = 2f;
        private const float OrbitThickness = 0.6f, OrbitAlpha = 0.45f;
        private const float TrailThickness = 1.2f, TrailDash = 3f;
        private const float SunSize = 6f, PlanetSize = 4.5f, SpacecraftSize = 4f;
        private const float GlowSize = 12f, GlowAlpha = 0.35f;
        private const float SunGlowSize = 16f, LegendSunGlow = 8f;
        private const float FlashStartSize = 6f, FlashEndSize = 30f, FlashLabelSize = 10f, FlashLabelFade = 0.7f;
        private const float FlashLabelWidth = 50f, FlashLabelHeight = 12f, FlashLabelGap = 2f;
        private const float TagRingSize = 8f, TagSize = 7f, TagWidth = 60f, TagHeight = 9f, TagGap = 4f;

        private const string MonthFormat = "MMM yyyy";
        private const string CaptionDateFormat = "MMM d, yyyy";
        private const string DateFormat = "yyyy-MM-dd";
        private const string CaptionFormat = "Positions: JPL Horizons, as of {0}. Dots not to scale.";
        private const string FlashText = "+1,000 mph";
        private const string TagText = "Psyche is here today";

        [SerializeField] private RectTransform plot;
        [SerializeField] private UIPolyline earthOrbit, marsOrbit, asteroidOrbit, trailSolid, trailDashed;
        [SerializeField] private RectTransform sun, earthDot, marsDot, asteroidDot, spacecraftDot;
        [SerializeField] private Image flashRing, tagRing;
        [SerializeField] private TMP_Text flashLabel, tagLabel, date;

        private MonitorSnapshot.Track _earth, _mars, _asteroid;
        private OrbitProjection _projection;
        private OrbitProjection.Timeline _timeline;
        private DateTime _buildDate;
        private DateTime[] _pathDates;
        private Vector2[] _pathAu, _pathPanel;
        private Vector2[] _earthOrbitAu, _marsOrbitAu, _asteroidOrbitAu;
        private Vector2[] _solid, _dashed;       // preallocated trail buffers, refilled per frame
        private int _buildIndex;                 // path samples dated before the build date
        private Vector2 _buildPanel, _flybyPanel;
        private float _flybySeconds;
        private string[] _monthLabels;
        private int _shownMonth = -1;
        private float _clock;
        private float _progress;
        private float _appliedProgress = -1f;
        private bool _solidComplete;             // the solid trail already runs launch to build date

        /// <summary>
        /// Creates the view stretched over <paramref name="parent"/>, at launch. Returns null (and builds
        /// nothing) when <paramref name="snapshot"/> lacks the spacecraft, asteroid, Earth or Mars track.
        /// Fonts come from <paramref name="content"/> (may be null: default font).
        /// </summary>
        public static TrajectoryView Create(RectTransform parent, MonitorContent content, MonitorSnapshot snapshot)
        {
            var spacecraftTrack = snapshot?.Body(SpacecraftBody);
            var asteroid = snapshot?.Body(AsteroidBody);
            var earth = snapshot?.Body(EarthBody);
            var mars = snapshot?.Body(MarsBody);
            if (!HasPoints(spacecraftTrack) || !HasPoints(asteroid) || !HasPoints(earth) || !HasPoints(mars)) return null;

            var rt = Child(parent, "Trajectory");
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.one * Pad; rt.offsetMax = -Vector2.one * Pad;
            var view = rt.gameObject.AddComponent<TrajectoryView>();
            view._earth = earth; view._mars = mars; view._asteroid = asteroid;
            view._buildDate = snapshot.BuildDate;
            view._timeline = new OrbitProjection.Timeline(LaunchDate, ArrivalDate, AnimSeconds);
            view._flybySeconds = view._timeline.SecondsAt(FlybyDate);
            view.BuildPath(spacecraftTrack, asteroid);
            view._earthOrbitAu = OneOrbit(earth);
            view._marsOrbitAu = OneOrbit(mars);
            view._asteroidOrbitAu = OneOrbit(asteroid);
            view.BuildMonthLabels();

            var titleFont = content != null ? content.titleFont : null;
            var bodyFont = content != null ? content.bodyFont : null;

            var caption = MonitorScreen.Text(rt, "Caption", bodyFont, CaptionSize, TextAlignmentOptions.BottomLeft, MonitorPalette.LightGrey);
            caption.text = string.Format(CultureInfo.InvariantCulture, CaptionFormat,
                snapshot.BuildDate.ToString(CaptionDateFormat, CultureInfo.InvariantCulture));
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            var crt = caption.rectTransform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.sizeDelta = new Vector2(0f, CaptionHeight); crt.anchoredPosition = Vector2.zero;

            // the plot: everything above the caption, pivot at its centre so local coordinates are centred
            view.plot = Child(rt, "Plot");
            view.plot.anchorMin = Vector2.zero; view.plot.anchorMax = Vector2.one;
            view.plot.offsetMin = new Vector2(0f, CaptionHeight); view.plot.offsetMax = Vector2.zero;

            view.earthOrbit = Line(view.plot, "EarthOrbit", Faint(MonitorPalette.White), OrbitThickness);
            view.marsOrbit = Line(view.plot, "MarsOrbit", Faint(MonitorPalette.Coral), OrbitThickness);
            view.asteroidOrbit = Line(view.plot, "AsteroidOrbit", Faint(MonitorPalette.Grey), OrbitThickness);
            // the Sun: a White disc inside a Mustard halo
            var sunGlow = Halo(view.plot, "SunGlow", WithAlpha(MonitorPalette.Mustard, GlowAlpha), SunGlowSize);
            Dot(sunGlow, "Sun", MonitorPalette.White, SunSize);
            view.sun = sunGlow;
            Legend(view.plot, bodyFont);

            // the moving parts: own canvas (inherits sorting, still clipped by the panel's mask)
            var animated = Child(view.plot, "Animated");
            Stretch(animated);
            animated.gameObject.AddComponent<Canvas>().overrideSorting = false;
            view.trailSolid = Line(animated, "TrailSolid", MonitorPalette.Mustard, TrailThickness);
            view.trailDashed = Line(animated, "TrailDashed", MonitorPalette.Mustard, TrailThickness);
            view.trailDashed.Dashed = true; view.trailDashed.DashLength = TrailDash;
            view.earthDot = Dot(animated, "Earth", MonitorPalette.White, PlanetSize);
            view.marsDot = Dot(animated, "Mars", MonitorPalette.Coral, PlanetSize);
            view.asteroidDot = Dot(animated, "Asteroid", MonitorPalette.Grey, PlanetSize);
            view.spacecraftDot = Dot(animated, "Spacecraft", MonitorPalette.Mustard, SpacecraftSize);
            var glow = Child(view.spacecraftDot, "Glow").gameObject.AddComponent<Image>();
            glow.sprite = MonitorSprites.Glow(); glow.raycastTarget = false;
            glow.color = WithAlpha(MonitorPalette.Mustard, GlowAlpha);
            glow.rectTransform.sizeDelta = Vector2.one * GlowSize;
            glow.transform.SetAsFirstSibling();

            view.flashRing = Dot(animated, "FlybyFlash", MonitorPalette.Mustard, FlashStartSize).GetComponent<Image>();
            view.flashRing.sprite = MonitorSprites.Ring();
            view.flashLabel = MonitorScreen.Text(view.flashRing.rectTransform, "Label", titleFont, FlashLabelSize, TextAlignmentOptions.MidlineLeft, MonitorPalette.Mustard);
            view.flashLabel.text = FlashText;
            view.flashLabel.textWrappingMode = TextWrappingModes.NoWrap;
            var frt = view.flashLabel.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f); frt.pivot = new Vector2(0f, 0.5f);
            frt.sizeDelta = new Vector2(FlashLabelWidth, FlashLabelHeight);
            frt.anchoredPosition = new Vector2(FlashEndSize * 0.5f + FlashLabelGap, 0f);   // clear of the grown ring

            view.tagRing = Dot(animated, "Today", MonitorPalette.White, TagRingSize).GetComponent<Image>();
            view.tagRing.sprite = MonitorSprites.Ring();
            view.tagLabel = MonitorScreen.Text(view.tagRing.rectTransform, "Label", bodyFont, TagSize, TextAlignmentOptions.Bottom, MonitorPalette.White);
            view.tagLabel.text = TagText;
            view.tagLabel.textWrappingMode = TextWrappingModes.NoWrap;
            var trt = view.tagLabel.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f); trt.pivot = new Vector2(0.5f, 0f);
            trt.sizeDelta = new Vector2(TagWidth, TagHeight);
            trt.anchoredPosition = new Vector2(0f, TagRingSize * 0.5f + TagGap);

            view.date = MonitorScreen.Text(animated, "Date", titleFont, DateSize, TextAlignmentOptions.TopLeft, MonitorPalette.White);
            view.date.textWrappingMode = TextWrappingModes.NoWrap;
            var drt = view.date.rectTransform;
            drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0f, 1f);
            drt.sizeDelta = new Vector2(DateWidth, DateHeight); drt.anchoredPosition = Vector2.zero;

            view.Layout();
            view.Restart();
            return view;
        }

        /// <summary>Back to launch.</summary>
        public void Restart()
        {
            _clock = 0f;
            Apply(0f);
        }

        /// <summary>
        /// Advances the animation by <paramref name="dt"/> seconds: 10 s from launch to arrival, a 3 s hold
        /// on the "Psyche is here today" tag, then launch again.
        /// </summary>
        public void Tick(float dt)
        {
            _clock = Mathf.Repeat(_clock + dt, AnimSeconds + HoldSeconds);
            Apply(Mathf.Clamp01(_clock / AnimSeconds));
        }

        /// <summary>Shows the frame at <paramref name="progress"/> (0 = launch, 1 = arrival with the tag), for edit-mode previews.</summary>
        public void SetProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            _clock = progress * AnimSeconds;
            Apply(progress);
        }

        /// <summary>The progress (0..1) at which the timeline shows <paramref name="when"/>.</summary>
        public float ProgressAt(DateTime when) => _timeline.SecondsAt(when) / AnimSeconds;

        private void Apply(float progress)
        {
            _progress = progress;
            if (_pathPanel == null || Mathf.Approximately(progress, _appliedProgress)) return;
            _appliedProgress = progress;
            float t = progress * AnimSeconds;
            DateTime now = _timeline.At(t);

            Vector2 head = _projection.ToPanel(PathAt(now, out int passed));
            if (now < _buildDate)
            {
                _solidComplete = false;
                Array.Copy(_pathPanel, _solid, passed);
                _solid[passed] = head;
                trailSolid.SetPoints(_solid, passed + 1);
                trailDashed.SetPoints(_dashed, 0);
            }
            else
            {
                if (!_solidComplete)
                {
                    _solidComplete = true;
                    Array.Copy(_pathPanel, _solid, _buildIndex);
                    _solid[_buildIndex] = _buildPanel;
                    trailSolid.SetPoints(_solid, _buildIndex + 1);
                }
                int after = passed - _buildIndex;
                _dashed[0] = _buildPanel;
                Array.Copy(_pathPanel, _buildIndex, _dashed, 1, after);
                _dashed[after + 1] = head;
                trailDashed.SetPoints(_dashed, after + 2);
            }

            Place(spacecraftDot, head);
            Place(earthDot, _projection.ToPanel(_earth.At(now)));
            Place(marsDot, _projection.ToPanel(_mars.At(now)));
            Place(asteroidDot, _projection.ToPanel(_asteroid.At(now)));

            // the flyby flash: the ring grows and fades over FlashSeconds; the label holds, then fades
            float flash = (t - _flybySeconds) / FlashSeconds;
            bool flashing = flash >= 0f && flash < 1f;
            flashRing.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(FlashStartSize, FlashEndSize, flashing ? 1f - (1f - flash) * (1f - flash) : 0f);
            SetAlpha(flashRing, flashing ? 1f - flash : 0f);
            SetAlpha(flashLabel, !flashing ? 0f : flash < FlashLabelFade ? 1f : (1f - flash) / (1f - FlashLabelFade));

            float tag = progress >= 1f ? 1f : 0f;
            SetAlpha(tagRing, tag);
            SetAlpha(tagLabel, tag);

            int month = MonthIndex(now);
            if (month != _shownMonth)
            {
                _shownMonth = month;
                date.text = _monthLabels[Mathf.Clamp(month, 0, _monthLabels.Length - 1)];
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_pathAu != null && plot != null) Layout();
        }

        // projects every AU array onto the plot's current rect and redraws
        private void Layout()
        {
            _projection = new OrbitProjection(plot.rect, PlotMargin);
            earthOrbit.SetPoints(Project(_earthOrbitAu));
            marsOrbit.SetPoints(Project(_marsOrbitAu));
            asteroidOrbit.SetPoints(Project(_asteroidOrbitAu));
            _pathPanel = Project(_pathAu);
            _buildPanel = _projection.ToPanel(PathAt(_buildDate, out _));
            _flybyPanel = _projection.ToPanel(PathAt(FlybyDate, out _));
            Place(sun, _projection.ToPanel(Vector2.zero));
            Place(flashRing.rectTransform, _flybyPanel);
            Place(tagRing.rectTransform, _buildPanel);
            _appliedProgress = -1f;
            _solidComplete = false;
            Apply(_progress);
        }

        // the route: spacecraft samples to the end of its track, then the asteroid's samples up to arrival
        private void BuildPath(MonitorSnapshot.Track spacecraft, MonitorSnapshot.Track asteroid)
        {
            var dates = new List<DateTime>();
            var au = new List<Vector2>();
            foreach (var p in spacecraft.points)
            {
                var d = ParseDate(p.date);
                if (d > ArrivalDate) break;
                dates.Add(d); au.Add(new Vector2(p.x, p.y));
            }
            DateTime last = dates[dates.Count - 1];
            foreach (var p in asteroid.points)
            {
                var d = ParseDate(p.date);
                if (d <= last) continue;
                if (d >= ArrivalDate) break;
                dates.Add(d); au.Add(new Vector2(p.x, p.y));
            }
            if (dates[dates.Count - 1] < ArrivalDate) { dates.Add(ArrivalDate); au.Add(asteroid.At(ArrivalDate)); }
            _pathDates = dates.ToArray();
            _pathAu = au.ToArray();
            if (_buildDate < LaunchDate) _buildDate = LaunchDate;
            if (_buildDate > ArrivalDate) _buildDate = ArrivalDate;
            _buildIndex = 0;
            while (_buildIndex < _pathDates.Length && _pathDates[_buildIndex] < _buildDate) _buildIndex++;
            int n = _pathDates.Length;
            _solid = new Vector2[_buildIndex + 1];
            _dashed = new Vector2[n - _buildIndex + 2];
        }

        // position on the route at when (linear between samples), and how many samples are dated on or before it
        private Vector2 PathAt(DateTime when, out int passed)
        {
            passed = 0;
            while (passed < _pathDates.Length && _pathDates[passed] <= when) passed++;
            if (passed == 0) return _pathAu[0];
            if (passed == _pathDates.Length) return _pathAu[passed - 1];
            DateTime a = _pathDates[passed - 1], b = _pathDates[passed];
            float f = (float)((when - a).TotalSeconds / (b - a).TotalSeconds);
            return Vector2.Lerp(_pathAu[passed - 1], _pathAu[passed], f);
        }

        private void BuildMonthLabels()
        {
            _monthLabels = new string[MonthIndex(ArrivalDate) + 1];
            for (int i = 0; i < _monthLabels.Length; i++)
                _monthLabels[i] = new DateTime(LaunchDate.Year, LaunchDate.Month, 1).AddMonths(i).ToString(MonthFormat, CultureInfo.InvariantCulture);
        }

        private static int MonthIndex(DateTime d) => (d.Year - LaunchDate.Year) * 12 + d.Month - LaunchDate.Month;

        /// <summary>
        /// One full orbit from the track's first sample: samples until the angle swept round the Sun reaches
        /// a full turn, closed back to the start. A track shorter than one orbit is drawn as it is.
        /// </summary>
        private static Vector2[] OneOrbit(MonitorSnapshot.Track track)
        {
            var points = new List<Vector2> { new Vector2(track.points[0].x, track.points[0].y) };
            float swept = 0f;
            for (int i = 1; i < track.points.Length; i++)
            {
                var p = new Vector2(track.points[i].x, track.points[i].y);
                swept += Mathf.Abs(Vector2.SignedAngle(points[points.Count - 1], p));
                if (swept >= FullTurnDeg) { points.Add(points[0]); break; }
                points.Add(p);
            }
            return points.ToArray();
        }

        private Vector2[] Project(Vector2[] au)
        {
            var panel = new Vector2[au.Length];
            for (int i = 0; i < au.Length; i++) panel[i] = _projection.ToPanel(au[i]);
            return panel;
        }

        // dots are anchored at the plot's centre: offset from it
        private void Place(RectTransform rt, Vector2 panel) => rt.anchoredPosition = panel - plot.rect.center;

        // coloured dot + label rows, top right of the plot
        private static void Legend(RectTransform plot, TMP_FontAsset font)
        {
            var rows = new[]
            {
                (MonitorPalette.White, "Sun"), (MonitorPalette.White, "Earth"), (MonitorPalette.Coral, "Mars"),
                (MonitorPalette.Mustard, "Psyche spacecraft"), (MonitorPalette.Grey, "Asteroid Psyche"),
            };
            var legend = Child(plot, "Legend");
            legend.anchorMin = legend.anchorMax = legend.pivot = Vector2.one;
            legend.sizeDelta = new Vector2(LegendWidth, LegendRow * rows.Length);
            legend.anchoredPosition = Vector2.zero;
            for (int i = 0; i < rows.Length; i++)
            {
                float y = -LegendRow * (i + 0.5f);
                if (i == 0)
                {
                    // the Sun's row carries the same Mustard halo as the plot's Sun
                    var halo = Halo(legend, "SunGlow", WithAlpha(MonitorPalette.Mustard, GlowAlpha), LegendSunGlow);
                    halo.anchorMin = halo.anchorMax = new Vector2(0f, 1f);
                    halo.anchoredPosition = new Vector2(LegendDot * 0.5f, y);
                }
                var dot = Child(legend, rows[i].Item2 + "Dot").gameObject.AddComponent<Image>();
                dot.sprite = MonitorSprites.Disc(); dot.color = rows[i].Item1; dot.raycastTarget = false;
                var drt = dot.rectTransform;
                drt.anchorMin = drt.anchorMax = new Vector2(0f, 1f);
                drt.sizeDelta = Vector2.one * LegendDot;
                drt.anchoredPosition = new Vector2(LegendDot * 0.5f, y);
                var label = MonitorScreen.Text(legend, rows[i].Item2 + "Label", font, LegendSize, TextAlignmentOptions.MidlineLeft, MonitorPalette.LightGrey);
                label.text = rows[i].Item2;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                var lrt = label.rectTransform;
                lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = Vector2.one;
                lrt.pivot = new Vector2(0f, 0.5f);
                lrt.offsetMin = new Vector2(LegendDot + LegendGap, y - LegendRow * 0.5f);
                lrt.offsetMax = new Vector2(0f, y + LegendRow * 0.5f);
            }
        }

        private static Color Faint(Color c) => WithAlpha(c, OrbitAlpha);
        private static bool HasPoints(MonitorSnapshot.Track t) => t != null && t.points != null && t.points.Length > 0;
        private static DateTime ParseDate(string s) => DateTime.ParseExact(s, DateFormat, CultureInfo.InvariantCulture);

    }
}
