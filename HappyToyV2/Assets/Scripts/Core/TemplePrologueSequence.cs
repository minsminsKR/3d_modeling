using System;

namespace HappyToy.V2
{
    // Interaction order only. Scene, presentation, input and saving belong to callers.
    public sealed class TemplePrologueSequence
    {
        // Values are persisted by callers; never reorder or reuse them.
        public enum Phase { FirstApproach = 0, FirstPrayer = 1, FirstDeparture = 2, SecondApproach = 3, SecondPrayer = 4, GateRevealed = 5, Complete = 6 }
        public Phase Current { get; private set; } = Phase.FirstApproach;
        public bool GateVisible => Current == Phase.GateRevealed || Current == Phase.Complete;
        public bool SmokePointsToGate => Current == Phase.SecondPrayer || GateVisible;

        // Validate before constructing so a damaged or newer save cannot silently
        // skip interactions or enter the corridor. Storage is owned by the caller.
        public static TemplePrologueSequence Restore(int savedPhase)
        {
            if (savedPhase < (int)Phase.FirstApproach || savedPhase > (int)Phase.Complete)
                throw new ArgumentOutOfRangeException(nameof(savedPhase));
            return new TemplePrologueSequence { Current = (Phase)savedPhase };
        }

        public bool PlaceIncense()
        {
            if (Current == Phase.FirstApproach) { Current = Phase.FirstPrayer; return true; }
            if (Current == Phase.SecondApproach) { Current = Phase.SecondPrayer; return true; }
            return false;
        }
        public bool FinishPrayer()
        {
            if (Current == Phase.FirstPrayer) { Current = Phase.FirstDeparture; return true; }
            if (Current == Phase.SecondPrayer) { Current = Phase.GateRevealed; return true; }
            return false;
        }
        public bool LeaveFirstDay()
        {
            if (Current != Phase.FirstDeparture) return false;
            Current = Phase.SecondApproach; return true;
        }
        public bool EnterGate()
        {
            if (Current != Phase.GateRevealed) return false;
            Current = Phase.Complete; return true;
        }
    }
}
