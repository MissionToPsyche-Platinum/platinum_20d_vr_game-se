using UnityEngine;

namespace PsycheVR.UI
{
    /// <summary>
    /// ScriptableObject holding educational data for a spacecraft component.
    /// Create via Assets > Create > Psyche VR > Component Data.
    /// Assign to ComponentInfo on each piece and reuse across scenes.
    /// </summary>
    [CreateAssetMenu(fileName = "NewComponent", menuName = "Psyche VR/Component Data")]
    public class ComponentData : ScriptableObject
    {
        [Tooltip("Display name of this spacecraft component.")]
        public string componentName = "Component";

        [Tooltip("Educational info shown on the chalkboard.")]
        [TextArea(3, 8)]
        public string description = "Description of this component.";

        [Tooltip("Optional icon/image for UI display.")]
        public Sprite icon;
    }
}
