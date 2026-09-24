using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PsycheVR.Data
{
    /// <summary>
    /// Writes one JSON object per line to a unique session file. Each event is flushed
    /// immediately so completed records can be read while the session is running.
    /// </summary>
    public sealed class SessionLogWriter : IDisposable
    {
        private const int SchemaVersion = 1;
        private readonly string mode;
        private readonly string applicationVersion;
        private readonly string platform;
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        private StreamWriter writer;
        private int sequence;

        /// <summary>Unique identifier for this session, unrelated to the player or device.</summary>
        public string SessionId { get; }

        /// <summary>Absolute path to this session's JSON Lines file.</summary>
        public string FilePath { get; }

        /// <summary>Opens a new session file and immediately records session_start.</summary>
        public SessionLogWriter(string directory, string mode, string scene,
            string applicationVersion, string platform)
        {
            this.mode = mode;
            this.applicationVersion = applicationVersion;
            this.platform = platform;
            SessionId = Guid.NewGuid().ToString("N");

            Directory.CreateDirectory(directory);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
            FilePath = Path.GetFullPath(Path.Combine(directory, $"session_{timestamp}_{SessionId}.jsonl"));
            var stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            writer = new StreamWriter(stream, new UTF8Encoding(false));

            try
            {
                writer.AutoFlush = true;
                RecordEvent("session_start", scene);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Appends an event on Unity's main thread. Details are optional free text;
        /// JSON serialization escapes quotes, tabs, and line breaks.
        /// </summary>
        public void RecordEvent(string eventName, string scene, string details = "")
        {
            if (writer == null)
                throw new ObjectDisposedException(nameof(SessionLogWriter));
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("An event name is required.", nameof(eventName));

            var entry = new SessionLogEntry
            {
                schemaVersion = SchemaVersion,
                sessionId = SessionId,
                sequence = sequence,
                timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                elapsedSeconds = Math.Round(elapsed.Elapsed.TotalSeconds, 3),
                eventName = eventName,
                mode = mode,
                scene = scene ?? string.Empty,
                details = details ?? string.Empty,
                applicationVersion = applicationVersion,
                platform = platform
            };

            writer.WriteLine(JsonUtility.ToJson(entry));
            sequence++;
        }

        /// <summary>Records session_end and closes the file. Repeated calls do nothing.</summary>
        public void EndSession(string scene, string reason)
        {
            if (writer == null)
                return;

            try
            {
                RecordEvent("session_end", scene, reason);
            }
            finally
            {
                Dispose();
            }
        }

        /// <summary>Closes the file without adding an event, including after a write failure.</summary>
        public void Dispose()
        {
            StreamWriter openWriter = writer;
            writer = null;
            elapsed.Stop();
            openWriter?.Dispose();
        }

        [Serializable]
        private sealed class SessionLogEntry
        {
            public int schemaVersion;
            public string sessionId;
            public int sequence;
            public string timestampUtc;
            public double elapsedSeconds;
            public string eventName;
            public string mode;
            public string scene;
            public string details;
            public string applicationVersion;
            public string platform;
        }
    }
}
