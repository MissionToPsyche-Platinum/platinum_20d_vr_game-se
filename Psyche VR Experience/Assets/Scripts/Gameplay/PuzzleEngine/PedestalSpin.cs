using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
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
    /// The pedestal sits inside the nested puzzle prefab under a non-uniform scale, so at Awake this makes a
    /// plain pivot ("Turntable", unit scale, on the post's axis) and moves the pedestal and the plate under
    /// it. Each piece a zone on the turntable takes (<see cref="SnapZone.Placed"/>) is moved under it too.
    /// The plate is the grab handle: an XRSimpleInteractable that glows like any grabbable.
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

        [Tooltip("A hand this far (m) from the plate's rim lets go: room for a long sideways swipe.")]
        [SerializeField] private float breakDistance = 0.45f;

        /// <summary>Weight of the newest step in the smoothed hand speed (0..1).</summary>
        private const float SpeedSmoothing = 0.15f;
        /// <summary>A grab closer than this (m) to the post has no useful rim direction; the visitor's side is used instead.</summary>
        private const float MinGrabRadius = 0.03f;

        private Transform _turntable;
        private IXRSelectInteractor _holder;
        private float _angle, _lastTwist, _speed, _dragRadius;
        private Vector3 _dragTangent, _lastHand;
        private Quaternion _baseRotation;
        private float _plateRadius;

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
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            _holder = args.interactorObject;
            _lastHand = _holder.GetAttachTransform(plate).position;
            _lastTwist = Twist();
            // the rim's direction of travel under the grab, for a positive turn; fixed for the whole drag
            Vector3 radial = Vector3.ProjectOnPlane(_lastHand - _turntable.position, Vector3.up);
            if (radial.magnitude < MinGrabRadius)
            {
                var cam = Camera.main;
                radial = cam != null ? Vector3.ProjectOnPlane(cam.transform.position - _turntable.position, Vector3.up) : Vector3.back;
            }
            _dragRadius = Mathf.Max(radial.magnitude, _plateRadius);
            _dragTangent = Vector3.Cross(Vector3.up, radial).normalized;
            _speed = 0f;
            SessionEvents.Interaction("pedestal_grabbed", this);
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            if (ReferenceEquals(args.interactorObject, _holder)) _holder = null;
        }

        /// <summary>The hand's twist (degrees) about the world vertical: the yaw part of its rotation.</summary>
        private float Twist()
        {
            Quaternion q = _holder.GetAttachTransform(plate).rotation;
            return 2f * Mathf.Atan2(q.y, q.w) * Mathf.Rad2Deg;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _turntable == null) return;
            float previous = _angle;

            if (_holder != null)
            {
                Vector3 hand = _holder.GetAttachTransform(plate).position;
                Vector3 flat = hand - _turntable.position;
                float fromRim = Mathf.Abs(new Vector2(flat.x, flat.z).magnitude - _plateRadius);
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
            _holder = null;
            if (plate.interactionManager != null && interactor != null && plate.interactorsSelecting.Contains(interactor))
                plate.interactionManager.SelectExit(interactor, (IXRSelectInteractable)plate);
        }
    }
}
