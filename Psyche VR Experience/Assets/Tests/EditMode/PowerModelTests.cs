using NUnit.Framework;
using PsycheVR.OpsConsole.Core;

namespace PsycheVR.OpsConsole.Tests
{
    /// <summary>Array power against distance from the Sun (<see cref="PowerModel"/>).</summary>
    public class PowerModelTests
    {
        [Test] public void ArrayAtOneAu() => Assert.AreEqual(21f, PowerModel.ArrayKw(1f), 1e-4f);
        [Test] public void ArrayAtAsteroidAphelion() => Assert.AreEqual(2.3f, PowerModel.ArrayKw(3.33f), 0.05f);
        [Test] public void ThrusterFullAtOneAu() => Assert.AreEqual(1f, PowerModel.ThrusterShare(1f));
        [Test] public void ThrusterShareAtAsteroidAphelion() => Assert.AreEqual(0.47f, PowerModel.ThrusterShare(3.33f), 0.02f);
    }
}
