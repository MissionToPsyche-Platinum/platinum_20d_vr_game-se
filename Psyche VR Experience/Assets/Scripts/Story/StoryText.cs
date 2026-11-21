using UnityEngine;

namespace PsycheVR.Story
{
    /// <summary>Editable story copy. Add later phases' text here as they are implemented.</summary>
    [CreateAssetMenu(fileName = AssetName, menuName = "Psyche VR/Story Text")]
    public sealed class StoryText : ScriptableObject
    {
        public const string AssetName = "StoryText";

        [Header("Intro: launch day at the mission control desk")]
        [TextArea(2, 4)]
        [SerializeField] private string introDateText = "October 13, 2023";

        //fade in then...
        [SerializeField] private string introOpening = "I always knew I wanted to work at NASA...";

        //fade out after completing goal
        [SerializeField] private string introEnding = "Ever since I was a little kid...";

        //////////////////////////////////////

        [Header("Memory: arriving in the childhood bedroom")]
        [SerializeField] private string memoryDateText = "2003";
        [SerializeField] private string buildDateText = "October, 2003";

        [Header("Build: the bedroom puzzle")]
        //After finishing building model
        [SerializeField] private string buildComplete = "Spacecraft Ready to Launch!";

        [Header("ChildLaunch: the bedroom desk button")]
        //After pressing launchbutton on desk
        [SerializeField] private string buildEnding = "Back then was just for fun... Now, I get to do the real thing";

        /////////////////////////////////////

        [Header("RealLaunch: back at the mission control desk")]
        [SerializeField] private string launchDateText = "October, 2023";

        /////////////////////////////////////

        [Header("FreePlay: after the launch video")]
        [TextArea(2, 4)]
        [SerializeField]
        private string freeplayMessage =
            "Thank you for playing our game!\n" +
            "If you want to learn more about the Psyche mission, please visit\n" +
            "https://www.jpl.nasa.gov/missions/psyche/";

        /////////////////////////////////////


        //Intro getters
        public string IntroDateText => introDateText;
        public string IntroOpening => introOpening;
        public string IntroEnding => introEnding;

        //Memory, Build and ChildLaunch getters
        public string MemoryDateText => memoryDateText;
        public string BuildDateText => buildDateText;
        public string BuildComplete => buildComplete;
        public string BuildEnding => buildEnding;

        //RealLaunch getter
        public string LaunchDateText => launchDateText;

        //FreePlay getter
        public string FreeplayMessage => freeplayMessage;
    }
}
