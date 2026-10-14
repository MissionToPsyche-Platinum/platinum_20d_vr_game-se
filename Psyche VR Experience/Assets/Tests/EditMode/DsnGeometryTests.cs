using NUnit.Framework;
using PsycheVR.OpsConsole.Core;

namespace PsycheVR.OpsConsole.Tests
{
    /// <summary>Which Deep Space Communications Complex faces a direction as Earth turns (<see cref="DsnGeometry"/>).</summary>
    public class DsnGeometryTests
    {
        private const float TurnStepDeg = 120f;

        [Test] public void GoldstoneFacesItsOwnLongitude() =>
            Assert.AreEqual(DsnGeometry.Goldstone, DsnGeometry.Facing(0f, -116.89f));

        [Test] public void MadridFacesItsOwnLongitude() =>
            Assert.AreEqual(DsnGeometry.Madrid, DsnGeometry.Facing(0f, -4.25f));

        [Test] public void CanberraFacesItsOwnLongitude() =>
            Assert.AreEqual(DsnGeometry.Canberra, DsnGeometry.Facing(0f, 148.98f));

        [Test]
        public void TurningByOneThirdHandsToTheNextComplex()
        {
            // Earth turns eastward: Goldstone's direction is reached next by Canberra (the next complex west), then Madrid
            float target = DsnGeometry.Longitudes[DsnGeometry.Goldstone];
            Assert.AreEqual(DsnGeometry.Goldstone, DsnGeometry.Facing(0f, target));
            Assert.AreEqual(DsnGeometry.Canberra, DsnGeometry.Facing(TurnStepDeg, target));
            Assert.AreEqual(DsnGeometry.Madrid, DsnGeometry.Facing(2f * TurnStepDeg, target));
            Assert.AreEqual(DsnGeometry.Goldstone, DsnGeometry.Facing(3f * TurnStepDeg, target));
        }

        [Test]
        public void WrapsAroundAtTheDateLine()
        {
            // Canberra at 148.98 is 31.98 degrees from -180 across the date line, Goldstone 63.11 the other way
            Assert.AreEqual(DsnGeometry.Canberra, DsnGeometry.Facing(0f, -180f));
            Assert.AreEqual(DsnGeometry.Canberra, DsnGeometry.Facing(0f, 180f));
            // rotations past a full turn and negative rotations give the same answer
            Assert.AreEqual(DsnGeometry.Madrid, DsnGeometry.Facing(720f, -4.25f));
            Assert.AreEqual(DsnGeometry.Madrid, DsnGeometry.Facing(-360f, -4.25f));
        }
    }
}
