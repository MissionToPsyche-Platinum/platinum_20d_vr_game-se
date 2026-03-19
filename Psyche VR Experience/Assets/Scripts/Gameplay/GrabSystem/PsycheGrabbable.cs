using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Universal grab component for all grabbable objects in Psyche VR.
    /// Configured for NearFarInteractor with VelocityTracking.
    /// Objects are grabbed and smoothly pulled to the controller's HoldPoint
    /// (created by PsycheHandSetup).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PsycheGrabbable : XRGrabInteractable
    {
        [Header("Psyche VR")]
        [Tooltip("Shared grab settings asset. Must be assigned.")]
        [SerializeField] private GrabSettings grabSettings;

        /// <inheritdoc />
        protected override void Awake()
        {
            if (grabSettings == null)
            {
                Debug.LogError("[PsycheGrabbable] GrabSettings is not assigned!", this);
                enabled = false;
                return;
            }

            // Configure before base.Awake() so XRI initializes correctly.
            movementType = MovementType.VelocityTracking;
            throwOnDetach = false;
            useDynamicAttach = false;
            attachEaseInTime = grabSettings.SnapEaseDuration;

            base.Awake();

            ConfigureRigidbody();
        }

        private void ConfigureRigidbody()
        {
            var rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        /// <inheritdoc />
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            SendGrabHaptic(args);
        }

        private void SendGrabHaptic(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRBaseInputInteractor inputInteractor)
            {
                inputInteractor.SendHapticImpulse(
                    grabSettings.GrabHapticIntensity,
                    grabSettings.HapticDuration
                );
            }
        }
    }
}
