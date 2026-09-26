using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using PsycheVR.UI;

namespace PsycheVR.Kiosk
{
    /// <summary>
    /// Runs one kiosk visitor's session in Event mode. The clock arms on scene load and
    /// starts at the visitor's first grip or trigger press, so handoff time does not
    /// count against them. Whichever comes first, puzzle complete or clock expiry, starts
    /// the launch video: that is the sponsor's "soft nudge" ending. The visitor may keep
    /// exploring afterwards; nothing fires again until the helper resets the room
    /// (<see cref="KioskResetListener"/>), which reloads the scene.
    ///
    /// Lives on the Kiosk prefab, which a <see cref="Modes.ModeGate"/> removes in Story
    /// mode, so this component never runs there.
    /// </summary>
    public sealed class KioskSession : MonoBehaviour
    {
        /// <summary>Where the session is.</summary>
        public enum Phase
        {
            /// <summary>Waiting for the visitor's first input.</summary>
            Armed,
            /// <summary>Clock running.</summary>
            Running,
            /// <summary>Launch video playing.</summary>
            Ending,
            /// <summary>Video done; free play until the helper resets.</summary>
            FreePlay
        }

        private const string LogPrefix = "[KioskSession]";
        private const string GripControl = "/gripPressed";
        private const string TriggerControl = "/triggerPressed";

        private static readonly string[] FirstInputControls =
        {
            ControllerHoldCombo.LeftHand + GripControl,
            ControllerHoldCombo.RightHand + GripControl,
            ControllerHoldCombo.LeftHand + TriggerControl,
            ControllerHoldCombo.RightHand + TriggerControl
        };

        [Header("Clock")]
        [Tooltip("Seconds from the visitor's first grip or trigger press until the launch video starts on its own. Sponsor asked for 3 to 5 minutes, leaning 3.")]
        [Min(1f)]
        [SerializeField] private float sessionSeconds = 180f;

        [Header("Ending")]
        [Tooltip("Video started when the puzzle completes or the clock expires. Unset: the first play_video in the scene.")]
        [SerializeField] private play_video launchVideo;
        [Tooltip("Puzzle watcher whose Completed event ends the session early. Unset: the PuzzleCompletion on this object, if any.")]
        [SerializeField] private PuzzleCompletion puzzle;

        [Header("Hand-over message")]
        [Tooltip("Headset camera the message locks to. Unset: Camera.main.")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Shown after the launch video ends.")]
        [TextArea]
        [SerializeField] private string messageText = "Thanks for exploring Psyche.\nPlease hand the headset to the event team.";
        [Tooltip("Seconds the message stays fully visible.")]
        [Min(0f)]
        [SerializeField] private float messageHoldSeconds = 6f;
        [Tooltip("Seconds for each fade, in and out.")]
        [Min(0f)]
        [SerializeField] private float messageFadeSeconds = 1f;
        [Tooltip("Metres in front of the camera.")]
        [Min(0.45f)]
        [SerializeField] private float messageDistance = 1.35f;
        [Tooltip("Seconds to wait for the video to prepare before giving up on its length.")]
        [Min(0f)]
        [SerializeField] private float videoPrepareTimeoutSeconds = 5f;
        [Tooltip("Seconds added to the clip length for the fallback timer that shows the message if the video's end event never arrives.")]
        [Min(0f)]
        [SerializeField] private float videoEndMarginSeconds = 2f;

        private ControllerHoldCombo _firstInput;
        private float _elapsedSeconds;
        private bool _armReady;
        private VideoPlayer _videoPlayer;
        private bool _messageShown;

        /// <summary>Current phase of the session.</summary>
        public Phase CurrentPhase { get; private set; } = Phase.Armed;

        /// <summary>Seconds left on the clock; the full length while armed.</summary>
        public float RemainingSeconds => Mathf.Max(0f, sessionSeconds - _elapsedSeconds);

        // Start does not re-run when the component is re-enabled, so construction stays
        // in Start and OnDestroy still owns Dispose; these two only park the input.
        private void OnEnable()
        {
            if (CurrentPhase == Phase.Armed)
                _firstInput?.Enable();
        }

        private void OnDisable()
        {
            _firstInput?.Disable();
            _armReady = false;
        }

        private void Start()
        {
            if (launchVideo == null)
                launchVideo = FindFirstObjectByType<play_video>();
            if (launchVideo == null)
                Debug.LogError($"{LogPrefix} No play_video in '{gameObject.scene.name}'; the session will end with the message only.", this);

            if (puzzle == null)
                puzzle = GetComponent<PuzzleCompletion>();
            if (puzzle != null)
            {
                puzzle.Completed += HandlePuzzleCompleted;

                // Completed is raised once and never replayed, and Start order within one
                // GameObject is unspecified, so a puzzle that finished first is only seen by asking.
                if (puzzle.IsComplete)
                    HandlePuzzleCompleted();
            }

            // Skipped when the line above already ended the session: arming then would
            // enable an input action nothing ever ticks.
            if (CurrentPhase == Phase.Armed)
            {
                // Hold 0: Completed fires on the first ticked frame one of the four controls
                // is observed pressed, and not again until all four have been released.
                _firstInput = new ControllerHoldCombo("Kiosk First Input", FirstInputControls, null, 0f, 0f, anyControl: true);
                _firstInput.Completed += HandleFirstInput;
                _firstInput.Enable();

                Debug.Log($"{LogPrefix} Armed; {sessionSeconds:0}s session starts on first input.", this);
            }
        }

        private void OnDestroy()
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.loopPointReached -= HandleVideoEnded;
                _videoPlayer.errorReceived -= HandleVideoError;
            }

            if (puzzle != null)
                puzzle.Completed -= HandlePuzzleCompleted;

            if (_firstInput != null)
            {
                _firstInput.Completed -= HandleFirstInput;
                _firstInput.Dispose();
                _firstInput = null;
            }
        }

        private void Update()
        {
            // Scaled time on purpose: an open pause menu freezes the session.
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            switch (CurrentPhase)
            {
                case Phase.Armed:
                    // Arm only after a frame with nothing held, so a helper still holding
                    // the reset combo through the reload cannot start the clock.
                    if (!_armReady)
                    {
                        _armReady = !(_firstInput?.IsHeld ?? false);
                        break;
                    }
                    _firstInput?.Tick(dt);
                    break;
                case Phase.Running:
                    TickClock(dt);
                    break;
            }
        }

        private void HandleFirstInput()
        {
            if (CurrentPhase != Phase.Armed)
                return;

            CurrentPhase = Phase.Running;
            _firstInput?.Disable();
            Debug.Log($"{LogPrefix} First input; clock running.", this);
        }

        private void TickClock(float deltaTime)
        {
            _elapsedSeconds += deltaTime;
            if (_elapsedSeconds >= sessionSeconds)
                BeginEnding("clock expired");
        }

        private void HandlePuzzleCompleted()
        {
            if (CurrentPhase == Phase.Ending || CurrentPhase == Phase.FreePlay)
                return;

            BeginEnding("puzzle complete");
        }

        /// <summary>
        /// Starts the ending now, whatever the clock says. Idempotent once the ending has
        /// begun. Public so a debug button or the admin section can trigger it; the string
        /// parameter also keeps it callable from a UnityEvent in the inspector.
        /// </summary>
        /// <param name="reason">Diagnostic only: logged, never branched on; e.g. "clock expired".</param>
        public void BeginEnding(string reason)
        {
            if (CurrentPhase == Phase.Ending || CurrentPhase == Phase.FreePlay)
                return;

            CurrentPhase = Phase.Ending;
            // Null before Start has armed the session: a caller may end it that early.
            _firstInput?.Disable();
            Debug.Log($"{LogPrefix} Ending session ({reason}) at {_elapsedSeconds:0}s.", this);

            if (launchVideo == null)
            {
                ShowMessage();
                return;
            }

            _videoPlayer = launchVideo.GetComponent<VideoPlayer>();
            if (_videoPlayer == null)
            {
                Debug.LogError($"{LogPrefix} '{launchVideo.name}' has no VideoPlayer; the message will show after the margin only.", this);
            }
            else
            {
                _videoPlayer.loopPointReached += HandleVideoEnded;
                _videoPlayer.errorReceived += HandleVideoError;
            }

            launchVideo.StartVideo();
            StartCoroutine(ShowMessageWhenVideoShouldBeOver());
        }

        private void HandleVideoEnded(VideoPlayer source)
        {
            ShowMessage();
        }

        private void HandleVideoError(VideoPlayer source, string message)
        {
            Debug.LogError($"{LogPrefix} Video error: {message}", this);
        }

        /// <summary>
        /// Backstop for a video whose end event never arrives (looping clip, codec
        /// failure): waits for the player to prepare, then the clip length plus a margin.
        /// Whichever of this and <see cref="HandleVideoEnded"/> comes first shows the
        /// message; the other is a no-op.
        /// </summary>
        private IEnumerator ShowMessageWhenVideoShouldBeOver()
        {
            float deadline = Time.unscaledTime + videoPrepareTimeoutSeconds;
            while (_videoPlayer != null && !_videoPlayer.isPrepared && Time.unscaledTime < deadline)
                yield return null;

            float length = _videoPlayer != null && _videoPlayer.isPrepared ? (float)_videoPlayer.length : 0f;
            if (length <= 0f)
                Debug.LogWarning($"{LogPrefix} Video length unknown; showing the message after {videoEndMarginSeconds:0.#}s.", this);

            // Realtime on purpose: the scene's VideoPlayer runs on UnscaledGameTime
            // (m_TimeUpdateMode: 2), so the clip, this timer and the fades share one clock.
            // Switch that setting to GameTime and this fallback drifts whenever the game pauses.
            yield return new WaitForSecondsRealtime(length + videoEndMarginSeconds);
            ShowMessage();
        }

        private void ShowMessage()
        {
            if (_messageShown)
                return;

            _messageShown = true;
            CurrentPhase = Phase.FreePlay;

            Transform camera = cameraTransform != null ? cameraTransform : Camera.main != null ? Camera.main.transform : null;
            if (camera == null)
            {
                Debug.LogError($"{LogPrefix} No camera for the hand-over message; free play without it.", this);
                return;
            }

            HandoverMessage.Spawn(camera, messageDistance).Show(messageText, messageFadeSeconds, messageHoldSeconds);
            Debug.Log($"{LogPrefix} Free play.", this);
        }
    }
}
