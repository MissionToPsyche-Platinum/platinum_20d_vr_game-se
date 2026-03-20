using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Shared tuning parameters for the grab system.
    /// All grabbable objects reference the same asset instance.
    /// Create via: Assets > Create > Psyche VR > Grab Settings
    /// </summary>
    [CreateAssetMenu(fileName = "GrabSettings", menuName = "Psyche VR/Grab Settings")]
    public class GrabSettings : ScriptableObject
    {
        [Header("Snap to Hand")]
        [Tooltip("Seconds for the grabbed object to ease into the hold position.")]
        [SerializeField, Range(1f, 20f)] private float snapEaseDuration = 10f;

        [Header("Haptics")]
        [Tooltip("Haptic vibration intensity when a piece is grabbed.")]
        [SerializeField, Range(0f, 1f)] private float grabHapticIntensity = 0.35f;

        [Tooltip("Duration of the haptic pulse (seconds).")]
        [SerializeField] private float hapticDuration = 0.08f;

        public float SnapEaseDuration => snapEaseDuration;
        public float GrabHapticIntensity => grabHapticIntensity;
        public float HapticDuration => hapticDuration;
    }
}
