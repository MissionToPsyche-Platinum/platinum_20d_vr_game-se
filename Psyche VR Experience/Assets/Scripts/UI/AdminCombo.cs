using System;

namespace PsycheVR.UI
{
    /// <summary>
    /// The staff-only combo that unlocks the admin section of the pause menu: both grips
    /// and both thumbstick clicks held together. Nobody clicks both sticks while gripping
    /// by accident, and the hold length absorbs a brief fumble. F8 stands in for it in
    /// the editor. A preset of <see cref="ControllerHoldCombo"/>; the pause menu ticks it
    /// with unscaled time because the menu pauses the game.
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
