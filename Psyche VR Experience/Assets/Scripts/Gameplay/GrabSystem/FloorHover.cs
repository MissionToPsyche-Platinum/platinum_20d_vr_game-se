using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Job Simulator style float for props on the floor: a grabbable lying on the floor rises to
    /// <see cref="GrabSettings.HoverHeight"/>, bobs, and waits to be grabbed by hand. A free hand
    /// pointing or reaching at it lifts it: it lies on a thick ray out of the fingers,
    /// <see cref="GrabSettings.HoverHandReach"/> long and <see cref="GrabSettings.HoverHandRayRadius"/>
    /// in radius, or is already in grab range. Once the hand points away it lingers for
    /// <see cref="GrabSettings.HoverLingerSeconds"/>, then settles back down, as fast as it rose, and
    /// returns to physics.
    /// The object is moved kinematically, never by forces, so nothing fights the solver.
    /// Grabbing ends the hover through <see cref="EndHover"/>, called by PsycheGrabbable before XRI
    /// records the Rigidbody state. One on the rig's Main Camera.
    /// </summary>
    public class FloorHover : MonoBehaviour
    {
        [Tooltip("Shared grab settings asset (the Floor Hover section).")]
        [SerializeField] private GrabSettings grabSettings;

        /// <summary>Seconds between refreshes of the grabbable list. The hand check itself runs every frame.</summary>
        private const float ScanInterval = 1f;

        /// <summary>Faster than this (m/s) an object is falling, bouncing or rolling, not lying on the floor.</summary>
        private const float RestSpeed = 0.2f;

        /// <summary>
        /// Seconds an object must stay under <see cref="RestSpeed"/> before it is on the floor: a
        /// bounce passes through zero speed at its top (a crumpled ball floated from 20 cm up).
        /// </summary>
        private const float RestSeconds = 0.3f;

        /// <summary>The object's lowest point must be this close (m) to its support: touching, not above it mid-bounce.</summary>
        private const float TouchGap = 0.01f;

        /// <summary>The support ray starts this far (m) above the centre of mass.</summary>
        private const float SupportRayLift = 0.05f;

        private readonly RaycastHit[] _supportHits = new RaycastHit[8];
        private readonly Dictionary<PsycheGrabbable, float> _restSince = new Dictionary<PsycheGrabbable, float>();

        /// <summary>What an object rests on must be within this height (m) of the floor: shelves and furniture are not.</summary>
        private const float FloorTolerance = 0.03f;

        /// <summary>Extra margin (m) on the settle sweep so the object stops just short of a surface.</summary>
        private const float SweepSkin = 0.005f;

        private enum Phase { Rising, Hovering, Settling }

        private class Hover
        {
            public PsycheGrabbable Grabbable;
            public Rigidbody Body;
            public Vector3 RestPosition;
            /// <summary>Centre of mass where the object lay. The hand ray is also tested here, so rising out of the ray does not drop it again.</summary>
            public Vector3 RestCentre;
            /// <summary>Hover height for the Rigidbody position, so the centre of mass (not the pivot) reaches HoverHeight.</summary>
            public float TopY;
            public float FromY;
            public float PhaseStart;
            public float LastTriggered;
            public Phase Phase;
        }

        private static readonly List<FloorHover> s_Active = new List<FloorHover>();

        private readonly Dictionary<PsycheGrabbable, Hover> _hovers = new Dictionary<PsycheGrabbable, Hover>();
        private readonly List<PsycheGrabbable> _finished = new List<PsycheGrabbable>();
        private Camera _head;
        private XROrigin _origin;
        private float _nextScan;
        private PsycheGrabbable[] _grabbables = new PsycheGrabbable[0];

        /// <summary>
        /// Stops any hover on the grabbable at once and hands it back to physics. Safe to call on
        /// an object that is not hovering.
        /// </summary>
        public static void EndHover(PsycheGrabbable grabbable)
        {
            foreach (var hover in s_Active)
                hover.Finish(grabbable);
        }

        private void Awake()
        {
            _head = GetComponent<Camera>();
            _origin = GetComponentInParent<XROrigin>();
            // Default raycast layers already skip the player's capsule (Ignore Raycast); skip the hands too.
            _floorMask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask(HandPhysics.LayerName);
            if (grabSettings == null || _head == null)
            {
                Debug.LogError($"[FloorHover] Needs GrabSettings and a Camera on {name}.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!s_Active.Contains(this)) s_Active.Add(this);
        }

        private void OnDisable()
        {
            s_Active.Remove(this);
            _finished.Clear();
            _finished.AddRange(_hovers.Keys);
            foreach (var g in _finished)
                Finish(g);
        }

        /// <summary>Height of the floor the player stands on, measured each frame by <see cref="MeasureFloor"/>.</summary>
        private float FloorY => _floorY;

        /// <summary>
        /// Measures the floor under the player's head with a ray that starts this far (m) above the
        /// rig origin, low enough to pass under desks, tables and the desk pedestal's bottom shelf
        /// (26 cm). The origin itself is not the floor: the body capsule rests 8 cm above it, and the
        /// Mission Control floor (0.14) sits above the Story start point (0.10).
        /// </summary>
        private const float FloorRayStart = 0.15f;

        /// <summary>Length (m) of the floor ray.</summary>
        private const float FloorRayLength = 1f;

        private float _floorY;
        private int _floorMask;
        private readonly RaycastHit[] _floorHits = new RaycastHit[8];

        /// <summary>The highest static surface under the head (props and drawers lying there do not count); the rig origin if none.</summary>
        private void MeasureFloor()
        {
            float originY = _origin != null ? _origin.Origin.transform.position.y : 0f;
            Vector3 head = _head.transform.position;
            int n = Physics.RaycastNonAlloc(new Vector3(head.x, originY + FloorRayStart, head.z), Vector3.down, _floorHits,
                FloorRayLength, _floorMask, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                if (_floorHits[i].rigidbody != null) continue;
                best = Mathf.Max(best, _floorHits[i].point.y);
            }
            _floorY = best > float.MinValue ? best : originY;
        }

        private void Update()
        {
            MeasureFloor();

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + ScanInterval;
                _grabbables = FindObjectsByType<PsycheGrabbable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }

            foreach (var g in _grabbables)
            {
                if (g == null || !g.isActiveAndEnabled) continue;
                if (g.isSelected)
                {
                    _restSince.Remove(g);
                    continue;
                }

                Hover hover;
                if (_hovers.TryGetValue(g, out hover))
                {
                    if (!HandNear(hover.Body.worldCenterOfMass) && !HandNear(hover.RestCentre)) continue;
                    hover.LastTriggered = Time.time;
                    if (hover.Phase == Phase.Settling)
                        SetPhase(hover, Phase.Rising);
                    continue;
                }

                if (Qualifies(g))
                    StartHover(g);
            }
        }

        private void FixedUpdate()
        {
            float riseSeconds = Mathf.Max(grabSettings.HoverRiseSeconds, 0.01f);
            _finished.Clear();

            foreach (var hover in _hovers.Values)
            {
                if (hover.Grabbable == null || hover.Body == null || hover.Grabbable.isSelected)
                {
                    _finished.Add(hover.Grabbable);
                    continue;
                }

                float t = Mathf.Clamp01((Time.time - hover.PhaseStart) / riseSeconds);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                Vector3 pos = hover.Body.position;

                switch (hover.Phase)
                {
                    case Phase.Rising:
                        pos.y = Mathf.Lerp(hover.FromY, hover.TopY, eased);
                        if (t >= 1f) SetPhase(hover, Phase.Hovering);
                        break;

                    case Phase.Hovering:
                        float since = Time.time - hover.PhaseStart;
                        pos.y = hover.TopY + grabSettings.HoverBobAmplitude * Mathf.Sin(2f * Mathf.PI * grabSettings.HoverBobFrequency * since);
                        if (Time.time - hover.LastTriggered > grabSettings.HoverLingerSeconds)
                            SetPhase(hover, Phase.Settling);
                        break;

                    case Phase.Settling:
                        pos.y = Mathf.Lerp(hover.FromY, hover.RestPosition.y, eased);
                        float drop = hover.Body.position.y - pos.y;
                        RaycastHit hit;
                        if (t >= 1f || (drop > 0f && hover.Body.SweepTest(Vector3.down, out hit, drop + SweepSkin, QueryTriggerInteraction.Ignore)))
                        {
                            _finished.Add(hover.Grabbable);
                            continue;
                        }
                        break;
                }

                hover.Body.MovePosition(pos);
            }

            foreach (var g in _finished)
                Finish(g);
        }

        /// <summary>
        /// In the floor state (at rest, resting on the floor itself, not on a shelf or in a low desk
        /// drawer), free, and a free hand points or reaches at it.
        /// </summary>
        private bool Qualifies(PsycheGrabbable g)
        {
            var body = g.GetComponent<Rigidbody>();
            if (body == null || body.isKinematic) return false;

            Vector3 centre = body.worldCenterOfMass;
            if (centre.y - FloorY > grabSettings.HoverFloorBand) return false;
            if (DrawerSlide.Containing(centre) != null) return false;
            if (!OnFloor(g, body)) return false;
            return HandNear(centre);
        }

        /// <summary>
        /// At rest for <see cref="RestSeconds"/> and lying on the floor itself (touching it): a
        /// falling or bouncing object, or one on a low shelf, is not.
        /// </summary>
        /// <remarks>
        /// The support is the first surface straight below the centre of mass that is not the object
        /// itself. Not a Rigidbody.SweepTest: sweeps skip a surface they start touching, so a sheet
        /// lying on the floor never counted (2026-09-30).
        /// </remarks>
        private bool OnFloor(PsycheGrabbable g, Rigidbody body)
        {
            if (body.linearVelocity.sqrMagnitude > RestSpeed * RestSpeed)
            {
                _restSince.Remove(g);
                return false;
            }
            float since;
            if (!_restSince.TryGetValue(g, out since))
            {
                since = Time.time;
                _restSince[g] = since;
            }
            if (Time.time - since < RestSeconds) return false;

            // From a little above the centre: a sheet's centre is 2 mm off the floor, and a ray that
            // starts inside the floor collider would never see it.
            Vector3 start = body.worldCenterOfMass + Vector3.up * SupportRayLift;
            int n = Physics.RaycastNonAlloc(start, Vector3.down, _supportHits, SupportRayLift + grabSettings.HoverFloorBand + FloorTolerance,
                _floorMask, QueryTriggerInteraction.Ignore);
            float support = float.MinValue;
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (_supportHits[i].rigidbody == body || _supportHits[i].distance >= nearest) continue;
                nearest = _supportHits[i].distance;
                support = _supportHits[i].point.y;
            }
            if (support == float.MinValue || support - FloorY > FloorTolerance) return false;
            return LowestPoint(g) - support <= TouchGap;
        }

        /// <summary>Lowest point of the object's enabled colliders.</summary>
        private static float LowestPoint(PsycheGrabbable g)
        {
            float lowest = float.MaxValue;
            foreach (var c in g.colliders)
                if (c != null && c.enabled && !c.isTrigger) lowest = Mathf.Min(lowest, c.bounds.min.y);
            return lowest;
        }

        /// <summary>
        /// A hand that holds nothing points or reaches at the point: it lies on the thick ray out of
        /// the fingers (the grab centre's forward), or within grab range. A sphere around the hand
        /// kept a floated object up after the player turned to something else, because rising brought
        /// it closer to the hand; a 35 degree cone caught far too much.
        /// </summary>
        private bool HandNear(Vector3 point)
        {
            foreach (var hand in GrabTargeting.Active)
            {
                if (!hand.IsHolding && hand.GrabCentre != null && HandReaches(hand, point)) return true;
            }
            return false;
        }

        /// <summary>
        /// The hover ray's direction: the grab centre's forward turned
        /// <see cref="GrabSettings.HoverHandRayDownTilt"/> toward world down. A tilt about a hand axis
        /// (toward the palm, then toward the pinky) swung the ray in a circle whenever the wrist
        /// rolled; tilting toward the floor keeps it on the palm axis's vertical plane (2026-10-01).
        /// </summary>
        private Vector3 RayDirection(GrabTargeting hand)
        {
            return Vector3.RotateTowards(hand.GrabCentre.forward, Vector3.down, grabSettings.HoverHandRayDownTilt * Mathf.Deg2Rad, 0f);
        }

        /// <summary>The point is in this hand's grab range or on its thick hover ray.</summary>
        private bool HandReaches(GrabTargeting hand, Vector3 point)
        {
            float grab = grabSettings.NearGrabRadius;
            float radius = grabSettings.HoverHandRayRadius;
            Vector3 toPoint = point - hand.GrabCentre.position;
            if (toPoint.sqrMagnitude <= grab * grab) return true;
            Vector3 dir = RayDirection(hand);
            float along = Vector3.Dot(dir, toPoint);
            if (along < 0f || along > grabSettings.HoverHandReach) return false;
            return (toPoint - dir * along).sqrMagnitude <= radius * radius;
        }

        private void StartHover(PsycheGrabbable g)
        {
            var body = g.GetComponent<Rigidbody>();
            body.isKinematic = true;

            var hover = new Hover
            {
                Grabbable = g,
                Body = body,
                RestPosition = body.position,
                RestCentre = body.worldCenterOfMass,
                // Pivots can sit far from the centre of mass (the solar panels are pivoted at their
                // grip end), so aim the centre of mass at HoverHeight, and never move down.
                TopY = Mathf.Max(body.position.y,
                    FloorY + grabSettings.HoverHeight - (body.worldCenterOfMass.y - body.position.y)),
                LastTriggered = Time.time,
            };
            SetPhase(hover, Phase.Rising);
            _hovers.Add(g, hover);
        }

        private static void SetPhase(Hover hover, Phase phase)
        {
            hover.Phase = phase;
            hover.PhaseStart = Time.time;
            hover.FromY = hover.Body.position.y;
        }

        /// <summary>Returns the object to physics, at rest, and forgets it.</summary>
        private void Finish(PsycheGrabbable g)
        {
            if (g == null)
            {
                // Destroyed while hovering: drop the stale entries. A local list, because the
                // caller may be iterating _finished.
                var dead = new List<PsycheGrabbable>();
                foreach (var kv in _hovers)
                    if (kv.Key == null) dead.Add(kv.Key);
                foreach (var d in dead) _hovers.Remove(d);
                return;
            }

            Hover hover;
            if (!_hovers.TryGetValue(g, out hover)) return;
            _hovers.Remove(g);
            if (hover.Body == null) return;

            hover.Body.isKinematic = false;
            hover.Body.linearVelocity = Vector3.zero;
            hover.Body.angularVelocity = Vector3.zero;
        }
    }
}
