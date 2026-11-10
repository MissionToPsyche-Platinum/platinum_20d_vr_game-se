using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Controls.Tests
{
    /// <summary>
    /// TG-385: the sponsor-approved controls. Grip and trigger both grab (Meta Quest standard buttons; the game has
    /// no separate use action, so a trigger that only activated would do nothing), and the trigger presses floating
    /// menus. These tests fail if a binding edit splits grab onto one button again.
    /// </summary>
    public class ControlBindingTests
    {
        private const string ActionsPath =
            "Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/XRI Default Input Actions.inputactions";
        private const string RigPath = "Assets/Prefabs/VR/XR Origin (XR Rig).prefab";

        private static InputActionAsset Actions()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            Assert.That(asset, Is.Not.Null, $"Input actions missing at {ActionsPath}.");
            return asset;
        }

        private static string[] Paths(string hand, string action)
        {
            var found = Actions().FindAction($"XRI {hand} Interaction/{action}");
            Assert.That(found, Is.Not.Null, $"XRI {hand} Interaction/{action} is missing.");
            return found.bindings.Select(b => b.path).ToArray();
        }

        [TestCase("Left")]
        [TestCase("Right")]
        public void GripAndTriggerBothGrab(string hand)
        {
            var select = Paths(hand, "Select");
            Assert.That(select, Does.Contain($"<XRController>{{{hand}Hand}}/{{GripButton}}"), $"{hand} grip must grab.");
            Assert.That(select, Does.Contain($"<XRController>{{{hand}Hand}}/{{TriggerButton}}"), $"{hand} trigger must grab.");
        }

        [TestCase("Left")]
        [TestCase("Right")]
        public void TriggerPressesMenus(string hand)
        {
            Assert.That(Paths(hand, "UI Press"), Does.Contain($"<XRController>{{{hand}Hand}}/{{TriggerButton}}"));
        }

        [Test]
        public void HandsGrabWithTheSelectAction()
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            Assert.That(rig, Is.Not.Null, $"Rig missing at {RigPath}.");
            var hands = rig.GetComponentsInChildren<NearFarInteractor>(true);
            Assert.That(hands, Has.Length.EqualTo(2), "One near-far interactor per hand.");
            foreach (var interactor in hands)
            {
                string hand = interactor.handedness == InteractorHandedness.Left ? "Left" : "Right";
                var action = interactor.selectInput.inputActionReferencePerformed;
                Assert.That(action, Is.Not.Null, $"{interactor.name} ({hand}) has no select action.");
                Assert.That(action.action.actionMap.name + "/" + action.action.name, Is.EqualTo($"XRI {hand} Interaction/Select"),
                    $"{hand} hand must grab with the Select action that grip and trigger are bound to.");
            }
        }
    }
}
