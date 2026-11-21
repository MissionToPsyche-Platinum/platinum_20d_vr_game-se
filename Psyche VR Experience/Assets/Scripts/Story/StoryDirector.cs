using PsycheVR.Modes;
using PsycheVR.Kiosk;
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
        private readonly StoryPhaseSequence sequence = new StoryPhaseSequence();
        private bool started;
        private PuzzleCompletion buildPuzzle;

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
            StopWatchingPuzzle();
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
                if (puzzle.isActiveAndEnabled && puzzle.gameObject.scene == gameObject.scene)
                {
                    buildPuzzle = puzzle;
                    break;
                }
            }
            if (buildPuzzle == null)
                buildPuzzle = gameObject.AddComponent<PuzzleCompletion>();

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
        }

        private void OnDisable()
        {
            StopWatchingPuzzle();
            if (!started) return;
            if (dateCard != null) dateCard.Hide();
            if (objectiveHighlight != null) objectiveHighlight.Hide();
        }

        private void OnEnable()
        {
            if (started && GameModeManager.IsStory)
            {
                PresentPhase(sequence.CurrentPhase);
                if (CurrentPhase == StoryPhase.Build)
                    WatchBuildPuzzle();
            }
        }

        private void OnDestroy()
        {
            StopWatchingPuzzle();
            sequence.PhaseChanged -= EnterPhase;
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
