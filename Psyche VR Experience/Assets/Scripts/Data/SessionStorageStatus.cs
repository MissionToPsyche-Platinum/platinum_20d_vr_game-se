using System;
using System.Globalization;
using System.IO;
using System.Security;
using UnityEngine;

namespace PsycheVR.Data
{
    /// <summary>Best-effort storage telemetry; -1 means a measurement was unavailable.</summary>
    public static class SessionStorageStatus
    {
        /// <summary>
        /// Counts only session logs directly in each folder, without opening them.
        /// Waiting includes the newly opened current session. Receipts are excluded.
        /// This is a snapshot before any startup upload/migration has completed.
        /// </summary>
        public static string Details(string directory)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "freeBytes={0};waiting={1};sent={2};rejected={3}",
                FreeBytes(directory), Count(directory), Count(Path.Combine(directory, "Sent")),
                Count(Path.Combine(directory, "Rejected")));
        }

        private static int Count(string directory)
        {
            try
            {
                int count = 0;
                foreach (string path in Directory.EnumerateFiles(directory, "session_*.jsonl", SearchOption.TopDirectoryOnly))
                    count++;
                return count;
            }
            catch (DirectoryNotFoundException) { return 0; }
            catch (Exception error) when (IsMeasurementError(error)) { return -1; }
        }

        private static long FreeBytes(string directory)
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                // Query the filesystem containing the logs, not device-wide storage.
                using (var stats = new AndroidJavaObject("android.os.StatFs", directory))
                    return stats.Call<long>("getAvailableBytes");
#else
                string path = Path.GetFullPath(directory);
                DriveInfo best = null;
                StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                    ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    string root = drive.RootDirectory.FullName;
                    string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if ((string.Equals(path, root, comparison) || path.StartsWith(prefix, comparison)) &&
                        (best == null || root.Length > best.RootDirectory.FullName.Length))
                        best = drive;
                }
                return best == null ? -1 : best.AvailableFreeSpace;
#endif
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            catch (AndroidJavaException) { return -1; }
#endif
            catch (Exception error) when (IsMeasurementError(error)) { return -1; }
        }

        private static bool IsMeasurementError(Exception error) =>
            error is IOException || error is UnauthorizedAccessException || error is SecurityException ||
            error is ArgumentException || error is NotSupportedException;
    }
}
