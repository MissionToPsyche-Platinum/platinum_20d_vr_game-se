using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace PsycheVR.Story.Tests
{
    public class StoryPhaseSequenceTests
    {
        [Test]
        public void StartsAtIntro()
        {
            Assert.That(new StoryPhaseSequence().CurrentPhase, Is.EqualTo(StoryPhase.Intro));
        }

        [Test]
        public void AdvancesInOrderAndFiresOncePerChange()
        {
            var sequence = new StoryPhaseSequence();
            var changes = new List<StoryPhase>();
            sequence.PhaseChanged += phase =>
            {
                Assert.That(sequence.CurrentPhase, Is.EqualTo(phase));
                changes.Add(phase);
            };
            var expected = new[]
            {
                StoryPhase.Memory, StoryPhase.Build, StoryPhase.ChildLaunch,
                StoryPhase.RealLaunch, StoryPhase.FreePlay
            };

            foreach (var phase in expected)
            {
                sequence.Advance();
                Assert.That(sequence.CurrentPhase, Is.EqualTo(phase));
            }

            Assert.That(changes, Is.EqualTo(expected));
        }

        [Test]
        public void AdvanceStopsAtFreePlayWithoutAnotherEvent()
        {
            var sequence = new StoryPhaseSequence();
            sequence.GoTo(StoryPhase.FreePlay);
            var count = 0;
            sequence.PhaseChanged += _ => count++;

            sequence.Advance();
            sequence.Advance();

            Assert.That(sequence.CurrentPhase, Is.EqualTo(StoryPhase.FreePlay));
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void GoToJumpsForwardAndBackwardAndAdvanceContinuesFromThere()
        {
            var sequence = new StoryPhaseSequence();
            var changes = new List<StoryPhase>();
            sequence.PhaseChanged += changes.Add;

            sequence.GoTo(StoryPhase.RealLaunch);
            sequence.GoTo(StoryPhase.Memory);
            sequence.Advance();

            Assert.That(sequence.CurrentPhase, Is.EqualTo(StoryPhase.Build));
            Assert.That(changes, Is.EqualTo(new[]
            {
                StoryPhase.RealLaunch, StoryPhase.Memory, StoryPhase.Build
            }));
        }

        [Test]
        public void GoToCurrentPhaseDoesNotFireAnEvent()
        {
            var sequence = new StoryPhaseSequence();
            var count = 0;
            sequence.PhaseChanged += _ => count++;

            sequence.GoTo(StoryPhase.Intro);

            Assert.That(count, Is.Zero);
        }

        [TestCase(-1)]
        [TestCase(6)]
        public void InvalidPhaseIsRejectedWithoutChangingStateOrFiringAnEvent(int phase)
        {
            var sequence = new StoryPhaseSequence();
            var count = 0;
            sequence.PhaseChanged += _ => count++;

            Assert.Throws<ArgumentOutOfRangeException>(() => sequence.GoTo((StoryPhase)phase));

            Assert.That(sequence.CurrentPhase, Is.EqualTo(StoryPhase.Intro));
            Assert.That(count, Is.Zero);
        }
    }
}
