using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Tab 1, Mars Flyby: the to-scale trajectory animation in the main panel (<see cref="TrajectoryView"/>,
    /// from the build-date snapshot; the content's main still stands in if the snapshot is missing), a
    /// rotating photo stack top right and the flyby stats bottom right.
    /// </summary>
    public class MarsFlybyTab : MonitorTab
    {
        private const float TopSecondsEach = 6f;
        private const string TrajectoryCredit = "Data: JPL Horizons";

        private TrajectoryView _trajectory;

        /// <inheritdoc/>
        protected override void BuildPanels(Transform main, Transform top, Transform bottom)
        {
            _trajectory = TrajectoryView.Create((RectTransform)main, Screen.Content, Snapshot);
            if (_trajectory != null) AddCredit(TrajectoryCredit);
            else Photos(main, Data.mainPhotos);
            Photos(top, Data.topPhotos, TopSecondsEach);
            Stats(bottom, Data.stats);
        }

        /// <summary>Shows the tab with the trajectory restarted from launch.</summary>
        public override void Show()
        {
            base.Show();
            if (_trajectory != null) _trajectory.Restart();
        }

        /// <inheritdoc/>
        public override void Tick(float dt)
        {
            if (_trajectory != null) _trajectory.Tick(dt);
        }
    }
}
