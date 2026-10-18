using TMPro;
using UnityEngine;

namespace PsycheVR.Kiosk
{
    /// <summary>
    /// Fonts for the kiosk cards that are built in code at the moment they are needed
    /// (<see cref="HandoverMessage"/>), so they match the controller guide and the monitors. One asset, loaded
    /// from Resources by <see cref="ResourceName"/>.
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "Psyche VR/Kiosk Message Style")]
    public sealed class KioskMessageStyle : ScriptableObject
    {
        /// <summary>Name of the asset under a Resources folder.</summary>
        public const string ResourceName = "KioskMessageStyle";

        [Tooltip("Headings: Barlow Condensed SemiBold.")]
        [SerializeField] private TMP_FontAsset titleFont;

        [Tooltip("Body text: Inter Regular.")]
        [SerializeField] private TMP_FontAsset bodyFont;

        /// <summary>Heading font, or null.</summary>
        public TMP_FontAsset TitleFont => titleFont;

        /// <summary>Body font, or null.</summary>
        public TMP_FontAsset BodyFont => bodyFont;

        private static KioskMessageStyle s_Loaded;

        /// <summary>The project's style asset, or null when it is missing (the cards then fall back to plain text).</summary>
        public static KioskMessageStyle Load()
        {
            if (s_Loaded == null) s_Loaded = Resources.Load<KioskMessageStyle>(ResourceName);
            return s_Loaded;
        }
    }
}
