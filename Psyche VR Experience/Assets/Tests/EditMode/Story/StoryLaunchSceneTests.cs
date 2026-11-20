using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace PsycheVR.Story.Tests
{
    public class StoryLaunchSceneTests
    {
        private static object Field(Component component, string name) => component.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component);

        [Test]
        public void BedroomHasDistinctLaunchButtonsAndExplicitReturnAndVideoReferences()
        {
            const string path = "Assets/Scenes/Bedroom.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                    .Where(component => component != null).ToArray();
                var director = components.Single(component => component.GetType().FullName == "PsycheVR.Story.StoryDirector");
                var child = (Component)Field(director, "childLaunchButton");
                var real = (Component)Field(director, "realLaunchButton");
                var video = (Component)Field(director, "launchVideo");
                var anchor = (Component)Field(director, "storyDeskReturn");
                Assert.That(child, Is.Not.Null);
                Assert.That(real, Is.Not.Null);
                Assert.That(child, Is.Not.SameAs(real));
                Assert.That(Field(child, "startVideoOnPress"), Is.False);
                Assert.That(Field(real, "startVideoOnPress"), Is.True);
                Assert.That(video, Is.Not.Null);
                Assert.That(Field(real, "videoToStart"), Is.SameAs(video));
                Assert.That(anchor, Is.Not.Null);
                Assert.That(anchor.name, Is.EqualTo("StoryDeskReturn"));
                Assert.That(Field(director, "teleporter"), Is.Not.Null);
                var puzzle = (Transform)Field(director, "bedroomPuzzle");
                Assert.That(puzzle, Is.Not.Null);
                Assert.That(puzzle.GetComponentsInChildren<MonoBehaviour>(true)
                    .Count(component => component != null && component.GetType().Name == "SnapZone"), Is.GreaterThan(0));
                Assert.That(child.GetComponents<MonoBehaviour>().Any(component =>
                    component != null && component.GetType().FullName == "PsycheVR.Modes.ModeGate"), Is.True);

                foreach (var button in new[] { child, real })
                {
                    var interactable = button.GetComponents<MonoBehaviour>().Single(component =>
                        component != null && component.GetType().Name == "XRSimpleInteractable");
                    var selected = (UnityEventBase)interactable.GetType().GetProperty("selectEntered").GetValue(interactable);
                    Assert.That(selected.GetPersistentEventCount(), Is.EqualTo(1));
                    Assert.That(selected.GetPersistentTarget(0), Is.SameAs(button));
                    Assert.That(selected.GetPersistentMethodName(0), Is.EqualTo("PressButton"));
                }
            }
            finally
            {
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
