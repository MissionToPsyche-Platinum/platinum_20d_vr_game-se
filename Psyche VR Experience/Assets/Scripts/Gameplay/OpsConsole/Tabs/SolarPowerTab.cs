using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Tab 3, Solar Power: the power figures in the main panel (the power drift animation replaces them
    /// in TG-196) and a photo in each of the top and bottom panels.
    /// </summary>
    public class SolarPowerTab : MonitorTab
    {
        /// <inheritdoc/>
        protected override void BuildPanels(Transform main, Transform top, Transform bottom)
        {
            Stats(main, Data.stats);
            Photos(top, Data.topPhotos);
            Photos(bottom, Data.bottomPhotos);
        }
    }
}
