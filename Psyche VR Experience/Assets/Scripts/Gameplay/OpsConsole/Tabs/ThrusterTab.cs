using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Tab 4, Thruster: photos in the main and top panels and the thrust stats bottom right.
    /// </summary>
    public class ThrusterTab : MonitorTab
    {
        /// <inheritdoc/>
        protected override void BuildPanels(Transform main, Transform top, Transform bottom)
        {
            Photos(main, Data.mainPhotos);
            Photos(top, Data.topPhotos);
            Stats(bottom, Data.stats);
        }
    }
}
