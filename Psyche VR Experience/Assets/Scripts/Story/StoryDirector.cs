using System.Collections;
using PsycheVR.Modes;
using PsycheVR.VR;
using PsycheVR.Kiosk;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
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
        [Tooltip("Rig teleporter used for the memory trip and the return to the desk.")]
        [SerializeField] private BlinkTeleporter teleporter;
        [Header("Memory transition")]
        [SerializeField] private BlinkTeleportAnchor bedroomArrival;
        [Tooltip("Optional date placement in the bedroom. Empty uses the current camera view.")]
        [SerializeField] private Transform memoryDateAnchor;
        [SerializeField, Min(0f)] private float memoryFadeOutDuration = 1.5f;
        [SerializeField, Min(0f)] private float memoryHoldDuration = 0.8f;
        [SerializeField, Min(0f)] private float memoryFadeInDuration = 1.5f;
        [SerializeField, Min(0f)] private float memoryDateDuration = 2.5f;
        private Coroutine pendingMemoryTrip;
        private Coroutine memoryTrip;
        private Coroutine dateRoutine;
        private bool showingMemoryDate;

        [Header("Phase 4: End the memory")]
        [Tooltip("Root of the bedroom puzzle; only its tagged snap zones count toward ChildLaunch.")]
        [SerializeField] private Transform bedroomPuzzle;
        [SerializeField] private ButtonController childLaunchButton;
        [SerializeField] private BlinkTeleportAnchor storyDeskReturn;
        [Min(0f)] [SerializeField] private float returnFadeSeconds = 1.5f;
        [Min(0f)] [SerializeField] private float returnHoldSeconds = 0.8f;

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
        private PuzzleCompletion buildPuzzle;
        private XRGrabInteractable introGrab;

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
            StopWatchingPuzzle();
            CancelMemoryTrip();
            if (phase != StoryPhase.Build || !showingMemoryDate)
                ClearMemoryDate();
            Debug.Log($"[StoryDirector] Entering {phase}.", this);
            PresentPhase(phase);
            if (phase == StoryPhase.Build)
                WatchBuildPuzzle();
        }

        private void WatchBuildPuzzle()
        {
            // The Kiosk watcher is gated off in Story mode. Reuse an active
            // scene-local watcher, or create one here without enabling the kiosk.
            foreach (var puzzle in FindObjectsByType<PuzzleCompletion>(FindObjectsSortMode.None))
            {
                if (puzzle.isActiveAndEnabled && puzzle.gameObject.scene == gameObject.scene
                    && puzzle.PuzzleRoot == bedroomPuzzle)
                {
                    buildPuzzle = puzzle;
                    break;
                }
            }
            if (buildPuzzle == null)
            {
                buildPuzzle = gameObject.AddComponent<PuzzleCompletion>();
                buildPuzzle.SetPuzzleRoot(bedroomPuzzle);
            }

            if (buildPuzzle.IsComplete)
                OnBuildCompleted();
            else
                buildPuzzle.Completed += OnBuildCompleted;
        }

        private void OnBuildCompleted()
        {
            if (CurrentPhase == StoryPhase.Build)
                Advance();
        }

        private void StopWatchingPuzzle()
        {
            if (buildPuzzle != null)
                buildPuzzle.Completed -= OnBuildCompleted;
            buildPuzzle = null;
        }

        private void PresentPhase(StoryPhase phase)
        {
            UnsubscribeFromGrab();
            if (phase == StoryPhase.Intro)
            {
                introGrab = introSpacecraft != null
                    ? introSpacecraft.GetComponent<XRGrabInteractable>() : null;
                if (introGrab != null)
                    introGrab.selectEntered.AddListener(OnIntroGrabbed);
                if (dateCard != null && storyText != null)
                    dateCard.Show(storyText.IntroDateText);
                if (objectiveHighlight != null)
                    objectiveHighlight.Show(introSpacecraft);
            }
            else
            {
                if (dateCard != null && !(phase == StoryPhase.Build && showingMemoryDate))
                    dateCard.Hide();
                if (objectiveHighlight != null) objectiveHighlight.Hide();
                if (phase == StoryPhase.Memory && Application.isPlaying)
                {
                    if (teleporter != null && bedroomArrival != null)
                        pendingMemoryTrip = StartCoroutine(StartMemoryTrip());
                    else
                        Debug.LogWarning("[StoryDirector] Memory requires a teleporter and bedroom arrival anchor.", this);
                }
            }

            UpdateLaunchButtons();
            if (phase != StoryPhase.Intro) UpdateLaunchHighlight();
            if (phase == StoryPhase.RealLaunch && deskReturnComplete && !realLaunchPressed && dateCard != null)
                dateCard.Show(storyText != null ? storyText.LaunchDateText : string.Empty);
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

        // The objective ring moves to whichever launch button the player should hit next (TG-360): the bedroom
        // button in ChildLaunch, the mission-control button once the return trip has landed.
        private void UpdateLaunchHighlight()
        {
            if (objectiveHighlight == null) return;
            ButtonController target =
                CurrentPhase == StoryPhase.ChildLaunch ? childLaunchButton
                : CurrentPhase == StoryPhase.RealLaunch && !returningToDesk && !realLaunchPressed ? realLaunchButton
                : null;
            if (target != null) objectiveHighlight.Show(target.PressTarget);
            else objectiveHighlight.Hide();
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
            teleporter.TeleportTo(storyDeskReturn, returnFadeSeconds,
                returnHoldSeconds, returnFadeSeconds, FinishDeskReturn, AbandonDeskReturn);
        }

        // A replaced or cancelled return trip never arrives; unlock the real launch
        // anyway so the story cannot stay stuck in RealLaunch.
        private void AbandonDeskReturn()
        {
            if (this == null) return;
            returningToDesk = false;
            if (isActiveAndEnabled && GameModeManager.IsStory && CurrentPhase == StoryPhase.RealLaunch)
            {
                UpdateLaunchButtons();
                UpdateLaunchHighlight();
            }
        }

        private void FinishDeskReturn()
        {
            if (this == null) return;
            returningToDesk = false;
            deskReturnComplete = true;
            if (!isActiveAndEnabled || !GameModeManager.IsStory || CurrentPhase != StoryPhase.RealLaunch)
                return;
            if (dateCard != null) dateCard.Show(storyText != null ? storyText.LaunchDateText : string.Empty);
            UpdateLaunchButtons();
            UpdateLaunchHighlight();
        }

        private void HandleRealLaunchPressed()
        {
            if (!isActiveAndEnabled || !GameModeManager.IsStory || CurrentPhase != StoryPhase.RealLaunch)
                return;
            realLaunchPressed = true;
            if (realLaunchButton != null) realLaunchButton.SetStoryInteractionAllowed(false);
            if (dateCard != null) dateCard.Hide();
            if (objectiveHighlight != null) objectiveHighlight.Hide();
        }

        private void SubscribeLaunchEvents()
        {
            if (launchEventsSubscribed)
                return;

            if (launchVideo != null) launchVideo.OnVideoCompleted.AddListener(HandleLaunchVideoCompleted);
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

        private IEnumerator StartMemoryTrip()
        {
            // Leave the XRI selectEntered dispatch before force-releasing the hands.
            yield return null;
            pendingMemoryTrip = null;
            if (!GameModeManager.IsStory || teleporter == null || bedroomArrival == null || !teleporter.isActiveAndEnabled)
                yield break;
            memoryTrip = teleporter.TeleportTo(bedroomArrival, memoryFadeOutDuration,
                memoryHoldDuration, memoryFadeInDuration, OnMemoryArrived);
        }

        private void OnMemoryArrived()
        {
            memoryTrip = null;
            if (!isActiveAndEnabled || !GameModeManager.IsStory || CurrentPhase != StoryPhase.Memory)
                return;

            showingMemoryDate = true;
            if (dateCard != null)
                dateCard.Show(storyText != null ? storyText.BuildDateText : string.Empty, memoryDateAnchor);
            Advance(); // Memory is only the trip; keep its arrival card visible in Build.
            dateRoutine = StartCoroutine(HideMemoryDate());
        }

        private IEnumerator HideMemoryDate()
        {
            yield return new WaitForSecondsRealtime(memoryDateDuration);
            dateRoutine = null;
            showingMemoryDate = false;
            if (dateCard != null) dateCard.Hide();
        }

        private void CancelMemoryTrip()
        {
            if (pendingMemoryTrip != null) StopCoroutine(pendingMemoryTrip);
            pendingMemoryTrip = null;
            if (teleporter != null) teleporter.CancelTeleport(memoryTrip);
            memoryTrip = null;
        }

        private void ClearMemoryDate()
        {
            if (dateRoutine != null) StopCoroutine(dateRoutine);
            dateRoutine = null;
            showingMemoryDate = false;
        }

        private void OnDisable()
        {
            StopWatchingPuzzle();
            CancelMemoryTrip();
            ClearMemoryDate();
            UnsubscribeFromGrab();
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
                if (CurrentPhase == StoryPhase.Build)
                    WatchBuildPuzzle();
            }
        }

        private void OnDestroy()
        {
            StopWatchingPuzzle();
            UnsubscribeFromGrab();
            sequence.PhaseChanged -= EnterPhase;
            UnsubscribeLaunchEvents();
        }

        private void OnIntroGrabbed(SelectEnterEventArgs args)
        {
            if (!started || !isActiveAndEnabled || !GameModeManager.IsStory ||
                CurrentPhase != StoryPhase.Intro)
                return;

            // Entering Memory hides the objective and removes this listener.
            Advance();
        }

        private void UnsubscribeFromGrab()
        {
            if (introGrab != null)
                introGrab.selectEntered.RemoveListener(OnIntroGrabbed);
            introGrab = null;
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
