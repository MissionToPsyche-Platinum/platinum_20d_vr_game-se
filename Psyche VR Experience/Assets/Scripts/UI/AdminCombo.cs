using System;

namespace PsycheVR.UI
{
    /// <summary>
    /// The staff-only combo that opens the pause menu with its admin section showing: both grips
    /// and both thumbstick clicks held together. F8 stands in for it in the editor. Since TG-269 the
    /// pause menu builds it only in the editor (a Quest kiosk has no use for the admin menu). A preset of
    /// <see cref="ControllerHoldCombo"/>; the pause menu ticks it with unscaled time because the menu
    /// pauses the game.
    /// </summary>
    public sealed class AdminCombo : ControllerHoldCombo
    {
        private const string GripControl = "/gripPressed";
        private const string ThumbstickClickControl = "/{Primary2DAxisClick}";
        private const string EditorFallbackControl = "<Keyboard>/f8";

        private static readonly string[] Controls =
        {
            LeftHand + GripControl,
            RightHand + GripControl,
            LeftHand + ThumbstickClickControl,
            RightHand + ThumbstickClickControl
        };

        /// <summary>Creates the combo on the real controller bindings (plus F8 in the editor).</summary>
        public AdminCombo(float holdSeconds, float indicatorDelaySeconds)
            : base("Admin", Controls, EditorFallbackControl, holdSeconds, indicatorDelaySeconds)
        {
        }

        /// <summary>Creates the combo with <paramref name="isHeldOverride"/> replacing the bindings.</summary>
        public AdminCombo(float holdSeconds, float indicatorDelaySeconds, Func<bool> isHeldOverride)
            : base(holdSeconds, indicatorDelaySeconds, isHeldOverride)
        {
        }
    }
}
