using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace PsycheVR.Data
{
    /// <summary>Pending log enumeration, legacy receipts, and closed-log archival.</summary>
    public static class SessionUploadStore
    {
        public const int MaxContentCharacters = 1000000;

        public static string[] Files(string directory)
        {
            if (!Directory.Exists(directory)) return Array.Empty<string>();
            var files = Directory.GetFiles(directory, "session_*.jsonl", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        public static string Read(string path)
        {
            // Unity writes on the main thread and flushes every event. Capture once,
            // before yielding, then send those exact bytes even if the session grows.
            if (new FileInfo(path).Length > MaxContentCharacters * 4L)
                throw new OversizedLogException();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                var content = reader.ReadToEnd();
                if (content.Length > MaxContentCharacters)
                    throw new OversizedLogException();
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

        public sealed class OversizedLogException : IOException
        {
            public OversizedLogException() : base("Session exceeds the receiver's size limit.") { }
        }

        public static bool IsActive(string path, string activePath) =>
            activePath != null && string.Equals(Path.GetFullPath(path), Path.GetFullPath(activePath),
                StringComparison.Ordinal);

        /// <summary>Move only closed logs; never overwrite an existing archive.</summary>
        public static bool Archive(string path, string activePath, bool rejected, string hash = null)
        {
            if (IsActive(path, activePath)) return false;
            string directory = Path.Combine(Path.GetDirectoryName(path), rejected ? "Rejected" : "Sent");
            Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory, Path.GetFileName(path));
            if (File.Exists(destination) || File.Exists(destination + ".sent"))
                destination = Path.Combine(directory, Path.GetFileNameWithoutExtension(path) + "_" +
                    Guid.NewGuid().ToString("N") + Path.GetExtension(path));

            // New confirmations live only in the archive. If interrupted before the
            // log move, the pending log has no new receipt and will be uploaded again.
            if (!rejected && hash != null) MarkSent(destination, hash);
            File.Move(path, destination);
            if (File.Exists(path + ".sent"))
            {
                if (File.Exists(destination + ".sent")) File.Delete(path + ".sent");
                else File.Move(path + ".sent", destination + ".sent");
            }
            return true;
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
