using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// A big key or button that a hand slaps: the cap sinks along <see cref="pressDirection"/>,
    /// springs back, and <see cref="OnPressed"/> fires once per slap. No trigger or grip input is
    /// involved; the hand only has to come down on the cap.
    ///
    /// Detection polls the hand colliders (layer <see cref="HandPhysics.LayerName"/>) in a box
    /// that covers the cap and reaches <see cref="reach"/> out in front of it, so the cap needs no
    /// collider of its own and never joins a grabbable's collider set. A slap is a hand within
    /// <see cref="contactDistance"/> of the cap face moving into it at <see cref="minSlapSpeed"/>
    /// or faster. After a press, and after the component is re-enabled, the key arms only once every
    /// hand has left the box, so a hand resting on it does not fire it again.
    ///
    /// While <see cref="blockWhileHeld"/> is held, slaps are ignored.
    ///
    /// Keys that share an <see cref="exclusiveGroup"/> (the keyboard's Esc, Enter and Space) count one
    /// slap between them: when one fires, the others in the group are disarmed until every hand has
    /// left them, so a palm landing across Enter and Space presses only one.
    ///
    /// Each press sends a haptic pulse to the hand that slapped (the hand whose grab centre is
    /// nearest the collider that hit).
    /// </summary>
    public class SlapKey : MonoBehaviour
    {
        [Tooltip("The moving part. Defaults to this transform.")]
        [SerializeField] private Transform cap;

        [Tooltip("Direction the cap moves when pressed, in the cap's own local space.")]
        [SerializeField] private Vector3 pressDirection = Vector3.down;

        [Tooltip("How far the cap sinks (m).")]
        [SerializeField] private float travel = 0.009f;

        [Tooltip("How far in front of the cap face hands are tracked (m).")]
        [SerializeField] private float reach = 0.07f;

        [Tooltip("A hand this close to the cap face (m), or past it, is touching it.")]
        [SerializeField] private float contactDistance = 0.02f;

        [Tooltip("Extra margin around the cap's sides (m).")]
        [SerializeField] private float sideMargin = 0.02f;

        [Tooltip("Minimum hand speed into the cap (m/s) for a slap to count.")]
        [SerializeField] private float minSlapSpeed = 0.2f;

        [Tooltip("Seconds after a press before the next one can fire.")]
        [SerializeField] private float cooldown = 0.25f;

        [Tooltip("Seconds to sink.")]
        [SerializeField] private float sinkTime = 0.04f;

        [Tooltip("Seconds to spring back.")]
        [SerializeField] private float returnTime = 0.18f;

        [Tooltip("While this is held, slaps are ignored. Leave empty for a key that is never carried.")]
        [SerializeField] private XRBaseInteractable blockWhileHeld;

        [Tooltip("Keys with the same non-empty group count one slap between them.")]
        [SerializeField] private string exclusiveGroup = "";

        [Tooltip("Haptic pulse on the slapping hand: intensity 0..1.")]
        [SerializeField, Range(0f, 1f)] private float hapticIntensity = 0.5f;

        [Tooltip("Haptic pulse duration (seconds).")]
        [SerializeField] private float hapticDuration = 0.06f;

        [SerializeField] private UnityEvent onPressed = new UnityEvent();

        /// <summary>Fires once per slap.</summary>
        public UnityEvent OnPressed => onPressed;

        private const int MaxOverlaps = 16;

        private static readonly List<SlapKey> s_Keys = new List<SlapKey>();

        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];
        private readonly Dictionary<int, float> _lastDepth = new Dictionary<int, float>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly List<int> _stale = new List<int>();
        private Transform _zone;        // cap rest pose, fixed to the cap's parent
        private Bounds _capBounds;      // cap mesh bounds, cap local space
        private Vector3 _dir;           // press direction, cap local space, normalized
        private Vector3 _restLocal;
        private int _handMask;
        private bool _armed = true;
        private float _lastPress = float.NegativeInfinity;
        private float _anim = -1f;      // seconds since the press started, -1 when idle

        private void Awake()
        {
            if (cap == null) cap = transform;
            _restLocal = cap.localPosition;
            _dir = pressDirection.sqrMagnitude > 0f ? pressDirection.normalized : Vector3.down;
            _handMask = LayerMask.GetMask(HandPhysics.LayerName);

            var filter = cap.GetComponent<MeshFilter>();
            _capBounds = filter != null && filter.sharedMesh != null
                ? filter.sharedMesh.bounds
                : new Bounds(Vector3.zero, Vector3.one * 0.02f);

            _zone = new GameObject(cap.name + " SlapZone").transform;
            _zone.SetParent(cap.parent, false);
            _zone.localPosition = cap.localPosition;
            _zone.localRotation = cap.localRotation;
            _zone.localScale = cap.localScale;
        }

        private void OnEnable()
        {
            if (!s_Keys.Contains(this)) s_Keys.Add(this);
        }

        private void OnDisable()
        {
            s_Keys.Remove(this);
            _lastDepth.Clear();
            // re-enabled keys arm only after a frame with no hand in the box, so a hand already resting
            // on the key (e.g. waiting out a ping) does not fire it
            _armed = false;
            _anim = -1f;
            if (cap != null) cap.localPosition = _restLocal;
        }

        private void FixedUpdate()
        {
            if (blockWhileHeld != null && blockWhileHeld.isSelected)
            {
                _lastDepth.Clear();
                _armed = true;
                return;
            }

            float scale = Mathf.Max(_zone.lossyScale.x, 1e-5f);
            float reachLocal = reach / scale;
            Vector3 extents = _capBounds.extents + Abs(_dir) * (reachLocal * 0.5f) + Vector3.one * (sideMargin / scale);
            Vector3 centreLocal = _capBounds.center - _dir * (reachLocal * 0.5f);
            Vector3 centre = _zone.TransformPoint(centreLocal);
            Vector3 half = Vector3.Scale(extents, _zone.lossyScale);
            Vector3 dirWorld = _zone.TransformDirection(_dir);
            // Cap face: the bounds corner furthest against the press direction.
            Vector3 face = _zone.TransformPoint(_capBounds.center - Vector3.Scale(_capBounds.extents, Abs(_dir)).magnitude * _dir);

            int n = Physics.OverlapBoxNonAlloc(centre, half, _overlaps, _zone.rotation, _handMask, QueryTriggerInteraction.Ignore);
            bool fire = false;
            Collider hitBy = null;
            _seen.Clear();
            for (int i = 0; i < n; i++)
            {
                var c = _overlaps[i];
                int id = c.GetInstanceID();
                _seen.Add(id);
                // Depth of the collider's leading point past the face, along the press direction (negative = still in front).
                float depth = Vector3.Dot(c.ClosestPoint(face) - face, dirWorld);
                if (_lastDepth.TryGetValue(id, out float last) && _armed && !fire
                    && depth >= -contactDistance
                    && (depth - last) / Time.fixedDeltaTime >= minSlapSpeed
                    && Time.time - _lastPress >= cooldown)
                {
                    fire = true;
                    hitBy = c;
                }
                _lastDepth[id] = depth;
            }

            _stale.Clear();
            foreach (var id in _lastDepth.Keys) if (!_seen.Contains(id)) _stale.Add(id);
            foreach (var id in _stale) _lastDepth.Remove(id);
            if (n == 0) _armed = true;

            if (fire)
            {
                Press();
                Buzz(hitBy);
            }
        }

        /// <summary>Haptic pulse on the controller whose hand is nearest the collider that slapped.</summary>
        private void Buzz(Collider hitBy)
        {
            if (hitBy == null || hapticIntensity <= 0f) return;
            Vector3 p = hitBy.bounds.center;
            GrabTargeting nearest = null;
            float best = float.MaxValue;
            foreach (var hand in GrabTargeting.Active)
            {
                if (hand.GrabCentre == null) continue;
                float d = (hand.GrabCentre.position - p).sqrMagnitude;
                if (d < best) { best = d; nearest = hand; }
            }
            if (nearest != null && nearest.Interactor != null)
                nearest.Interactor.SendHapticImpulse(hapticIntensity, hapticDuration);
        }

        /// <summary>Sinks the cap and fires <see cref="OnPressed"/>, exactly as a slap does.</summary>
        public void Press()
        {
            if (!string.IsNullOrEmpty(exclusiveGroup))
                foreach (var other in s_Keys)
                    if (other != this && other.exclusiveGroup == exclusiveGroup)
                    {
                        other._armed = false;
                        other._lastPress = Time.time;
                    }
            _armed = false;
            _lastPress = Time.time;
            _anim = 0f;
            onPressed.Invoke();
        }

        private void Update()
        {
            if (_anim < 0f) return;
            _anim += Time.deltaTime;
            float amount;
            if (_anim < sinkTime)
                amount = _anim / sinkTime;
            else
            {
                float t = (_anim - sinkTime) / returnTime;
                if (t >= 1f) { amount = 0f; _anim = -1f; }
                else amount = 1f - t * t * (3f - 2f * t);
            }
            // _dir is in cap space; move in parent space by the matching vector.
            Vector3 dirParent = cap.localRotation * Vector3.Scale(_dir, cap.localScale).normalized;
            float parentScale = cap.parent != null ? Mathf.Max(cap.parent.lossyScale.x, 1e-5f) : 1f;
            cap.localPosition = _restLocal + dirParent * (travel * amount / parentScale);
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
