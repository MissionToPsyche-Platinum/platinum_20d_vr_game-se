using PsycheVR.Modes;
using PsycheVR.VR;
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

        [Header("Phase 4: End the memory")]
        [Tooltip("Root of the bedroom puzzle; only its tagged snap zones count toward ChildLaunch.")]
        [SerializeField] private Transform bedroomPuzzle;
        [SerializeField] private ButtonController childLaunchButton;
        [SerializeField] private BlinkTeleporter teleporter;
        [SerializeField] private BlinkTeleportAnchor storyDeskReturn;
        [Min(0f)] [SerializeField] private float returnFadeSeconds = 1.5f;

        [Header("Phase 5: Real launch")]
        [Tooltip("Mission-control launch button. Its video target must be the launch video below.")]
        [SerializeField] private ButtonController realLaunchButton;
        [Tooltip("Only completion from this player can finish the real-launch phase.")]
        [SerializeField] private play_video launchVideo;
        private readonly StoryPhaseSequence sequence = new StoryPhaseSequence();
        private bool started;
        private bool launchEventsSubscribed;
        private bool returningToDesk;
        private bool realLaunchPressed;
        private bool deskReturnComplete;

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
            if (phase == StoryPhase.ChildLaunch)
            {
                realLaunchPressed = false;
                deskReturnComplete = false;
            }
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

            UpdateLaunchButtons();
            if (phase == StoryPhase.RealLaunch && deskReturnComplete && !realLaunchPressed && dateCard != null)
                dateCard.Show("October 2023");
            if (phase == StoryPhase.Build) CheckPuzzleCompleted();
        }

        private void UpdateLaunchButtons()
        {
            if (childLaunchButton != null)
            {
                bool allowed = CurrentPhase == StoryPhase.ChildLaunch;
                childLaunchButton.SetStoryInteractionAllowed(allowed);
                if (allowed) childLaunchButton.UnlockButton();
            }
            if (realLaunchButton != null)
            {
                bool allowed = CurrentPhase == StoryPhase.RealLaunch && !returningToDesk && !realLaunchPressed;
                realLaunchButton.SetStoryInteractionAllowed(allowed);
                if (allowed) realLaunchButton.UnlockButton();
            }
        }

        private void HandlePuzzleSnapped(SnapZone zone)
        {
            if (bedroomPuzzle != null && zone != null && zone.transform.IsChildOf(bedroomPuzzle))
                CheckPuzzleCompleted();
        }

        private void CheckPuzzleCompleted()
        {
            if (CurrentPhase != StoryPhase.Build || bedroomPuzzle == null) return;
            int required = 0;
            foreach (var zone in bedroomPuzzle.GetComponentsInChildren<SnapZone>(true))
            {
                if (string.IsNullOrEmpty(zone.requiredTag)) continue;
                required++;
                if (!zone.hasSnapped) return;
            }
            if (required > 0) Advance();
        }

        private void HandleChildLaunchPressed()
        {
            if (!isActiveAndEnabled || !GameModeManager.IsStory || CurrentPhase != StoryPhase.ChildLaunch)
                return;
            if (teleporter == null || storyDeskReturn == null || !teleporter.isActiveAndEnabled)
            {
                Debug.LogError("[StoryDirector] Child launch requires an active teleporter and StoryDeskReturn anchor.", this);
                childLaunchButton.UnlockButton();
                return;
            }

            returningToDesk = true;
            Advance();
            teleporter.TeleportToSlow(storyDeskReturn, returnFadeSeconds, FinishDeskReturn);
        }

        private void FinishDeskReturn()
        {
            if (this == null) return;
            returningToDesk = false;
            deskReturnComplete = true;
            if (!isActiveAndEnabled || !GameModeManager.IsStory || CurrentPhase != StoryPhase.RealLaunch)
                return;
            if (dateCard != null) dateCard.Show("October 2023");
            UpdateLaunchButtons();
        }

        private void HandleRealLaunchPressed()
        {
            if (!isActiveAndEnabled || !GameModeManager.IsStory || CurrentPhase != StoryPhase.RealLaunch)
                return;
            realLaunchPressed = true;
            if (realLaunchButton != null) realLaunchButton.SetStoryInteractionAllowed(false);
            if (dateCard != null) dateCard.Hide();
        }

        private void SubscribeLaunchEvents()
        {
            if (launchEventsSubscribed)
                return;

            if (launchVideo != null) launchVideo.OnVideoCompleted.AddListener(HandleLaunchVideoCompleted);
            SnapZone.Snapped += HandlePuzzleSnapped;
            if (childLaunchButton != null) childLaunchButton.onPressed.AddListener(HandleChildLaunchPressed);
            if (realLaunchButton != null) realLaunchButton.onPressed.AddListener(HandleRealLaunchPressed);
            launchEventsSubscribed = true;
        }

        private void UnsubscribeLaunchEvents()
        {
            if (!launchEventsSubscribed)
                return;

            if (launchVideo != null)
                launchVideo.OnVideoCompleted.RemoveListener(HandleLaunchVideoCompleted);
            SnapZone.Snapped -= HandlePuzzleSnapped;
            if (childLaunchButton != null) childLaunchButton.onPressed.RemoveListener(HandleChildLaunchPressed);
            if (realLaunchButton != null) realLaunchButton.onPressed.RemoveListener(HandleRealLaunchPressed);
            launchEventsSubscribed = false;
        }

        private void HandleLaunchVideoCompleted()
        {
            // Ignore unrelated/late completion events and repeated notifications.
            if (sequence.CurrentPhase == StoryPhase.RealLaunch && !returningToDesk)
                Advance();
        }

        private void OnDisable()
        {
            if (!started) return;
            UnsubscribeLaunchEvents();
            if (childLaunchButton != null) childLaunchButton.SetStoryInteractionAllowed(false);
            if (realLaunchButton != null) realLaunchButton.SetStoryInteractionAllowed(false);
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
