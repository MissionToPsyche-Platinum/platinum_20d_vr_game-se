using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Toggles gravity on the spine Rigidbody for grab/release.
    /// On release the book falls; on grab it floats to the hand.
    /// </summary>
    [RequireComponent(typeof(PsycheGrabbable))]
    [RequireComponent(typeof(Rigidbody))]
    public class BookGravityToggle : MonoBehaviour
    {
        private PsycheGrabbable _grabbable;
        private Rigidbody _rb;

        private void Awake()
        {
            _grabbable = GetComponent<PsycheGrabbable>();
            _rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            // Override PsycheGrabbable's default (useGravity = false).
            _rb.useGravity = true;
        }

        private void OnEnable()
        {
            _grabbable.selectEntered.AddListener(OnGrabbed);
            _grabbable.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            _grabbable.selectEntered.RemoveListener(OnGrabbed);
            _grabbable.selectExited.RemoveListener(OnReleased);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            _rb.useGravity = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            _rb.useGravity = true;
        }
    }
}
