using System.Collections;
using PsycheVR.VR;
using PsycheVR.Modes;
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
        [Header("Memory transition")]
        [SerializeField] private BlinkTeleporter teleporter;
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
        private readonly StoryPhaseSequence sequence = new StoryPhaseSequence();
        private bool started;
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
            CancelMemoryTrip();
            if (phase != StoryPhase.Build || !showingMemoryDate)
                ClearMemoryDate();
            Debug.Log($"[StoryDirector] Entering {phase}.", this);
            PresentPhase(phase);
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
                dateCard.Show(storyText != null ? storyText.MemoryDateText : "2003", memoryDateAnchor);
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
            CancelMemoryTrip();
            ClearMemoryDate();
            UnsubscribeFromGrab();
            if (!started) return;
            if (dateCard != null) dateCard.Hide();
            if (objectiveHighlight != null) objectiveHighlight.Hide();
        }

        private void OnEnable()
        {
            if (started && GameModeManager.IsStory)
                PresentPhase(sequence.CurrentPhase);
        }

        private void OnDestroy()
        {
            UnsubscribeFromGrab();
            sequence.PhaseChanged -= EnterPhase;
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
