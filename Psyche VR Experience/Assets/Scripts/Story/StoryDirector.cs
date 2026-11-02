using PsycheVR.Modes;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace PsycheVR.Story
{
    /// <summary>
    /// Scene-local story coordinator. Mode switches reload Bedroom, creating a
    /// fresh sequence. Phase presentation is added by the Phase 1 integration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryDirector : MonoBehaviour
    {
        private readonly StoryPhaseSequence sequence = new StoryPhaseSequence();
        private bool started;

        public StoryPhase CurrentPhase => sequence.CurrentPhase;

        private void Start()
        {
            if (!GameModeManager.IsStory || started)
                return;

            started = true;
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
            Debug.Log($"[StoryDirector] Entering {phase}.", this);
        }

        private void OnDestroy()
        {
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
