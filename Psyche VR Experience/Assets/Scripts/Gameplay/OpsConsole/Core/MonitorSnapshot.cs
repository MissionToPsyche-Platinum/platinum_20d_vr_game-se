using System;
using System.Globalization;
using UnityEngine;

namespace PsycheVR.OpsConsole.Core
{
    /// <summary>
    /// The build-date snapshot (fetch_monitor_data.py): heliocentric ecliptic x/y in AU, weekly, per
    /// body, plus the Earth-spacecraft distance and light time on the build date.
    /// </summary>
    [Serializable]
    public class MonitorSnapshot
    {
        private const string DateFormat = "yyyy-MM-dd";

        /// <summary>One weekly sample: date (yyyy-MM-dd) and ecliptic x/y in AU.</summary>
        [Serializable] public class Point { public string date; public float x; public float y; }

        /// <summary>One body's weekly samples, in date order.</summary>
        [Serializable]
        public class Track
        {
            public string name;
            public Point[] points;
            [NonSerialized] private DateTime[] _dates;

            /// <summary>Date of the first sample.</summary>
            public DateTime First => Dates[0];

            /// <summary>Date of the last sample.</summary>
            public DateTime Last => Dates[Dates.Length - 1];

            private DateTime[] Dates => _dates ??= Array.ConvertAll(points, p => ParseDate(p.date));

            /// <summary>
            /// Position on <paramref name="when"/>, linear between samples, clamped to the ends.
            /// Zero for a track with no samples.
            /// </summary>
            public Vector2 At(DateTime when)
            {
                if (points == null || points.Length == 0) return Vector2.zero;
                var d = Dates;
                if (when <= d[0]) return new Vector2(points[0].x, points[0].y);
                for (int i = 1; i < d.Length; i++)
                {
                    if (when > d[i]) continue;
                    float t = (float)((when - d[i - 1]).TotalSeconds / (d[i] - d[i - 1]).TotalSeconds);
                    return Vector2.Lerp(new Vector2(points[i - 1].x, points[i - 1].y), new Vector2(points[i].x, points[i].y), t);
                }
                var last = points[points.Length - 1];
                return new Vector2(last.x, last.y);
            }
        }

        public string buildDate;
        public double earthDistanceKm;
        public double oneWayLightSeconds;
        public Track[] bodies;

        /// <summary><see cref="buildDate"/> as a date.</summary>
        public DateTime BuildDate => ParseDate(buildDate);

        /// <summary>Reads a snapshot from its JSON text.</summary>
        public static MonitorSnapshot Parse(string json) => JsonUtility.FromJson<MonitorSnapshot>(json);

        /// <summary>The track named <paramref name="name"/>, or null if the snapshot has none.</summary>
        public Track Body(string name) => bodies == null ? null : Array.Find(bodies, b => b.name == name);

        private static DateTime ParseDate(string s) => DateTime.ParseExact(s, DateFormat, CultureInfo.InvariantCulture);
    }
}
