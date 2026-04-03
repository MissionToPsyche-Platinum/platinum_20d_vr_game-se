using UnityEngine;

namespace PsycheVR.UI
{
    /// <summary>
    /// Shared tuning parameters for the chalkboard display system.
    /// Create via: Assets > Create > Psyche VR > Chalkboard Settings
    /// </summary>
    [CreateAssetMenu(fileName = "ChalkboardSettings", menuName = "Psyche VR/Chalkboard Settings")]
    public class ChalkboardSettings : ScriptableObject
    {
        [Header("Animation")]
        [Tooltip("Duration of the fade transition between text changes.")]
        [SerializeField, Range(0.05f, 1f)] private float fadeSpeed = 0.25f;

        [Tooltip("Scale multiplier for the name punch animation on snap.")]
        [SerializeField, Range(1f, 1.5f)] private float snapPunchScale = 1.15f;

        [Header("Highlight")]
        [Tooltip("Color applied to a piece when grabbed/held.")]
        [SerializeField] private Color highlightColor = new Color(0.4f, 0.8f, 1f, 1f);

        [Tooltip("Color applied to a piece after snapping into place.")]
        [SerializeField] private Color snappedColor = new Color(0.2f, 1f, 0.2f, 1f);

        [Tooltip("Emission intensity when a piece is highlighted.")]
        [SerializeField, Range(0f, 1f)] private float highlightEmission = 0.3f;

        public float FadeSpeed => fadeSpeed;
        public float SnapPunchScale => snapPunchScale;
        public Color HighlightColor => highlightColor;
        public Color SnappedColor => snappedColor;
        public float HighlightEmission => highlightEmission;
    }
}
