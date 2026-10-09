namespace PsycheVR.OpsConsole.Core
{
    /// <summary>
    /// Psyche's array power against distance from the Sun: our fit through NASA's two design points
    /// (21 kW at 1 AU, 2.3 kW at 3.33 AU), P = 21 * r^-1.838. The thruster draws up to ~4.9 kW input.
    /// Shown on screen as "approx.".
    /// </summary>
    public static class PowerModel
    {
        /// <summary>Array output at 1 AU, in kW.</summary>
        public const float KwAtOneAu = 21f;
        /// <summary>Falloff exponent, fitted through the two design points.</summary>
        public const float Exponent = -1.838f;
        /// <summary>Most input power one Hall-effect thruster draws, in kW.</summary>
        public const float ThrusterMaxKw = 4.9f;

        /// <summary>Array output in kW at <paramref name="au"/> from the Sun.</summary>
        public static float ArrayKw(float au) => KwAtOneAu * UnityEngine.Mathf.Pow(au, Exponent);

        /// <summary>Share of full thruster power the array can feed at <paramref name="au"/>, 0 to 1.</summary>
        public static float ThrusterShare(float au) => UnityEngine.Mathf.Clamp01(ArrayKw(au) / ThrusterMaxKw);
    }
}
