using UnityEngine;
using UnityEngine.SceneManagement;

namespace PsycheVR.VR
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public class SceneTransitionSpawnApplier : MonoBehaviour
    {
        private void Start()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (!PsycheVR.Gameplay.SceneTransitionSpawnState.TryConsume(sceneName, out Vector3 rigPosition, out float yawDegrees))
                return;

            transform.SetPositionAndRotation(rigPosition, Quaternion.Euler(0f, yawDegrees, 0f));
        }
    }
}
