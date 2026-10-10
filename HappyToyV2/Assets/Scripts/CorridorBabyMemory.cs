using System;

namespace HappyToy.V2
{
    // Evidence transitions only. Navigation/hearing/real LOS are admitted by the
    // existing brain; this helper never samples the player's unseen position.
    public sealed class CorridorBabyMemory
    {
        public enum Phase { WaitingCry, InvestigatingCry, Chasing, WanderingCry }
        public Phase Current { get; private set; } = Phase.WaitingCry;
        public bool HasChased { get; private set; }
        public bool Waiting => Current == Phase.WaitingCry;
        public bool Wandering => Current == Phase.WanderingCry;
        public bool HearSmall()
        {
            if (Current == Phase.Chasing) return false;
            Current = Phase.InvestigatingCry; return true;
        }
        public void HearLoud() { HasChased = true; Current = Phase.Chasing; }
        public void SeePlayer() { HasChased = true; Current = Phase.Chasing; }
        public void LosePlayer()
        { if (Current == Phase.Chasing) Current = Phase.WanderingCry; }
        public void FinishInvestigation()
        { if (Current == Phase.InvestigatingCry) Current = HasChased ? Phase.WanderingCry : Phase.WaitingCry; }
        [Serializable] public sealed class Progress
        {
            public int version = 1;
            public Phase phase;
            public bool hasChased;
            // Unity's inline serializer replaces null Serializable references
            // with an empty default object. A separate owner's marker determines
            // whether this empty shape is actual Baby state or an absent field.
            public bool LegacyEmpty => (version == 0 || version == 1) && phase == Phase.WaitingCry && !hasChased;
            public void Validate()
            {
                if (version != 1 || !Enum.IsDefined(typeof(Phase), phase) ||
                    hasChased && phase == Phase.WaitingCry ||
                    !hasChased && (phase == Phase.Chasing || phase == Phase.WanderingCry))
                    throw new ArgumentException("Invalid corridor baby memory");
            }
        }
        public Progress Capture() => new Progress { phase = Current, hasChased = HasChased };
        public void Restore(Progress saved)
        {
            if (saved == null) { Current = Phase.WaitingCry; HasChased = false; return; }
            saved.Validate(); Current = saved.phase; HasChased = saved.hasChased;
        }
    }
}
