using UnityEngine;

namespace PsycheVR.Modes
{
    /// <summary>
    /// Build-level mode configuration. One asset lives at
    /// <c>Assets/Resources/GameModeConfig.asset</c> and is loaded by
    /// <see cref="GameModeManager"/> at startup. The editor's Tools &gt; Build APK
    /// menu (GameModeBuilder) writes the flavor's mode here for the length of one
    /// build and restores it afterwards; nothing else reads this asset.
    /// </summary>
    [CreateAssetMenu(fileName = GameModeConfig.AssetName, menuName = "Psyche VR/Game Mode Config")]
    public sealed class GameModeConfig : ScriptableObject
    {
        /// <summary>Resource name the manager loads. Must match the asset file name.</summary>
        public const string AssetName = "GameModeConfig";

        [Tooltip("Mode the application boots into.")]
        [SerializeField] private GameMode defaultMode = GameMode.Event;

        /// <summary>Mode the application boots into.</summary>
        public GameMode DefaultMode => defaultMode;
    }
}
