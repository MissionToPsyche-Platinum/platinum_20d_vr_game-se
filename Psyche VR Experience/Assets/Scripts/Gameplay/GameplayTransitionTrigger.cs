using UnityEngine;
using UnityEngine.Events;
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

        [Header("Events")]
        [SerializeField] private UnityEvent onTriggered = new UnityEvent();

        public void TriggerTransition()
        {
            onTriggered.Invoke();

            FadeManager fadeManager = FadeManager.GetOrCreate();
            if (useBuildIndex)
            {
                fadeManager.LoadSceneWithFade(sceneBuildIndex, fadeOutDuration, holdDuration, fadeInDuration);
                return;
            }

            if (!string.IsNullOrWhiteSpace(sceneName))
            {
                fadeManager.LoadSceneWithFade(sceneName, fadeOutDuration, holdDuration, fadeInDuration);
                return;
            }

            fadeManager.FadePulse(fadeOutDuration, holdDuration, fadeInDuration);
        }
    }
}
