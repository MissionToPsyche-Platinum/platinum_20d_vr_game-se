using System;
using System.IO;
using PsycheVR.Data;
using UnityEditor;
using UnityEngine;

namespace PsycheVR.Modes.Editor
{
    public static class SessionUploadPreparation
    {
        public const string UploadConfigPath = "Assets/Resources/SessionUploadConfig.json";
        public const string LocalConfigPath = "UserSettings/SessionUpload.local.json";

        /// <summary>Also allows editor testing with the same local configuration as an APK.</summary>
        [MenuItem("Tools/Session Logs/Refresh Local Upload Configuration")]
        public static void RefreshUploadConfig()
        {
            SessionUploadConfig config = null;
            if (File.Exists(LocalConfigPath))
            {
                try { config = JsonUtility.FromJson<SessionUploadConfig>(File.ReadAllText(LocalConfigPath)); }
                catch (Exception error) when (error is ArgumentException || error is IOException || error is UnauthorizedAccessException)
                { /* Never include file contents or parser messages containing credentials. */ }
            }
            if (config == null || !config.IsValid)
            {
                if (File.Exists(UploadConfigPath) && !AssetDatabase.DeleteAsset(UploadConfigPath))
                    throw new IOException("Cannot remove stale upload configuration; build cancelled.");
                Debug.LogWarning("[GameModeBuilder] Upload disabled: missing or invalid UserSettings/SessionUpload.local.json.");
                return;
            }
            Directory.CreateDirectory("Assets/Resources");
            File.WriteAllText(UploadConfigPath, JsonUtility.ToJson(config, true));
            AssetDatabase.ImportAsset(UploadConfigPath, ImportAssetOptions.ForceSynchronousImport);
        }

    }
}
