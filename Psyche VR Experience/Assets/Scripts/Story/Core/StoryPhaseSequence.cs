using System;

namespace PsycheVR.Story
{
    public enum StoryPhase
    {
        Intro,
        Memory,
        Build,
        ChildLaunch,
        RealLaunch,
        FreePlay
    }

    /// <summary>
    /// Scene-independent story progression. The director handles phase effects
    /// and checks the active game mode before requesting a transition.
    /// </summary>
    public sealed class StoryPhaseSequence
    {
        public StoryPhase CurrentPhase { get; private set; } = StoryPhase.Intro;

        /// <summary>Raised once after a change, with the newly entered phase.</summary>
        public event Action<StoryPhase> PhaseChanged;

        /// <summary>Advances one phase; FreePlay is terminal.</summary>
        public void Advance()
        {
            if (CurrentPhase != StoryPhase.FreePlay)
                GoTo((StoryPhase)((int)CurrentPhase + 1));
        }

        /// <summary>Jumps to a phase. Selecting the current phase is a no-op.</summary>
        public void GoTo(StoryPhase phase)
        {
            if (!Enum.IsDefined(typeof(StoryPhase), phase))
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown story phase.");

            if (phase == CurrentPhase)
                return;

            CurrentPhase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
