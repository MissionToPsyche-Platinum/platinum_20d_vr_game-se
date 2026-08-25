using UnityEngine;

namespace PsycheVR.VR
{
    [DisallowMultipleComponent]
    public class BlinkTeleportAnchor : MonoBehaviour
    {
        [Tooltip("If enabled, Yaw returns YawOverride. If disabled, Yaw comes from the transform's rotation.")]
        [SerializeField] private bool useYawOverride;

        [Tooltip("Yaw (Y-axis rotation, degrees) used when UseYawOverride is enabled.")]
        [SerializeField] private float yawOverride;

        public Vector3 Position => transform.position;

        public float Yaw => useYawOverride ? yawOverride : transform.rotation.eulerAngles.y;
    }
}
