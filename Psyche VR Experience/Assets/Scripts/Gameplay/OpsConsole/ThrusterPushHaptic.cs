using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// "Feel Psyche's push": while the visitor holds the ops console's mouse and the monitor shows
    /// the Thruster tab, the holding controller gets a soft, steady buzz for up to
    /// <see cref="MaxSeconds"/>, and the footer steps through the content's push lines (one at the
    /// grab, the next after <see cref="SecondLineSeconds"/>, the last at <see cref="ThirdLineSeconds"/>).
    ///
    /// The pulse is subtle on purpose. Psyche's Hall-effect thruster pushes with about 250 mN, the
    /// weight of an AA battery on your palm, but it keeps that up for weeks. A strong rumble would
    /// tell the wrong story; a faint, constant one is the point, and the footer says so.
    ///
    /// It starts when the mouse is grabbed with the console on and on the Thruster tab, or when the
    /// tab turns to Thruster while the mouse is held. It stops (and the footer returns to the tab's
    /// own) on release, when the tab changes away, or when the console powers off. Timing follows
    /// scaled time, so it freezes while the game is paused.
    /// </summary>
    public class ThrusterPushHaptic : MonoBehaviour
    {
        private const int ThrusterTabIndex = 3;
        private const float Amplitude = 0.15f;
        private const float PulseSeconds = 0.15f;      // overlaps the resend so the buzz never gaps
        private const float ResendSeconds = 0.1f;
        private const float MaxSeconds = 6f;
        private const float FirstLineSeconds = 0f;
        private const float SecondLineSeconds = 3f;
        private const float ThirdLineSeconds = 6f;
        private static readonly float[] LineSeconds = { FirstLineSeconds, SecondLineSeconds, ThirdLineSeconds };

        [Tooltip("The monitor's console: its tab, power and footer.")]
        [SerializeField] private OpsConsole console;

        [Tooltip("The grabbable mouse.")]
        [SerializeField] private PsycheGrabbable grabbable;

        private XRBaseInputInteractor _holder;
        private bool _running;
        private float _elapsed;
        private float _nextPulse;
        private int _linesShown;

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
            // single select: a hand-to-hand handoff releases first, so the push restarts from 0 s at line 1
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
            if (tab == ThrusterTabIndex) { if (!_running) TryStart(); }
            else Stop();
        }

        private void OnPowerChanged(bool on)
        {
            if (!on) Stop();
        }

        private void TryStart()
        {
            if (_holder == null || !console.IsOn || console.CurrentTab != ThrusterTabIndex) return;
            _running = true;
            _elapsed = 0f;
            _nextPulse = 0f;
            _linesShown = 0;
            Step();
        }

        private void Stop()
        {
            if (!_running) return;
            _running = false;
            console.SetFooterOverride(null);
        }

        private void Update()
        {
            if (!_running) return;
            _elapsed += Time.deltaTime;
            Step();
        }

        /// <summary>Shows any line now due and sends a pulse when one is due, until the push ends.</summary>
        private void Step()
        {
            var lines = console.Content != null ? console.Content.pushLines : null;
            while (lines != null && _linesShown < LineSeconds.Length && _linesShown < lines.Length
                   && _elapsed >= LineSeconds[_linesShown])
            {
                console.SetFooterOverride(lines[_linesShown]);
                _linesShown++;
            }

            if (_elapsed >= MaxSeconds || _holder == null || _elapsed < _nextPulse) return;
            _holder.SendHapticImpulse(Amplitude, PulseSeconds);
            _nextPulse += ResendSeconds;
        }
    }
}
