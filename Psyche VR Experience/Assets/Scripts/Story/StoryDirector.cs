using PsycheVR.Modes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace PsycheVR.Story
{
    /// <summary>
    /// Scene-local story coordinator. Mode switches reload Bedroom, creating a
    /// fresh sequence. Intro delegates presentation to the date card and highlight.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryDirector : MonoBehaviour
    {
        [SerializeField] private StoryText storyText;
        [SerializeField] private StoryDateCard dateCard;
        [SerializeField] private ObjectiveHighlight objectiveHighlight;
        [Tooltip("Assign the model spacecraft when the story target is finalized.")]
        [SerializeField] private Transform introSpacecraft;

        [Header("Phase 5: Real launch")]
        [Tooltip("Mission-control launch button. Its video target must be the launch video below.")]
        [SerializeField] private ButtonController realLaunchButton;
        [Tooltip("Only completion from this player can finish the real-launch phase.")]
        [SerializeField] private play_video launchVideo;
        private readonly StoryPhaseSequence sequence = new StoryPhaseSequence();
        private bool started;
        private bool launchEventsSubscribed;

        public StoryPhase CurrentPhase => sequence.CurrentPhase;

        private void Start()
        {
            if (!GameModeManager.IsStory || started)
                return;

            started = true;
            if (storyText == null)
                storyText = Resources.Load<StoryText>(StoryText.AssetName);
            sequence.PhaseChanged += EnterPhase;
            SubscribeLaunchEvents();
            // Intro is the initial state, so it does not raise PhaseChanged.
            EnterPhase(sequence.CurrentPhase);
        }

        public void Advance()
        {
            if (started && isActiveAndEnabled && GameModeManager.IsStory)
                sequence.Advance();
        }

        public void GoTo(StoryPhase phase)
        {
            if (started && isActiveAndEnabled && GameModeManager.IsStory)
                sequence.GoTo(phase);
        }

        private void EnterPhase(StoryPhase phase)
        {
            Debug.Log($"[StoryDirector] Entering {phase}.", this);
            PresentPhase(phase);
        }

        private void PresentPhase(StoryPhase phase)
        {
            if (phase == StoryPhase.Intro)
            {
                if (dateCard != null && storyText != null)
                    dateCard.Show(storyText.IntroDateText);
                if (objectiveHighlight != null)
                    objectiveHighlight.Show(introSpacecraft);
            }
            else
            {
                if (dateCard != null) dateCard.Hide();
                if (objectiveHighlight != null) objectiveHighlight.Hide();
            }

            if (phase == StoryPhase.RealLaunch && realLaunchButton != null)
                realLaunchButton.UnlockButton();
        }

        private void SubscribeLaunchEvents()
        {
            if (launchEventsSubscribed || launchVideo == null)
                return;

            launchVideo.OnVideoCompleted.AddListener(HandleLaunchVideoCompleted);
            launchEventsSubscribed = true;
        }

        private void UnsubscribeLaunchEvents()
        {
            if (!launchEventsSubscribed)
                return;

            if (launchVideo != null)
                launchVideo.OnVideoCompleted.RemoveListener(HandleLaunchVideoCompleted);
            launchEventsSubscribed = false;
        }

        private void HandleLaunchVideoCompleted()
        {
            // Ignore unrelated/late completion events and repeated notifications.
            if (sequence.CurrentPhase == StoryPhase.RealLaunch)
                Advance();
        }

        private void OnDisable()
        {
            if (!started) return;
            UnsubscribeLaunchEvents();
            if (dateCard != null) dateCard.Hide();
            if (objectiveHighlight != null) objectiveHighlight.Hide();
        }

        private void OnEnable()
        {
            if (started && GameModeManager.IsStory)
            {
                SubscribeLaunchEvents();
                PresentPhase(sequence.CurrentPhase);
            }
        }

        private void OnDestroy()
        {
            sequence.PhaseChanged -= EnterPhase;
            UnsubscribeLaunchEvents();
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (!GameModeManager.IsStory)
                return;

            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
                Advance();
        }
#endif
    }
}
