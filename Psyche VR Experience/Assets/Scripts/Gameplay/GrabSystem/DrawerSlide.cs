using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using PsycheVR.Audio;
using PsycheVR.Data;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// A desk drawer that slides on one axis (its local +Z, out of the desk) between fully closed
    /// (where it starts) and <see cref="maxOpen"/>, Job Simulator style:
    /// <list type="bullet">
    /// <item>Grip the handle (an <see cref="XRSimpleInteractable"/> child that glows like any
    /// grabbable) and the drawer follows the hand along the axis. Pulling the hand more than
    /// <see cref="GrabSettings.DrawerBreakDistance"/> from the handle lets go.</item>
    /// <item>Let go while moving and the drawer keeps that speed, slows under
    /// <see cref="GrabSettings.DrawerFriction"/> and stops dead at either limit.</item>
    /// <item>While it is open, a hand coming at the front from outside pushes it in; a slap keeps
    /// gliding (at <see cref="GrabSettings.DrawerPushCarry"/> of its speed) after the hand stops.</item>
    /// <item>A hand inside the drawer pushes the inside of the front out, so it can slap it open. It
    /// cannot push the drawer in until it has been <see cref="ClearDistance"/> in front of the face,
    /// so a hand working inside that drifts out past the face and back does not close it.</item>
    /// </list>
    /// <see cref="Containing"/> tells other systems whether a point is inside a drawer: FloorHover
    /// skips drawer contents, and PsycheGrabbable lifts a prop out through the walls.
    /// The drawer is a kinematic Rigidbody moved with MovePosition, so props inside ride along
    /// and the hand proxies (also kinematic) never fight it: pushes are read from the Hand layer
    /// with an overlap query instead of contacts.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DrawerSlide : MonoBehaviour, ISessionInteractable
    {
        [Tooltip("Shared grab settings asset (the Drawers section).")]
        [SerializeField] private GrabSettings grabSettings;

        [Tooltip("The handle's interactable. Auto-found in children if empty.")]
        [SerializeField] private XRSimpleInteractable handle;

        [Tooltip("How far (m) the drawer opens from closed.")]
        [SerializeField, Min(0.01f)] private float maxOpen = 0.3f;

        [Tooltip("Size (m, drawer-local X and Y) of the front face that hands can push.")]
        [SerializeField] private Vector2 frontSize = new Vector2(0.37f, 0.24f);

        [Tooltip("Centre (m, drawer-local X and Y) of the front face.")]
        [SerializeField] private Vector2 frontCentre = new Vector2(0f, 0.12f);

        /// <summary>Hands this far (m) in front of the face are tracked as possible pushers.</summary>
        private const float PushReachFront = 0.15f;

        /// <summary>Hands this far (m) behind the face are still tracked, so a fast slap is not missed.</summary>
        private const float PushReachBehind = 0.08f;

        /// <summary>Extra margin (m) around the front face for the push query.</summary>
        private const float PushMargin = 0.03f;

        /// <summary>A hand this close (m) behind the face last step still counts as in front (numerical slack).</summary>
        private const float FaceTolerance = 0.005f;

        /// <summary>Depth (m) behind the face of the point used to find a hand's leading surface.</summary>
        private const float LeadingPointDepth = 1f;

        /// <summary>A hand that was inside must get this far (m) in front of the face before it can push the drawer in.</summary>
        private const float ClearDistance = 0.08f;

        /// <summary>Drawers reaching a stop slower than this (m/s) make no sound.</summary>
        private const float StopSoundMinSpeed = 0.15f;

        /// <summary>Drawers reaching a stop at or above this speed (m/s) play at the library volume.</summary>
        private const float StopSoundFullSpeed = 1.5f;

        /// <summary>Where a hand is relative to the drawer. Front hands push the face in; Inside hands push the inside of the front out.</summary>
        private enum HandState { Front, Inside }

        private static readonly List<DrawerSlide> s_Drawers = new List<DrawerSlide>();

        private Rigidbody _rb;
        private Transform _parent;
        private Vector3 _closedLocal;
        private Vector3 _axisLocal;
        private float _offset;
        private float _velocity;
        private int _handMask;

        private IXRSelectInteractor _holder;
        private float _grabHandCoord;
        private float _grabOffset;

        /// <summary>The drawer box (front, sides, floor, back) in drawer-local space.</summary>
        private Bounds _box;
        /// <summary>Inside surface of the front panel, drawer-local Z.</summary>
        private float _innerFaceLocal;
        private Collider[] _deskColliders;

        private readonly Collider[] _overlaps = new Collider[8];
        private Dictionary<int, HandState> _states = new Dictionary<int, HandState>();
        private Dictionary<int, HandState> _statesNext = new Dictionary<int, HandState>();
        private readonly Dictionary<int, float> _trailCoords = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _trailCoordsNext = new Dictionary<int, float>();

        /// <summary>How far open the drawer is, 0 (closed) to <see cref="maxOpen"/>.</summary>
        public float Offset => _offset;

        /// <summary>Every collider of the desk this drawer belongs to (carcass, drawers, handles).</summary>
        public IReadOnlyList<Collider> DeskColliders => _deskColliders;

        /// <summary>The enabled drawer whose box holds the point, or null.</summary>
        public static DrawerSlide Containing(Vector3 world)
        {
            foreach (var d in s_Drawers)
                if (d.Contains(world)) return d;
            return null;
        }

        /// <summary>The enabled drawer whose handle is this interactable, or null.</summary>
        public static DrawerSlide ForHandle(IXRInteractable interactable)
        {
            foreach (var d in s_Drawers)
                if (ReferenceEquals(d.handle, interactable)) return d;
            return null;
        }

        /// <summary>True when the point is inside this drawer's box.</summary>
        public bool Contains(Vector3 world)
        {
            return _box.Contains(transform.InverseTransformPoint(world));
        }

        /// <summary>True when the point is above the top edge of the front face by more than <paramref name="margin"/> (m).</summary>
        public bool IsAboveFace(Vector3 world, float margin)
        {
            return transform.InverseTransformPoint(world).y > frontCentre.y + frontSize.y * 0.5f + margin;
        }

        /// <summary>True when the point is more than <paramref name="margin"/> (m) behind the front face, over the drawer.</summary>
        public bool IsBehindFace(Vector3 world, float margin)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return p.z < -margin && p.x >= _box.min.x && p.x <= _box.max.x;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            _parent = transform.parent;
            _closedLocal = transform.localPosition;
            _axisLocal = transform.localRotation * Vector3.forward;
            _handMask = LayerMask.GetMask(HandPhysics.LayerName);

            if (handle == null) handle = GetComponentInChildren<XRSimpleInteractable>(true);
            if (grabSettings == null || handle == null || _parent == null)
            {
                Debug.LogError($"[DrawerSlide] Needs GrabSettings, a handle XRSimpleInteractable and a parent on {name}.", this);
                enabled = false;
                return;
            }

            var glow = handle.GetComponent<GrabGlow>();
            if (glow != null) glow.Init(handle, grabSettings);

            BuildBox();
            var desk = new List<Collider>();
            // Props parked under the desk in a scene are not part of it.
            foreach (var c in _parent.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger && (c.attachedRigidbody == null || c.attachedRigidbody.GetComponent<PsycheGrabbable>() == null))
                    desk.Add(c);
            _deskColliders = desk.ToArray();
        }

        /// <summary>
        /// The drawer box from this object's own box colliders (the front is the one reaching
        /// furthest forward); falls back to the front face and <see cref="maxOpen"/> deep.
        /// </summary>
        private void BuildBox()
        {
            var boxes = GetComponents<BoxCollider>();
            BoxCollider front = null;
            foreach (var b in boxes)
            {
                var local = new Bounds(b.center, b.size);
                if (front == null) _box = local; else _box.Encapsulate(local);
                if (front == null || b.center.z + b.size.z * 0.5f > front.center.z + front.size.z * 0.5f) front = b;
            }

            if (front == null)
            {
                _box = new Bounds(new Vector3(frontCentre.x, frontCentre.y, -maxOpen * 0.5f),
                    new Vector3(frontSize.x, frontSize.y, maxOpen));
                _innerFaceLocal = 0f;
                return;
            }
            _innerFaceLocal = front.center.z - front.size.z * 0.5f;
        }

        private void OnEnable()
        {
            if (handle == null) return;
            if (!s_Drawers.Contains(this)) s_Drawers.Add(this);
            handle.selectEntered.AddListener(OnGrab);
            handle.selectExited.AddListener(OnRelease);
        }

        private void OnDisable()
        {
            s_Drawers.Remove(this);
            if (handle == null) return;
            handle.selectEntered.RemoveListener(OnGrab);
            handle.selectExited.RemoveListener(OnRelease);
            _holder = null;
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            _holder = args.interactorObject;
            SessionEvents.Interaction("drawer_grabbed", this);
            _grabHandCoord = AxisCoord(HandPosition());
            _grabOffset = _offset;
            _velocity = 0f;
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            if (!ReferenceEquals(args.interactorObject, _holder)) return;
            _holder = null;
            _velocity *= grabSettings.DrawerReleaseScale;
        }

        private Vector3 HandPosition()
        {
            return _holder.GetAttachTransform(handle).position;
        }

        /// <summary>Position of a world point along the drawer axis, in the desk's space.</summary>
        private float AxisCoord(Vector3 world)
        {
            return Vector3.Dot(_parent.InverseTransformPoint(world), _axisLocal);
        }

        /// <summary>The front face's position along the axis at the current offset.</summary>
        private float FaceCoord => Vector3.Dot(_closedLocal, _axisLocal) + _offset;

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float max = grabSettings.DrawerMaxSpeed;
            float target;

            if (_holder != null)
            {
                if (Vector3.Distance(HandPosition(), handle.transform.position) > grabSettings.DrawerBreakDistance)
                {
                    ReleaseHolder();
                    target = Glide(dt);
                }
                else
                {
                    target = _grabOffset + AxisCoord(HandPosition()) - _grabHandCoord;
                    target = Mathf.MoveTowards(_offset, target, max * dt);
                    _velocity = (Mathf.Clamp(target, 0f, maxOpen) - _offset) / dt;
                }
            }
            else
            {
                target = Glide(dt);
                target = ApplyPushes(target, dt);
            }

            SetOffset(target);
        }

        /// <summary>Free motion: keep the current speed, lose some to friction.</summary>
        private float Glide(float dt)
        {
            _velocity = Mathf.MoveTowards(_velocity, 0f, grabSettings.DrawerFriction * dt);
            _velocity = Mathf.Clamp(_velocity, -grabSettings.DrawerMaxSpeed, grabSettings.DrawerMaxSpeed);
            return _offset + _velocity * dt;
        }

        /// <summary>
        /// Hand pushes, tracked per hand collider across steps:
        /// <list type="bullet">
        /// <item>A Front hand whose leading surface goes behind the face pushes the drawer in to it.
        /// Crossing above the face's top edge is reaching in, not a push.</item>
        /// <item>An Inside hand whose front-most surface passes the inside of the front pushes it out.
        /// It turns Front only once it is <see cref="ClearDistance"/> in front of the face.</item>
        /// </list>
        /// The push speed times <see cref="GrabSettings.DrawerPushCarry"/> becomes the glide speed.
        /// </summary>
        private float ApplyPushes(float target, float dt)
        {
            _statesNext.Clear();
            _trailCoordsNext.Clear();
            if (_offset <= 0f && _velocity <= 0f)
            {
                SwapHandStates();
                return target;
            }

            float face = FaceCoord;
            float innerFace = face + _innerFaceLocal;
            float faceTop = frontCentre.y + frontSize.y * 0.5f;

            // One box from the back of the drawer to PushReachFront in front of the face.
            float back = Mathf.Min(_box.min.z, -PushReachBehind);
            Vector3 centreLocal = new Vector3(frontCentre.x, frontCentre.y, (PushReachFront + back) * 0.5f);
            Vector3 halfExtents = new Vector3(frontSize.x * 0.5f + PushMargin, frontSize.y * 0.5f + PushMargin, (PushReachFront - back) * 0.5f);
            int n = Physics.OverlapBoxNonAlloc(transform.TransformPoint(centreLocal), Vector3.Scale(halfExtents, transform.lossyScale),
                _overlaps, transform.rotation, _handMask, QueryTriggerInteraction.Ignore);

            Vector3 faceCentre = transform.TransformPoint(new Vector3(frontCentre.x, frontCentre.y, 0f));
            float pushTo = float.MaxValue;
            float pullTo = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                var c = _overlaps[i];
                int id = c.GetInstanceID();
                // Leading surface: the point nearest a spot well inside the desk. Trailing: nearest a spot well out in front.
                float lead = AxisCoord(c.ClosestPoint(faceCentre - transform.forward * LeadingPointDepth));
                float trail = AxisCoord(c.ClosestPoint(faceCentre + transform.forward * LeadingPointDepth));
                float low = transform.InverseTransformPoint(c.ClosestPoint(faceCentre - transform.up * LeadingPointDepth)).y;
                _trailCoordsNext[id] = trail;

                HandState state;
                bool seen = _states.TryGetValue(id, out state);
                if (!seen)
                    state = lead >= face - FaceTolerance ? HandState.Front : HandState.Inside;

                switch (state)
                {
                    case HandState.Front:
                        if (lead < face - FaceTolerance && seen)
                        {
                            if (low > faceTop) state = HandState.Inside;   // reached in over the top
                            else pushTo = Mathf.Min(pushTo, lead);
                        }
                        break;

                    case HandState.Inside:
                        float prevTrail;
                        if (_trailCoords.TryGetValue(id, out prevTrail) && prevTrail <= innerFace + FaceTolerance && trail > innerFace)
                            pullTo = Mathf.Max(pullTo, trail);
                        if (lead >= face + ClearDistance) state = HandState.Front;
                        break;
                }
                _statesNext[id] = state;
            }

            float result = target;
            if (pushTo != float.MaxValue)
            {
                float pushed = _offset - (face - pushTo);
                if (pushed < result)
                {
                    _velocity = Mathf.Max(-grabSettings.DrawerMaxSpeed, (pushed - _offset) / dt * grabSettings.DrawerPushCarry);
                    result = pushed;
                }
            }
            else if (pullTo != float.MinValue)
            {
                float pulled = _offset + (pullTo - innerFace);
                if (pulled > result)
                {
                    _velocity = Mathf.Min(grabSettings.DrawerMaxSpeed, (pulled - _offset) / dt * grabSettings.DrawerPushCarry);
                    result = pulled;
                }
            }

            SwapHandStates();
            return result;
        }

        private void SwapHandStates()
        {
            var t = _states;
            _states = _statesNext;
            _statesNext = t;
            _trailCoords.Clear();
            foreach (var kv in _trailCoordsNext) _trailCoords[kv.Key] = kv.Value;
        }

        /// <summary>Clamps to the limits (stopping dead there), knocks when it reaches one, and moves the body.</summary>
        private void SetOffset(float target)
        {
            if (target <= 0f && _offset > 0f)
            {
                PlayStop(InteractionSound.DrawerStopClosed);
                SessionEvents.Interaction("drawer_closed", this);
            }
            else if (target >= maxOpen && _offset < maxOpen)
            {
                PlayStop(InteractionSound.DrawerStopOpen);
                SessionEvents.Interaction("drawer_opened", this);
            }

            if (target <= 0f)
            {
                target = 0f;
                if (_holder == null) _velocity = 0f;
            }
            else if (target >= maxOpen)
            {
                target = maxOpen;
                if (_holder == null) _velocity = 0f;
            }

            _offset = target;
            _rb.MovePosition(_parent.TransformPoint(_closedLocal + _axisLocal * _offset));
        }

        /// <summary>The end-stop knock, louder the faster the drawer arrives.</summary>
        private void PlayStop(InteractionSound sound)
        {
            float speed = Mathf.Abs(_velocity);
            if (speed < StopSoundMinSpeed)
                return;
            InteractionAudio.Play(sound, handle.transform.position,
                Mathf.InverseLerp(StopSoundMinSpeed, StopSoundFullSpeed, speed) * 0.8f + 0.2f);
        }

        private void ReleaseHolder()
        {
            var interactor = _holder;
            _holder = null;
            if (handle.interactionManager != null && interactor != null && handle.interactorsSelecting.Contains(interactor))
                handle.interactionManager.SelectExit(interactor, (IXRSelectInteractable)handle);
            _velocity *= grabSettings.DrawerReleaseScale;
        }
    }
}
