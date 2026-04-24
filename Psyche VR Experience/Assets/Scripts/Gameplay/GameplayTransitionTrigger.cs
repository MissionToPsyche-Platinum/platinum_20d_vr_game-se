using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using PsycheVR.UI;

namespace PsycheVR.Gameplay
{
    public class GameplayTransitionTrigger : MonoBehaviour
    {
        [Header("Transition Target")]
        [SerializeField] private string sceneName;
        [SerializeField] private int sceneBuildIndex = -1;
        [SerializeField] private bool useBuildIndex;

        [Header("Fade Timing")]
        [SerializeField] private float fadeOutDuration = 0.35f;
        [SerializeField] private float holdDuration = 0.05f;
        [SerializeField] private float fadeInDuration = 0.35f;

        [Header("Spawn Override")]
        [SerializeField] private bool applySpawnOverride;
        [SerializeField] private Vector3 targetRigPosition;
        [SerializeField] private float targetYawDegrees;

        [Header("Events")]
        [SerializeField] private UnityEvent onTriggered = new UnityEvent();

        public void TriggerTransition()
        {
            onTriggered.Invoke();

            FadeManager fadeManager = FadeManager.GetOrCreate();
            if (useBuildIndex)
            {
                SetPendingSpawnOverride(GetSceneNameFromBuildIndex(sceneBuildIndex));
                fadeManager.LoadSceneWithFade(sceneBuildIndex, fadeOutDuration, holdDuration, fadeInDuration);
                return;
            }

            if (!string.IsNullOrWhiteSpace(sceneName))
            {
                SetPendingSpawnOverride(sceneName);
                fadeManager.LoadSceneWithFade(sceneName, fadeOutDuration, holdDuration, fadeInDuration);
                return;
            }

            fadeManager.FadePulse(fadeOutDuration, holdDuration, fadeInDuration);
        }

        private void SetPendingSpawnOverride(string targetScene)
        {
            if (!applySpawnOverride || string.IsNullOrWhiteSpace(targetScene))
                return;

            SceneTransitionSpawnState.SetPending(targetScene, targetRigPosition, targetYawDegrees);
        }

        private static string GetSceneNameFromBuildIndex(int buildIndex)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
            if (string.IsNullOrWhiteSpace(scenePath))
                return string.Empty;

            return System.IO.Path.GetFileNameWithoutExtension(scenePath);
        }
    }
}
