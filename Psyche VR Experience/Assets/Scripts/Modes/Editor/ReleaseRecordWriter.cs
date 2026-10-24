using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PsycheVR.Modes.Editor
{
    public sealed class ReleaseRecordWriter : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android ||
                report.summary.result != BuildResult.Succeeded)
                return;

            string apkPath = report.summary.outputPath;
            if (!apkPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                return;

            string directory = Path.GetDirectoryName(apkPath);
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string commit = RunGit(repoRoot, "rev-parse HEAD");
            string status = RunGit(repoRoot, "status --porcelain");
            string contents =
                $"APK: {Path.GetFileName(apkPath)}{Environment.NewLine}" +
                $"Version: {PlayerSettings.bundleVersion}{Environment.NewLine}" +
                $"Version code: {PlayerSettings.Android.bundleVersionCode}{Environment.NewLine}" +
                $"Git commit: {commit}{Environment.NewLine}" +
                $"Uncommitted changes: {(status == "<unavailable>" ? "unknown" : (!string.IsNullOrWhiteSpace(status)).ToString())}{Environment.NewLine}" +
                $"Built (UTC): {DateTime.UtcNow:O}{Environment.NewLine}";

            string recordPath = Path.Combine(directory,
                Path.GetFileNameWithoutExtension(apkPath) + "-release.txt");
            File.WriteAllText(recordPath, contents);
            File.WriteAllText(Path.Combine(directory, "release.txt"), contents);
            Debug.Log($"[ReleaseRecordWriter] Wrote {recordPath}");
        }

        private static string RunGit(string directory, string arguments)
        {
            try
            {
                var start = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = directory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (var process = Process.Start(start))
                {
                    if (process == null) return "<unavailable>";
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.StandardError.ReadToEnd();
                    process.WaitForExit(5000);
                    return process.ExitCode == 0 ? output : "<unavailable>";
                }
            }
            catch { return "<unavailable>"; }
        }
    }
}
