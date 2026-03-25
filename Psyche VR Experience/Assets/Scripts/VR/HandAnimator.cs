using UnityEngine;
using UnityEngine.InputSystem;

namespace PsycheVR.VR
{
    /// <summary>
    /// Reads grip and trigger input, takes the max, and drives a single Fist
    /// animator parameter for open-to-grab hand animation.
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

        private Animator _animator;
        private static readonly int FistHash = Animator.StringToHash("Fist");
        private float _currentFist;

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

            float grip = gripAction?.action?.ReadValue<float>() ?? 0f;
            float trigger = triggerAction?.action?.ReadValue<float>() ?? 0f;
            float target = Mathf.Max(grip, trigger);

            _currentFist = Mathf.Lerp(_currentFist, target, animationSpeed * Time.deltaTime);
            _animator.SetFloat(FistHash, _currentFist);
        }
    }
}
