using System;
using HappyToy.V2;

static class BabyMemoryTestProgram
{
    static int count;
    static void Check(bool value, string message)
    { count++; if (!value) throw new InvalidOperationException(message); }
    static int Main()
    {
        try
        {
            var baby = new CorridorBabyMemory();
            Check(baby.Waiting && !baby.HasChased, "A new Baby must stay and cry before evidence");
            Check(baby.HearSmall() && baby.Current == CorridorBabyMemory.Phase.InvestigatingCry, "Small heard sound must be investigated");
            baby.FinishInvestigation();
            Check(baby.Waiting && !baby.HasChased, "First small sound cannot invent a prior pursuit or start wandering");
            baby.SeePlayer();
            Check(baby.Current == CorridorBabyMemory.Phase.Chasing && baby.HasChased, "Real sight must begin chase");
            Check(!baby.HearSmall() && baby.Current == CorridorBabyMemory.Phase.Chasing, "Small distraction cannot erase established pursuit");
            baby.LosePlayer();
            Check(baby.Wandering && baby.HasChased, "After losing the player, Baby must wander and cry");
            Check(baby.HearSmall(), "Wandering Baby must still investigate nearby small sounds");
            var saved = baby.Capture();
            var resumed = new CorridorBabyMemory(); resumed.Restore(saved);
            Check(resumed.Current == CorridorBabyMemory.Phase.InvestigatingCry && resumed.HasChased, "Save lost post-chase investigation history");
            resumed.FinishInvestigation();
            Check(resumed.Wandering, "Resumed investigation must return to wandering rather than initial waiting");
            resumed.HearLoud();
            Check(resumed.Current == CorridorBabyMemory.Phase.Chasing, "A heard loud report must start pursuit");
            resumed.HearLoud(); resumed.LosePlayer();
            Check(resumed.Wandering, "Repeated loud reports cannot erase pursuit-loss behavior");
            resumed.Restore(null);
            Check(resumed.Waiting && !resumed.HasChased, "A fresh run inherited stale pursuit history");
            bool bad = false;
            try { resumed.Restore(new CorridorBabyMemory.Progress { phase = CorridorBabyMemory.Phase.WanderingCry }); }
            catch (ArgumentException) { bad = true; }
            Check(bad && resumed.Waiting, "Invalid wandering snapshot mutated valid state");
            bad = false;
            try { resumed.Restore(new CorridorBabyMemory.Progress { phase = (CorridorBabyMemory.Phase)99 }); }
            catch (ArgumentException) { bad = true; }
            Check(bad && resumed.Waiting, "Invalid enum snapshot must be rejected before mutation");
            bad = false;
            try { resumed.Restore(new CorridorBabyMemory.Progress { phase = CorridorBabyMemory.Phase.WaitingCry, hasChased = true }); }
            catch (ArgumentException) { bad = true; }
            Check(bad && resumed.Waiting && !resumed.HasChased, "Saved prior pursuit cannot revert to initial waiting");
            Console.WriteLine("CorridorBabyMemory: " + count + " actual C# assertions PASS; not Unity hearing/navigation/PCM proof.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
