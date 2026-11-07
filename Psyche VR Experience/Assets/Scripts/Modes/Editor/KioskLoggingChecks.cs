using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using PsycheVR.Data;
using PsycheVR.Kiosk;
using UnityEditor;
using UnityEngine;

namespace PsycheVR.Modes.Editor
{
    /// <summary>Exercises kiosk transitions and real JSONL output without scene or video playback.</summary>
    public static class KioskLoggingChecks
    {
        [MenuItem("Tools/Session Logs/Check Kiosk Outcomes")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run kiosk checks in Edit mode.");
            string directory = Path.Combine(Path.GetTempPath(), "psyche-kiosk-checks-" + Guid.NewGuid().ToString("N"));
            var mode = typeof(GameModeManager).GetProperty("ActiveMode");
            var originalMode = GameModeManager.ActiveMode;
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                foreach (GameMode gameMode in new[] { GameMode.Event, GameMode.Story })
                {
                    mode.SetValue(null, gameMode);
                    CheckVisit(directory, gameMode, false, false);
                    CheckVisit(directory, gameMode, true, false);
                    CheckVisit(directory, gameMode, false, true);
                }
                Require(Directory.GetFiles(directory, "*.jsonl").Length == 6, "Visitors have separate session files");
                Debug.Log("[KioskLoggingChecks] PASS: first input, completion without ending, clock-only ending, duplicate suppression, early completion, Story exclusion, visitor files, and invariant elapsed seconds.");
            }
            finally
            {
                mode.SetValue(null, originalMode);
                CultureInfo.CurrentCulture = originalCulture;
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void CheckVisit(string directory, GameMode mode, bool expires, bool early)
        {
            var singleton = typeof(SessionDataLogger).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
            object previous = singleton.GetValue(null);
            var loggerObject = new GameObject("Kiosk checks logger");
            var kioskObject = new GameObject("Kiosk checks visitor");
            // Keep scene lifecycle, input actions, and visual presentation out of these checks.
            loggerObject.SetActive(false);
            kioskObject.SetActive(false);
            var logger = loggerObject.AddComponent<SessionDataLogger>();
            var kiosk = kioskObject.AddComponent<KioskSession>();
            Set(kiosk, "_messageShown", true);
            using (var writer = new SessionLogWriter(directory, mode.ToString(), "Checks", "test", "Editor", "test-build"))
            {
                try
                {
                    singleton.SetValue(null, logger);
                    Set(logger, "session", writer);
                    Invoke(kiosk, "TickClock", 50f); // Armed time must not count.
                    if (!early)
                    {
                        Invoke(kiosk, "HandleFirstInput");
                        Invoke(kiosk, "HandleFirstInput");
                        Invoke(kiosk, "TickClock", 0f);
                        Invoke(kiosk, "TickClock", expires ? 180.25f : 5.25f);
                    }
                    Invoke(kiosk, "HandlePuzzleCompleted");
                    Invoke(kiosk, "HandlePuzzleCompleted");
                    Require(kiosk.CurrentPhase != KioskSession.Phase.Ending || expires, "Puzzle completion never starts the ending");
                    Invoke(kiosk, "HandleFirstInput");
                    Invoke(kiosk, "TickClock", 200f); // The clock still ends a completed visit; late callbacks emit nothing more.
                    Invoke(kiosk, "HandlePuzzleCompleted");
                    var entries = File.ReadAllLines(writer.FilePath).Select(JsonUtility.FromJson<Record>).ToArray();
                    Require(entries.All(entry => entry.sessionId == writer.SessionId), "Events belong to this visitor");
                    var events = entries.Where(entry => entry.eventName.StartsWith("kiosk_", StringComparison.Ordinal)).ToArray();
                    if (mode == GameMode.Story)
                    {
                        Require(events.Length == 0, "Story sessions exclude kiosk events");
                        return;
                    }
                    // Expected kiosk events in order, as "name=elapsedSeconds".
                    string[] expected = early
                        ? new[] { "kiosk_puzzle_completed=0", "kiosk_first_input=0", "kiosk_clock_expired=200" }
                        : expires
                            ? new[] { "kiosk_first_input=0", "kiosk_clock_expired=180.25" }
                            : new[] { "kiosk_first_input=0", "kiosk_puzzle_completed=5.25", "kiosk_clock_expired=205.25" };
                    string[] actual = events.Select(e => e.eventName + "=" + e.details.Replace("elapsedSeconds=", "")).ToArray();
                    Require(actual.SequenceEqual(expected), "Kiosk events are [" + string.Join(", ", expected) + "] but were [" + string.Join(", ", actual) + "]: completion logs once, the clock alone ends the visit, elapsed seconds use invariant decimals and exclude armed time");
                }
                finally
                {
                    Set(logger, "session", null);
                    UnityEngine.Object.DestroyImmediate(kioskObject);
                    UnityEngine.Object.DestroyImmediate(loggerObject);
                    singleton.SetValue(null, previous);
                }
            }
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Invoke(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, arguments);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Kiosk logging check failed: " + message);
        }
        [Serializable]
        private sealed class Record { public string eventName, details, sessionId; }
    }
}
