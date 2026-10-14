using NUnit.Framework;
using PsycheVR.OpsConsole.Core;

namespace PsycheVR.OpsConsole.Tests
{
    /// <summary>How a ping reaches the DSN tab (<see cref="PingRoute"/>).</summary>
    public class PingRouteTests
    {
        private const int Dsn = 1;

        [Test] public void StartsInPlaceWhenTheDsnTabShowsAndNothingSlides() =>
            Assert.AreEqual(0, PingRoute.SlideDirection(Dsn, Dsn, false));

        [Test] public void SlidesForwardFromAnEarlierTab() =>
            Assert.AreEqual(+1, PingRoute.SlideDirection(0, Dsn, false));

        [Test] public void SlidesBackFromALaterTab() =>
            Assert.AreEqual(-1, PingRoute.SlideDirection(3, Dsn, false));

        [Test] public void SlidesWhenTheScreenIsMidSlideEvenFromTheDsnTab() =>
            Assert.AreNotEqual(0, PingRoute.SlideDirection(Dsn, Dsn, true));

        [Test] public void SlidesWhenNoTabShowsYet() =>
            Assert.AreNotEqual(0, PingRoute.SlideDirection(-1, Dsn, false));
    }
}
