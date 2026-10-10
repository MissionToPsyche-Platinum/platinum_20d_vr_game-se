using System;
using System.IO;
using PsycheVR.Data;
using UnityEditor;
using UnityEngine;

namespace PsycheVR.Modes.Editor
{
    /// <summary>Checks naming and serialization while restoring the operator's preference.</summary>
    public static class SessionDeviceNameChecks
    {
        private const string PreferenceKey = "PsycheVR.SessionDeviceName";

        [MenuItem("Tools/Session Logs/Check Device Name")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run device name checks in Edit mode.");
            bool hadName = PlayerPrefs.HasKey(PreferenceKey);
            string original = PlayerPrefs.GetString(PreferenceKey);
            string directory = Path.Combine(Path.GetTempPath(), "psyche-name-checks-" + Guid.NewGuid().ToString("N"));
            try
            {
                SessionDeviceName.Set(null);
                string fallback = string.IsNullOrWhiteSpace(SystemInfo.deviceName) ? "Unnamed device" : SystemInfo.deviceName;
                Require(SessionDeviceName.Current == fallback && !PlayerPrefs.HasKey(PreferenceKey), "Default Unity device name");
                SessionDeviceName.Set("  Quest \"A\" ☀  ");
                string name = "Quest \"A\" ☀";
                Require(SessionDeviceName.Current == name && PlayerPrefs.GetString(PreferenceKey) == name, "Trimmed name stored in PlayerPrefs");
                using (var writer = new SessionLogWriter(directory, "Event", "Checks", "test", "Editor", "test-build", SessionDeviceName.Current))
                {
                    string content = SessionUploadStore.Read(writer.FilePath);
                    var first = JsonUtility.FromJson<Record>(content.Trim());
                    Require(first.eventName == "session_start" && first.deviceName == name, "Session start records device name");
                    SessionDeviceName.Set("Quest B");
                    var payload = SessionUploadStore.Payload(writer.FilePath, content, "test-token", SessionDeviceName.Current);
                    var decoded = JsonUtility.FromJson<SessionUploadStore.UploadPayload>(JsonUtility.ToJson(payload));
                    Require(decoded.deviceName == "Quest B" && decoded.content == content, "Uploads use current name and preserve historical session name");
                }
                SessionDeviceName.Set(new string('x', 256));
                bool rejected = false;
                try { SessionDeviceName.Set(new string('x', 257)); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected && SessionDeviceName.Current.Length == 256, "Receiver length limit preserves previous name on rejection");
                SessionDeviceName.Set(" \t\n");
                Require(SessionDeviceName.Current == fallback && !PlayerPrefs.HasKey(PreferenceKey), "Blank name resets to device default");
                Debug.Log("[SessionDeviceNameChecks] PASS: default, stored name, setter validation, session_start, upload payload, and reset.");
            }
            finally
            {
                if (hadName) PlayerPrefs.SetString(PreferenceKey, original);
                else PlayerPrefs.DeleteKey(PreferenceKey);
                PlayerPrefs.Save();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Device name check failed: " + message);
        }

        [Serializable]
        private sealed class Record { public string eventName, deviceName; }
    }
}
