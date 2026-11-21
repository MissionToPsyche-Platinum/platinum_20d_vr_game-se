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
        private Material memoryMaterial;
        private float previousTimeScale;
        private PropertyInfo activeMode;
        private object previousMode;
        private readonly List<string> phaseLogs = new List<string>();

        public override void Setup()
        {
            base.Setup();
            previousTimeScale = Time.timeScale;
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
            if (memoryMaterial != null) UnityEngine.Object.DestroyImmediate(memoryMaterial);
            Time.timeScale = previousTimeScale;
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
            // Own child objects, as in Bedroom: both switch their object off when hidden.
            card = Child("StoryDateCard");
            highlight = Child("ObjectiveHighlight");
            Assign("dateCard", card);
            Assign("objectiveHighlight", highlight);
            Assign("introSpacecraft", owner.transform);
            owner.SetActive(true);
        }

        private Component Child(string typeName)
        {
            var child = new GameObject(typeName);
            child.transform.SetParent(owner.transform);
            return child.AddComponent(RuntimeType("PsycheVR.Story." + typeName));
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
            Assert.That(Read(highlight, "IsVisible"), Is.True);

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
            Assert.That(Read(highlight, "IsVisible"), Is.False);
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
            Assert.That(card.gameObject.activeSelf || highlight.gameObject.activeSelf, Is.False);
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

    public class BlinkTeleporterPlayModeTests
    {
        private GameObject rig;
        private GameObject destination;
        private Material material;
        private float previousTimeScale;

        [SetUp]
        public void RememberTimeScale() => previousTimeScale = Time.timeScale;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (rig != null) UnityEngine.Object.Destroy(rig);
            if (destination != null) UnityEngine.Object.Destroy(destination);
            if (material != null) UnityEngine.Object.Destroy(material);
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        private static Type RuntimeType(string name) => Type.GetType("PsycheVR.VR." + name + ", Assembly-CSharp", true);

        [UnityTest]
        public IEnumerator CustomTripUsesUnscaledDurationsAndPreservesNormalBlink()
        {
            rig = new GameObject("Blink timing test");
            rig.SetActive(false);
            var overlayObject = new GameObject("Overlay");
            overlayObject.transform.SetParent(rig.transform);
            material = new Material(Shader.Find("Sprites/Default"));
            overlayObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            var overlay = overlayObject.AddComponent(RuntimeType("BlinkFadeOverlay"));
            var teleporterType = RuntimeType("BlinkTeleporter");
            var teleporter = rig.AddComponent(teleporterType);
            teleporterType.GetField("fadeOverlay", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(teleporter, overlay);
            destination = new GameObject("Destination");
            destination.transform.SetPositionAndRotation(new Vector3(5, 2, 3), Quaternion.Euler(0, 90, 0));
            var anchorType = RuntimeType("BlinkTeleportAnchor");
            var anchor = destination.AddComponent(anchorType);
            rig.SetActive(true);
            yield return null;
            Time.timeScale = 0f;

            var custom = teleporterType.GetMethod("TeleportTo", new[] { anchorType, typeof(float), typeof(float), typeof(float) });
            float start = Time.realtimeSinceStartup;
            custom.Invoke(teleporter, new object[] { anchor, 0.5f, 0.3f, 0.5f });
            Assert.That(rig.transform.position, Is.EqualTo(Vector3.zero));
            var routine = teleporterType.GetField("_activeRoutine", BindingFlags.Instance | BindingFlags.NonPublic);
            while (routine.GetValue(teleporter) != null && Time.realtimeSinceStartup - start < 5f)
                yield return null;
            Assert.That(routine.GetValue(teleporter), Is.Null, "Teleport should finish while timeScale is zero.");
            Assert.That(Time.realtimeSinceStartup - start, Is.GreaterThanOrEqualTo(1.2f));
            Assert.That(rig.transform.position, Is.EqualTo(destination.transform.position));
            Assert.That(Quaternion.Angle(rig.transform.rotation, destination.transform.rotation), Is.LessThan(0.01f));
            Assert.That((float)overlay.GetType().GetProperty("CurrentAlpha").GetValue(overlay), Is.Zero);

            foreach (var timing in new[] { ("fadeOutDuration", 0.35f), ("holdDuration", 0.05f), ("fadeInDuration", 0.35f) })
                Assert.That((float)teleporterType.GetField(timing.Item1, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(teleporter), Is.EqualTo(timing.Item2));

            destination.transform.position = Vector3.one;
            start = Time.realtimeSinceStartup;
            teleporterType.GetMethod("TeleportTo", new[] { anchorType }).Invoke(teleporter, new object[] { anchor });
            while (routine.GetValue(teleporter) != null && Time.realtimeSinceStartup - start < 5f)
                yield return null;
            Assert.That(routine.GetValue(teleporter), Is.Null);
            Assert.That(Time.realtimeSinceStartup - start, Is.GreaterThanOrEqualTo(0.65f));
            Assert.That(rig.transform.position, Is.EqualTo(Vector3.one));
        }
    }

}
