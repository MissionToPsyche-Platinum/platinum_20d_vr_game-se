using System.Collections.Generic;
using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Finds open hands touching a set of colliders and reports where each one touches and how fast it moves,
    /// so a slap can spin something. Call <see cref="Poll"/> once per physics step. Touches are found by polling
    /// the hand colliders (layer <see cref="HandPhysics.LayerName"/>) against the parts, because the parts are
    /// kinematic and so are the hands: no collision events fire between them. A hand's speed is its movement
    /// since the last step it was near, so a hand that comes back after leaving does not count the gap as speed.
    /// </summary>
    public sealed class HandPushProbe
    {
        /// <summary>Receives one touching hand: its centre and its velocity (m/s).</summary>
        public delegate void Touch(Vector3 at, Vector3 velocity);

        private int _handMask = -1;   // looked up on the first poll: Unity forbids layer lookups while a component is constructed
        private readonly Collider[] _nearby = new Collider[8];
        private readonly Dictionary<Collider, Vector3> _lastAt = new Dictionary<Collider, Vector3>();
        private readonly HashSet<Collider> _seen = new HashSet<Collider>();
        private readonly List<Collider> _gone = new List<Collider>();

        /// <summary>
        /// Reports every hand touching <paramref name="parts"/> this step to <paramref name="onTouch"/>.
        /// Hands within <paramref name="reach"/> (m) of the parts' bounds are tracked so their speed is known
        /// on the step they touch.
        /// </summary>
        public void Poll(IReadOnlyList<Collider> parts, float reach, Touch onTouch)
        {
            _seen.Clear();
            if (_handMask < 0) _handMask = LayerMask.GetMask(HandPhysics.LayerName);
            if (_handMask != 0 && parts != null && Bounds(parts, out Bounds b))
            {
                int n = Physics.OverlapBoxNonAlloc(b.center, b.extents + Vector3.one * reach, _nearby,
                    Quaternion.identity, _handMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var hand = _nearby[i];
                    _seen.Add(hand);
                    Vector3 at = hand.bounds.center;
                    bool known = _lastAt.TryGetValue(hand, out Vector3 before);
                    _lastAt[hand] = at;
                    if (known && Touching(hand, parts))
                        onTouch(at, (at - before) / Time.fixedDeltaTime);
                }
            }
            if (_lastAt.Count == _seen.Count) return;
            _gone.Clear();
            foreach (var c in _lastAt.Keys) if (!_seen.Contains(c)) _gone.Add(c);
            foreach (var c in _gone) _lastAt.Remove(c);
        }

        private static bool Touching(Collider hand, IReadOnlyList<Collider> parts)
        {
            foreach (var part in parts)
            {
                if (part == null || !part.enabled) continue;
                if (Physics.ComputePenetration(hand, hand.transform.position, hand.transform.rotation,
                        part, part.transform.position, part.transform.rotation, out _, out _))
                    return true;
            }
            return false;
        }

        private static bool Bounds(IReadOnlyList<Collider> parts, out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (var part in parts)
            {
                if (part == null || !part.enabled) continue;
                if (!any) { b = part.bounds; any = true; } else b.Encapsulate(part.bounds);
            }
            return any;
        }
    }
}
