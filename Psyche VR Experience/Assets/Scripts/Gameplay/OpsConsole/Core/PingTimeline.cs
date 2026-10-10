using System;
using System.Globalization;

namespace PsycheVR.OpsConsole.Core
{
    /// <summary>Where the ping animation is: going out, blinking at the spacecraft, coming back, or landed.</summary>
    public enum PingPhase { Out, Blink, Back, Done }

    /// <summary>
    /// The ping's compressed timeline (TG-227): a leg out to the spacecraft, a short blink there, the same
    /// leg back. Maps animation seconds to the phase, the dot's position along the link (0 = the dish,
    /// 1 = the spacecraft) and the simulated real time. Real time runs with the dot: half the round trip
    /// at the spacecraft, the full round trip on landing; the blink takes no real time (the spacecraft
    /// answers at once).
    /// </summary>
    public sealed class PingTimeline
    {
        /// <summary>Real seconds per animation second (31 min becomes about 12 s).</summary>
        public const float CompressionRatio = 157f;
        /// <summary>Shortest animation, seconds.</summary>
        public const float MinSeconds = 8f;
        /// <summary>Longest animation, seconds.</summary>
        public const float MaxSeconds = 15f;
        /// <summary>Seconds the dot blinks at the spacecraft.</summary>
        public const float DefaultBlinkSeconds = 0.9f;

        private const double SecondsPerMinute = 60.0;
        private const string ClockFormat = "h:mm tt";

        /// <summary>A timeline of <paramref name="totalSeconds"/>, blinking for <paramref name="blinkSeconds"/> at the far end.</summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="totalSeconds"/> is not positive, <paramref name="blinkSeconds"/> is negative or not shorter than
        /// the total, or <paramref name="realRoundTripSeconds"/> is negative.
        /// </exception>
        public PingTimeline(float totalSeconds, float blinkSeconds, double realRoundTripSeconds)
        {
            if (totalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(totalSeconds), totalSeconds, "Must be positive.");
            if (blinkSeconds < 0f || blinkSeconds >= totalSeconds)
                throw new ArgumentOutOfRangeException(nameof(blinkSeconds), blinkSeconds, "Must be 0 or more and shorter than the total.");
            if (realRoundTripSeconds < 0.0)
                throw new ArgumentOutOfRangeException(nameof(realRoundTripSeconds), realRoundTripSeconds, "Must not be negative.");
            TotalSeconds = totalSeconds;
            BlinkSeconds = blinkSeconds;
            RealRoundTripSeconds = realRoundTripSeconds;
        }

        /// <summary>The standard ping for a real round trip: <see cref="RoundTrip.CompressedSeconds"/> with this class's ratio and limits.</summary>
        public static PingTimeline ForRoundTrip(double realRoundTripSeconds) =>
            new PingTimeline(RoundTrip.CompressedSeconds(realRoundTripSeconds, CompressionRatio, MinSeconds, MaxSeconds),
                DefaultBlinkSeconds, realRoundTripSeconds);

        /// <summary>Whole animation length, seconds.</summary>
        public float TotalSeconds { get; }
        /// <summary>Seconds of blinking at the spacecraft.</summary>
        public float BlinkSeconds { get; }
        /// <summary>The real round trip being shown, seconds.</summary>
        public double RealRoundTripSeconds { get; }
        /// <summary>Seconds for one leg (out or back).</summary>
        public float LegSeconds => (TotalSeconds - BlinkSeconds) * 0.5f;
        /// <summary>The animation length rounded to whole seconds ("You waited N seconds").</summary>
        public int WaitedSeconds => (int)Math.Round(TotalSeconds);
        /// <summary>The real round trip rounded to whole minutes.</summary>
        public int RoundTripMinutes => RoundTrip.WholeMinutes(RealRoundTripSeconds);

        /// <summary>Phase at <paramref name="t"/> animation seconds (negative counts as 0).</summary>
        public PingPhase PhaseAt(float t)
        {
            t = Math.Max(t, 0f);
            if (t >= TotalSeconds) return PingPhase.Done;
            if (t < LegSeconds) return PingPhase.Out;
            if (t < LegSeconds + BlinkSeconds) return PingPhase.Blink;
            return PingPhase.Back;
        }

        /// <summary>The dot's position along the link at <paramref name="t"/>: 0 at the dish, 1 at the spacecraft.</summary>
        public float PositionAt(float t)
        {
            t = Math.Max(t, 0f);
            switch (PhaseAt(t))
            {
                case PingPhase.Out: return t / LegSeconds;
                case PingPhase.Blink: return 1f;
                case PingPhase.Back: return 1f - (t - LegSeconds - BlinkSeconds) / LegSeconds;
                default: return 0f;
            }
        }

        /// <summary>Simulated real seconds since the ping left, at <paramref name="t"/> animation seconds.</summary>
        public double RealSecondsAt(float t)
        {
            t = Math.Max(t, 0f);
            double oneWay = RealRoundTripSeconds * 0.5;
            switch (PhaseAt(t))
            {
                case PingPhase.Out: return oneWay * (t / LegSeconds);
                case PingPhase.Blink: return oneWay;
                case PingPhase.Back: return oneWay * (1.0 + (t - LegSeconds - BlinkSeconds) / LegSeconds);
                default: return RealRoundTripSeconds;
            }
        }

        /// <summary><paramref name="seconds"/> as minutes and whole seconds, "m:ss" (rounded down).</summary>
        public static string Clock(double seconds)
        {
            long whole = (long)Math.Floor(Math.Max(seconds, 0.0));
            long minutes = whole / (long)SecondsPerMinute;
            long rest = whole % (long)SecondsPerMinute;
            return minutes.ToString(CultureInfo.InvariantCulture) + ":" + rest.ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Local clock time ("h:mm tt", invariant culture) when a signal sent at <paramref name="now"/> reaches
        /// the spacecraft, <paramref name="oneWaySeconds"/> later.
        /// </summary>
        public static string ArrivalClock(DateTime now, double oneWaySeconds) =>
            now.AddSeconds(oneWaySeconds).ToString(ClockFormat, CultureInfo.InvariantCulture);
    }
}
