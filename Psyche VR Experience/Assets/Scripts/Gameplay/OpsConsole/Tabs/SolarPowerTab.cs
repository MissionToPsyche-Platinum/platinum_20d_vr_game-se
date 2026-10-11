using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Tab 3, Solar Power: the power drift animation in the main panel (array power and thruster share
    /// falling as the spacecraft moves out from the Sun) and a photo in each of the top and bottom panels.
    /// The asset's solar stats are not shown here.
    /// </summary>
    public class SolarPowerTab : MonitorTab
    {
        private PowerDriftView _drift;

        /// <inheritdoc/>
        protected override void BuildPanels(Transform main, Transform top, Transform bottom)
        {
            _drift = PowerDriftView.Create((RectTransform)main, Screen.Content);
            if (Screen.Content.spacecraftIcon != null) AddCredit(Screen.Content.spacecraftIconCredit);
            Photos(top, Data.topPhotos);
            Photos(bottom, Data.bottomPhotos);
        }

        /// <summary>Shows the tab with the drift restarted from 1 AU.</summary>
        public override void Show()
        {
            base.Show();
            _drift.Restart();
        }

        /// <inheritdoc/>
        public override void Tick(float dt) => _drift.Tick(dt);
    }
}
