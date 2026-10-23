using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PsycheVR.Kiosk
{
    /// <summary>
    /// Floats a world-space panel in front of the visitor the way the Meta Quest system menu does
    /// (TG-322): it appears at eye height straight ahead, stays put through small head movements, and
    /// glides back in front only after a large turn or step. It stays upright (turned about the
    /// vertical only), so looking down or tilting the head never drags it along.
    ///
    /// It never sits inside a wall, desk or screen: the spot ahead is swept for blockers every frame
    /// and the panel is pulled in to the clear distance, shrinking with it so it keeps the same size
    /// in view. A blocker closer than the panel re-centres it at once. Fixed scenery counts, and so does the
    /// spacecraft model, built or not (its pieces on the pedestal, the finished model floating): it is big
    /// enough to hide the card. Other props with a rigidbody (a book in the hand) and anything held are
    /// ignored, so they never push the card about.
    ///
    /// Put it on the canvas root (not under the camera) and call <see cref="Init"/>.
    /// </summary>
    public sealed class HeadFollow : MonoBehaviour
    {
        /// <summary>Re-centre once the panel is this far (degrees) to the side of where the visitor faces.</summary>
        private const float StartAngle = 30f;
        /// <summary>Stop re-centring once it is back within this (degrees).</summary>
        private const float StopAngle = 2f;
        /// <summary>Re-centre once the visitor has stepped this far (m) from where the panel expects them.</summary>
        private const float StartStep = 0.3f;
        /// <summary>Stop re-centring once the panel is this close (m) to its place.</summary>
        private const float StopStep = 0.02f;
        /// <summary>Seconds the glide back takes, roughly.</summary>
        private const float SmoothSeconds = 0.35f;
        /// <summary>Gap (m) kept between the panel and a blocker in front of it.</summary>
        private const float ClearancePadding = 0.12f;
        /// <summary>Closest (m) the panel ever comes, however tight the space.</summary>
        private const float MinDistance = 0.25f;

        private Transform _head;
        private float _distance;
        private float _sweepRadius;
        private Vector3 _baseScale;
        private bool _recentring;
        private Vector3 _velocity;
        private float _scaleVelocity;
        private float _scaleK = 1f;

        /// <summary>
        /// Starts following <paramref name="head"/>, placed straight ahead at once.
        /// </summary>
        /// <param name="head">The headset camera.</param>
        /// <param name="distance">Metres ahead when nothing is in the way.</param>
        /// <param name="sweepRadius">Half the panel's short side (m): blockers inside this are found.</param>
        public void Init(Transform head, float distance, float sweepRadius)
        {
            _head = head;
            _distance = distance;
            _sweepRadius = sweepRadius;
            _baseScale = transform.localScale;
            _scaleK = Clear(out Vector3 at) / _distance;
            transform.position = at;
            Face();
            transform.localScale = _baseScale * _scaleK;
        }

        private void LateUpdate()
        {
            if (_head == null) return;
            float clear = Clear(out Vector3 target);

            Vector3 flatToPanel = Flat(transform.position - _head.position);
            float panelDistance = flatToPanel.magnitude;
            float off = Vector3.Angle(flatToPanel, Forward());
            if (!_recentring && (off > StartAngle
                                 || Mathf.Abs(panelDistance - clear) > StartStep
                                 || Blocked(flatToPanel, panelDistance)))
                _recentring = true;

            if (_recentring)
            {
                transform.position = Vector3.SmoothDamp(transform.position, target, ref _velocity, SmoothSeconds);
                if (off < StopAngle && Vector3.Distance(transform.position, target) < StopStep)
                {
                    _recentring = false;
                    _velocity = Vector3.zero;
                }
            }

            _scaleK = Mathf.SmoothDamp(_scaleK, clear / _distance, ref _scaleVelocity, SmoothSeconds);
            transform.localScale = _baseScale * _scaleK;
            Face();
        }

        /// <summary>Turns the panel about the vertical to face the visitor (a canvas reads from its -Z side).</summary>
        private void Face()
        {
            Vector3 away = Flat(transform.position - _head.position);
            if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        private Vector3 Forward()
        {
            Vector3 f = Flat(_head.forward);
            // looking straight up or down: fall back to the head's up, which then points where the face does
            if (f.sqrMagnitude < 1e-4f) f = Flat(_head.forward.y > 0f ? -_head.up : _head.up);
            return f.normalized;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>True when something now stands between the visitor and the panel where it is.</summary>
        private bool Blocked(Vector3 flatToPanel, float panelDistance)
        {
            if (panelDistance < 1e-3f) return true;
            return ClearDistance(_head, _head.position, flatToPanel / panelDistance, panelDistance, _sweepRadius) < panelDistance - 0.01f;
        }

        /// <summary>Distance the panel can sit ahead at eye height, and that spot.</summary>
        private float Clear(out Vector3 at)
        {
            Vector3 dir = Forward();
            float d = ClearDistance(_head, _head.position, dir, _distance, _sweepRadius);
            at = _head.position + dir * d;
            return d;
        }

        /// <summary>
        /// Metres a panel can sit along <paramref name="direction"/> from <paramref name="origin"/>: the
        /// requested distance pulled in to the nearest blocker less a small gap. Hits on the visitor's own rig
        /// are skipped, since the sweep starts inside it, and so are props (see <see cref="IsBlocker"/>).
        /// </summary>
        public static float ClearDistance(Transform head, Vector3 origin, Vector3 direction, float requested, float sweepRadius)
        {
            float distance = requested;
            RaycastHit[] hits = Physics.SphereCastAll(origin, sweepRadius, direction, requested,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                // The rig must stay at the scene root: nesting it would make .root that container.
                if (hit.transform.IsChildOf(head.root) || !IsBlocker(hit.collider)) continue;
                distance = Mathf.Min(distance, hit.distance - ClearancePadding);
            }
            return Mathf.Clamp(distance, Mathf.Min(MinDistance, requested), requested);
        }

        /// <summary>
        /// True when <paramref name="c"/> should keep the card in front of it: scenery (no rigidbody) or a
        /// spacecraft piece that nobody is holding. Other props are left out so a book in the hand does not
        /// shove the card around.
        /// </summary>
        private static bool IsBlocker(Collider c)
        {
            if (c.attachedRigidbody == null) return true;
            if (c.GetComponentInParent<SnappableObject>() == null) return false;
            var grab = c.GetComponentInParent<XRBaseInteractable>();
            return grab == null || !grab.isSelected;
        }
    }
}
