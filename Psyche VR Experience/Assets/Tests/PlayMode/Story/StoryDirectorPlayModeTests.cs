using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace PsycheVR.Story.PlayModeTests
{
    // InputTestFixture isolates keyboard input and feeds it through the real
    // player loop. No direct calls to StoryDirector.Start, Update, or Advance.
    public class StoryDirectorPlayModeTests : InputTestFixture
    {
        private GameObject owner;
        private Component director;
        private Component card;
        private Component highlight;
        private Keyboard keyboard;
        private PropertyInfo activeMode;
        private object previousMode;
        private readonly List<string> phaseLogs = new List<string>();

        public override void Setup()
        {
            base.Setup();
            Assert.That(Application.isPlaying, Is.True);
            var manager = RuntimeType("PsycheVR.Modes.GameModeManager");
            activeMode = manager.GetProperty("ActiveMode");
            previousMode = activeMode.GetValue(null);
            keyboard = InputSystem.AddDevice<Keyboard>();
            phaseLogs.Clear();
            Application.logMessageReceived += CapturePhaseLog;
        }

        public override void TearDown()
        {
            Application.logMessageReceived -= CapturePhaseLog;
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            if (activeMode != null) activeMode.SetValue(null, previousMode);
            base.TearDown();
        }

        private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Read(Component component, string name) => component.GetType().GetProperty(name).GetValue(component);
        private void Assign(string name, object value) => director.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(director, value);
        private StoryPhase Phase => (StoryPhase)Read(director, "CurrentPhase");

        private void CapturePhaseLog(string message, string stackTrace, LogType type)
        {
            if (message.StartsWith("[StoryDirector] Entering ", StringComparison.Ordinal))
                phaseLogs.Add(message);
        }

        private void CreateDirector(string mode)
        {
            activeMode.SetValue(null, Enum.Parse(activeMode.PropertyType, mode));
            owner = new GameObject("Story PlayMode fixture");
            owner.SetActive(false);
            director = owner.AddComponent(RuntimeType("PsycheVR.Story.StoryDirector"));
            card = owner.AddComponent(RuntimeType("PsycheVR.Story.StoryDateCard"));
            highlight = owner.AddComponent(RuntimeType("PsycheVR.Story.ObjectiveHighlight"));
            Assign("dateCard", card);
            Assign("objectiveHighlight", highlight);
            Assign("introSpacecraft", owner.transform);
            owner.SetActive(true);
        }

        [UnityTest]
        public IEnumerator StoryStartupShowsIntroThenF8AdvancesOncePerPressAndStopsAtFreePlay()
        {
            CreateDirector("Story");
            yield return null; // Unity invokes Start and Update.
            Assert.That(Phase, Is.EqualTo(StoryPhase.Intro));
            Assert.That(Read(card, "IsVisible"), Is.True);
            var text = Resources.Load("StoryText");
            Assert.That(Read(card, "CurrentText"), Is.EqualTo(text.GetType().GetProperty("IntroDateText").GetValue(text)));
            Assert.That(Read(highlight, "Target"), Is.EqualTo(owner.transform));
            Assert.That(owner.GetComponentInChildren<LineRenderer>().enabled, Is.True);

            var expected = new List<string> { "[StoryDirector] Entering Intro." };
            foreach (var phase in new[] { StoryPhase.Memory, StoryPhase.Build,
                         StoryPhase.ChildLaunch, StoryPhase.RealLaunch, StoryPhase.FreePlay })
            {
                Press(keyboard.f8Key, queueEventOnly: true);
                yield return null;
                Assert.That(Phase, Is.EqualTo(phase));
                expected.Add($"[StoryDirector] Entering {phase}.");
                yield return null; // Holding F8 must not repeat on the next frame.
                Assert.That(Phase, Is.EqualTo(phase));
                Assert.That(phaseLogs, Is.EqualTo(expected));
                Release(keyboard.f8Key, queueEventOnly: true);
                yield return null;
            }

            Assert.That(Read(card, "IsVisible"), Is.False);
            Assert.That(Read(highlight, "Target"), Is.Null);
            Assert.That(owner.GetComponentInChildren<LineRenderer>().enabled, Is.False);
            Press(keyboard.f8Key, queueEventOnly: true);
            yield return null;
            Assert.That(Phase, Is.EqualTo(StoryPhase.FreePlay));
            Assert.That(phaseLogs, Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator EventModeIgnoresF8AndLeavesIntroPresentationInactive()
        {
            CreateDirector("Event");
            yield return null;
            Press(keyboard.f8Key, queueEventOnly: true);
            yield return null;
            Release(keyboard.f8Key, queueEventOnly: true);
            yield return null;
            Assert.That(Phase, Is.EqualTo(StoryPhase.Intro));
            Assert.That(Read(card, "IsVisible"), Is.False);
            Assert.That(Read(card, "CurrentText"), Is.Null);
            Assert.That(Read(highlight, "Target"), Is.Null);
            Assert.That(owner.transform.childCount, Is.Zero);
            Assert.That(phaseLogs, Is.Empty);
        }

        [UnityTest]
        public IEnumerator StoryStartupWithoutKeyboardStillEntersIntro()
        {
            InputSystem.RemoveDevice(keyboard);
            CreateDirector("Story");
            yield return null;
            yield return null;
            Assert.That(Phase, Is.EqualTo(StoryPhase.Intro));
            Assert.That(Read(card, "IsVisible"), Is.True);
            Assert.That(phaseLogs, Is.EqualTo(new[] { "[StoryDirector] Entering Intro." }));
        }
    }
}
