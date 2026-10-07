using NUnit.Framework;
using PsycheVR.OpsConsole.Core;

namespace PsycheVR.OpsConsole.Tests
{
    /// <summary>Light-time arithmetic of <see cref="RoundTrip"/>.</summary>
    public class RoundTripTests
    {
        [Test] public void RoundTripIsTwiceOneWay() => Assert.AreEqual(1885.7, RoundTrip.Seconds(942.85), 0.01);
        [Test] public void MinutesRounded() => Assert.AreEqual(31, RoundTrip.WholeMinutes(1885.7));
        [Test] public void FromDistance() => Assert.AreEqual(1885.7, RoundTrip.SecondsFromKm(282659369.1), 0.5);
        [Test] public void CompressedClamps()
        {
            Assert.AreEqual(12f, RoundTrip.CompressedSeconds(1885.7, 157f, 8f, 15f), 0.05f);
            Assert.AreEqual(8f, RoundTrip.CompressedSeconds(100, 157f, 8f, 15f));
            Assert.AreEqual(15f, RoundTrip.CompressedSeconds(100000, 157f, 8f, 15f));
        }
    }
}
