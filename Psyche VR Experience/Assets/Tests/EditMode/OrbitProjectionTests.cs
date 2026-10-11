using System;
using NUnit.Framework;
using PsycheVR.OpsConsole.Core;
using UnityEngine;

namespace PsycheVR.OpsConsole.Tests
{
    /// <summary>AU to panel mapping (<see cref="OrbitProjection"/>) and the animation clock (<see cref="OrbitProjection.Timeline"/>).</summary>
    public class OrbitProjectionTests
    {
        private const float Margin = 5f;
        private static readonly Rect Square = new Rect(-100f, -100f, 200f, 200f);
        private static readonly Rect Wide = new Rect(0f, 0f, 300f, 200f);
        private static readonly DateTime Start = new DateTime(2023, 10, 14);
        private static readonly DateTime End = new DateTime(2029, 8, 1);

        [Test] public void SunAtCentre()
        {
            var p = new OrbitProjection(Wide, Margin);
            Assert.AreEqual(new Vector2(150f, 100f), p.ToPanel(Vector2.zero));
        }

        [Test] public void MaxRadiusAtRightEdgeMinusMargin()
        {
            var p = new OrbitProjection(Square, Margin);
            Vector2 v = p.ToPanel(new Vector2(OrbitProjection.DefaultMaxRadiusAu, 0f));
            Assert.AreEqual(Square.xMax - Margin, v.x, 1e-4f);
            Assert.AreEqual(Square.center.y, v.y, 1e-4f);
        }

        [Test] public void YIsUpAndScaleFollowsTheShortSide()
        {
            var p = new OrbitProjection(Wide, Margin);
            Vector2 v = p.ToPanel(new Vector2(0f, OrbitProjection.DefaultMaxRadiusAu));
            Assert.AreEqual(Wide.yMax - Margin, v.y, 1e-4f);
            Assert.AreEqual((100f - Margin) / OrbitProjection.DefaultMaxRadiusAu, p.UnitsPerAu, 1e-4f);
        }

        [Test] public void TimelineEnds()
        {
            var t = new OrbitProjection.Timeline(Start, End, 10f);
            Assert.AreEqual(Start, t.At(0f));
            Assert.AreEqual(End, t.At(10f));
        }

        [Test] public void TimelineMidpoint()
        {
            var t = new OrbitProjection.Timeline(Start, End, 10f);
            var mid = Start + TimeSpan.FromTicks((End - Start).Ticks / 2);
            Assert.AreEqual(0, (t.At(5f) - mid).TotalHours, 1.0);
        }

        [Test] public void TimelineInverse()
        {
            var t = new OrbitProjection.Timeline(Start, End, 10f);
            Assert.AreEqual(5f, t.SecondsAt(t.At(5f)), 1e-3f);
            Assert.AreEqual(0f, t.SecondsAt(Start.AddDays(-30)));
            Assert.AreEqual(10f, t.SecondsAt(End.AddDays(30)));
        }
    }
}
