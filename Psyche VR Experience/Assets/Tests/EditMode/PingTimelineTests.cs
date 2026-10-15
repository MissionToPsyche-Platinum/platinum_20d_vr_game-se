using System;
using NUnit.Framework;
using PsycheVR.OpsConsole.Core;

namespace PsycheVR.OpsConsole.Tests
{
    /// <summary>The ping animation's phases, dot position and simulated real time (<see cref="PingTimeline"/>).</summary>
    public class PingTimelineTests
    {
        private const double RealRoundTrip = 1885.7;   // 31 min, the build-date snapshot
        private const float Total = 12f, Blink = 1f;   // legs of 5.5 s each
        private const float Leg = 5.5f;

        private static PingTimeline Timeline() => new PingTimeline(Total, Blink, RealRoundTrip);

        [Test] public void LegIsHalfOfWhatTheBlinkLeaves() => Assert.AreEqual(Leg, Timeline().LegSeconds, 1e-5f);

        [Test]
        public void PhasesRunOutBlinkBackDone()
        {
            var t = Timeline();
            Assert.AreEqual(PingPhase.Out, t.PhaseAt(0f));
            Assert.AreEqual(PingPhase.Out, t.PhaseAt(Leg - 0.01f));
            Assert.AreEqual(PingPhase.Blink, t.PhaseAt(Leg + Blink * 0.5f));
            Assert.AreEqual(PingPhase.Back, t.PhaseAt(Leg + Blink + 0.01f));
            Assert.AreEqual(PingPhase.Back, t.PhaseAt(Total - 0.01f));
            Assert.AreEqual(PingPhase.Done, t.PhaseAt(Total));
            Assert.AreEqual(PingPhase.Done, t.PhaseAt(Total + 5f));
        }

        [Test]
        public void PositionGoesOutHoldsAtTheSpacecraftAndComesBack()
        {
            var t = Timeline();
            Assert.AreEqual(0f, t.PositionAt(0f), 1e-5f);
            Assert.AreEqual(0.5f, t.PositionAt(Leg * 0.5f), 1e-5f);
            Assert.AreEqual(1f, t.PositionAt(Leg + Blink * 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, t.PositionAt(Leg + Blink + Leg * 0.5f), 1e-5f);
            Assert.AreEqual(0f, t.PositionAt(Total), 1e-5f);
        }

        [Test]
        public void RealTimeReachesOneWayAtTheSpacecraftAndTheFullTripAtTheEnd()
        {
            var t = Timeline();
            Assert.AreEqual(0.0, t.RealSecondsAt(0f), 1e-6);
            Assert.AreEqual(RealRoundTrip / 4.0, t.RealSecondsAt(Leg * 0.5f), 1e-3);
            Assert.AreEqual(RealRoundTrip / 2.0, t.RealSecondsAt(Leg), 1e-3);
            Assert.AreEqual(RealRoundTrip / 2.0, t.RealSecondsAt(Leg + Blink * 0.5f), 1e-3);   // the turnaround takes no real time
            Assert.AreEqual(RealRoundTrip * 0.75, t.RealSecondsAt(Leg + Blink + Leg * 0.5f), 1e-3);
            Assert.AreEqual(RealRoundTrip, t.RealSecondsAt(Total), 1e-6);
            Assert.AreEqual(RealRoundTrip, t.RealSecondsAt(Total + 3f), 1e-6);
        }

        [Test] public void NegativeTimeCountsAsTheStart() => Assert.AreEqual(0f, Timeline().PositionAt(-1f));

        [Test]
        public void ForRoundTripCompressesThirtyOneMinutesToAboutTwelveSeconds()
        {
            var t = PingTimeline.ForRoundTrip(RealRoundTrip);
            Assert.AreEqual(12f, t.TotalSeconds, 0.05f);
            Assert.AreEqual(12, t.WaitedSeconds);
            Assert.AreEqual(31, t.RoundTripMinutes);
            Assert.AreEqual(RealRoundTrip, t.RealRoundTripSeconds);
        }

        [Test]
        public void ClockShowsMinutesAndWholeSeconds()
        {
            Assert.AreEqual("0:00", PingTimeline.Clock(0));
            Assert.AreEqual("1:01", PingTimeline.Clock(61.9));
            Assert.AreEqual("31:25", PingTimeline.Clock(RealRoundTrip));
        }

        [Test]
        public void ArrivalClockAddsTheOneWayTime()
        {
            Assert.AreEqual("3:05 PM", PingTimeline.ArrivalClock(new DateTime(2026, 10, 10, 14, 50, 0), 942.85));
            Assert.AreEqual("9:15 AM", PingTimeline.ArrivalClock(new DateTime(2026, 10, 10, 9, 0, 0), 942.85));
        }

        [Test]
        public void RejectsImpossibleTimings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingTimeline(0f, 0f, RealRoundTrip));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingTimeline(Total, -1f, RealRoundTrip));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingTimeline(Total, Total, RealRoundTrip));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PingTimeline(Total, Blink, -1.0));
        }
    }
}
