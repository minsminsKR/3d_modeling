using System;

namespace HappyToy.V2
{
    // One observed entry. No player/session, RNG, engine clock or navigation dependency.
    public sealed class CabinetEncounterClock
    {
        public enum Phase { None, Approach, Inspect, Rush, Strike, Recovery, Retreat, Complete }
        public enum Step { Continue, Warning, Impact, Finished }
        public const float InspectSeconds = 1.6f, WarningSeconds = .75f;
        public Phase State { get; private set; }
        public bool Active => State != Phase.None && State != Phase.Complete;
        public bool Aggressive => State == Phase.Rush || State == Phase.Strike;
        public float Elapsed { get; private set; }
        public int Entry { get; private set; }
        public bool Vulnerable { get; private set; }
        public float Windup => State == Phase.Strike ? (float)Math.Min(1, Elapsed / WarningSeconds) : 0;
        public float Recovery => State == Phase.Recovery ? (float)Math.Max(0, 1 - Elapsed / .9f) : 0;
        public void Begin(int entry, CabinetHidingOutcome outcome)
        {
            if (entry <= 0 || outcome == CabinetHidingOutcome.None) throw new ArgumentException("Observed entry required");
            Entry = entry; Vulnerable = outcome == CabinetHidingOutcome.Defeated; Set(Phase.Approach);
        }
        void Set(Phase phase) { State = phase; Elapsed = 0; }
        public void Reset() { Entry = 0; Vulnerable = false; Set(Phase.None); }
        public Step Tick(float seconds, bool sameHiddenEntry, bool protectedEntry, bool arrived, bool physicalImpact)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentException("Invalid elapsed seconds");
            if (!Active) return Step.Continue;
            if (!sameHiddenEntry) { Reset(); return Step.Finished; }
            if (seconds == 0) return Step.Continue;
            Elapsed += seconds;
            if (protectedEntry && Aggressive) { Set(Phase.Retreat); return Step.Continue; }
            switch (State)
            {
                case Phase.Approach:
                    if (arrived) Set(Phase.Inspect);
                    else if (Elapsed >= 12) Set(Phase.Retreat);
                    break;
                case Phase.Inspect:
                    if (Elapsed >= InspectSeconds) Set(Vulnerable && !protectedEntry ? Phase.Rush : Phase.Retreat);
                    break;
                case Phase.Rush:
                    if (arrived && physicalImpact) { Set(Phase.Strike); return Step.Warning; }
                    if (Elapsed >= 8) Set(Phase.Retreat);
                    break;
                case Phase.Strike:
                    if (Elapsed >= WarningSeconds)
                    {
                        if (physicalImpact && Vulnerable && !protectedEntry) { Set(Phase.Complete); return Step.Impact; }
                        Set(Phase.Recovery);
                    }
                    break;
                case Phase.Recovery:
                    if (Elapsed >= .9f) Set(Phase.Retreat);
                    break;
                case Phase.Retreat:
                    if (arrived || Elapsed >= 6) { Set(Phase.Complete); return Step.Finished; }
                    break;
            }
            return Step.Continue;
        }
    }
}
