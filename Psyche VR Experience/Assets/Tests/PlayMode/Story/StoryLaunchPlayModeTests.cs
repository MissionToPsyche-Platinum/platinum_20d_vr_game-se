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
        private Component childButton;
        private Behaviour childInteractable;
        private Component card;
        private Transform rig;
        private Transform returnAnchor;
        private Component[] zones;
        private Material fadeMaterial;

        private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static void Assign(Component target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Call(Component target, string name, params object[] args) => target.GetType()
            .GetMethod(name).Invoke(target, args);
        private static bool IsUnlocked(Component target) => (bool)target.GetType().GetProperty("IsUnlocked").GetValue(target);
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
            if (fadeMaterial != null) UnityEngine.Object.DestroyImmediate(fadeMaterial);
            activeMode.SetValue(null, previousMode);
        }

        private void CreateFixture(string mode, bool useSlapKey = false)
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
            if (useSlapKey)
                Assign(button, "slapKey", top.AddComponent(RuntimeType("PsycheVR.Gameplay.SlapKey")));

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
            Assert.That(IsUnlocked(button), Is.False);
            Call(director, "GoTo", StoryPhase.RealLaunch);
            Assert.That(IsUnlocked(button), Is.True);

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
                Assert.That(IsUnlocked(button), Is.False);
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
            Assert.That(IsUnlocked(button), Is.False);
            Call(button, "UnlockButton");
            director.enabled = false;
            director.enabled = true;
            Assert.That(IsUnlocked(button), Is.True);
            completed.Invoke();
            Assert.That(existingCompletions, Is.EqualTo(1));
            Assert.That(Phase, Is.EqualTo(StoryPhase.Intro));
        }

        [UnityTest]
        public IEnumerator UnlockBeforeFirstStartIsNotOverwritten()
        {
            CreateFixture("Event");
            Call(button, "UnlockButton");
            Assert.That(IsUnlocked(button), Is.True);
            yield return null;
            Assert.That(IsUnlocked(button), Is.True);
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
            Assert.That(IsUnlocked(button), Is.False);
        }

        [UnityTest]
        public IEnumerator SlapKeyRespectsStoryGateAndEventUnlockWithoutEnablingRaySelection()
        {
            CreateFixture("Event", useSlapKey: true);
            yield return null;
            var key = (Component)button.GetType().GetField("slapKey",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(button);
            var presses = 0;
            ((UnityEvent)button.GetType().GetField("onPressed").GetValue(button))
                .AddListener(() => presses++);
            Assert.That(key.GetType().GetProperty("Blocked").GetValue(key), Is.True);
            Call(button, "UnlockButton");
            Assert.That(IsUnlocked(button), Is.True);
            Assert.That(interactable.enabled, Is.False, "Develop keeps launch buttons slap-only.");
            Assert.That(key.GetType().GetProperty("Blocked").GetValue(key), Is.False);
            Call(button, "SetStoryInteractionAllowed", false);
            Call(button, "UnlockButton");
            Call(key, "Press");
            Assert.That(presses, Is.Zero);
            Assert.That(key.GetType().GetProperty("Blocked").GetValue(key), Is.True);
            Call(button, "SetStoryInteractionAllowed", true);
            Call(button, "UnlockButton");
            Call(key, "Press");
            Call(key, "Press");
            Assert.That(presses, Is.EqualTo(1));
            Assert.That(IsUnlocked(button), Is.False);
            Assert.That(key.GetType().GetProperty("Blocked").GetValue(key), Is.True);
            Assert.That(interactable.enabled, Is.False);
        }

        private void CreateReturnFixture()
        {
            CreateFixture("Story");
            owner.SetActive(false);
            var child = UnityEngine.Object.Instantiate(button.gameObject, owner.transform);
            child.name = "Bedroom launch button";
            childButton = child.GetComponent(RuntimeType("ButtonController"));
            childInteractable = (Behaviour)child.GetComponent(interactable.GetType());
            Assign(childButton, "startVideoOnPress", false);
            Assign(director, "childLaunchButton", childButton);
            var cardObject = new GameObject("Date card");
            cardObject.transform.SetParent(owner.transform);
            card = cardObject.AddComponent(RuntimeType("PsycheVR.Story.StoryDateCard"));
            Assign(director, "dateCard", card);

            var rigObject = new GameObject("Rig");
            rig = rigObject.transform;
            rig.SetParent(owner.transform);
            var teleporter = rigObject.AddComponent(RuntimeType("PsycheVR.VR.BlinkTeleporter"));
            Assign(teleporter, "rigRoot", rig);
            Assign(teleporter, "holdDuration", 0f);
            var overlayObject = new GameObject("Fade overlay");
            overlayObject.transform.SetParent(rig);
            fadeMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            overlayObject.AddComponent<MeshRenderer>().sharedMaterial = fadeMaterial;
            var overlay = overlayObject.AddComponent(RuntimeType("PsycheVR.VR.BlinkFadeOverlay"));
            Assign(teleporter, "fadeOverlay", overlay);
            Assign(director, "teleporter", teleporter);
            Assign(director, "returnFadeSeconds", 0.05f);
            var destination = new GameObject("StoryDeskReturn");
            returnAnchor = destination.transform;
            returnAnchor.SetParent(owner.transform);
            returnAnchor.SetPositionAndRotation(new Vector3(10, 0, 4), Quaternion.Euler(0, 90, 0));
            Assign(director, "storyDeskReturn", destination.AddComponent(RuntimeType("PsycheVR.VR.BlinkTeleportAnchor")));

            var puzzle = new GameObject("Bedroom puzzle");
            puzzle.transform.SetParent(owner.transform);
            Assign(director, "bedroomPuzzle", puzzle.transform);
            zones = new Component[2];
            for (int i = 0; i < zones.Length; i++)
            {
                var zone = new GameObject("Required zone " + i);
                zone.transform.SetParent(puzzle.transform);
                zones[i] = zone.AddComponent(RuntimeType("SnapZone"));
                zones[i].GetType().GetField("requiredTag").SetValue(zones[i], "Untagged");
            }
            owner.SetActive(true);
        }

        private static void CompleteZone(Component zone)
        {
            zone.GetType().GetField("hasSnapped").SetValue(zone, true);
            var snapped = (Delegate)zone.GetType().GetField("Snapped", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            snapped?.DynamicInvoke(zone);
        }

        [UnityTest]
        public IEnumerator PuzzleAndChildPressReturnRigThenUnlockRealLaunchWithoutF8()
        {
            CreateReturnFixture();
            yield return null;
            Call(director, "GoTo", StoryPhase.Build); // Earlier phases are a separate integration.
            yield return null; // Let PuzzleCompletion discover the scoped bedroom zones.
            CompleteZone(zones[0]);
            Assert.That(Phase, Is.EqualTo(StoryPhase.Build));
            Assert.That(IsUnlocked(childButton), Is.False);
            CompleteZone(zones[1]);
            Assert.That(Phase, Is.EqualTo(StoryPhase.ChildLaunch));
            Assert.That(IsUnlocked(childButton), Is.True);
            Assert.That(IsUnlocked(button), Is.False);

            // A VideoPlayer makes an accidental child-button video start observable.
            var player = owner.AddComponent<UnityEngine.Video.VideoPlayer>();
            player.playOnAwake = false;
            var video = owner.GetComponent(RuntimeType("play_video"));
            int videoStarts = 0;
            ((UnityEvent)video.GetType().GetField("onVideoStarted", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(video)).AddListener(() => videoStarts++);
            Call(childButton, "PressButton");
            Assert.That(Phase, Is.EqualTo(StoryPhase.RealLaunch));
            Assert.That(IsUnlocked(button), Is.False, "Real launch must stay locked during the fade.");
            completed.Invoke();
            Assert.That(Phase, Is.EqualTo(StoryPhase.RealLaunch), "Stale video completion must not skip the return.");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(Vector3.Distance(rig.position, returnAnchor.position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(rig.rotation, returnAnchor.rotation), Is.LessThan(0.001f));
            Assert.That(card.GetType().GetProperty("CurrentText").GetValue(card), Is.EqualTo("October 2023"));
            Assert.That(card.GetType().GetProperty("IsVisible").GetValue(card), Is.True);
            Assert.That(videoStarts, Is.Zero);
            Assert.That(IsUnlocked(button), Is.True);
            Assert.That(IsUnlocked(childButton), Is.False);
            completed.Invoke();
            Assert.That(Phase, Is.EqualTo(StoryPhase.FreePlay));
            Assert.That(IsUnlocked(button), Is.False);
        }

        [UnityTest]
        public IEnumerator CaseUnlockAndDirectPressCannotBypassStoryPhaseGate()
        {
            CreateReturnFixture();
            yield return null;
            var presses = 0;
            ((UnityEvent)childButton.GetType().GetField("onPressed").GetValue(childButton)).AddListener(() => presses++);
            Call(childButton, "UnlockButton");
            Call(childButton, "PressButton");
            Call(button, "UnlockButton");
            Call(button, "PressButton");
            Assert.That(presses, Is.Zero);
            Assert.That(IsUnlocked(childButton), Is.False);
            Assert.That(IsUnlocked(button), Is.False);
            Assert.That(Phase, Is.EqualTo(StoryPhase.Intro));
        }

        [UnityTest]
        public IEnumerator UnrelatedPuzzleAndDisabledDirectorCannotAdvanceBuild()
        {
            CreateReturnFixture();
            yield return null;
            var other = new GameObject("Other puzzle");
            other.transform.SetParent(owner.transform);
            var otherZone = other.AddComponent(RuntimeType("SnapZone"));
            otherZone.GetType().GetField("requiredTag").SetValue(otherZone, "Untagged");
            Call(director, "GoTo", StoryPhase.Build);
            yield return null;
            CompleteZone(otherZone);
            Assert.That(Phase, Is.EqualTo(StoryPhase.Build));
            director.enabled = false;
            CompleteZone(zones[0]);
            CompleteZone(zones[1]);
            Assert.That(Phase, Is.EqualTo(StoryPhase.Build));
            director.enabled = true;
            Assert.That(Phase, Is.EqualTo(StoryPhase.ChildLaunch), "Catch up if the puzzle completed while disabled.");
        }
    }
}
