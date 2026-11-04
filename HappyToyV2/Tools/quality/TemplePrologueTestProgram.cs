using System;
using HappyToy.V2;

static class TemplePrologueTestProgram
{
    static void Check(bool condition) { if (!condition) throw new Exception("Unexpected prologue transition"); }
    static void Main()
    {
        var s = new TemplePrologueSequence();
        Check(!s.GateVisible && !s.SmokePointsToGate && !s.EnterGate() && !s.LeaveFirstDay());
        Check(s.PlaceIncense() && !s.PlaceIncense() && !s.EnterGate());
        Check(s.FinishPrayer() && !s.FinishPrayer() && !s.GateVisible);
        Check(s.LeaveFirstDay() && !s.LeaveFirstDay() && !s.GateVisible);
        Check(s.PlaceIncense() && s.SmokePointsToGate && !s.GateVisible && !s.EnterGate());
        Check(s.FinishPrayer() && s.GateVisible && s.EnterGate());
        Check(!s.EnterGate() && !s.PlaceIncense() && s.Current == TemplePrologueSequence.Phase.Complete);
        for (int phase = 0; phase <= 6; phase++)
        {
            var restored = TemplePrologueSequence.Restore(phase);
            Check((int)restored.Current == phase);
            Check(restored.GateVisible == (phase >= 5));
            Check(restored.SmokePointsToGate == (phase >= 4));
            Check(restored.EnterGate() == (phase == 5));
        }
        Check(TemplePrologueSequence.Restore(0).PlaceIncense());
        Check(TemplePrologueSequence.Restore(1).FinishPrayer());
        Check(TemplePrologueSequence.Restore(2).LeaveFirstDay());
        Check(TemplePrologueSequence.Restore(3).PlaceIncense());
        Check(TemplePrologueSequence.Restore(4).FinishPrayer());
        Check(!TemplePrologueSequence.Restore(6).EnterGate());
        foreach (int invalid in new[] { -1, 7, int.MinValue, int.MaxValue })
        {
            try { TemplePrologueSequence.Restore(invalid); throw new Exception("Invalid phase accepted"); }
            catch (ArgumentOutOfRangeException) { }
        }
        Console.WriteLine("Temple prologue sequence passed.");
    }
}
