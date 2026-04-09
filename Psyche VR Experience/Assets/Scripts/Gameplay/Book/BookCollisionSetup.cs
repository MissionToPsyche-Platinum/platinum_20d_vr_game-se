using UnityEngine;

namespace PsycheVR.Gameplay
{
    /// <summary>
    /// Disables physics collisions between every pair of colliders found under this GameObject.
    /// Used on the InstructionManualBook root to prevent self-collision chaos between the
    /// articulated cover/spine/page pieces while preserving collisions with the world and hands.
    ///
    /// Runs on every OnEnable because Physics.IgnoreCollision pairs reset whenever a collider
    /// is disabled and re-enabled (per Unity docs). Re-applying on enable is cheap (n*(n-1)/2
    /// pairs for n colliders, n=8 -> 28 pairs).
    /// </summary>
    public class BookCollisionSetup : MonoBehaviour
    {
        private void OnEnable()
        {
            var colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
            {
                for (int j = i + 1; j < colliders.Length; j++)
                {
                    Physics.IgnoreCollision(colliders[i], colliders[j], true);
                }
            }
        }
    }
}
