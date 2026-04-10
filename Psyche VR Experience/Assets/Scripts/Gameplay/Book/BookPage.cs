using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Interactive book page using the XRLever pattern.
    /// Drives localRotation directly from hand position — no physics, no joints.
    /// </summary>
    public class BookPage : XRBaseInteractable
    {
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

        private const float SnapConvergenceThreshold = 0.5f;

        private float currentAngle;
        private float targetAngle;
        private float angleVelocity;
        private bool isAnimating;
        private float grabAngleOffset; // delta between hand angle and page angle at grab start
        private Quaternion initialLocalRotation;
        private Vector3 initialLocalPosition;
        private Vector3 spineEdgeParent; // spine edge position in parent space
        private Vector3 zeroAngleDir;
        private Vector3 perpendicularDir;
        private PageManager pageManager;

        /// <summary>Whether this page is currently on the flipped (left) side.</summary>
        public bool IsFlipped { get; private set; }

        protected override void Awake()
        {
            // Register own collider BEFORE base.Awake() so XRI skips auto-discovery
            // and doesn't claim parent's (spine) colliders for this interactable.
            var col = GetComponent<Collider>();
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
            float handAngle = ComputeHandAngle(interactorsSelecting[0]);
            float rawAngle = handAngle - grabAngleOffset;

            // Prevent wrap-around snap when hand crosses the 0/180 boundary.
            if (rawAngle < minAngle - 10f || rawAngle > maxAngle + 10f)
                rawAngle = currentAngle > (minAngle + maxAngle) / 2f ? maxAngle : minAngle;

            currentAngle = Mathf.Clamp(rawAngle, minAngle, maxAngle);
            ApplyRotation();
        }

        /// <summary>
        /// Compute the absolute angle of the hand relative to the spine edge
        /// on the hinge plane. Returns degrees.
        /// </summary>
        private float ComputeHandAngle(IXRSelectInteractor interactor)
        {
            Vector3 handPos = interactor.GetAttachTransform(this).position;

            // Direction from spine edge to hand, in parent's local space.
            Vector3 pivotToHand = transform.parent.InverseTransformPoint(handPos)
                                  - spineEdgeParent;

            // Project onto the plane perpendicular to the hinge axis.
            float h = Vector3.Dot(pivotToHand, hingeAxis);
            Vector3 projected = pivotToHand - h * hingeAxis;
            if (projected.sqrMagnitude < 0.0001f) return currentAngle + grabAngleOffset;
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

            // Cache the offset between where the hand is and where the page is,
            // so the page starts moving immediately with no jump or dead zone.
            float handAngle = ComputeHandAngle(args.interactorObject);
            grabAngleOffset = handAngle - currentAngle;

            pageManager?.OnPageGrabbed(this);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);

            // Snap to nearest rest angle.
            float snapThreshold = (minAngle + maxAngle) / 2f;
            if (currentAngle >= snapThreshold)
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
