using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Gives a hand a body. Builds a kinematic collider proxy at runtime (palm box, finger
    /// box, thumb capsule) on the Hand layer and drives it to <see cref="palmAnchor"/> every
    /// physics step with MovePosition, so props receive a real push and the hand passes
    /// through static furniture instead of wedging. HandPresence switches
    /// <see cref="CollisionEnabled"/> off while the hand holds something; switching it back
    /// on waits <see cref="reenableDelay"/> so the collider does not pop the just-released
    /// object out of the palm.
    ///
    /// Inside a desk drawer the hand stops pushing props (it excludes the Default layer) but keeps
    /// its colliders, so DrawerSlide still sees it: a kinematic hand pressing a light sheet into
    /// a kinematic drawer floor forced it straight through (2026-09-30).
    ///
    /// The proxy lives under the rig root, not the controller: a kinematic body that is
    /// also moved by its parent transform reports no velocity to the things it touches.
    /// </summary>
    public class HandPhysics : MonoBehaviour
    {
        /// <summary>
        /// Farther than this in one physics step (25 m/s at 50 Hz) is a teleport, not a hand
        /// movement. A kinematic sweep that long launches every prop on its path and can stall
        /// the physics step for tens of seconds (headset freeze, 2026-09-29).
        /// </summary>
        private const float MaxStepDistance = 0.5f;

        [Tooltip("Transform at the palm centre: +Z along the fingers, +Y out of the back of the hand.")]
        [SerializeField] private Transform palmAnchor;

        [Tooltip("Which side the thumb capsule sits on, in palm anchor space (-1 = -X, +1 = +X).")]
        [SerializeField, Range(-1f, 1f)] private float thumbSide = 1f;

        [Tooltip("Palm box size (metres).")]
        [SerializeField] private Vector3 palmSize = new Vector3(0.085f, 0.03f, 0.095f);

        [Tooltip("Finger box size (metres), placed just ahead of the palm.")]
        [SerializeField] private Vector3 fingersSize = new Vector3(0.085f, 0.026f, 0.075f);

        [Tooltip("Thumb capsule radius (metres).")]
        [SerializeField] private float thumbRadius = 0.013f;

        [Tooltip("Thumb capsule length (metres).")]
        [SerializeField] private float thumbLength = 0.07f;

        [Tooltip("Seconds to wait after a release (and after start-up) before the hand collides again.")]
        [SerializeField] private float reenableDelay = 0.25f;

        [Tooltip("Layers the hand must be clear of before its collider switches on. A collider that wakes up inside a prop launches it.")]
        [SerializeField] private LayerMask clearanceMask = ~0;

        /// <summary>Physics layer the proxy lives on. Must exist in the Tags and Layers settings.</summary>
        public const string LayerName = "Hand";

        private Rigidbody _rb;
        private GameObject _proxy;
        private Collider[] _colliders;
        private bool _wantCollision = true;
        private float _reenableAt;
        private bool _inDrawer;

        /// <summary>
        /// Whether the hand collides. Off is immediate; on takes effect after the delay.
        /// </summary>
        public bool CollisionEnabled
        {
            get => _wantCollision;
            set
            {
                _wantCollision = value;
                if (!value)
                    SetColliders(false);
                else
                    _reenableAt = Time.time + reenableDelay;
            }
        }

        private void Awake()
        {
            if (palmAnchor == null)
            {
                Debug.LogError($"[HandPhysics] palmAnchor is not assigned on {gameObject.name}.", this);
                enabled = false;
                return;
            }

            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogError($"[HandPhysics] Physics layer '{LayerName}' does not exist.", this);
                enabled = false;
                return;
            }

            BuildProxy(layer);
            // Start switched off: the controllers sit at the rig origin until tracking arrives, and a
            // kinematic collider that wakes up inside a prop launches it (puzzle pieces, 2026-09-29).
            SetColliders(false);
            _reenableAt = Time.time + reenableDelay;
        }

        private void BuildProxy(int layer)
        {
            _proxy = new GameObject($"[HandCollider] {gameObject.name}");
            _proxy.layer = layer;
            _proxy.transform.SetParent(transform.root, worldPositionStays: false);
            _proxy.transform.SetPositionAndRotation(palmAnchor.position, palmAnchor.rotation);

            _rb = _proxy.AddComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var palm = _proxy.AddComponent<BoxCollider>();
            palm.center = Vector3.zero;
            palm.size = palmSize;

            var fingers = _proxy.AddComponent<BoxCollider>();
            fingers.center = new Vector3(0f, 0f, palmSize.z * 0.5f + fingersSize.z * 0.5f);
            fingers.size = fingersSize;

            var thumb = _proxy.AddComponent<CapsuleCollider>();
            thumb.center = ThumbCenter();
            thumb.radius = thumbRadius;
            thumb.height = thumbLength;
            thumb.direction = 2; // Z axis

            _colliders = new Collider[] { palm, fingers, thumb };
        }

        private Vector3 ThumbCenter()
        {
            return new Vector3(thumbSide * (palmSize.x * 0.5f + thumbRadius), 0f, palmSize.z * 0.35f);
        }

        private void OnEnable()
        {
            if (_proxy == null) return;
            _proxy.SetActive(true);
            // Teleport, do not sweep, from wherever the proxy last was.
            _proxy.transform.SetPositionAndRotation(palmAnchor.position, palmAnchor.rotation);
            _rb.position = palmAnchor.position;
            _rb.rotation = palmAnchor.rotation;
        }

        private void OnDisable()
        {
            if (_proxy != null) _proxy.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_proxy != null) Destroy(_proxy);
        }

        private void FixedUpdate()
        {
            if (_wantCollision && !_colliders[0].enabled && Time.time >= _reenableAt && IsClear())
                SetColliders(true);

            if ((palmAnchor.position - _rb.position).sqrMagnitude > MaxStepDistance * MaxStepDistance)
            {
                // Teleport with the colliders off; they come back once the hand is clear.
                SetColliders(false);
                _reenableAt = Time.time + reenableDelay;
                _proxy.transform.SetPositionAndRotation(palmAnchor.position, palmAnchor.rotation);
                _rb.position = palmAnchor.position;
                _rb.rotation = palmAnchor.rotation;
                return;
            }

            UpdateDrawerExclusion();
            _rb.MovePosition(palmAnchor.position);
            _rb.MoveRotation(palmAnchor.rotation);
        }

        /// <summary>Props (Default layer) are ignored while the palm or the fingertips are inside a drawer.</summary>
        private void UpdateDrawerExclusion()
        {
            Vector3 tips = palmAnchor.TransformPoint(new Vector3(0f, 0f, palmSize.z * 0.5f + fingersSize.z));
            bool inDrawer = DrawerSlide.Containing(palmAnchor.position) != null || DrawerSlide.Containing(tips) != null;
            if (inDrawer == _inDrawer) return;
            _inDrawer = inDrawer;
            _rb.excludeLayers = inDrawer ? 1 << DefaultLayer : 0;
        }

        /// <summary>The layer every prop lives on.</summary>
        private const int DefaultLayer = 0;

        /// <summary>True when neither the palm nor the finger box overlaps anything on the clearance mask.</summary>
        private bool IsClear()
        {
            int mask = clearanceMask & ~(1 << _proxy.layer);
            var rot = palmAnchor.rotation;
            if (Physics.CheckBox(palmAnchor.position, palmSize * 0.5f, rot, mask, QueryTriggerInteraction.Ignore))
                return false;
            var fingersCenter = palmAnchor.TransformPoint(new Vector3(0f, 0f, palmSize.z * 0.5f + fingersSize.z * 0.5f));
            return !Physics.CheckBox(fingersCenter, fingersSize * 0.5f, rot, mask, QueryTriggerInteraction.Ignore);
        }

        private void SetColliders(bool on)
        {
            if (_colliders == null) return;
            foreach (var c in _colliders)
                c.enabled = on;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (palmAnchor == null) return;
            Gizmos.matrix = palmAnchor.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, palmSize);
            Gizmos.DrawWireCube(new Vector3(0f, 0f, palmSize.z * 0.5f + fingersSize.z * 0.5f), fingersSize);
            Gizmos.DrawWireSphere(ThumbCenter(), thumbRadius);
        }
#endif
    }
}
