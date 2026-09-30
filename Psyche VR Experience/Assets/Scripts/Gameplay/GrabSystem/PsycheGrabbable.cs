using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Universal grab component for every grabbable object in Psyche VR.
    /// Velocity-tracked to the hand's palm anchor (set up by PsycheHandSetup).
    /// Physics comes from one <see cref="GrabProfile"/> in the shared <see cref="GrabSettings"/>:
    /// gravity when free, throw scales, mass, damping, physics material. XRI itself turns
    /// gravity off during a hold and restores it on release.
    ///
    /// Far grabs are kept, but gated: the ease into the palm scales with distance, and an
    /// object that starts farther than the near threshold flies in with its colliders off,
    /// so a ball grabbed from behind the desk does not plough through it. Colliders come
    /// back the moment it is within the threshold, or on release.
    ///
    /// A held object still collides with the world. So it cannot fight the hand forever, the
    /// velocity XRI applies each step is capped, and after <see cref="GrabSettings.PressedContactSteps"/>
    /// consecutive steps of contact with something the hold softens to
    /// <see cref="GrabSettings.PressedVelocityScale"/> until the contact ends.
    ///
    /// Runaway guard: a held object's speed is always capped at <see cref="GrabSettings.MaxHeldSpeed"/>,
    /// and one stuck more than <see cref="GrabSettings.HoldBreakDistance"/> from the hand (pushed through
    /// the ceiling, wedged behind furniture) is moved back to the hand and dropped. Grabbables never
    /// collide with the player's body capsule (on <see cref="PlayerBodyLayerName"/>).
    ///
    /// Grabbed inside a desk drawer, the object ignores the whole desk until it is out of the
    /// drawer and clear of every desk collider, so it lifts out instead of catching on the walls
    /// and the desk top.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PsycheGrabbable : XRGrabInteractable
    {
        [Header("Psyche VR")]
        [Tooltip("Shared grab settings asset. Must be assigned.")]
        [SerializeField] private GrabSettings grabSettings;

        [Tooltip("Which physics profile from GrabSettings this object uses.")]
        [SerializeField] private GrabProfileKind profile = GrabProfileKind.DeskToy;

        private Rigidbody _rb;
        private GrabProfile _profile;
        private bool _pullingIn;
        private readonly List<Collider> _gated = new List<Collider>();
        private bool _contactThisStep;
        private float _contactScore;

        private string _lastContact;

        /// <summary>Contact score lost per physics step without contact; gained per step with it is 1.</summary>
        private const float ContactDecay = 0.5f;

        /// <summary>
        /// A contact counts as a touch only this close (m). PhysX reports contacts before bodies
        /// meet: within the contact offsets, and under speculative CCD up to a step of travel
        /// ahead (a sheet swept 5 cm over the desk reported contact every step). Those never push,
        /// but counting them softened the hold on things the object was not touching.
        /// </summary>
        private const float TouchSeparation = 0.002f;
        private bool _pressed;
        private int _handLayer = -1;
        private readonly List<Collider> _ignoredStatic = new List<Collider>();
        private DrawerSlide _leavingDrawer;
        private int _baseExcludeLayers;
        private float _grabTime;
        private float _farFromHandFor;
        private bool _dropWithoutThrow;

        /// <summary>
        /// Layer of the rig's CharacterController (the only collider on it in the Bedroom). Props
        /// held near the chest were shoved by the capsule, which started the 2026-09-30 room explosion.
        /// </summary>
        public const string PlayerBodyLayerName = "Ignore Raycast";

        /// <summary>Seconds of hand motion considered for the release velocity.</summary>
        private const float ThrowWindow = 0.1f;
        private const int HandSampleCapacity = 32;
        private readonly Vector3[] _handPositions = new Vector3[HandSampleCapacity];
        private readonly float[] _handTimes = new float[HandSampleCapacity];
        private int _handSampleCount;
        private int _handSampleHead;

        /// <summary>The last controller that held this object, for landing feedback after a throw.</summary>
        public XRBaseInputInteractor LastHolder { get; private set; }

        /// <summary>The profile currently applied.</summary>
        public GrabProfileKind Profile => profile;

        /// <inheritdoc />
        protected override void Awake()
        {
            if (grabSettings == null)
            {
                Debug.LogError("[PsycheGrabbable] GrabSettings is not assigned!", this);
                enabled = false;
                return;
            }

            // Configure before base.Awake() so XRI initializes correctly.
            _profile = grabSettings.GetProfile(profile);
            movementType = _profile.kinematicHold ? MovementType.Kinematic : MovementType.VelocityTracking;
            attachEaseInTime = grabSettings.MinSnapDuration;

            // Most props come to the hand in the orientation they were grabbed: a dynamic attach
            // whose position is the object's own attach point (so it still flies in) and whose
            // rotation is the hand's at grab time (so nothing re-orients). The book keeps a fixed
            // attach pose instead.
            useDynamicAttach = _profile.keepGrabbedRotation;
            // One holder at a time: the other hand grabbing it takes it over and the first lets go.
            selectMode = InteractableSelectMode.Single;
            matchAttachPosition = false;
            matchAttachRotation = true;

            // Held into the desk, an uncapped velocity-tracked object is shoved back every step and
            // fights the hand (two near-freezes on 2026-09-29). The contact watchdog in FixedUpdate
            // caps lower and softens the hold while pressed. The loose cap is always on: a held
            // sheet stuck on top of the ceiling was driven at 600 m/s and flung the whole room
            // (2026-09-30). Throws do not use it; the release speed comes from the hand samples.
            // The angular cap stays on so a wrist flick cannot spin a held object up.
            limitLinearVelocity = true;
            maxLinearVelocityDelta = grabSettings.MaxHeldSpeed;
            limitAngularVelocity = true;
            maxAngularVelocityDelta = grabSettings.MaxHeldAngularVelocity;

            // XRI drops predicted visuals back to the body whenever the object touches something,
            // so during contact the visual flickers between two poses (the ball "splitting").
            // Rigidbody interpolation below gives the smoothness without that.
            predictedVisualsTransform = null;
            _handLayer = LayerMask.NameToLayer(HandPhysics.LayerName);

            base.Awake();

            _rb = GetComponent<Rigidbody>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            int body = LayerMask.NameToLayer(PlayerBodyLayerName);
            _baseExcludeLayers = body >= 0 ? 1 << body : 0;
            _rb.excludeLayers = _baseExcludeLayers;
            ApplyProfile(profile);

            // Every grabbable, the book included, gets the same hover tint.
            var glow = GetComponent<GrabGlow>();
            if (glow == null)
                glow = gameObject.AddComponent<GrabGlow>();
            glow.Init(this, grabSettings);
        }

        /// <summary>
        /// Switches the physics profile, also mid-hold (PaperCrumple's sheet-to-ball swap).
        /// Gravity stays off while held; XRI restores it from the profile on release.
        /// </summary>
        public void SetProfile(GrabProfileKind kind)
        {
            profile = kind;
            ApplyProfile(kind);
        }

        private void ApplyProfile(GrabProfileKind kind)
        {
            _profile = grabSettings.GetProfile(kind);

            throwOnDetach = _profile.throwOnRelease;
            throwVelocityScale = _profile.throwVelocityScale;
            throwAngularVelocityScale = _profile.throwAngularVelocityScale;

            _rb.mass = _profile.mass;
            _rb.linearDamping = _profile.linearDamping;
            _rb.angularDamping = _profile.angularDamping;
            _rb.collisionDetectionMode = _profile.collisionDetection;
            _rb.maxDepenetrationVelocity = _profile.maxDepenetrationVelocity;
            _rb.useGravity = !isSelected && _profile.gravityWhenFree;

            if (_profile.physicsMaterial != null)
            {
                foreach (var c in colliders)
                    c.sharedMaterial = _profile.physicsMaterial;
            }
        }

        /// <inheritdoc />
        protected override void OnSelectEntering(SelectEnterEventArgs args)
        {
            // First hand only: a second hand joining an existing hold does not re-gate.
            if (interactorsSelecting.Count == 0)
            {
                _handSampleCount = 0;
                _grabTime = Time.time;
                _farFromHandFor = 0f;

                float distance = Vector3.Distance(
                    args.interactorObject.GetAttachTransform(this).position,
                    GetAttachTransform(args.interactorObject).position);

                attachEaseInTime = Mathf.Clamp(
                    distance / grabSettings.PullSpeed,
                    grabSettings.MinSnapDuration,
                    grabSettings.MaxSnapDuration);

                if (distance > grabSettings.NearThreshold)
                    GateColliders();

                // Before base: XRI records isKinematic here and restores it on release, so a
                // hovering (kinematic) object must be handed back to physics first.
                FloorHover.EndHover(this);

                var drawer = DrawerSlide.Containing(_rb.worldCenterOfMass);
                if (drawer != null)
                    IgnoreDesk(drawer, true);
            }

            base.OnSelectEntering(args);
        }

        /// <inheritdoc />
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);

            if (args.interactorObject is XRBaseInputInteractor holder)
            {
                LastHolder = holder;
                holder.SendHapticImpulse(grabSettings.GrabHapticIntensity, grabSettings.HapticDuration);
            }

            // A held object never touches a hand: the holding hand's collider is off anyway, and the
            // free hand must be able to reach in and take it over (or turn the book's pages).
            // Excluding the layer on the rigidbody covers every collider attached to it, including
            // the book's page colliders, which are not in the XRI collider list.
            if (_handLayer >= 0)
                _rb.excludeLayers = _baseExcludeLayers | (1 << _handLayer);
        }

        /// <inheritdoc />
        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);

            if (!isSelected)
            {
                UngateColliders();
                SetPressed(false);
                _contactScore = 0f;
                _contactThisStep = false;
                RestoreStaticCollisions();
                if (_leavingDrawer != null)
                    IgnoreDesk(_leavingDrawer, false);
                _rb.excludeLayers = _baseExcludeLayers;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            var other = collision.collider;
            if (!isSelected || !_profile.passThroughStaticWhileHeld || other.attachedRigidbody != null || other.isTrigger)
                return;
            if (_ignoredStatic.Contains(other))
                return;

            foreach (var c in colliders)
            {
                if (c != null)
                    Physics.IgnoreCollision(c, other, true);
            }
            _ignoredStatic.Add(other);
        }

        private void RestoreStaticCollisions()
        {
            foreach (var other in _ignoredStatic)
            {
                if (other == null) continue;
                foreach (var c in colliders)
                {
                    if (c != null)
                        Physics.IgnoreCollision(c, other, false);
                }
            }
            _ignoredStatic.Clear();
        }

        /// <summary>Turns collisions between this object and every collider of the drawer's desk off or back on.</summary>
        private void IgnoreDesk(DrawerSlide drawer, bool ignore)
        {
            foreach (var other in drawer.DeskColliders)
            {
                if (other == null) continue;
                foreach (var c in colliders)
                {
                    if (c != null)
                        Physics.IgnoreCollision(c, other, ignore);
                }
            }
            _leavingDrawer = ignore ? drawer : null;
        }

        /// <summary>True once the object has left the drawer's box and overlaps no desk collider.</summary>
        private bool ClearOfDesk()
        {
            if (_leavingDrawer.Contains(_rb.worldCenterOfMass))
                return false;

            foreach (var c in colliders)
            {
                if (c == null || !c.enabled) continue;
                foreach (var other in _leavingDrawer.DeskColliders)
                {
                    if (other == null || !other.enabled) continue;
                    if (!c.bounds.Intersects(other.bounds)) continue;
                    Vector3 dir;
                    float dist;
                    if (Physics.ComputePenetration(c, c.transform.position, c.transform.rotation,
                            other, other.transform.position, other.transform.rotation, out dir, out dist))
                        return false;
                }
            }
            return true;
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!isSelected || _pullingIn)
                return;
            if (_handLayer >= 0 && collision.collider.gameObject.layer == _handLayer)
                return;

            float nearest = float.MaxValue;
            for (int i = 0; i < collision.contactCount; i++)
            {
                float separation = collision.GetContact(i).separation;
                nearest = Mathf.Min(nearest, separation);
            }

            if (nearest <= TouchSeparation)
            {
                _contactThisStep = true;
                _lastContact = collision.collider.name;
            }

        }


        private void FixedUpdate()
        {
            if (!isSelected)
                return;


            if (_leavingDrawer != null && ClearOfDesk())
                IgnoreDesk(_leavingDrawer, false);

            if (CheckRunaway())
                return;

            // A score, not a run of consecutive steps: a wide piece that brushes something on and
            // off (the solar panels) never had 6 steps in a row and fought every touch. Contact
            // adds 1 a step, a clear step takes ContactDecay off; the hold softens at the
            // threshold and only firms up again once the score is back to zero.
            int threshold = grabSettings.PressedContactSteps;
            _contactScore = _contactThisStep
                ? Mathf.Min(_contactScore + 1f, threshold * 2f)
                : Mathf.Max(_contactScore - ContactDecay, 0f);
            _contactThisStep = false;

            if (!_pressed && _contactScore >= threshold)
                SetPressed(true);
            else if (_pressed && _contactScore <= 0f)
                SetPressed(false);
        }

        /// <summary>Softens or restores the velocity tracking while the held object is pressed against something.</summary>
        private void SetPressed(bool pressed)
        {
            if (pressed == _pressed)
                return;

            _pressed = pressed;
            if (pressed)
                Debug.Log($"[PsycheGrabbable] {name} pressed against {_lastContact}", this);
            maxLinearVelocityDelta = pressed ? grabSettings.MaxHeldVelocity : grabSettings.MaxHeldSpeed;
            velocityScale = pressed ? grabSettings.PressedVelocityScale : 1f;
            angularVelocityScale = pressed ? grabSettings.PressedVelocityScale : 1f;
        }

        /// <inheritdoc />
        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic && isSelected)
                RecordHandSample(interactorsSelecting[0].GetAttachTransform(this).position);

            if (!_pullingIn || updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic || !isSelected)
                return;

            var interactor = interactorsSelecting[0];
            float distance = Vector3.Distance(
                interactor.GetAttachTransform(this).position,
                GetAttachTransform(interactor).position);

            if (distance <= grabSettings.NearThreshold)
                UngateColliders();
        }

        /// <summary>
        /// Drops a held object that has been stuck far from the hand: moves it to the hand and
        /// releases it at rest. True when it did.
        /// </summary>
        private bool CheckRunaway()
        {
            if (_pullingIn || Time.time - _grabTime < attachEaseInTime)
            {
                _farFromHandFor = 0f;
                return false;
            }

            var interactor = interactorsSelecting[0];
            Vector3 hand = interactor.GetAttachTransform(this).position;
            Vector3 attach = GetAttachTransform(interactor).position;
            float distance = Vector3.Distance(hand, attach);
            if (distance <= grabSettings.HoldBreakDistance)
            {
                _farFromHandFor = 0f;
                return false;
            }

            _farFromHandFor += Time.fixedDeltaTime;
            if (_farFromHandFor < grabSettings.HoldBreakSeconds)
                return false;

            Debug.Log($"[PsycheGrabbable] {name} runaway: {distance:F2} m from the hand for {_farFromHandFor:F2} s, dropped at the hand", this);
            Vector3 shift = hand - attach;
            _rb.position += shift;
            transform.position += shift;
            // XRI detaches (and throws) in LateUpdate; Detach sees the flag and drops it at rest.
            _dropWithoutThrow = true;
            if (interactionManager != null)
                interactionManager.SelectExit(interactor, (IXRSelectInteractable)this);
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            return true;
        }

        /// <inheritdoc />
        protected override void Detach()
        {
            base.Detach();
            if (_dropWithoutThrow)
            {
                _dropWithoutThrow = false;
                if (!_rb.isKinematic)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
                return;
            }

            // XRI's throw uses the object's own recent motion, which lags the hand and misses
            // the arm push. Throw with how the hand itself moved instead; the angular part
            // stays XRI's, scaled by the profile, so a wrist flick adds spin but no speed.
            if (throwOnDetach && _handSampleCount >= 3 && !_rb.isKinematic)
                _rb.linearVelocity = PeakHandVelocity() * _profile.throwVelocityScale;
        }

        private void RecordHandSample(Vector3 position)
        {
            _handPositions[_handSampleHead] = position;
            _handTimes[_handSampleHead] = Time.time;
            _handSampleHead = (_handSampleHead + 1) % HandSampleCapacity;
            _handSampleCount = Mathf.Min(_handSampleCount + 1, HandSampleCapacity);
        }

        /// <summary>
        /// Fastest hand velocity in the last <see cref="ThrowWindow"/> seconds, each measured
        /// over two frames to damp tracking noise. The peak, not the last frame, because people
        /// slow their hand in the instant before they open it.
        /// </summary>
        private Vector3 PeakHandVelocity()
        {
            Vector3 best = Vector3.zero;
            float now = Time.time;
            for (int k = 0; k + 2 < _handSampleCount; k++)
            {
                int newer = Wrap(_handSampleHead - 1 - k);
                int older = Wrap(_handSampleHead - 3 - k);
                if (now - _handTimes[newer] > ThrowWindow)
                    break;

                float dt = _handTimes[newer] - _handTimes[older];
                if (dt <= 1e-4f)
                    continue;

                Vector3 v = (_handPositions[newer] - _handPositions[older]) / dt;
                if (v.sqrMagnitude > best.sqrMagnitude)
                    best = v;
            }

            return best;
        }

        private static int Wrap(int i)
        {
            return ((i % HandSampleCapacity) + HandSampleCapacity) % HandSampleCapacity;
        }

        private void GateColliders()
        {
            _gated.Clear();
            foreach (var c in colliders)
            {
                if (c != null && c.enabled)
                {
                    c.enabled = false;
                    _gated.Add(c);
                }
            }

            _pullingIn = _gated.Count > 0;
        }

        private void UngateColliders()
        {
            foreach (var c in _gated)
            {
                if (c != null)
                    c.enabled = true;
            }

            _gated.Clear();
            _pullingIn = false;
        }
    }
}
