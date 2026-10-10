using PsycheVR.OpsConsole.Core;
using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Tab 2, Deep Space Network: a photo panel in the main and bottom panels, the turning-Earth handoff
    /// animation (<see cref="DsnComplexesView"/>) in the top panel, and a footer with the real round-trip
    /// light time from the build-date snapshot. Without a snapshot the footer shows without the number.
    /// The content's top photo (the old three-sites still) stays in the asset but is not shown.
    /// The ping (TG-227) runs over the main illustration through <see cref="PingView"/>; the console
    /// starts and cancels it.
    /// </summary>
    public class DsnTab : MonitorTab
    {
        private const string RoundTripToken = "{RT}";

        private string _footer;
        private DsnComplexesView _complexes;
        private PingView _ping;

        /// <inheritdoc/>
        protected override void BuildPanels(Transform main, Transform top, Transform bottom)
        {
            var illustration = Photos(main, Data.mainPhotos);
            _ping = PingView.Create((RectTransform)main, Screen.Content, illustration);
            var console = Screen.GetComponent<OpsConsole>();
            // the chime is read at each handoff, so a clip swapped in the content asset plays at once
            _complexes = DsnComplexesView.Create((RectTransform)top, Screen.Content,
                () => { if (console != null && Screen.Content != null) console.PlaySound(Screen.Content.handoffChime); });
            Photos(bottom, Data.bottomPhotos);
            _footer = FillRoundTrip(Data.footer ?? "");
        }

        /// <summary>True when a ping can run: the snapshot gives the real light time.</summary>
        public bool CanPing => Snapshot != null;

        /// <summary>The ping view (for previews).</summary>
        public PingView Ping => _ping;

        /// <summary>
        /// Shows the tab with Earth's turn restarted (Madrid facing Psyche). A running ping keeps going (a
        /// slide that lands on this tab must not cut it off); a finished one is cleared.
        /// </summary>
        public override void Show()
        {
            base.Show();
            Screen.Footer.text = _footer;
            _complexes.Restart();
            if (!_ping.Running) _ping.Stop();
        }

        /// <summary>Cancels any ping and hides the tab.</summary>
        public override void Hide()
        {
            _ping.Stop();
            base.Hide();
        }

        /// <inheritdoc/>
        public override void Tick(float dt)
        {
            _complexes.Tick(dt);
            _ping.Tick(dt);
        }

        /// <summary>
        /// Starts the ping for the snapshot's real round trip; <paramref name="onLanded"/> runs once when the
        /// reply lands. Returns the timeline, or null (nothing starts) without a snapshot.
        /// </summary>
        public PingTimeline BeginPing(System.Action onLanded)
        {
            var snapshot = Snapshot;
            if (snapshot == null) return null;
            var timeline = PingTimeline.ForRoundTrip(RoundTrip.Seconds(snapshot.oneWayLightSeconds));
            _ping.Begin(timeline, onLanded);
            return timeline;
        }

        /// <summary>Stops a ping in flight without its landing callback.</summary>
        public void CancelPing()
        {
            if (_ping != null) _ping.Stop();   // may already be destroyed when the console tears down
        }

        private string FillRoundTrip(string footer)
        {
            if (!footer.Contains(RoundTripToken)) return footer;
            var snapshot = Snapshot;
            if (snapshot != null)
                return footer.Replace(RoundTripToken,
                    RoundTrip.WholeMinutes(RoundTrip.Seconds(snapshot.oneWayLightSeconds)).ToString());
            // no number: drop the token and the space before it
            return footer.Replace(" " + RoundTripToken, "").Replace(RoundTripToken, "");
        }
    }
}
