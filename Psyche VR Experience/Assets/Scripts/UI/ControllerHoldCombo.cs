using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PsycheVR.UI
{
    /// <summary>
    /// A button combination on the XR controllers that must be held for a set time.
    /// Built from Input System control paths, so each combo in the project is a preset
    /// of this one class: <see cref="AdminCombo"/> (pause-menu admin reveal) and the
    /// kiosk reset and first-input detectors in <c>PsycheVR.Kiosk</c>.
    ///
    /// The caller ticks it every frame with the time base it wants (unscaled for
    /// anything that must work while paused) and reads <see cref="IndicatorVisible"/> /
    /// <see cref="Progress"/> to draw feedback, which only starts after
    /// <see cref="IndicatorDelaySeconds"/> so a brief brush shows nothing.
    ///
    /// In the editor an optional keyboard control stands in for the whole combo: the XR
    /// Device Simulator drives one controller at a time, so a two-hand combo cannot be
    /// performed there.
    ///
    /// Whoever constructs the combo disposes it; after <see cref="Dispose"/> every member
    /// is inert.
    /// </summary>
    public class ControllerHoldCombo : IDisposable
    {
        /// <summary>Control path prefix for the left controller.</summary>
        public const string LeftHand = "<XRController>{LeftHand}";

        /// <summary>Control path prefix for the right controller.</summary>
        public const string RightHand = "<XRController>{RightHand}";

        private readonly InputAction[] _comboActions;
        private readonly InputAction _editorFallback;
        private readonly Func<bool> _isHeldOverride;
        private readonly bool _anyControl;

        private float _heldSeconds;
        private bool _completed;
        private bool _disposed;

        /// <summary>Seconds the combo must be held before <see cref="Completed"/> fires.</summary>
        public float HoldSeconds { get; }

        /// <summary>Seconds of holding before the progress indicator should appear.</summary>
        public float IndicatorDelaySeconds { get; }

        /// <summary>Raised once per hold when the combo has been held for the full duration.</summary>
        public event Action Completed;

        /// <summary>True while the indicator should be drawn: past the delay, not yet completed.</summary>
        public bool IndicatorVisible => !_completed && _heldSeconds > 0f && _heldSeconds >= IndicatorDelaySeconds;

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

        /// <summary>
        /// Creates a combo on real controller bindings.
        /// </summary>
        /// <param name="name">Prefix for the InputAction names, for the input debugger.</param>
        /// <param name="controlPaths">Full control paths, e.g. <c>LeftHand + "/gripPressed"</c>.</param>
        /// <param name="editorFallbackPath">Keyboard control that stands in for the combo in the editor, or null for none.</param>
        /// <param name="holdSeconds">Hold length before <see cref="Completed"/> fires; 0 fires on the first tick the combo is held.</param>
        /// <param name="indicatorDelaySeconds">Delay before <see cref="IndicatorVisible"/> turns true.</param>
        /// <param name="anyControl">True: the combo counts as held while any one control is pressed. False (default): all controls must be pressed.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="controlPaths"/> is null.</exception>
        public ControllerHoldCombo(
            string name,
            string[] controlPaths,
            string editorFallbackPath,
            float holdSeconds,
            float indicatorDelaySeconds,
            bool anyControl = false)
        {
            if (controlPaths == null)
                throw new ArgumentNullException(nameof(controlPaths));

            HoldSeconds = Mathf.Max(0f, holdSeconds);
            IndicatorDelaySeconds = Mathf.Clamp(indicatorDelaySeconds, 0f, HoldSeconds);
            _anyControl = anyControl;

            _comboActions = new InputAction[controlPaths.Length];
            for (int i = 0; i < controlPaths.Length; i++)
                _comboActions[i] = ButtonAction($"{name} {controlPaths[i]}", controlPaths[i]);

            // Editor only: the simulator drives one controller at a time.
            _editorFallback = Application.isEditor && !string.IsNullOrEmpty(editorFallbackPath)
                ? ButtonAction($"{name} Editor Fallback", editorFallbackPath)
                : null;
        }

        /// <summary>
        /// Creates a combo with <paramref name="isHeldOverride"/> replacing the input
        /// bindings. Used to drive the timing logic without devices.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="isHeldOverride"/> is null.</exception>
        public ControllerHoldCombo(float holdSeconds, float indicatorDelaySeconds, Func<bool> isHeldOverride)
        {
            HoldSeconds = Mathf.Max(0f, holdSeconds);
            IndicatorDelaySeconds = Mathf.Clamp(indicatorDelaySeconds, 0f, HoldSeconds);
            _isHeldOverride = isHeldOverride ?? throw new ArgumentNullException(nameof(isHeldOverride));
            _comboActions = Array.Empty<InputAction>();
        }

        /// <summary>True while the combo counts as held (see the <c>anyControl</c> constructor flag). Always false once disposed.</summary>
        public bool IsHeld
        {
            get
            {
                if (_disposed)
                    return false;

                if (_isHeldOverride != null)
                    return _isHeldOverride();

                if (_editorFallback != null && _editorFallback.IsPressed())
                    return true;

                if (_anyControl)
                {
                    foreach (var action in _comboActions)
                    {
                        if (action.IsPressed())
                            return true;
                    }
                    return false;
                }

                foreach (var action in _comboActions)
                {
                    if (!action.IsPressed())
                        return false;
                }
                return _comboActions.Length > 0;
            }
        }

        /// <summary>Enables the combo's input actions. Call before ticking. Does nothing once disposed.</summary>
        public void Enable()
        {
            if (_disposed)
                return;

            foreach (var action in _comboActions)
                action.Enable();
            _editorFallback?.Enable();
        }

        /// <summary>Disables the combo's input actions and forgets any partial hold. Does nothing once disposed.</summary>
        public void Disable()
        {
            if (_disposed)
                return;

            foreach (var action in _comboActions)
                action.Disable();
            _editorFallback?.Disable();
            Reset();
        }

        /// <summary>Forgets any partial hold.</summary>
        public void Reset()
        {
            _heldSeconds = 0f;
            _completed = false;
        }

        /// <summary>
        /// Advances the hold timer by the caller's time base. Releasing the combo resets
        /// the hold and lets it fire again later. Does nothing once disposed.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_disposed)
                return;

            if (!IsHeld)
            {
                Reset();
                return;
            }

            if (_completed)
                return;

            _heldSeconds += Mathf.Max(0f, deltaTime);
            if (_heldSeconds < HoldSeconds)
                return;

            _completed = true;
            Completed?.Invoke();
        }

        /// <summary>
        /// Releases the combo's input actions and drops every <see cref="Completed"/>
        /// subscriber. Safe to call twice; every member is inert afterwards.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var action in _comboActions)
                action.Dispose();
            _editorFallback?.Dispose();
            Completed = null;
            Reset();
        }

        private static InputAction ButtonAction(string name, string bindingPath)
        {
            var action = new InputAction(name, InputActionType.Button);
            action.AddBinding(bindingPath);
            return action;
        }
    }
}
