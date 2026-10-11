using System;
using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Everything the ops monitor shows, in one asset: tab titles, stats, captions, credits, images and
    /// the build-date snapshot. Change a fact here and nowhere else. Sources are in the design doc §4-§5.
    /// Create via: Assets > Create > Psyche VR > Monitor Content
    /// </summary>
    [CreateAssetMenu(fileName = "MonitorContent", menuName = "Psyche VR/Monitor Content")]
    public class MonitorContent : ScriptableObject
    {
        [Serializable]
        public class Stat
        {
            public string value;
            public string label;
        }

        [Serializable]
        public class Photo
        {
            public Sprite sprite;
            [TextArea] public string caption;
            public string credit;
        }

        [Serializable]
        public class Tab
        {
            public string title;
            [Tooltip("Scrolls across the top when it does not fit.")]
            [TextArea] public string banner;
            [TextArea] public string footer;
            public Photo[] mainPhotos;
            public Photo[] topPhotos;
            public Photo[] bottomPhotos;
            public Stat[] stats;
        }

        [Tooltip("Build-date snapshot from docs/superpowers/scripts/fetch_monitor_data.py (JSON).")]
        public TextAsset snapshot;

        [Header("Fonts")]
        public TMPro.TMP_FontAsset titleFont;
        public TMPro.TMP_FontAsset bodyFont;

        [Header("Sounds")]
        public AudioClip pageClick;
        public AudioClip handoffChime;

        [Header("Tabs, in order")]
        public Tab marsFlyby = new Tab();
        public Tab deepSpaceNetwork = new Tab();
        public Tab solarPower = new Tab();
        public Tab thruster = new Tab();

        [Header("Solar Power")]
        [Tooltip("The spacecraft drifting along the power track.")]
        public Sprite spacecraftIcon;
        [Tooltip("Credit for the spacecraft icon; shown in the Solar Power tab's footer credits.")]
        public string spacecraftIconCredit;

        [Header("Thruster push (mouse)")]
        [Tooltip("Header pop-up bar on the Thruster tab until the visitor first grabs the mouse.")]
        [TextArea] public string pushPrompt;
        [Tooltip("Header pop-up bar while the mouse rumbles.")]
        [TextArea] public string pushRumbleLine;

        [Header("Ping (DSN tab)")]
        [Tooltip("Dish end of the X-band link on the DSN main illustration, normalised image coordinates (0..1, origin bottom-left).")]
        public Vector2 pingLinkStart = new Vector2(0.28f, 0.34f);
        [Tooltip("Spacecraft end of the X-band link, normalised image coordinates.")]
        public Vector2 pingLinkEnd = new Vector2(0.69f, 0.63f);
        [Tooltip("Footer while the ping is in flight.")]
        public string pingSentLine = "Ping sent. Waiting for Psyche to answer.";
        [Tooltip("Footer when the reply lands: {0} = real round trip in whole minutes, {1} = seconds the visitor waited.")]
        public string pingResultFormat = "Round trip: {0} min. You waited {1} seconds. NASA waits the full {0}.";
        [Tooltip("Second footer line when the reply lands: {0} = local clock time a signal sent now reaches Psyche.")]
        public string pingArrivalFormat = "Signal arrives at Psyche at {0}.";

        [Header("Kiosk")]
        public string attractPrompt = "Slap ENTER";

        /// <summary>Tabs in screen order.</summary>
        public Tab[] Tabs => new[] { marsFlyby, deepSpaceNetwork, solarPower, thruster };
    }
}
