using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PsycheVR.UI
{
    /// <summary>
    /// Detects the staff-only combo that unlocks the admin section of the pause menu:
    /// both grips and both thumbstick clicks held together for <see cref="HoldSeconds"/>.
    /// Nobody clicks both sticks while gripping by accident, and the hold length absorbs a
    /// brief fumble. The caller ticks this with unscaled time (the menu pauses the game)
    /// and reads <see cref="IndicatorVisible"/> / <see cref="Progress"/> to draw feedback,
    /// which only starts after <see cref="IndicatorDelaySeconds"/> so a 200 ms brush shows
    /// nothing.
    ///
    /// In the editor, holding F8 stands in for the combo: the XR Device Simulator drives
    /// one controller at a time, so the real combo cannot be performed there.
    /// </summary>
    public sealed class AdminCombo : IDisposable
    {
        private const string LeftHand = "<XRController>{LeftHand}";
        private const string RightHand = "<XRController>{RightHand}";
        private const string GripControl = "/gripPressed";
        private const string ThumbstickClickControl = "/{Primary2DAxisClick}";
        private const string EditorFallbackControl = "<Keyboard>/f8";

        private readonly InputAction[] _comboActions;
        private readonly InputAction _editorFallback;
        private readonly Func<bool> _isHeldOverride;

        private float _heldSeconds;
        private bool _completed;

        /// <summary>Seconds the combo must be held before <see cref="Completed"/> fires.</summary>
        public float HoldSeconds { get; }

        /// <summary>Seconds of holding before the progress indicator should appear.</summary>
        public float IndicatorDelaySeconds { get; }

        /// <summary>Raised once per hold when the combo has been held for the full duration.</summary>
        public event Action Completed;

        /// <summary>True while the indicator should be drawn: past the delay, not yet completed.</summary>
        public bool IndicatorVisible => !_completed && _heldSeconds >= IndicatorDelaySeconds;

        /// <summary>0 when the indicator first appears, 1 at completion.</summary>
        public float Progress
        {
            get
            {
                float span = HoldSeconds - IndicatorDelaySeconds;
                if (span <= 0f)
                    return _heldSeconds >= HoldSeconds ? 1f : 0f;
                return Mathf.Clamp01((_heldSeconds - IndicatorDelaySeconds) / span);
            }
        }

        /// <summary>Creates the combo on the real controller bindings (plus F8 in the editor).</summary>
        public AdminCombo(float holdSeconds, float indicatorDelaySeconds)
            : this(holdSeconds, indicatorDelaySeconds, null)
        {
        }

        /// <summary>
        /// Creates the combo with <paramref name="isHeldOverride"/> replacing the input
        /// bindings. Used to drive the timing logic without devices.
        /// </summary>
        public AdminCombo(float holdSeconds, float indicatorDelaySeconds, Func<bool> isHeldOverride)
        {
            HoldSeconds = Mathf.Max(0f, holdSeconds);
            IndicatorDelaySeconds = Mathf.Clamp(indicatorDelaySeconds, 0f, HoldSeconds);
            _isHeldOverride = isHeldOverride;

            if (isHeldOverride != null)
            {
                _comboActions = Array.Empty<InputAction>();
                return;
            }

            _comboActions = new[]
            {
                ButtonAction("Admin Left Grip", LeftHand + GripControl),
                ButtonAction("Admin Right Grip", RightHand + GripControl),
                ButtonAction("Admin Left Stick Click", LeftHand + ThumbstickClickControl),
                ButtonAction("Admin Right Stick Click", RightHand + ThumbstickClickControl)
            };

            // Editor only: the simulator drives one controller at a time.
            _editorFallback = Application.isEditor ? ButtonAction("Admin Editor Fallback", EditorFallbackControl) : null;
        }

        /// <summary>True while every control of the combo is pressed.</summary>
        public bool IsHeld
        {
            get
            {
                if (_isHeldOverride != null)
                    return _isHeldOverride();

                if (_editorFallback != null && _editorFallback.IsPressed())
                    return true;

                foreach (var action in _comboActions)
                {
                    if (!action.IsPressed())
                        return false;
                }
                return true;
            }
        }

        public void Enable()
        {
            foreach (var action in _comboActions)
                action.Enable();
            _editorFallback?.Enable();
        }

        public void Disable()
        {
            foreach (var action in _comboActions)
                action.Disable();
            _editorFallback?.Disable();
            Reset();
        }

        /// <summary>Forgets any partial hold. Call when the menu closes.</summary>
        public void Reset()
        {
            _heldSeconds = 0f;
            _completed = false;
        }

        /// <summary>
        /// Advances the hold timer. Pass unscaled time: the pause menu sets the time scale
        /// to zero. Releasing any control resets the hold and lets it fire again later.
        /// </summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (!IsHeld)
            {
                Reset();
                return;
            }

            if (_completed)
                return;

            _heldSeconds += Mathf.Max(0f, unscaledDeltaTime);
            if (_heldSeconds < HoldSeconds)
                return;

            _completed = true;
            Completed?.Invoke();
        }

        public void Dispose()
        {
            foreach (var action in _comboActions)
                action.Dispose();
            _editorFallback?.Dispose();
        }

        private static InputAction ButtonAction(string name, string bindingPath)
        {
            var action = new InputAction(name, InputActionType.Button);
            action.AddBinding(bindingPath);
            return action;
        }
    }
}
