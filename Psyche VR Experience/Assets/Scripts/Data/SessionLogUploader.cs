using System;
using System.Collections;
using System.IO;
using System.Security;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace PsycheVR.Data
{
    /// <summary>One attempt per pending snapshot on demand, and one pass at startup.</summary>
    public sealed class SessionLogUploader : MonoBehaviour
    {
        private static SessionLogUploader instance;
        private SessionUploadConfig config;
        private bool busy;
        private bool attemptedUpload;
        private string lastResult = "Not uploaded yet.";
        private UnityWebRequest activeRequest;

        public static bool IsUploading => instance != null && instance.busy;
        public static bool IsEnabled => instance != null && instance.config != null;
        public static string LastResult => instance != null ? instance.lastResult : "Uploader not initialized.";

        /// <summary>Includes an active file whose content changed since its last acknowledgment.</summary>
        public static int PendingCount
        {
            get
            {
                try
                {
                    int count = 0;
                    foreach (string path in SessionUploadStore.Files(SessionDataLogger.LogDirectory))
                    {
                        try
                        {
                            if (!SessionUploadStore.IsSent(path, SessionUploadStore.Hash(SessionUploadStore.Read(path)))) count++;
                        }
                        catch (Exception error) when (IsFileError(error)) { count++; }
                    }
                    return count;
                }
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
            if (!attemptedUpload) UploadPending();
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
            int sent = 0, failed = 0;
            try
            {
                string[] files = null;
                try { files = SessionUploadStore.Files(SessionDataLogger.LogDirectory); }
                catch (Exception error) when (IsFileError(error)) { lastResult = "Unable to read pending session files."; }
                if (files == null) yield break;
                foreach (string path in files)
                {
                    string hash = null, body = null;
                    try
                    {
                        string content = SessionUploadStore.Read(path);
                        hash = SessionUploadStore.Hash(content);
                        if (!SessionUploadStore.IsSent(path, hash))
                            body = JsonUtility.ToJson(SessionUploadStore.Payload(path, content, config.token, SessionDeviceName.Current));
                    }
                    catch (Exception error) when (IsFileError(error) || error is ArgumentException)
                    { failed++; }
                    if (body == null) continue;

                    lastResult = "Uploading " + Path.GetFileName(path);
                    bool acknowledged = false;
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
                        acknowledged = request.result == UnityWebRequest.Result.Success &&
                            SessionUploadStore.Acknowledged(request.downloadHandler.text);
                        activeRequest = null;
                    }
                    if (!acknowledged) { failed++; continue; }
                    try { SessionUploadStore.MarkSent(path, hash); sent++; }
                    catch (Exception error) when (IsFileError(error)) { failed++; }
                }
                lastResult = $"Uploaded {sent}; failed {failed}; pending {PendingCount}.";
            }
            finally { activeRequest = null; busy = false; }
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
