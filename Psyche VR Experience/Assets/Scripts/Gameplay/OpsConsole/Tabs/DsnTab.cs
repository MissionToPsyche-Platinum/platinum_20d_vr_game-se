using PsycheVR.OpsConsole.Core;
using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Tab 2, Deep Space Network: a photo panel in the main and bottom panels, the turning-Earth handoff
    /// animation (<see cref="DsnComplexesView"/>) in the top panel, and a footer with the real round-trip
    /// light time from the build-date snapshot. Without a snapshot the footer shows without the number.
    /// The content's top photo (the old three-sites still) stays in the asset but is not shown.
    /// </summary>
    public class DsnTab : MonitorTab
    {
        private const string RoundTripToken = "{RT}";

        private string _footer;
        private DsnComplexesView _complexes;

        /// <inheritdoc/>
        protected override void BuildPanels(Transform main, Transform top, Transform bottom)
        {
            Photos(main, Data.mainPhotos);
            var console = Screen.GetComponent<OpsConsole>();
            // the chime is read at each handoff, so a clip swapped in the content asset plays at once
            _complexes = DsnComplexesView.Create((RectTransform)top, Screen.Content,
                () => { if (console != null && Screen.Content != null) console.PlaySound(Screen.Content.handoffChime); });
            Photos(bottom, Data.bottomPhotos);
            _footer = FillRoundTrip(Data.footer ?? "");
        }

        /// <summary>Shows the tab with Earth's turn restarted (Madrid facing Psyche).</summary>
        public override void Show()
        {
            base.Show();
            Screen.Footer.text = _footer;
            _complexes.Restart();
        }

        /// <inheritdoc/>
        public override void Tick(float dt) => _complexes.Tick(dt);

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
