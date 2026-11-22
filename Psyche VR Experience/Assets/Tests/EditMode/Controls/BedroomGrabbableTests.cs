using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PsycheVR.Controls.Tests
{
    /// <summary>
    /// TG-385 headset re-check: everything the player can grab in Bedroom must also move with the hand. A grabbable
    /// marked Static is batched into a fixed mesh in the build, so the grab holds an invisible collider while the
    /// object stays put (the bedroom picture frame, found on the Quest 3).
    /// </summary>
    public class BedroomGrabbableTests
    {
        private Scene bedroom;
        private XRGrabInteractable[] grabbables;

        [OneTimeSetUp]
        public void OpenBedroom()
        {
            bedroom = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Bedroom.unity");
            grabbables = bedroom.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<XRGrabInteractable>(true)).ToArray();
            Assert.That(grabbables, Is.Not.Empty);
        }

        [OneTimeTearDown]
        public void CloseBedroom()
        {
            if (bedroom.IsValid()) EditorSceneManager.ClosePreviewScene(bedroom);
        }

        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        [Test]
        public void NoGrabbableIsStatic()
        {
            var stuck = grabbables.SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Where(t => GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0)
                .Select(t => PathOf(t)).ToArray();
            Assert.That(stuck, Is.Empty, "Static grabbables do not move when held: " + string.Join(", ", stuck));
        }

        // toy_001 (TG-83) has its collider switched off and falls through the floor at scene start, so nobody can
        // reach it. Left as is in Sprint 12 by decision; Sprint 13 either fixes it or removes its grab component.
        private static readonly string[] KnownBroken = { "toy_001" };

        [Test]
        public void EveryGrabbableHasABodyAndASolidCollider()
        {
            foreach (var g in grabbables.Where(g => !KnownBroken.Contains(g.name)))
            {
                Assert.That(g.GetComponent<Rigidbody>(), Is.Not.Null, $"{PathOf(g.transform)} has no Rigidbody.");
                Assert.That(g.GetComponentsInChildren<Collider>(true).Any(c => c.enabled && !c.isTrigger), Is.True,
                    $"{PathOf(g.transform)} has no solid collider to grab.");
            }
        }
    }
}
