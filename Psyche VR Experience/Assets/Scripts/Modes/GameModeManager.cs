using UnityEngine;
using UnityEngine.SceneManagement;

namespace PsycheVR.Modes
{
    /// <summary>
    /// Persistent owner of the active <see cref="GameMode"/>.
    /// Created by code before the first scene loads, so no scene has to contain it
    /// and pressing Play in any scene works. It is the only script that stores mode
    /// state; the few systems that care (currently <see cref="ModeSpawnPlacer"/>)
    /// read <see cref="ActiveMode"/>.
    ///
    /// A mode switch is a full reload of the master scene, which resets every
    /// scene object to its saved state. That reload is the whole point: an admin
    /// moving from Event to Story must get Story exactly as a cold boot would.
    /// </summary>
    public sealed class GameModeManager : MonoBehaviour
    {
        /// <summary>The single scene every mode runs in.</summary>
        public const string MasterSceneName = "Bedroom";

        /// <summary>Mode used when the config asset cannot be loaded.</summary>
        public const GameMode FallbackMode = GameMode.Event;

        private const string LogPrefix = "[GameModeManager]";
        private const string RuntimeObjectName = "[GameModeManager]";

        private static GameModeManager instance;

        /// <summary>The mode the master scene is currently running in.</summary>
        public static GameMode ActiveMode { get; private set; } = FallbackMode;

        /// <summary>True when <see cref="ActiveMode"/> is <see cref="GameMode.Story"/>.</summary>
        public static bool IsStory => ActiveMode == GameMode.Story;

        /// <summary>
        /// Creates the persistent manager before the first scene loads and reads the
        /// default mode from <see cref="GameModeConfig"/>. Runs in builds and in the
        /// editor alike, whatever scene Play starts in.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
                return;

            var go = new GameObject(RuntimeObjectName);
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GameModeManager>();

            ActiveMode = ReadDefaultMode();
            Debug.Log($"{LogPrefix} Booting in {ActiveMode} mode.");
        }

        /// <summary>
        /// Sets the active mode and reloads the master scene so the target mode starts
        /// from a clean slate. Switching to the current mode is a plain reset.
        /// </summary>
        /// <param name="mode">Mode to start.</param>
        public static void SwitchTo(GameMode mode)
        {
            if (!Application.CanStreamedLevelBeLoaded(MasterSceneName))
            {
                Debug.LogError($"{LogPrefix} Cannot switch to {mode}: scene '{MasterSceneName}' is not in Build Settings.");
                return;
            }

            Debug.Log($"{LogPrefix} Switching {ActiveMode} -> {mode}; reloading '{MasterSceneName}'.");
            ActiveMode = mode;
            SceneManager.LoadScene(MasterSceneName, LoadSceneMode.Single);
        }

        private static GameMode ReadDefaultMode()
        {
            var config = Resources.Load<GameModeConfig>(GameModeConfig.AssetName);
            if (config != null)
                return config.DefaultMode;

            Debug.LogError($"{LogPrefix} Resources/{GameModeConfig.AssetName}.asset not found; defaulting to {FallbackMode}.");
            return FallbackMode;
        }

        private void Awake()
        {
            // Guard against a copy dropped into a scene by hand.
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"{LogPrefix} Duplicate on '{gameObject.name}' destroyed; the bootstrapped instance owns mode state.");
                Destroy(gameObject);
            }
        }
    }
}
