using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Grab interactable for ArticulationBody objects (e.g., book pages).
    /// Uses velocity tracking to move the body while respecting articulation constraints.
    /// Grabs where the hand touches (dynamic attach) and floats in zero-g on release.
    /// </summary>
    [RequireComponent(typeof(ArticulationBody))]
    public class ArticulationGrabbable : XRBaseInteractable
    {
        [SerializeField] private GrabSettings grabSettings;

        private ArticulationBody artBody;
        private IXRSelectInteractor activeInteractor;
        private Vector3 grabPositionOffset;
        private Quaternion grabRotationOffset;

        protected override void Awake()
        {
            base.Awake();
            artBody = GetComponent<ArticulationBody>();

            if (grabSettings == null)
            {
                Debug.LogError("[ArticulationGrabbable] GrabSettings not assigned!", this);
                enabled = false;
            }
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            activeInteractor = args.interactorObject;

            // Store offset between hand and object at grab time (dynamic attach)
            var hand = activeInteractor.GetAttachTransform(this);
            grabPositionOffset = Quaternion.Inverse(hand.rotation) *
                                 (transform.position - hand.position);
            grabRotationOffset = Quaternion.Inverse(hand.rotation) * transform.rotation;

            // Root bodies become immovable during grab — we teleport them directly
            if (artBody.isRoot)
                artBody.immovable = true;

            // Haptic feedback
            if (args.interactorObject is XRBaseInputInteractor inputInteractor)
            {
                inputInteractor.SendHapticImpulse(
                    grabSettings.GrabHapticIntensity,
                    grabSettings.HapticDuration);
            }
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            activeInteractor = null;

            // Release: float in place
            if (artBody.isRoot)
                artBody.immovable = true; // Stay where released
            artBody.linearVelocity = Vector3.zero;
            artBody.angularVelocity = Vector3.zero;
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
                return;
            if (activeInteractor == null)
                return;

            var hand = activeInteractor.GetAttachTransform(this);
            var targetPos = hand.position + hand.rotation * grabPositionOffset;
            var targetRot = hand.rotation * grabRotationOffset;

            if (artBody.isRoot)
            {
                // Root body: teleport directly — entire chain follows
                artBody.TeleportRoot(targetPos, targetRot);
            }
            else
            {
                // Child body (page): apply force toward hand, solver converts to joint rotation
                var force = (targetPos - transform.position) * 10f / Time.deltaTime;
                artBody.AddForce(force, ForceMode.Force);
            }
        }
    }
}
