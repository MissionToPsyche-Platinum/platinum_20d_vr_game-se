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

        [Header("Thruster push (mouse)")]
        [TextArea] public string[] pushLines;

        [Header("Kiosk")]
        public string attractPrompt = "Slap ENTER";

        /// <summary>Tabs in screen order.</summary>
        public Tab[] Tabs => new[] { marsFlyby, deepSpaceNetwork, solarPower, thruster };
    }
}
