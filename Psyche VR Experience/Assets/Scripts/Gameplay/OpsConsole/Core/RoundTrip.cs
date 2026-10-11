namespace PsycheVR.OpsConsole.Core
{
    /// <summary>Light-time arithmetic for the ping: real round trips and the compressed animation time.</summary>
    public static class RoundTrip
    {
        /// <summary>Speed of light in vacuum, km/s.</summary>
        public const double SpeedOfLightKmPerSecond = 299792.458;

        private const double SecondsPerMinute = 60.0;

        /// <summary>Round-trip time from a one-way light time, seconds.</summary>
        public static double Seconds(double oneWayLightSeconds) => 2.0 * oneWayLightSeconds;

        /// <summary>Round-trip time for a signal to <paramref name="distanceKm"/> and back, seconds.</summary>
        public static double SecondsFromKm(double distanceKm) => 2.0 * distanceKm / SpeedOfLightKmPerSecond;

        /// <summary><paramref name="seconds"/> rounded to the nearest whole minute.</summary>
        public static int WholeMinutes(double seconds) => (int)System.Math.Round(seconds / SecondsPerMinute);

        /// <summary>Animation length: real seconds divided by <paramref name="ratio"/>, clamped.</summary>
        public static float CompressedSeconds(double realSeconds, float ratio, float min, float max) =>
            UnityEngine.Mathf.Clamp((float)(realSeconds / ratio), min, max);
    }
}
