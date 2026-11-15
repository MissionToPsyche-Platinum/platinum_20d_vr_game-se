using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PsycheVR.Story.Tests
{
    /// <summary>
    /// TG-397: inspect the saved Bedroom scene, including prefab overrides, rather
    /// than reconstructing its Inspector wiring in a runtime fixture.
    /// Preview scenes keep the user's open scenes and unsaved edits untouched.
    /// </summary>
    public class BedroomStoryIntegrationTests
    {
        private Scene bedroom;
        private Component director;

        [OneTimeSetUp]
        public void OpenBedroom()
        {
            bedroom = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Bedroom.unity");
            var directors = bedroom.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren(RuntimeType("PsycheVR.Story.StoryDirector"), true))
                .ToArray();
            Assert.That(directors, Has.Length.EqualTo(1), "Bedroom must have exactly one story director.");
            director = directors[0];
        }

        [OneTimeTearDown]
        public void CloseBedroom()
        {
            if (bedroom.IsValid()) EditorSceneManager.ClosePreviewScene(bedroom);
        }

        private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);

        private static SerializedProperty Field(SerializedObject owner, string name)
        {
            var field = owner.FindProperty(name);
            Assert.That(field, Is.Not.Null, $"{owner.targetObject.name}: serialized field '{name}' is missing.");
            return field;
        }

        private static T Reference<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object
        {
            using var serialized = new SerializedObject(owner);
            var reference = Field(serialized, name).objectReferenceValue as T;
            Assert.That(reference, Is.Not.Null, $"{owner.name}.{name} must reference a {typeof(T).Name}.");
            return reference;
        }

        private static float Number(UnityEngine.Object owner, string name)
        {
            using var serialized = new SerializedObject(owner);
            return Field(serialized, name).floatValue;
        }

        private Component SceneComponent(string typeName) => bedroom.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren(RuntimeType(typeName), true)).Single();

        [Test]
        public void IntroTargetIsAnActiveGrabbableWithPhysicsAndStoryOnlyVisibility()
        {
            Assert.That(((Behaviour)director).isActiveAndEnabled, Is.True);
            var target = Reference<Transform>(director, "introSpacecraft");
            Assert.That(target.gameObject.scene, Is.EqualTo(bedroom));
            Assert.That(target.gameObject.activeInHierarchy, Is.True);
            var grab = target.GetComponent(RuntimeType("PsycheVR.Gameplay.PsycheGrabbable"));
            Assert.That(grab, Is.Not.Null, "The highlighted target itself must receive selectEntered.");
            Assert.That(((Behaviour)grab).enabled, Is.True);
            Reference<ScriptableObject>(grab, "grabSettings");
            Assert.That(target.GetComponent<Rigidbody>(), Is.Not.Null);
            using var serializedGrab = new SerializedObject(grab);
            var colliders = Field(serializedGrab, "m_Colliders");
            Assert.That(colliders.arraySize, Is.GreaterThan(0), "XRI needs explicit grab colliders.");
            for (int i = 0; i < colliders.arraySize; i++)
            {
                var collider = colliders.GetArrayElementAtIndex(i).objectReferenceValue as Collider;
                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.enabled && collider.gameObject.activeInHierarchy, Is.True);
                Assert.That(collider.attachedRigidbody, Is.EqualTo(target.GetComponent<Rigidbody>()));
            }
            var gate = target.GetComponent(RuntimeType("PsycheVR.Modes.ModeGate"));
            Assert.That(gate, Is.Not.Null, "The story display must be hidden in Event mode.");
            using var serializedGate = new SerializedObject(gate);
            var modes = Field(serializedGate, "activeIn");
            Assert.That(modes.arraySize, Is.EqualTo(1));
            var mode = modes.GetArrayElementAtIndex(0);
            Assert.That(mode.enumNames[mode.enumValueIndex], Is.EqualTo("Story"));
        }

        [Test]
        public void MemoryUsesTheLiveRigFadeAndBothHandInteractors()
        {
            var teleporter = Reference<Component>(director, "teleporter");
            Assert.That(teleporter, Is.SameAs(SceneComponent("PsycheVR.VR.BlinkTeleporter")));
            Assert.That(((Behaviour)teleporter).isActiveAndEnabled, Is.True);
            var rig = Reference<Transform>(teleporter, "rigRoot");
            var overlay = Reference<Component>(teleporter, "fadeOverlay");
            Assert.That(overlay.transform.IsChildOf(rig), Is.True);
            Assert.That(overlay.GetComponentInParent<Camera>(), Is.Not.Null, "Fade must follow the HMD camera.");
            var left = Reference<Component>(teleporter, "leftHandInteractor");
            var right = Reference<Component>(teleporter, "rightHandInteractor");
            Assert.That(left, Is.Not.SameAs(right));
            Assert.That(left.transform.IsChildOf(rig) && right.transform.IsChildOf(rig), Is.True);
            var route = SceneComponent("PsycheVR.VR.BlinkTeleportRoute");
            Assert.That(Reference<Component>(route, "teleporter"), Is.SameAs(teleporter));
        }

        [Test]
        public void MemoryArrivalIsASeparateBedroomAnchorAwayFromMissionControl()
        {
            var arrival = Reference<Component>(director, "bedroomArrival");
            Assert.That(arrival.GetType(), Is.EqualTo(RuntimeType("PsycheVR.VR.BlinkTeleportAnchor")));
            Assert.That(arrival.name, Is.EqualTo("StoryBedroomArrival"));
            Assert.That(arrival.gameObject.scene, Is.EqualTo(bedroom));
            Assert.That(arrival.gameObject.activeInHierarchy, Is.True);
            var route = SceneComponent("PsycheVR.VR.BlinkTeleportRoute");
            var normalBedroom = Reference<Component>(route, "bedroomAnchor");
            var missionControl = Reference<Component>(route, "missionControlAnchor");
            Assert.That(arrival, Is.Not.SameAs(normalBedroom), "Story tuning must not move the ordinary blink anchor.");
            // Allow modest headset placement tuning while catching a wrong-room assignment.
            Assert.That(Vector3.Distance(arrival.transform.position, normalBedroom.transform.position), Is.LessThan(1f));
            Assert.That(Vector3.Distance(arrival.transform.position, missionControl.transform.position), Is.GreaterThan(5f));
            float yaw = (float)arrival.GetType().GetProperty("Yaw").GetValue(arrival);
            float bedroomYaw = (float)normalBedroom.GetType().GetProperty("Yaw").GetValue(normalBedroom);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, bedroomYaw)), Is.LessThan(45f));
        }

        [Test]
        public void MemoryIsSlowerWhileSceneOverridesPreserveOrdinaryBlinkTimings()
        {
            var teleporter = Reference<Component>(director, "teleporter");
            Assert.That(Number(teleporter, "fadeOutDuration"), Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(Number(teleporter, "holdDuration"), Is.EqualTo(0.05f).Within(0.001f));
            Assert.That(Number(teleporter, "fadeInDuration"), Is.EqualTo(0.35f).Within(0.001f));
            foreach (var stage in new[] { ("memoryFadeOutDuration", "fadeOutDuration"),
                         ("memoryHoldDuration", "holdDuration"), ("memoryFadeInDuration", "fadeInDuration") })
            {
                float duration = Number(director, stage.Item1);
                Assert.That(float.IsNaN(duration) || float.IsInfinity(duration), Is.False);
                Assert.That(duration, Is.GreaterThan(Number(teleporter, stage.Item2)), stage.Item1);
            }
        }

        [Test]
        public void ArrivalDateHasTextPresentationAndBedroomPlacement()
        {
            var text = Reference<ScriptableObject>(director, "storyText");
            Assert.That(text.GetType().GetProperty("MemoryDateText").GetValue(text), Is.EqualTo("2003"));
            var card = Reference<Component>(director, "dateCard");
            Assert.That(card.GetType(), Is.EqualTo(RuntimeType("PsycheVR.Story.StoryDateCard")));
            Assert.That(card.gameObject.scene, Is.EqualTo(bedroom));
            Reference<Component>(director, "objectiveHighlight");
            float duration = Number(director, "memoryDateDuration");
            Assert.That(float.IsNaN(duration) || float.IsInfinity(duration), Is.False);
            Assert.That(duration, Is.GreaterThan(0f), "Arrival text must remain visible after entering Build.");
            using var serialized = new SerializedObject(director);
            var placement = Field(serialized, "memoryDateAnchor").objectReferenceValue as Transform;
            if (placement != null)
            {
                var arrival = Reference<Component>(director, "bedroomArrival");
                Assert.That(Vector3.Distance(placement.position, arrival.transform.position), Is.LessThan(5f),
                    "Arrival card must not use the Intro anchor back at mission control.");
            }
            else
            {
                var cameras = bedroom.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .Where(camera => camera.CompareTag("MainCamera") && camera.isActiveAndEnabled).ToArray();
                Assert.That(cameras, Has.Length.EqualTo(1), "Camera-relative arrival text needs one active MainCamera.");
            }
        }
    }
}
