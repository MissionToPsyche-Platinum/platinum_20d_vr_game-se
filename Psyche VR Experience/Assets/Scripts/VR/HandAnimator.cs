using UnityEngine;
using UnityEngine.InputSystem;

namespace PsycheVR.VR
{
    /// <summary>
    /// Reads XRI controller input actions and drives hand animation parameters.
    /// Attach to each hand model prefab alongside an Animator component.
    /// Action lifecycle is managed by the XRI Input Action Manager — this script
    /// only reads values, it does not enable or disable actions.
    /// </summary>
    public class HandAnimator : MonoBehaviour
    {
        [Header("Input Action References")]
        [Tooltip("Grip/Select action (float 0-1). Map to 'XRI LeftHand Interaction / Select Value'.")]
        [SerializeField] private InputActionReference gripAction;

        [Tooltip("Trigger/Activate action (float 0-1). Map to 'XRI LeftHand Interaction / Activate Value'.")]
        [SerializeField] private InputActionReference triggerAction;

        [Tooltip("Thumbstick or button touch (float 0-1). Map to 'XRI LeftHand Interaction / Thumbstick Touch Value'.")]
        [SerializeField] private InputActionReference thumbTouchAction;

        [Header("Animation")]
        [Tooltip("How quickly finger animation catches up to input. Higher = snappier.")]
        [SerializeField] private float animationSpeed = 10f;

        private Animator _animator;

        private static readonly int GripHash = Animator.StringToHash("Grip");
        private static readonly int TriggerHash = Animator.StringToHash("Trigger");
        private static readonly int ThumbTouchHash = Animator.StringToHash("ThumbTouch");

        private float _currentGrip;
        private float _currentTrigger;
        private float _currentThumb;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                Debug.LogError($"[HandAnimator] No Animator found on {gameObject.name}.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_animator == null) return;

            float targetGrip = gripAction?.action?.ReadValue<float>() ?? 0f;
            float targetTrigger = triggerAction?.action?.ReadValue<float>() ?? 0f;
            float targetThumb = thumbTouchAction?.action?.ReadValue<float>() ?? 0f;

            float lerpFactor = animationSpeed * Time.deltaTime;
            _currentGrip = Mathf.Lerp(_currentGrip, targetGrip, lerpFactor);
            _currentTrigger = Mathf.Lerp(_currentTrigger, targetTrigger, lerpFactor);
            _currentThumb = Mathf.Lerp(_currentThumb, targetThumb, lerpFactor);

            _animator.SetFloat(GripHash, _currentGrip);
            _animator.SetFloat(TriggerHash, _currentTrigger);
            _animator.SetFloat(ThumbTouchHash, _currentThumb);
        }
    }
}