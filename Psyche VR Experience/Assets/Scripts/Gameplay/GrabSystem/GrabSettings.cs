using System;
using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>Which physics profile a grabbable uses. Index into <see cref="GrabSettings"/>.</summary>
    public enum GrabProfileKind
    {
        DeskToy,
        Ball,
        Paper,
        Book,
        /// <summary>Spacecraft puzzle pieces: big, so they pass through furniture while held.</summary>
        Puzzle,
        /// <summary>A sheet after crumpling: thrown lighter-handed than the basketball.</summary>
        CrumpledPaper,
    }

    /// <summary>Rigidbody and throw tuning for one kind of grabbable.</summary>
    [Serializable]
    public class GrabProfile
    {
        [Tooltip("Gravity while nobody holds it. XRI turns gravity off during a hold and puts this back on release.")]
        public bool gravityWhenFree = true;

        [Tooltip("Keep the hand's velocity on release. Off means the object stops where it is let go.")]
        public bool throwOnRelease = true;

        [Tooltip("Multiplier on hand velocity at release.")]
        [Range(0f, 3f)] public float throwVelocityScale = 1f;

        [Tooltip("Multiplier on hand angular velocity at release.")]
        [Range(0f, 3f)] public float throwAngularVelocityScale = 1f;

        [Tooltip("Rigidbody mass (kg).")]
        [Min(0.001f)] public float mass = 0.2f;

        [Tooltip("Rigidbody linear damping.")]
        [Min(0f)] public float linearDamping;

        [Tooltip("Rigidbody angular damping.")]
        [Min(0f)] public float angularDamping = 0.05f;

        [Tooltip("Optional physics material applied to every collider of the grabbable (bounce, friction).")]
        public PhysicsMaterial physicsMaterial;

        [Tooltip("Collision detection mode. Continuous Dynamic for anything thrown hard.")]
        public CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousSpeculative;

        [Tooltip("Cap on the speed physics may use to push this object out of an overlap (m/s). Low keeps props that start nested or stacked from launching at scene start.")]
        [Min(0.1f)] public float maxDepenetrationVelocity = 1f;

        [Tooltip("Keep the orientation the object had when grabbed and just bring it to the hand. Off means it snaps to its attach pose (the book).")]
        public bool keepGrabbedRotation = true;

        [Tooltip("Carry the object kinematically (moved straight to the hand each frame) instead of velocity tracking. For pieces grabbed far from their centre of mass (the solar panels, grip point at one end of a 70 cm wing), where velocity tracking over-corrects the rotation every step and the piece shakes. Kinematic carries push through dynamic props, so pair it with passThroughStaticWhileHeld.")]
        public bool kinematicHold;

        [Tooltip("While held, furniture and walls (colliders without a rigidbody) this object touches stop colliding with it until release. For pieces too big to carry through the booth without striking the counter (the solar panels). Triggers still fire, so snapping works.")]
        public bool passThroughStaticWhileHeld;
    }

    /// <summary>
    /// Shared tuning for the grab system: snap timing, the pull-in collision gate, haptics,
    /// and one <see cref="GrabProfile"/> per <see cref="GrabProfileKind"/>. Every grabbable
    /// references this one asset. Create via: Assets > Create > Psyche VR > Grab Settings
    /// </summary>
    [CreateAssetMenu(fileName = "GrabSettings", menuName = "Psyche VR/Grab Settings")]
    public class GrabSettings : ScriptableObject
    {
        [Serializable]
        private struct ProfileEntry
        {
            public GrabProfileKind kind;
            public GrabProfile profile;
        }

        [Header("Snap to Hand")]
        [Tooltip("Shortest ease into the palm (seconds). Used for anything grabbed within reach.")]
        [SerializeField, Range(0.05f, 1f)] private float minSnapDuration = 0.12f;

        [Tooltip("Longest ease into the palm (seconds), for objects grabbed across the room.")]
        [SerializeField, Range(0.2f, 3f)] private float maxSnapDuration = 1.5f;

        [Tooltip("Pull-in speed (m/s) that sets the ease time between the two limits.")]
        [SerializeField, Range(0.5f, 20f)] private float pullSpeed = 4f;

        [Tooltip("Distance from the palm (metres) under which a grab is 'near': no collider gating, and where a far pull-in turns collision back on.")]
        [SerializeField, Range(0.02f, 0.5f)] private float nearThreshold = 0.15f;

        [Header("Held Against Something")]
        [Tooltip("Cap on the linear velocity (m/s) XRI applies to a held object each physics step while it is pressed against something. Limits the shove when the hand moves through something solid; off during a free swing so throws are not clipped.")]
        [SerializeField, Range(0.5f, 20f)] private float maxHeldVelocity = 3f;

        [Tooltip("Cap on the angular velocity (rad/s) XRI applies to a held object each physics step.")]
        [SerializeField, Range(1f, 50f)] private float maxHeldAngularVelocity = 10f;

        [Header("Runaway Guard")]
        [Tooltip("Hard cap on a held object's linear speed (m/s), always on. Throws are unaffected: release speed comes from the hand's own motion. Stops a held object stuck behind something from being driven at hundreds of m/s (the 2026-09-30 room explosion).")]
        [SerializeField, Range(3f, 30f)] private float maxHeldSpeed = 10f;

        [Tooltip("A held object this far (m) from the hand for Hold Break Seconds is stuck: it is moved back to the hand and dropped.")]
        [SerializeField, Range(0.1f, 2f)] private float holdBreakDistance = 0.4f;

        [Tooltip("Seconds a held object may stay beyond Hold Break Distance before it is dropped.")]
        [SerializeField, Range(0.05f, 2f)] private float holdBreakSeconds = 0.25f;

        [Tooltip("Contact score at which a held object's hold softens. Each physics step in contact with something (not the hand) adds 1, each clear step takes 0.5 off; the hold firms up again at 0.")]
        [SerializeField, Range(1, 60)] private int pressedContactSteps = 6;

        [Tooltip("Velocity scale while the held object is pressed against something. 1 = full strength, 0 = the object stays where physics leaves it.")]
        [SerializeField, Range(0f, 1f)] private float pressedVelocityScale = 0.25f;

        [Header("Haptics")]
        [Tooltip("Haptic vibration intensity when a piece is grabbed.")]
        [SerializeField, Range(0f, 1f)] private float grabHapticIntensity = 0.35f;

        [Tooltip("Duration of the haptic pulse (seconds).")]
        [SerializeField] private float hapticDuration = 0.08f;

        [Header("Targeting")]
        [Tooltip("Allow the far ray to pull objects after a dwell. Off (the near-only test): only objects within Near Grab Radius of the palm can be grabbed; the UI pointer line still works.")]
        [SerializeField] private bool farGrabEnabled;

        [Tooltip("Radius (m) of the grab zone around the palm, measured to the nearest collider surface. Anything this close glows and can be grabbed without pointing. Not the same as Near Threshold, which is the attach-to-attach distance that decides pull-in timing and collider gating once a grab has started.")]
        [SerializeField, Range(0.03f, 0.3f)] private float nearGrabRadius = 0.12f;

        [Tooltip("How far (m) the grab zone's centre sits ahead of the palm, toward the fingertips. Reach in front of the hand is radius plus this; behind it, radius minus this.")]
        [SerializeField, Range(0f, 0.1f)] private float nearGrabForwardOffset = 0.04f;

        [Tooltip("Seconds the far ray must rest on a grabbable before grip can pull it. Glow and line fade in over this time.")]
        [SerializeField, Range(0.2f, 3f)] private float farDwellSeconds = 1f;

        [Tooltip("Colour of the line that fades in from the hand to a far target during the dwell. Alpha is the strength at full dwell.")]
        [SerializeField] private Color dwellLineColor = new Color(1f, 1f, 1f, 0.8f);

        [Tooltip("Width (m) of the dwell line.")]
        [SerializeField, Range(0.001f, 0.02f)] private float dwellLineWidth = 0.004f;

        [Header("Hover Glow")]
        [Tooltip("Rim glow on a grabbable the hand is pointing at. Alpha is the strength.")]
        [SerializeField] private Color glowColor = new Color(1f, 1f, 1f, 0.35f);

        [Tooltip("Rim falloff: higher keeps the glow closer to the silhouette.")]
        [SerializeField, Range(0.5f, 10f)] private float glowRimPower = 3f;

        [Tooltip("Seconds the glow takes to fade in or out.")]
        [SerializeField, Range(0f, 1f)] private float glowFadeDuration = 0.15f;

        [Tooltip("Additive rim material drawn over props whose own shader has no rim (plain Lit). Uses Psyche/RimGlowOverlay.")]
        [SerializeField] private Material glowOverlayMaterial;

        [Header("Floor Hover")]
        [Tooltip("Objects whose centre is at most this high (m) above the floor count as on the floor.")]
        [SerializeField, Range(0.05f, 1f)] private float hoverFloorBand = 0.3f;

        [Tooltip("Length (m) of the thick ray out of a free hand's fingers. An object on the ray rises and stays up; point away and it settles.")]
        [SerializeField, Range(0.1f, 1.5f)] private float hoverHandReach = 0.85f;

        [Tooltip("Radius (m) of the thick ray around the line the fingers point along, measured to the object's centre of mass.")]
        [SerializeField, Range(0.02f, 0.4f)] private float hoverHandRayRadius = 0.1f;

        [Tooltip("Degrees the ray turns from the palm's forward axis toward the floor. Holding a controller, the untilted axis points up toward the index finger. Measured against world down, not a hand axis, so rolling the wrist does not swing the ray.")]
        [SerializeField, Range(0f, 45f)] private float hoverHandRayDownTilt = 10f;

        [Tooltip("Height (m) above the floor a floating object's centre rises to.")]
        [SerializeField, Range(0.3f, 1.5f)] private float hoverHeight = 0.5f;

        [Tooltip("Seconds the rise (and the settle) takes.")]
        [SerializeField, Range(0.1f, 3f)] private float hoverRiseSeconds = 0.6f;

        [Tooltip("Seconds an object keeps hovering after the hand points away.")]
        [SerializeField, Range(0f, 10f)] private float hoverLingerSeconds = 0f;

        [Tooltip("Bob amplitude (m) while hovering.")]
        [SerializeField, Range(0f, 0.1f)] private float hoverBobAmplitude = 0.02f;

        [Tooltip("Bob cycles per second while hovering.")]
        [SerializeField, Range(0.1f, 3f)] private float hoverBobFrequency = 0.5f;

        [Header("Drawers")]
        [Tooltip("Deceleration (m/s^2) of a drawer gliding on its own after a pull or a push.")]
        [SerializeField, Range(0.05f, 5f)] private float drawerFriction = 0.6f;

        [Tooltip("Fraction of the hand's speed along the drawer axis a drawer keeps when let go.")]
        [SerializeField, Range(0f, 1.5f)] private float drawerReleaseScale = 1f;

        [Tooltip("Top speed (m/s) of a drawer, pulled, pushed or gliding.")]
        [SerializeField, Range(0.2f, 5f)] private float drawerMaxSpeed = 1.5f;

        [Tooltip("A hand holding a handle lets go once it is this far (m) from the handle.")]
        [SerializeField, Range(0.05f, 0.6f)] private float drawerBreakDistance = 0.2f;

        [Tooltip("Fraction of a hand push's speed a drawer keeps gliding with once the hand stops. Lower is a gentler slap.")]
        [SerializeField, Range(0f, 1f)] private float drawerPushCarry = 0.4f;

        [Header("Profiles")]
        [SerializeField] private ProfileEntry[] profiles = Array.Empty<ProfileEntry>();

        public float MinSnapDuration => minSnapDuration;
        public float MaxSnapDuration => maxSnapDuration;
        public float PullSpeed => pullSpeed;
        public float NearThreshold => nearThreshold;
        public float MaxHeldVelocity => maxHeldVelocity;
        public float MaxHeldSpeed => maxHeldSpeed;
        public float HoldBreakDistance => holdBreakDistance;
        public float HoldBreakSeconds => holdBreakSeconds;
        public float MaxHeldAngularVelocity => maxHeldAngularVelocity;
        public int PressedContactSteps => pressedContactSteps;
        public float PressedVelocityScale => pressedVelocityScale;
        public float GrabHapticIntensity => grabHapticIntensity;
        public float HapticDuration => hapticDuration;
        public Color GlowColor => glowColor;
        public float GlowRimPower => glowRimPower;
        public float GlowFadeDuration => glowFadeDuration;
        public Material GlowOverlayMaterial => glowOverlayMaterial;
        public float NearGrabRadius => nearGrabRadius;
        public float NearGrabForwardOffset => nearGrabForwardOffset;
        public float FarDwellSeconds => farDwellSeconds;
        public bool FarGrabEnabled => farGrabEnabled;
        public float DrawerFriction => drawerFriction;
        public float DrawerReleaseScale => drawerReleaseScale;
        public float DrawerMaxSpeed => drawerMaxSpeed;
        public float DrawerBreakDistance => drawerBreakDistance;
        public float DrawerPushCarry => drawerPushCarry;
        public float HoverFloorBand => hoverFloorBand;
        public float HoverHandReach => hoverHandReach;
        public float HoverHandRayRadius => hoverHandRayRadius;
        public float HoverHandRayDownTilt => hoverHandRayDownTilt;
        public float HoverHeight => hoverHeight;
        public float HoverRiseSeconds => hoverRiseSeconds;
        public float HoverLingerSeconds => hoverLingerSeconds;
        public float HoverBobAmplitude => hoverBobAmplitude;
        public float HoverBobFrequency => hoverBobFrequency;
        public Color DwellLineColor => dwellLineColor;
        public float DwellLineWidth => dwellLineWidth;

        /// <summary>
        /// The profile for a kind. Logs and returns a default profile if the asset has none,
        /// so a missing entry shows up in the console instead of as a null reference.
        /// </summary>
        public GrabProfile GetProfile(GrabProfileKind kind)
        {
            foreach (var entry in profiles)
            {
                if (entry.kind == kind && entry.profile != null)
                    return entry.profile;
            }

            Debug.LogError($"[GrabSettings] No profile for {kind} in {name}.", this);
            return new GrabProfile();
        }
    }
}
