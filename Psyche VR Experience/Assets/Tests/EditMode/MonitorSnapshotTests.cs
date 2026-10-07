using System;
using System.IO;
using NUnit.Framework;
using PsycheVR.OpsConsole.Core;
using UnityEngine;

namespace PsycheVR.OpsConsole.Tests
{
    /// <summary>Parsing and interpolation of <see cref="MonitorSnapshot"/>, plus a check of the shipped snapshot.</summary>
    public class MonitorSnapshotTests
    {
        private const string Json = "{\"buildDate\":\"2026-10-02\",\"earthDistanceKm\":282659369.1,\"oneWayLightSeconds\":942.85," +
            "\"bodies\":[{\"name\":\"Earth\",\"points\":[{\"date\":\"2026-01-01\",\"x\":1,\"y\":0},{\"date\":\"2026-01-08\",\"x\":0,\"y\":1}]}]}";

        private const double LightSecondsTolerance = 0.5;
        private const float FlybyMaxSeparationAu = 0.01f;
        private static readonly DateTime LaunchDate = new DateTime(2023, 10, 14);
        private static readonly DateTime MarsFlyby = new DateTime(2026, 5, 15);

        [Test] public void Parses()
        {
            var s = MonitorSnapshot.Parse(Json);
            Assert.AreEqual("2026-10-02", s.buildDate);
            Assert.AreEqual(2, s.Body("Earth").points.Length);
        }

        [Test] public void InterpolatesByDate()
        {
            var p = MonitorSnapshot.Parse(Json).Body("Earth").At(new DateTime(2026, 1, 4, 12, 0, 0));
            Assert.AreEqual(0.5f, p.x, 1e-4f); Assert.AreEqual(0.5f, p.y, 1e-4f);
        }

        [Test] public void ClampsOutsideRange()
        {
            var e = MonitorSnapshot.Parse(Json).Body("Earth");
            Assert.AreEqual(1f, e.At(new DateTime(2025, 1, 1)).x, 1e-4f);
            Assert.AreEqual(1f, e.At(new DateTime(2027, 1, 1)).y, 1e-4f);
        }

        [Test] public void MissingBodyIsNull() => Assert.IsNull(MonitorSnapshot.Parse(Json).Body("Mars"));

        [Test] public void NoBodiesGivesNull() => Assert.IsNull(MonitorSnapshot.Parse("{\"buildDate\":\"2026-10-02\"}").Body("Earth"));

        [Test] public void EmptyTrackIsZero() =>
            Assert.AreEqual(Vector2.zero, new MonitorSnapshot.Track { name = "Earth", points = new MonitorSnapshot.Point[0] }.At(MarsFlyby));

        [Test] public void ShippedSnapshotIsConsistent()
        {
            string path = Path.Combine(Application.dataPath, "Data", "OpsMonitor", "monitor_snapshot.json");
            var s = MonitorSnapshot.Parse(File.ReadAllText(path));

            foreach (var name in new[] { "Spacecraft", "Asteroid", "Earth", "Mars" })
                Assert.IsNotNull(s.Body(name), name);

            var craft = s.Body("Spacecraft");
            Assert.AreEqual(LaunchDate, craft.First);
            Assert.AreEqual(s.earthDistanceKm / RoundTrip.SpeedOfLightKmPerSecond, s.oneWayLightSeconds, LightSecondsTolerance);
            Assert.That(s.BuildDate, Is.InRange(craft.First, craft.Last));

            float separationAu = Vector2.Distance(craft.At(MarsFlyby), s.Body("Mars").At(MarsFlyby));
            Assert.That(separationAu, Is.LessThan(FlybyMaxSeparationAu));
        }
    }
}
