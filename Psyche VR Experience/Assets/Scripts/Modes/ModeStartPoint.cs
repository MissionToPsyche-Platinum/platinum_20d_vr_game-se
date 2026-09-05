using UnityEngine;

namespace PsycheVR.Modes
{
    /// <summary>
    /// Marks where the player starts in one <see cref="GameMode"/>. Place one per mode
    /// in the master scene; its position and forward direction become the XR Origin's
    /// position and facing. Read by <see cref="ModeSpawnPlacer"/> on scene load.
    /// </summary>
    public sealed class ModeStartPoint : MonoBehaviour
    {
        [Tooltip("Mode that starts here.")]
        [SerializeField] private GameMode mode = GameMode.Story;

        /// <summary>Mode that starts at this transform.</summary>
        public GameMode Mode => mode;

        private const float GizmoRadius = 0.25f;
        private const float GizmoForwardLength = 0.6f;

        private void OnDrawGizmos()
        {
            Gizmos.color = mode == GameMode.Story ? Color.cyan : Color.magenta;
            Gizmos.DrawWireSphere(transform.position, GizmoRadius);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * GizmoForwardLength);
        }
    }
}
