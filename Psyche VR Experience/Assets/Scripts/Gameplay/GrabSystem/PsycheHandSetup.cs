using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Configures the NearFarInteractor on each hand controller: grabbed objects attach at
    /// the hand's palm anchor (falling back to a generated HoldPoint), and none of the
    /// hand's casters can hit the hand's own collider proxy on the Hand layer.
    /// Place on each hand's controller GameObject in the XR rig.
    /// </summary>
    public class PsycheHandSetup : MonoBehaviour
    {
        [Tooltip("Where grabbed objects attach. Assign the hand model's PalmAnchor. If empty, a HoldPoint is created holdDistance ahead of the controller.")]
        [SerializeField] private Transform holdPoint;

        [Tooltip("Fallback only: distance in front of the controller for the generated HoldPoint (metres).")]
        [SerializeField] private float holdDistance = 0.05f;

        [Tooltip("NearFarInteractor on this hand. Auto-found in children if not assigned.")]
        [SerializeField] private NearFarInteractor nearFarInteractor;

        /// <summary>Layer for large static-ish furniture colliders (the office desk carcass and drawer boxes) that are never grab targets.</summary>
        public const string FurnitureLayerName = "Furniture";

        private void Awake()
        {
            if (nearFarInteractor == null)
                nearFarInteractor = GetComponentInChildren<NearFarInteractor>();

            if (nearFarInteractor == null)
            {
                Debug.LogError($"[PsycheHandSetup] No NearFarInteractor found on {gameObject.name}.", this);
                enabled = false;
                return;
            }

            if (holdPoint == null)
                holdPoint = CreateHoldPoint();

            // The hand models are rolled +-90 degrees about the controller's forward axis so the
            // palms face inward. The hold point sits under the palm, so it inherited that roll and
            // twisted every fixed-pose grab (the book). Keep its rotation on the controller's.
            holdPoint.rotation = transform.rotation;

            nearFarInteractor.attachTransform = holdPoint;
            ExcludeHandLayer();
        }

        private Transform CreateHoldPoint()
        {
            var holdPointGO = new GameObject("[HoldPoint]");
            var t = holdPointGO.transform;
            t.SetParent(nearFarInteractor.transform, worldPositionStays: false);
            t.localPosition = new Vector3(0f, 0f, holdDistance);
            return t;
        }

        /// <summary>
        /// The hand collider sits right at the ray origin; without this every far ray and
        /// near sphere would hit the hand itself first. The near sphere also skips the
        /// <see cref="FurnitureLayerName"/> layer: XRI's sphere keeps only 10 colliders, and inside
        /// a desk drawer the drawer walls and desk body used them all, so the prop was never found.
        /// </summary>
        private void ExcludeHandLayer()
        {
            int hand = LayerMask.NameToLayer(HandPhysics.LayerName);
            if (hand < 0)
                return;

            int keep = ~(1 << hand);
            int furniture = LayerMask.NameToLayer(FurnitureLayerName);
            int keepNear = furniture >= 0 ? keep & ~(1 << furniture) : keep;

            foreach (var caster in GetComponentsInChildren<CurveInteractionCaster>(true))
                caster.raycastMask &= keep;

            foreach (var caster in GetComponentsInChildren<SphereInteractionCaster>(true))
                caster.physicsLayerMask &= keepNear;

            foreach (var ray in GetComponentsInChildren<XRRayInteractor>(true))
                ray.raycastMask &= keep;
        }
    }
}
