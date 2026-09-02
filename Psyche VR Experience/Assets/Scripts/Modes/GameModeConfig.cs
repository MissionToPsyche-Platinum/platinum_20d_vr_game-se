using UnityEngine;

namespace PsycheVR.Modes
{
    /// <summary>
    /// Build-level mode configuration. One asset lives at
    /// <c>Assets/Resources/GameModeConfig.asset</c> and is loaded by
    /// <see cref="GameModeManager"/> at startup. The build-flavor task selects
    /// the default per APK; nothing else reads this asset.
    /// </summary>
    [CreateAssetMenu(fileName = GameModeConfig.AssetName, menuName = "Psyche VR/Game Mode Config")]
    public sealed class GameModeConfig : ScriptableObject
    {
        /// <summary>Resource name the manager loads. Must match the asset file name.</summary>
        public const string AssetName = "GameModeConfig";

        [Tooltip("Mode the application boots into. Event is the kiosk (NASA) flavor.")]
        [SerializeField] private GameMode defaultMode = GameMode.Event;

        /// <summary>Mode the application boots into.</summary>
        public GameMode DefaultMode => defaultMode;
    }
}
