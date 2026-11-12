using UnityEngine;

namespace PsycheVR.Story
{
    /// <summary>Editable story copy. Add later phases' text here as they are implemented.</summary>
    [CreateAssetMenu(fileName = AssetName, menuName = "Psyche VR/Story Text")]
    public sealed class StoryText : ScriptableObject
    {
        public const string AssetName = "StoryText";

        [Tooltip("Date displayed on entering Intro, set on Psyche's launch day.")]
        [TextArea(2, 4)]
        [SerializeField] private string introDateText = "October 13, 2023";

        [Tooltip("Date displayed on arrival in the childhood bedroom.")]
        [SerializeField] private string memoryDateText = "2003";

        public string MemoryDateText => memoryDateText;
        public string IntroDateText => introDateText;
    }
}
