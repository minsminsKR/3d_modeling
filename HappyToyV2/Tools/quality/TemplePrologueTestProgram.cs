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
        Console.WriteLine("Temple prologue sequence passed.");
    }
}
