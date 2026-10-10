using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using PsycheVR.Data;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PsycheVR.Modes.Editor
{
    /// <summary>Generates the build identity embedded in each APK.</summary>
    public static class SessionBuildPreparation
    {
        public const string BuildInfoPath = "Assets/Resources/SessionBuildInfo.json";

        /// <summary>Called once per APK; failed builds can consume a version code.</summary>
        public static void Prepare(GameMode mode)
        {
            string hash = ReadGitHash();
            int next = NextVersionCode(PlayerSettings.Android.bundleVersionCode);
            var info = new SessionBuildInfo
            {
                gitShortHash = hash,
                buildTimeUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                flavor = mode.ToString().ToLowerInvariant(), versionCode = next
            };
            Directory.CreateDirectory("Assets/Resources");
            File.WriteAllText(BuildInfoPath, JsonUtility.ToJson(info, true));
            AssetDatabase.ImportAsset(BuildInfoPath, ImportAssetOptions.ForceSynchronousImport);
            PlayerSettings.Android.bundleVersionCode = next;
            AssetDatabase.SaveAssets();
            Debug.Log($"[GameModeBuilder] Build stamp: {info.Stamp}");
        }

        public static int NextVersionCode(int current)
        {
            if (current < 0 || current >= 2100000000)
                throw new InvalidOperationException("Android version code cannot be incremented safely.");
            return current + 1;
        }

        private static string ReadGitHash()
        {
            var start = new ProcessStartInfo("git", "rev-parse --short=12 HEAD")
            {
                WorkingDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(start))
            {
                if (process == null) throw new InvalidOperationException("Cannot start Git to stamp this build.");
                if (!process.WaitForExit(5000))
                {
                    process.Kill();
                    throw new InvalidOperationException("Timed out reading the build commit.");
                }
                string hash = process.StandardOutput.ReadToEnd().Trim();
                if (process.ExitCode != 0 || !System.Text.RegularExpressions.Regex.IsMatch(hash, "^[0-9a-f]{7,40}$"))
                    throw new InvalidOperationException("Cannot identify the Git commit. Build from a Git checkout with Git installed.");
                return hash;
            }
        }
    }
}
