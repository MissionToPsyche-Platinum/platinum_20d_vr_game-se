using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace PsycheVR.Story.PlayModeTests
{
    public class StoryLaunchPlayModeTests
    {
        private GameObject owner;
        private Behaviour director;
        private Component button;
        private Behaviour interactable;
        private UnityEvent completed;
        private PropertyInfo activeMode;
        private object previousMode;

        private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static void Assign(Component target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(Component target, string name, params object[] args) => target.GetType()
            .GetMethod(name).Invoke(target, args);
        private StoryPhase Phase => (StoryPhase)director.GetType().GetProperty("CurrentPhase").GetValue(director);

        [SetUp]
        public void SetUp()
        {
            activeMode = RuntimeType("PsycheVR.Modes.GameModeManager").GetProperty("ActiveMode");
            previousMode = activeMode.GetValue(null);
        }

        [TearDown]
        public void TearDown()
        {
            if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            activeMode.SetValue(null, previousMode);
        }

        private void CreateFixture(string mode)
        {
            activeMode.SetValue(null, Enum.Parse(activeMode.PropertyType, mode));
            owner = new GameObject("Story launch fixture");
            owner.SetActive(false);
            director = (Behaviour)owner.AddComponent(RuntimeType("PsycheVR.Story.StoryDirector"));

            var buttonObject = new GameObject("Mission control button");
            buttonObject.transform.SetParent(owner.transform);
            buttonObject.AddComponent<BoxCollider>();
            interactable = (Behaviour)buttonObject.AddComponent(Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable, Unity.XR.Interaction.Toolkit", true));
            button = buttonObject.AddComponent(RuntimeType("ButtonController"));
            var top = new GameObject("Button top");
            top.transform.SetParent(buttonObject.transform);
            button.GetType().GetField("buttonTop").SetValue(button, top.transform);

            var video = owner.AddComponent(RuntimeType("play_video"));
            completed = (UnityEvent)video.GetType().GetProperty("OnVideoCompleted").GetValue(video);
            Assign(button, "videoToStart", video);
            Assign(director, "realLaunchButton", button);
            Assign(director, "launchVideo", video);
            owner.SetActive(true);
        }

        [UnityTest]
        public IEnumerator RealLaunchUnlocksButtonAndVideoCompletionReachesFreePlayOnce()
        {
            CreateFixture("Story");
            yield return null;
            Assert.That(interactable.enabled, Is.False);
            Call(director, "GoTo", StoryPhase.RealLaunch);
            Assert.That(interactable.enabled, Is.True);

            completed.Invoke(); // Same notification emitted by VideoPlayer.loopPointReached.
            Assert.That(Phase, Is.EqualTo(StoryPhase.FreePlay));
            completed.Invoke();
            Assert.That(Phase, Is.EqualTo(StoryPhase.FreePlay));
        }

        [UnityTest]
        public IEnumerator CompletionOutsideRealLaunchDoesNotSkipStoryPhases()
        {
            CreateFixture("Story");
            yield return null;
            foreach (var phase in new[] { StoryPhase.Intro, StoryPhase.Memory,
                         StoryPhase.Build, StoryPhase.ChildLaunch })
            {
                Call(director, "GoTo", phase);
                completed.Invoke();
                Assert.That(Phase, Is.EqualTo(phase));
                Assert.That(interactable.enabled, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator DisabledDirectorIgnoresCompletionAndResubscribesOnEnable()
        {
            CreateFixture("Story");
            yield return null;
            Call(director, "GoTo", StoryPhase.RealLaunch);
            for (var i = 0; i < 3; i++)
            {
                director.enabled = false;
                completed.Invoke();
                Assert.That(Phase, Is.EqualTo(StoryPhase.RealLaunch));
                director.enabled = true;
            }
            completed.Invoke();
            Assert.That(Phase, Is.EqualTo(StoryPhase.FreePlay));
        }

        [UnityTest]
        public IEnumerator EventModeLeavesButtonAndVideoListenersUnderExistingControl()
        {
            CreateFixture("Event");
            var existingCompletions = 0;
            completed.AddListener(() => existingCompletions++);
            yield return null;
            Call(director, "GoTo", StoryPhase.RealLaunch);
            Assert.That(interactable.enabled, Is.False);
            Call(button, "UnlockButton");
            director.enabled = false;
            director.enabled = true;
            Assert.That(interactable.enabled, Is.True);
            completed.Invoke();
            Assert.That(existingCompletions, Is.EqualTo(1));
            Assert.That(Phase, Is.EqualTo(StoryPhase.Intro));
        }

        [UnityTest]
        public IEnumerator UnlockBeforeFirstStartIsNotOverwritten()
        {
            CreateFixture("Story");
            Call(button, "UnlockButton");
            Assert.That(interactable.enabled, Is.True);
            yield return null;
            Assert.That(interactable.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator RepeatedPressDuringAnimationEmitsOnlyOnePressEvent()
        {
            CreateFixture("Event");
            yield return null;
            var presses = 0;
            ((UnityEvent)button.GetType().GetField("onPressed").GetValue(button))
                .AddListener(() => presses++);
            Call(button, "UnlockButton");
            Call(button, "PressButton");
            Call(button, "PressButton");
            Assert.That(presses, Is.EqualTo(1));
            Assert.That(interactable.enabled, Is.False);
        }
    }
}
