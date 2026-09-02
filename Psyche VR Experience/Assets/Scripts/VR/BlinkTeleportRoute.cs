using UnityEngine;

namespace PsycheVR.VR
{
    /// <summary>
    /// Alternates the player between two fixed <see cref="BlinkTeleportAnchor"/> points
    /// (Mission Control and the bedroom) using the rig's <see cref="BlinkTeleporter"/>.
    ///
    /// Lives on a scene GameObject, not on the XR Rig prefab: a prefab cannot hold
    /// references to scene objects, so anchor slots assigned on the prefab would clear
    /// themselves on every reload.
    /// </summary>
    [DisallowMultipleComponent]
    public class BlinkTeleportRoute : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The rig's BlinkTeleporter. Auto-filled from the scene on Reset.")]
        [SerializeField] private BlinkTeleporter teleporter;

        [Tooltip("Landing point inside Mission Control.")]
        [SerializeField] private BlinkTeleportAnchor missionControlAnchor;

        [Tooltip("Landing point inside the bedroom.")]
        [SerializeField] private BlinkTeleportAnchor bedroomAnchor;

        private void Reset()
        {
            teleporter = FindFirstObjectByType<BlinkTeleporter>();
        }

        /// <summary>
        /// Teleports to whichever anchor the player is currently farther from, so each
        /// call moves them to the other room.
        ///
        /// The destination is derived from the rig's live position rather than a stored
        /// flag. A flag desynchronises the moment anything else repositions the rig — a
        /// pause-menu restart, a respawn, or dragging the rig in the editor — and would
        /// then send the player to the room they are already standing in.
        /// </summary>
        [ContextMenu("Trigger")]
        public void Trigger()
        {
            if (!HasRequiredReferences())
                return;

            Vector3 rigPosition = teleporter.RigPosition;

            float toMissionControl = (rigPosition - missionControlAnchor.Position).sqrMagnitude;
            float toBedroom = (rigPosition - bedroomAnchor.Position).sqrMagnitude;

            // Ties resolve to Mission Control so the behaviour stays deterministic.
            Go(toBedroom > toMissionControl ? bedroomAnchor : missionControlAnchor);
        }

        /// <summary>Teleports to Mission Control regardless of where the player is.</summary>
        [ContextMenu("Go To Mission Control")]
        public void GoToMissionControl()
        {
            if (HasRequiredReferences())
                Go(missionControlAnchor);
        }

        /// <summary>Teleports to the bedroom regardless of where the player is.</summary>
        [ContextMenu("Go To Bedroom")]
        public void GoToBedroom()
        {
            if (HasRequiredReferences())
                Go(bedroomAnchor);
        }

        private void Go(BlinkTeleportAnchor anchor)
        {
            teleporter.TeleportTo(anchor);
        }

        private bool HasRequiredReferences()
        {
            if (teleporter == null)
            {
                Debug.LogWarning("BlinkTeleportRoute: teleporter not assigned; ignoring.", this);
                return false;
            }

            if (missionControlAnchor == null || bedroomAnchor == null)
            {
                Debug.LogWarning("BlinkTeleportRoute: both anchors must be assigned; ignoring.", this);
                return false;
            }

            return true;
        }
    }
}
