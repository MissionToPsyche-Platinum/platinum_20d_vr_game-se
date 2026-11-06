using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using PsycheVR.Audio;
using PsycheVR.Gameplay;
using PsycheVR.UI;
using PsycheVR.Data;
using PsycheVR.Modes;

namespace PsycheVR.Kiosk
{
    /// <summary>
    /// Runs one kiosk visitor's session in Event mode. The clock arms on scene load and
    /// starts at the visitor's first input, so handoff time does not
    /// count against them. Only clock expiry starts the launch video: that is the sponsor's
    /// "soft nudge" ending. Finishing the puzzle is logged but never ends the session early;
    /// what the room does on completion belongs to the puzzle's own listeners. The visitor may keep
    /// exploring afterwards; nothing fires again until the helper resets the room
    /// (<see cref="KioskResetListener"/>), which reloads the scene.
    ///
    /// The first input is both triggers squeezed together (TG-324), with the puzzle success tone, and
    /// until then nothing in the room can be touched: no hover, no grab, no XRI press, and the hands
    /// have no colliders, so they push and slap nothing. A grip or trigger brushed while the controllers
    /// change hands neither moves anything nor dismisses the controller guide.
    ///
    /// Lives on the Kiosk prefab, which a <see cref="Modes.ModeGate"/> removes in Story
    /// mode, so this component never runs there.
    /// </summary>
    public sealed class KioskSession : MonoBehaviour
    {
        /// <summary>Where the session is.</summary>
        public enum Phase
        {
            /// <summary>Waiting for both triggers; the room is locked.</summary>
            Armed,
            /// <summary>Clock running.</summary>
            Running,
            /// <summary>Launch video playing.</summary>
            Ending,
            /// <summary>Video done; free play until the helper resets.</summary>
            FreePlay
        }

        private const string LogPrefix = "[KioskSession]";
        private const string TriggerControl = "/triggerPressed";
        /// <summary>Starts the session in the editor, where the simulator holds one trigger at a time.</summary>
        private const string EditorStartControl = "<Keyboard>/f7";

        private static readonly string[] StartControls =
        {
            ControllerHoldCombo.LeftHand + TriggerControl,
            ControllerHoldCombo.RightHand + TriggerControl
        };

        [Header("Clock")]
        [Tooltip("Seconds from the visitor's two-trigger start until the launch video starts on its own. Sponsor asked for 3 to 5 minutes; set to 2.5 for the Oct 10 sponsor build.")]
        [Min(1f)]
        [SerializeField] private float sessionSeconds = 150f;

        [Header("Ending")]
        [Tooltip("Video started when the clock expires. Unset: the first play_video in the scene.")]
        [SerializeField] private play_video launchVideo;
        [Tooltip("Puzzle watcher whose Completed event is logged; it never ends the session. Unset: the PuzzleCompletion on this object, if any.")]
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
        private RoomLock _lock;
        private float _elapsedSeconds;
        private bool _armReady;
        private bool _puzzleLogged;
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

            // Skipped when something already ended the session (BeginEnding is public):
            // arming then would enable an input action nothing ever ticks.
            if (CurrentPhase == Phase.Armed)
            {
                // Hold 0: Completed fires on the first ticked frame both triggers are observed
                // pressed together.
                _firstInput = new ControllerHoldCombo("Kiosk First Input", StartControls, EditorStartControl, 0f, 0f);
                _firstInput.Completed += HandleFirstInput;
                _firstInput.Enable();

                _lock = new RoomLock();
                _lock.Engage();
                Debug.Log($"{LogPrefix} Armed; {sessionSeconds:0}s session starts when both triggers are squeezed.", this);
            }
        }

        private void OnDestroy()
        {
            ReleaseLock();
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
            ReleaseLock();
            var head = cameraTransform != null ? cameraTransform : (Camera.main != null ? Camera.main.transform : transform);
            InteractionAudio.Play(InteractionSound.PuzzleSnap, head.position);
            RecordOutcome("kiosk_first_input");
            Debug.Log($"{LogPrefix} Both triggers; room unlocked, clock running.", this);
        }

        private void ReleaseLock()
        {
            _lock?.Release();
            _lock = null;
        }

        /// <summary>
        /// Keeps the room untouchable before the session starts: a hover and select filter on every
        /// interaction manager (grabs, XRI buttons, teleport) plus <see cref="HandPhysics.Locked"/>, which
        /// keeps the hand colliders off (they push props and press the slap keys).
        /// </summary>
        private sealed class RoomLock : IXRHoverFilter, IXRSelectFilter
        {
            private XRInteractionManager[] _managers;

            public bool canProcess => true;

            public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable) => false;

            public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => false;

            public void Engage()
            {
                _managers = UnityEngine.Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None);
                foreach (var m in _managers)
                {
                    m.hoverFilters.Add(this);
                    m.selectFilters.Add(this);
                }
                HandPhysics.Locked = true;
            }

            public void Release()
            {
                if (_managers != null)
                    foreach (var m in _managers)
                    {
                        if (m == null) continue;
                        m.hoverFilters.Remove(this);
                        m.selectFilters.Remove(this);
                    }
                _managers = null;
                HandPhysics.Locked = false;
            }
        }

        private void TickClock(float deltaTime)
        {
            if (CurrentPhase != Phase.Running || deltaTime <= 0f)
                return;

            _elapsedSeconds += deltaTime;
            if (_elapsedSeconds >= sessionSeconds)
            {
                RecordOutcome("kiosk_clock_expired");
                BeginEnding("clock expired");
            }
        }

        /// <summary>
        /// Logs the completion once, while the clock has not yet run out, so the log can
        /// show completion before expiry. Deliberately does not start the ending: the
        /// 2.5-minute clock is the only thing that does.
        /// </summary>
        private void HandlePuzzleCompleted()
        {
            if (_puzzleLogged || CurrentPhase == Phase.Ending || CurrentPhase == Phase.FreePlay)
                return;

            _puzzleLogged = true;
            RecordOutcome("kiosk_puzzle_completed");
        }

        private void RecordOutcome(string eventName)
        {
            if (GameModeManager.ActiveMode == GameMode.Event)
            {
                SessionDataLogger.LogEvent(eventName,
                    "elapsedSeconds=" + _elapsedSeconds.ToString("0.###", CultureInfo.InvariantCulture));
                if (eventName != "kiosk_first_input")
                    SessionLogUploader.RequestUpload();   // the visitor's result goes up as soon as it is known
            }
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
