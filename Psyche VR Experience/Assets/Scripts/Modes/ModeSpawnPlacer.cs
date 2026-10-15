using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace PsycheVR.Modes
{
    /// <summary>
    /// Moves the XR Origin to the <see cref="ModeStartPoint"/> for the active mode
    /// when the scene starts. Lives on the XR Origin. Runs after every scene load,
    /// including the reload <see cref="GameModeManager.SwitchTo"/> performs, so a
    /// mode switch lands the player at the other mode's start.
    ///
    /// Then, once the headset reports its first tracked pose, it shifts and turns the rig so the
    /// player's head (not the rig origin) stands over the start point facing its forward. With a
    /// room-scale boundary the rig origin is wherever the boundary's centre is, so a visitor standing
    /// off it would start off the spot; this puts every visitor, and every helper reset (which reloads
    /// the scene), on the same spot. A stationary boundary already recentres on the headset. The headset's
    /// own recentre (holding the Meta button) moves the tracking origin and fires the XR input subsystem's
    /// trackingOriginUpdated; the head is put back on the start spot then too.
    ///
    /// Scenes without any start points (test scenes) are left alone silently.
    /// </summary>
    public sealed class ModeSpawnPlacer : MonoBehaviour
    {
        private const string LogPrefix = "[ModeSpawnPlacer]";

        /// <summary>Longest wait (s, unscaled) for the headset's first tracked pose before centring anyway.</summary>
        private const float HeadWaitSeconds = 2f;

        [Tooltip("After placing the rig, put the player's head over the start point, facing its forward, as soon as the headset is tracked.")]
        [SerializeField] private bool centreHead = true;

        private readonly List<XRInputSubsystem> _subsystems = new List<XRInputSubsystem>();
        private Transform _startPoint;

        private IEnumerator Start()
        {
            var points = FindObjectsByType<ModeStartPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (points.Length == 0)
                yield break;

            var target = FindPointFor(points, GameModeManager.ActiveMode);
            if (target == null)
            {
                Debug.LogWarning($"{LogPrefix} No ModeStartPoint for {GameModeManager.ActiveMode} in '{gameObject.scene.name}'; rig left at scene position.");
                yield break;
            }

            PlaceAt(target.transform);
            Debug.Log($"{LogPrefix} Placed rig at '{target.name}' for {GameModeManager.ActiveMode} mode.");
            if (!centreHead)
                yield break;
            _startPoint = target.transform;
            yield return CentreHead(_startPoint);
            SubsystemManager.GetSubsystems(_subsystems);
            foreach (var subsystem in _subsystems)
                subsystem.trackingOriginUpdated += OnTrackingOriginUpdated;
        }

        private void OnDestroy()
        {
            foreach (var subsystem in _subsystems)
                subsystem.trackingOriginUpdated -= OnTrackingOriginUpdated;
            _subsystems.Clear();
        }

        /// <summary>The headset recentred (Meta button held): put the head back on the start spot once the new pose lands.</summary>
        private void OnTrackingOriginUpdated(XRInputSubsystem subsystem)
        {
            if (_startPoint != null && isActiveAndEnabled)
                StartCoroutine(CentreAfterRecentre());
        }

        private IEnumerator CentreAfterRecentre()
        {
            yield return null;   // the recentred pose reaches the camera on the next tracking update
            Debug.Log($"{LogPrefix} Headset recentred; centring the head again.");
            yield return CentreHead(_startPoint);
        }

        /// <summary>
        /// Turns the rig about the head until the head faces <paramref name="point"/>'s forward, then slides it
        /// so the head is over the point (height untouched). Waits for tracking first: until then the camera
        /// sits at its default pose and the correction would be wrong.
        /// </summary>
        private IEnumerator CentreHead(Transform point)
        {
            var origin = GetComponent<XROrigin>();
            if (origin == null || origin.Camera == null)
                yield break;

            var head = origin.Camera.transform;
            for (float waited = 0f; head.localPosition == Vector3.zero && waited < HeadWaitSeconds; waited += Time.unscaledDeltaTime)
                yield return null;

            Vector3 up = origin.transform.up;
            Vector3 facing = Vector3.ProjectOnPlane(head.forward, up), wanted = Vector3.ProjectOnPlane(point.forward, up);
            if (facing.sqrMagnitude > 1e-4f && wanted.sqrMagnitude > 1e-4f)
                origin.RotateAroundCameraUsingOriginUp(Vector3.SignedAngle(facing, wanted, up));
            origin.MoveCameraToWorldLocation(new Vector3(point.position.x, head.position.y, point.position.z));
            Debug.Log($"{LogPrefix} Head centred on '{point.name}'.");
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
