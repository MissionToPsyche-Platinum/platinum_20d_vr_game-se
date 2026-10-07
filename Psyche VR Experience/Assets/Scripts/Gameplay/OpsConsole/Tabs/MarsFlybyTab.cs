using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Tab 1, Mars Flyby: the main photo (the trajectory animation replaces it in TG-196), a rotating
    /// photo stack top right and the flyby stats bottom right.
    /// </summary>
    public class MarsFlybyTab : MonitorTab
    {
        private const float TopSecondsEach = 6f;

        /// <inheritdoc/>
        protected override void BuildPanels(Transform main, Transform top, Transform bottom)
        {
            Photos(main, Data.mainPhotos);
            Photos(top, Data.topPhotos, TopSecondsEach);
            Stats(bottom, Data.stats);
        }
    }
}
