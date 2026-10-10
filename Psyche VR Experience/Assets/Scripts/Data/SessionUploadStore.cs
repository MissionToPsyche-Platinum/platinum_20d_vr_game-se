using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace PsycheVR.Data
{
    /// <summary>Content-based receipts let a growing active file become pending again.</summary>
    public static class SessionUploadStore
    {
        public const int MaxContentCharacters = 1000000;

        public static string[] Files(string directory)
        {
            if (!Directory.Exists(directory)) return Array.Empty<string>();
            var files = Directory.GetFiles(directory, "session_*.jsonl");
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        public static string Read(string path)
        {
            // Unity writes on the main thread and flushes every event. Capture once,
            // before yielding, then send those exact bytes even if the session grows.
            if (new FileInfo(path).Length > MaxContentCharacters * 4L)
                throw new InvalidDataException("Session exceeds the receiver's size limit.");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                var content = reader.ReadToEnd();
                if (content.Length > MaxContentCharacters)
                    throw new InvalidDataException("Session exceeds the receiver's size limit.");
                return content;
            }
        }

        public static string Hash(string content)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-", "");
        }

        public static bool IsSent(string path, string hash) =>
            File.Exists(path + ".sent") && File.ReadAllText(path + ".sent") == hash;

        /// <summary>Only call after the server acknowledges this exact snapshot.</summary>
        public static void MarkSent(string path, string hash)
        {
            string marker = path + ".sent";
            File.WriteAllText(marker + ".tmp", hash, new UTF8Encoding(false));
            // A crash between delete/move can only cause a retry, never false success.
            if (File.Exists(marker)) File.Delete(marker);
            File.Move(marker + ".tmp", marker);
        }

        public static UploadPayload Payload(string path, string content, string token, string deviceName)
        {
            int newline = content.IndexOf('\n');
            string firstLine = (newline < 0 ? content : content.Substring(0, newline)).TrimStart('\uFEFF');
            var first = JsonUtility.FromJson<StartRecord>(firstLine);
            if (first == null || first.eventName != "session_start" ||
                (first.mode != "Event" && first.mode != "Story"))
                throw new InvalidDataException("Session start metadata is invalid.");
            return new UploadPayload
            {
                token = token, deviceName = deviceName,
                buildStamp = string.IsNullOrWhiteSpace(first.buildStamp)
                    ? "legacy-" + (first.applicationVersion ?? "unknown") : first.buildStamp,
                type = first.mode.ToLowerInvariant(), fileName = Path.GetFileName(path), content = content
            };
        }

        public static bool Acknowledged(string response)
        {
            // TG-280 returns exactly {"ok":true} on success. JsonUtility coerces
            // strings/numbers to bool, so accept only the receiver's literal contract.
            return response != null && System.Text.RegularExpressions.Regex.IsMatch(
                response, @"\A\s*\{\s*""ok""\s*:\s*true\s*\}\s*\z");
        }

        [Serializable]
        public sealed class UploadPayload
        {
            public string token, deviceName, buildStamp, type, fileName, content;
        }

        [Serializable]
        private sealed class StartRecord
        {
            public string eventName, mode, buildStamp, applicationVersion;
        }

    }
}
