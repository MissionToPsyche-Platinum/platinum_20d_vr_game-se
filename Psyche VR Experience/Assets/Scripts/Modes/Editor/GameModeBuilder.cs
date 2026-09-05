using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PsycheVR.Modes.Editor
{
    /// <summary>
    /// Builds the two Android flavors of the game, one per <see cref="GameMode"/>.
    /// Each build temporarily writes the flavor's mode into
    /// <c>Assets/Resources/GameModeConfig.asset</c>, builds the master scene as the
    /// only scene, and then restores the asset, so the checked-in default never changes.
    ///
    /// Menu: Tools &gt; Build APK. Command line:
    /// <c>Unity -batchmode -quit -buildTarget Android -projectPath &lt;project&gt;
    /// -executeMethod PsycheVR.Modes.Editor.GameModeBuilder.BuildBoth</c>.
    /// </summary>
    public static class GameModeBuilder
    {
        private const string LogPrefix = "[GameModeBuilder]";
        private const string MenuRoot = "Tools/Build APK/";

        /// <summary>Asset the runtime reads its default mode from.</summary>
        private const string ConfigAssetPath = "Assets/Resources/" + GameModeConfig.AssetName + ".asset";

        /// <summary>Serialized field on <see cref="GameModeConfig"/> that holds the default.</summary>
        private const string DefaultModeProperty = "defaultMode";

        /// <summary>The only scene shipped in either flavor.</summary>
        private const string MasterScenePath = "Assets/Scenes/" + GameModeManager.MasterSceneName + ".unity";

        /// <summary>Output folder, relative to the repository root (one level above Assets).</summary>
        private const string OutputFolder = "Builds/Android";

        private const string OutputFilePrefix = "PsycheVR-";
        private const string OutputFileExtension = ".apk";

        private const int ExitCodeFailure = 1;

        [MenuItem(MenuRoot + "Event")]
        public static void BuildEvent() => BuildFlavors(GameMode.Event);

        [MenuItem(MenuRoot + "Story")]
        public static void BuildStory() => BuildFlavors(GameMode.Story);

        [MenuItem(MenuRoot + "Both")]
        public static void BuildBoth() => BuildFlavors(GameMode.Event, GameMode.Story);

        /// <summary>Path the APK for <paramref name="mode"/> is written to.</summary>
        public static string OutputPathFor(GameMode mode)
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            return Path.Combine(repoRoot, OutputFolder, OutputFilePrefix + mode + OutputFileExtension);
        }

        private static void BuildFlavors(params GameMode[] modes)
        {
            var allSucceeded = true;
            foreach (var mode in modes)
            {
                if (!Build(mode))
                {
                    allSucceeded = false;
                    break;
                }
            }

            if (Application.isBatchMode && !allSucceeded)
                EditorApplication.Exit(ExitCodeFailure);
        }

        /// <summary>
        /// Builds one flavor. Returns true when the player build succeeded. The config
        /// asset is restored to its previous default whether or not the build succeeds.
        /// The asset is re-loaded by path for every read and write: a player build
        /// refreshes the asset database and destroys any object reference held across it.
        /// </summary>
        private static bool Build(GameMode mode)
        {
            if (!File.Exists(MasterScenePath))
            {
                Debug.LogError($"{LogPrefix} Master scene not found at '{MasterScenePath}'.");
                return false;
            }

            if (!TryReadDefaultMode(out var originalMode))
                return false;

            var outputPath = OutputPathFor(mode);

            try
            {
                if (!TryWriteDefaultMode(mode))
                    return false;

                Debug.Log($"{LogPrefix} Building {mode} flavor -> {outputPath}");

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { MasterScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                var report = BuildPipeline.BuildPlayer(options);
                return ReportResult(mode, report, outputPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} {mode} build threw: {ex}");
                return false;
            }
            finally
            {
                if (!TryWriteDefaultMode(originalMode))
                    Debug.LogError($"{LogPrefix} Could not restore {ConfigAssetPath} to {originalMode}; revert it with git before committing.");
            }
        }

        private static bool ReportResult(GameMode mode, BuildReport report, string outputPath)
        {
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                var apkBytes = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0L;
                Debug.Log($"{LogPrefix} {mode} flavor built: {outputPath} ({apkBytes / (1024 * 1024)} MB, {summary.totalTime:mm\\:ss}).");
                return true;
            }

            Debug.LogError($"{LogPrefix} {mode} flavor {summary.result}: {summary.totalErrors} error(s). See the editor log.");
            return false;
        }

        private static bool TryReadDefaultMode(out GameMode mode)
        {
            mode = GameModeManager.FallbackMode;
            var config = LoadConfig();
            if (config == null)
                return false;

            mode = (GameMode)new SerializedObject(config).FindProperty(DefaultModeProperty).enumValueIndex;
            return true;
        }

        private static bool TryWriteDefaultMode(GameMode mode)
        {
            var config = LoadConfig();
            if (config == null)
                return false;

            var serialized = new SerializedObject(config);
            var property = serialized.FindProperty(DefaultModeProperty);
            if (property.enumValueIndex == (int)mode)
                return true;

            property.enumValueIndex = (int)mode;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            return true;
        }

        private static GameModeConfig LoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameModeConfig>(ConfigAssetPath);
            if (config == null)
                Debug.LogError($"{LogPrefix} Config asset not found at '{ConfigAssetPath}'.");
            return config;
        }
    }
}
