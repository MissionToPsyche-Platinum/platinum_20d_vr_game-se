using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>Psyche brand colours used on the ops monitor (Psyche Brand Guide; see the design doc §3).</summary>
    public static class MonitorPalette
    {
        public static readonly Color Black = Hex(0x12031D);
        public static readonly Color DarkPurple = Hex(0x302144);
        public static readonly Color Purple = Hex(0x592651);
        public static readonly Color Mustard = Hex(0xF9A000);
        public static readonly Color Gold = Hex(0xF47C33);
        public static readonly Color Coral = Hex(0xEF5966);
        public static readonly Color Grey = Hex(0x88818E);
        public static readonly Color LightGrey = Hex(0xC4C0C6);
        public static readonly Color White = Color.white;

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
