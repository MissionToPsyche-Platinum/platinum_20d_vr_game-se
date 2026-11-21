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

        // Each presentation gets its own child object, as the prefabs do in Bedroom: the card and the highlight
        // switch their own object off when hidden, which would switch the director off with them.
        private Component Presentation(string typeName, string field)
        {
            var child = new GameObject(typeName);
            child.transform.SetParent(owner.transform);
            var component = child.AddComponent(Type.GetType($"PsycheVR.Story.{typeName}, Assembly-CSharp", true));
            directorType.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, component);
            return component;
        }

        private object Property(Component component, string name) => component.GetType().GetProperty(name).GetValue(component);

        [Test]
        public void HighlightHoversOverTheMeshInsteadOfTheTargetPivot()
        {
            var highlight = Presentation("ObjectiveHighlight", "objectiveHighlight");
            var target = new GameObject("Target");
            target.transform.SetParent(owner.transform);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.transform.SetParent(target.transform);
            mesh.transform.localPosition = new Vector3(2, 1, 3);
            mesh.transform.localScale = new Vector3(2, 1, 4);
            highlight.GetType().GetMethod("Show").Invoke(highlight, new object[] { target.transform });
            // Centred over the cube (top at y 1.5), above it by the hover height plus at most one bob.
            var position = highlight.transform.position;
            Assert.That(position.x, Is.EqualTo(2).Within(0.001f));
            Assert.That(position.z, Is.EqualTo(3).Within(0.001f));
            Assert.That(position.y, Is.InRange(1.5f, 1.75f));
        }

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
            Assert.That(Property(highlight, "IsVisible"), Is.True);

            LogAssert.Expect(LogType.Log, "[StoryDirector] Entering Memory.");
            Call("Advance");
            Assert.That(Property(card, "IsVisible"), Is.False);
            Assert.That(Property(highlight, "Target"), Is.Null);
            Assert.That(Property(highlight, "IsVisible"), Is.False);

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
            Assert.That(Property(highlight, "IsVisible"), Is.False);
        }

        [Test]
        public void UnassignedSpacecraftStillAllowsIntroDateCard()
        {
            var card = Presentation("StoryDateCard", "dateCard");
            var highlight = Presentation("ObjectiveHighlight", "objectiveHighlight");
            StartStory();
            Assert.That(Property(card, "IsVisible"), Is.True);
            Assert.That(Property(highlight, "Target"), Is.Null);
            Assert.That(Property(highlight, "IsVisible"), Is.False);
        }

        private object CreateGrabTarget()
        {
            var target = new GameObject("Grab target");
            target.transform.SetParent(owner.transform);
            target.SetActive(false);
            target.AddComponent<BoxCollider>();
            var grabType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit", true);
            var grab = target.AddComponent(grabType);
            directorType.GetField("introSpacecraft", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(director, target.transform);
            return grabType.GetProperty("selectEntered").GetValue(grab);
        }

        private static void Grab(object grabEvent)
        {
            var argsType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs, Unity.XR.Interaction.Toolkit", true);
            grabEvent.GetType().GetMethod("Invoke").Invoke(grabEvent, new[] { Activator.CreateInstance(argsType) });
        }

        [Test]
        public void GrabEntersMemoryHidesHighlightAndDoesNotRepeat()
        {
            var grabEvent = CreateGrabTarget();
            var highlight = Presentation("ObjectiveHighlight", "objectiveHighlight");
            StartStory();
            Assert.That(Property(highlight, "Target"), Is.Not.Null);
            LogAssert.Expect(LogType.Log, "[StoryDirector] Entering Memory.");
            Grab(grabEvent);
            Grab(grabEvent);
            Assert.That(Phase, Is.EqualTo(StoryPhase.Memory));
            Assert.That(Property(highlight, "Target"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GrabSubscriptionRestoresAfterDisableAndIntroReentry()
        {
            var grabEvent = CreateGrabTarget();
            StartStory();
            director.enabled = false;
            Grab(grabEvent);
            Assert.That(Phase, Is.EqualTo(StoryPhase.Intro));
            director.enabled = true;
            LogAssert.Expect(LogType.Log, "[StoryDirector] Entering Memory.");
            Grab(grabEvent);
            LogAssert.Expect(LogType.Log, "[StoryDirector] Entering Intro.");
            Call("GoTo", StoryPhase.Intro);
            LogAssert.Expect(LogType.Log, "[StoryDirector] Entering Memory.");
            Grab(grabEvent);
            Grab(grabEvent);
            Assert.That(Phase, Is.EqualTo(StoryPhase.Memory));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GrabDoesNothingInEventModeOrBeforeStartup()
        {
            var grabEvent = CreateGrabTarget();
            Grab(grabEvent);
            Mode("Event");
            Call("Start");
            Grab(grabEvent);
            Assert.That(Phase, Is.EqualTo(StoryPhase.Intro));
            LogAssert.NoUnexpectedReceived();
        }

    }
}
