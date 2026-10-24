using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// The grip and trigger axes of the controller in the hand that holds something, for desk toys that
    /// act on "the other control" (PenClicker, PaperCrumple). Read from the controller itself: the rig's
    /// select value is wired to the Select button, which is bound to both grip and trigger, so it reads
    /// "grip held" whenever the trigger holds the toy. Falls back to the interactor's readers when no
    /// controller is found (left/right not set).
    /// </summary>
    public static class HeldControllerAxes
    {
        /// <summary>Grip axis, 0 to 1, of the controller behind <paramref name="holder"/>.</summary>
        public static float Grip(XRBaseInputInteractor holder) => Axis(holder, "grip", holder.selectInput.ReadValue());

        /// <summary>Trigger axis, 0 to 1, of the controller behind <paramref name="holder"/>.</summary>
        public static float Trigger(XRBaseInputInteractor holder) => Axis(holder, "trigger", holder.activateInput.ReadValue());

        private static float Axis(XRBaseInputInteractor holder, string control, float fallback)
        {
            UnityEngine.InputSystem.XR.XRController device =
                holder.handedness == InteractorHandedness.Left ? UnityEngine.InputSystem.XR.XRController.leftHand
                : holder.handedness == InteractorHandedness.Right ? UnityEngine.InputSystem.XR.XRController.rightHand : null;
            var axis = device != null ? device.TryGetChildControl<AxisControl>(control) : null;
            return axis != null ? axis.ReadValue() : fallback;
        }
    }
}
