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

        public string IntroDateText => introDateText;
    }
}
