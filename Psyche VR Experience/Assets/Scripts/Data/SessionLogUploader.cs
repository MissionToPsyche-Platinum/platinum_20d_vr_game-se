using System;
using System.Collections;
using System.IO;
using System.Security;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PsycheVR.Data
{
    /// <summary>
    /// Sends every new or grown session file: once at startup, on demand, and every
    /// <see cref="PeriodicSeconds"/> while running. <see cref="RequestUpload"/> is the trigger the
    /// logger uses at moments a visitor's data must not wait for the next launch (headset off,
    /// visitor finished, session restarted); a request that arrives mid-pass runs right after it.
    /// </summary>
    public sealed class SessionLogUploader : MonoBehaviour
    {
        /// <summary>Seconds between automatic passes while the app runs.</summary>
        private const float PeriodicSeconds = 180f;

        private static SessionLogUploader instance;
        private bool rerunWanted;
        private float nextPeriodic;
        private SessionUploadConfig config;
        private bool busy;
        private bool attemptedUpload;
        private string lastResult = "Not uploaded yet.";
        private UnityWebRequest activeRequest;
        private string acknowledgedActivePath, acknowledgedActiveHash;
        private int sent, failed;

        public static bool IsUploading => instance != null && instance.busy;
        public static bool IsEnabled => instance != null && instance.config != null;
        public static string LastResult => instance != null ? instance.lastResult : "Uploader not initialized.";

        /// <summary>Logs in the pending folder, including the current session.</summary>
        public static int PendingCount
        {
            get
            {
                try { return SessionUploadStore.Files(SessionDataLogger.LogDirectory).Length; }
                catch (Exception error) when (IsFileError(error)) { return -1; }
            }
        }

        public static void EnsureCreated()
        {
            if (instance == null) new GameObject("[SessionLogUploader]").AddComponent<SessionLogUploader>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            config = SessionUploadConfig.Load();
            if (config == null)
            {
                lastResult = "Upload disabled: no valid upload configuration.";
                Debug.LogWarning("[SessionLogUploader] " + lastResult, this);
            }
        }

        private IEnumerator Start()
        {
            // Logger bootstrap finishes before the startup pass, regardless of Start order.
            yield return null;
            if (config == null)
            {
                // Legacy receipts still migrate when uploads are disabled.
                busy = true;
                yield return UploadFiles();
            }
            else if (!attemptedUpload) UploadPending();
        }

        /// <summary>
        /// Starts a pass now, or right after the current one finishes. Unlike
        /// <see cref="UploadPending"/> it is never dropped while a pass is running.
        /// </summary>
        public static void RequestUpload()
        {
            if (!UploadPending() && instance != null && instance.config != null)
                instance.rerunWanted = true;
        }

        private void Update()
        {
            if (config == null || Time.unscaledTime < nextPeriodic) return;
            nextPeriodic = Time.unscaledTime + PeriodicSeconds;
            if (attemptedUpload) RequestUpload();
        }

        /// <summary>Starts an asynchronous pass; returns false if disabled or already busy.</summary>
        public static bool UploadPending()
        {
            EnsureCreated();
            if (instance.config == null || instance.busy) return false;
            instance.attemptedUpload = true;
            instance.busy = true;
            instance.StartCoroutine(instance.UploadFiles());
            return true;
        }

        private IEnumerator UploadFiles()
        {
            sent = 0;
            failed = 0;
            try
            {
                string[] files = null;
                try { files = SessionUploadStore.Files(SessionDataLogger.LogDirectory); }
                catch (Exception error) when (IsFileError(error)) { lastResult = "Unable to read pending session files."; }
                if (files == null) yield break;
                foreach (string path in files)
                {
                    yield return UploadFile(path);
                    // Includes skipped, migrated, oversized, and failed files.
                    yield return null;
                }
                lastResult = $"Uploaded {sent}; failed {failed}; pending {PendingCount}.";
            }
            finally
            {
                activeRequest = null;
                busy = false;
                nextPeriodic = Time.unscaledTime + PeriodicSeconds;
            }
            if (rerunWanted)
            {
                rerunWanted = false;
                UploadPending();
            }
        }

        private IEnumerator UploadFile(string path)
        {
            string hash = null, body = null;
            bool wasActive = SessionUploadStore.IsActive(path, SessionDataLogger.CurrentLogPath);
            try
            {
                string content = SessionUploadStore.Read(path);
                hash = SessionUploadStore.Hash(content);
                if (SessionUploadStore.MigrateLegacy(path, SessionDataLogger.CurrentLogPath, hash))
                {
                    // Only older builds write receipts beside pending logs. Validate
                    // the hash because a previously active log may have grown.
                    // Migration is complete; this file needs no network request.
                }
                else if (config != null && !(wasActive && path == acknowledgedActivePath && hash == acknowledgedActiveHash))
                    body = JsonUtility.ToJson(SessionUploadStore.Payload(path, content, config.token, SessionDeviceName.Current));
            }
            catch (SessionUploadStore.OversizedLogException)
            {
                try { SessionUploadStore.Archive(path, SessionDataLogger.CurrentLogPath, true); }
                catch (Exception error) when (IsFileError(error)) { failed++; }
            }
            catch (Exception error) when (IsFileError(error) || error is ArgumentException) { failed++; }
            if (body == null) yield break;

            lastResult = "Uploading " + Path.GetFileName(path);
            bool acknowledged;
            string response;
            string url = config.endpointUrl + (config.endpointUrl.Contains("?") ? "&" : "?") +
                "uploadRequest=" + Guid.NewGuid().ToString("N");
            using (var request = new UnityWebRequest(url, "POST"))
            {
                activeRequest = request;
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Cache-Control", "no-cache");
                request.timeout = 45;
                request.redirectLimit = 5;
                yield return request.SendWebRequest();
                response = request.downloadHandler.text;
                acknowledged = request.result == UnityWebRequest.Result.Success &&
                    SessionUploadStore.Acknowledged(response);
                activeRequest = null;
            }
            if (!acknowledged) { failed++; yield break; }
            try
            {
                if (wasActive)
                {
                    // It may have ended or grown while the request was in flight.
                    // Leave it pending for a complete final upload on the next pass.
                    acknowledgedActivePath = path;
                    acknowledgedActiveHash = hash;
                }
                else SessionUploadStore.ArchiveAcknowledged(path, SessionDataLogger.CurrentLogPath, response, hash);
                sent++;
            }
            catch (Exception error) when (IsFileError(error)) { failed++; }
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            activeRequest?.Abort();
            StopAllCoroutines();
            activeRequest?.Dispose();
            activeRequest = null;
            busy = false;
            instance = null;
        }

        private static bool IsFileError(Exception error) =>
            error is IOException || error is UnauthorizedAccessException || error is SecurityException;
    }
}
