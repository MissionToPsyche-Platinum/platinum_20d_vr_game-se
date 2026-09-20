using System;
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
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SessionDataLogger : MonoBehaviour
    {
        private const string LogPrefix = "[SessionDataLogger]";
        private const string DirectoryName = "SessionLogs";
        private static SessionDataLogger instance;

        private SessionLogWriter session;
        private GameMode sessionMode;
        private bool restartPending;
        private bool loggingFailed;
        private bool applicationPaused;
        private bool applicationFocused = true;

        /// <summary>Current session identifier, or null when logging is unavailable.</summary>
        public static string CurrentSessionId => instance != null ? instance.session?.SessionId : null;

        /// <summary>Current log file path, or null when logging is unavailable.</summary>
        public static string CurrentLogPath => instance != null ? instance.session?.FilePath : null;

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
                    Path.Combine(Application.persistentDataPath, DirectoryName),
                    sessionMode.ToString(), scene, Application.version, Application.platform.ToString());
                Debug.Log($"{LogPrefix} Saving session to {session.FilePath}", this);
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
            EndSession(nextMode == sessionMode ? "restart" : "mode_change");
            restartPending = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (restartPending && loadMode == LoadSceneMode.Single)
            {
                restartPending = false;
                BeginSession(scene.name);
                return;
            }

            RecordEvent("scene_loaded", scene.name, loadMode.ToString());
        }

        private void OnApplicationPause(bool paused)
        {
            if (instance != this || applicationPaused == paused)
                return;

            applicationPaused = paused;
            LogEvent(paused ? "application_paused" : "application_resumed");
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
            EndSession("logger_destroyed");
            instance = null;
        }

        private void EndSession(string reason)
        {
            if (session == null)
                return;

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
