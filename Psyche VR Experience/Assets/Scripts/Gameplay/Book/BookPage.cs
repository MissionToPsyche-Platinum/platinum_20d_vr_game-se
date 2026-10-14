using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Interactive book page using the XRLever pattern.
    /// Drives localRotation directly from hand movement — no physics, no joints. The page's
    /// outer edge follows the hand 1:1 from wherever it was grabbed, so a pinch near the
    /// spine turns the page as far as one at the edge.
    /// </summary>
    public class BookPage : XRBaseInteractable
    {
        [Header("Interaction")]
        [Tooltip("When false, this page cannot be grabbed by the player. " +
                 "Used for covers driven only by BookAutoOpen.")]
        [SerializeField] private bool allowHandInteraction = true;

        /// <summary>Whether this page can be grabbed by the player.</summary>
        public bool AllowHandInteraction => allowHandInteraction;

        [Header("Hinge")]
        [Tooltip("Local axis the page rotates around (spine edge).")]
        [SerializeField] private Vector3 hingeAxis = Vector3.right;

        [Tooltip("Minimum rotation angle in degrees (closed/unflipped).")]
        [SerializeField] private float minAngle;

        [Tooltip("Maximum rotation angle in degrees (fully flipped).")]
        [SerializeField] private float maxAngle = 180f;

        [Header("Pivot")]
        [Tooltip("Vector from page origin to spine edge in page LOCAL space. " +
                 "Set automatically by the editor setup tool.")]
        [SerializeField] private Vector3 pivotOffset;

        [Header("Reference Direction")]
        [Tooltip("Direction the page extends at 0 degrees, in parent local space. " +
                 "Set automatically by the editor setup tool.")]
        [SerializeField] private Vector3 zeroAngleDirection = Vector3.forward;

        [Header("Snap Animation")]
        [Tooltip("SmoothDamp smooth time in seconds. Lower = snappier settle.")]
        [SerializeField] private float snapSmoothTime = 0.15f;

        [Header("Grab Tracking")]
        [Tooltip("SmoothDamp smooth time (sec) for page rotation while held. " +
                 "0 = instant 1:1 with hand. Higher = slower, more weighted feel.")]
        [SerializeField] private float grabSmoothTime = 0.08f;

        [Header("Velocity Flip")]
        [Tooltip("Angular velocity (degrees/sec) above which a release is treated " +
                 "as an intentional flick — page completes the flip in the velocity " +
                 "direction regardless of current position.")]
        [SerializeField] private float velocityFlipThreshold = 75f;

        [Tooltip("Smoothing factor for angular velocity (0=no smoothing, 1=max). " +
                 "Higher reduces jitter but adds latency.")]
        [Range(0f, 0.95f)]
        [SerializeField] private float velocitySmoothing = 0.6f;

        [Header("Attach")]
        [Tooltip("Where the XR ray connects when this page is grabbed. " +
                 "If null, uses the page's own transform.")]
        [SerializeField] private Transform attachTransform;

        public override Transform GetAttachTransform(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor interactor)
        {
            return attachTransform != null ? attachTransform : base.GetAttachTransform(interactor);
        }

        private const float SnapConvergenceThreshold = 0.5f;

        private float currentAngle;
        private float targetAngle;
        private float angleVelocity;
        private bool isAnimating;
        private float grabAngleOffset; // delta between edge-target angle and page angle at grab start
        private float lastRawAngle; // unwrapped target angle from the previous frame
        private Vector3 grabHandStartParent; // hand position in parent space at grab start
        private Vector3 grabEdgeStartParent; // page edge grip point in parent space at grab start
        private BoxCollider pageCollider;
        private float smoothedAngularVelocity; // smoothed degrees/sec during grab
        private float grabSmoothVelocity; // SmoothDamp velocity buffer for grab tracking
        private Quaternion initialLocalRotation;
        private Vector3 initialLocalPosition;
        private Vector3 spineEdgeParent; // spine edge position in parent space
        private Vector3 zeroAngleDir;
        private Vector3 perpendicularDir;
        private PageManager pageManager;

        /// <summary>Whether this page is currently on the flipped (left) side.</summary>
        public bool IsFlipped { get; private set; }

        /// <summary>Current rotation angle in degrees.</summary>
        public float CurrentAngle => currentAngle;

        protected override void Awake()
        {
            // Register own collider BEFORE base.Awake() so XRI skips auto-discovery
            // and doesn't claim parent's (spine) colliders for this interactable.
            var col = GetComponent<Collider>();
            pageCollider = col as BoxCollider;
            if (col != null)
            {
                colliders.Clear();
                colliders.Add(col);
            }

            base.Awake();

            selectMode = InteractableSelectMode.Single;
            pageManager = GetComponentInParent<PageManager>();
            initialLocalRotation = transform.localRotation;
            initialLocalPosition = transform.localPosition;

            // Compute spine edge position in parent space.
            // pivotOffset is the vector from page origin to spine edge in LOCAL space.
            spineEdgeParent = initialLocalPosition + initialLocalRotation * pivotOffset;

            // Build orthonormal reference frame on the hinge plane.
            // Check BEFORE normalization so near-parallel configurations are caught.
            float h = Vector3.Dot(zeroAngleDirection, hingeAxis);
            Vector3 projected = zeroAngleDirection - h * hingeAxis;
            if (projected.sqrMagnitude < 0.001f)
                projected = Vector3.forward;
            zeroAngleDir = projected.normalized;
            perpendicularDir = Vector3.Cross(hingeAxis, zeroAngleDir).normalized;
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic) return;

            if (isSelected)
                UpdateGrabAngle();
            else if (isAnimating)
                UpdateSnapAnimation();
        }

        private void UpdateGrabAngle()
        {
            // Unwrap so crossing the +-180 boundary behind the spine does not jump the page.
            float edgeAngle = ComputeEdgeTargetAngle(interactorsSelecting[0]);
            lastRawAngle += Mathf.DeltaAngle(lastRawAngle, edgeAngle);
            float rawAngle = lastRawAngle - grabAngleOffset;

            float targetGrabAngle = Mathf.Clamp(rawAngle, minAngle, maxAngle);

            // Smooth the page rotation toward the hand target so it feels weighted
            // rather than 1:1. grabSmoothTime=0 keeps the original instant tracking.
            float newAngle = grabSmoothTime > 0f
                ? Mathf.SmoothDamp(currentAngle, targetGrabAngle,
                    ref grabSmoothVelocity, grabSmoothTime)
                : targetGrabAngle;

            // Track angular velocity (deg/sec) with exponential smoothing for flick detection.
            if (Time.deltaTime > 0f)
            {
                float instantVelocity = (newAngle - currentAngle) / Time.deltaTime;
                smoothedAngularVelocity = Mathf.Lerp(
                    instantVelocity, smoothedAngularVelocity, velocitySmoothing);
            }

            currentAngle = newAngle;
            ApplyRotation();
        }

        /// <summary>
        /// Middle of the page's outer edge (the edge opposite the spine), in world space.
        /// The pinching hand is drawn here, and hand motion drives this point.
        /// </summary>
        public Vector3 EdgeGripPoint => transform.TransformPoint(EdgeGripLocal());

        private Vector3 EdgeGripLocal()
        {
            if (pageCollider == null)
                return -pivotOffset;

            // From the spine edge to the collider centre, minus the hinge and thickness
            // components, is the page's width direction; the outer edge is half the
            // collider's width beyond the centre along it.
            Vector3 centre = pageCollider.center;
            Vector3 size = pageCollider.size;
            Vector3 localHinge = Quaternion.Inverse(initialLocalRotation) * hingeAxis;
            Vector3 across = centre - pivotOffset;
            across -= Vector3.Dot(across, localHinge) * localHinge;
            int thin = size.x < size.y ? (size.x < size.z ? 0 : 2) : (size.y < size.z ? 1 : 2);
            across[thin] = 0f;
            if (across.sqrMagnitude < 1e-8f)
                return -pivotOffset;

            across.Normalize();
            float halfWidth = 0.5f * Mathf.Abs(Vector3.Dot(Vector3.Scale(across, size), across));
            return centre + across * halfWidth;
        }

        /// <summary>
        /// Angle of the point the page edge should reach: the edge grip point at grab start,
        /// moved by exactly as far as the hand has moved since. The hand position is the
        /// interactor's own transform, not its attach point, which a far grab puts at the
        /// ray hit and the pinching hand model drags along with the page. Degrees.
        /// </summary>
        private float ComputeEdgeTargetAngle(IXRSelectInteractor interactor)
        {
            Vector3 hand = transform.parent.InverseTransformPoint(interactor.transform.position);
            return AngleAroundSpine(grabEdgeStartParent + (hand - grabHandStartParent));
        }

        /// <summary>Angle of a parent-space point around the spine edge on the hinge plane. Degrees.</summary>
        private float AngleAroundSpine(Vector3 pointParent)
        {
            Vector3 pivotToPoint = pointParent - spineEdgeParent;
            float h = Vector3.Dot(pivotToPoint, hingeAxis);
            Vector3 projected = pivotToPoint - h * hingeAxis;
            if (projected.sqrMagnitude < 0.0001f)
                return lastRawAngle;
            projected.Normalize();

            float y = Vector3.Dot(projected, perpendicularDir);
            float z = Vector3.Dot(projected, zeroAngleDir);
            return Mathf.Atan2(y, z) * Mathf.Rad2Deg;
        }

        private void UpdateSnapAnimation()
        {
            currentAngle = Mathf.SmoothDamp(
                currentAngle, targetAngle, ref angleVelocity, snapSmoothTime);

            if (Mathf.Abs(currentAngle - targetAngle) < SnapConvergenceThreshold)
            {
                currentAngle = targetAngle;
                angleVelocity = 0f;
                isAnimating = false;
            }

            ApplyRotation();
        }

        private void ApplyRotation()
        {
            Quaternion newRot = Quaternion.AngleAxis(currentAngle, hingeAxis) * initialLocalRotation;
            // Rotate around spine edge, not page center.
            // P' = spineEdge + newRot * (-pivotOffset) - initialRot * (-pivotOffset) + initialPos
            // Simplified: P' = initialPos + initialRot * pivotOffset - newRot * pivotOffset
            transform.localPosition = initialLocalPosition
                + initialLocalRotation * pivotOffset
                - newRot * pivotOffset;
            transform.localRotation = newRot;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // If disabled mid-animation (e.g. PageManager refresh), snap immediately
            // so the page doesn't freeze at an intermediate angle.
            if (isAnimating)
            {
                currentAngle = targetAngle;
                isAnimating = false;
                angleVelocity = 0f;
                ApplyRotation();
            }
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            isAnimating = false;
            angleVelocity = 0f;
            smoothedAngularVelocity = 0f;
            grabSmoothVelocity = 0f;

            // The edge follows the hand's movement from here on; the offset absorbs any
            // difference between the edge's measured angle and the page's own angle.
            grabHandStartParent = transform.parent.InverseTransformPoint(args.interactorObject.transform.position);
            grabEdgeStartParent = transform.parent.InverseTransformPoint(EdgeGripPoint);
            lastRawAngle = AngleAroundSpine(grabEdgeStartParent);
            grabAngleOffset = lastRawAngle - currentAngle;

            pageManager?.OnPageGrabbed(this);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);

            // If the user flicked the page hard enough, complete the flip in
            // the velocity direction regardless of current position.
            // Otherwise fall back to the midpoint snap.
            bool flipForward;
            if (Mathf.Abs(smoothedAngularVelocity) >= velocityFlipThreshold)
            {
                flipForward = smoothedAngularVelocity > 0f;
            }
            else
            {
                float snapThreshold = (minAngle + maxAngle) / 2f;
                flipForward = currentAngle >= snapThreshold;
            }

            if (flipForward)
            {
                targetAngle = maxAngle;
                IsFlipped = true;
            }
            else
            {
                targetAngle = minAngle;
                IsFlipped = false;
            }

            isAnimating = true;
            pageManager?.OnPageReleased(this);
        }

        /// <summary>
        /// Sets the page to a specific angle immediately. Stops any ongoing animation.
        /// Updates IsFlipped based on the min/max midpoint threshold.
        /// Safe to call when the component is disabled.
        /// </summary>
        public void SetAngle(float angle)
        {
            currentAngle = angle;
            isAnimating = false;
            angleVelocity = 0f;
            ApplyRotation();
            IsFlipped = angle >= (minAngle + maxAngle) / 2f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Show the pivot point (spine edge) as a yellow sphere.
            Vector3 pivot;
            if (Application.isPlaying)
            {
                pivot = transform.parent.TransformPoint(spineEdgeParent);
            }
            else
            {
                // In edit mode, compute from serialized values.
                pivot = transform.TransformPoint(pivotOffset);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(pivot, 0.01f);

            // Draw hinge axis as a cyan line through the pivot.
            Vector3 worldHinge = transform.parent != null
                ? transform.parent.TransformDirection(hingeAxis)
                : transform.TransformDirection(hingeAxis);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pivot - worldHinge * 0.05f, pivot + worldHinge * 0.05f);

            // Draw zero-angle direction as a green ray from the pivot.
            Vector3 worldZeroDir = transform.parent != null
                ? transform.parent.TransformDirection(zeroAngleDirection)
                : transform.TransformDirection(zeroAngleDirection);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(pivot, worldZeroDir.normalized * 0.08f);
        }
#endif
    }
}
