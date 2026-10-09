using System;
using System.IO;
using PsycheVR.Data;
using UnityEditor;
using UnityEngine;

namespace PsycheVR.Modes.Editor
{
    /// <summary>Focused, offline checks; run in Edit mode with no session in progress.</summary>
    public static class SessionUploadChecks
    {
        [MenuItem("Tools/Session Logs/Check Upload Storage")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run session checks in Edit mode.");
            string directory = Path.Combine(Path.GetTempPath(), "psyche-session-checks-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                CheckFiles(directory);
                Require(!new SessionUploadConfig { endpointUrl = "http://example.com", token = "test" }.IsValid,
                    "Reject cleartext configuration");
                Require(new SessionUploadConfig { endpointUrl = "https://example.com", token = "test" }.IsValid,
                    "Accept HTTPS configuration");
                Require(!new SessionUploadConfig { endpointUrl = "https://example.com", token = "" }.IsValid,
                    "Missing credentials disable uploads");
                Debug.Log("[SessionUploadChecks] PASS: payloads, snapshot receipts, strict acknowledgments, and configuration.");
            }
            finally { Directory.Delete(directory, true); }
        }

        private static void CheckFiles(string directory)
        {
            using (var writer = new SessionLogWriter(directory, "Event", "Bedroom", "0.1.0", "Android", "abc UTC event v9"))
            {
                string initial = SessionUploadStore.Read(writer.FilePath);
                var first = JsonUtility.FromJson<Record>(initial.Trim());
                Require(first.eventName == "session_start" && first.buildStamp == "abc UTC event v9",
                    "Session start includes stamp");
                var payload = SessionUploadStore.Payload(writer.FilePath, initial, "test-token", "Renamed device");
                Require(payload.buildStamp == first.buildStamp && payload.type == "event" && payload.deviceName == "Renamed device" && payload.content == initial,
                    "Payload uses file build identity and latest device name");
                Require(JsonUtility.FromJson<SessionUploadStore.UploadPayload>(JsonUtility.ToJson(payload)).content == initial,
                    "JSONL is escaped correctly in upload payload");
                string hash = SessionUploadStore.Hash(initial);
                Require(!SessionUploadStore.IsSent(writer.FilePath, hash), "New file is pending");
                SessionUploadStore.MarkSent(writer.FilePath, hash);
                Require(SessionUploadStore.IsSent(writer.FilePath, hash), "Receipt survives separate read");
                writer.RecordEvent("test", "Bedroom", "quotes \" and Unicode ☀\nnew line");
                string changedHash = SessionUploadStore.Hash(SessionUploadStore.Read(writer.FilePath));
                Require(!SessionUploadStore.IsSent(writer.FilePath, changedHash), "Growing active file becomes pending again");
                SessionUploadStore.MarkSent(writer.FilePath, hash);
                Require(!SessionUploadStore.IsSent(writer.FilePath, changedHash), "Old in-flight acknowledgment cannot mark new events sent");
                writer.EndSession("Bedroom", "restart");
                string ended = SessionUploadStore.Read(writer.FilePath);
                Require(ended.Contains("session_end") && ended.Contains("restart"), "Session end remains recorded");
            }
            foreach (string bad in new[] { "", "<html>error</html>", "{}", "{\"ok\":false}", "{\"ok\":\"true\"}", "not json", "{\"ok\":1}", "{\"nested\":{\"ok\":true}}", "{\"ok\":true}junk", "{\"ok\":true,\"ok\":false}" })
                Require(!SessionUploadStore.Acknowledged(bad), "Non-boolean/failed acknowledgment rejected: " + bad);
            Require(SessionUploadStore.Acknowledged("{\"ok\":true}"), "Boolean success acknowledged");
            Require(SessionUploadStore.Acknowledged(" \n{ \"ok\" : true }\r\n"), "Whitespace is accepted");
            Require(SessionUploadStore.Files(directory).Length == 1, "Receipt files are excluded from pending enumeration");
            var legacy = SessionUploadStore.Payload("session_old.jsonl",
                "{\"eventName\":\"session_start\",\"mode\":\"Story\",\"applicationVersion\":\"0.1.0\"}\n", "test", "device");
            Require(legacy.buildStamp == "legacy-0.1.0" && legacy.type == "story", "Legacy file metadata");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Session check failed: " + message);
        }

        [Serializable]
        private sealed class Record { public string eventName, buildStamp, deviceName; }
    }
}
