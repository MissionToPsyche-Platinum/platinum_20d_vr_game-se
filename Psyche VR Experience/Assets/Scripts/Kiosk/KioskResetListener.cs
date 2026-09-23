using UnityEngine;
using PsycheVR.Modes;
using PsycheVR.UI;

namespace PsycheVR.Kiosk
{
    /// <summary>
    /// The helper's reset: both grips and both A/X buttons held together, from any
    /// state, without wearing the headset. Nothing in Event mode uses the face buttons,
    /// so a visitor grabbing two objects cannot trigger it, and it shares no full set of
    /// controls with the pause menu's admin combo (grips + stick clicks). Completing it
    /// reloads the master scene in Event mode, which resets every object.
    ///
    /// Ticks on unscaled time so it works while the game is paused. F9 in the editor.
    /// </summary>
    public sealed class KioskResetListener : MonoBehaviour
    {
        private const string LogPrefix = "[KioskResetListener]";
        private const string GripControl = "/gripPressed";
        private const string PrimaryButtonControl = "/primaryButton";
        private const string EditorFallbackControl = "<Keyboard>/f9";

        private static readonly string[] Controls =
        {
            ControllerHoldCombo.LeftHand + GripControl,
            ControllerHoldCombo.RightHand + GripControl,
            ControllerHoldCombo.LeftHand + PrimaryButtonControl,
            ControllerHoldCombo.RightHand + PrimaryButtonControl
        };

        [Tooltip("Seconds both grips and both A/X buttons must be held before the room resets. F9 in the editor.")]
        [Min(0.5f)]
        [SerializeField] private float holdSeconds = 3f;

        private ControllerHoldCombo _combo;
        private bool _ready;

        // Built in OnEnable and disposed in OnDisable, unlike KioskSession, because this
        // listener has no Start-time lookups and must simply follow the object's active state.
        private void OnEnable()
        {
            _ready = false;
            _combo = new ControllerHoldCombo("Kiosk Reset", Controls, EditorFallbackControl, holdSeconds, 0f);
            _combo.Completed += ResetRoom;
            _combo.Enable();
        }

        private void OnDisable()
        {
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
        }

        private void ResetRoom()
        {
            Debug.Log($"{LogPrefix} Reset combo held {holdSeconds:0.#}s; reloading Event mode.", this);
            GameModeManager.SwitchTo(GameMode.Event);
        }
    }
}
