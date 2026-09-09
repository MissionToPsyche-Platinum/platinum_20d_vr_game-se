using UnityEngine;

namespace PsycheVR.Modes
{
    /// <summary>
    /// Moves the XR Origin to the <see cref="ModeStartPoint"/> for the active mode
    /// when the scene starts. Lives on the XR Origin. Runs after every scene load,
    /// including the reload <see cref="GameModeManager.SwitchTo"/> performs, so a
    /// mode switch lands the player at the other mode's start.
    ///
    /// Scenes without any start points (test scenes) are left alone silently.
    /// </summary>
    public sealed class ModeSpawnPlacer : MonoBehaviour
    {
        private const string LogPrefix = "[ModeSpawnPlacer]";

        private void Start()
        {
            var points = FindObjectsByType<ModeStartPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (points.Length == 0)
                return;

            var target = FindPointFor(points, GameModeManager.ActiveMode);
            if (target == null)
            {
                Debug.LogWarning($"{LogPrefix} No ModeStartPoint for {GameModeManager.ActiveMode} in '{gameObject.scene.name}'; rig left at scene position.");
                return;
            }

            PlaceAt(target.transform);
            Debug.Log($"{LogPrefix} Placed rig at '{target.name}' for {GameModeManager.ActiveMode} mode.");
        }

        private static ModeStartPoint FindPointFor(ModeStartPoint[] points, GameMode mode)
        {
            foreach (var point in points)
            {
                if (point.Mode == mode)
                    return point;
            }
            return null;
        }

        private void PlaceAt(Transform point)
        {
            // Yaw only: the player's head supplies pitch and roll, and a tilted rig
            // is disorienting in VR.
            var yaw = point.rotation.eulerAngles.y;
            transform.SetPositionAndRotation(point.position, Quaternion.Euler(0f, yaw, 0f));
        }
    }
}
