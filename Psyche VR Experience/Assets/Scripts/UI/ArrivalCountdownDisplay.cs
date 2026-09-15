using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace PsycheVR.UI
{
    /// <summary>
    /// Drives the world-space arrival display using game time. Connect StartCountdown
    /// to the approach trigger and On Arrived to the next step of the experience.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArrivalCountdownDisplay : MonoBehaviour
    {
        private const float DefaultDurationSeconds = 10f;
        private const float MaximumDurationSeconds = 5999f;
        private const int SecondsPerMinute = 60;
        private const string CountdownHeading = "ARRIVAL IN";
        private const string PausedHeading = "ARRIVAL PAUSED";
        private const string ArrivedHeading = "ARRIVED";

        [Header("Display")]
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text headingText;

        [Header("Timing")]
        [Tooltip("Game seconds until arrival, from 0 to 5999 (99:59).")]
        [Min(0f)]
        [SerializeField] private float durationSeconds = DefaultDurationSeconds;
        [Tooltip("Start automatically the first time this instance becomes active.")]
        [SerializeField] private bool playOnStart;

        [Header("Events")]
        [Tooltip("Invoked once when each started countdown reaches zero.")]
        [SerializeField] private UnityEvent onArrived = new UnityEvent();

        private float remainingSeconds;
        private bool initialized;
        private bool hasStarted;
        private bool hasArrived;
        private int displayedSeconds = -1;
        private string displayedHeading;

        /// <summary>Game seconds left until arrival.</summary>
        public float RemainingSeconds => remainingSeconds;

        /// <summary>Whether the countdown is advancing when game time advances.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Raised once per completed countdown, after the display reaches zero.</summary>
        public UnityEvent OnArrived => onArrived;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            if (playOnStart && !hasStarted)
                StartCountdown();
        }

        private void Update()
        {
            if (!IsRunning)
                return;

            // Scaled time keeps this in sync with the existing pause menu.
            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            if (remainingSeconds <= 0f)
                CompleteCountdown();
            else
                RefreshDisplay();
        }

        /// <summary>Starts a fresh countdown using the duration set in the Inspector.</summary>
        public void StartCountdown()
        {
            StartCountdown(durationSeconds);
        }

        /// <summary>
        /// Starts or restarts with the given duration in game seconds. Values are
        /// clamped to 00:00 through 99:59; zero completes immediately.
        /// </summary>
        public void StartCountdown(float seconds)
        {
            EnsureInitialized();
            remainingSeconds = SanitizeDuration(seconds);
            hasStarted = true;
            hasArrived = false;
            IsRunning = true;

            if (remainingSeconds <= 0f)
                CompleteCountdown();
            else
                RefreshDisplay();
        }

        /// <summary>Pauses this countdown without changing the rest of the game.</summary>
        public void PauseCountdown()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            RefreshDisplay();
        }

        /// <summary>Resumes a paused countdown. Idle and completed displays stay stopped.</summary>
        public void ResumeCountdown()
        {
            if (!hasStarted || hasArrived)
                return;

            IsRunning = true;
            RefreshDisplay();
        }

        /// <summary>Stops and restores the configured duration without firing arrival.</summary>
        public void ResetCountdown()
        {
            initialized = true;
            IsRunning = false;
            hasStarted = false;
            hasArrived = false;
            remainingSeconds = SanitizeDuration(durationSeconds);
            RefreshDisplay();
        }

        private void EnsureInitialized()
        {
            if (!initialized)
                ResetCountdown();
        }

        private void CompleteCountdown()
        {
            remainingSeconds = 0f;
            IsRunning = false;
            hasArrived = true;
            RefreshDisplay();
            onArrived.Invoke();
        }

        private void RefreshDisplay()
        {
            // Round up so 00:00 is shown only when arrival actually occurs.
            int seconds = Mathf.CeilToInt(remainingSeconds);
            if (countdownText != null && displayedSeconds != seconds)
            {
                displayedSeconds = seconds;
                countdownText.text = (seconds / SecondsPerMinute).ToString("00", CultureInfo.InvariantCulture)
                    + ":" + (seconds % SecondsPerMinute).ToString("00", CultureInfo.InvariantCulture);
            }

            string heading = hasArrived ? ArrivedHeading
                : hasStarted && !IsRunning ? PausedHeading : CountdownHeading;
            if (headingText != null && displayedHeading != heading)
            {
                displayedHeading = heading;
                headingText.text = heading;
            }
        }

        private static float SanitizeDuration(float seconds)
        {
            return float.IsNaN(seconds) ? 0f : Mathf.Clamp(seconds, 0f, MaximumDurationSeconds);
        }

        private void OnValidate()
        {
            durationSeconds = SanitizeDuration(durationSeconds);
        }
    }
}
