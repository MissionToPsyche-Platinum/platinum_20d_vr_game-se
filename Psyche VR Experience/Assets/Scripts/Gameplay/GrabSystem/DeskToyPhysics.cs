using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Physics behavior for small desk props such as the pen. Add alongside
    /// PsycheGrabbable. Like BasketballPhysics it turns gravity back on so the
    /// prop falls when released, but the release velocity is scaled well down:
    /// a desk toy should drop close to where it was let go instead of flying
    /// across the room the way a thrown basketball is meant to.
    /// </summary>
    [RequireComponent(typeof(PsycheGrabbable))]
    public class DeskToyPhysics : MonoBehaviour
    {
        [Tooltip("Multiplier on controller velocity at release. Below 1 so the prop drops instead of being thrown.")]
        [SerializeField, Range(0f, 1f)] private float throwVelocityScale = 0.35f;

        [Tooltip("Multiplier on angular velocity at release. Low values stop the prop tumbling away.")]
        [SerializeField, Range(0f, 1f)] private float throwAngularVelocityScale = 0.2f;

        private PsycheGrabbable _grabbable;
        private Rigidbody _rb;

        private void Awake()
        {
            _grabbable = GetComponent<PsycheGrabbable>();
            _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            // Override PsycheGrabbable defaults, which leave props floating.
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
