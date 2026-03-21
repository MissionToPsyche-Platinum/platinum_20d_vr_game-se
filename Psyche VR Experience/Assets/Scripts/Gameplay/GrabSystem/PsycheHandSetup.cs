using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Configures the NearFarInteractor on each hand controller and creates
    /// a HoldPoint child at a fixed distance in front of the controller.
    /// Grabbed objects target this point via XRI's VelocityTracking.
    /// Place on each hand's controller GameObject in the XR rig.
    /// </summary>
    public class PsycheHandSetup : MonoBehaviour
    {
        [Tooltip("Distance in front of controller where grabbed objects are held (meters).")]
        [SerializeField] private float holdDistance = 0.25f;

        [Tooltip("NearFarInteractor on this hand. Auto-found in children if not assigned.")]
        [SerializeField] private NearFarInteractor nearFarInteractor;

        private Transform _holdPoint;

        private void Awake()
        {
            if (nearFarInteractor == null)
                nearFarInteractor = GetComponentInChildren<NearFarInteractor>();

            if (nearFarInteractor == null)
            {
                Debug.LogError(
                    $"[PsycheHandSetup] No NearFarInteractor found on {gameObject.name}.",
                    this
                );
                enabled = false;
                return;
            }

            CreateHoldPoint();
            ConfigureInteractor();
        }

        private void CreateHoldPoint()
        {
            var holdPointGO = new GameObject("[HoldPoint]");
            _holdPoint = holdPointGO.transform;
            _holdPoint.SetParent(nearFarInteractor.transform, worldPositionStays: false);
            _holdPoint.localPosition = new Vector3(0f, 0f, holdDistance);
        }

        private void ConfigureInteractor()
        {
            nearFarInteractor.attachTransform = _holdPoint;
        }
    }
}
