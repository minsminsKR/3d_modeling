using System;

namespace HappyToy.V2
{
    // Narrative progression only. Scene input, text and persistence are owned
    // by callers. The existing prologue phase numbers remain unchanged.
    public sealed class TemplePrologueClueState
    {
        public const int FirstLegacyExemption = 1;
        public const int SecondLegacyExemption = 2;
        public bool FirstClueRead { get; private set; }
        public bool SecondClueRead { get; private set; }
        public int LegacyClueExemptMask { get; private set; }

        public TemplePrologueClueState(bool firstClueRead = false, bool secondClueRead = false, int legacyClueExemptMask = 0)
        {
            if (legacyClueExemptMask < 0 || legacyClueExemptMask > 3 ||
                firstClueRead && (legacyClueExemptMask & FirstLegacyExemption) != 0 ||
                secondClueRead && (legacyClueExemptMask & SecondLegacyExemption) != 0 ||
                legacyClueExemptMask == FirstLegacyExemption && !secondClueRead ||
                legacyClueExemptMask == SecondLegacyExemption && !firstClueRead ||
                secondClueRead && !firstClueRead && (legacyClueExemptMask & FirstLegacyExemption) == 0)
                throw new ArgumentException("Invalid temple clue state");
            FirstClueRead = firstClueRead;
            SecondClueRead = secondClueRead;
            LegacyClueExemptMask = legacyClueExemptMask;
        }

        public bool TryRead(TemplePrologueSequence.Phase phase)
        {
            // A resumed legacy run can inspect the note even after a prayer;
            // record that actual observation rather than keeping it unknown.
            if (phase >= TemplePrologueSequence.Phase.FirstApproach &&
                phase <= TemplePrologueSequence.Phase.FirstDeparture && !FirstClueRead)
            {
                FirstClueRead = true;
                LegacyClueExemptMask &= ~FirstLegacyExemption;
                return true;
            }
            if (phase >= TemplePrologueSequence.Phase.SecondApproach &&
                phase <= TemplePrologueSequence.Phase.GateRevealed && !SecondClueRead &&
                (FirstClueRead || (LegacyClueExemptMask & FirstLegacyExemption) != 0))
            {
                SecondClueRead = true;
                LegacyClueExemptMask &= ~SecondLegacyExemption;
                return true;
            }
            return false;
        }

        public bool CanPlaceIncense(TemplePrologueSequence.Phase phase)
        {
            bool firstSatisfied = FirstClueRead || (LegacyClueExemptMask & FirstLegacyExemption) != 0;
            bool secondSatisfied = SecondClueRead || (LegacyClueExemptMask & SecondLegacyExemption) != 0;
            if (phase == TemplePrologueSequence.Phase.FirstApproach) return firstSatisfied;
            if (phase == TemplePrologueSequence.Phase.SecondApproach) return firstSatisfied && secondSatisfied;
            return false;
        }
    }
}
