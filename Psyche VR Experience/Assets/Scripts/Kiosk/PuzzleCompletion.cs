using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PsycheVR.Kiosk
{
    /// <summary>
    /// Watches the scene's spacecraft puzzles and raises <see cref="Completed"/> the first
    /// time every zone of any one puzzle has accepted its piece. Zones are grouped into
    /// puzzles by the instruction board they report to (each Snappable puzzle has its own),
    /// so the story room's copy of the puzzle, which stays loaded in Event mode, never holds
    /// the event room's completion back.
    /// Two kinds of zone are left out rather than counted against completion: a zone that is
    /// inactive at Start (for example under a ModeGate that is off for this mode), and a zone
    /// with no required tag, which can never accept a piece (the Snappable prefab carries one,
    /// Bus_Snap_Zone).
    /// Fires once per scene load; the reload a mode switch or kiosk reset performs starts
    /// it fresh.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PuzzleCompletion : MonoBehaviour
    {
        private const string LogPrefix = "[PuzzleCompletion]";

        [Tooltip("Invoked once when the last snap zone of any one puzzle is filled.")]
        [SerializeField] private UnityEvent onCompleted = new UnityEvent();

        private SnapZone[][] _puzzles = Array.Empty<SnapZone[]>();

        /// <summary>Raised once when every zone of one puzzle has snapped.</summary>
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
            // No-ops before Start, when _puzzles is still empty.
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
            var groups = new Dictionary<UnityEngine.Object, List<SnapZone>>();
            int inactive = 0;
            int untagged = 0;
            foreach (var zone in found)
            {
                if (zone == null || !zone.gameObject.activeInHierarchy)
                {
                    inactive++;
                    continue;
                }
                if (string.IsNullOrEmpty(zone.requiredTag))
                {
                    untagged++;
                    continue;
                }

                UnityEngine.Object key = zone.textManager != null ? zone.textManager : zone.transform.root;
                if (!groups.TryGetValue(key, out var zones))
                    groups.Add(key, zones = new List<SnapZone>());
                zones.Add(zone);
            }

            _puzzles = new SnapZone[groups.Count][];
            int index = 0;
            foreach (var zones in groups.Values)
                _puzzles[index++] = zones.ToArray();

            if (_puzzles.Length == 0)
            {
                Debug.LogWarning($"{LogPrefix} No usable SnapZone in '{gameObject.scene.name}' ({inactive} inactive, {untagged} without a required tag); the puzzle can never complete here.");
                return;
            }

            Debug.Log($"{LogPrefix} Watching {_puzzles.Length} puzzle(s) of {string.Join(", ", Array.ConvertAll(_puzzles, p => p.Length.ToString()))} zones ({inactive} inactive and {untagged} untagged zones skipped).");
            CheckCompletion();
        }

        /// <summary>
        /// Handles a snap from any zone in the scene. Zones this watcher does not track
        /// (spawned late, gated away for this mode, or untagged) are ignored.
        /// </summary>
        private void HandleSnapped(SnapZone zone)
        {
            foreach (var puzzle in _puzzles)
            {
                if (Array.IndexOf(puzzle, zone) >= 0)
                {
                    CheckCompletion();
                    return;
                }
            }
        }

        private void CheckCompletion()
        {
            if (IsComplete)
                return;

            foreach (var puzzle in _puzzles)
            {
                if (!IsSolved(puzzle))
                    continue;

                IsComplete = true;
                Debug.Log($"{LogPrefix} Puzzle complete ({puzzle.Length} zones).");
                Completed?.Invoke();
                onCompleted.Invoke();
                return;
            }
        }

        private static bool IsSolved(SnapZone[] puzzle)
        {
            foreach (var zone in puzzle)
            {
                // A destroyed zone means this puzzle cannot complete this session:
                // fail closed on purpose rather than treat it as satisfied.
                if (zone == null || !zone.hasSnapped)
                    return false;
            }
            return true;
        }
    }
}
