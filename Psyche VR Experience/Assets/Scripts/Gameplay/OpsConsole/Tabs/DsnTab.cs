using PsycheVR.OpsConsole.Core;
using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Tab 2, Deep Space Network: three photo panels and a footer with the real round-trip light time
    /// from the build-date snapshot. Without a snapshot the footer shows without the number.
    /// </summary>
    public class DsnTab : MonitorTab
    {
        private const string RoundTripToken = "{RT}";

        private string _footer;

        /// <inheritdoc/>
        protected override void BuildPanels(Transform main, Transform top, Transform bottom)
        {
            Photos(main, Data.mainPhotos);
            Photos(top, Data.topPhotos);
            Photos(bottom, Data.bottomPhotos);
            _footer = FillRoundTrip(Data.footer ?? "");
        }

        /// <inheritdoc/>
        public override void Show()
        {
            base.Show();
            Screen.Footer.text = _footer;
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
