using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// "Feel Psyche's push": while the visitor holds the ops console's mouse and the monitor shows
    /// the Thruster tab, the holding controller gets a soft, steady buzz for <see cref="MaxSeconds"/>,
    /// with the content's rumble line on the monitor's header pop-up bar for as long as it lasts.
    ///
    /// Until a visitor's first grab, the Thruster tab carries the content's prompt ("grab the mouse")
    /// on that pop-up; after it, the prompt never shows again (a kiosk reset reloads the scene, so the
    /// next visitor sees it). The footer keeps the tab's own closing line throughout.
    ///
    /// The pulse is subtle on purpose. Psyche's Hall-effect thruster pushes with about 250 mN, the
    /// weight of an AA battery on your palm, but it keeps that up for weeks. A strong rumble would
    /// tell the wrong story; a faint, constant one is the point.
    ///
    /// The push starts when the mouse is grabbed with the console on and on the Thruster tab, or when
    /// the tab turns to Thruster while the mouse is held. It stops on release, when the tab changes
    /// away, or when the console powers off. Timing follows scaled time, so it freezes while paused.
    /// </summary>
    public class ThrusterPushHaptic : MonoBehaviour
    {
        private const int ThrusterTabIndex = 3;
        private const float Amplitude = 0.15f;
        private const float PulseSeconds = 0.15f;      // overlaps the resend so the buzz never gaps
        private const float ResendSeconds = 0.1f;
        private const float MaxSeconds = 6f;

        [Tooltip("The monitor's console: its tab, power and pop-up.")]
        [SerializeField] private OpsConsole console;

        [Tooltip("The grabbable mouse.")]
        [SerializeField] private PsycheGrabbable grabbable;

        private XRBaseInputInteractor _holder;
        private bool _running;
        private float _elapsed;
        private float _nextPulse;
        private bool _rumbleShown;     // the rumble line is on the pop-up
        private bool _promptRetired;   // this visitor has grabbed the mouse once

        private void OnEnable()
        {
            if (console == null || grabbable == null)
            {
                Debug.LogError($"[ThrusterPushHaptic] {name}: {(console == null ? "console" : "grabbable")} is not assigned; no push haptic.", this);
                enabled = false;
                return;
            }
            grabbable.selectEntered.AddListener(OnGrabbed);
            grabbable.selectExited.AddListener(OnReleased);
            console.OnTabShown.AddListener(OnTabShown);
            console.OnPowerChanged.AddListener(OnPowerChanged);
        }

        private void OnDisable()
        {
            if (console == null || grabbable == null) return;
            grabbable.selectEntered.RemoveListener(OnGrabbed);
            grabbable.selectExited.RemoveListener(OnReleased);
            console.OnTabShown.RemoveListener(OnTabShown);
            console.OnPowerChanged.RemoveListener(OnPowerChanged);
            Stop();
            _holder = null;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            _holder = args.interactorObject as XRBaseInputInteractor;
            // single select: a hand-to-hand handoff releases first, so the push restarts from 0 s
            if (!_running) TryStart();
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (!ReferenceEquals(args.interactorObject, _holder)) return;
            _holder = null;
            Stop();
        }

        private void OnTabShown(int tab)
        {
            if (tab != ThrusterTabIndex) { Stop(); console.HidePopup(); return; }
            if (!_running) TryStart();
            if (!_running && !_promptRetired && console.Content != null) console.ShowPopup(console.Content.pushPrompt);
        }

        private void OnPowerChanged(bool on)
        {
            if (!on) { Stop(); console.HidePopup(); }
        }

        private void TryStart()
        {
            if (_holder == null || !console.IsOn || console.CurrentTab != ThrusterTabIndex) return;
            _running = true;
            _promptRetired = true;
            _elapsed = 0f;
            _nextPulse = 0f;
            _rumbleShown = console.Content != null && !string.IsNullOrEmpty(console.Content.pushRumbleLine);
            if (_rumbleShown) console.ShowPopup(console.Content.pushRumbleLine);
            else console.HidePopup();   // the prompt goes either way
            Step();
        }

        private void Stop()
        {
            if (!_running) return;
            _running = false;
            HideRumble();
        }

        private void Update()
        {
            if (!_running) return;
            _elapsed += Time.deltaTime;
            Step();
        }

        /// <summary>Sends a pulse when one is due; at the end of the push the rumble line goes.</summary>
        private void Step()
        {
            if (_elapsed >= MaxSeconds) { HideRumble(); return; }
            if (_holder == null || _elapsed < _nextPulse) return;
            _holder.SendHapticImpulse(Amplitude, PulseSeconds);
            _nextPulse += ResendSeconds;
        }

        private void HideRumble()
        {
            if (!_rumbleShown) return;
            _rumbleShown = false;
            console.HidePopup();
        }
    }
}
