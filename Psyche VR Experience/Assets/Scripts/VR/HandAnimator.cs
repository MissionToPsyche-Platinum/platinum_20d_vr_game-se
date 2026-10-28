using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

namespace PsycheVR.VR
{
    /// <summary>
    /// Reads grip and trigger input, takes the max, and drives the Fist animator
    /// parameter for the open-to-fist pose (either control grabs). The Pinch layer,
    /// weighted only by <see cref="Pinch"/>, overrides that with the page-turning
    /// pinch while HandPresence reports a page in the hand.
    ///
    /// Only physical controller axes count. The XRI grip action is also bound to Meta hand tracking's
    /// pinch strength, and when the headset stops tracking bare hands that pinch value freezes; the
    /// action then falls back to it on every grip release and the glove stays curled (seen after
    /// resets, when the controllers are put down and picked up again).
    /// </summary>
    public class HandAnimator : MonoBehaviour
    {
        [Header("Input Action References")]
        [Tooltip("Grip/Select action (float 0-1).")]
        [SerializeField] private InputActionReference gripAction;

        [Tooltip("Trigger/Activate action (float 0-1).")]
        [SerializeField] private InputActionReference triggerAction;

        [Header("Animation")]
        [Tooltip("How quickly animation catches up to input. Higher = snappier.")]
        [SerializeField] private float animationSpeed = 10f;

        /// <summary>Name of the animator layer holding the pinch pose.</summary>
        private const string PinchLayerName = "Pinch";

        private Animator _animator;
        private static readonly int FistHash = Animator.StringToHash("Fist");
        private int _pinchLayer = -1;
        private float _currentFist;
        private float _currentPinch;

        /// <summary>The fist amount the glove is showing now, 0 open to 1 closed (after smoothing).</summary>
        public float CurrentFist => _currentFist;

        /// <summary>Target pinch amount, 0 open to 1 pinched. HandPresence sets it while a page is held.</summary>
        public float Pinch { get; set; }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                Debug.LogError($"[HandAnimator] No Animator found on {gameObject.name}.", this);
                enabled = false;
                return;
            }

            _pinchLayer = _animator.GetLayerIndex(PinchLayerName);
        }

        private void Update()
        {
            if (_animator == null) return;

            float grip = ControllerValue(gripAction);
            float trigger = ControllerValue(triggerAction);

            float target = Mathf.Max(grip, trigger);

            float step = animationSpeed * Time.deltaTime;
            _currentFist = Mathf.Lerp(_currentFist, target, step);
            _animator.SetFloat(FistHash, _currentFist);

            if (_pinchLayer >= 0)
            {
                _currentPinch = Mathf.Lerp(_currentPinch, Pinch, step);
                _animator.SetLayerWeight(_pinchLayer, _currentPinch);
            }
        }

        /// <summary>The highest value among the action's controls on an XR controller; hand-tracking inputs are left out.</summary>
        private static float ControllerValue(InputActionReference reference)
        {
            var action = reference != null ? reference.action : null;
            if (action == null) return 0f;
            float value = 0f;
            foreach (var control in action.controls)
                if (control.device is XRController && control is AxisControl axis)
                    value = Mathf.Max(value, axis.ReadValue());
            return value;
        }
    }
}
