using System;
using UnityEngine;

namespace PsycheVR.Data
{
    /// <summary>Operator-assigned name stored on this headset for future uploads.</summary>
    public static class SessionDeviceName
    {
        private const string PreferenceKey = "PsycheVR.SessionDeviceName";

        public static string Current => PlayerPrefs.GetString(PreferenceKey,
            string.IsNullOrWhiteSpace(SystemInfo.deviceName) ? "Unnamed device" : SystemInfo.deviceName);

        /// <summary>Sets a name for the TG-269 menu; blank resets to Unity's device name.</summary>
        public static void Set(string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length > 256) throw new ArgumentException("Device names must be at most 256 characters.", nameof(value));
            if (value.Length == 0) PlayerPrefs.DeleteKey(PreferenceKey);
            else PlayerPrefs.SetString(PreferenceKey, value);
            PlayerPrefs.Save();
        }
    }
}
