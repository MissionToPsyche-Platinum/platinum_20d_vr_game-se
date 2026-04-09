using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Grab interactable for book pages/covers attached to a spine via ConfigurableJoint.
    /// Drives the joint's angular drive to a target angle computed from the hand's
    /// position, instead of moving the rigidbody directly. This avoids the velocity-
    /// tracking-fights-joint-solver problem that causes "rubbery" page motion.
    ///
    /// Inherits from PsycheGrabbable so it gets the project's GrabSettings, haptics,
    /// rigidbody config, and dynamic-attach behavior for free.
    /// </summary>
    [RequireComponent(typeof(ConfigurableJoint))]
    public class BookPageGrabbable : PsycheGrabbable
    {
        [Header("Page Drive (during grab)")]
        [Tooltip("Drive spring stiffness while a hand is holding this page.")]
        [SerializeField] private float grabSpring = 5000f;

        [Tooltip("Drive damping while a hand is holding this page.")]
        [SerializeField] private float grabDamper = 50f;

        [Tooltip("Drive damping after release (no spring, just settling).")]
        [SerializeField] private float releaseDamper = 2f;

        private ConfigurableJoint joint;
        private Rigidbody body;
        private IXRSelectInteractor activeInteractor;

        // Cached at grab time:
        private Vector3 hingeAxisWorld;
        private Vector3 anchorWorld;
        private Vector3 initialHandDir;
        private float initialJointAngleDeg;

        protected override void Awake()
        {
            // base.Awake() runs PsycheGrabbable.Awake() which sets movementType to
            // VelocityTracking before XRI initializes. We must override AFTER that runs
            // so XRI sees Instantaneous when it later reads the property each frame.
            // Instantaneous disables velocity tracking which is what fights the joint solver.
            base.Awake();
            movementType = MovementType.Instantaneous;
            joint = GetComponent<ConfigurableJoint>();
            body = GetComponent<Rigidbody>();
            ApplyDrive(0f, releaseDamper);
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            activeInteractor = args.interactorObject;
            CacheJointFrame(args.interactorObject.GetAttachTransform(this).position);
            ApplyDrive(grabSpring, grabDamper);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            activeInteractor = null;
            // Drop spring so the page stays at whatever angle it's currently at,
            // settled by light damping.
            ApplyDrive(0f, releaseDamper);
        }

        private void FixedUpdate()
        {
            if (activeInteractor == null) return;

            Transform hand = activeInteractor.GetAttachTransform(this);
            Vector3 currentHandDir = ProjectAndNormalize(hand.position - anchorWorld, hingeAxisWorld);
            // Hand on the hinge axis: no useful direction, hold the last target.
            if (currentHandDir.sqrMagnitude < 0.0001f) return;

            float deltaAngleDeg = SignedAngle(initialHandDir, currentHandDir, hingeAxisWorld);
            float targetAngleDeg = Mathf.Clamp(
                initialJointAngleDeg + deltaAngleDeg,
                joint.lowAngularXLimit.limit,
                joint.highAngularXLimit.limit);

            // ConfigurableJoint.targetRotation uses the joint's local frame; the X
            // axis convention is inverted for angular X drive, so we negate.
            joint.targetRotation = Quaternion.AngleAxis(-targetAngleDeg, Vector3.right);
        }

        private void CacheJointFrame(Vector3 handPos)
        {
            // World-space hinge axis (joint.axis is in body-local space)
            hingeAxisWorld = (transform.rotation * joint.axis).normalized;

            // World-space anchor position (joint.anchor is in body-local space)
            anchorWorld = transform.TransformPoint(joint.anchor);

            // Initial hand direction projected onto plane perpendicular to hinge axis
            initialHandDir = ProjectAndNormalize(handPos - anchorWorld, hingeAxisWorld);
            if (initialHandDir.sqrMagnitude < 0.0001f)
            {
                // Fallback: pick an arbitrary perpendicular direction
                initialHandDir = Vector3.Cross(hingeAxisWorld, Vector3.up).normalized;
                if (initialHandDir.sqrMagnitude < 0.0001f)
                    initialHandDir = Vector3.Cross(hingeAxisWorld, Vector3.right).normalized;
            }

            initialJointAngleDeg = GetCurrentJointAngleDeg();
        }

        private float GetCurrentJointAngleDeg()
        {
            if (joint.connectedBody == null) return 0f;

            // Relative rotation from connected body to this body
            Quaternion relRot = Quaternion.Inverse(joint.connectedBody.rotation) * body.rotation;
            relRot.ToAngleAxis(out float angle, out Vector3 axis);

            // ToAngleAxis returns axis in world space. joint.axis is body-local — transform
            // it to world space before comparing, otherwise the sign flip is meaningless
            // for any body that isn't at identity rotation.
            Vector3 hingeAxisWorldSpace = transform.rotation * joint.axis;
            if (Vector3.Dot(axis, hingeAxisWorldSpace) < 0f) angle = -angle;

            // Wrap to [-180, 180]
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        private void ApplyDrive(float spring, float damper)
        {
            var drive = joint.angularXDrive;
            drive.positionSpring = spring;
            drive.positionDamper = damper;
            drive.maximumForce = Mathf.Infinity;
            joint.angularXDrive = drive;
        }

        private static Vector3 ProjectAndNormalize(Vector3 v, Vector3 axis)
        {
            Vector3 projected = v - Vector3.Dot(v, axis) * axis;
            return projected.normalized;
        }

        private static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            float angle = Vector3.Angle(from, to);
            Vector3 cross = Vector3.Cross(from, to);
            return angle * Mathf.Sign(Vector3.Dot(cross, axis));
        }
    }
}
