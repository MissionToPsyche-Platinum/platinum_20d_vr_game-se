using UnityEngine;

namespace PsycheVR.OpsConsole.Core
{
    /// <summary>
    /// The three Deep Space Communications Complexes on a turning Earth, seen from above the North Pole:
    /// which one faces a given direction. Angles are degrees, counter-clockwise (east) positive; a complex
    /// sits at its longitude plus Earth's rotation.
    /// </summary>
    public static class DsnGeometry
    {
        /// <summary>Index of Goldstone (California) in <see cref="Longitudes"/> and <see cref="Names"/>.</summary>
        public const int Goldstone = 0;
        /// <summary>Index of Madrid (Spain).</summary>
        public const int Madrid = 1;
        /// <summary>Index of Canberra (Australia).</summary>
        public const int Canberra = 2;
        /// <summary>Number of complexes.</summary>
        public const int Count = 3;

        private const float HalfTurnDeg = 180f;

        /// <summary>East longitude of each complex, degrees.</summary>
        public static readonly float[] Longitudes = { -116.89f, -4.25f, 148.98f };

        /// <summary>Short on-screen name of each complex.</summary>
        public static readonly string[] Names = { "Goldstone", "Madrid", "Canberra" };

        /// <summary>
        /// Where complex <paramref name="index"/> sits with Earth turned by <paramref name="earthRotationDeg"/>,
        /// in degrees from -180 (inclusive) to 180.
        /// </summary>
        public static float AngleOf(int index, float earthRotationDeg) => Wrap(Longitudes[index] + earthRotationDeg);

        /// <summary>
        /// The index of the complex whose position (longitude + <paramref name="earthRotationDeg"/>) is
        /// angularly closest to <paramref name="targetDeg"/>, across the ±180° wrap.
        /// </summary>
        public static int Facing(float earthRotationDeg, float targetDeg)
        {
            int best = 0;
            float bestGap = float.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                float gap = Mathf.Abs(Wrap(AngleOf(i, earthRotationDeg) - targetDeg));
                if (gap < bestGap) { bestGap = gap; best = i; }
            }
            return best;
        }

        // to -180 (inclusive) .. 180
        private static float Wrap(float deg) => Mathf.Repeat(deg + HalfTurnDeg, 2f * HalfTurnDeg) - HalfTurnDeg;
    }
}
