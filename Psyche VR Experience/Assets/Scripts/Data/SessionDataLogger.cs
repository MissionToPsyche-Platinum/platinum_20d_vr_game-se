using System;
using System.Globalization;
using System.IO;
using System.Security;
using PsycheVR.Modes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PsycheVR.Data
{
    /// <summary>
    /// Automatically records local sessions. Survives scene changes and starts a new
    /// session when the admin restarts the experience or changes the game mode.
    /// Besides lifecycle records it logs, for the quality plan: <c>scene_ready</c> with the load
    /// time, <c>interactables_present</c> (what a visitor could have found),
    /// <c>idle_started</c>/<c>idle_ended</c> when nobody interacts for <see cref="IdleSeconds"/>,
    /// a <c>perf_sample</c> every <see cref="PerfSampleSeconds"/>, and <c>low_memory</c>.
    /// Gameplay events come from <see cref="SessionEvents"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SessionDataLogger : MonoBehaviour
    {
        private const string LogPrefix = "[SessionDataLogger]";
        private const string DirectoryName = "SessionLogs";

        /// <summary>No interaction for this long (s) counts as the visitor stalling.</summary>
        private const float IdleSeconds = 20f;

        /// <summary>Seconds between frame-rate samples.</summary>
        private const float PerfSampleSeconds = 60f;

        /// <summary>
        /// Frames longer than this (s) count as overruns: 1.25 x the Quest 3's 72 Hz frame, so
        /// vsync jitter is ignored and every dropped frame (about 27.8 ms) is counted.
        /// </summary>
        private const float OverrunFrameSeconds = 1.25f / 72f;
        private static SessionDataLogger instance;

        private SessionLogWriter session;
        private GameMode sessionMode;
        private bool restartPending;
        private bool loggingFailed;
        private bool applicationPaused;
        private bool applicationFocused = true;

        private bool readyPending;
        private float loadStartRealtime;
        private bool idle;
        private float idleSince;
        private float perfWindowStart;
        private int perfFrames;
        private int perfOverruns;
        private float perfWorstFrame;

        /// <summary>Current session identifier, or null when logging is unavailable.</summary>
        public static string CurrentSessionId => instance != null ? instance.session?.SessionId : null;

        /// <summary>Current log file path, or null when logging is unavailable.</summary>
        public static string CurrentLogPath => instance != null ? instance.session?.FilePath : null;

        public static string LogDirectory => Path.Combine(Application.persistentDataPath, DirectoryName);

        // GameModeManager loads its config BeforeSceneLoad. Starting after the first
        // scene guarantees that the first record contains the configured mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance == null)
            {
                var root = new GameObject("[SessionDataLogger]");
                root.AddComponent<SessionDataLogger>();
            }

            instance.BeginSession(SceneManager.GetActiveScene().name);
            SessionLogUploader.EnsureCreated();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameModeManager.SessionRestarting += OnSessionRestarting;
            Application.lowMemory += OnLowMemory;
        }

        /// <summary>
        /// Records a named gameplay event on Unity's main thread. Returns false if
        /// logging is unavailable or the event name is empty. Uses the active scene.
        /// </summary>
        public static bool LogEvent(string eventName, string details = "")
        {
            return instance != null && instance.RecordEvent(eventName, SceneManager.GetActiveScene().name, details);
        }

        private void BeginSession(string scene)
        {
            if (session != null || loggingFailed)
                return;

            if (string.IsNullOrWhiteSpace(Application.persistentDataPath))
            {
                loggingFailed = true;
                Debug.LogWarning($"{LogPrefix} No persistent data directory is available; session logging is disabled.", this);
                return;
            }

            try
            {
                sessionMode = GameModeManager.ActiveMode;
                session = new SessionLogWriter(
                    LogDirectory,
                    sessionMode.ToString(), scene, Application.version, Application.platform.ToString(),
                    SessionBuildInfo.CurrentStamp, SessionDeviceName.Current);
                Debug.Log($"{LogPrefix} Saving session to {session.FilePath}", this);
                readyPending = true;
                idle = false;
                SessionEvents.MarkActive("session_start");
                ResetPerfWindow();
            }
            catch (Exception error) when (IsStorageError(error))
            {
                HandleStorageError(error);
            }
        }

        private bool RecordEvent(string eventName, string scene, string details = "")
        {
            if (session == null || string.IsNullOrWhiteSpace(eventName))
                return false;

            try
            {
                session.RecordEvent(eventName, scene, details);
                return true;
            }
            catch (Exception error) when (IsStorageError(error))
            {
                HandleStorageError(error);
                return false;
            }
        }

        private void OnSessionRestarting(GameMode nextMode)
        {
            loadStartRealtime = Time.realtimeSinceStartup;
            EndSession(nextMode == sessionMode ? "restart" : "mode_change");
            restartPending = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (restartPending && loadMode == LoadSceneMode.Single)
            {
                restartPending = false;
                BeginSession(scene.name);
                SessionLogUploader.RequestUpload();   // the session that just ended (one Event visitor)
                return;
            }

            RecordEvent("scene_loaded", scene.name, loadMode.ToString());
        }

        private void OnApplicationPause(bool paused)
        {
            if (instance != this || applicationPaused == paused)
                return;

            applicationPaused = paused;
            if (paused)
            {
                EndIdle();
                FlushPerfSample();
            }
            LogEvent(paused ? "application_paused" : "application_resumed");
            // Headset off or the Quest menu opened: the app may be closed next without another
            // frame, so the request goes out now. Back on: anything still unsent goes too.
            SessionLogUploader.RequestUpload();
            if (!paused)
            {
                // Time with the headset off is neither idling nor a frame-rate sample.
                SessionEvents.MarkActive(SessionEvents.LastActivity);
                ResetPerfWindow();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (instance != this || applicationFocused == focused)
                return;

            applicationFocused = focused;
            LogEvent(focused ? "application_focus_gained" : "application_focus_lost");
        }

        private void OnApplicationQuit()
        {
            if (instance == this)
                EndSession("application_quit");
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameModeManager.SessionRestarting -= OnSessionRestarting;
            Application.lowMemory -= OnLowMemory;
            EndSession("logger_destroyed");
            instance = null;
        }

        private void EndSession(string reason)
        {
            if (session == null)
                return;

            EndIdle();
            FlushPerfSample();
            SessionLogWriter endingSession = session;
            session = null;
            try
            {
                endingSession.EndSession(SceneManager.GetActiveScene().name, reason);
            }
            catch (Exception error) when (IsStorageError(error))
            {
                HandleStorageError(error);
            }
        }

        private void Update()
        {
            if (instance != this || session == null)
                return;

            if (readyPending)
            {
                // The first frame of a session is the first the visitor can act in.
                readyPending = false;
                LogEvent("scene_ready", "loadSeconds=" + SessionEvents.Seconds(Time.realtimeSinceStartup - loadStartRealtime));
                string inventory = SessionEvents.Inventory(out int count);
                LogEvent("interactables_present", "count=" + count + ";objects=" + inventory);
                ResetPerfWindow();
                return;
            }

            if (applicationPaused)
                return;

            TrackIdle();
            TrackFrames();
        }

        private void TrackIdle()
        {
            float now = Time.unscaledTime;
            if (idle)
            {
                if (SessionEvents.LastActivityTime > idleSince)
                    EndIdle();
                return;
            }

            if (now - SessionEvents.LastActivityTime < IdleSeconds)
                return;

            idle = true;
            idleSince = SessionEvents.LastActivityTime;
            Transform head = Camera.main != null ? Camera.main.transform : null;
            string where = head == null ? "" : string.Format(CultureInfo.InvariantCulture,
                ";head={0:0.0},{1:0.0},{2:0.0};yaw={3:0}", head.position.x, head.position.y, head.position.z, head.eulerAngles.y);
            LogEvent("idle_started", "after=" + SessionEvents.LastActivity + where);
        }

        private void EndIdle()
        {
            if (!idle)
                return;
            idle = false;
            float end = Mathf.Max(SessionEvents.LastActivityTime, idleSince);
            if (end <= idleSince) end = Time.unscaledTime;
            LogEvent("idle_ended", "seconds=" + SessionEvents.Seconds(end - idleSince) + ";next=" + SessionEvents.LastActivity);
        }

        private void TrackFrames()
        {
            float frame = Time.unscaledDeltaTime;
            perfFrames++;
            if (frame > OverrunFrameSeconds) perfOverruns++;
            if (frame > perfWorstFrame) perfWorstFrame = frame;
            if (Time.unscaledTime - perfWindowStart >= PerfSampleSeconds)
                FlushPerfSample();
        }

        private void FlushPerfSample()
        {
            float seconds = Time.unscaledTime - perfWindowStart;
            if (session != null && perfFrames > 0 && seconds > 0f)
            {
                LogEvent("perf_sample", string.Format(CultureInfo.InvariantCulture,
                    "seconds={0:0.#};fps={1:0.#};overrunPct={2:0.##};worstMs={3:0.#}",
                    seconds, perfFrames / seconds, 100f * perfOverruns / perfFrames, perfWorstFrame * 1000f));
            }
            ResetPerfWindow();
        }

        private void ResetPerfWindow()
        {
            perfWindowStart = Time.unscaledTime;
            perfFrames = 0;
            perfOverruns = 0;
            perfWorstFrame = 0f;
        }

        private void OnLowMemory()
        {
            LogEvent("low_memory");
        }

        private void HandleStorageError(Exception error)
        {
            loggingFailed = true;
            SessionLogWriter failedSession = session;
            session = null;
            try
            {
                failedSession?.Dispose();
            }
            catch (Exception cleanupError) when (IsStorageError(cleanupError))
            {
                // The original failure below already explains why logging stopped.
            }

            Debug.LogWarning($"{LogPrefix} Session logging stopped after a storage error: {error.Message}", this);
        }

        private static bool IsStorageError(Exception error)
        {
            return error is IOException || error is UnauthorizedAccessException || error is SecurityException;
        }
    }
}
