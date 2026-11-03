using System;

namespace HappyToy.V2
{
    // Interaction order only. Scene, presentation, input and saving belong to callers.
    public sealed class TemplePrologueSequence
    {
        public enum Phase { FirstApproach, FirstPrayer, FirstDeparture, SecondApproach, SecondPrayer, GateRevealed, Complete }
        public Phase Current { get; private set; } = Phase.FirstApproach;
        public bool GateVisible => Current == Phase.GateRevealed || Current == Phase.Complete;
        public bool SmokePointsToGate => Current == Phase.SecondPrayer || GateVisible;

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
