using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PsycheVR.Kiosk
{
    /// <summary>
    /// Watches every active <see cref="SnapZone"/> in the scene and raises <see cref="Completed"/>
    /// once each one has accepted its piece. A zone that is inactive at Start (for example
    /// under a ModeGate that is off for this mode) is left out of the set, not counted against it.
    /// Fires once per scene load; the reload a mode switch or kiosk reset performs starts
    /// it fresh.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PuzzleCompletion : MonoBehaviour
    {
        private const string LogPrefix = "[PuzzleCompletion]";

        [Tooltip("Invoked once when the last snap zone is filled.")]
        [SerializeField] private UnityEvent onCompleted = new UnityEvent();

        private SnapZone[] _zones = Array.Empty<SnapZone>();

        /// <summary>Raised once when every snap zone has snapped.</summary>
        public event Action Completed;

        /// <summary>
        /// True after <see cref="Completed"/> has fired. A subscriber that wires up after
        /// <c>Start</c> must check this, because the event is raised once and never replayed.
        /// </summary>
        public bool IsComplete { get; private set; }

        private void OnEnable()
        {
            SnapZone.Snapped += HandleSnapped;

            // Catch up on any snap that landed while this component was disabled.
            // No-ops before Start, when _zones is still empty.
            CheckCompletion();
        }

        private void OnDisable()
        {
            SnapZone.Snapped -= HandleSnapped;
        }

        private void Start()
        {
            // Include inactive so the four zones that start with their component disabled
            // are still found, then drop zones whose GameObject is deactivated: a zone
            // under a ModeGate that is off for this mode can never snap, and counting it
            // would make completion unreachable with no log line saying why.
            SnapZone[] found = FindObjectsByType<SnapZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<SnapZone> active = new List<SnapZone>(found.Length);
            int skipped = 0;
            foreach (var zone in found)
            {
                if (zone != null && zone.gameObject.activeInHierarchy)
                    active.Add(zone);
                else
                    skipped++;
            }

            _zones = active.ToArray();
            if (_zones.Length == 0)
            {
                Debug.LogWarning($"{LogPrefix} No active SnapZone in '{gameObject.scene.name}' ({skipped} inactive); the puzzle can never complete here.");
                return;
            }

            Debug.Log(skipped > 0
                ? $"{LogPrefix} Watching {_zones.Length} snap zones ({skipped} skipped as inactive)."
                : $"{LogPrefix} Watching {_zones.Length} snap zones.");
            CheckCompletion();
        }

        /// <summary>
        /// Handles a snap from any zone in the scene. Zones this watcher does not track
        /// (spawned late, or gated away for this mode) are ignored.
        /// </summary>
        private void HandleSnapped(SnapZone zone)
        {
            if (Array.IndexOf(_zones, zone) < 0)
                return;

            CheckCompletion();
        }

        private void CheckCompletion()
        {
            if (IsComplete || _zones.Length == 0)
                return;

            foreach (var zone in _zones)
            {
                // A destroyed zone means the puzzle cannot complete this session:
                // fail closed on purpose rather than treat it as satisfied.
                if (zone == null || !zone.hasSnapped)
                    return;
            }

            IsComplete = true;
            Debug.Log($"{LogPrefix} Puzzle complete.");
            Completed?.Invoke();
            onCompleted.Invoke();
        }
    }
}
