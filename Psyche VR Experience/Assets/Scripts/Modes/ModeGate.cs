using System;
using UnityEngine;

namespace PsycheVR.Modes
{
    /// <summary>
    /// Keeps this GameObject active only in the listed modes. In any other mode it is
    /// deactivated on Awake, before any Start runs, so for that session the object and
    /// everything under it are inactive. They are still in the hierarchy: a
    /// FindObjectsByType call that includes inactive objects will still see them.
    ///
    /// The gate runs in every scene that contains it, because GameModeManager boots in
    /// any scene. The mission-control work scenes therefore also lose the launch station
    /// while the default mode is Event; they are authoring husks slated for removal.
    ///
    /// Used to hide the launch button station in Event mode (the kiosk ends with the
    /// video auto-playing) and to hide the kiosk systems in Story mode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModeGate : MonoBehaviour
    {
        private const string LogPrefix = "[ModeGate]";

        [Tooltip("Modes in which this object stays active. In every other mode it is deactivated on Awake.")]
        [SerializeField] private GameMode[] activeIn = { GameMode.Story };

        private void Awake()
        {
            if (activeIn == null || activeIn.Length == 0)
            {
                Debug.LogWarning($"{LogPrefix} '{name}' lists no modes; hidden in every mode.", this);
                gameObject.SetActive(false);
                return;
            }

            if (Array.IndexOf(activeIn, GameModeManager.ActiveMode) >= 0)
                return;

            Debug.Log($"{LogPrefix} '{name}' is not for {GameModeManager.ActiveMode} mode; deactivating.", this);
            gameObject.SetActive(false);
        }
    }
}
