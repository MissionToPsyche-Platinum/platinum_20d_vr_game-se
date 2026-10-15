using UnityEngine;
using PsycheVR.Modes;
using PsycheVR.UI;

namespace PsycheVR.Kiosk
{
    /// <summary>
    /// The helper's reset: on the left controller grip, trigger and Y, on the right grip, trigger and a
    /// thumbstick click, all six held together, from any state. The old grips + A/X hold was too easy
    /// for a visitor holding two props to fall into; this one needs a deliberate hand shape on each
    /// controller, and nothing in Event mode uses Y or the stick click. After
    /// <see cref="indicatorDelaySeconds"/> the shared <see cref="HoldRingHud"/> appears mid-view and fills;
    /// completing the hold reloads the master scene in Event mode, which resets every object.
    ///
    /// Ticks on unscaled time so it works while the game is paused. F9 in the editor.
    /// </summary>
    public sealed class KioskResetListener : MonoBehaviour
    {
        private const string LogPrefix = "[KioskResetListener]";
        private const string GripControl = "/gripPressed";
        private const string TriggerControl = "/triggerPressed";
        private const string SecondaryButtonControl = "/secondaryButton";      // Y on the left controller
        private const string ThumbstickClickControl = "/{Primary2DAxisClick}";
        private const string EditorFallbackControl = "<Keyboard>/f9";

        private static readonly string[] Controls =
        {
            ControllerHoldCombo.LeftHand + GripControl,
            ControllerHoldCombo.LeftHand + TriggerControl,
            ControllerHoldCombo.LeftHand + SecondaryButtonControl,
            ControllerHoldCombo.RightHand + GripControl,
            ControllerHoldCombo.RightHand + TriggerControl,
            ControllerHoldCombo.RightHand + ThumbstickClickControl
        };

        [Tooltip("Seconds the combo (left grip + trigger + Y, right grip + trigger + stick click) must be held before the room resets. F9 in the editor.")]
        [Min(0.5f)]
        [SerializeField] private float holdSeconds = 4f;

        [Tooltip("Seconds into the hold before the progress ring appears mid-view. A brief accidental press shows nothing.")]
        [Min(0f)]
        [SerializeField] private float indicatorDelaySeconds = 1f;

        [Tooltip("Headset camera the ring locks to. Unset: Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        private ControllerHoldCombo _combo;
        private HoldRingHud _ring;
        private bool _ready;

        // Built in OnEnable and disposed in OnDisable, unlike KioskSession, because this
        // listener has no Start-time lookups and must simply follow the object's active state.
        private void OnEnable()
        {
            _ready = false;
            _combo = new ControllerHoldCombo("Kiosk Reset", Controls, EditorFallbackControl, holdSeconds, indicatorDelaySeconds);
            _combo.Completed += ResetRoom;
            _combo.Enable();
        }

        private void OnDisable()
        {
            if (_ring != null)
                _ring.Report(this, false, 0f);

            if (_combo == null)
                return;

            _combo.Completed -= ResetRoom;
            _combo.Dispose();
            _combo = null;
        }

        private void Update()
        {
            if (_combo == null)
                return;

            // Arm only after a frame with the combo released, so a helper still holding it
            // through the reload does not trigger a second reload 3 s later.
            if (!_ready)
            {
                _ready = !_combo.IsHeld;
                return;
            }

            // Unscaled: the reset must work while the pause menu has the game frozen.
            _combo.Tick(Time.unscaledDeltaTime);
            ShowRing();
        }

        private void ShowRing()
        {
            if (_ring == null)
            {
                if (cameraTransform == null && Camera.main != null)
                    cameraTransform = Camera.main.transform;
                _ring = HoldRingHud.For(cameraTransform);
                if (_ring == null)
                    return;
            }
            _ring.Report(this, _combo.IndicatorVisible, _combo.Progress);
        }

        private void ResetRoom()
        {
            Debug.Log($"{LogPrefix} Reset combo held {holdSeconds:0.#}s; reloading Event mode.", this);
            GameModeManager.SwitchTo(GameMode.Event);
        }
    }
}
