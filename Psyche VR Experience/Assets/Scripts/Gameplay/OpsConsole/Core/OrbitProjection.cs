using System;
using UnityEngine;

namespace PsycheVR.OpsConsole.Core
{
    /// <summary>
    /// Maps heliocentric ecliptic x/y in AU onto a panel: the Sun at the panel's centre, y up, and
    /// <see cref="MaxRadiusAu"/> reaching the nearer edge less a margin, so circles stay circles.
    /// </summary>
    public readonly struct OrbitProjection
    {
        /// <summary>Default radius at the panel edge: just past the asteroid's 3.3 AU aphelion.</summary>
        public const float DefaultMaxRadiusAu = 3.5f;

        /// <summary>The Sun's position in panel coordinates.</summary>
        public Vector2 Centre { get; }

        /// <summary>Panel units per AU.</summary>
        public float UnitsPerAu { get; }

        /// <summary>The radius, in AU, that reaches the panel's nearer edge less the margin.</summary>
        public float MaxRadiusAu { get; }

        /// <summary>
        /// A projection onto <paramref name="panel"/> (in the panel's own coordinates), keeping
        /// <paramref name="margin"/> panel units free at the nearer edges.
        /// </summary>
        public OrbitProjection(Rect panel, float margin, float maxRadiusAu = DefaultMaxRadiusAu)
        {
            Centre = panel.center;
            MaxRadiusAu = maxRadiusAu;
            float half = Mathf.Min(panel.width, panel.height) * 0.5f;
            UnitsPerAu = Mathf.Max(half - margin, 0f) / maxRadiusAu;
        }

        /// <summary>Panel position of the heliocentric point <paramref name="au"/>.</summary>
        public Vector2 ToPanel(Vector2 au) => Centre + au * UnitsPerAu;

        /// <summary>A linear animation clock: <c>seconds</c> of playback cover the dates start to end.</summary>
        public readonly struct Timeline
        {
            /// <summary>First date, at 0 s.</summary>
            public DateTime Start { get; }
            /// <summary>Last date, at <see cref="Seconds"/>.</summary>
            public DateTime End { get; }
            /// <summary>Playback length, seconds.</summary>
            public float Seconds { get; }

            /// <summary>A clock running from <paramref name="start"/> to <paramref name="end"/> in <paramref name="seconds"/>.</summary>
            public Timeline(DateTime start, DateTime end, float seconds)
            {
                Start = start; End = end; Seconds = seconds;
            }

            /// <summary>The date shown <paramref name="t"/> seconds in, clamped to the ends.</summary>
            public DateTime At(float t)
            {
                if (Seconds <= 0f || t >= Seconds) return End;
                if (t <= 0f) return Start;
                return Start + TimeSpan.FromTicks((long)((End - Start).Ticks * (double)(t / Seconds)));
            }

            /// <summary>The playback time at which <paramref name="when"/> is shown, clamped to 0..<see cref="Seconds"/>.</summary>
            public float SecondsAt(DateTime when)
            {
                double span = (End - Start).Ticks;
                if (span <= 0) return 0f;
                return Mathf.Clamp((float)((when - Start).Ticks / span * Seconds), 0f, Seconds);
            }
        }
    }
}
