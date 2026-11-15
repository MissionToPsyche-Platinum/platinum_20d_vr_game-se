using System.ComponentModel.Design.Serialization;
using UnityEngine;

namespace PsycheVR.Story
{
    /// <summary>Editable story copy. Add later phases' text here as they are implemented.</summary>
    [CreateAssetMenu(fileName = AssetName, menuName = "Psyche VR/Story Text")]
    public sealed class StoryText : ScriptableObject
    {
        public const string AssetName = "StoryText";

        [Header("Phase 1 - Introduction")]
        [TextArea(2, 4)]
        [SerializeField] private string introDateText = "October 13, 2023";
        //fade in then...
        [SerialzeField] private string introOpening = "I always knew I wanted to work at NASA..."
        //fade out after completing goal
        [SerialzeField] private string introEnding = "Ever since I was a little kid..."

        //////////////////////////////////////

        [Header("Phase 2 - Build")]
        [SerializeField] private string buildDateText = "October, 2003";
        //After finishing building model
        [SerializeField] private string buildComplete = "Spacecraft Ready to Launch!";

        //After pressing launchbutton on desk
        [SerializeField] private string buildEnding = "Back then was just for fun... Now, I get to do the real thing";

 
        /////////////////////////////////////


        public string IntroDateText => introDateText;
        public string IntroOpening => introOpening;
        public string IntroEnding => introEnding;

    }
}
