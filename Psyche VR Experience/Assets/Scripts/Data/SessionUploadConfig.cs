using System;
using UnityEngine;

namespace PsycheVR.Data
{
    /// <summary>Local build configuration, copied into a gitignored Resources JSON asset.</summary>
    [Serializable]
    public sealed class SessionUploadConfig
    {
        public const string ResourceName = "SessionUploadConfig";
        public string endpointUrl;
        public string token;

        public bool IsValid => Uri.TryCreate(endpointUrl, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) &&
            string.IsNullOrEmpty(uri.Fragment) && !string.IsNullOrWhiteSpace(token);

        public static SessionUploadConfig Load()
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null) return null;
            try
            {
                var config = JsonUtility.FromJson<SessionUploadConfig>(asset.text);
                return config != null && config.IsValid ? config : null;
            }
            catch (ArgumentException) { return null; }
        }
    }
}
