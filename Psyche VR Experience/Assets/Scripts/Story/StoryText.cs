using UnityEngine;

namespace PsycheVR.Story
{
    /// <summary>Editable story copy. Add later phases' text here as they are implemented.</summary>
    [CreateAssetMenu(fileName = AssetName, menuName = "Psyche VR/Story Text")]
    public sealed class StoryText : ScriptableObject
    {
        public const string AssetName = "StoryText";

        [Tooltip("Date displayed on entering Intro. Replace the placeholder with approved story copy.")]
        [TextArea(2, 4)]
        [SerializeField] private string introDateText = "[Intro date pending]";

        public string IntroDateText => introDateText;
    }
}
