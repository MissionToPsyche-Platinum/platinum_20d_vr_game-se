using System.Collections.Generic;
using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// A bookcase that books are pulled out of (TG-180). Two things keep a grab from wrecking it:
    /// <list type="bullet">
    /// <item>Everything resting in the case is held still (kinematic) until a hand grabs it, so a hand
    /// reaching in, a book swinging out or a gap left behind cannot topple the books beside it. Something
    /// put back and left to settle for <see cref="SettleSeconds"/> is held again.</item>
    /// <item>A grabbable picked up while its centre of mass is inside the case ignores the case and
    /// everything else in it until it is out and clear, the way <see cref="PsycheGrabbable"/> lifts a prop
    /// out of a desk drawer.</item>
    /// </list>
    /// A held body is kinematic, so FloorHover leaves it alone.
    ///
    /// The case's box is its renderers' bounds, taken once in its own space at Awake, so the case can sit
    /// at any angle but must not change shape.
    /// </summary>
    public class Bookshelf : MonoBehaviour
    {
        private const int OverlapCapacity = 64;
        /// <summary>Slower than this (m/s, and rad/s for the spin) counts as resting.</summary>
        private const float SettleSpeed = 0.05f;
        /// <summary>Seconds something must rest in the case before it is held still.</summary>
        private const float SettleSeconds = 0.3f;
        /// <summary>Seconds between looks for things that came to rest in the case.</summary>
        private const float ScanInterval = 0.2f;

        private static readonly List<Bookshelf> s_Shelves = new List<Bookshelf>();

        private Collider[] _caseColliders;
        private Bounds _box;   // case-local
        private readonly Collider[] _overlaps = new Collider[OverlapCapacity];
        private readonly HashSet<PsycheGrabbable> _held = new HashSet<PsycheGrabbable>();
        private readonly Dictionary<PsycheGrabbable, float> _restingSince = new Dictionary<PsycheGrabbable, float>();
        private readonly HashSet<PsycheGrabbable> _seen = new HashSet<PsycheGrabbable>();
        // reused every scan: the case is checked five times a second, so nothing here allocates
        private readonly List<PsycheGrabbable> _inCase = new List<PsycheGrabbable>();
        private readonly HashSet<PsycheGrabbable> _found = new HashSet<PsycheGrabbable>();
        private readonly List<PsycheGrabbable> _gone = new List<PsycheGrabbable>();
        private float _nextScan;

        /// <summary>The enabled bookcase whose box holds the point, or null.</summary>
        public static Bookshelf Containing(Vector3 world)
        {
            foreach (var shelf in s_Shelves)
                if (shelf.Contains(world)) return shelf;
            return null;
        }

        /// <summary>
        /// Hands a held body back to physics. PsycheGrabbable calls it at the start of a grab, before XRI
        /// records the body's kinematic state to restore on release.
        /// </summary>
        public static void Unshelve(PsycheGrabbable grabbable)
        {
            foreach (var shelf in s_Shelves)
            {
                if (!shelf._held.Remove(grabbable)) continue;
                var rb = grabbable.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = false;
            }
        }

        /// <summary>True when the point is inside the case's box.</summary>
        public bool Contains(Vector3 world) => _box.Contains(transform.InverseTransformPoint(world));

        /// <summary>
        /// The colliders a body leaving the case must not touch on the way out: the case's own and
        /// those of every other body standing in it now. The leaving body's own colliders are left out.
        /// </summary>
        public List<Collider> CollidersToClear(Rigidbody leaving)
        {
            var result = new List<Collider>(_caseColliders);
            Vector3 centre = transform.TransformPoint(_box.center);
            Vector3 halfExtents = Vector3.Scale(_box.extents, transform.lossyScale);
            int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, _overlaps, transform.rotation,
                                                   Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = _overlaps[i];
                if (c.attachedRigidbody != null && c.attachedRigidbody != leaving && !result.Contains(c))
                    result.Add(c);
            }
            return result;
        }

        private void Awake()
        {
            _caseColliders = GetComponentsInChildren<Collider>(true);
            bool first = true;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 local = transform.InverseTransformPoint(corner);
                    if (first) { _box = new Bounds(local, Vector3.zero); first = false; }
                    else _box.Encapsulate(local);
                }
            }
            if (first)
                Debug.LogWarning($"[Bookshelf] {name} has no renderers to take its box from; no book will be eased out of it.", this);
        }

        private void OnEnable() => s_Shelves.Add(this);

        private void OnDisable() => s_Shelves.Remove(this);

        /// <summary>Everything standing in the case when the scene starts is held at once.</summary>
        private void Start()
        {
            foreach (var g in GrabbablesInCase())
                Hold(g);   // GrabbablesInCase returns the reused list
        }

        private void FixedUpdate()
        {
            if (Time.time < _nextScan) return;
            _nextScan = Time.time + ScanInterval;

            _seen.Clear();
            foreach (var g in GrabbablesInCase())   // a List: its foreach does not allocate
            {
                _seen.Add(g);
                var rb = g.GetComponent<Rigidbody>();
                bool resting = !g.isSelected && !rb.isKinematic
                    && rb.linearVelocity.magnitude < SettleSpeed && rb.angularVelocity.magnitude < SettleSpeed;
                if (!resting) { _restingSince.Remove(g); continue; }
                if (!_restingSince.TryGetValue(g, out float since)) _restingSince[g] = Time.time;
                else if (Time.time - since >= SettleSeconds) Hold(g);
            }
            // forget bodies that left the case before settling
            _gone.Clear();
            foreach (var g in _restingSince.Keys) if (!_seen.Contains(g)) _gone.Add(g);
            foreach (var g in _gone) _restingSince.Remove(g);
        }

        private void Hold(PsycheGrabbable g)
        {
            var rb = g.GetComponent<Rigidbody>();
            if (rb == null || g.isSelected) return;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            _held.Add(g);
            _restingSince.Remove(g);
        }

        /// <summary>Grabbables whose centre of mass is inside the case's box, in a list reused by every call.</summary>
        private List<PsycheGrabbable> GrabbablesInCase()
        {
            Vector3 centre = transform.TransformPoint(_box.center);
            Vector3 halfExtents = Vector3.Scale(_box.extents, transform.lossyScale);
            int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, _overlaps, transform.rotation,
                                                   Physics.AllLayers, QueryTriggerInteraction.Ignore);
            _inCase.Clear();
            _found.Clear();
            for (int i = 0; i < count; i++)
            {
                var rb = _overlaps[i].attachedRigidbody;
                var g = rb != null ? rb.GetComponent<PsycheGrabbable>() : null;
                if (g != null && Contains(rb.worldCenterOfMass) && _found.Add(g))
                    _inCase.Add(g);
            }
            return _inCase;
        }
    }
}
