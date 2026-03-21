using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Basketball-specific physics behavior. Add alongside PsycheGrabbable.
    /// Toggles gravity on grab/release and enables throw velocity so the
    /// ball falls, bounces, and can be shot like a real basketball.
    /// </summary>
    [RequireComponent(typeof(PsycheGrabbable))]
    public class BasketballPhysics : MonoBehaviour
    {
        [Tooltip("Multiplier on controller velocity at release. Higher = stronger shots.")]
        [SerializeField] private float throwVelocityScale = 1.5f;

        [Tooltip("Multiplier on angular velocity at release. Preserves spin.")]
        [SerializeField] private float throwAngularVelocityScale = 1.0f;

        private PsycheGrabbable _grabbable;
        private Rigidbody _rb;

        private void Awake()
        {
            _grabbable = GetComponent<PsycheGrabbable>();
            _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            // Override PsycheGrabbable defaults for basketball behavior.
            // Done in OnEnable to guarantee it runs after all Awake calls.
            _rb.useGravity = true;
            _grabbable.throwOnDetach = true;
            _grabbable.throwVelocityScale = throwVelocityScale;
            _grabbable.throwAngularVelocityScale = throwAngularVelocityScale;

            _grabbable.selectEntered.AddListener(OnGrabbed);
            _grabbable.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            _grabbable.selectEntered.RemoveListener(OnGrabbed);
            _grabbable.selectExited.RemoveListener(OnReleased);

            // Restore PsycheGrabbable defaults.
            _grabbable.throwOnDetach = false;
            _grabbable.throwVelocityScale = 1f;
            _grabbable.throwAngularVelocityScale = 1f;
            _rb.useGravity = false;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            _rb.useGravity = false;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            _rb.useGravity = true;
        }
    }
}
