using System;

namespace PsycheVR.OpsConsole.Core
{
    /// <summary>
    /// The mission ops monitor's state, free of Unity objects: which tab shows, power, the ping lock
    /// and the idle "attract" cycle. Paging is refused while the screen is off or a ping is in flight.
    /// Any accepted input restarts the idle clock and ends attract mode. Time comes in through
    /// <see cref="Tick"/>, so tests drive it directly.
    /// </summary>
    public sealed class ConsoleState
    {
        private readonly float _idleSeconds;
        private readonly float _attractStepSeconds;
        private float _idle;
        private float _attractClock;

        /// <summary>Creates a powered-on console showing the first tab.</summary>
        /// <param name="tabCount">Number of tabs; paging wraps within this range.</param>
        /// <param name="idleSeconds">Seconds without input before attract mode starts.</param>
        /// <param name="attractStepSeconds">Seconds between page turns while attract mode runs.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="tabCount"/> is below 1, <paramref name="idleSeconds"/> is negative,
        /// or <paramref name="attractStepSeconds"/> is not positive.
        /// </exception>
        public ConsoleState(int tabCount, float idleSeconds, float attractStepSeconds)
        {
            if (tabCount < 1)
                throw new ArgumentOutOfRangeException(nameof(tabCount), tabCount, "Need at least one tab.");
            if (idleSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(idleSeconds), idleSeconds, "Must not be negative.");
            if (attractStepSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(attractStepSeconds), attractStepSeconds, "Must be positive.");
            TabCount = tabCount;
            _idleSeconds = idleSeconds;
            _attractStepSeconds = attractStepSeconds;
            IsOn = true;
        }

        /// <summary>Number of tabs the screen pages through.</summary>
        public int TabCount { get; }

        /// <summary>Zero-based index of the tab on screen.</summary>
        public int CurrentTab { get; private set; }

        /// <summary>True while the screen is powered on.</summary>
        public bool IsOn { get; private set; }

        /// <summary>True between <see cref="StartPing"/> and <see cref="CompletePing"/> (or power off).</summary>
        public bool PingInFlight { get; private set; }

        /// <summary>True while the idle attract cycle is turning pages on its own.</summary>
        public bool IsAttracting { get; private set; }

        /// <summary>True when paging is allowed right now.</summary>
        public bool CanPage => IsOn && !PingInFlight;

        /// <summary>Next tab, wrapping. Returns false (no change) while off or pinging.</summary>
        public bool Next() => Step(+1);

        /// <summary>Previous tab, wrapping. Returns false (no change) while off or pinging.</summary>
        public bool Previous() => Step(-1);

        /// <summary>Power on keeps the current tab; power off cancels a ping and attract mode.</summary>
        public void SetPower(bool on)
        {
            IsOn = on;
            PingInFlight = false;
            EndAttract();
        }

        /// <summary>Starts a ping and jumps to <paramref name="dsnTab"/>. False while off or already pinging.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="dsnTab"/> is outside 0..TabCount-1.</exception>
        public bool StartPing(int dsnTab)
        {
            if (dsnTab < 0 || dsnTab >= TabCount)
                throw new ArgumentOutOfRangeException(nameof(dsnTab), dsnTab, "Not a valid tab index.");
            if (!IsOn || PingInFlight) return false;
            CurrentTab = dsnTab;
            PingInFlight = true;
            EndAttract();
            return true;
        }

        /// <summary>The reply has landed: paging works again.</summary>
        public void CompletePing()
        {
            PingInFlight = false;
            EndAttract();
        }

        /// <summary>
        /// Advances the idle clock. Returns true when attract mode turned the page this tick.
        /// The clock does not run while off or pinging. At most one page turns per call; time past
        /// one step carries into later calls rather than turning extra pages (fine at frame deltas).
        /// </summary>
        public bool Tick(float seconds)
        {
            if (!IsOn || PingInFlight) return false;
            if (!IsAttracting)
            {
                _idle += seconds;
                if (_idle >= _idleSeconds)
                {
                    IsAttracting = true;
                    _attractClock = 0f;
                }
                return false;
            }
            _attractClock += seconds;
            if (_attractClock < _attractStepSeconds) return false;
            _attractClock -= _attractStepSeconds;
            CurrentTab = (CurrentTab + 1) % TabCount;
            return true;
        }

        private bool Step(int direction)
        {
            if (!CanPage) return false;
            CurrentTab = (CurrentTab + direction + TabCount) % TabCount;
            EndAttract();
            return true;
        }

        private void EndAttract()
        {
            IsAttracting = false;
            _idle = 0f;
            _attractClock = 0f;
        }
    }
}
