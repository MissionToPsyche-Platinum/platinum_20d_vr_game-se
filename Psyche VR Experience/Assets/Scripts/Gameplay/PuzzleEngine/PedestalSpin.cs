using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using PsycheVR.Audio;
using PsycheVR.Data;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Lets a visitor turn the puzzle pedestal like a lazy Susan (TG-296): grip the turntable plate and the
    /// pedestal turns flat about its post one to one with the hand; let go and it coasts, slowing to a stop
    /// under <see cref="friction"/>. Pieces already placed turn with it, so every side of the model can be
    /// reached while building.
    ///
    /// A visitor turns it two ways, and each frame the turn is whichever moved further:
    /// <list type="bullet">
    /// <item>Dragging, like pushing a lazy Susan's rim: at the grab, the direction the rim moves under the
    /// hand is fixed; from then on the hand's travel along it turns the plate by the same distance at the grab
    /// radius, so a straight swipe to the side keeps it turning that way for as long as the hand moves.</item>
    /// <item>Twisting the wrist, like a knob: the twist about the vertical, one to one.</item>
    /// </list>
    /// Taking the larger, not the sum, keeps a drag where the wrist turns along from counting twice.
    /// Placed pieces lose their rigidbody interpolation: turning their parent every frame fought it and
    /// made them jiggle.
    ///
    /// When every tagged zone on the turntable holds its piece (TG-297), the model is lifted off: its pieces
    /// move under a new <see cref="FloatingModel"/> at the model's centre, which glides <see cref="floatOffset"/>
    /// off the pedestal and can be turned on every axis, while the post and the plate sink <see cref="lowerDistance"/> into the column and the
    /// plate stops taking grabs.
    ///
    /// The pedestal sits inside the nested puzzle prefab under a non-uniform scale, so at Awake this makes a
    /// plain pivot ("Turntable", unit scale, on the post's axis) and moves the pedestal and the plate under
    /// it. Each piece a zone on the turntable takes (<see cref="SnapZone.Placed"/>) is moved under it too.
    /// The plate is the grab handle: an XRSimpleInteractable that glows like any grabbable. Visitors also
    /// reach for the model by a solar panel, so an open hand slapping a placed piece spins the turntable the
    /// way a slap spins the finished model (<see cref="HandPushProbe"/>): the rim follows the hand's speed
    /// across it at the touch, then coasts down under <see cref="friction"/>, capped at <see cref="maxSpeed"/>.
    /// </summary>
    public class PedestalSpin : MonoBehaviour, ISessionInteractable
    {
        [Tooltip("Shared grab settings (for the glow).")]
        [SerializeField] private GrabSettings grabSettings;

        [Tooltip("The pedestal post that turns (with the bus zone on it).")]
        [SerializeField] private Transform pedestal;

        [Tooltip("The turntable plate a hand grips to turn the pedestal.")]
        [SerializeField] private XRSimpleInteractable plate;

        [Tooltip("How fast a coasting pedestal slows (degrees per second, per second).")]
        [SerializeField] private float friction = 220f;

        [Tooltip("Fastest the pedestal coasts after a release (degrees per second). A held plate follows the hand at any speed.")]
        [SerializeField] private float maxSpeed = 360f;

        [Tooltip("Raised once when the last piece is placed and the model lifts off (the board shows its completion page).")]
        [SerializeField] private UnityEvent onFinished = new UnityEvent();

        [Tooltip("How far (m) the post and plate sink into the column when the puzzle is done.")]
        [SerializeField] private float lowerDistance = 0.27f;

        [Tooltip("Seconds the post and plate take to sink.")]
        [SerializeField] private float lowerSeconds = 1.2f;

        [Tooltip("Where (m, world lengths) the finished model glides to from the pedestal, along the pedestal parent's axes. Event room: level, toward the video screen, clear of the message board.")]
        [SerializeField] private Vector3 floatOffset = new Vector3(0f, 0f, 0.3f);

        [Tooltip("Degrees the finished model banks into as it glides (x, y, z about the pedestal parent's axes): z lifts the right panel, negative x tips the top toward the visitor.")]
        [SerializeField] private Vector3 floatTilt = new Vector3(-10f, 0f, 20f);

        [Tooltip("A hand this far (m) from the plate's rim (or from its grab radius on a placed piece) lets go: room for a long sideways swipe.")]
        [SerializeField] private float breakDistance = 0.45f;

        /// <summary>A hand this far (m) past the placed pieces' bounds is tracked for a slap.</summary>
        private const float TouchReach = 0.3f;
        /// <summary>A slap this close (m) to the post has no lever.</summary>
        private const float MinPushRadius = 0.05f;
        /// <summary>Weight of the newest step in the smoothed hand speed (0..1).</summary>
        private const float SpeedSmoothing = 0.15f;
        /// <summary>A grab closer than this (m) to the post has no useful rim direction; the visitor's side is used instead.</summary>
        private const float MinGrabRadius = 0.03f;

        private Transform _turntable;
        private IXRSelectInteractor _holder;
        private IXRSelectInteractable _held;
        private readonly List<Collider> _pieceParts = new List<Collider>();
        private readonly HandPushProbe _push = new HandPushProbe();
        private float _angle, _lastTwist, _speed, _dragRadius, _breakRadius;
        private Vector3 _dragTangent, _lastHand;
        private bool _finished;
        private float _lowerStart = -1f;
        private Vector3 _turntableHome;
        private Quaternion _baseRotation;
        private float _plateRadius;

        /// <summary>Raised once when the last piece is placed and the model lifts off.</summary>
        public UnityEvent OnFinished => onFinished;

        /// <summary>The pivot everything on the pedestal turns with (null before Awake).</summary>
        public Transform Turntable => _turntable;

        private void Awake()
        {
            if (pedestal == null || plate == null)
            {
                Debug.LogError($"[PedestalSpin] {name} needs the pedestal and the plate.", this);
                enabled = false;
                return;
            }
            var pivot = new GameObject("Turntable").transform;
            pivot.SetParent(pedestal.parent, false);
            pivot.position = new Vector3(pedestal.position.x, plate.transform.position.y, pedestal.position.z);
            pivot.rotation = Quaternion.identity;
            _turntable = pivot;
            _baseRotation = pivot.rotation;
            pedestal.SetParent(pivot, true);
            plate.transform.SetParent(pivot, true);

            var plateRenderer = plate.GetComponent<Renderer>();
            _plateRadius = plateRenderer != null ? Mathf.Max(plateRenderer.bounds.extents.x, plateRenderer.bounds.extents.z) : 0.1f;

            var glow = plate.GetComponent<GrabGlow>();
            if (glow == null) glow = plate.gameObject.AddComponent<GrabGlow>();
            if (grabSettings != null) glow.Init(plate, grabSettings);
        }

        private void OnEnable()
        {
            SnapZone.Snapped += OnSnapped;
            if (plate == null) return;
            plate.selectEntered.AddListener(OnGrab);
            plate.selectExited.AddListener(OnRelease);
        }

        private void OnDisable()
        {
            SnapZone.Snapped -= OnSnapped;
            if (plate == null) return;
            plate.selectEntered.RemoveListener(OnGrab);
            plate.selectExited.RemoveListener(OnRelease);
            _holder = null;
        }

        /// <summary>A piece placed on the turntable turns with it from now on.</summary>
        private void OnSnapped(SnapZone zone)
        {
            if (_turntable == null || zone == null || zone.Placed == null) return;
            if (!zone.transform.IsChildOf(_turntable)) return;
            zone.Placed.transform.SetParent(_turntable, true);
            var rb = zone.Placed.GetComponent<Rigidbody>();
            if (rb != null) rb.interpolation = RigidbodyInterpolation.None;
            if (_finished) return;
            _pieceParts.AddRange(zone.Placed.GetComponentsInChildren<Collider>());
            if (AllPlaced()) Finish();
        }

        /// <summary>True when every tagged zone on the turntable holds its piece (untagged zones never take one).</summary>
        private bool AllPlaced()
        {
            int zones = 0;
            foreach (var z in _turntable.GetComponentsInChildren<SnapZone>(true))
            {
                if (string.IsNullOrEmpty(z.requiredTag)) continue;
                zones++;
                if (!z.hasSnapped) return false;
            }
            return zones > 0;
        }

        /// <summary>Lifts the finished model off the turntable and starts sinking the post and plate.</summary>
        private void Finish()
        {
            _finished = true;
            if (_holder != null) ReleaseHolder();
            _speed = 0f;
            plate.enabled = false;
            _pieceParts.Clear();

            var pieces = _turntable.GetComponentsInChildren<SnappableObject>(true);
            Bounds b = default; bool first = true;
            foreach (var p in pieces)
                foreach (var r in p.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
            var model = new GameObject("FloatingModel").transform;
            model.SetParent(_turntable.parent, false);
            model.position = first ? _turntable.position : b.center;
            foreach (var p in pieces)
                if (p.transform.parent == _turntable) p.transform.SetParent(model, true);
            model.gameObject.AddComponent<FloatingModel>().Init(grabSettings, friction, maxSpeed,
                _turntable.parent.TransformDirection(floatOffset),   // direction only: the parent may be non-uniformly scaled
                _turntable.parent.rotation * Quaternion.Euler(floatTilt) * Quaternion.Inverse(_turntable.parent.rotation));

            _turntableHome = _turntable.position;
            _lowerStart = Time.time;
            SessionEvents.Interaction("pedestal_lowered", this);
            InteractionAudio.Play(InteractionSound.PuzzleComplete, model.position);
            onFinished.Invoke();
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            _holder = args.interactorObject;
            _held = args.interactableObject;
            _lastHand = _holder.GetAttachTransform(_held).position;
            _lastTwist = Twist();
            // the rim's direction of travel under the grab, for a positive turn; fixed for the whole drag
            Vector3 radial = Vector3.ProjectOnPlane(_lastHand - _turntable.position, Vector3.up);
            if (radial.magnitude < MinGrabRadius)
            {
                var cam = Camera.main;
                radial = cam != null ? Vector3.ProjectOnPlane(cam.transform.position - _turntable.position, Vector3.up) : Vector3.back;
            }
            _dragRadius = Mathf.Max(radial.magnitude, _plateRadius);
            _breakRadius = _dragRadius;
            _dragTangent = Vector3.Cross(Vector3.up, radial).normalized;
            _speed = 0f;
            SessionEvents.Interaction("pedestal_grabbed", this);
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            if (!ReferenceEquals(args.interactorObject, _holder)) return;
            _holder = null;
            _held = null;
        }

        /// <summary>The hand's twist (degrees) about the world vertical: the yaw part of its rotation.</summary>
        private float Twist()
        {
            Quaternion q = _holder.GetAttachTransform(_held).rotation;
            return 2f * Mathf.Atan2(q.y, q.w) * Mathf.Rad2Deg;
        }

        /// <summary>Turns open hands slapping a placed piece this physics step into turntable spin (not while the plate is held).</summary>
        private void FixedUpdate()
        {
            if (_finished || _holder != null || _turntable == null || _pieceParts.Count == 0) return;
            _push.Poll(_pieceParts, TouchReach, Push);
        }

        private void Push(Vector3 at, Vector3 velocity)
        {
            Vector3 radial = Vector3.ProjectOnPlane(at - _turntable.position, Vector3.up);
            if (radial.magnitude < MinPushRadius) return;
            // the rim under the hand follows the hand, as in a drag: speed along the rim / radius
            float along = Vector3.Dot(velocity, Vector3.Cross(Vector3.up, radial).normalized);
            float target = Mathf.Clamp(along / radial.magnitude * Mathf.Rad2Deg, -maxSpeed, maxSpeed);
            // a faster slap takes over; a slap against the turn reverses it
            if (Mathf.Abs(target) > Mathf.Abs(_speed) || Mathf.Sign(target) != Mathf.Sign(_speed))
            {
                if (_speed == 0f) SessionEvents.Interaction("pedestal_pushed", this);
                _speed = target;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _turntable == null) return;
            if (_finished)
            {
                if (_lowerStart < 0f) return;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - _lowerStart) / lowerSeconds));
                _turntable.position = _turntableHome + Vector3.down * (lowerDistance * k);
                if (k >= 1f) _lowerStart = -1f;
                return;
            }
            float previous = _angle;

            if (_holder != null)
            {
                Vector3 hand = _holder.GetAttachTransform(_held).position;
                Vector3 flat = hand - _turntable.position;
                float fromRim = Mathf.Abs(new Vector2(flat.x, flat.z).magnitude - _breakRadius);
                if (fromRim > breakDistance)
                {
                    ReleaseHolder();
                }
                else
                {
                    // frame by frame, the larger of the drag along the rim and the wrist's twist; no cap while held
                    float twist = Twist();
                    float drag = Vector3.Dot(hand - _lastHand, _dragTangent) / _dragRadius * Mathf.Rad2Deg;
                    float turn = Mathf.DeltaAngle(_lastTwist, twist);
                    _lastHand = hand;
                    _lastTwist = twist;
                    float step = Mathf.Abs(turn) > Mathf.Abs(drag) ? turn : drag;
                    _angle += step;
                    _speed = Mathf.Lerp(_speed, step / dt, SpeedSmoothing);
                }
            }
            if (_holder == null && _speed != 0f)
            {
                _speed = Mathf.Clamp(_speed, -maxSpeed, maxSpeed);
                float slowed = Mathf.MoveTowards(_speed, 0f, friction * dt);
                _angle += (_speed + slowed) * 0.5f * dt;
                _speed = slowed;
            }

            if (!Mathf.Approximately(previous, _angle))
                _turntable.rotation = _baseRotation * Quaternion.AngleAxis(_angle, Vector3.up);
        }

        private void ReleaseHolder()
        {
            var interactor = _holder;
            var held = _held as XRBaseInteractable;
            _holder = null;
            _held = null;
            if (held != null && held.interactionManager != null && interactor != null && held.interactorsSelecting.Contains(interactor))
                held.interactionManager.SelectExit(interactor, (IXRSelectInteractable)held);
        }
    }
}
