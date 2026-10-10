using System;
using PsycheVR.OpsConsole.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PsycheVR.Gameplay.MonitorUi;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// The ping over the DSN main illustration (TG-227): a glowing Gold dot runs along the X-band link from
    /// the dish to the spacecraft, blinks there with a ring burst, and runs back, on a
    /// <see cref="PingTimeline"/>. A large counter top-left shows the simulated real time and a status line
    /// names the phase. The link's two ends come from the content (normalised image coordinates) and are
    /// placed through <see cref="PhotoPanel.TryImageToLocal"/>, so the photo's cover crop is allowed for;
    /// without a photo they span the panel itself. Hidden until <see cref="Begin"/>; advances only
    /// through <see cref="Tick"/>. After landing the counter and status stay until <see cref="Stop"/>.
    /// </summary>
    public class PingView : MonoBehaviour
    {
        // layout, mm
        private const float Pad = 8f;
        private const float DotSize = 8f, CoreSize = 3f, GlowSize = 30f, GlowAlpha = 0.7f;
        private const float BurstStartSize = 6f, BurstEndSize = 30f;
        private const float LabelSize = 7f, LabelHeight = 9f;
        private const float CounterSize = 34f, CounterHeight = 40f;
        private const float StatusSize = 8f, StatusHeight = 11f;
        private const float BlockWidth = 120f;
        private const float Backing = 0.55f;          // alpha of the dark backing behind the counter block
        private const int BlinkCount = 3;             // on/off cycles while at the spacecraft

        private const string LabelText = "REAL SIGNAL TIME";
        private static readonly string[] StatusText =
            { "Ping going out to Psyche", "Psyche answers", "Reply coming home", "Reply received" };

        [SerializeField] private RectTransform dot;
        [SerializeField] private Image glow, burst;
        [SerializeField] private TMP_Text counter, status;

        private PhotoPanel _photo;
        private Vector2 _start, _end;   // normalised image coordinates
        private PingTimeline _timeline;
        private Action _onLanded;
        private float _clock;
        private bool _running;
        private long _shownSecond = -1;
        private PingPhase _shownPhase = (PingPhase)(-1);

        /// <summary>The ping on screen, or null when none has started since the last <see cref="Stop"/>.</summary>
        public PingTimeline Timeline => _timeline;

        /// <summary>True from <see cref="Begin"/> until the reply lands or <see cref="Stop"/>.</summary>
        public bool Running => _running;

        /// <summary>
        /// Creates the view stretched over <paramref name="parent"/>, above <paramref name="photo"/> (may be
        /// null), hidden. Fonts and the link ends come from <paramref name="content"/>.
        /// </summary>
        public static PingView Create(RectTransform parent, MonitorContent content, PhotoPanel photo)
        {
            var rt = Child(parent, "Ping");
            Stretch(rt);
            // own canvas: the per-frame dot move does not rebuild the rest of the monitor
            rt.gameObject.AddComponent<Canvas>().overrideSorting = false;
            var view = rt.gameObject.AddComponent<PingView>();
            view._photo = photo;
            view._start = content != null ? content.pingLinkStart : Vector2.zero;
            view._end = content != null ? content.pingLinkEnd : Vector2.one;
            var titleFont = content != null ? content.titleFont : null;
            var bodyFont = content != null ? content.bodyFont : null;

            view.burst = Dot(rt, "Burst", MonitorPalette.Gold, BurstStartSize).GetComponent<Image>();
            view.burst.sprite = MonitorSprites.Ring();
            view.glow = Halo(rt, "Glow", WithAlpha(MonitorPalette.Gold, GlowAlpha), GlowSize).GetComponent<Image>();
            view.dot = Dot(view.glow.rectTransform, "Dot", MonitorPalette.Gold, DotSize);
            Dot(view.dot, "Core", MonitorPalette.White, CoreSize);

            // counter block, top-left over the illustration's empty sky
            var block = Child(rt, "Counter");
            block.anchorMin = block.anchorMax = block.pivot = new Vector2(0f, 1f);
            block.sizeDelta = new Vector2(BlockWidth, LabelHeight + CounterHeight + StatusHeight + Pad);
            block.anchoredPosition = new Vector2(Pad, -Pad);
            var back = block.gameObject.AddComponent<Image>();
            back.color = WithAlpha(MonitorPalette.Black, Backing); back.raycastTarget = false;

            var label = TextRow(block, "Label", bodyFont, LabelSize, MonitorPalette.Mustard, 0f, LabelHeight);
            label.text = LabelText;
            view.counter = TextRow(block, "Clock", titleFont, CounterSize, MonitorPalette.White, LabelHeight, CounterHeight);
            view.status = TextRow(block, "Status", bodyFont, StatusSize, MonitorPalette.White, LabelHeight + CounterHeight, StatusHeight);

            rt.gameObject.SetActive(false);
            return view;
        }

        /// <summary>Starts <paramref name="timeline"/> from the dish; <paramref name="onLanded"/> runs once when the reply lands.</summary>
        public void Begin(PingTimeline timeline, Action onLanded)
        {
            _timeline = timeline;
            _onLanded = onLanded;
            _clock = 0f;
            _shownSecond = -1;
            _shownPhase = (PingPhase)(-1);
            _running = timeline != null;
            gameObject.SetActive(_running);
            if (_running) Apply(0f);
        }

        /// <summary>Advances the running ping by <paramref name="dt"/> seconds; fires the landing callback at the end.</summary>
        public void Tick(float dt)
        {
            if (!_running) return;
            _clock += dt;
            Apply(_clock);
            if (_clock < _timeline.TotalSeconds) return;
            _running = false;
            var landed = _onLanded;
            _onLanded = null;
            landed?.Invoke();
        }

        /// <summary>Preview: shows the ping at <paramref name="seconds"/> without advancing it or firing the callback.</summary>
        public void SetTime(float seconds)
        {
            if (_timeline != null) Apply(seconds);
        }

        /// <summary>Cancels any ping (no callback) and hides the view.</summary>
        public void Stop()
        {
            _running = false;
            _onLanded = null;
            _timeline = null;
            gameObject.SetActive(false);
        }

        private void Apply(float t)
        {
            var phase = _timeline.PhaseAt(t);
            bool flying = phase != PingPhase.Done;
            glow.gameObject.SetActive(flying);
            if (flying) glow.rectTransform.localPosition = Vector2.Lerp(LinkPoint(_start), LinkPoint(_end), _timeline.PositionAt(t));

            bool blinking = phase == PingPhase.Blink;
            burst.gameObject.SetActive(blinking);
            if (blinking)
            {
                float f = _timeline.BlinkSeconds > 0f ? (t - _timeline.LegSeconds) / _timeline.BlinkSeconds : 1f;
                glow.gameObject.SetActive(Mathf.Repeat(f * BlinkCount, 1f) < 0.5f);
                var brt = burst.rectTransform;
                brt.localPosition = LinkPoint(_end);
                brt.sizeDelta = Vector2.one * Mathf.Lerp(BurstStartSize, BurstEndSize, f);
                SetAlpha(burst, 1f - f);
            }

            long second = (long)Math.Floor(_timeline.RealSecondsAt(t));
            if (second != _shownSecond)
            {
                _shownSecond = second;
                counter.text = PingTimeline.Clock(second);
            }
            if (phase != _shownPhase)
            {
                _shownPhase = phase;
                status.text = StatusText[(int)phase];
            }
        }

        /// <summary>A link end in this view's local space: on the photo when one shows, else across the view itself.</summary>
        private Vector2 LinkPoint(Vector2 normalised)
        {
            var self = (RectTransform)transform;
            if (_photo != null && _photo.gameObject.activeInHierarchy && _photo.TryImageToLocal(normalised, out var onPhoto))
                return self.InverseTransformPoint(_photo.transform.TransformPoint(onPhoto));
            Rect r = self.rect;
            return r.min + Vector2.Scale(normalised, r.size);
        }

        private static TMP_Text TextRow(RectTransform block, string name, TMP_FontAsset font, float size, Color colour, float top, float height)
        {
            var t = MonitorScreen.Text(block, name, font, size, TextAlignmentOptions.TopLeft, colour);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(Pad * 0.5f, -top - height - Pad * 0.5f);
            rt.offsetMax = new Vector2(-Pad * 0.5f, -top - Pad * 0.5f);
            return t;
        }
    }
}
