using System;
using System.Globalization;
using System.IO;
using PsycheVR.Data;
using UnityEditor;
using UnityEngine;

namespace PsycheVR.Modes.Editor
{
    /// <summary>Exercises real asset generation and JSONL output without building an APK.</summary>
    public static class SessionBuildChecks
    {
        [MenuItem("Tools/Session Logs/Check Build Stamps")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run build stamp checks in Edit mode.");
            string path = SessionBuildPreparation.BuildInfoPath;
            byte[] original = File.Exists(path) ? File.ReadAllBytes(path) : null;
            int version = PlayerSettings.Android.bundleVersionCode;
            string directory = Path.Combine(Path.GetTempPath(), "psyche-build-checks-" + Guid.NewGuid().ToString("N"));
            try
            {
                PlayerSettings.Android.bundleVersionCode = 100;
                CheckBuild(GameMode.Event, 101, directory);
                CheckBuild(GameMode.Story, 102, directory);
                Require(SessionBuildInfo.CurrentStamp == "editor-unbuilt", "Editor must not reuse the last APK stamp");
                foreach (int invalid in new[] { -1, 2100000000, int.MaxValue })
                {
                    bool rejected = false;
                    try { SessionBuildPreparation.NextVersionCode(invalid); }
                    catch (InvalidOperationException) { rejected = true; }
                    Require(rejected, "Invalid or exhausted Android version code must fail");
                }
                Debug.Log("[SessionBuildChecks] PASS: Event/Story Resources stamps, UTC timestamps, consecutive Android versions, session_start JSONL, and editor identity.");
            }
            finally
            {
                PlayerSettings.Android.bundleVersionCode = version;
                if (original == null) AssetDatabase.DeleteAsset(path);
                else
                {
                    File.WriteAllBytes(path, original);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }
                AssetDatabase.SaveAssets();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void CheckBuild(GameMode mode, int expectedVersion, string directory)
        {
            DateTime before = DateTime.UtcNow;
            SessionBuildPreparation.Prepare(mode);
            var asset = Resources.Load<TextAsset>(SessionBuildInfo.ResourceName);
            Require(asset != null, "Generated stamp must be loadable from Resources");
            var info = JsonUtility.FromJson<SessionBuildInfo>(asset.text);
            Require(System.Text.RegularExpressions.Regex.IsMatch(info.gitShortHash, "^[0-9a-f]{12,40}$"), "Git short hash");
            DateTime time = DateTime.Parse(info.buildTimeUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Require(time.Kind == DateTimeKind.Utc && time >= before && time <= DateTime.UtcNow, "Current UTC build time");
            Require(info.flavor == mode.ToString().ToLowerInvariant(), "Requested flavor");
            Require(info.versionCode == expectedVersion && PlayerSettings.Android.bundleVersionCode == expectedVersion,
                "Version must increase for every flavor");
            using (var writer = new SessionLogWriter(directory, mode.ToString(), "Checks", "0.1.0", "Android", info.Stamp))
            {
                using (var reader = new StreamReader(writer.FilePath))
                {
                    var entry = JsonUtility.FromJson<Record>(reader.ReadLine());
                    Require(entry.eventName == "session_start" && entry.buildStamp == info.Stamp,
                        "First JSONL record must contain this build's stamp");
                }
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Build stamp check failed: " + message);
        }

        [Serializable]
        private sealed class Record { public string eventName, buildStamp; }
    }
}
