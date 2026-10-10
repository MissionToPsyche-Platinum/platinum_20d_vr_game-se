using System;
using NUnit.Framework;
using PsycheVR.OpsConsole.Core;

namespace PsycheVR.OpsConsole.Tests
{
    /// <summary>Behaviour of <see cref="ConsoleState"/>: paging, power, the ping and attract mode.</summary>
    public class ConsoleStateTests
    {
        private const int TabCount = 4;
        private const float IdleSeconds = 45f;
        private const float AttractStepSeconds = 10f;
        private const int DsnTab = 1;
        private const float OneSecond = 1f;
        private const float LongIdle = IdleSeconds * 2f;

        private static ConsoleState Make() =>
            new ConsoleState(tabCount: TabCount, idleSeconds: IdleSeconds, attractStepSeconds: AttractStepSeconds);

        [Test]
        public void StartsOnFirstTabPoweredOn()
        {
            var s = Make();
            Assert.AreEqual(0, s.CurrentTab);
            Assert.IsTrue(s.IsOn);
            Assert.IsFalse(s.IsAttracting);
        }

        [Test]
        public void NextWrapsForward()
        {
            var s = Make();
            for (int i = 0; i < TabCount; i++) Assert.IsTrue(s.Next());
            Assert.AreEqual(0, s.CurrentTab);
        }

        [Test]
        public void PreviousWrapsBackward()
        {
            var s = Make();
            Assert.IsTrue(s.Previous());
            Assert.AreEqual(TabCount - 1, s.CurrentTab);
        }

        [Test]
        public void PagingIgnoredWhileOff()
        {
            var s = Make();
            s.SetPower(false);
            Assert.IsFalse(s.Next());
            Assert.AreEqual(0, s.CurrentTab);
        }

        [Test]
        public void PowerOnKeepsTab()
        {
            var s = Make();
            s.Next();
            s.SetPower(false);
            s.SetPower(true);
            Assert.AreEqual(1, s.CurrentTab);
        }

        [Test]
        public void PagingAllowedDuringPing()
        {
            var s = Make();
            Assert.IsTrue(s.StartPing(DsnTab));
            Assert.AreEqual(DsnTab, s.CurrentTab);
            Assert.IsTrue(s.Next());
            Assert.AreEqual(DsnTab + 1, s.CurrentTab);
            Assert.IsTrue(s.PingInFlight);   // the ping keeps going in the background
        }

        [Test]
        public void PingNeedsPower()
        {
            var s = Make();
            s.SetPower(false);
            Assert.IsFalse(s.StartPing(DsnTab));
        }

        [Test]
        public void PowerOffCancelsPing()
        {
            var s = Make();
            s.StartPing(DsnTab);
            s.SetPower(false);
            Assert.IsFalse(s.PingInFlight);
        }

        [Test]
        public void PingCompleteAllowsAnotherPing()
        {
            var s = Make();
            s.StartPing(DsnTab);
            s.CompletePing();
            Assert.IsFalse(s.PingInFlight);
            Assert.IsTrue(s.StartPing(DsnTab));
        }

        [Test]
        public void IdleStartsAttractThenSteps()
        {
            var s = Make();
            Assert.IsFalse(s.Tick(IdleSeconds - OneSecond));
            Assert.IsFalse(s.Tick(OneSecond));
            Assert.IsTrue(s.IsAttracting);
            Assert.AreEqual(0, s.CurrentTab);
            Assert.IsTrue(s.Tick(AttractStepSeconds));
            Assert.AreEqual(1, s.CurrentTab);
        }

        [Test]
        public void AnyInputEndsAttract()
        {
            var s = Make();
            s.Tick(IdleSeconds + OneSecond);
            s.Next();
            Assert.IsFalse(s.IsAttracting);
            s.Tick(IdleSeconds - OneSecond);
            Assert.IsFalse(s.IsAttracting);
            s.Tick(OneSecond);
            Assert.IsTrue(s.IsAttracting);
        }

        [Test]
        public void IdleClockPausedWhileOffOrPinging()
        {
            var s = Make();
            s.SetPower(false);
            s.Tick(LongIdle);
            Assert.IsFalse(s.IsAttracting);
            s.SetPower(true);
            s.StartPing(DsnTab);
            s.Tick(LongIdle);
            Assert.IsFalse(s.IsAttracting);
        }

        [Test]
        public void ConstructorRejectsZeroTabs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ConsoleState(tabCount: 0, idleSeconds: IdleSeconds, attractStepSeconds: AttractStepSeconds));
        }

        [Test]
        public void StartPingRejectsTabOutOfRange()
        {
            var s = Make();
            Assert.Throws<ArgumentOutOfRangeException>(() => s.StartPing(TabCount));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.StartPing(-1));
        }

        [Test]
        public void StartPingRefusedWhilePingInFlight()
        {
            var s = Make();
            Assert.IsTrue(s.StartPing(DsnTab));
            Assert.IsFalse(s.StartPing(DsnTab));
        }

        [Test]
        public void PreviousIgnoredWhileOff()
        {
            var s = Make();
            s.SetPower(false);
            Assert.IsFalse(s.Previous());
            Assert.AreEqual(0, s.CurrentTab);
        }

        [Test]
        public void PowerOnEndsAttract()
        {
            var s = Make();
            s.Tick(IdleSeconds + OneSecond);
            Assert.IsTrue(s.IsAttracting);
            s.SetPower(true);
            Assert.IsFalse(s.IsAttracting);
        }
    }
}
