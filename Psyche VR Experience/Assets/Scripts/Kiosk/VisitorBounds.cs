using Unity.XR.CoreUtils;
using UnityEngine;
using PsycheVR.VR;

namespace PsycheVR.Kiosk
{
    /// <summary>
    /// Keeps the visitor's view inside the Event room (TG-289). The room is closed on every side by
    /// colliders, the open window included (Glass_Catch, on the Ignore Raycast layer so it never stops a
    /// grab ray), but a visitor walks for real, so nothing physical stops the headset going through a
    /// wall, into the desk or out of the window.
    ///
    /// Every frame the head is swept, as a small sphere, from the last spot known to be inside the room
    /// to where the headset now is, sliding along anything solid in the way. The view fades as the
    /// headset gets further past that spot (TG-290), and the rig is shifted sideways by the same
    /// distance so the view stays at it (TG-291): walking or leaning into a wall stops the view at the
    /// wall, and stepping back clears it at once. The shift is sideways only, so the rig never leaves
    /// the floor and gravity has nothing to fight; leaning down into the desk only fades.
    ///
    /// Only scenery counts (colliders without a rigidbody): props, puzzle pieces and the hands never
    /// hold the head back, so hands still knock things over as before. Sweeping from the last clear spot,
    /// not testing where the head is now, means a fast step can never carry it through a thin wall.
    ///
    /// Lives on the Event room under a <see cref="Modes.ModeGate"/> for Event mode; Story mode keeps
    /// its own locomotion and is untouched.
    /// </summary>
    [DefaultExecutionOrder(-50)] // before HeadFollow, so the cards place themselves from the shifted head
    public sealed class VisitorBounds : MonoBehaviour
    {
        /// <summary>Radius (m) of the sphere swept for the head: keeps the camera's near plane out of walls.</summary>
        private const float HeadRadius = 0.1f;
        /// <summary>Gap (m) kept between the head sphere and what it touches.</summary>
        private const float Skin = 0.01f;
        /// <summary>Slides along surfaces per frame, enough for a corner.</summary>
        private const int SlideSteps = 3;
        /// <summary>Metres past the last clear spot where the fade begins.</summary>
        private const float FadeStart = 0.02f;
        /// <summary>Metres past the last clear spot where the view is black.</summary>
        private const float FadeFull = 0.25f;
        /// <summary>A head move larger than this (m) in one frame is a placement or a recentre, not a step.</summary>
        private const float JumpDistance = 1f;
        private const float AlphaEpsilon = 0.001f;

        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly Collider[] _overlaps = new Collider[32];
        private Transform _rig;
        private Transform _head;
        private BlinkFadeOverlay _fade;
        private int _mask;
        private bool _seeded;
        private Vector3 _clear;         // the last head position known to be inside the room
        private Vector3 _lastRaw;
        private Vector3 _applied;       // the sideways shift this component has added to the rig
        private Vector3 _expectedRig;   // where it left the rig, to notice anything else moving it
        private float _alpha = -1f;

        private void Awake()
        {
            _mask = Physics.DefaultRaycastLayers | 1 << LayerMask.NameToLayer("Ignore Raycast");
        }

        private void OnDisable()
        {
            SetFade(0f);
        }

        private void LateUpdate()
        {
            if (!Find()) return;

            if ((_rig.position - _expectedRig).sqrMagnitude > 1e-8f) _applied = Vector3.zero; // placed by someone else
            Vector3 raw = _head.position - _applied; // where the head would be without the shift
            if (!_seeded || (raw - _lastRaw).sqrMagnitude > JumpDistance * JumpDistance)
            {
                _clear = raw;
                _seeded = true;
            }
            else
            {
                _clear = Slide(_clear, raw);
            }
            _lastRaw = raw;

            Vector3 shift = _clear - raw;
            shift.y = 0f;
            _rig.position += shift - _applied;
            _applied = shift;
            _expectedRig = _rig.position;

            SetFade(Mathf.InverseLerp(FadeStart, FadeFull, Vector3.Distance(raw, _clear)));
        }

        private bool Find()
        {
            if (_head != null && _rig != null) return true;
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null) return false;
            _rig = origin.Origin != null ? origin.Origin.transform : origin.transform;
            _head = origin.Camera.transform;
            _fade = _head.GetComponentInChildren<BlinkFadeOverlay>(true);
            _expectedRig = _rig.position;
            if (_fade == null) Debug.LogWarning("[VisitorBounds] no BlinkFadeOverlay under the camera; the view will not fade.", this);
            return true;
        }

        /// <summary>Moves a head sphere from <paramref name="from"/> toward <paramref name="to"/>, sliding along scenery.</summary>
        /// <remarks>
        /// A step that would end overlapping scenery (a slide into a corner) is refused, so the clear spot
        /// never ends up inside a wall. A spot that starts inside one (a visitor placed in the desk) moves freely
        /// until it is out.
        /// </remarks>
        private Vector3 Slide(Vector3 from, Vector3 to)
        {
            bool startInside = Overlaps(from);
            Vector3 pos = from, move = to - from;
            for (int i = 0; i < SlideSteps; i++)
            {
                float length = move.magnitude;
                if (length < 1e-5f) break;
                Vector3 dir = move / length;
                bool blocked = Nearest(pos, dir, length + Skin, out RaycastHit hit);
                float travel = blocked ? Mathf.Max(0f, hit.distance - Skin) : length;
                Vector3 next = pos + dir * travel;
                if (!startInside && Overlaps(next)) break;
                pos = next;
                if (!blocked) break;
                move = Vector3.ProjectOnPlane(move - dir * travel, hit.normal);
            }
            return pos;
        }

        /// <summary>True when a head sphere at <paramref name="at"/> overlaps scenery.</summary>
        private bool Overlaps(Vector3 at)
        {
            int count = Physics.OverlapSphereNonAlloc(at, HeadRadius, _overlaps, _mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (IsScenery(_overlaps[i])) return true;
            return false;
        }

        /// <summary>Only colliders without a rigidbody, and not the visitor's own rig, hold the head back.</summary>
        private bool IsScenery(Collider c) => c.attachedRigidbody == null && !c.transform.IsChildOf(_rig);

        /// <summary>The nearest piece of scenery the head sphere meets along the move, if any.</summary>
        private bool Nearest(Vector3 origin, Vector3 dir, float distance, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            int count = Physics.SphereCastNonAlloc(origin, HeadRadius, dir, _hits, distance, _mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                // already overlapping at the start (distance 0): skipped, so a head that starts inside can get out
                if (hit.distance <= 0f) continue;
                if (!IsScenery(hit.collider)) continue;
                if (!found || hit.distance < nearest.distance) { nearest = hit; found = true; }
            }
            return found;
        }

        private void SetFade(float alpha)
        {
            if (_fade == null || Mathf.Abs(alpha - _alpha) < AlphaEpsilon) return;
            _alpha = alpha;
            _fade.SetAlpha(alpha);
        }
    }
}
