using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PsycheVR.Story.Tests
{
    public class StoryDirectorTests
    {
        // Like SessionUploadTests, resolve Assembly-CSharp types at runtime:
        // Unity test asmdefs cannot reference predefined assemblies directly.
        private Type directorType;
        private PropertyInfo activeMode;
        private object previousMode;
        private GameObject owner;
        private Behaviour director;

        [SetUp]
        public void SetUp()
        {
            directorType = Type.GetType("PsycheVR.Story.StoryDirector, Assembly-CSharp", true);
            var manager = Type.GetType("PsycheVR.Modes.GameModeManager, Assembly-CSharp", true);
            activeMode = manager.GetProperty("ActiveMode");
            previousMode = activeMode.GetValue(null);
            owner = new GameObject("StoryDirector test");
            director = (Behaviour)owner.AddComponent(directorType);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(owner);
            activeMode.SetValue(null, previousMode);
        }

        private void Mode(string name) => activeMode.SetValue(null, Enum.Parse(activeMode.PropertyType, name));
        private StoryPhase Phase => (StoryPhase)directorType.GetProperty("CurrentPhase").GetValue(director);
        private void Call(string name, params object[] args) => directorType.GetMethod(
            name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(director, args);

        private void StartStory()
        {
            Mode("Story");
            LogAssert.Expect(LogType.Log, "[StoryDirector] Entering Intro.");
            Call("Start");
        }

        [Test]
        public void EventModeDoesNotStartAdvanceOrJump()
        {
            Mode("Event");
            Call("Start");
            Call("Advance");
            Call("GoTo", StoryPhase.RealLaunch);
            Assert.That(Phase, Is.EqualTo(StoryPhase.Intro));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void StoryStartsAtIntroAndLogsEveryTransitionOnce()
        {
            StartStory();
            Call("Start");
            foreach (var phase in new[] { StoryPhase.Memory, StoryPhase.Build,
                         StoryPhase.ChildLaunch, StoryPhase.RealLaunch, StoryPhase.FreePlay })
            {
                LogAssert.Expect(LogType.Log, $"[StoryDirector] Entering {phase}.");
                Call("Advance");
                Assert.That(Phase, Is.EqualTo(phase));
            }
            Call("Advance");
            Call("GoTo", StoryPhase.FreePlay);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DisabledDirectorAndEventModeRejectTransitions()
        {
            StartStory();
            director.enabled = false;
            Call("Advance");
            Call("GoTo", StoryPhase.Build);
            director.enabled = true;
            Mode("Event");
            Call("Advance");
            Call("GoTo", StoryPhase.Build);
            Assert.That(Phase, Is.EqualTo(StoryPhase.Intro));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GoToLogsTheRequestedPhase()
        {
            StartStory();
            LogAssert.Expect(LogType.Log, "[StoryDirector] Entering RealLaunch.");
            Call("GoTo", StoryPhase.RealLaunch);
            Assert.That(Phase, Is.EqualTo(StoryPhase.RealLaunch));
        }

        private Component Presentation(string typeName, string field)
        {
            var component = owner.AddComponent(Type.GetType($"PsycheVR.Story.{typeName}, Assembly-CSharp", true));
            directorType.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, component);
            return component;
        }

        private object Property(Component component, string name) => component.GetType().GetProperty(name).GetValue(component);

        [Test]
        public void IntroShowsResourceTextAndTargetThenHidesBothOnExit()
        {
            var card = Presentation("StoryDateCard", "dateCard");
            var highlight = Presentation("ObjectiveHighlight", "objectiveHighlight");
            directorType.GetField("introSpacecraft", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(director, owner.transform);
            StartStory();
            var text = Resources.Load("StoryText");
            Assert.That(Property(card, "CurrentText"), Is.EqualTo(text.GetType().GetProperty("IntroDateText").GetValue(text)));
            Assert.That(Property(card, "IsVisible"), Is.True);
            Assert.That(Property(highlight, "Target"), Is.EqualTo(owner.transform));
            Assert.That(owner.GetComponentInChildren<LineRenderer>().enabled, Is.True);

            LogAssert.Expect(LogType.Log, "[StoryDirector] Entering Memory.");
            Call("Advance");
            Assert.That(Property(card, "IsVisible"), Is.False);
            Assert.That(Property(highlight, "Target"), Is.Null);
            Assert.That(owner.GetComponentInChildren<LineRenderer>().enabled, Is.False);

            LogAssert.Expect(LogType.Log, "[StoryDirector] Entering Intro.");
            Call("GoTo", StoryPhase.Intro);
            Assert.That(Property(card, "IsVisible"), Is.True);
            Assert.That(Property(highlight, "Target"), Is.EqualTo(owner.transform));
        }

        [Test]
        public void EventModeLeavesPresentationUntouched()
        {
            var card = Presentation("StoryDateCard", "dateCard");
            var highlight = Presentation("ObjectiveHighlight", "objectiveHighlight");
            Mode("Event");
            Call("Start");
            Assert.That(Property(card, "CurrentText"), Is.Null);
            Assert.That(Property(card, "IsVisible"), Is.False);
            Assert.That(Property(highlight, "Target"), Is.Null);
            Assert.That(owner.transform.childCount, Is.Zero);
        }

        [Test]
        public void UnassignedSpacecraftStillAllowsIntroDateCard()
        {
            var card = Presentation("StoryDateCard", "dateCard");
            var highlight = Presentation("ObjectiveHighlight", "objectiveHighlight");
            StartStory();
            Assert.That(Property(card, "IsVisible"), Is.True);
            Assert.That(Property(highlight, "Target"), Is.Null);
            Assert.That(owner.GetComponentInChildren<LineRenderer>(), Is.Null);
        }

    }
}
